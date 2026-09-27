using System.Threading.Tasks;
using UnityEngine;
#if UNITY_STANDALONE || UNITY_EDITOR
using Steamworks;
using Steamworks.Data;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
#endif

/// <summary>
/// ★스팀 멀티 경로(2026-08-09): SteamMatchmaking 로비 + FacepunchTransport(SteamNetworkingSockets, 무료 SDR 릴레이).
/// NetSession이 스팀 가용 여부에 따라 이 경로/Unity Relay 경로를 고른다(공개 API는 NetSession 그대로).
/// 방코드 = 영문을 포함한 5자리 코드. 스팀 오버레이 '친구 초대'와 친구 목록 '게임 참가'도 지원.
/// </summary>
public static partial class SteamLobby
{
#if UNITY_STANDALONE || UNITY_EDITOR
    static Lobby? current;
    static bool hooked, joiningInvite;
    const string Protocol="wat-story-checkpoints-v2";
    static ulong pendingInvite;   // +connect_lobby 런치 파라미터로 받은 로비 ID

    // Steam에 로그인돼 있지 않으면 NetSession이 Unity Relay 경로를 사용한다.
    public static bool Available => SteamManager.TryInit() && SteamManager.LoggedOn;
    public static bool InLobby => current.HasValue;
    public static string JoinCode => current.HasValue ? current.Value.GetData(CodeKey) : "";
    static string lastError="";
    public static string LastError { get=>L.Text(lastError); private set=>lastError=value; }

    /// <summary>초대 수락 콜백 연결(1회) — 게임 실행 중 오버레이/친구 목록에서 '게임 참가'.</summary>
    public static void InitializeInvites(){if(SteamClient.IsValid)Hook();}
    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SteamFriends.OnGameLobbyJoinRequested += (lobby, friendId) => { _ = JoinInvitedAsync(lobby.Id.Value); };
        // 실패 원인 진단용 — 스팀이 준 실제 결과 코드(OK/NoConnection/LimitedUserAccount 등)
        SteamMatchmaking.OnLobbyCreated += (r, lobby) => Debug.Log("[SteamLobby] OnLobbyCreated result=" + r + " id=" + lobby.Id.Value);
    }

    /// <summary>NetworkManager에서 FacepunchTransport 확보(없으면 런타임 부착) + AppID 동기화.</summary>
    static FacepunchTransport Transport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;
        var tp = nm.GetComponent<FacepunchTransport>();
        if (tp == null)
        {
            tp = nm.gameObject.AddComponent<FacepunchTransport>();
            // steamAppId는 [SerializeField] private — SteamManager.AppId 한 곳만 고치면 되게 리플렉션으로 동기화
            var f = typeof(FacepunchTransport).GetField("steamAppId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(tp, SteamManager.AppId);
        }
        return tp;
    }

    /// <summary>스팀 로비 생성 + NGO 호스트 시작. 성공 시 JoinCode = 영문을 포함한 5자리 코드.</summary>
    public static async Task<bool> HostAsync()
    {
        Hook();
        LastError = "";
        var nm = NetworkManager.Singleton;
        var tp = Transport();
        if (nm == null || tp == null) { LastError = L.T("NetworkManager 없음", "no NetworkManager"); return false; }

        Debug.Log("[SteamLobby] IsSubscribed(앱 라이선스)=" + SteamApps.IsSubscribed + " LoggedOn=" + SteamClient.IsLoggedOn);
        var made = await WaitLobby(SteamMatchmaking.CreateLobbyAsync(4));
        if (!made.HasValue) { LastError = L.T("스팀 로비 생성 실패", "steam lobby creation failed") + (SteamApps.IsSubscribed ? "" : L.T(" (앱 라이선스 없음)", " (no app license)")); return false; }
        var lobby = made.Value;
        current = lobby;
        try
        {
            lobby.SetJoinable(false);
            if (!lobby.SetData("wat_host", SteamClient.SteamId.Value.ToString()) ||
                !lobby.SetData("wat_protocol", Protocol) || !lobby.SetData(GameKey, GameValue) ||
                !lobby.SetData(RoomStateKey, "preparing"))
                throw new System.InvalidOperationException(CodeServiceError);
            nm.NetworkConfig.NetworkTransport = tp;
            if (!nm.StartHost()) { Leave(); LastError = L.T("호스트 시작 실패", "failed to start host"); return false; }
            await PublishCodeAsync(lobby);
            if (!lobby.SetData(RoomStateKey, "open")) throw new System.InvalidOperationException(CodeServiceError);
            Debug.Log("[SteamLobby] room ready — code " + JoinCode);
            return true;
        }
        catch { Leave(); throw; }
    }

    /// <summary>5자리 코드를 Steam 로비 ID로 조회한 뒤 참가한다. 기존 숫자 코드도 수용한다.</summary>
    public static async Task<bool> JoinByCodeAsync(string code)
    {
        Hook();
        LastError = "";
        code = RoomCodeInput.Normalize(code);
        if (RoomCodeInput.IsLegacySteamCode(code)) return await JoinByIdAsync(ulong.Parse(code));
        if (!RoomCodeInput.IsSteamCode(code)) { LastError = InvalidCodeError; return false; }
        var found = await ResolveCodeAsync(code);
        if (!found.HasValue) return false;
        return await JoinByIdAsync(found.Value.Id.Value, code);
    }

    static async Task<bool> JoinByIdAsync(ulong id, string expectedCode = null, ulong expectedHost = 0)
    {
        var joined = await WaitLobby(SteamMatchmaking.JoinLobbyAsync(id));
        if (!joined.HasValue) { LastError = MissingCodeError; return false; }
        // Metadata can change between searching and joining. Never connect to a different code.
        if (expectedCode != null && (joined.Value.GetData(CodeKey) != expectedCode || joined.Value.GetData(GameKey) != GameValue))
        { joined.Value.Leave(); LastError = MissingCodeError; return false; }
        if (expectedHost != 0 && (joined.Value.Owner.Id.Value != expectedHost || joined.Value.GetData("wat_host") != expectedHost.ToString()))
        { joined.Value.Leave(); LastError = FriendRoomGone; return false; }
        return await ConnectAsClient(joined.Value);
    }

    /// <summary>초대 수락(오버레이/친구 목록) — 로비 입장 후 클라이언트로 연결.</summary>
    static async Task JoinInvitedAsync(ulong lobbyId)
    {
        var nm=NetworkManager.Singleton;
        if(joiningInvite||NetSession.Busy||(nm!=null&&nm.IsListening))return;
        joiningInvite=true;
        try
        {
            NetSession.SetStatus(L.T("초대 수락 — 참가 중...","invite accepted — joining..."));
            LastError="";
            bool ok=await JoinByIdAsync(lobbyId);
            NetSession.SetStatus(ok?L.T("참가됨","joined"):L.T("참가 실패: ","join failed: ")+LastError);
            if(ok){var menu=Object.FindAnyObjectByType<LevelSelect>();if(menu!=null)menu.Close();}
        }
        catch(System.Exception e){LastError=e.Message;NetSession.SetStatus(L.T("참가 실패: ","join failed: ")+LastError);}
        finally{joiningInvite=false;}
    }
    static async Task<Lobby?> WaitLobby(Task<Lobby?> pending)
    {
        var done=await Task.WhenAny(pending,Task.Delay(15000));
        if(done==pending)return await pending;
        _=LeaveLate(pending);
        throw new System.TimeoutException(L.T("연결 시간이 초과됐어요. 방 코드를 확인하고 다시 시도해 주세요.","Connection timed out. Check the room code and try again."));
    }
    static async Task LeaveLate(Task<Lobby?> pending)
    {
        try{var result=await pending;if(result.HasValue)result.Value.Leave();}catch(System.Exception e){Debug.LogWarning("[SteamLobby] late response: "+e.Message);}
    }

    static async Task<bool> ConnectAsClient(Lobby lobby)
    {
        var nm = NetworkManager.Singleton;
        var tp = Transport();
        if (nm == null || tp == null) { lobby.Leave(); LastError = L.T("NetworkManager 없음", "no NetworkManager"); return false; }

        if(lobby.GetData("wat_protocol")!=Protocol)
        {lobby.Leave();LastError=L.T("게임 버전이 달라요. 모두 같은 버전으로 업데이트해 주세요.","Game versions differ. Everyone must update to the same version.");return false;}
        ulong host = 0;
        ulong.TryParse(lobby.GetData("wat_host"), out host);
        if (host == 0) host = lobby.Owner.Id.Value;   // 데이터 누락 시 로비 소유자 = 호스트
        current = lobby;

        tp.targetSteamId = host;
        nm.NetworkConfig.NetworkTransport = tp;
        var connected=new TaskCompletionSource<bool>();
        void OnConnected(ulong id){if(id==nm.LocalClientId)connected.TrySetResult(true);}
        void OnDisconnected(ulong id){if(id==nm.LocalClientId)connected.TrySetResult(false);}
        nm.OnClientConnectedCallback+=OnConnected;nm.OnClientDisconnectCallback+=OnDisconnected;
        try
        {
            if(!nm.StartClient()){Leave();LastError=L.T("클라이언트 시작 실패","failed to start client");return false;}
            var done=await Task.WhenAny(connected.Task,Task.Delay(20000));
            bool ok=done==connected.Task&&await connected.Task;
            if(!ok){LastError=L.T("연결 시간이 초과됐어요. 방 코드를 확인하고 다시 시도해 주세요.","Connection timed out. Check the room code and try again.");Leave();}
            return ok;
        }
        finally{nm.OnClientConnectedCallback-=OnConnected;nm.OnClientDisconnectCallback-=OnDisconnected;}
    }

    /// <summary>게임 시작 후 난입 차단(NetSession.LockAsync에서 호출).</summary>
    public static void Lock()
    {
        if (current.HasValue) { current.Value.SetData(RoomStateKey, "playing"); current.Value.SetJoinable(false); }
    }

    /// <summary>로비 나가기 + NGO 종료.</summary>
    public static void Leave()
    {
        if (current.HasValue)
        {
            if (current.Value.Owner.Id.Value == SteamClient.SteamId.Value)
            { current.Value.SetData(RoomStateKey, "closed"); current.Value.SetJoinable(false); }
            current.Value.Leave();
        }
        current = null;
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) nm.Shutdown();
    }

    /// <summary>스팀 오버레이 '친구 초대' 열기(로비에 있을 때만).</summary>
    public static void OpenInviteOverlay()
    {
        if (current.HasValue) SteamFriends.OpenGameInviteOverlay(current.Value.Id);
    }

    public static void SetPendingInvite(ulong lobbyId) { pendingInvite = lobbyId; }

    /// <summary>+connect_lobby로 켜진 경우 — NetworkManager가 준비되면 자동 참가(SteamManager.Update가 호출).</summary>
    public static void ProcessPending()
    {
        Hook();
        if (pendingInvite == 0 || NetSession.Busy || joiningInvite) return;
        var nm = NetworkManager.Singleton;
        if (nm == null) return;            // NetBoot가 아직 프리팹을 안 깔았음 — 다음 프레임
        ulong id = pendingInvite;
        pendingInvite = 0;
        if (nm.IsListening) return;
        _ = JoinInvitedAsync(id);
    }
#else
    // WebGL 등 비지원 플랫폼 — 전부 비활성 스텁(NetSession은 Unity Relay 경로만 사용)
    public static void InitializeInvites(){}
    public static bool Available => false;
    public static bool InLobby => false;
    public static string JoinCode => "";
    public static string LastError => "";
    public static Task<bool> HostAsync() => Task.FromResult(false);
    public static Task<bool> JoinByCodeAsync(string code) => Task.FromResult(false);
    public static void Lock() { }
    public static void Leave() { }
    public static void OpenInviteOverlay() { }
    public static void SetPendingInvite(ulong lobbyId) { }
    public static void ProcessPending() { }
#endif
}
