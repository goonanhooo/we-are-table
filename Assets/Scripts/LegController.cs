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

    [Header("Chassis")]
    public string chassisName = "Table";

    Rigidbody rb;
    HingeJoint hinge;
    Transform legMesh;
    float yaw;
    bool frozen;   // 자기 키가 안 눌릴 때 힌지 각도를 잠가 상판과의 상대 관계 고정
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

        if (transform.childCount > 0) legMesh = transform.GetChild(0);
    }

    static void StabilizeBody(Rigidbody body)
    {
        body.maxAngularVelocity = 15f;          // 각속도 상한(폭주 방지)
        body.maxDepenetrationVelocity = 3f;     // 깊은 겹침 시 폭발적 분리 방지(핵심)
        body.solverIterations = 24;             // 관절 안정화
        body.solverVelocityIterations = 24;
    }

    void Update()
    {
        if (hinge == null) return;

        // 리셋 보정: holdTimer 동안 다리를 수직(각도 0)으로 강제 고정했다가 해제
        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;
            hinge.useMotor = false;
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
        bool hasInput = upP || downP || leftP || rightP;

        // 자기 키가 하나도 안 눌리면, 상판과의 상대 각도를 그대로 '잠근다'(힌지 한계를 현재 각으로 고정).
        // kinematic이 아니므로 전체(상판+다리)는 여전히 동적 → 다른 다리로 밀면 테이블이 움직일 수 있음.
        if (!hasInput)
        {
            if (!frozen)
            {
                float a = hinge.angle;
                hinge.useMotor = false;
                hinge.limits = new JointLimits { min = a - 0.05f, max = a + 0.05f };
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

        // 상/하 = 앞뒤 스윙 (방향 반전됨), 한계각 근처에서 부드럽게 감속
        float dir = (downP ? 1f : 0f) - (upP ? 1f : 0f);
        float v = dir * swingVelocity;
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
            yaw += spin * spinSpeed * Time.deltaTime;
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
