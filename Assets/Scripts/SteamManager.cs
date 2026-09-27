using UnityEngine;
#if UNITY_STANDALONE || UNITY_EDITOR
using Steamworks;
#endif

/// <summary>
/// ★스팀 부트스트랩(2026-08-09): 게임 시작 시 SteamClient를 1회 초기화하고 매 프레임 콜백을 돌린다.
/// 스팀 미실행/미설치 환경에서는 경고만 남기고 조용히 넘어감 — 멀티는 NetSession이 Unity Relay로 폴백.
/// SteamClient의 수명은 이 클래스가 전담(FacepunchTransport 쪽 Shutdown은 패치로 제거됨).
/// 출시 시: AppId 상수를 실제 발급 AppID로 교체(+ 프로젝트 루트 steam_appid.txt도 같은 번호로).
/// </summary>
public class SteamManager : MonoBehaviour
{
    /// <summary>개발용 Spacewar AppID(480). 스팀 파트너 등록 후 실제 AppID로 교체.</summary>
    public const uint AppId = 480;

#if UNITY_STANDALONE || UNITY_EDITOR
    public static bool Initialized => SteamClient.IsValid;
    public static string PlayerName => SteamClient.IsValid ? SteamClient.Name : "";
    /// <summary>스팀 서버에 실제로 로그인돼 있는지(오프라인 모드/연결 끊김이면 false — 로비 생성 NoConnection의 원인).</summary>
    public static bool LoggedOn => SteamClient.IsValid && SteamClient.IsLoggedOn;

    static SteamManager instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("SteamManager");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SteamManager>();
    }

    /// <summary>스팀 초기화(이미 살아 있으면 true). 실패 = 스팀 미실행 등 — 폴백 판단용.</summary>
    public static bool TryInit()
    {
        if (SteamClient.IsValid){SteamLobby.InitializeInvites();return true;}
        try
        {
            SteamClient.Init(AppId, false);
            L.UseSteamDefault(SteamApps.GameLanguage);
            SteamLobby.InitializeInvites();
            Debug.Log("[Steam] 초기화 완료 — " + SteamClient.Name + " (" + SteamClient.SteamId.Value + ")");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Steam] 초기화 실패(스팀 클라이언트 미실행?): " + e.Message);
            return false;
        }
    }

    void Awake()
    {
        if (TryInit())
        {
            // 초대 수락으로 게임이 켜진 경우(+connect_lobby <로비ID>) — 로비 준비되면 자동 참가
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id))
                    SteamLobby.SetPendingInvite(id);
        }
    }

    void Update()
    {
        if (!SteamClient.IsValid) return;
        SteamClient.RunCallbacks();
        SteamLobby.ProcessPending();
    }

    void OnDestroy()
    {
        if (instance == this && SteamClient.IsValid) SteamClient.Shutdown();
    }

    void OnApplicationQuit()
    {
        if (SteamClient.IsValid) SteamClient.Shutdown();
    }
#else
    // WebGL 등 비지원 플랫폼 — 전부 비활성 스텁
    public static bool Initialized => false;
    public static string PlayerName => "";
    public static bool LoggedOn => false;
    public static bool TryInit() => false;
#endif
}
