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
    public float motorForce = 150f;      // 토크 상한 (2026-07-05 80→150: 하중 하 90° 확실 도달 — 슬램/기린 스트레스 폭발0 검증)
    public float idleHoldForce = 320f;   // 정지 유지 토크 상한 (220→320: 서있을 때 다리 버팀 강화)
    [HideInInspector] public float forceMultiplier = 1f;   // 지라프 모드
    public float swingLimit = 95f;   // 95: 감속(stopMargin)+하중을 이기고 실질 90° 도달 보장
    public float stopMargin = 8f;    // 12→8: 한계각 앞 감속 구간 축소(90° 도달 빨라짐)
    [Tooltip("목표속도 램프(도/초²) — 계단 입력 반작용 저크 분산")]
    public float motorAccel = 4000f;

    [Header("Spin (yaw — 부모 LegYaw 바디, 수직축 무제한 회전)")]
    public float spinSpeed = 180f;       // 도/초
    [Tooltip("yaw 속도 드라이브 토크 상한")]
    public float yawForce = 350f;   // 실측: 120으론 19°/s(힘 부족), 350이면 목표 180°/s 도달
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
    }

    /// <summary>관절/드라이브 초기화(빌더 재호출 가능). 부모 LegYaw 바디도 함께 구성.</summary>
    public void SetupJoint()
    {
        if (ab == null) ab = GetComponent<ArticulationBody>();

        // ── 다리 스윙(이 바디): Revolute X ±swingLimit
        ab.jointType = ArticulationJointType.RevoluteJoint;
        ab.anchorPosition = Vector3.zero;                  // 피벗(다리 상단) 기준
        ab.anchorRotation = Quaternion.identity;           // twist=X(앞뒤 스윙)
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
            yawAb.anchorPosition = Vector3.zero;
            yawAb.anchorRotation = Quaternion.Euler(0f, 0f, 90f);   // 회전축 X → 부모(상판) Y(수직)
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

        // ── 스윙 축
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
                xr.stiffness = 0f; xr.damping = 120f;
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
            xd.damping = 120f;
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
            yd.stiffness = 0f; yd.damping = 120f;
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
