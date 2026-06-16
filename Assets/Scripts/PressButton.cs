using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 무게로 눌리는 물리 버튼 (재사용 컴포넌트).
///
/// - 버튼 캡(이 Rigidbody)은 Y축으로만 움직이며, 약한 스프링으로 원위치(restY)로 복귀한다.
///   스프링이 약해서 '작은 무게'로도 눌린다. 위에서 무언가 누르면 아래로 내려가고, 치우면 올라온다.
/// - 아래로 pressDepth 이상 내려가면 '눌림(IsPressed=true)'으로 판정하고 onPressed 이벤트를 호출한다.
///   다시 올라오면 onReleased를 호출한다.
/// - 바닥 스토퍼(예: 홈 바닥 콜라이더)가 캡을 막아 무한히 내려가지 않게 한다.
///
/// 사용법: 작은 박스(콜라이더+Rigidbody)에 이 스크립트를 붙이면 버튼이 된다. (프리팹: Assets/Prefabs/PressButton.prefab)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PressButton : MonoBehaviour
{
    [Tooltip("이만큼(미터) 내려가면 '눌림'으로 판정")]
    public float pressDepth = 0.012f;

    [Tooltip("복귀 스프링 세기(N/m) — 작을수록 더 작은 무게로도 눌림")]
    public float springForce = 10f;

    [Tooltip("복귀 감쇠 — 출렁임 방지")]
    public float damping = 2f;

    [Header("Events")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    /// <summary>현재 눌려 있는지</summary>
    public bool IsPressed { get; private set; }

    Rigidbody rb;
    float restY;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionX
                       | RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotation;
        restY = transform.position.y;   // 눌리지 않은 기준 위치(시작 위치)
    }

    void FixedUpdate()
    {
        float y = transform.position.y;
        float drop = restY - y;                 // 눌린 깊이(>0)
        float vy = rb.linearVelocity.y;

        // 약한 스프링으로 restY 복귀 (위로 미는 힘) - 감쇠
        float force = (restY - y) * springForce - vy * damping;
        rb.AddForce(0f, force, 0f, ForceMode.Force);

        bool pressed = drop >= pressDepth;
        if (pressed != IsPressed)
        {
            IsPressed = pressed;
            if (pressed) onPressed?.Invoke();
            else onReleased?.Invoke();
        }
    }
}
