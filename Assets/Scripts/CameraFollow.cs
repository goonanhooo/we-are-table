using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3인칭 추적 카메라.
/// - 지정한 대상(target)을 따라다니며, 마우스 이동으로 시점(궤도)을 회전한다.
/// - 일반적인 3D 게임의 3인칭 카메라처럼 동작한다.
/// - ※ 플레이어(대상 오브젝트) 자체의 이동 로직은 의도적으로 전혀 포함하지 않는다.
/// - New Input System(Mouse.current)을 사용한다. (이 프로젝트는 New Input System 전용)
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("따라갈 대상. 비어 있으면 시작 시 targetName 이름의 오브젝트를 찾는다.")]
    public Transform target;
    public string targetName = "Table";

    [Header("Framing")]
    [Tooltip("대상으로부터 카메라까지의 거리")]
    public float distance = 6f;
    [Tooltip("대상 피벗의 높이 오프셋(대상의 중심보다 위를 바라보게 함)")]
    public float height = 1.5f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.12f;
    public float minPitch = -10f;
    public float maxPitch = 75f;
    [Tooltip("시작 시 커서를 잠글지 여부. (ESC는 PauseMenu 일시정지가 담당)")]
    public bool lockCursor = true;

    // 휠 줌(상수 — 모든 씬 공통). distance를 이 범위로 조절.
    const float ZoomSpeed = 0.01f;
    const float MinDistance = 2.5f;
    const float MaxDistance = 14f;

    float yaw;
    float pitch = 20f;

    void Start()
    {
        if (target == null && !string.IsNullOrEmpty(targetName))
        {
            GameObject go = GameObject.Find(targetName);
            if (go != null) target = go.transform;
        }

        Vector3 e = transform.eulerAngles;
        yaw = e.y;

        SetCursor(lockCursor);
    }

    void LateUpdate()
    {
        // ESC는 PauseMenu(일시정지)가 담당. 여기서 커서를 건드리지 않는다.
        if (target == null) return;

        // 커서가 잠겨 있을 때만 마우스로 시점 회전
        Mouse mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * mouseSensitivity;
            pitch -= d.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // 휠 줌(커서 잠금 여부와 무관하게 동작)
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                distance = Mathf.Clamp(distance - scroll * ZoomSpeed, MinDistance, MaxDistance);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * height;
        Vector3 pos = pivot - rot * Vector3.forward * distance;
        transform.SetPositionAndRotation(pos, rot);
    }

    void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
