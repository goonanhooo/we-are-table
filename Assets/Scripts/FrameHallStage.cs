using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★2026-07-15 액자 맵(FrameHall) — 정글과 빙산 사이의 어두운 갤러리 홀.
/// 중앙 낮은 단상 위 화분에 스포트라이트, 정면 거대한 문 위에 '기린 테이블이 화분에 다리 하나로 선' 액자.
/// 퍼즐: 그림 자세를 흉내 — 화분(트리거)에 다리 하나를 꽂고 몸을 일정 높이로 들어올려 잠시 유지하면
/// 방 조명이 환하게 켜지고 문 두 짝이 열린다. 문 뒤 출구 트리거로 나가면 빙산(IceMountain).
/// 치트 ON + ` 키 = 즉시 해결(테스트용, GiraffeGate 패턴).
/// </summary>
public class FrameHallStage : MonoBehaviour
{
    [Header("퍼즐 — 그림 자세: 기린 모드 + 홈 속 다리만 길고 나머지는 제일 짧게")]
    [Tooltip("화분 홈 트리거(다리 감지)")] public Collider potTrigger;
    [Tooltip("홈 속 다리 최소 길이 배율(기본 길이 대비)")] public float longScale = 1.6f;
    [Tooltip("나머지 다리 최대 배율(제일 짧게 = 1.0 근처)")] public float shortScale = 1.15f;
    [Tooltip("자세 유지 시간(초)")] public float holdNeed = 1.0f;

    [Header("문(두 짝, 바깥쪽 경첩 피벗)")]
    public Transform doorL;
    public Transform doorR;
    public float doorOpenAngle = 100f;
    public float doorSeconds = 1.6f;

    [Header("조명 연출")]
    [Tooltip("성공 시 켜지는 홀 라이트들")] public Light[] hallLights;
    [Tooltip("성공 후 앰비언트 색")] public Color litAmbient = new Color(0.42f, 0.41f, 0.38f);
    public float lightSeconds = 1.2f;

    [Header("출구")]
    public string nextScene = "IceMountain";
    [Tooltip("문 뒤 출구 트리거")] public Collider exitTrigger;

    static readonly string[] LegNames = { "Leg_FR", "Leg_FL", "Leg_BR", "Leg_BL" };
    Transform board;
    GiraffeMode giraffe;
    readonly Transform[] legs = new Transform[4];
    readonly float[] baseScaleY = new float[4];
    float legInPotUntil;
    string potLegName;
    float holdTimer;
    bool solved, transitioned;
    Color darkAmbient;

    // ── 검증/디버그용 읽기 전용 상태(RunCommand 폴링) ──
    public bool LegInPotNow => Time.time < legInPotUntil;
    public string PotLegName => potLegName;
    public float HoldTimerNow => holdTimer;
    public bool Solved => solved;
    public float BoardY => board != null ? board.position.y : -999f;
    public string PoseDebug()
    {
        string s2 = "giraffe=" + (giraffe != null && giraffe.active) + " potLeg=" + potLegName + " inPot=" + LegInPotNow;
        for (int i = 0; i < 4; i++)
            if (legs[i] != null && baseScaleY[i] > 1e-4f)
                s2 += " " + LegNames[i] + "=" + (legs[i].localScale.y / baseScaleY[i]).ToString("F2");
        return s2;
    }

    void Start()
    {
        var b = GameObject.Find("Table");
        if (b != null) board = b.transform;
        giraffe = Object.FindAnyObjectByType<GiraffeMode>();
        for (int i = 0; i < 4; i++)
        {
            var g = GameObject.Find(LegNames[i]);
            if (g != null) { legs[i] = g.transform; baseScaleY[i] = g.transform.localScale.y; }
        }
        darkAmbient = RenderSettings.ambientLight;
        if (hallLights != null)
            foreach (var l in hallLights) if (l != null) l.enabled = false;
    }

    void Update()
    {
        // board 레이지 재탐색(스폰 타이밍에 따라 Start에서 못 잡는 경우 대비)
        if (board == null)
        {
            var b = GameObject.Find("Table");
            if (b != null) board = b.transform;
        }

        // [테스트] 치트 ON + ` = 즉시 해결
        if (!solved && CheatMode.Active)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame) Solve();
        }

        if (solved) return;

        // 그림 자세 판정: ① 기린 모드 ② 홈 안에 다리 하나 ③ 그 다리는 길게(longScale 이상)
        // ④ 나머지 세 다리는 제일 짧게(shortScale 이하) → 유지 시간 누적
        bool posing = false;
        if (giraffe != null && giraffe.active && Time.time < legInPotUntil && !string.IsNullOrEmpty(potLegName))
        {
            bool legOk = false, othersOk = true;
            for (int i = 0; i < 4; i++)
            {
                if (legs[i] == null || baseScaleY[i] < 1e-4f) continue;
                float k = legs[i].localScale.y / baseScaleY[i];
                if (LegNames[i] == potLegName) legOk = k >= longScale;
                else if (k > shortScale) othersOk = false;
            }
            posing = legOk && othersOk;
        }
        holdTimer = posing ? holdTimer + Time.deltaTime : 0f;
        if (holdTimer >= holdNeed) Solve();
    }

    /// <summary>화분 홈 트리거(PotTriggerRelay)가 다리 체류를 알림(어느 다리인지 포함).</summary>
    public void NotifyLegInPot(string legName)
    {
        legInPotUntil = Time.time + 0.25f;
        potLegName = legName;
    }

    public void Solve()
    {
        if (solved) return;
        solved = true;
        StartCoroutine(SolveCutscene());
    }

    IEnumerator SolveCutscene()
    {
        // 1) 방이 환해짐 — 홀 라이트 점등 + 앰비언트 상승
        if (hallLights != null)
            foreach (var l in hallLights) if (l != null) { l.enabled = true; l.intensity = 0f; }
        float targetI = 2.6f;
        for (float t = 0f; t < lightSeconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / lightSeconds);
            RenderSettings.ambientLight = Color.Lerp(darkAmbient, litAmbient, u);
            if (hallLights != null)
                foreach (var l in hallLights) if (l != null) l.intensity = targetI * u;
            yield return null;
        }
        RenderSettings.ambientLight = litAmbient;

        yield return new WaitForSeconds(0.3f);

        // 2) 문 두 짝이 경첩으로 열림
        Quaternion l0 = doorL != null ? doorL.localRotation : Quaternion.identity;
        Quaternion r0 = doorR != null ? doorR.localRotation : Quaternion.identity;
        Quaternion l1 = l0 * Quaternion.Euler(0f, -doorOpenAngle, 0f);
        Quaternion r1 = r0 * Quaternion.Euler(0f, doorOpenAngle, 0f);
        for (float t = 0f; t < doorSeconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / doorSeconds);
            if (doorL != null) doorL.localRotation = Quaternion.Slerp(l0, l1, u);
            if (doorR != null) doorR.localRotation = Quaternion.Slerp(r0, r1, u);
            yield return null;
        }
        if (doorL != null) doorL.localRotation = l1;
        if (doorR != null) doorR.localRotation = r1;
    }

    void FixedUpdate()
    {
        // 출구: 문이 열린 뒤 테이블이 출구 트리거 안으로 들어오면 빙산으로
        if (!solved || transitioned || board == null || exitTrigger == null) return;
        if (exitTrigger.bounds.Contains(board.position))
        {
            transitioned = true;
            Time.timeScale = 1f;
            ScreenFader.FadeOut(0.6f);
            ScreenFader.FadeInAfterNextLoad(0.4f, 1.4f);
            NetScene.Load(nextScene);
        }
    }
}

/// <summary>화분 트리거에 부착 — 다리 콜라이더 체류를 스테이지에 알린다.</summary>
public class PotTriggerRelay : MonoBehaviour
{
    public FrameHallStage stage;

    void OnTriggerStay(Collider other)
    {
        if (stage == null) return;
        // 다리 메시 이름("Leg_FR" 등)으로 정규화 — 콜라이더는 다리 메시, AB는 피벗("Leg_FR_Pivot")
        string n = other.transform.name;
        var ab = other.attachedArticulationBody;
        if (!n.StartsWith("Leg_") && ab != null)
            n = ab.name.Replace("_Pivot", "");
        if (!n.StartsWith("Leg_")) return;
        if (n.EndsWith("_Pivot")) n = n.Replace("_Pivot", "");
        stage.NotifyLegInPot(n);
    }
}
