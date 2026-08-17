using UnityEngine;

/// <summary>
/// ★ 테니스공 모드(2026-07-08) — 켜지면 네 다리 끝에 테니스공이 생긴다.
/// 붙이기 키(FL=C/FR=N/BL=./BR=오른쪽Shift)를 누르면 공이 닿아 있는 벽/오브젝트에 '끈끈이'처럼 딱 붙고,
/// 떼기 키(FL=Z/FR=V/BL=M/BR=/)를 누르면 놓는다. (★2026-08-17 기린 신축 키와 분리 — 아랫줄 전용 키) 다리별 독립 — 번갈아 붙이며 벽을 오르는 조작.
/// 붙기 구현: FixedJoint.connectedArticulationBody(2020.2+).
///  - 정적 벽: 발끝 위치에 키네마틱 Rigidbody 앵커 GO를 만들어 벽 transform에 붙이고(움직이는 발판도 추종) 조인트.
///  - 동적 오브젝트: 그 Rigidbody에 조인트를 직접 부착(물건을 다리로 붙잡아 끌 수 있음).
/// 공은 시각 전용(콜라이더 없음 — 보행 물리 불변). 붙은 다리 공은 주황으로 바뀜.
/// 테이블 기능 패턴: Table.prefab 루트, 평소 active=false. 전용 키라 기린 모드와 동시 사용 가능(붙은 다리는 GiraffeMode가 신축 차단).
/// </summary>
public class TennisMode : MonoBehaviour, ICheatUnlockable
{
    [Tooltip("켜짐 여부(평소 꺼짐 — TennisStage가 씬 입장 시 켬).")]
    public bool active;
    public bool unlocked;
    /// <summary>한 번이라도 켜졌는가 — 조작법 노출용(GiraffeMode.Unlocked 패턴).</summary>
    public static bool Unlocked;
    [Tooltip("공 반지름(시각).")]
    public float ballRadius = 0.17f;
    [Tooltip("붙기 탐지 반경(발끝 기준) — 공이 '닿아 있다'로 치는 거리.")]
    public float grabRadius = 0.35f;
    [Tooltip("테니스공 재질(연두).")]
    public Material ballMat;
    [Tooltip("붙은 상태 재질(주황).")]
    public Material grabMat;

    // ★2026-08-17 전용 키(사용자 요청): 기린 신축 키와 안 겹치게 키보드 아랫줄 사용.
    //   붙이기: WASD(앞왼)=C, TFGH(앞오)=N, IJKL(뒤왼)=., Arrows(뒤오)=오른쪽Shift
    //   떼기:   WASD=Z, TFGH=V, IJKL=M, Arrows=/  — 각 키셋의 왼→오 배치와 일치
    static readonly UnityEngine.InputSystem.Key[] GrabKeys =
    { UnityEngine.InputSystem.Key.C, UnityEngine.InputSystem.Key.N, UnityEngine.InputSystem.Key.Period, UnityEngine.InputSystem.Key.RightShift };
    static readonly UnityEngine.InputSystem.Key[] ReleaseKeys =
    { UnityEngine.InputSystem.Key.Z, UnityEngine.InputSystem.Key.V, UnityEngine.InputSystem.Key.M, UnityEngine.InputSystem.Key.Slash };

    TableInput ti;
    ArtLegController[] legs;
    ArticulationBody[] legAbs;
    Transform[] meshes;
    Transform[] balls;
    MeshRenderer[] ballRs;
    Joint[] joints;
    GameObject[] anchors;
    bool[] prevLonger, prevShorter;
    readonly Collider[] overlap = new Collider[16];

    public void CheatUnlock() { unlocked = true; }

    /// <summary>이 키셋 다리의 공이 어딘가에 붙어 있는가(GiraffeMode가 신축 차단 판단에 사용).</summary>
    public bool IsStuck(LegControlSet set)
    {
        if (legs == null || joints == null) return false;
        for (int i = 0; i < legs.Length; i++)
            if (legs[i] != null && legs[i].controlSet == set) return joints[i] != null;
        return false;
    }

    void Start()
    {
        ti = TableInput.Of(this);
        legs = transform.root.GetComponentsInChildren<ArtLegController>(true);
        int n = legs.Length;
        legAbs = new ArticulationBody[n];
        meshes = new Transform[n];
        balls = new Transform[n];
        ballRs = new MeshRenderer[n];
        joints = new Joint[n];
        anchors = new GameObject[n];
        prevLonger = new bool[n];
        prevShorter = new bool[n];
        for (int i = 0; i < n; i++)
        {
            legAbs[i] = legs[i].GetComponent<ArticulationBody>();
            meshes[i] = legs[i].transform.childCount > 0 ? legs[i].transform.GetChild(0) : legs[i].transform;
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(b.GetComponent<Collider>());
            b.name = "TennisBall_" + i;
            b.transform.localScale = Vector3.one * (ballRadius * 2f);
            ballRs[i] = b.GetComponent<MeshRenderer>();
            if (ballMat != null) ballRs[i].sharedMaterial = ballMat;
            b.SetActive(false);
            balls[i] = b.transform;
        }
    }

    Vector3 Tip(int i) => meshes[i].TransformPoint(0f, -0.5f, 0f);

    void Update()
    {
        if (!active)
        {
            for (int i = 0; i < legs.Length; i++)
            {
                if (balls[i] != null && balls[i].gameObject.activeSelf) balls[i].gameObject.SetActive(false);
                Release(i);
            }
            return;
        }
        Unlocked = true;
        if (ti == null) ti = TableInput.Of(this);

        for (int i = 0; i < legs.Length; i++)
        {
            if (balls[i] == null) continue;
            if (!balls[i].gameObject.activeSelf) balls[i].gameObject.SetActive(true);
            balls[i].SetPositionAndRotation(Tip(i), meshes[i].rotation);

            var kb = UnityEngine.InputSystem.Keyboard.current;
            int ks = (int)legs[i].controlSet;
            bool grabNow = kb != null && ks >= 0 && ks < 4 && kb[GrabKeys[ks]].isPressed;
            bool relNow = kb != null && ks >= 0 && ks < 4 && kb[ReleaseKeys[ks]].isPressed;
            if (grabNow && !prevLonger[i] && joints[i] == null) TryGrab(i);
            if (relNow && !prevShorter[i]) Release(i);
            prevLonger[i] = grabNow;
            prevShorter[i] = relNow;

            // 조인트가 외부 요인으로 파괴됐으면(씬 오브젝트 삭제 등) 상태 정리
            if (joints[i] == null && anchors[i] != null) { Destroy(anchors[i]); anchors[i] = null; }
            var want = joints[i] != null ? grabMat : ballMat;
            if (want != null && ballRs[i].sharedMaterial != want) ballRs[i].sharedMaterial = want;
        }
    }

    void TryGrab(int i)
    {
        if (legAbs[i] == null || !legAbs[i].enabled) return;
        Vector3 tip = Tip(i);
        int n = Physics.OverlapSphereNonAlloc(tip, grabRadius, overlap, ~0, QueryTriggerInteraction.Ignore);
        Collider best = null;
        float bestD = float.MaxValue;
        for (int k = 0; k < n; k++)
        {
            var c = overlap[k];
            if (c == null || c.transform.root == transform.root) continue;   // 자기 몸 제외
            float d = (c.ClosestPoint(tip) - tip).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        if (best == null) return;

        if (best.attachedRigidbody != null)
        {
            var j = best.attachedRigidbody.gameObject.AddComponent<FixedJoint>();
            j.connectedArticulationBody = legAbs[i];
            j.enableCollision = true;
            joints[i] = j;
        }
        else
        {
            var a = new GameObject("TennisAnchor_" + i);
            a.transform.position = tip;
            a.transform.SetParent(best.transform, true);   // 움직이는 벽/발판도 추종
            var rb = a.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var j = a.AddComponent<FixedJoint>();
            j.connectedArticulationBody = legAbs[i];
            joints[i] = j;
            anchors[i] = a;
        }
    }

    void Release(int i)
    {
        if (joints[i] != null) Destroy(joints[i]);
        joints[i] = null;
        if (anchors[i] != null) Destroy(anchors[i]);
        anchors[i] = null;
    }

    void OnDisable()
    {
        if (legs == null) return;
        for (int i = 0; i < legs.Length; i++) Release(i);
    }
}
