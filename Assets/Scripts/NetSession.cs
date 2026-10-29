using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// ★ 멀티 3단계 기반: Unity Multiplayer Services(Sessions)로 '방 코드' 세션을 만들고 참가한다.
/// Sessions = Lobby+Relay 통합 SDK — 방 생성 시 Relay 할당·NGO(NetworkManager) 시작·방코드 발급을 한 번에 처리.
/// (프로젝트는 Unity Cloud에 연결돼 있어야 함 — cloudProjectId 확인됨.)
///
/// 사용: await NetSession.HostAsync() → NetSession.JoinCode를 친구에게 공유 →
///       친구는 await NetSession.JoinAsync(code). 로그인은 익명(Steam 전환 시 여기만 교체).
/// 설계 반영: MaxPlayers 4, IsPrivate(목록 비공개 — 방코드로만 입장), 호스트 퇴장=세션 종료.
/// </summary>
public static class NetSession
{
    static string status="";
    public static string Status { get=>L.Text(status); private set=>status=value; }
    public static ISession Session { get; private set; }
    public static string JoinCode => SteamLobby.InLobby ? SteamLobby.JoinCode : (Session != null ? Session.Code : "");
    public static bool Busy { get; private set; }

    /// <summary>스팀 초대 수락 등 외부 플로우가 상태 문구를 갱신할 때(SteamLobby 전용).</summary>
    internal static void SetStatus(string s) { Status = s; }

    /// <summary>Unity Relay 경로 진입 전 트랜스포트를 UnityTransport로 복원(스팀 세션 후 재사용 대비).</summary>
    static void EnsureUnityTransport()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null) return;
        var utp = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (utp != null) nm.NetworkConfig.NetworkTransport = utp;
    }

    // ★2026-08-20 출시 안전망: 서비스 호출이 무한 대기하지 않게 타임아웃(과거 InitializeAsync 무한 대기 사례)
    // ★2026-10-29 핫스팟처럼 느린 연결에서 방이 안 만들어지던 문제: SDK는 요청 하나에 10초를 기다렸다 다시 시도하는데
    //   로그인 제한이 20초라 끝나기 전에 실패 처리됨(바로 다음 시도는 성공). → 게임을 켤 때 미리 로그인 + 진행 중인 로그인 이어받기 + 제한 늘림.
    static string TimeoutMessage => L.T("연결 시간이 초과됐어요. 인터넷 연결을 확인하고 다시 시도해 주세요.",
        "Connection timed out. Check your internet connection and try again.",
        "接続がタイムアウトしました。インターネット接続を確認して、もう一度お試しください。",
        "连接超时。请检查网络连接后重试。");
    static async Task<T> WithTimeout<T>(Task<T> t, float seconds, string what)
    {
        var done = await Task.WhenAny(t, Task.Delay((int)(seconds * 1000f)));
        if (done != t) throw new System.TimeoutException(TimeoutMessage);
        return await t;
    }
    static async Task WithTimeout(Task t, float seconds, string what)
    {
        var done = await Task.WhenAny(t, Task.Delay((int)(seconds * 1000f)));
        if (done != t) throw new System.TimeoutException(TimeoutMessage);
        await t;
    }
    /// <summary>방 만들기/참가가 시간 초과된 뒤 늦게 성공하면 그 세션은 바로 떠난다(아무도 모르는 방이 남지 않게).</summary>
    static async Task<T> SessionWithTimeout<T>(Task<T> t, float seconds, string what) where T : class, ISession
    {
        var done = await Task.WhenAny(t, Task.Delay((int)(seconds * 1000f)));
        if (done != t) { _ = LeaveWhenLate(t); throw new System.TimeoutException(TimeoutMessage); }
        return await t;
    }
    static async Task LeaveWhenLate<T>(Task<T> t) where T : class, ISession
    {
        try
        {
            var late = await t;
            if (late != null && late != Session) { await late.LeaveAsync(); Debug.LogWarning("[NetSession] 시간 초과 뒤 늦게 열린 세션을 정리함"); }
        }
        catch { }
    }

    // 진행 중인 초기화·로그인 — 시간이 초과돼도 버리지 않고 다음 시도가 이어받는다(같은 요청을 두 번 보내지 않음)
    static Task initTask, signInTask;

    /// <summary>서비스 초기화 + 익명 로그인(1회). Steam 출시 때 이 로그인만 Steam 연동으로 교체.</summary>
    /// <param name="quiet">게임 시작 때 미리 로그인해 둘 때 — 실패해도 상태 문구·에러 로그를 남기지 않음.</param>
    public static async Task<bool> EnsureSignedInAsync(bool quiet = false)
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                if (initTask == null || initTask.IsFaulted || initTask.IsCanceled) initTask = UnityServices.InitializeAsync();
                await WithTimeout(initTask, 30f, "서비스 초기화");
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                if (signInTask == null || signInTask.IsCompleted) signInTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
                await WithTimeout(signInTask, 45f, "로그인");
                if (quiet) Debug.Log("[NetSession] 미리 로그인 완료");
            }
            return true;
        }
        catch (System.Exception e)
        {
            if (quiet) { Debug.Log("[NetSession] 미리 로그인 실패(방 만들 때 다시 시도): " + e.Message); return false; }
            Status = L.T("로그인 실패: ", "sign-in failed: ") + e.Message;
            Debug.LogError("[NetSession] " + Status);
            return false;
        }
    }

    // 빌드에서 게임을 켜면 바로 로그인해 둔다 — '방 만들기'를 누를 때 기다릴 일이 없게(에디터 플레이는 그대로)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Prewarm()
    {
        initTask = null; signInTask = null;
        if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer) return;
        _ = EnsureSignedInAsync(true);
    }

    /// <summary>방 생성(호스트). 성공 시 JoinCode에 방 코드. Relay 경유라 포트포워딩 불필요.</summary>
    public static async Task<bool> HostAsync()
    {
        if (Busy) return false;
        Busy = true;
        try
        {
            // ★스팀 경로(2026-08-09): 스팀 클라이언트가 살아 있으면 스팀 로비 + SDR 릴레이(방코드 = 영문을 포함한 5자리)
            if (SteamLobby.Available)
            {
                Status = L.T("스팀 방 생성 중...", "creating steam room...");
                bool sOk = await SteamLobby.HostAsync();
                Status = sOk ? L.T("방 생성됨 — 코드: ", "room created — code: ") + SteamLobby.JoinCode : L.T("스팀 방 생성 실패: ", "steam room failed: ") + SteamLobby.LastError;
                Debug.Log("[NetSession] " + Status);
                return sOk;
            }

            EnsureUnityTransport();
            Status = L.T("로그인 중...", "signing in...");
            if (!await EnsureSignedInAsync()) return false;
            Status = L.T("방 생성 중...", "creating room...");
            var options = new SessionOptions { MaxPlayers = 4, IsPrivate = true, IsLocked = false }
                .WithRelayNetwork();   // Relay 할당 + NGO 호스트 자동 시작
            Session = await SessionWithTimeout(MultiplayerService.Instance.CreateSessionAsync(options), 40f, "방 생성");
            Status = L.T("방 생성됨 — 코드: ", "room created — code: ") + Session.Code;
            Debug.Log("[NetSession] " + Status);
            return true;
        }
        catch (System.Exception e)
        {
            Status = L.T("방 생성 실패: ", "room creation failed: ") + e.Message;
            Debug.LogError("[NetSession] " + Status);
            return false;
        }
        finally { Busy = false; }
    }

    /// <summary>방 코드로 참가(게스트). Relay 연결 + NGO 클라이언트 자동 시작.</summary>
    public static async Task<bool> JoinAsync(string code)
    {
        if (Busy || string.IsNullOrWhiteSpace(code)) return false;
        Busy = true;
        try
        {
            // Five-character codes resolve through Steam. Preserve legacy numeric IDs and Relay codes.
            string trimmed = RoomCodeInput.Normalize(code);
            if (!RoomCodeInput.IsValid(trimmed)) { Status = L.T("잘못된 방코드", "invalid room code", "ルームコードが正しくありません", "房间代码无效"); return false; }
            if (RoomCodeInput.IsSteamCode(trimmed) || RoomCodeInput.IsLegacySteamCode(trimmed))
            {
                if (!SteamLobby.Available) { Status = L.T("스팀 방코드 — 스팀 실행 필요", "steam room code — steam must be running"); return false; }
                Status = L.T("스팀 로비 참가 중...", "joining steam lobby...");
                bool sOk = await SteamLobby.JoinByCodeAsync(trimmed);
                Status = sOk ? L.T("참가됨", "joined") : L.T("참가 실패: ", "join failed: ") + SteamLobby.LastError;
                Debug.Log("[NetSession] " + Status);
                return sOk;
            }

            EnsureUnityTransport();
            Status = L.T("로그인 중...", "signing in...");
            if (!await EnsureSignedInAsync()) return false;
            Status = L.T("참가 중...", "joining...");
            Session = await SessionWithTimeout(MultiplayerService.Instance.JoinSessionByCodeAsync(trimmed), 40f, "참가");
            Status = L.T("참가됨", "joined");
            Debug.Log("[NetSession] " + Status);
            return true;
        }
        catch (System.Exception e)
        {
            Status = L.T("참가 실패: ", "join failed: ") + e.Message;
            Debug.LogError("[NetSession] " + Status);
            return false;
        }
        finally { Busy = false; }
    }

    /// <summary>Join the selected Steam friend's actual lobby, without asking for a room code.</summary>
    public static async Task<bool> JoinFriendAsync(ulong lobbyId, ulong hostId)
    {
        var nm=Unity.Netcode.NetworkManager.Singleton;
        if(Busy || (nm!=null && nm.IsListening))return false;
        Busy=true;
        try
        {
            if(!SteamLobby.Available){Status=L.T("Steam에 로그인해 주세요.","Please sign in to Steam.","Steamにログインしてください。","请登录 Steam。");return false;}
            Status=L.T("친구의 방에 들어가는 중","Joining your friend's room","フレンドのルームに参加中","正在加入好友的房间");
            NetLegLink.ResetSessionCache();
            bool ok=await SteamLobby.JoinFriendRoomAsync(lobbyId,hostId);
            Status=ok?L.T("참가됨","joined"):SteamLobby.LastError;
            return ok;
        }
        catch(System.Exception e)
        {
            if(SteamLobby.InLobby)SteamLobby.Leave();
            Status=SteamLobby.FriendRoomGone;
            Debug.LogWarning("[NetSession] Friend join failed: "+e.Message);
            return false;
        }
        finally{Busy=false;}
    }

    /// <summary>세션 잠금(호스트 전용) — 게임 시작 후 방코드 참가 차단(중간 난입 방지).</summary>
    public static async Task LockAsync()
    {
        if (SteamLobby.InLobby) { SteamLobby.Lock(); Debug.Log("[NetSession] 스팀 로비 잠금(난입 차단)"); return; }
        if (Session == null) return;
        try
        {
            var host = Session.AsHost();
            host.IsLocked = true;
            await host.SavePropertiesAsync();
            Debug.Log("[NetSession] 세션 잠금(난입 차단)");
        }
        catch (System.Exception e) { Debug.LogWarning("[NetSession] 잠금 실패: " + e.Message); }
    }

    /// <summary>세션 떠나기(호스트가 떠나면 세션 종료 → 게스트들은 연결 해제됨 = 설계대로).</summary>
    public static async Task LeaveAsync()
    {
        if (SteamLobby.InLobby) { SteamLobby.Leave(); Status = L.T("세션 종료", "session ended"); }
        if (Session == null) return;
        try { await Session.LeaveAsync(); }
        catch (System.Exception e) { Debug.LogWarning("[NetSession] Leave: " + e.Message); }
        Session = null;
        Status = L.T("세션 종료", "session ended");
    }
}
