using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>다리에 매핑되는 키 세트.</summary>
public enum LegControlSet
{
    WASD = 0,   // 상/하 = W / S,  좌/우 = A / D
    TFGH = 1,   // 상/하 = T / G,  좌/우 = F / H
    IJKL = 2,   // 상/하 = I / K,  좌/우 = J / L
    Arrows = 3, // 상/하 = ↑ / ↓,  좌/우 = ← / →
}

/// <summary>
/// 다리(관절 피벗)를 물리 관절 모터로 구동한다. (안정화 버전)
///
///   - 상/하 키: 속도 모터로 앞/뒤 스윙. 한계각 근처에서 속도를 부드럽게 0으로 줄여 들이받지 않음(폭발 방지).
///   - 자기 키가 하나도 안 눌리면 힌지 각도를 잠가 상판과의 상대 관계를 고정(독립).
///     (kinematic이 아니므로 전체는 동적 — 다른 다리로 밀면 테이블이 움직일 수 있음)
///   - 좌/우 키: 다리를 긴 축(로컬 Y)으로 회전(yaw)시키고 스윙 축도 같이 회전 → 향한 방향으로 스윙.
///   - Rigidbody 솔버 반복/감속 상한을 높여 무거운 관절 구조의 수치 폭발을 막는다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LegController : MonoBehaviour
{
    [Tooltip("이 다리를 제어할 키 세트")]
    public LegControlSet controlSet = LegControlSet.WASD;

    [Header("Motor (상/하 = 앞뒤 스윙)")]
    [Tooltip("모터 목표 각속도(도/초).")]
    public float swingVelocity = 300f;
    [Tooltip("모터 힘(토크).")]
    public float motorForce = 60f;
    [Tooltip("스윙 각도 제한(도): 초기 기준 ±")]
    public float swingLimit = 90f;
    [Tooltip("한계각 앞에서 속도를 줄이기 시작하는 여유 각도(도)")]
    public float stopMargin = 12f;
    [Tooltip("힌지 회전축(로컬). (1,0,0) = 앞뒤 스윙")]
    public Vector3 hingeAxis = new Vector3(1f, 0f, 0f);

    [Header("Spin (좌/우 = 긴 축 기준 회전, 스윙 방향도 같이 회전)")]
    [Tooltip("다리를 자기 긴 축(로컬 Y) 기준으로 돌리는 속도(도/초)")]
    public float spinSpeed = 180f;

    [Header("보조 키(선택) — 이 다리를 추가 키로도 조작 (Key.None = 미사용)")]
    public Key altUp = Key.None;
    public Key altDown = Key.None;
    public Key altLeft = Key.None;
    public Key altRight = Key.None;

    [Header("Chassis")]
    public string chassisName = "Table";

    Rigidbody rb;
    HingeJoint hinge;
    Transform legMesh;
    float baseLegScaleY = 1f;   // 초기 다리 길이(스케일.y) — 길어지면 각속도를 반비례로 줄여 발끝 속도 일정
    float yaw;
    bool frozen;     // 입력 없을 때 현재 각도로 하드 고정(상판과의 상대 각도 잠금 → 안 휨)
    float holdTimer; // >0 동안 다리를 수직(각도 0)으로 강제 고정 (리셋 보정용)

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        StabilizeBody(rb);

        Rigidbody chassis = null;
        var go = GameObject.Find(chassisName);
        if (go != null) chassis = go.GetComponent<Rigidbody>();
        if (chassis != null) StabilizeBody(chassis);

        hinge = GetComponent<HingeJoint>();
        if (hinge == null) hinge = gameObject.AddComponent<HingeJoint>();

        hinge.connectedBody = chassis;
        hinge.autoConfigureConnectedAnchor = true;
        hinge.anchor = Vector3.zero;
        hinge.axis = hingeAxis.normalized;
        hinge.useSpring = false;
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -swingLimit, max = swingLimit };
        hinge.useMotor = true;

        var m = hinge.motor;
        m.force = motorForce;
        m.targetVelocity = 0f;
        m.freeSpin = false;
        hinge.motor = m;

        if (transform.childCount > 0) { legMesh = transform.GetChild(0); baseLegScaleY = legMesh.localScale.y; }
    }

    static void StabilizeBody(Rigidbody body)
    {
        body.maxAngularVelocity = 15f;          // 각속도 상한(폭주 방지)
        body.maxLinearVelocity = 30f;           // 선속도 상한 → 솔버 발산 시 멀리 튀어나가 worldAABB 폭발하는 것 차단(핵심)
        body.maxDepenetrationVelocity = 3f;     // 깊은 겹침 시 폭발적 분리 방지(핵심)
        body.solverIterations = 40;             // 관절 안정화(하드 고정 떨림 억제 — 절충값)
        body.solverVelocityIterations = 40;
        body.interpolation = RigidbodyInterpolation.Interpolate;  // 렌더링 보간 → 미세 떨림 시각적으로 부드럽게(물리/체감 불변)
    }

    void Update()
    {
        if (hinge == null) return;

        // 리셋 보정: holdTimer 동안 다리를 수직(각도 0)으로 강제 고정했다가 해제
        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            hinge.useMotor = false;
            hinge.useSpring = false;
            hinge.limits = new JointLimits { min = -0.1f, max = 0.1f }; // 0도 부근으로 잠금 = 수직
            if (rb != null) rb.angularVelocity = Vector3.zero;
            if (holdTimer <= 0f)
            {
                // 해제: 정상 한계/모터 복원
                hinge.limits = new JointLimits { min = -swingLimit, max = swingLimit };
                hinge.useMotor = true;
                frozen = false;
            }
            return; // 고정 중에는 입력 무시
        }

        var kb = Keyboard.current;

        Key up, down, left, right;
        switch (controlSet)
        {
            case LegControlSet.TFGH:   up = Key.T;       down = Key.G;         left = Key.F;         right = Key.H;          break;
            case LegControlSet.IJKL:   up = Key.I;       down = Key.K;         left = Key.J;         right = Key.L;          break;
            case LegControlSet.Arrows: up = Key.UpArrow; down = Key.DownArrow; left = Key.LeftArrow; right = Key.RightArrow; break;
            default:                   up = Key.W;       down = Key.S;         left = Key.A;         right = Key.D;          break; // WASD
        }

        // 이 다리에 해당하는 키들의 입력 상태
        bool upP    = kb != null && kb[up].isPressed;
        bool downP  = kb != null && kb[down].isPressed;
        bool leftP  = kb != null && kb[left].isPressed;
        bool rightP = kb != null && kb[right].isPressed;
        // 보조 키(설정된 경우 OR로 추가) → 같은 다리를 추가 키로도 조작
        if (kb != null)
        {
            if (altUp    != Key.None && kb[altUp].isPressed)    upP    = true;
            if (altDown  != Key.None && kb[altDown].isPressed)  downP  = true;
            if (altLeft  != Key.None && kb[altLeft].isPressed)  leftP  = true;
            if (altRight != Key.None && kb[altRight].isPressed) rightP = true;
        }
        bool hasInput = upP || downP || leftP || rightP;

        // 자기 키가 하나도 안 눌리면, 상판과의 상대 각도를 그대로 '잠근다'(힌지 한계를 현재 각으로 고정).
        // kinematic이 아니므로 전체(상판+다리)는 여전히 동적 → 다른 다리로 밀면 테이블이 움직일 수 있음.
        if (!hasInput)
        {
            // 입력 없음: 현재 각도로 '하드 고정'(힌지 한계를 현재 각 ±작은 폭으로 잠금) → 상판과의 상대 각도 고정.
            // 브레이크 모터(유한 힘)는 다른 다리가 밀면 조금씩 밀려 휘므로, 안 휘게 하려면 한계로 딱 잠근다.
            // bounciness=0 + 미세 타임스텝/솔버/인터폴레이션으로 하중 시 떨림을 억제.
            if (!frozen)
            {
                float a = hinge.angle;
                hinge.useMotor = false;
                hinge.useSpring = false;
                hinge.limits = new JointLimits { min = a - 0.05f, max = a + 0.05f, bounciness = 0f, bounceMinVelocity = 0f, contactDistance = 0f };
                if (rb != null) rb.angularVelocity = Vector3.zero;
                frozen = true;
            }
            return;
        }
        // 자기 키가 눌리면 잠금 해제: 한계를 ±스윙으로 되돌리고 모터 재가동
        if (frozen)
        {
            hinge.limits = new JointLimits { min = -swingLimit, max = swingLimit };
            hinge.useMotor = true;
            frozen = false;
        }

        // 다리가 길수록(지라프 모드) 각속도를 길이에 반비례 → '발끝 선속도'를 일정하게 유지.
        // (긴 다리를 일반 각속도로 돌리면 각운동량이 폭증해 한계각을 오버슈트하고 정지 다리가 흔들려 접힘.)
        float lenScale = (legMesh != null && legMesh.localScale.y > 1e-4f)
            ? Mathf.Clamp(baseLegScaleY / legMesh.localScale.y, 0.12f, 1f) : 1f;

        // 상/하 = 앞뒤 스윙 (방향 반전됨), 한계각 근처에서 부드럽게 감속
        float dir = (downP ? 1f : 0f) - (upP ? 1f : 0f);
        float v = dir * swingVelocity * lenScale;
        float angle = hinge.angle;
        if (v > 0f) v *= Mathf.Clamp01((swingLimit - angle) / stopMargin);
        if (v < 0f) v *= Mathf.Clamp01((angle + swingLimit) / stopMargin);

        var motor = hinge.motor;
        motor.force = motorForce;
        motor.targetVelocity = v;
        hinge.motor = motor;

        // 좌/우 = 다리를 긴 축(Y)으로 회전 + 스윙 축도 같이 회전
        float spin = (leftP ? 1f : 0f) - (rightP ? 1f : 0f);
        if (spin != 0f)
        {
            yaw += spin * spinSpeed * lenScale * Time.deltaTime;
            if (legMesh != null) legMesh.localRotation = Quaternion.Euler(0f, yaw, 0f);
            hinge.axis = (Quaternion.AngleAxis(yaw, Vector3.up) * hingeAxis).normalized;
        }
    }

    /// <summary>지정 시간(초) 동안 다리를 수직(각도 0)으로 강제 고정한다. (리셋 직후 안착 보정용)</summary>
    public void HoldVertical(float seconds)
    {
        holdTimer = seconds;
    }

    /// <summary>다리 내부 상태(yaw, 힌지 축/한계/모터, 잠금, 메시 회전)를 초기로 되돌린다.</summary>
    public void ResetState()
    {
        yaw = 0f;
        frozen = false;
        if (legMesh != null) legMesh.localRotation = Quaternion.identity;
        if (hinge != null)
        {
            hinge.axis = hingeAxis.normalized;
            hinge.useSpring = false;
            hinge.useMotor = true;
            hinge.limits = new JointLimits { min = -swingLimit, max = swingLimit };
            var m = hinge.motor;
            m.force = motorForce;
            m.targetVelocity = 0f;
            hinge.motor = m;
        }
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
