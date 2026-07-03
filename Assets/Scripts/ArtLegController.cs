using UnityEngine;

/// <summary>
/// ★ ArticulationBody 판 다리 컨트롤러 (LegController의 축소좌표 버전 — ArticulationTest 씬).
/// 축소좌표(reduced-coordinate) 관절은 '관절 위치 오차'라는 개념 자체가 없어서
/// 하드 밴드 스냅·리밋 슬램·축 재설정 저크가 원천적으로 발생하지 않는다(튕김 근본 해결).
///
/// 관절 구성: SphericalJoint —
///   · twist(X)  = 앞뒤 스윙: ±swingLimit 리밋 + 속도 드라이브(damping 기반, forceLimit=motorForce)
///   · swingY(Y) = 스핀(yaw): ±yawLimit 리밋 + 위치 드라이브(targetYaw)
///     ⚠️ PhysX 분해 순서(swing∘twist) 덕에 yaw가 트위스트(스윙) 축을 자연히 회전 —
///        원게임 'hinge.axis 재설정'과 동일 거동을 축 재설정 없이 얻는다(무제한 회전만 ±yawLimit로 제한).
///   · swingZ    = 잠금
/// 입력은 TableInput 경유(본게임과 동일 — 퍼저/멀티 재사용 가능).
/// 체감 매칭: swingVelocity/motorForce/idleHoldForce/stopMargin/motorAccel(램프)/lenScale(지라프) 전부 미러링.
/// </summary>
[RequireComponent(typeof(ArticulationBody))]
public class ArtLegController : MonoBehaviour
{
    [Tooltip("이 다리를 제어할 키 세트")]
    public LegControlSet controlSet = LegControlSet.WASD;

    [Header("Motor (본게임 LegController와 동일 수치)")]
    public float swingVelocity = 300f;   // 도/초
    public float motorForce = 80f;       // 토크 상한
    public float idleHoldForce = 220f;   // 정지 유지 토크 상한
    [HideInInspector] public float forceMultiplier = 1f;   // 지라프 모드
    public float swingLimit = 90f;
    public float stopMargin = 12f;
    [Tooltip("목표속도 램프(도/초²) — 계단 입력 반작용 저크 분산")]
    public float motorAccel = 4000f;

    [Header("Spin (yaw)")]
    public float spinSpeed = 180f;
    [Tooltip("yaw 회전 한계(±도). 스피리컬 스윙 분해 특성상 무제한 회전 불가 — 방향 조정 용도로 충분")]
    public float yawLimit = 175f;
    [Tooltip("yaw 위치 드라이브 강성/감쇠/힘상한")]
    public float yawStiffness = 600f;
    public float yawDamping = 30f;
    public float yawForce = 120f;

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
    Transform legMesh;
    TableInput tableInput;
    float baseLegScaleY = 1f;
    float rampedV;       // 램프된 목표 각속도(도/초)
    float targetYaw;
    bool frozen;
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

    /// <summary>관절/드라이브 초기화(빌더 재호출 가능).</summary>
    public void SetupJoint()
    {
        if (ab == null) ab = GetComponent<ArticulationBody>();
        ab.jointType = ArticulationJointType.SphericalJoint;
        ab.anchorPosition = Vector3.zero;                  // 피벗(다리 상단) 기준
        ab.anchorRotation = Quaternion.identity;           // twist=X(앞뒤 스윙), swingY=Y(yaw)
        ab.twistLock = ArticulationDofLock.LimitedMotion;  // ⚠️리밋 적용엔 LimitedMotion 필수(CLAUDE.md)
        ab.swingYLock = ArticulationDofLock.LimitedMotion;
        ab.swingZLock = ArticulationDofLock.LockedMotion;
        ab.linearLockX = ArticulationDofLock.LockedMotion;
        ab.linearLockY = ArticulationDofLock.LockedMotion;
        ab.linearLockZ = ArticulationDofLock.LockedMotion;

        var xd = ab.xDrive;
        xd.lowerLimit = -swingLimit; xd.upperLimit = swingLimit;
        xd.stiffness = 0f; xd.damping = 120f; xd.forceLimit = motorForce;
        xd.target = 0f; xd.targetVelocity = 0f;
        ab.xDrive = xd;

        var yd = ab.yDrive;
        yd.lowerLimit = -yawLimit; yd.upperLimit = yawLimit;
        yd.stiffness = yawStiffness; yd.damping = yawDamping; yd.forceLimit = yawForce;
        yd.target = 0f; yd.targetVelocity = 0f;
        ab.yDrive = yd;

        ab.maxAngularVelocity = 15f;
        ab.maxLinearVelocity = 30f;
        ab.maxDepenetrationVelocity = 2f;
    }

    /// <summary>현재 스윙 각도(도) — jointPosition[0] = twist(rad).</summary>
    public float SwingAngle => ab != null && ab.jointPosition.dofCount > 0 ? ab.jointPosition[0] * Mathf.Rad2Deg : 0f;

    void Update()
    {
        if (ab == null) return;

        // 리셋 보정: holdTimer 동안 수직(0도) 강제 유지
        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            DriveHold(0f);
            return;
        }

        if (tableInput == null) tableInput = TableInput.Of(this);
        LegInput inp = tableInput != null ? tableInput.Get(controlSet) : default;
        bool hasInput = inp.up || inp.down || inp.left || inp.right;

        float angle = SwingAngle;

        if (!hasInput)
        {
            // 동결 = 위치 드라이브로 현재 각 유지. 축소좌표라 '밴드 위반→스냅'이 존재하지 않음 —
            // 다른 다리가 몸을 밀면 드라이브가 연속적인 토크로 저항(C1), 오차 누적도 없음.
            if (!frozen)
            {
                DriveHold(angle);
                rampedV = 0f;
                frozen = true;
            }
            return;
        }

        if (frozen)
        {
            // 해제: 속도 드라이브 복귀
            var xr = ab.xDrive;
            xr.stiffness = 0f; xr.damping = 120f;
            xr.forceLimit = motorForce * forceMultiplier;
            ab.xDrive = xr;
            rampedV = 0f;
            frozen = false;
        }

        // 지라프: 다리 길이에 반비례한 각속도(발끝 선속도 일정) — 본게임과 동일 공식
        float lenScale = (legMesh != null && legMesh.localScale.y > 1e-4f)
            ? Mathf.Clamp(baseLegScaleY / legMesh.localScale.y, 0.12f, 1f) : 1f;

        // 스윙: 한계각 근처 감속(stopMargin) + 램프(motorAccel)
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

        // 스핀(yaw): 위치 드라이브 target을 spinSpeed로 증감 — 축 재설정 없음(분해 순서가 처리)
        float spin = (inp.left ? 1f : 0f) - (inp.right ? 1f : 0f);
        if (spin != 0f)
        {
            targetYaw = Mathf.Clamp(targetYaw + spin * spinSpeed * lenScale * Time.deltaTime, -yawLimit, yawLimit);
            var yd = ab.yDrive;
            yd.target = targetYaw;                     // 도 단위
            ab.yDrive = yd;
            // 메시는 시각적으로 yaw를 따라 돌 필요 없음 — 관절이 실제로 돌리므로(원게임과 다른 점: 진짜 회전)
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

    public void HoldVertical(float seconds) { holdTimer = seconds; frozen = false; }

    /// <summary>관절 각도/속도/드라이브를 0으로(리셋).</summary>
    public void ResetState()
    {
        targetYaw = 0f; rampedV = 0f; frozen = false;
        if (ab == null) return;
        if (ab.jointPosition.dofCount > 0)
        {
            var jp = ab.jointPosition; for (int i = 0; i < jp.dofCount; i++) jp[i] = 0f; ab.jointPosition = jp;
            var jv = ab.jointVelocity; for (int i = 0; i < jv.dofCount; i++) jv[i] = 0f; ab.jointVelocity = jv;
        }
        var xd = ab.xDrive; xd.target = 0f; xd.targetVelocity = 0f; ab.xDrive = xd;
        var yd = ab.yDrive; yd.target = 0f; ab.yDrive = yd;
    }
}
