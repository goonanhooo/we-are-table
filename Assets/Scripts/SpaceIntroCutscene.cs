using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-09-05 우주 맵 도입 컷씬(v2 — 시네마틱 추격). 씬 GO `SpaceIntro`에 부착, 참조는 전부 인스펙터(씬 오브젝트).
///  ① 추격(chaseShots 합계 ≈15s): 의자가 탄 우주선(chairShip, 앞·위)을 테이블이 탄 로켓(tableShip, 뒤)이 pathStart→pathBreak로 뒤쫓음.
///     카메라는 `chaseShots` 샷 리스트를 순서대로 — 월드 고정 샷(옆에서 스쳐 지나감)과 TableShip 자식 마커(옆/앞 3/4/위 등) 혼합.
///     두 우주선 위에 동그란 손그림 말풍선(bubbleTex) + 의자/테이블 렌더 아이콘(chairIcon/tableIcon)을 labelSeconds 동안 표시.
///     샷에 orbitDeg를 주면 holdSeconds 동안 고정 후 시선점 중심으로 서서히 공전(옆에서 오래 보다가 천천히 도는 연출). 불꽃은 Flicker()로 일렁임.
///  ② 충돌: pathBreak의 소행성(crashAsteroid)에 테이블선이 부딪혀 섬광+파편(debris/debrisSparks), 로켓 모델 off, 숨겨둔 테이블이 나타나 무중력 표류.
///     의자선은 chairEscape로 가속 이탈.
///  ③ driftSeconds 동안 파괴 연출을 지켜본 뒤 → 클로즈업(watchSeconds: 어깨 너머에서 멀어지는 의자선을 FOV watchZoomFov까지 당김) → 허탈(floatSeconds: 줌아웃, 홀로 떠다니는 테이블 주위를 천천히 돎).
///  ④ 우주오징어(squid, SquidDummy)가 squidFrom에서 천천히(squidArriveSeconds, SmoothStep) 테이블 곁(squidStopOffset)으로 스윽 나타나 말풍선 2줄 → 힘 구슬(orb) → 테이블 보랏빛 발광 → SquidMode.thrustLocked 해제 + 조작 힌트.
///     대화 카메라는 테이블 기준 squidCamOffset(표류하는 테이블을 따라감).
///  ⑤ 오징어 퇴장, CameraFollow.BlendFrom 복귀.
/// 로켓 모델 노즈=로컬 +Z. 멀티: 호스트 → NetStageSync가 게스트 PlayRemote()(테이블 조작 생략).
/// </summary>
public class SpaceIntroCutscene : MonoBehaviour
{
    public enum LookAt { ChairShip, TableShip, Midpoint, ShipToAsteroid }

    [System.Serializable]
    public class ChaseShot
    {
        [Tooltip("카메라 위치 마커(월드 고정이면 SpaceIntro 자식, 추적이면 TableShip 자식)")] public Transform cam;
        public LookAt look = LookAt.Midpoint;
        public float seconds = 3f;
        public float fov = 45f;
        [Tooltip("이 시간 동안 고정 후 서서히 공전 시작")] public float holdSeconds = 0f;
        [Tooltip("시선점을 중심으로 공전할 각도(0=고정, 음수=뒤쪽으로)")] public float orbitDeg = 0f;
    }

    [Header("배우")]
    public Transform tableShip;            // 테이블이 탄 로켓(뒤)
    public Transform chairShip;            // 의자가 탄 우주선(앞)
    public Transform crashAsteroid;        // 충돌 소행성(pathBreak 근처)
    public Transform squid;                // SquidDummy 루트(비활성 시작)
    public Transform squidHead;            // 말풍선 앵커
    public Transform orb;                  // 힘 구슬(비활성 시작)
    public ParticleSystem debris, debrisSparks;
    public Light flashLight;
    [Tooltip("두 우주선의 불꽃(일렁임)")] public Transform tableFlame, chairFlame;
    public float flameFlicker = 0.35f, flameFlickerSpeed = 11f;

    [Header("탑승 말풍선(동그란 말풍선 + 의자/테이블 이미지)")]
    public Texture2D bubbleTex, chairIcon, tableIcon;
    [Tooltip("말풍선 높이(화면 높이 비율)")] public float bubbleSize = 0.21f;
    [Tooltip("말풍선 앵커: 우주선 피벗(꼬리)에서 앞으로 / 위로")] public float bubbleAnchorForward = 3f, bubbleAnchorUp = 2.6f;

    [Header("경로/마커")]
    public Transform pathStart, pathBreak, chairEscape;
    [Tooltip("의자선 오프셋(테이블선 기준, 로컬 XYZ — z=앞서는 거리)")] public Vector3 chairOffsetStart = new Vector3(0f, 5f, 18f), chairOffsetEnd = new Vector3(0f, 6f, 30f);
    public ChaseShot[] chaseShots;
    public Transform squidFrom, squidExit;
    [Tooltip("클로즈업 카메라: 테이블 기준 오프셋(x=옆, y=위, z=뒤가 음수) — 어깨 너머")] public Vector3 watchOffset = new Vector3(-3.5f, 1.8f, -7f);
    [Tooltip("충돌 튕김 속도(우주선 기준 right/up/forward)")] public Vector3 breakBounce = new Vector3(1.5f, 3.0f, -0.8f);
    [Tooltip("클로즈업 끝 FOV(멀어지는 의자선을 점점 당겨 봄)")] public float watchZoomFov = 10f;
    [Tooltip("허탈 비트 동안 테이블 주위를 도는 각도")] public float floatArcDeg = 40f;
    [Tooltip("오징어 정지 위치(테이블 기준 월드축 오프셋)")] public Vector3 squidStopOffset = new Vector3(5f, 0.3f, 3.5f);
    [Tooltip("오징어 대화 카메라(테이블 기준 오프셋)")] public Vector3 squidCamOffset = new Vector3(-1.2f, 1.8f, -6.5f);

    [Header("타이밍")]
    public float labelSeconds = 10f;
    [Tooltip("파괴 연출을 지켜보는 시간(파편이 다 퍼질 때까지)")] public float driftSeconds = 3.5f;
    [Tooltip("멀어지는 의자선 클로즈업 시간")] public float watchSeconds = 6f;
    [Tooltip("둥둥 떠다니는 허탈 비트 시간")] public float floatSeconds = 5f;
    public float chairEscapeSeconds = 14f;
    [Tooltip("의자선 탈출 가속 지수(클수록 처음 느리고 나중 빠름)")] public float chairEscapePow = 2.2f;
    [Tooltip("오징어 등장 시간(스윽 — SmoothStep)")] public float squidArriveSeconds = 4f;
    public float lineSeconds = 2.2f;
    public float orbSeconds = 1.4f;
    public float glowSeconds = 1.8f;
    public float squidLeaveSeconds = 2.6f;
    public float hintSeconds = 6f;
    [Tooltip("충돌 순간 카메라 흔들림(m) / 지속(s)")] public float impactShake = 0.45f, impactShakeSeconds = 0.7f;
    public Color glowColor = new Color(0.7f, 0.4f, 1.5f);

    [HideInInspector] public bool netRemoteDriven;
    /// <summary>테스트용: true면 세션 없이도 게스트처럼 동작(Start에서 자동 재생 안 함 → PlayRemote(skip) 수동 호출).</summary>
    public static bool debugForceRemote;
    public bool Started => started;
    /// <summary>호스트가 Play()한 서버 시각(NetworkManager.ServerTime) — NetStageSync가 발행, 게스트는 경과분만큼 건너뛰어 재생.</summary>
    public double StartServerTime { get; private set; }

    bool started, remote; float remoteSkip;
    ArticulationBody tableRoot; Renderer[] tableRenderers;   // 컷씬 시작 시 켜져 있던 테이블 렌더러만(테니스공/분사 등 꺼진 것은 제외)
    Transform cam; Camera camC; CameraFollow camFollow;
    string bubbleText = "", hintText = ""; float flashAlpha; bool showLabels;
    GUIStyle bubbleStyle, hintStyle, labelStyle;
    Vector3 shipDir = Vector3.forward;
    Vector3 tableFlameBase = Vector3.one, chairFlameBase = Vector3.one; Quaternion tableFlameRot = Quaternion.identity, chairFlameRot = Quaternion.identity;

    void Start()
    {
        if (squid != null) squid.gameObject.SetActive(false);
        if (orb != null) orb.gameObject.SetActive(false);
        if (debris != null) debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (debrisSparks != null) debrisSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (flashLight != null) flashLight.enabled = false;
        if (pathStart != null && pathBreak != null) shipDir = (pathBreak.position - pathStart.position).normalized;
        if (tableFlame != null) { tableFlameBase = tableFlame.localScale; tableFlameRot = tableFlame.localRotation; }
        if (chairFlame != null) { chairFlameBase = chairFlame.localScale; chairFlameRot = chairFlame.localRotation; }
        PlaceShips(0f, 0f);
        // ★멀티 게스트: NetStageSync.OnNetworkSpawn(netRemoteDriven=true)이 이 Start보다 늦게 올 수 있어 세션 상태로도 판정.
        //   게스트는 호스트의 시작 신호(PlayRemote(경과초))를 기다리고, 그동안 테이블(호스트 위치로 텔레포트 중)은 숨긴다.
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (debugForceRemote || (nm != null && nm.IsListening && !nm.IsServer)) netRemoteDriven = true;
        if (netRemoteDriven) PrepareRemote(); else Play();
    }

    public void Play()
    {
        if (started) return; started = true; remote = false;
        var nm = Unity.Netcode.NetworkManager.Singleton;
        StartServerTime = nm != null && nm.IsListening ? nm.ServerTime.Time : 0.0;
        StartCoroutine(Run());
    }
    /// <param name="skipSeconds">호스트가 시작한 뒤 흐른 시간 — 그만큼 추격을 건너뛰어 호스트와 타임라인을 맞춤</param>
    public void PlayRemote(float skipSeconds = 0f) { if (started) return; started = true; remote = true; remoteSkip = Mathf.Max(0f, skipSeconds); StartCoroutine(Run()); }

    /// <summary>테이블 루트/렌더러 수집(1회). 렌더러는 '지금 켜져 있는 MeshRenderer'만 — 테니스공/분사 등 꺼진 것은 제외.</summary>
    void CollectTable()
    {
        if (tableRenderers != null) return;
        var asr = Object.FindAnyObjectByType<ArtSceneReset>(FindObjectsInactive.Include);
        if (asr == null) { tableRenderers = new Renderer[0]; return; }
        foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { tableRoot = ab; break; }
        var all = asr.transform.root.GetComponentsInChildren<Renderer>(false);
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in all) if (r != null && r.enabled && r is MeshRenderer) list.Add(r);
        tableRenderers = list.ToArray();
    }

    void PrepareRemote()
    {
        CollectTable();
        foreach (var r in tableRenderers) if (r != null) r.enabled = false;
    }

    void Update()
    {
        if (flashAlpha > 0f) flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.deltaTime * 2.5f);
        if (flashLight != null && flashLight.enabled) flashLight.intensity = Mathf.MoveTowards(flashLight.intensity, 0f, Time.deltaTime * 40f);
    }

    float ChaseTotal()
    {
        float s = 0f;
        if (chaseShots != null) foreach (var c in chaseShots) if (c != null) s += Mathf.Max(0.1f, c.seconds);
        return s > 0.5f ? s : 12f;
    }

    /// <summary>u = 0..1 추격 진행, t = 경과 초(흔들림용). 두 우주선 배치.</summary>
    void PlaceShips(float u, float t)
    {
        Vector3 a = pathStart != null ? pathStart.position : Vector3.zero, b = pathBreak != null ? pathBreak.position : a + Vector3.forward * 200f;
        Vector3 right = Vector3.Cross(Vector3.up, shipDir).normalized;
        Vector3 sway = right * Mathf.Sin(t * 1.3f) * 1.4f + Vector3.up * Mathf.Sin(t * 0.9f + 1f) * 0.9f;
        Vector3 p = Vector3.Lerp(a, b, u) + sway * (1f - u * u);   // 충돌 지점에선 정확히 소행성으로
        if (tableShip != null) { tableShip.position = p; tableShip.rotation = Quaternion.LookRotation(shipDir + right * Mathf.Cos(t * 1.3f) * 0.04f); }
        Vector3 off = Vector3.Lerp(chairOffsetStart, chairOffsetEnd, u);
        Vector3 sway2 = right * Mathf.Sin(t * 1.1f + 2f) * 2.2f + Vector3.up * Mathf.Cos(t * 0.8f) * 1.0f;
        if (chairShip != null) { chairShip.position = Vector3.Lerp(a, b, u) + shipDir * off.z + Vector3.up * off.y + right * off.x + sway2; chairShip.rotation = Quaternion.LookRotation(shipDir + right * Mathf.Cos(t * 1.1f + 2f) * 0.05f); }
        Flicker(tableFlame, tableFlameBase, tableFlameRot, t, 0f);
        Flicker(chairFlame, chairFlameBase, chairFlameRot, t, 3.1f);
    }

    /// <summary>불꽃 일렁임: 펄린 노이즈로 길이(±flameFlicker)·굵기(±¼)·끝 흔들림(±4°).</summary>
    void Flicker(Transform f, Vector3 baseScale, Quaternion baseRot, float t, float phase)
    {
        if (f == null) return;
        float n1 = Mathf.PerlinNoise(t * flameFlickerSpeed + phase, 0.37f) - 0.5f;
        float n2 = Mathf.PerlinNoise(0.81f, t * flameFlickerSpeed * 0.7f + phase) - 0.5f;
        float n3 = Mathf.PerlinNoise(t * flameFlickerSpeed * 1.3f + phase * 2f, 5.2f) - 0.5f;
        f.localScale = new Vector3(baseScale.x * (1f + n1 * flameFlicker * 0.5f), baseScale.y * (1f + n2 * flameFlicker * 2f), baseScale.z * (1f + n1 * flameFlicker * 0.5f));
        f.localRotation = baseRot * Quaternion.Euler(n3 * 8f, n1 * 8f, 0f);
    }

    Vector3 LookPoint(LookAt l)
    {
        Vector3 tp = tableShip != null ? tableShip.position + Vector3.up * 0.5f : Vector3.zero;
        Vector3 cp = chairShip != null ? chairShip.position + Vector3.up * 0.5f : tp;
        switch (l)
        {
            case LookAt.ChairShip: return cp;
            case LookAt.TableShip: return tp;
            case LookAt.ShipToAsteroid: return crashAsteroid != null ? Vector3.Lerp(tp, crashAsteroid.position, 0.35f) : tp;
            default: return (tp + cp) * 0.5f;
        }
    }

    IEnumerator Run()
    {
        CollectTable();
        var squidMode = Object.FindAnyObjectByType<SquidMode>();
        if (squidMode != null) squidMode.thrustLocked = true;
        StageTimer.Suppress = true;   // 컷씬 동안 초 표시 숨김
        if (tableRenderers != null) foreach (var r in tableRenderers) if (r != null) r.enabled = false;
        if (!remote && tableRoot != null) tableRoot.immovable = true;
        var mainCam = Camera.main;
        if (mainCam != null) { cam = mainCam.transform; camC = mainCam; camFollow = mainCam.GetComponent<CameraFollow>(); if (camFollow != null) camFollow.enabled = false; }
        float baseFov = camC != null ? camC.fieldOfView : 60f;

        // ① 추격 — 샷 리스트 (게스트는 remoteSkip만큼 건너뛰어 호스트 타임라인에 맞춤)
        float total = ChaseTotal(); float t = Mathf.Clamp(remoteSkip, 0f, total); showLabels = t < labelSeconds;
        int shotCount = chaseShots != null ? chaseShots.Length : 0;
        float shotStart = 0f;
        for (int si = 0; si < Mathf.Max(1, shotCount); si++)
        {
            var shot = shotCount > 0 ? chaseShots[si] : null;
            float dur = shot != null ? Mathf.Max(0.1f, shot.seconds) : total;
            float skipIn = t - shotStart; shotStart += dur;
            if (skipIn >= dur) continue;   // 이미 지난 샷
            Quaternion lookRot = cam != null ? cam.rotation : Quaternion.identity; bool firstFrame = true;
            bool orbit = shot != null && Mathf.Abs(shot.orbitDeg) > 0.01f; Vector3 orbitLocal0 = Vector3.zero;
            for (float st = Mathf.Max(0f, skipIn); st < dur; st += Time.deltaTime, t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / total);
                PlaceShips(u, t);
                if (!remote && tableRoot != null && tableShip != null) tableRoot.TeleportRoot(tableShip.position, tableShip.rotation);
                if (cam != null)
                {
                    Vector3 look = LookPoint(shot != null ? shot.look : LookAt.Midpoint);
                    if (shot != null && shot.cam != null)
                    {
                        if (orbit)
                        {
                            // 시선점을 중심으로 서서히 공전(진행 방향 기준 프레임, holdSeconds 뒤 SmoothStep 가감속)
                            Quaternion frame = Quaternion.LookRotation(shipDir, Vector3.up);
                            if (firstFrame) orbitLocal0 = Quaternion.Inverse(frame) * (shot.cam.position - look);
                            float p = Mathf.Clamp01((st - shot.holdSeconds) / Mathf.Max(0.1f, dur - shot.holdSeconds));
                            cam.position = look + frame * (Quaternion.AngleAxis(shot.orbitDeg * Mathf.SmoothStep(0f, 1f, p), Vector3.up) * orbitLocal0);
                        }
                        else cam.position = shot.cam.position;
                    }
                    Quaternion want = Quaternion.LookRotation(look - cam.position, Vector3.up);
                    lookRot = firstFrame ? want : Quaternion.Slerp(lookRot, want, Time.deltaTime * 6f);
                    cam.rotation = lookRot;
                    if (camC != null) camC.fieldOfView = shot != null ? shot.fov : baseFov;
                }
                if (t > labelSeconds) showLabels = false;
                firstFrame = false;
                yield return null;
            }
        }
        showLabels = false;
        PlaceShips(1f, t);

        // ② 소행성 충돌 → 파괴
        Vector3 breakPos = (tableShip != null ? tableShip.position : (pathBreak != null ? pathBreak.position : Vector3.zero)) + shipDir * 5f;   // 노즈 = 충돌점
        flashAlpha = 1f;
        if (flashLight != null) { flashLight.transform.position = breakPos; flashLight.enabled = true; flashLight.intensity = 60f; }
        if (debris != null) { debris.transform.position = breakPos; debris.Play(true); }
        if (debrisSparks != null) { debrisSparks.transform.position = breakPos; debrisSparks.Play(true); }
        if (tableShip != null) tableShip.gameObject.SetActive(false);
        if (tableRenderers != null) foreach (var r in tableRenderers) if (r != null) r.enabled = true;
        Vector3 right2 = Vector3.Cross(Vector3.up, shipDir).normalized;
        if (!remote && tableRoot != null)
        {
            tableRoot.TeleportRoot(breakPos - shipDir * 5f + Vector3.up * 1.2f, Quaternion.LookRotation(shipDir));
            tableRoot.immovable = false;
            tableRoot.linearVelocity = right2 * breakBounce.x + Vector3.up * breakBounce.y + shipDir * breakBounce.z;   // 소행성에 튕겨 위·옆으로 표류
            tableRoot.angularVelocity = new Vector3(0.15f, 0.35f, 0.1f);
        }
        StartCoroutine(ChairEscape());
        // 충돌 직후 잠깐: 카메라는 그 자리에서(흔들리며) 파편/테이블을 봄
        Vector3 driftCam = cam != null ? cam.position : breakPos;
        for (float d = 0f; d < driftSeconds; d += Time.deltaTime)
        {
            if (cam != null)
            {
                float k = impactShakeSeconds > 0f ? Mathf.Clamp01(1f - d / impactShakeSeconds) : 0f;
                cam.position = driftCam + Random.insideUnitSphere * impactShake * k * k;
                Vector3 target = tableRoot != null ? tableRoot.transform.position : breakPos;
                cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(target - driftCam, Vector3.up), Time.deltaTime * 4f);
            }
            yield return null;
        }
        if (cam != null) cam.position = driftCam;

        // ③-a 클로즈업: 테이블 어깨 너머에서 멀어지는 의자선을 점점 당겨 봄(FOV 50 → watchZoomFov)
        float fovA = camC != null ? camC.fieldOfView : 50f;
        {
            Vector3 camA = cam != null ? cam.position : breakPos; Quaternion camRA = cam != null ? cam.rotation : Quaternion.identity;
            for (float w = 0f; w < watchSeconds; w += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w / 1.4f));
                float z = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w / watchSeconds));
                Vector3 tp = tableRoot != null ? tableRoot.transform.position : breakPos;
                Vector3 desired = tp + right2 * watchOffset.x + Vector3.up * watchOffset.y + shipDir * watchOffset.z;
                Vector3 lookAt = chairShip != null && chairShip.gameObject.activeSelf ? chairShip.position : tp + shipDir * 300f + Vector3.up * 40f;
                if (cam != null)
                {
                    cam.position = Vector3.Lerp(camA, desired, u);
                    cam.rotation = Quaternion.Slerp(camRA, Quaternion.LookRotation(lookAt - cam.position, Vector3.up), u);
                    if (camC != null) camC.fieldOfView = Mathf.Lerp(fovA, watchZoomFov, z);
                }
                yield return null;
            }
        }
        // ③-b 허탈: 줌아웃하며 홀로 둥둥 떠다니는 테이블로 시선을 돌리고, 천천히 주위를 돈다
        {
            Vector3 tp0 = tableRoot != null ? tableRoot.transform.position : breakPos;
            Vector3 off0 = cam != null ? cam.position - tp0 : right2 * watchOffset.x + Vector3.up * watchOffset.y + shipDir * watchOffset.z;
            Quaternion camRA = cam != null ? cam.rotation : Quaternion.identity;
            for (float w = 0f; w < floatSeconds; w += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w / 1.4f));
                Vector3 tp = tableRoot != null ? tableRoot.transform.position : breakPos;
                Vector3 off = Quaternion.AngleAxis(floatArcDeg * Mathf.SmoothStep(0f, 1f, w / floatSeconds), Vector3.up) * off0;
                if (cam != null)
                {
                    cam.position = tp + off;
                    cam.rotation = Quaternion.Slerp(camRA, Quaternion.LookRotation((tp + Vector3.up * 0.3f) - cam.position, Vector3.up), u);
                    if (camC != null) camC.fieldOfView = Mathf.Lerp(watchZoomFov, 52f, u);
                }
                yield return null;
            }
        }

        // ④ 오징어 갑자기 등장
        if (squid != null)
        {
            squid.gameObject.SetActive(true);
            Vector3 s0 = squidFrom != null ? squidFrom.position : breakPos + new Vector3(40f, 8f, 10f);
            Vector3 camA = cam != null ? cam.position : breakPos; Quaternion camRA = cam != null ? cam.rotation : Quaternion.identity;
            for (float a2 = 0f; a2 < squidArriveSeconds; a2 += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a2 / squidArriveSeconds));   // 스윽(부드럽게 들어와 멈춤)
                Vector3 tp = tableRoot != null ? tableRoot.transform.position : breakPos;
                Vector3 s1 = tp + squidStopOffset;
                squid.position = Vector3.Lerp(s0, s1, u) + Vector3.up * Mathf.Sin(a2 * 6f) * 0.25f * (1f - u);
                squid.rotation = Quaternion.Slerp(squid.rotation, Quaternion.LookRotation((tp - squid.position).normalized, Vector3.up), Time.deltaTime * 6f);
                SquidCam(camA, camRA, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(a2 / squidArriveSeconds)));
                yield return null;
            }
            bubbleText = L.T("내 힘을 줄게!", "I'll give you my power!");
            for (float w = 0f; w < lineSeconds; w += Time.deltaTime) { SquidHold(); yield return null; }
            bubbleText = L.T("얼른 쫓아가!", "hurry, go after it!");
            for (float w = 0f; w < lineSeconds; w += Time.deltaTime) { SquidHold(); yield return null; }
            bubbleText = "";
        }

        // ⑤ 힘 구슬 → 테이블 발광 → 능력 해제
        if (orb != null)
        {
            Vector3 o0 = squidHead != null ? squidHead.position : (squid != null ? squid.position : breakPos);
            orb.gameObject.SetActive(true);
            for (float ot = 0f; ot < orbSeconds; ot += Time.deltaTime)
            {
                float u = ot / orbSeconds;
                Vector3 o1 = tableRoot != null ? tableRoot.transform.position : breakPos;
                orb.position = Vector3.Lerp(o0, o1, Mathf.SmoothStep(0f, 1f, u)) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 1.6f;
                SquidHold();
                yield return null;
            }
            orb.gameObject.SetActive(false);
        }
        var mpb = new MaterialPropertyBlock();
        var rends = tableRenderers ?? new Renderer[0];
        for (float g = 0f; g < glowSeconds; g += Time.deltaTime)
        {
            float u = g / glowSeconds;
            float k = Mathf.Pow(Mathf.Clamp01(u / 0.2f) * Mathf.Clamp01((1f - u) / 0.3f), 0.6f);
            if (!remote && squidMode != null && u >= 0.45f && squidMode.thrustLocked) { squidMode.thrustLocked = false; squidMode.unlocked = true; squidMode.active = true; }
            foreach (var r in rends)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;
                if (m == null || !m.IsKeywordEnabled("_EMISSION")) continue;   // ⚠️sharedMaterial에 EnableKeyword 하면 .mat 에셋이 수정됨 — Table.mat은 이미 켜져 있음
                r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", glowColor * k); r.SetPropertyBlock(mpb);
            }
            SquidHold();
            yield return null;
        }
        foreach (var r in rends) if (r != null) r.SetPropertyBlock(null);
        if (squidMode != null) { squidMode.thrustLocked = false; squidMode.unlocked = true; if (!remote) squidMode.active = true; SquidMode.Unlocked = true; }
        hintText = L.T("다리 늘리기 키(E · Y · O · ])를 누르면 그 발끝에서 분사돼요 — 다리를 겨눠서 날아가요!",
                       "hold a leg's stretch key (E · Y · O · ]) to thrust from that foot — aim your legs to fly!");
        StartCoroutine(HideHint());

        // ⑥ 오징어 퇴장 + 카메라 복귀 (+ 타이머 다시 표시·0부터)
        if (camC != null) camC.fieldOfView = baseFov;
        if (camFollow != null) { camFollow.BlendFrom(1.4f); camFollow.enabled = true; }
        StageTimer.Suppress = false; if (StageTimer.Instance != null) StageTimer.Instance.RestartClock();
        if (squid != null)
        {
            Vector3 e0 = squid.position, e1 = squidExit != null ? squidExit.position : e0 + new Vector3(80f, 30f, 60f);
            for (float lt = 0f; lt < squidLeaveSeconds; lt += Time.deltaTime)
            {
                float u = lt / squidLeaveSeconds; u = u * u;
                squid.position = Vector3.Lerp(e0, e1, u);
                squid.rotation = Quaternion.Slerp(squid.rotation, Quaternion.LookRotation((e1 - e0).normalized, Vector3.up), Time.deltaTime * 4f);
                yield return null;
            }
            squid.gameObject.SetActive(false);
        }
    }

    /// <summary>오징어 대화 카메라: 테이블 기준 오프셋에서 테이블↔오징어 머리 중간을 봄(blend = camA/camRA → 목표).</summary>
    void SquidCam(Vector3 camA, Quaternion camRA, float blend)
    {
        if (cam == null) return;
        Vector3 tp = tableRoot != null ? tableRoot.transform.position : transform.position;
        Vector3 head = squidHead != null ? squidHead.position : (squid != null ? squid.position : tp);
        Vector3 want = tp + squidCamOffset;
        Vector3 look = (tp + head) * 0.5f + Vector3.up * 0.3f;
        cam.position = Vector3.Lerp(camA, want, blend);
        cam.rotation = Quaternion.Slerp(camRA, Quaternion.LookRotation(look - cam.position, Vector3.up), blend);
    }

    /// <summary>대화/구슬/발광 동안: 오징어는 테이블 곁(오프셋)을 따라가며 테이블을 바라보고, 카메라도 테이블 기준 유지.</summary>
    void SquidHold()
    {
        Vector3 tp = tableRoot != null ? tableRoot.transform.position : transform.position;
        if (squid != null && squid.gameObject.activeSelf)
        {
            squid.position = Vector3.Lerp(squid.position, tp + squidStopOffset, Time.deltaTime * 2f);
            Vector3 d = tp - squid.position; d.y *= 0.3f;
            if (d.sqrMagnitude > 0.01f) squid.rotation = Quaternion.Slerp(squid.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), Time.deltaTime * 4f);
        }
        if (cam != null) SquidCam(cam.position, cam.rotation, Mathf.Clamp01(Time.deltaTime * 5f));
    }

    IEnumerator ChairEscape()
    {
        if (chairShip == null) yield break;
        Vector3 c0 = chairShip.position, c1 = chairEscape != null ? chairEscape.position : c0 + shipDir * 600f;
        Vector3 right = Vector3.Cross(Vector3.up, shipDir).normalized;
        for (float t = 0f; t < chairEscapeSeconds; t += Time.deltaTime)
        {
            float u = Mathf.Pow(Mathf.Clamp01(t / chairEscapeSeconds), chairEscapePow);
            chairShip.position = Vector3.Lerp(c0, c1, u) + right * Mathf.Sin(t * 1.5f) * 2f;
            chairShip.rotation = Quaternion.LookRotation((c1 - c0).normalized);
            Flicker(chairFlame, chairFlameBase, chairFlameRot, Time.time, 3.1f);
            yield return null;
        }
        chairShip.gameObject.SetActive(false);
    }

    IEnumerator HideHint() { yield return new WaitForSeconds(hintSeconds); hintText = ""; }

    void DrawBubble(Vector3 world, string text, GUIStyle style, float lift)
    {
        if (Camera.main == null || string.IsNullOrEmpty(text)) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(world);
        if (sp.z <= 0f) return;
        var sz = style.CalcSize(new GUIContent(text));
        float w = sz.x + 52f, h = sz.y + 26f;
        float ax = sp.x, ay = Screen.height - sp.y;                 // 앵커(GUI 좌표)
        var r = new Rect(ax - w * 0.5f, ay - lift - h, w, h);
        r.x = Mathf.Clamp(r.x, 8f, Screen.width - w - 8f); r.y = Mathf.Clamp(r.y, 8f, Screen.height - h - 8f);
        // 꼬리: 박스 아래 중앙 → 앵커까지 짧은 선
        UiKit.Fill(new Rect(Mathf.Clamp(ax, r.x + 12f, r.xMax - 12f) - 1.5f, r.yMax, 3f, Mathf.Max(6f, Mathf.Min(lift, ay - r.yMax))), new Color(0.1f, 0.1f, 0.1f, 0.85f));
        HandUI.Paper(r, HandUI.BoxWide, 0.96f);
        GUI.Label(r, text, style);
    }

    /// <summary>동그란 손그림 말풍선(bubbleTex: 512×640, 꼬리 끝 (262,606), 원 중심 (256,262) 반지름 226) + 아이콘.</summary>
    void DrawIconBubble(Vector3 world, Texture2D icon)
    {
        if (Camera.main == null || bubbleTex == null) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(world);
        if (sp.z <= 0f) return;
        float h = Screen.height * bubbleSize, w = h * bubbleTex.width / bubbleTex.height;
        float ax = sp.x, ay = Screen.height - sp.y;
        var r = new Rect(ax - w * (262f / 512f), ay - h * (606f / 640f), w, h);
        r.x = Mathf.Clamp(r.x, 4f, Screen.width - w - 4f); r.y = Mathf.Clamp(r.y, 4f, Screen.height - h - 4f);
        GUI.DrawTexture(r, bubbleTex, ScaleMode.StretchToFill, true);
        if (icon != null)
        {
            float cx = r.x + w * (256f / 512f), cy = r.y + h * (262f / 640f), rad = h * (226f / 640f), s = rad * 1.36f;
            GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s), icon, ScaleMode.ScaleToFit, true);
        }
    }

    void OnGUI()
    {
        if (!started) return;
        UiKit.ApplyKoreanFont();
        if (flashAlpha > 0f) UiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 1f, 1f, flashAlpha));
        if (bubbleStyle == null)
        {
            bubbleStyle = HandUI.Style(34, HandUI.Ink, TextAnchor.MiddleCenter);
            labelStyle = HandUI.Style(26, HandUI.Ink, TextAnchor.MiddleCenter);
            hintStyle = HandUI.Style(24, HandUI.Ink, TextAnchor.MiddleCenter);
        }
        if (showLabels)
        {
            if (chairShip != null && chairShip.gameObject.activeSelf) DrawIconBubble(chairShip.position + chairShip.forward * bubbleAnchorForward + Vector3.up * bubbleAnchorUp, chairIcon);
            if (tableShip != null && tableShip.gameObject.activeSelf) DrawIconBubble(tableShip.position + tableShip.forward * bubbleAnchorForward + Vector3.up * bubbleAnchorUp, tableIcon);
        }
        if (!string.IsNullOrEmpty(bubbleText) && squidHead != null) DrawBubble(squidHead.position + Vector3.up * 0.6f, bubbleText, bubbleStyle, 30f);
        if (!string.IsNullOrEmpty(hintText))
        {
            var sz = hintStyle.CalcSize(new GUIContent(hintText));
            var r = new Rect((Screen.width - sz.x - 44f) * 0.5f, Screen.height - sz.y - 60f, sz.x + 44f, sz.y + 20f);
            UiKit.Fill(r, new Color(1f, 1f, 1f, 0.6f));
            GUI.Label(r, hintText, hintStyle);
            if (HandUI.Underline != null) GUI.DrawTexture(new Rect(r.center.x - 200f, r.yMax - 9f, 400f, 12f), HandUI.Underline, ScaleMode.StretchToFill);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (pathStart != null && pathBreak != null) { Gizmos.color = Color.cyan; Gizmos.DrawLine(pathStart.position, pathBreak.position); Gizmos.DrawWireSphere(pathBreak.position, 2f); }
        if (pathBreak != null && chairEscape != null) { Gizmos.color = Color.yellow; Gizmos.DrawLine(pathBreak.position, chairEscape.position); }
        if (squidFrom != null && squidExit != null) { Gizmos.color = Color.magenta; Gizmos.DrawLine(squidFrom.position, squidExit.position); }
        if (chaseShots != null) { Gizmos.color = new Color(1f, 0.6f, 0.1f); foreach (var s in chaseShots) if (s != null && s.cam != null) Gizmos.DrawWireSphere(s.cam.position, 0.6f); }
    }
}
