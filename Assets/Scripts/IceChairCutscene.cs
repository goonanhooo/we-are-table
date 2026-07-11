using System.Collections;
using UnityEngine;

/// <summary>
/// ★ 빙산(IceMountain) 의자 퇴장 컷씬 v2 (2026-07-10, 사용자 요청) — JungleChairCutscene의 보행을 가져오되:
/// ① 동굴 안 클로즈업 카메라가 걸어 나가는 의자를 지켜봄 → ② 의자가 입구(z24)를 지나는 순간
/// 바깥 측면 카메라로 '컷' 전환 → ③ 슬라이드 게이트 직전(freezeZ)에서 잠깐 멈춤 → ④ 얼음화
/// (IcePower와 똑같이 전 렌더러를 iceMaterial로 스왑) → ⑤ 얼어붙은 채 슬라이드를 타고 내려가 소멸.
/// 카메라는 슬라이드 출발을 잠깐 지켜보다 플레이 카메라로 복귀(의자는 독립적으로 계속 미끄러짐).
/// 정지 판정/테이블 동결/1회 재생은 정글 컷씬과 동일 패턴.
/// </summary>
public class IceChairCutscene : MonoBehaviour
{
    [Tooltip("씬에 배치된 의자(홀웨이 Chair 복사본)")]
    public GameObject chairTemplate;
    [Tooltip("동굴 입구 중앙(바닥 높이) — 이 z를 지나면 바깥 카메라로 컷")]
    public Vector3 doorPos = new Vector3(3.5f, 95f, 24f);
    [Tooltip("입구 안쪽 이 거리에서 걷기 시작")]
    public float startInside = 4f;
    [Tooltip("슬라이드 게이트 직전 — 여기서 멈추고 얼음화")]
    public float freezeZ = 34.5f;
    [Tooltip("슬라이드로 이 z까지 내려가면 소멸")]
    public float slideEndZ = 88f;
    [Tooltip("슬라이드 홈 중앙 x — 탑승 후 첫 10m 동안 여기로 스르르 쏠림")]
    public float slideCenterX = 0f;
    public float walkSpeed = 3f;

    [Header("카메라")]
    [Tooltip("동굴 안 클로즈업 — 의자 시작 위치 기준 오프셋")]
    public Vector3 camInsideOffset = new Vector3(-2.4f, 1.2f, -2.8f);
    [Tooltip("바깥 측면 카메라(월드) — 입구~게이트 사이를 봄")]
    public Vector3 camOutsidePos = new Vector3(10.5f, 98.5f, 31f);
    [Tooltip("슬라이드 축 뒤 카메라(월드) — 출발~마루 너머로 사라질 때까지(게이트 벽이 측면을 가려서 별도 컷)")]
    public Vector3 camSlidePos = new Vector3(1.2f, 101.5f, 28.5f);
    public float insideFov = 40f;
    public float outsideFov = 38f;
    public float blendIn = 0.5f;
    public float blendOut = 0.8f;
    [Tooltip("슬라이드 출발 후 카메라가 지켜보는 시간")]
    public float watchSlide = 1.6f;

    [Header("정지 판정(정글과 동일 — 동굴 시작은 낙하 없음)")]
    public float settleSpeed = 0.8f;
    public float settleTime = 0.6f;

    [Header("걸음/얼음화 연출")]
    public float stepHz = 2.2f;
    public float swingAmp = 26f;
    public float bobAmp = 0.03f;
    [Tooltip("멈춘 뒤 얼음화까지의 뜸")]
    public float stopBeat = 0.4f;
    [Tooltip("얼음화 후 슬라이드 출발까지의 뜸")]
    public float frozenBeat = 0.8f;
    [Tooltip("비우면 씬의 IcePower.iceMaterial을 가져옴")]
    public Material iceMaterial;

    enum Phase { Walking, Stopped, Frozen, Sliding, Gone }
    Phase phase = Phase.Walking;

    Transform board;
    Transform cam;
    Behaviour camFollow;
    bool played;
    float calm;
    Vector3 lastPos;
    Transform[] legPivots;

    void Start()
    {
        var t = GameObject.Find("Table");
        if (t != null) board = t.transform;
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            camFollow = Camera.main.GetComponent<CameraFollow>();
        }
        if (board != null) lastPos = board.position;
        if (iceMaterial == null)
        {
            var ip = Object.FindAnyObjectByType<IcePower>(FindObjectsInactive.Include);
            if (ip != null) iceMaterial = ip.iceMaterial;
        }
    }

    void Update()
    {
        if (played || board == null || chairTemplate == null || Time.timeSinceLevelLoad < 1.5f) return;
        float sp = (board.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
        lastPos = board.position;
        calm = sp < settleSpeed ? calm + Time.deltaTime : 0f;
        if (calm < settleTime) return;
        played = true;
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        FreezeTable(true);
        if (camFollow != null) camFollow.enabled = false;
        Vector3 p0 = cam.position;
        Quaternion r0 = cam.rotation;
        var camCam = cam.GetComponent<Camera>();
        float fov0 = camCam != null ? camCam.fieldOfView : 60f;

        // 의자 준비: 동굴 안에서 입구를 향해 정렬(의자 정면 = -forward)
        var chair = chairTemplate;
        if (!chair.activeSelf) chair.SetActive(true);
        SetupLegPivots(chair.transform);
        Vector3 startP = new Vector3(doorPos.x, doorPos.y, doorPos.z - startInside);
        chair.transform.rotation = Quaternion.LookRotation(Vector3.back);
        chair.transform.position = Grounded(startP, doorPos.y);
        StartCoroutine(ChairRun(chair));

        // ① 클로즈업 블렌드 인(동굴 안, 의자 바로 뒤-옆)
        Vector3 camA = chair.transform.position + camInsideOffset;
        for (float t = 0f; t < blendIn; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / blendIn);
            Vector3 p = Vector3.Lerp(p0, camA, u);
            Quaternion look = Quaternion.LookRotation(Focus(chair) - p, Vector3.up);
            cam.SetPositionAndRotation(p, Quaternion.Slerp(r0, look, u));
            if (camCam != null) camCam.fieldOfView = Mathf.Lerp(fov0, insideFov, u);
            yield return null;
        }

        // 클로즈업으로 지켜봄 — 의자가 입구를 지날 때까지
        while (phase != Phase.Gone && chair.activeSelf && chair.transform.position.z < doorPos.z + 0.2f)
        {
            cam.SetPositionAndRotation(camA, Quaternion.LookRotation(Focus(chair) - camA, Vector3.up));
            yield return null;
        }

        // ② 입구 통과 → 바깥 측면 카메라로 컷(즉시 전환)
        if (camCam != null) camCam.fieldOfView = outsideFov;
        while (phase != Phase.Gone && chair.activeSelf && phase != Phase.Sliding)
        {
            cam.SetPositionAndRotation(camOutsidePos, Quaternion.LookRotation(Focus(chair) - camOutsidePos, Vector3.up));
            yield return null;
        }

        // ⑤ 슬라이드 출발 → 슬라이드 축 뒤 카메라로 컷(측면은 게이트 벽에 가림) — 마루 너머로 사라지는 걸 지켜봄
        for (float t = 0f; t < watchSlide && phase != Phase.Gone && chair.activeSelf; t += Time.deltaTime)
        {
            cam.SetPositionAndRotation(camSlidePos, Quaternion.LookRotation(Focus(chair) - camSlidePos, Vector3.up));
            yield return null;
        }

        // 블렌드 아웃 — 의자가 아직 미끄러지는 중에 플레이 카메라로(시선이 따라가다 떨어져 나옴)
        Vector3 p1 = cam.position;
        for (float t = 0f; t < blendOut; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / blendOut);
            Vector3 focus = chair != null && chair.activeSelf ? Focus(chair) : camOutsidePos + Vector3.forward;
            Quaternion watch = Quaternion.LookRotation(focus - p1, Vector3.up);
            cam.SetPositionAndRotation(Vector3.Lerp(p1, p0, u), Quaternion.Slerp(watch, r0, u));
            if (camCam != null) camCam.fieldOfView = Mathf.Lerp(outsideFov, fov0, u);
            yield return null;
        }
        cam.SetPositionAndRotation(p0, r0);
        if (camCam != null) camCam.fieldOfView = fov0;
        if (camFollow != null) camFollow.enabled = true;
        FreezeTable(false);
    }

    // 의자 진행: 걷기(입구 통과, freezeZ 직전 감속 정지) → 뜸 → 얼음화 → 슬라이드 하강 → 소멸
    IEnumerator ChairRun(GameObject chair)
    {
        float[] phaseOff = { 0f, 0.5f, 0.25f, 0.75f };
        float curY = chair.transform.position.y;
        float t = 0f;
        float x = doorPos.x;

        // 걷기: z를 freezeZ까지 — 마지막 1.2m는 감속, 다리 스윙도 함께 잦아듦
        float z = chair.transform.position.z;
        while (z < freezeZ)
        {
            float remain = freezeZ - z;
            float speed = walkSpeed * Mathf.Clamp01(remain / 1.2f + 0.12f);
            z = Mathf.Min(freezeZ, z + Mathf.Min(walkSpeed, speed) * Time.deltaTime);
            t += Time.deltaTime;
            float targetY = Grounded(new Vector3(x, curY, z), doorPos.y).y;
            curY = Mathf.Lerp(curY, targetY, Mathf.Clamp01(Time.deltaTime * 4.5f));
            float cyc = t * stepHz;
            float gait = Mathf.Clamp01(remain / 1.2f);   // 정지 직전 스윙/봅 페이드
            chair.transform.position = new Vector3(x, curY + Mathf.Abs(Mathf.Sin(cyc * Mathf.PI * 2f)) * bobAmp * gait, z);
            float roll = Mathf.Sin(cyc * Mathf.PI * 2f) * 2.2f * gait;
            chair.transform.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(0f, 0f, roll);
            for (int i = 0; i < legPivots.Length; i++)
            {
                if (legPivots[i] == null) continue;
                float a = Mathf.Sin((cyc + phaseOff[i]) * Mathf.PI * 2f) * swingAmp * gait;
                legPivots[i].localRotation = Quaternion.Euler(a, 0f, 0f);
            }
            yield return null;
        }

        // 정지: 다리 가지런히
        phase = Phase.Stopped;
        chair.transform.rotation = Quaternion.LookRotation(Vector3.back);
        for (int i = 0; i < legPivots.Length; i++)
            if (legPivots[i] != null) legPivots[i].localRotation = Quaternion.identity;
        yield return new WaitForSeconds(stopBeat);

        // 얼음화 — IcePower.Apply()와 동일: 전 렌더러 재질을 iceMaterial로 스왑
        if (iceMaterial != null)
            foreach (var r in chair.GetComponentsInChildren<MeshRenderer>(true))
                if (r != null) r.sharedMaterial = iceMaterial;
        phase = Phase.Frozen;
        yield return new WaitForSeconds(frozenBeat);

        // 슬라이드: 얼어붙은 채 가속 하강(내리막에서 강하게), 경사 따라 앞으로 기울임.
        // 얼음이라 홈(하프파이프) 가운데로 스르르 쏠림 — x를 슬라이드 중앙으로 수렴.
        phase = Phase.Sliding;
        float v = 1.2f;
        float pitch = 0f;
        float slid = 0f;
        float slideStartZ = z;
        while (z < slideEndZ && slid < 10f)
        {
            slid += Time.deltaTime;
            float dz = v * Time.deltaTime;
            z += dz;
            float prog = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((z - slideStartZ) / 10f));
            float cx = Mathf.Lerp(doorPos.x, slideCenterX, prog);
            float targetY = Grounded(new Vector3(cx, curY, z), curY).y;
            bool downhill = targetY < curY - 0.01f;
            v = Mathf.Min(14f, v + (downhill ? 8f : 2.2f) * Time.deltaTime);
            float slope = dz > 1e-4f ? Mathf.Atan2(curY - targetY, dz) * Mathf.Rad2Deg : 0f;
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(slope, -10f, 50f), Mathf.Clamp01(Time.deltaTime * 3f));
            curY = Mathf.Lerp(curY, targetY, Mathf.Clamp01(Time.deltaTime * 6f));
            chair.transform.position = new Vector3(cx, curY, z);
            chair.transform.rotation = Quaternion.AngleAxis(pitch, Vector3.right) * Quaternion.LookRotation(Vector3.back);
            yield return null;
        }
        chair.SetActive(false);   // 슬라이드 저 아래로 사라짐
        phase = Phase.Gone;
    }

    static Vector3 Focus(GameObject chair) => chair.transform.position + Vector3.up * 0.55f;

    // 현재 높이 기준으로 발밑 지면 탐색(고지대 y96에서도 유효 — 정글 컷씬 버그 교훈)
    Vector3 Grounded(Vector3 p, float fallbackY)
    {
        if (Physics.Raycast(new Vector3(p.x, p.y + 4f, p.z), Vector3.down, out var hit, 16f))
            return new Vector3(p.x, hit.point.y, p.z);
        return new Vector3(p.x, fallbackY, p.z);
    }

    void SetupLegPivots(Transform chair)
    {
        legPivots = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var leg = chair.Find("Leg" + (i + 1));
            if (leg == null) continue;
            var pv = new GameObject("LegPivot" + (i + 1)).transform;
            pv.SetParent(chair, false);
            pv.localPosition = new Vector3(leg.localPosition.x, 0.42f, leg.localPosition.z);
            leg.SetParent(pv, true);
            legPivots[i] = pv;
        }
    }

    void FreezeTable(bool freeze)
    {
        if (board == null) return;
        foreach (var ab in board.root.GetComponentsInChildren<ArticulationBody>(true))
            if (ab != null && ab.isRoot)
            {
                ab.immovable = freeze;
                if (freeze) { ab.linearVelocity = Vector3.zero; ab.angularVelocity = Vector3.zero; }
            }
    }
}
