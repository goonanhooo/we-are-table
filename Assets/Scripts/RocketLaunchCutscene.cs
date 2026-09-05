using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-09-05 빙산 결말 — 로켓 발사 컷씬(착지 플랫폼 끝 오른쪽 발사대). 씬 GO `LandingZone/RocketLaunch`에 부착,
/// 같은 GO가 바닥의 빛나는 탑승 원(대시 링 메시 + 트리거)이다. 테이블이 원에 들어오면:
///  ① 테이블 동결(root.immovable) + 카메라가 탑승 샷(camBoard)으로 블렌드, 원이 확 밝아지고 로켓 창문에서 빛기둥(beam)이 내려옴
///  ② 테이블이 빙글 돌며 떠올라 창문(windowPoint)으로 빨려 들어감 → 섬광과 함께 사라짐(렌더러 off)
///  ③ 발사 샷(camLaunch)으로 컷, 손글씨 카운트다운 3·2·1·발사!
///  ④ 불꽃(flame 콘, 깜빡임)·연기(ParticleSystem)·조명, 로켓이 흔들리다 가속 상승, 카메라가 올려다보며 추적
///  ⑤ 하늘로 사라지면 페이드 아웃 → nextScene 로드(비어 있으면 원위치 복구 — 데모/테스트용)
/// 씬 참조 전부 인스펙터(런타임 생성 없음). 멀티: 호스트 트리거 → NetStageSync가 게스트에 PlayRemote()(시각만, 씬 전환은 호스트가 NetScene.Load).
/// </summary>
public class RocketLaunchCutscene : MonoBehaviour
{
    [Header("씬 참조")]
    public Transform rocket;
    [Tooltip("로켓 창문 — 테이블이 빨려 들어가는 지점")] public Transform windowPoint;
    [Tooltip("바닥 탑승 마커(발광 대시 링) — ⚠️자식 GO에 둘 것: 링만 회전시키므로 이 GO(트리거/마커 부모)에 두면 카메라 마커까지 같이 돈다")] public Renderer ring;
    [Tooltip("빛기둥(원→창문). 비활성으로 시작")] public Transform beam;
    [Tooltip("불꽃 콘(로켓 밑). 비활성으로 시작")] public Transform flame;
    public Light flameLight;
    public ParticleSystem smoke;
    public Transform camBoard, camBoardLook, camLaunch, camLaunchLook;

    [Header("타이밍")]
    public float camBlendSeconds = 0.9f;
    public float boardSeconds = 2.4f;
    public int countdownFrom = 3;
    public float shakeSeconds = 1.3f;
    public float liftSeconds = 5.5f;
    [Tooltip("상승 가속도(m/s²)")] public float liftAccel = 12f;
    public float fadeSeconds = 1.5f;
    [Tooltip("발사 후 로드할 씬. 비우면 원위치 복구(데모)")] public string nextScene = "Space";
    public float boardCamFov = 42f, launchCamFov = 50f;

    [Header("마커 연출")]
    public Color ringEmission = new Color(0.25f, 0.85f, 1.2f);
    public float ringPulseSpeed = 2.2f;
    public float ringSpin = 25f;

    [HideInInspector] public bool netRemoteDriven;   // 게스트: 트리거 무시(NetStageSync가 PlayRemote)
    public bool Started => started;

    bool started, remote, fading;
    float ringBoost;                       // 탑승 중 링 밝기(음수=어둡게)
    MaterialPropertyBlock mpb;
    Vector3 rocketStart, flameOffset, flameBase; Quaternion rocketStartRot; float flameLightBase = 6f;
    Transform cam; CameraFollow camFollow; float camFov = 60f;
    ArticulationBody tableRoot; Renderer[] tableRenderers; Vector3 tableStart; Quaternion tableStartRot;
    string countdownText = ""; float flashAlpha;
    GUIStyle countStyle;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (rocket != null) { rocketStart = rocket.position; rocketStartRot = rocket.rotation; }
        if (flame != null) { flameBase = flame.localScale; if (rocket != null) flameOffset = flame.position - rocket.position; }
        if (flameLight != null) flameLightBase = flameLight.intensity;
    }

    void Start()
    {
        if (beam != null) beam.gameObject.SetActive(false);
        if (flame != null) flame.gameObject.SetActive(false);
        if (flameLight != null) flameLight.enabled = false;
        if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Update()
    {
        if (ring != null)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * ringPulseSpeed);
            float k = started ? Mathf.Max(0f, 1f + ringBoost) : pulse;
            mpb.SetColor("_EmissionColor", ringEmission * k);
            ring.SetPropertyBlock(mpb);
            ring.transform.Rotate(0f, ringSpin * Time.deltaTime, 0f, Space.World);
        }
        if (started && flame != null && flame.gameObject.activeSelf && rocket != null)
        {
            float f = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 23f, 0.3f);
            float g = 0.8f + 0.45f * Mathf.PerlinNoise(0.7f, Time.time * 31f);
            flame.localScale = new Vector3(flameBase.x * f, flameBase.y * g, flameBase.z * f);
            flame.position = rocket.position + flameOffset;
            if (flameLight != null)
            {
                flameLight.transform.position = rocket.position + flameOffset + Vector3.down * 1.2f;
                flameLight.intensity = flameLightBase * f;
            }
        }
        if (flashAlpha > 0f) flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.deltaTime * 2.2f);
    }

    float retriggerBlockUntil;   // 데모 원위치 직후 재발동 방지

    void OnTriggerEnter(Collider other)
    {
        if (started || netRemoteDriven || Time.time < retriggerBlockUntil) return;
        Transform bodyRoot = other.attachedRigidbody != null ? other.attachedRigidbody.transform.root
                           : other.attachedArticulationBody != null ? other.attachedArticulationBody.transform.root : null;
        if (bodyRoot == null || bodyRoot.GetComponentInChildren<ArtLegController>() == null) return;
        Play();
    }

    /// <summary>호스트/1인: 컷씬 시작(테이블 동결·이동 포함).</summary>
    public void Play() { if (started) return; started = true; remote = false; StartCoroutine(Run()); }
    /// <summary>게스트: 시각만 재생(테이블 포즈는 호스트 NetworkTransform, 씬 전환은 호스트).</summary>
    public void PlayRemote() { if (started) return; started = true; remote = true; StartCoroutine(Run()); }

    IEnumerator Run()
    {
        // 테이블/카메라 확보
        var asr = Object.FindAnyObjectByType<ArtSceneReset>(FindObjectsInactive.Include);
        if (asr != null)
        {
            foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { tableRoot = ab; break; }
            tableRenderers = asr.transform.root.GetComponentsInChildren<Renderer>(true);
        }
        if (!remote && tableRoot != null)
        {
            foreach (var tm in Object.FindObjectsByType<TennisMode>(FindObjectsSortMode.None)) if (tm != null) tm.ReleaseAll();
            tableStart = tableRoot.transform.position; tableStartRot = tableRoot.transform.rotation;
            tableRoot.immovable = true;
        }
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            cam = mainCam.transform; camFollow = mainCam.GetComponent<CameraFollow>(); camFov = mainCam.fieldOfView;
            if (camFollow != null) camFollow.enabled = false;
        }

        // ① 원 부스트 + 빛기둥 + 탑승 샷
        ringBoost = 2.5f;
        if (beam != null) { beam.gameObject.SetActive(true); PlaceBeam(); }
        yield return CamMove(camBoard, camBoardLook, camBlendSeconds, boardCamFov);

        // ② 탑승: 빙글 돌며 떠올라 창문으로
        Vector3 p0 = tableRoot != null ? tableRoot.transform.position : (ring != null ? ring.transform.position : transform.position);
        float yaw0 = tableRoot != null ? tableRoot.transform.eulerAngles.y : 0f;
        Vector3 p1 = windowPoint != null ? windowPoint.position : p0 + Vector3.up * 6f;
        for (float t = 0f; t < boardSeconds; t += Time.deltaTime)
        {
            float s = Mathf.SmoothStep(0f, 1f, t / boardSeconds);
            Vector3 p = Vector3.Lerp(p0, p1, s) + Vector3.up * Mathf.Sin(s * Mathf.PI) * 1.2f;
            Quaternion r = Quaternion.Euler(0f, yaw0 + s * 540f, 0f) * Quaternion.Euler(Mathf.Sin(s * Mathf.PI) * 18f, 0f, 0f);
            if (!remote && tableRoot != null) tableRoot.TeleportRoot(p, r);
            ringBoost = Mathf.Lerp(2.5f, 0.3f, s);
            yield return null;
        }
        flashAlpha = 1f;
        if (tableRenderers != null) foreach (var r in tableRenderers) if (r != null) r.enabled = false;
        if (beam != null) beam.gameObject.SetActive(false);
        ringBoost = -0.7f;
        yield return new WaitForSeconds(0.55f);

        // ③ 발사 샷으로 컷 + 카운트다운
        SnapCam(camLaunch, camLaunchLook, launchCamFov);
        for (int n = countdownFrom; n >= 1; n--) { countdownText = n.ToString(); yield return new WaitForSeconds(1f); }
        countdownText = L.T("발사!", "liftoff!");

        // ④ 발사: 흔들림 → 가속 상승, 카메라 추적
        if (flame != null) flame.gameObject.SetActive(true);
        if (flameLight != null) flameLight.enabled = true;
        if (smoke != null) smoke.Play(true);
        float v = 0f, elapsed = 0f, total = shakeSeconds + liftSeconds;
        Vector3 basePos = rocket != null ? rocket.position : Vector3.zero;
        fading = false;
        while (elapsed < total)
        {
            float dt = Time.deltaTime; elapsed += dt;
            if (rocket != null)
            {
                float a;
                if (elapsed < shakeSeconds) a = 0.10f * (elapsed / shakeSeconds);
                else { v += liftAccel * dt; basePos += Vector3.up * v * dt; a = 0.03f; }
                rocket.position = basePos + new Vector3(Random.Range(-a, a), 0f, Random.Range(-a, a));
            }
            if (elapsed > shakeSeconds + 1.2f) countdownText = "";
            if (smoke != null && elapsed > shakeSeconds + 2.5f && smoke.isEmitting) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (cam != null && rocket != null)
            {
                Vector3 look = rocket.position + Vector3.up * 5f;
                cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(look - cam.position), dt * 6f);
            }
            if (!fading && elapsed > total - fadeSeconds) { fading = true; ScreenFader.FadeOut(fadeSeconds); }
            yield return null;
        }

        // ⑤ 다음 씬(호스트/1인) — 게스트는 호스트의 씬 전환을 기다림
        if (remote) yield break;
        if (!string.IsNullOrEmpty(nextScene))
        {
            ScreenFader.FadeInAfterNextLoad(0.4f, 1.6f);
            NetScene.Load(nextScene);
            yield break;
        }
        // 데모 폴백: 원위치 복구 후 다시 놀 수 있게
        yield return new WaitForSeconds(0.3f);
        ResetScene();
        ScreenFader.FadeIn(1.0f);
    }

    /// <summary>데모/테스트용 원위치: 로켓·테이블·카메라 복구, 다시 탑승 가능.</summary>
    public void ResetScene()
    {
        if (rocket != null) { rocket.position = rocketStart; rocket.rotation = rocketStartRot; }
        if (flame != null) { flame.gameObject.SetActive(false); flame.localScale = flameBase; }
        if (flameLight != null) flameLight.enabled = false;
        if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (tableRenderers != null) foreach (var r in tableRenderers) if (r != null) r.enabled = true;
        if (!remote && tableRoot != null)
        {
            // 원 밖(플랫폼 쪽 4m)에 내려놓기 — 원 위에 다시 놓으면 즉시 재발동되어 무한 반복
            Vector3 center = transform.position;
            Vector3 dir = tableStart - center; dir.y = 0f;
            if (dir.sqrMagnitude < 1f) dir = Vector3.back;
            Vector3 back = center + dir.normalized * 4f;
            tableRoot.TeleportRoot(new Vector3(back.x, tableStart.y, back.z), tableStartRot);
            tableRoot.linearVelocity = Vector3.zero; tableRoot.angularVelocity = Vector3.zero;
            tableRoot.immovable = false;
        }
        if (cam != null) { var c = cam.GetComponent<Camera>(); if (c != null) c.fieldOfView = camFov; }
        if (camFollow != null) { camFollow.BlendFrom(1.0f); camFollow.enabled = true; }
        countdownText = ""; ringBoost = 0f; started = false; fading = false;
        retriggerBlockUntil = Time.time + 3f;
    }

    void PlaceBeam()
    {
        if (beam == null || ring == null || windowPoint == null) return;
        Vector3 a = ring.transform.position + Vector3.up * 0.2f, b = windowPoint.position;
        beam.position = (a + b) * 0.5f;
        beam.up = (b - a).normalized;                      // Unity Cylinder 축 = 로컬 Y
        var s = beam.localScale; s.y = (b - a).magnitude * 0.5f; beam.localScale = s;
    }

    IEnumerator CamMove(Transform to, Transform look, float seconds, float fov)
    {
        if (cam == null || to == null) yield break;
        var c = cam.GetComponent<Camera>();
        Vector3 p0 = cam.position; Quaternion r0 = cam.rotation; float f0 = c != null ? c.fieldOfView : fov;
        Quaternion r1 = look != null ? Quaternion.LookRotation(look.position - to.position) : to.rotation;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float s = Mathf.SmoothStep(0f, 1f, t / seconds);
            cam.position = Vector3.Lerp(p0, to.position, s);
            cam.rotation = Quaternion.Slerp(r0, r1, s);
            if (c != null) c.fieldOfView = Mathf.Lerp(f0, fov, s);
            yield return null;
        }
        cam.position = to.position; cam.rotation = r1; if (c != null) c.fieldOfView = fov;
    }

    void SnapCam(Transform to, Transform look, float fov)
    {
        if (cam == null || to == null) return;
        cam.position = to.position;
        cam.rotation = look != null ? Quaternion.LookRotation(look.position - to.position) : to.rotation;
        var c = cam.GetComponent<Camera>(); if (c != null) c.fieldOfView = fov;
    }

    void OnGUI()
    {
        if (!started) return;
        UiKit.ApplyKoreanFont();
        if (flashAlpha > 0f) UiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 1f, 1f, flashAlpha));
        if (string.IsNullOrEmpty(countdownText)) return;
        if (countStyle == null) countStyle = HandUI.Style(150, HandUI.Ink, TextAnchor.MiddleCenter, true);
        var r = new Rect(0f, Screen.height * 0.05f, Screen.width, 200f);   // 로켓 머리와 안 겹치게 위쪽
        // 흰 종이 느낌 살짝(3D 위 가독)
        var sz = countStyle.CalcSize(new GUIContent(countdownText));
        UiKit.Fill(new Rect(r.center.x - sz.x * 0.5f - 24f, r.center.y - sz.y * 0.5f, sz.x + 48f, sz.y), new Color(1f, 1f, 1f, 0.55f));
        GUI.Label(r, countdownText, countStyle);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (windowPoint != null) Gizmos.DrawWireSphere(windowPoint.position, 0.6f);
        if (camBoard != null && camBoardLook != null) { Gizmos.color = Color.yellow; Gizmos.DrawLine(camBoard.position, camBoardLook.position); Gizmos.DrawWireSphere(camBoard.position, 0.4f); }
        if (camLaunch != null && camLaunchLook != null) { Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawLine(camLaunch.position, camLaunchLook.position); Gizmos.DrawWireSphere(camLaunch.position, 0.4f); }
    }
}
