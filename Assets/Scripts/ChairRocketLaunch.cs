using UnityEngine;

/// <summary>
/// ★2026-09-18 왼쪽 발사대의 '의자 로켓' — 컷씬 없이 자동 발사(사용자 요청: "슬라이드 끝을 지날 때쯤 왼쪽 로켓이 발사, 오른쪽 로켓 이펙트와 동일, 우주 말풍선처럼 의자 표시").
/// 테이블이 triggerZ(슬라이드 킥커 부근)를 지나면 점화(불꽃·연기·플룸·불똥 = RocketLaunchCutscene과 같은 오브젝트 복제) → 흔들림 → 가속 상승 → 시야 밖(vanishY)에서 정리.
/// 카메라/테이블은 건드리지 않음(플레이어는 그대로 활강·착지). 날아가는 로켓 위에 우주 도입 컷씬과 같은 동그란 손그림 말풍선 + 의자 아이콘(거리·FOV 비례 축소, 작아지면 페이드).
/// 멀티: 테이블 위치는 NetworkTransform으로 전원 동일 → 각자 로컬 판정/재생(홀웨이 트랩과 같은 방식).
/// </summary>
public class ChairRocketLaunch : MonoBehaviour
{
    [Header("씬 참조(RocketLaunch의 것을 복제해 왼쪽 발사대에 배치)")]
    public Transform rocket;
    public Transform flame;
    public Light flameLight;
    public ParticleSystem smoke, plume, sparks;

    [Header("발동")]
    [Tooltip("테이블 z가 이 값을 넘으면 발사(슬라이드 킥커 팁 z≈145 직전)")] public float triggerZ = 125f;
    [Tooltip("점화 흔들림(초) — RocketLaunchCutscene과 동일값 권장")] public float shakeSeconds = 1.6f;
    [Tooltip("상승 가속도(m/s²)")] public float liftAccel = 26f;
    [Tooltip("이륙 킥(m/s)")] public float liftKick = 5f;
    [Tooltip("이 높이를 넘으면 로켓·이펙트 정리")] public float vanishY = 420f;
    [Tooltip("살짝 앞(+z)으로 기울며 멀어지는 가속(m/s²) — 0이면 수직")] public float driftAccel = 1.5f;

    [Header("말풍선(우주 도입 컷씬과 같은 손그림 말풍선 — 꼬리가 옆으로 난 판)")]
    [Tooltip("BubbleRoundSide.png(640×512): BubbleRound를 90° 돌려 꼬리가 오른쪽을 가리킴 — 꼬리 끝 (606,249), 원 중심 (262,255) r226")] public Texture2D bubbleTex;
    public Texture2D chairIcon;
    [Tooltip("말풍선 크기(원 기준, 화면 높이 비율) — 기준 거리에서")] public float bubbleSize = 0.17f;
    [Tooltip("로켓 피벗(바닥)에서 위로: 꼬리가 가리킬 몸통 높이(m)")] public float bubbleAnchorUp = 6.5f;
    [Tooltip("로켓 몸통 반지름(m) — 꼬리 끝을 몸통 왼쪽 가장자리 바로 바깥에 두기 위함")] public float rocketRadius = 1.7f;
    [Tooltip("이 거리(m)에서 bubbleSize, 멀어지면 거리·FOV에 비례해 작아지고 20% 아래부터 페이드")] public float bubbleRefDistance = 90f;

    public bool Started => started;
    public bool Finished => finished;
    public bool BubbleVisible => bubbleAlpha > 0.01f;

    bool started, finished, lifted;
    float elapsed, vel, driftVel, bubbleAlpha;
    Vector3 basePos, flameOffset, flameBase, rocketStart; float flameLightBase = 6f;
    Transform table;

    void Awake()
    {
        if (rocket != null) rocketStart = rocket.position;
        if (flame != null) { flameBase = flame.localScale; if (rocket != null) flameOffset = flame.position - rocket.position; }
        if (flameLight != null) flameLightBase = flameLight.intensity;
    }

    void Start()
    {
        if (flame != null) flame.gameObject.SetActive(false);
        if (flameLight != null) flameLight.enabled = false;
        if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (plume != null) plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Update()
    {
        if (finished || rocket == null) return;
        if (!started)
        {
            if (table == null)
            {
                var asr = Object.FindAnyObjectByType<ArtSceneReset>();
                if (asr != null) foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { table = ab.transform; break; }
                if (table == null) return;
            }
            if (table.position.z > triggerZ) Launch();
            return;
        }

        float dt = Time.deltaTime; elapsed += dt;
        float a;
        if (elapsed < shakeSeconds) a = 0.10f * (elapsed / shakeSeconds);
        else
        {
            if (!lifted) { lifted = true; vel = liftKick; }
            vel += liftAccel * dt; driftVel += driftAccel * dt;
            basePos += Vector3.up * vel * dt + Vector3.forward * driftVel * dt;
            a = 0.03f;
        }
        rocket.position = basePos + new Vector3(Random.Range(-a, a), 0f, Random.Range(-a, a));
        if (driftAccel > 0f && lifted) rocket.rotation = Quaternion.Slerp(rocket.rotation, Quaternion.Euler(270f, 180f, 0f) * Quaternion.Euler(0f, 0f, Mathf.Clamp(driftVel * 0.6f, 0f, 12f)), dt * 0.8f);

        // 불꽃/이펙트 추종(RocketLaunchCutscene.Update와 동일)
        if (flame != null && flame.gameObject.activeSelf)
        {
            float f = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 23f, 0.3f);
            float g = 0.8f + 0.45f * Mathf.PerlinNoise(0.7f, Time.time * 31f);
            flame.localScale = new Vector3(flameBase.x * f, flameBase.y * g, flameBase.z * f);
            flame.position = rocket.position + flameOffset;
            if (plume != null) plume.transform.position = rocket.position + flameOffset + Vector3.down * 0.8f;
            if (sparks != null && rocket.position.y - rocketStart.y < 6f) sparks.transform.position = rocket.position + flameOffset + Vector3.down * 0.5f;
            if (flameLight != null) { flameLight.transform.position = rocket.position + flameOffset + Vector3.down * 1.2f; flameLight.intensity = flameLightBase * f; }
        }
        if (smoke != null && elapsed > shakeSeconds + 3.5f && smoke.isEmitting) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (sparks != null && elapsed > shakeSeconds + 2.2f && sparks.isEmitting) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (rocket.position.y > vanishY)
        {
            finished = true;
            if (plume != null) plume.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (flame != null) flame.gameObject.SetActive(false);
            if (flameLight != null) flameLight.enabled = false;
            rocket.gameObject.SetActive(false);
        }
    }

    public void Launch()
    {
        if (started || rocket == null) return;
        started = true; elapsed = 0f; vel = 0f; driftVel = 0f; lifted = false;
        basePos = rocket.position;
        if (flame != null) flame.gameObject.SetActive(true);
        if (flameLight != null) flameLight.enabled = true;
        if (smoke != null) smoke.Play(true);
        if (plume != null) plume.Play(true);
        if (sparks != null) sparks.Play(true);
    }

    /// <summary>로켓 '왼쪽'에 떠서 가로 꼬리로 로켓을 가리키는 손그림 말풍선 + 의자 아이콘. 로켓을 그대로 따라감(화면 클램프 없음 — 로켓이 화면을 벗어나면 같이 벗어남).
    /// 크기 = bubbleSize × (bubbleRefDistance/거리) × (50/FOV), 최대 1 — 멀어지면 작아지고 20% 아래부터 페이드(SpaceIntroCutscene과 같은 규칙).</summary>
    void OnGUI()
    {
        bubbleAlpha = 0f;
        if (!started || finished || rocket == null || bubbleTex == null || Event.current.type != EventType.Repaint) return;
        var cam = Camera.main; if (cam == null) return;
        Vector3 body = rocket.position + Vector3.up * bubbleAnchorUp;
        Vector3 sp = cam.WorldToScreenPoint(body);
        if (sp.z <= 0f) return;
        float k = Mathf.Clamp((bubbleRefDistance / Mathf.Max(1f, sp.z)) * (50f / Mathf.Max(5f, cam.fieldOfView)), 0.01f, 1f);
        float alpha = Mathf.InverseLerp(0.06f, 0.2f, k);
        if (alpha <= 0.01f) return;
        bubbleAlpha = alpha;
        // 화면상 로켓 몸통 반지름(px) → 꼬리 끝을 몸통 왼쪽 가장자리 바로 바깥에
        float rPix = Mathf.Abs(cam.WorldToScreenPoint(body + cam.transform.right * rocketRadius).x - sp.x);
        float w = Screen.height * bubbleSize * k, h = w * bubbleTex.height / bubbleTex.width;   // 640×512 — 원 지름은 세로판과 동일
        float ax = sp.x - rPix - w * 0.02f, ay = Screen.height - sp.y;
        var r = new Rect(ax - w * (606f / 640f), ay - h * (249f / 512f), w, h);
        var prev = GUI.color; GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(r, bubbleTex, ScaleMode.StretchToFill, true);
        if (chairIcon != null)
        {
            float cx = r.x + w * (262f / 640f), cy = r.y + h * (255f / 512f), rad = h * (226f / 512f), s = rad * 1.36f;
            GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s), chairIcon, ScaleMode.ScaleToFit, true);
        }
        GUI.color = prev;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawLine(new Vector3(-30f, 0f, triggerZ), new Vector3(30f, 0f, triggerZ));
        Gizmos.DrawLine(new Vector3(0f, 0f, triggerZ), new Vector3(0f, 40f, triggerZ));
    }
}
