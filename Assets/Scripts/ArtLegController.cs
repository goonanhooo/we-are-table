using UnityEngine;

/// <summary>
/// ★ ArticulationBody 판 다리 컨트롤러 — 2관절 체인(2026-07-05 재설계).
/// 축소좌표(reduced-coordinate) 관절은 '관절 위치 오차'라는 개념 자체가 없어서
/// 하드 밴드 스냅·리밋 슬램·축 재설정 저크가 원천적으로 발생하지 않는다(튕김 근본 해결).
///
/// 관절 구성(다리마다 2단):
///   상판 → [LegYaw_XX] RevoluteJoint(축=상판 수직 Y, ★FreeMotion=360° 무제한)
///        → [Leg_XX_Pivot(이 컴포넌트)] RevoluteJoint(축=X 앞뒤 스윙, ±swingLimit)
///   → 좌우 키가 yaw를 돌리면 '스윙 축이 통째로' 따라 돈다 = 원게임 hinge.axis 재설정 거동.
///   90°로 든 다리는 완전히 평평한 원을 그리며 회전(프로토타입 실측: 5바퀴 연속·발끝 높이 변동 0.000m).
///   ⚠️구(舊) 구면 관절 1개 방식은 swing∘twist 분해라 원뿔 느낌 + yaw 한계 175 부근 특이점(스윙각 붕괴)
///     — 그래서 폐기(CLAUDE.md 참고).
/// 입력은 TableInput 경유(본게임과 동일 — 퍼저/멀티 재사용 가능).
/// LegYaw 바디가 없는 씬(구 구조)에선 스핀만 비활성(스윙은 동작) — 하위 호환.
/// </summary>
[RequireComponent(typeof(ArticulationBody))]
public class ArtLegController : MonoBehaviour
{
    [Tooltip("이 다리를 제어할 키 세트")]
    public LegControlSet controlSet = LegControlSet.WASD;

    [Header("Motor")]
    public float swingVelocity = 300f;   // 도/초
    public float motorForce = 600f;      // ⚠️체인 관절 힘 임계: fl~300 이하 무력 — 구 150의 4배가 동일 감각(2026-07-07 실측)
    public float idleHoldForce = 1300f;  // 구 320의 4배(체인 힘 임계 보정)
    [HideInInspector] public float forceMultiplier = 1f;   // 지라프 모드
    public float swingLimit = 90f;   // 90: 사용자 요청(2026-07-07) — 95는 ±94까지 넘어갔음. 힘 600이라 감속(stopMargin)에도 실질 ~90 도달
    public float stopMargin = 8f;    // 12→8: 한계각 앞 감속 구간 축소(90° 도달 빨라짐)
    [Tooltip("목표속도 램프(도/초²) — 계단 입력 반작용 저크 분산")]
    public float motorAccel = 4000f;

    [Header("Spin (yaw — 부모 LegYaw 바디, 수직축 무제한 회전)")]
    public float spinSpeed = 180f;       // 도/초
    [Tooltip("yaw 속도 드라이브 토크 상한")]
    public float yawForce = 600f;   // 실측: 600이면 목표 180°/s 정확 도달(체인 힘 임계 보정)
    [Tooltip("yaw 정지 유지(위치 드라이브) 강성/감쇠")]
    public float yawHoldStiffness = 2000f;
    public float yawHoldDamping = 60f;

    [Header("보조 키(선택) — 이 다리를 추가 키로도 조작 (본게임 Arrows 다리: [=위 '=아래 ;=왼쪽 오른쪽Enter=오른쪽)")]
    public UnityEngine.InputSystem.Key altUp = UnityEngine.InputSystem.Key.None;
    public UnityEngine.InputSystem.Key altDown = UnityEngine.InputSystem.Key.None;
    public UnityEngine.InputSystem.Key altLeft = UnityEngine.InputSystem.Key.None;
    public UnityEngine.InputSystem.Key altRight = UnityEngine.InputSystem.Key.None;

    [Header("Freeze (입력 없음 — 위치 드라이브 유지)")]
    [Tooltip("동결 위치 드라이브 강성(축소좌표라 스냅 없음 — 강해도 안전)")]
    public float holdStiffness = 3000f;
    public float holdDamping = 80f;

    ArticulationBody ab;
    ArticulationBody yawAb;   // 부모 LegYaw 바디(없으면 스핀 비활성 — 구 구조 호환)
    Transform legMesh;
    TableInput tableInput;
    float baseLegScaleY = 1f;
    float rampedV;       // 램프된 목표 각속도(도/초)
    bool frozen;
    bool yawFrozen;
    float holdTimer;

    public ArticulationBody Body => ab;

    void Awake()
    {
        ab = GetComponent<ArticulationBody>();
        tableInput = TableInput.Of(this);
        // 보조 키 등록(본게임 LegController와 동일 경로 — TableInput이 OR로 합침)
        if (tableInput != null) tableInput.RegisterAltKeys(controlSet, altUp, altDown, altLeft, altRight);
        if (transform.childCount > 0) { legMesh = transform.GetChild(0); baseLegScaleY = legMesh.localScale.y; }
        SetupJoint();

        // ★다리↔상판 충돌 무시(2026-07-05): PhysX는 '직접 연결' 링크만 자동 무시 —
        //   yaw 바디가 사이에 끼면서 다리가 상판과 충돌하게 돼 다리 윗부분이 상판에 끼어
        //   스윙이 ±1°로 잠겼음(실측). 구 구조(직결)의 자동 무시를 명시적으로 복원.
        var walk = transform.parent;
        ArticulationBody rootAb = null;
        while (walk != null)
        {
            var w = walk.GetComponent<ArticulationBody>();
            if (w != null && w.isRoot) { rootAb = w; break; }
            walk = walk.parent;
        }
        if (rootAb != null)
        {
            // ⚠️상판 콜라이더는 루트 GO가 아니라 자식 비주얼(TableTop)에 붙어 있다 —
            //   GetComponents(루트 GO만)로 걸면 무시 쌍이 0개 등록돼 90° 든 다리가
            //   yaw 스핀 ~81°에서 슬래브에 걸려 잼(2026-07-07 실측). '루트 바디에 속한'
            //   콜라이더 전부(=가장 가까운 부모 AB가 루트인 것)를 대상으로 무시.
            foreach (var boardCol in rootAb.GetComponentsInChildren<Collider>(true))
            {
                if (boardCol.GetComponentInParent<ArticulationBody>() != rootAb) continue;   // 다른 링크(다리) 소속 제외
                foreach (var myCol in GetComponentsInChildren<Collider>(true))
                    Physics.IgnoreCollision(myCol, boardCol, true);
            }
        }
    }

    /// <summary>관절/드라이브 초기화(빌더 재호출 가능). 부모 LegYaw 바디도 함께 구성.</summary>
    public void SetupJoint()
    {
        if (ab == null) ab = GetComponent<ArticulationBody>();

        // ── 다리 스윙(이 바디): Revolute X ±swingLimit
        ab.jointType = ArticulationJointType.RevoluteJoint;
        ab.matchAnchors = false;                           // ⚠️명시 앵커 — 재부모화 후 낡은 부모앵커가 남으면 관절이 강체로 잠김(실측)
        ab.anchorPosition = Vector3.zero;                  // 피벗(다리 상단) 기준
        ab.anchorRotation = Quaternion.identity;           // twist=X(앞뒤 스윙)
        ab.parentAnchorPosition = Vector3.zero;            // 부모(LegYaw) 원점 = 피벗 위치(로컬 identity)
        ab.parentAnchorRotation = Quaternion.identity;
        ab.twistLock = ArticulationDofLock.LimitedMotion;  // ⚠️리밋 적용엔 LimitedMotion 필수(CLAUDE.md)
        ab.linearLockX = ArticulationDofLock.LockedMotion;
        ab.linearLockY = ArticulationDofLock.LockedMotion;
        ab.linearLockZ = ArticulationDofLock.LockedMotion;

        var xd = ab.xDrive;
        xd.lowerLimit = -swingLimit; xd.upperLimit = swingLimit;
        xd.stiffness = 0f; xd.damping = 120f; xd.forceLimit = motorForce;
        xd.target = 0f; xd.targetVelocity = 0f;
        ab.xDrive = xd;

        ab.maxAngularVelocity = 15f;
        ab.maxLinearVelocity = 30f;
        ab.maxDepenetrationVelocity = 2f;

        // ── 부모 yaw(LegYaw 바디): Revolute, 축=상판 로컬 Y(수직), 무제한
        var p = transform.parent != null ? transform.parent.GetComponent<ArticulationBody>() : null;
        yawAb = (p != null && !p.isRoot) ? p : null;   // 부모가 상판(root)이면 구 구조 — 스핀 없음
        if (yawAb != null)
        {
            yawAb.jointType = ArticulationJointType.RevoluteJoint;
            yawAb.matchAnchors = false;                              // 명시 앵커(위와 동일 이유)
            yawAb.anchorPosition = Vector3.zero;
            yawAb.anchorRotation = Quaternion.Euler(0f, 0f, 90f);   // 회전축 X → 부모(상판) Y(수직)
            yawAb.parentAnchorPosition = yawAb.transform.localPosition;   // 상판 프레임에서의 피벗 위치
            yawAb.parentAnchorRotation = yawAb.transform.localRotation * Quaternion.Euler(0f, 0f, 90f);
            yawAb.twistLock = ArticulationDofLock.FreeMotion;        // ★360° 무제한(관절각 무한 누적)
            yawAb.linearLockX = ArticulationDofLock.LockedMotion;
            yawAb.linearLockY = ArticulationDofLock.LockedMotion;
            yawAb.linearLockZ = ArticulationDofLock.LockedMotion;
            var yd = yawAb.xDrive;
            yd.stiffness = 0f; yd.damping = 120f; yd.forceLimit = yawForce;
            yd.target = 0f; yd.targetVelocity = 0f;
            yawAb.xDrive = yd;
            yawAb.maxAngularVelocity = 15f;
            yawAb.maxLinearVelocity = 30f;
            yawAb.maxDepenetrationVelocity = 2f;
        }
    }

    /// <summary>현재 스윙 각도(도) — jointPosition[0] = twist(rad).</summary>
    public float SwingAngle => ab != null && ab.jointPosition.dofCount > 0 ? ab.jointPosition[0] * Mathf.Rad2Deg : 0f;

    /// <summary>현재 yaw 각도(도, 무제한 누적).</summary>
    public float YawAngle => yawAb != null && yawAb.jointPosition.dofCount > 0 ? yawAb.jointPosition[0] * Mathf.Rad2Deg : 0f;

    void Update()
    {
        if (ab == null) return;

        // 리셋 보정: holdTimer 동안 수직(0도) 강제 유지
        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            DriveHold(0f);
            YawHold(0f);
            return;
        }

        if (tableInput == null) tableInput = TableInput.Of(this);
        LegInput inp = tableInput != null ? tableInput.Get(controlSet) : default;
        bool swingInput = inp.up || inp.down;
        bool spinInput = inp.left || inp.right;

        float angle = SwingAngle;

        // 지라프: 다리 길이에 반비례한 각속도(발끝 선속도 일정) — 본게임과 동일 공식
        float lenScale = (legMesh != null && legMesh.localScale.y > 1e-4f)
            ? Mathf.Clamp(baseLegScaleY / legMesh.localScale.y, 0.12f, 1f) : 1f;

        // ★스톨 토크 보정(2026-07-07): 속도 드라이브의 실토크 = damping × 속도오차(rad/s)이고
        //   forceLimit은 상한일 뿐이다. 일반 모드는 300°/s(5.2rad/s)×120 ≈ 628로 상한 600에 닿지만,
        //   기린 최대신장은 lenScale이 목표속도를 43°/s로 줄여 스톨 토크가 ~90까지 추락(1560 상한 무의미)
        //   → 사다리꼴로 벌린 큰 걸음을 '오므릴' 수 없었음(다리가 하중에 밀림). damping을
        //   forceMultiplier/lenScale로 키워 어느 길이·배수에서도 스톨 토크가 forceLimit에 도달하게 한다.
        //   일반 모드(mult 1, lenScale 1)=120 그대로 → 기존 감각 불변. 옛 힌지 모터(스톨 풀토크)와 파리티.
        float driveDamping = Mathf.Clamp(120f * forceMultiplier / lenScale, 120f, 3000f);

        // ── 스윙 축: 원본 속도 드라이브(감각 보존). ⚠️★체인 관절 힘 임계(2026-07-07 실측):
        //   이 2단 체인에선 forceLimit ~300 이하 드라이브가 사실상 무력(150=완전 정지, 600=정상).
        //   그래서 motorForce/idleHoldForce/yawForce 전부 구값의 4배로 상향(600/1300/600) — 감각은 동일.
        if (!swingInput)
        {
            // 동결 = 위치 드라이브로 현재 각 유지(스핀만 눌러도 스윙각 유지 — 90° 든 채 평평하게 돎)
            if (!frozen)
            {
                DriveHold(angle);
                rampedV = 0f;
                frozen = true;
            }
        }
        else
        {
            if (frozen)
            {
                var xr = ab.xDrive;
                xr.stiffness = 0f; xr.damping = driveDamping;
                xr.forceLimit = motorForce * forceMultiplier;
                ab.xDrive = xr;
                rampedV = 0f;
                frozen = false;
            }

            // 한계각 근처 감속(stopMargin) + 램프(motorAccel)
            float dir = (inp.down ? 1f : 0f) - (inp.up ? 1f : 0f);
            float v = dir * swingVelocity * lenScale;
            if (v > 0f) v *= Mathf.Clamp01((swingLimit - angle) / stopMargin);
            if (v < 0f) v *= Mathf.Clamp01((angle + swingLimit) / stopMargin);
            rampedV = Mathf.MoveTowards(rampedV, v, motorAccel * Time.deltaTime);

            var xd = ab.xDrive;
            xd.stiffness = 0f;
            xd.damping = driveDamping;
            xd.forceLimit = motorForce * forceMultiplier;
            xd.targetVelocity = rampedV;   // ⚠️ArticulationDrive target/targetVelocity는 '도' 단위(jointPosition만 라디안)
            ab.xDrive = xd;
        }

        // ── yaw 축(부모 LegYaw): 좌우 키 = 속도 드라이브(무제한 회전), 무입력 = 현재 각 위치 유지
        if (yawAb == null) return;
        if (spinInput)
        {
            float spin = (inp.left ? 1f : 0f) - (inp.right ? 1f : 0f);
            var yd = yawAb.xDrive;
            yd.stiffness = 0f; yd.damping = driveDamping;   // 스톨 토크 보정(위 주석) — 기린 접지 스핀도 동일
            yd.forceLimit = yawForce * forceMultiplier;
            yd.targetVelocity = spin * spinSpeed * lenScale;
            yawAb.xDrive = yd;
            yawFrozen = false;
        }
        else if (!yawFrozen)
        {
            YawHold(YawAngle);
            yawFrozen = true;
        }
    }

    void DriveHold(float angleDeg)
    {
        var xd = ab.xDrive;
        xd.stiffness = holdStiffness;
        xd.damping = holdDamping;
        xd.forceLimit = idleHoldForce * Mathf.Min(forceMultiplier, 1.5f);
        xd.target = angleDeg;   // 도 단위
        xd.targetVelocity = 0f;
        ab.xDrive = xd;
    }

    void YawHold(float angleDeg)
    {
        if (yawAb == null) return;
        var yd = yawAb.xDrive;
        yd.stiffness = yawHoldStiffness;
        yd.damping = yawHoldDamping;
        yd.forceLimit = yawForce;
        yd.target = angleDeg;
        yd.targetVelocity = 0f;
        yawAb.xDrive = yd;
    }

    public void HoldVertical(float seconds) { holdTimer = seconds; frozen = false; yawFrozen = false; }

    /// <summary>관절 각도/속도/드라이브를 0으로(리셋) — 스윙+yaw 둘 다.</summary>
    public void ResetState()
    {
        rampedV = 0f; frozen = false; yawFrozen = false;
        if (ab == null) return;
        if (ab.jointPosition.dofCount > 0)
        {
            var jp = ab.jointPosition; for (int i = 0; i < jp.dofCount; i++) jp[i] = 0f; ab.jointPosition = jp;
            var jv = ab.jointVelocity; for (int i = 0; i < jv.dofCount; i++) jv[i] = 0f; ab.jointVelocity = jv;
        }
        var xd = ab.xDrive; xd.target = 0f; xd.targetVelocity = 0f; ab.xDrive = xd;
        if (yawAb != null)
        {
            if (yawAb.jointPosition.dofCount > 0)
            {
                var jp = yawAb.jointPosition; for (int i = 0; i < jp.dofCount; i++) jp[i] = 0f; yawAb.jointPosition = jp;
                var jv = yawAb.jointVelocity; for (int i = 0; i < jv.dofCount; i++) jv[i] = 0f; yawAb.jointVelocity = jv;
            }
            var yd = yawAb.xDrive; yd.target = 0f; yd.targetVelocity = 0f; yawAb.xDrive = yd;
        }
    }
}
