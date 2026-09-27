using UnityEngine;

/// <summary>
/// ★ 테니스공 모드(2026-07-08) — 켜지면 네 다리 끝에 테니스공이 생긴다.
/// 붙이기 키(BL=E/BR=Y/FL=O/FR=])를 누르면 공이 닿아 있는 벽/오브젝트에 '끈끈이'처럼 딱 붙고,
/// 떼기 키(BL=Q/BR=R/FL=U/FR=P)를 누르면 놓는다. 선택된 능력만 공유 키를 사용한다. 다리별 독립 — 번갈아 붙이며 벽을 오르는 조작.
/// 붙기 구현: FixedJoint.connectedArticulationBody(2020.2+).
///  - 정적 벽: 발끝 위치에 키네마틱 Rigidbody 앵커 GO를 만들어 벽 transform에 붙이고(움직이는 발판도 추종) 조인트.
///  - 동적 오브젝트: 그 Rigidbody에 조인트를 직접 부착(물건을 다리로 붙잡아 끌 수 있음).
/// 공은 시각 전용(콜라이더 없음 — 보행 물리 불변). 붙은 다리 공은 주황으로 바뀜.
/// 테이블 기능 패턴: Table.prefab 루트, 평소 active=false. 다른 능력으로 전환하면 부착을 해제한다.
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
    public float grabRadius = 0.5f;   // 0.35→0.5(2026-08-20 벽타기 후하게)
    [Tooltip("테니스공 재질(연두).")]
    public Material ballMat;
    [Tooltip("붙은 상태 재질(주황).")]
    public Material grabMat;

    // The selected power uses each keyboard group's longer/shorter keys: E/Q, Y/R, O/U, ]/P.
    public void SetActive(bool on)
    {
        active=on;
        if(on){if(!netVisualOnly)AbilitySelection.Select(this);return;}
        if(legs==null)return;
        for(int i=0;i<legs.Length;i++)
        {
            Release(i);
            if(balls[i]!=null)balls[i].gameObject.SetActive(false);
            prevLonger[i]=prevShorter[i]=false;
        }
    }

    // ★2026-09-03 등반 보조(사용자 요청): 한 다리라도 붙어 있으면 테이블 무게를 거의 0으로 —
    //   모든 ArticulationBody에 중력의 weightCancelWhenStuck 배만큼 반대 힘을 매 FixedUpdate 가해 상쇄.
    //   (드라이브가 무게를 버티는 대신 몸을 옮기는 데만 쓰여 벽 등반이 안정·수월)
    [Tooltip("한 다리라도 붙어 있을 때 테이블 무게 상쇄 비율(0=그대로, 1=완전 무중력). 0.95=거의 0")]
    [Range(0f, 1f)] public float weightCancelWhenStuck = 0.95f;

    /// <summary>★멀티 게스트: 붙기 물리 없이 공 표시만(NetVisualSync가 NetApplyVisual로 상태 반영).</summary>
    [HideInInspector] public bool netVisualOnly;
    ArticulationBody[] allBodies;   // 무게 상쇄 대상(테이블 루트 아래 전부)
    int netStuckMask;

    /// <summary>다리별 붙음 상태 비트마스크(legs 순서) — 호스트가 NetVisualSync로 발행.</summary>
    public int StuckMask
    {
        get { int m = 0; if (joints != null) for (int i = 0; i < joints.Length; i++) if (joints[i] != null) m |= 1 << i; return m; }
    }

    /// <summary>★2026-09-05 동적 물체(눈사람 머리처럼 움직이는 Rigidbody)를 하나라도 잡고 있으면 true — 이때는 무게 상쇄 없음
    /// (상쇄가 있으면 물건을 든 채 몸이 둥실 떠 물건을 옮기는 감각이 깨짐). 벽/키네마틱(광차 등) 붙기만 등반 보조.</summary>
    public bool AnyDynamicStuck
    {
        get { if (joints == null || stuckDynamic == null) return false; for (int i = 0; i < joints.Length; i++) if (joints[i] != null && stuckDynamic[i]) return true; return false; }
    }

    /// <summary>게스트 시각 적용: 공 표시 on/off + 붙은 다리 주황(멱등, 매 프레임 호출 무해).</summary>
    public void NetApplyVisual(bool on, int stuckMask) { active = on; netStuckMask = stuckMask; }

    public bool PracticeStuck(LegControlSet set)
    {
        if (legs == null) return false;
        int mask=netVisualOnly?netStuckMask:StuckMask;
        for(int i=0;i<legs.Length;i++)if(legs[i]!=null&&legs[i].controlSet==set)return (mask&(1<<i))!=0;
        return false;
    }

    TableInput ti;
    ArtLegController[] legs;
    ArticulationBody[] legAbs;
    Transform[] meshes;
    Transform[] balls;
    MeshRenderer[] ballRs;
    Joint[] joints;
    GameObject[] anchors;
    bool[] stuckDynamic;            // ★2026-09-05 다리별: 동적(비키네마틱) 강체(눈사람 머리·상자 등)에 붙었나 — 무게 상쇄 제외 판정
    bool[] prevLonger, prevShorter;
    readonly Collider[] overlap = new Collider[16];

    public void CheatUnlock() { unlocked = true; }

    /// <summary>[검증용] 지정 다리로 즉시 붙기 시도 — 성공 여부 반환.</summary>
    public bool DebugGrab(int i)
    {
        if (joints == null || i < 0 || i >= joints.Length || joints[i] != null) return false;
        TryGrab(i);
        return joints[i] != null;
    }

    /// <summary>이 키셋 다리의 공이 어딘가에 붙어 있는가(GiraffeMode가 신축 차단 판단에 사용).</summary>
    public bool IsStuck(LegControlSet set)
    {
        if (legs == null || joints == null) return false;
        for (int i = 0; i < legs.Length; i++)
            if (legs[i] != null && legs[i].controlSet == set) return joints[i] != null;
        return false;
    }

    void FixedUpdate()
    {
        // ★붙은 다리가 하나라도 있으면 무게 상쇄(게스트/비활성 제외). ★2026-09-05 동적 물체(눈사람 머리 등)를 잡고 있으면 상쇄 없음(사용자 요청)
        if (!active || netVisualOnly || allBodies == null || weightCancelWhenStuck <= 0f || StuckMask == 0 || AnyDynamicStuck) return;
        Vector3 lift = -Physics.gravity * weightCancelWhenStuck;
        foreach (var ab in allBodies)
            if (ab != null && ab.enabled && ab.useGravity) ab.AddForce(lift * ab.mass, ForceMode.Force);
    }

    void Start()
    {
        ti = TableInput.Of(this);
        legs = transform.root.GetComponentsInChildren<ArtLegController>(true);
        allBodies = transform.root.GetComponentsInChildren<ArticulationBody>(true);
        int n = legs.Length;
        legAbs = new ArticulationBody[n];
        meshes = new Transform[n];
        balls = new Transform[n];
        ballRs = new MeshRenderer[n];
        joints = new Joint[n];
        anchors = new GameObject[n];
        stuckDynamic = new bool[n];
        prevLonger = new bool[n];
        prevShorter = new bool[n];
        for (int i = 0; i < n; i++)
        {
            legAbs[i] = legs[i].GetComponent<ArticulationBody>();
            meshes[i] = legs[i].transform.childCount > 0 ? legs[i].transform.GetChild(0) : legs[i].transform;
            // ★2026-09-03 실제 공(사용자 요청): 다리 AB의 자식으로 붙인 SphereCollider — 벽/바닥을 뚫지 않고 진짜 공처럼 닿는다.
            //   피벗(스케일 1) 자식으로 두고 발끝 로컬 위치만 맞춤 — 메시 자식이면 기린 신축 스케일에 찌그러짐.
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.name = "TennisBall_" + i;
            b.layer = legs[i].gameObject.layer;
            b.transform.SetParent(legs[i].transform, false);
            b.transform.localScale = Vector3.one * (ballRadius * 2f);
            b.transform.localPosition = legs[i].transform.InverseTransformPoint(Tip(i));
            b.transform.localRotation = Quaternion.Inverse(legs[i].transform.rotation) * meshes[i].rotation;
            ballRs[i] = b.GetComponent<MeshRenderer>();
            if (ballMat != null) ballRs[i].sharedMaterial = ballMat;
            b.SetActive(false);
            balls[i] = b.transform;
        }
    }

    Vector3 Tip(int i) => meshes[i].TransformPoint(0f, -0.5f, 0f);

    /// <summary>공(콜라이더)을 발끝에 유지 — 기린 신축으로 발끝이 움직일 때만 로컬 위치 갱신(매 프레임 물리 형상 이동 방지).</summary>
    void KeepBallAtTip(int i)
    {
        Vector3 lp = legs[i].transform.InverseTransformPoint(Tip(i));
        if ((balls[i].localPosition - lp).sqrMagnitude > 1e-6f) balls[i].localPosition = lp;
    }

    void Update()
    {
        if (active && !netVisualOnly) unlocked = true;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (!netVisualOnly && unlocked && keyboard != null && keyboard.digit3Key.wasPressedThisFrame && AbilityHud.CanReadInput(this))
            SetActive(!active);

        if(active&&!netVisualOnly)AbilitySelection.Select(this);
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
            KeepBallAtTip(i);

            if (netVisualOnly)
            {
                // 게스트: 붙기 물리 없음 — 호스트가 보낸 붙음 마스크로 색만
                var wantG = (netStuckMask & (1 << i)) != 0 ? grabMat : ballMat;
                if (wantG != null && ballRs[i].sharedMaterial != wantG) ballRs[i].sharedMaterial = wantG;
                continue;
            }
            // ★2026-08-20 입력은 TableInput 경유(로컬 키보드=PollKeyset, 게스트=입력 RPC) — 멀티에서 게스트도 붙기 가능
            var inp = ti != null ? ti.Get(legs[i].controlSet) : default;
            bool grabNow = inp.grab;
            bool relNow = inp.release;
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
            j.enableCollision = false;   // 공 콜라이더와 물건이 서로 밀며 떨지 않게(조인트가 고정)
            joints[i] = j;
            stuckDynamic[i] = !best.attachedRigidbody.isKinematic;   // 키네마틱(광차 등)은 벽처럼 취급
        }
        else
        {
            // ★2026-08-20 밀착: 앵커를 '벽 표면의 최근접점'에 두고, ★2026-09-03 공 표면(발끝에서 벽 쪽으로 반지름만큼)을
            //   그 점에 맞추도록 connectedAnchor를 명시 — 실제 공 콜라이더가 벽에 딱 닿은 상태로 고정된다.
            Vector3 surface = best.ClosestPoint(tip);
            Vector3 toWall = surface - tip;
            Vector3 ballPt = toWall.sqrMagnitude > 1e-6f ? tip + toWall.normalized * ballRadius : tip;
            var a = new GameObject("TennisAnchor_" + i);
            a.transform.position = surface;
            a.transform.SetParent(best.transform, true);   // 움직이는 벽/발판도 추종
            var rb = a.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var j = a.AddComponent<FixedJoint>();
            j.autoConfigureConnectedAnchor = false;
            j.anchor = Vector3.zero;
            j.connectedArticulationBody = legAbs[i];
            j.connectedAnchor = legAbs[i].transform.InverseTransformPoint(ballPt);
            joints[i] = j;
            anchors[i] = a;
            stuckDynamic[i] = false;
        }
    }

    void Release(int i)
    {
        if (joints[i] != null) Destroy(joints[i]);
        joints[i] = null;
        if (stuckDynamic != null) stuckDynamic[i] = false;
        if (anchors[i] != null) Destroy(anchors[i]);
        anchors[i] = null;
    }

    /// <summary>★2026-09-04 모든 다리 놓기 — 리스폰/재시작 때 ArtSceneReset이 호출(붙은 채 순간이동하면 조인트가 테이블을 끌고 감).</summary>
    public void ReleaseAll()
    {
        if (joints == null) return;
        for (int i = 0; i < joints.Length; i++) Release(i);
    }

    void OnDisable()
    {
        if (legs == null) return;
        for (int i = 0; i < legs.Length; i++) Release(i);
    }
}
