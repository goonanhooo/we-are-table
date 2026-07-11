using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ★ 멀티 축구 스테이지(Soccer.unity) — 1인 1테이블(PerTableMode), 1v1/2v2.
/// 규칙(자체 설계):
///  - 팀 = 슬롯 짝수(0,2)=파랑 / 홀수(1,3)=빨강. 접속 순서 자동 배정(1v1=슬롯0 vs 1, 2v2=0·2 vs 1·3).
///  - 경기 3분. 상단 중앙 "파랑 n : n 빨강 + 남은 시간" 표시.
///  - 골 → 3초 GOAL 배너 → 공 중앙 + 전원 킥오프 위치 재생성 → 재개. 종료 → 승자 배너 10초 → 0:0 재경기.
///  - ★'1' 수동 리셋 금지(ArtSceneReset.disableManualReset) — 재배치는 골/킥오프로만(워치독 복구는 유지).
/// 골 판정: SoccerGoalZone(goalTeam=그 골대를 지키는 팀) 트리거에 '공'이 들어오면 상대 팀 득점(서버만).
/// 시계: 호스트가 초 단위로만 NetworkVariable 갱신(브로드캐스트 절약).
/// </summary>
public class SoccerStage : NetworkBehaviour
{
    public PerTableMode mode;
    public Rigidbody ball;
    public Transform ballSpawn;
    [Tooltip("슬롯별 킥오프 지점(0,2=파랑 진영 / 1,3=빨강 진영)")]
    public Transform[] kickoff = new Transform[4];
    public float matchLength = 180f;
    public float goalPause = 3f;
    public float endPause = 10f;

    static readonly Color Blue = new Color(0.25f, 0.5f, 1f);
    static readonly Color Red = new Color(1f, 0.3f, 0.25f);

    readonly NetworkVariable<byte> blueScore = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<byte> redScore = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<short> timeLeft = new(180, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<byte> phase = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);  // 0=경기 1=골 연출 2=종료
    readonly NetworkVariable<byte> lastScorer = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); // 0=파랑 1=빨강

    float clockLocal;
    double resumeAt = -1;
    bool tinted, resetApplied;

    double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            clockLocal = matchLength;
            timeLeft.Value = (short)Mathf.CeilToInt(matchLength);
        }
    }

    void Start()
    {
        // '1' 수동 리셋 금지(호스트/게스트 공통 — 게스트는 어차피 비활성)
        foreach (var asr in FindObjectsByType<ArtSceneReset>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            asr.disableManualReset = true;
    }

    /// <summary>SoccerGoalZone이 전달: goalTeam 골대(0=파랑, 1=빨강)에 공이 들어옴 → 상대 득점.</summary>
    public void OnGoal(int goalTeam, Collider other)
    {
        if (!IsServer || !IsSpawned || phase.Value != 0) return;
        if (ball == null || other.attachedRigidbody != ball) return;
        if (goalTeam == 0) { redScore.Value++; lastScorer.Value = 1; }
        else { blueScore.Value++; lastScorer.Value = 0; }
        phase.Value = 1;
        resumeAt = Now + goalPause;
    }

    void Update()
    {
        // 팀 색 비콘(파/빨) — PerTableMode 비콘 생성 후 1회
        if (!tinted && mode != null && mode.MySlot >= 0 && Time.frameCount > 30)
        {
            tinted = true;
            for (int i = 0; i < 4; i++)
                if (mode.SlotUsed(i)) mode.TintBeacon(i, i % 2 == 0 ? Blue : Red);
        }

        if (!IsServer || !IsSpawned) return;

        if (phase.Value == 0)
        {
            clockLocal -= Time.deltaTime;
            short t = (short)Mathf.Max(0, Mathf.CeilToInt(clockLocal));
            if (t != timeLeft.Value) timeLeft.Value = t;
            if (clockLocal <= 0f)
            {
                phase.Value = 2;
                resumeAt = Now + endPause;
            }
        }
        else if (resumeAt > 0 && Now >= resumeAt)
        {
            if (phase.Value == 2) { blueScore.Value = 0; redScore.Value = 0; clockLocal = matchLength; timeLeft.Value = (short)matchLength; }
            ResetPositions();
            phase.Value = 0;
            resumeAt = -1;
        }
    }

    void ResetPositions()
    {
        if (ball != null && ballSpawn != null)
        {
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.position = ballSpawn.position;
        }
        for (int i = 0; i < 4; i++)
        {
            if (mode == null || !mode.SlotUsed(i) || kickoff[i] == null || mode.tables[i] == null) continue;
            var asr = mode.tables[i].GetComponentInChildren<ArtSceneReset>(true);
            if (asr != null) asr.ResetPoseAtXZ(kickoff[i].position.x, kickoff[i].position.z, 30f);
        }
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (!IsSpawned) return;
        // 스코어보드
        var r = UiKit.Chip(Screen.width * 0.5f, 12f, 340f, 40f);
        int t = timeLeft.Value;
        GUI.Label(r, "파랑  " + blueScore.Value + "  :  " + redScore.Value + "  빨강     " + (t / 60) + ":" + (t % 60).ToString("00"), UiKit.Btn);

        if (phase.Value == 1)
        {
            var g = UiKit.Chip(Screen.width * 0.5f, Screen.height * 0.28f, 380f, 76f);
            GUI.Label(g, "GOAL!  " + (lastScorer.Value == 0 ? "파랑" : "빨강") + " 득점!", UiKit.Title);
        }
        else if (phase.Value == 2)
        {
            string msg = blueScore.Value == redScore.Value ? "무승부!"
                : (blueScore.Value > redScore.Value ? "파랑 승리!" : "빨강 승리!");
            var g = UiKit.Chip(Screen.width * 0.5f, Screen.height * 0.28f, 420f, 76f);
            GUI.Label(g, "경기 종료 — " + msg + "  (곧 재경기)", UiKit.Title);
        }
        // 내 팀 표시
        int my = mode != null ? mode.MySlot : -1;
        if (my >= 0)
        {
            var mr = UiKit.Chip(Screen.width * 0.5f, 56f, 150f, 28f);
            GUI.Label(mr, "내 팀: " + (my % 2 == 0 ? "파랑" : "빨강"), UiKit.Small);
        }
    }
}
