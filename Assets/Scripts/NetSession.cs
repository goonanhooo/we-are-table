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
    public static string Status { get; private set; } = "";
    public static ISession Session { get; private set; }
    public static string JoinCode => Session != null ? Session.Code : "";
    public static bool Busy { get; private set; }

    /// <summary>서비스 초기화 + 익명 로그인(1회). Steam 출시 때 이 로그인만 Steam 연동으로 교체.</summary>
    public static async Task<bool> EnsureSignedInAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            return true;
        }
        catch (System.Exception e)
        {
            Status = "로그인 실패: " + e.Message;
            Debug.LogError("[NetSession] " + Status);
            return false;
        }
    }

    /// <summary>방 생성(호스트). 성공 시 JoinCode에 방 코드. Relay 경유라 포트포워딩 불필요.</summary>
    public static async Task<bool> HostAsync()
    {
        if (Busy) return false;
        Busy = true;
        try
        {
            Status = "로그인 중...";
            if (!await EnsureSignedInAsync()) return false;
            Status = "방 생성 중...";
            var options = new SessionOptions { MaxPlayers = 4, IsPrivate = true, IsLocked = false }
                .WithRelayNetwork();   // Relay 할당 + NGO 호스트 자동 시작
            Session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Status = "방 생성됨 — 코드: " + Session.Code;
            Debug.Log("[NetSession] " + Status);
            return true;
        }
        catch (System.Exception e)
        {
            Status = "방 생성 실패: " + e.Message;
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
            Status = "로그인 중...";
            if (!await EnsureSignedInAsync()) return false;
            Status = "참가 중...";
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
            Status = "참가됨";
            Debug.Log("[NetSession] " + Status);
            return true;
        }
        catch (System.Exception e)
        {
            Status = "참가 실패: " + e.Message;
            Debug.LogError("[NetSession] " + Status);
            return false;
        }
        finally { Busy = false; }
    }

    /// <summary>세션 잠금(호스트 전용) — 게임 시작 후 방코드 참가 차단(중간 난입 방지).</summary>
    public static async Task LockAsync()
    {
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
        if (Session == null) return;
        try { await Session.LeaveAsync(); }
        catch (System.Exception e) { Debug.LogWarning("[NetSession] Leave: " + e.Message); }
        Session = null;
        Status = "세션 종료";
    }
}
