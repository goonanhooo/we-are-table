using System.Collections;
using UnityEngine;

/// <summary>
/// ★ 정글 인트로 컷씬(2026-07-08, 사용자 요청) — 테이블이 낙하 후 '적당히 멈추면' 1회:
/// 카메라가 창고 입구로 전환 → 홀웨이의 그 의자가 테이블처럼 다리를 하나하나 피벗하며 창고 안으로
/// 걸어 들어감 → 완전히 안으로 들어가면 카메라가 원래 플레이 카메라 포즈로 자연 복귀. 총 ~4초.
/// - 의자: 씬에 비활성으로 둔 홀웨이 Chair 복사본(chairTemplate). 시작 시 Leg1~4를 상단 피벗 GO로
///   감싸 X축 스윙(테이블 보행 느낌) + 몸통 봅/롤. 걷기 후엔 창고 안에 그대로 서 있음(끄지 않음).
/// - 정지 판정: 위치 변화 속도(게스트=NetworkTransform 수신도 커버) < settleSpeed 가 settleTime 지속.
/// - 컷씬 동안 테이블 동결(GiraffeCutscene 패턴: 루트 immovable), CameraFollow 비활성 후 복귀.
/// - '1' 리셋/리스폰에도 재생 1회뿐(played).
/// </summary>
public class JungleChairCutscene : MonoBehaviour
{
    [Tooltip("씬에 비활성으로 둔 의자(홀웨이 Chair 복사본)")]
    public GameObject chairTemplate;
    [Tooltip("창고 입구 중앙(바닥 높이)")]
    public Vector3 doorPos = new Vector3(-23f, 6.2f, 30.7f);
    [Tooltip("입구 바깥쪽 수평 방향(정규화)")]
    public Vector3 doorOutward = new Vector3(0.843f, 0f, -0.538f);
    public float startOutside = 4f;
    [Tooltip("문 안쪽으로 걸어 들어가는 최종 깊이 — 카메라 복귀 후에도 여기까지 계속 걷다가 사라짐")]
    public float endInside = 15f;
    [Tooltip("의자 걷는 속도(m/s) — 걷기 총 시간 = 전체 거리/속도")]
    public float walkSpeed = 3.2f;

    [Header("타이밍(카메라 ≈ 4초 — 의자는 그 뒤로도 계속 걸음)")]
    public float blendIn = 0.5f;
    [Tooltip("카메라가 의자를 지켜보는 시간(이후 걷는 도중에 블렌드 아웃)")]
    public float walkTime = 2.7f;
    public float blendOut = 0.8f;

    [Header("정지 판정")]
    public float settleSpeed = 0.8f;
    public float settleTime = 0.6f;
    [Tooltip("이 높이보다 위면 아직 낙하 중으로 간주")]
    public float airborneY = 12f;

    [Header("걸음 연출")]
    public float stepHz = 2.2f;
    public float swingAmp = 26f;
    public float bobAmp = 0.03f;
    [Tooltip("컷씬 동안 FOV(줌인 — 기본 카메라보다 좁게). 블렌드로 들어가고 나옴.")]
    public float cutsceneFov = 32f;

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
    }

    void Update()
    {
        if (played || board == null || chairTemplate == null || Time.timeSinceLevelLoad < 1.5f) return;
        float sp = (board.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
        lastPos = board.position;
        if (board.position.y > airborneY) { calm = 0f; return; }
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

        // 의자 준비(씬에 처음부터 문 앞에 서 있음): 다리 상단 피벗화 + 시작 위치 정렬
        var chair = chairTemplate;
        if (!chair.activeSelf) chair.SetActive(true);
        SetupLegPivots(chair.transform);
        Vector3 fwdDir = -doorOutward.normalized;                       // 걷는 방향(안쪽)
        Vector3 startP = doorPos + doorOutward.normalized * startOutside;
        Vector3 endP = doorPos - doorOutward.normalized * endInside;
        chair.transform.rotation = Quaternion.LookRotation(-fwdDir);    // 의자 정면 = -forward
        chair.transform.position = Grounded(startP);

        // 컷씬 카메라: 테이블 약간 왼쪽, 테이블과 비슷한 높이에서 창고로 걸어가는 의자를 바라봄
        Vector3 toDoor = doorPos - board.position; toDoor.y = 0f; toDoor.Normalize();
        Vector3 left = Vector3.Cross(toDoor, Vector3.up).normalized;
        Vector3 camPos = board.position + left * 2.4f - toDoor * 1.2f + Vector3.up * 0.75f;

        // ① 블렌드 인(+FOV 줌인)
        for (float t = 0f; t < blendIn; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / blendIn);
            Vector3 p = Vector3.Lerp(p0, camPos, u);
            Quaternion look = Quaternion.LookRotation(ChairFocus(chair) - p, Vector3.up);
            cam.SetPositionAndRotation(p, Quaternion.Slerp(r0, look, u));
            if (camCam != null) camCam.fieldOfView = Mathf.Lerp(fov0, cutsceneFov, u);
            yield return null;
        }

        // ② 의자 걷기: 독립 코루틴 — 카메라가 복귀한 뒤에도 끝(endInside)까지 계속 걷고 사라짐
        StartCoroutine(WalkChair(chair, startP, endP, fwdDir));

        // 카메라는 걷는 의자를 walkTime 동안 지켜봄
        for (float t = 0f; t < walkTime; t += Time.deltaTime)
        {
            cam.SetPositionAndRotation(camPos, Quaternion.LookRotation(ChairFocus(chair) - camPos, Vector3.up));
            yield return null;
        }

        // ③ 블렌드 아웃 — 의자가 아직 걷는 중에 플레이 카메라로 전환(시선은 의자를 따라가다 떨어져 나옴)
        Vector3 p1 = cam.position;
        Quaternion r1 = cam.rotation;
        for (float t = 0f; t < blendOut; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / blendOut);
            Quaternion watch = Quaternion.LookRotation(ChairFocus(chair) - p1, Vector3.up);
            cam.SetPositionAndRotation(Vector3.Lerp(p1, p0, u), Quaternion.Slerp(watch, r0, u));
            if (camCam != null) camCam.fieldOfView = Mathf.Lerp(cutsceneFov, fov0, u);
            yield return null;
        }
        cam.SetPositionAndRotation(p0, r0);
        if (camCam != null) camCam.fieldOfView = fov0;
        if (camFollow != null) camFollow.enabled = true;
        FreezeTable(false);
    }

    // 의자 보행(다리 하나하나 피벗 스윙 + 봅/롤) — 끝 지점 도달 시 사라짐. 카메라와 독립적으로 진행.
    // 창고 문턱 같은 단차는 y를 지수 보간으로 부드럽게('스윽') 넘는다.
    IEnumerator WalkChair(GameObject chair, Vector3 startP, Vector3 endP, Vector3 fwdDir)
    {
        float[] phase = { 0f, 0.5f, 0.25f, 0.75f };   // 대각 보행
        float total = Vector3.Distance(startP, endP) / Mathf.Max(walkSpeed, 0.1f);
        float curY = Grounded(startP).y;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            if (chair == null) yield break;
            float u = t / total;
            Vector3 p = Vector3.Lerp(startP, endP, u);
            float targetY = Grounded(p).y;
            curY = Mathf.Lerp(curY, targetY, Mathf.Clamp01(Time.deltaTime * 4.5f));
            p.y = curY;
            float cyc = t * stepHz;
            p.y += Mathf.Abs(Mathf.Sin(cyc * Mathf.PI * 2f)) * bobAmp;
            chair.transform.position = p;
            float roll = Mathf.Sin(cyc * Mathf.PI * 2f) * 2.2f;
            chair.transform.rotation = Quaternion.LookRotation(-fwdDir) * Quaternion.Euler(0f, 0f, roll);
            for (int i = 0; i < legPivots.Length; i++)
            {
                if (legPivots[i] == null) continue;
                float a = Mathf.Sin((cyc + phase[i]) * Mathf.PI * 2f) * swingAmp;
                legPivots[i].localRotation = Quaternion.Euler(a, 0f, 0f);
            }
            yield return null;
        }
        if (chair != null) chair.SetActive(false);   // 끝까지 걸어간 뒤 사라짐
    }

    static Vector3 ChairFocus(GameObject chair) => chair.transform.position + Vector3.up * 0.55f;

    // 지면/창고 바닥에 발 붙이기(레이캐스트, 실패 시 문 바닥 높이)
    Vector3 Grounded(Vector3 p)
    {
        if (Physics.Raycast(new Vector3(p.x, p.y + 4f, p.z), Vector3.down, out var hit, 12f))
            return new Vector3(p.x, hit.point.y, p.z);
        return new Vector3(p.x, doorPos.y, p.z);
    }

    // Leg1~4를 '다리 상단' 피벗 GO로 감싸 스윙 회전 가능하게(의자 로컬: 다리 top y=0.42)
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
