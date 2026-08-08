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
/// 방코드 = 스팀 로비 번호(숫자 ~18자리). 스팀 오버레이 '친구 초대'와 친구 목록 '게임 참가'도 지원.
/// </summary>
public static class SteamLobby
{
#if UNITY_STANDALONE || UNITY_EDITOR
    static Lobby? current;
    static bool hooked;
    static ulong pendingInvite;   // +connect_lobby 런치 파라미터로 받은 로비 ID

    // 오프라인 모드/연결 끊김이면 로비 생성이 NoConnection으로 실패 — 그 경우 Unity Relay 경로로 폴백
    public static bool Available => SteamManager.TryInit() && SteamManager.LoggedOn;
    public static bool InLobby => current.HasValue;
    public static string JoinCode => current.HasValue ? current.Value.Id.Value.ToString() : "";
    public static string LastError { get; private set; } = "";

    /// <summary>초대 수락 콜백 연결(1회) — 게임 실행 중 오버레이/친구 목록에서 '게임 참가'.</summary>
    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SteamFriends.OnGameLobbyJoinRequested += (lobby, friendId) => { _ = JoinInvitedAsync(lobby); };
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

    /// <summary>스팀 로비 생성 + NGO 호스트 시작. 성공 시 JoinCode = 로비 번호.</summary>
    public static async Task<bool> HostAsync()
    {
        Hook();
        LastError = "";
        var nm = NetworkManager.Singleton;
        var tp = Transport();
        if (nm == null || tp == null) { LastError = "NetworkManager 없음"; return false; }

        var made = await SteamMatchmaking.CreateLobbyAsync(4);
        if (!made.HasValue) { LastError = "스팀 로비 생성 실패"; return false; }
        var lobby = made.Value;
        lobby.SetFriendsOnly();   // 친구 목록에서 '게임 참가' 가능 + 목록 비공개(방코드/초대로만)
        lobby.SetData("wat_host", SteamClient.SteamId.Value.ToString());
        lobby.SetJoinable(true);
        current = lobby;

        nm.NetworkConfig.NetworkTransport = tp;
        if (!nm.StartHost()) { Leave(); LastError = "NGO 호스트 시작 실패"; return false; }
        Debug.Log("[SteamLobby] 방 생성 — 로비 " + lobby.Id.Value);
        return true;
    }

    /// <summary>방코드(로비 번호)로 참가 + NGO 클라이언트 시작.</summary>
    public static async Task<bool> JoinByCodeAsync(string code)
    {
        Hook();
        LastError = "";
        if (!ulong.TryParse(code.Trim(), out ulong id)) { LastError = "잘못된 방코드"; return false; }
        var joined = await SteamMatchmaking.JoinLobbyAsync(id);
        if (!joined.HasValue) { LastError = "로비 참가 실패(코드 확인)"; return false; }
        return ConnectAsClient(joined.Value);
    }

    /// <summary>초대 수락(오버레이/친구 목록) — 로비 입장 후 클라이언트로 연결.</summary>
    static async Task JoinInvitedAsync(Lobby lobby)
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) return;   // 이미 세션 중이면 무시
        NetSession.SetStatus("초대 수락 — 참가 중...");
        var r = await lobby.Join();
        if (r != RoomEnter.Success) { NetSession.SetStatus("초대 참가 실패: " + r); return; }
        NetSession.SetStatus(ConnectAsClient(lobby) ? "참가됨" : "참가 실패: " + LastError);
    }

    static bool ConnectAsClient(Lobby lobby)
    {
        var nm = NetworkManager.Singleton;
        var tp = Transport();
        if (nm == null || tp == null) { LastError = "NetworkManager 없음"; return false; }

        ulong host = 0;
        ulong.TryParse(lobby.GetData("wat_host"), out host);
        if (host == 0) host = lobby.Owner.Id.Value;   // 데이터 누락 시 로비 소유자 = 호스트
        current = lobby;

        tp.targetSteamId = host;
        nm.NetworkConfig.NetworkTransport = tp;
        if (!nm.StartClient()) { Leave(); LastError = "NGO 클라이언트 시작 실패"; return false; }
        return true;
    }

    /// <summary>게임 시작 후 난입 차단(NetSession.LockAsync에서 호출).</summary>
    public static void Lock()
    {
        if (current.HasValue) current.Value.SetJoinable(false);
    }

    /// <summary>로비 나가기 + NGO 종료.</summary>
    public static void Leave()
    {
        if (current.HasValue) current.Value.Leave();
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
        if (pendingInvite == 0) return;
        var nm = NetworkManager.Singleton;
        if (nm == null) return;            // NetBoot가 아직 프리팹을 안 깔았음 — 다음 프레임
        ulong id = pendingInvite;
        pendingInvite = 0;
        if (nm.IsListening) return;
        NetSession.SetStatus("초대 수락 — 참가 중...");
        _ = JoinByCodeAsync(id.ToString()).ContinueWith(t =>
            NetSession.SetStatus(t.Status == TaskStatus.RanToCompletion && t.Result ? "참가됨" : "참가 실패: " + LastError),
            TaskScheduler.FromCurrentSynchronizationContext());
    }
#else
    // WebGL 등 비지원 플랫폼 — 전부 비활성 스텁(NetSession은 Unity Relay 경로만 사용)
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
