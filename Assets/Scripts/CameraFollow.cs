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

    // ★2026-09-03 컷씬 복귀 블렌드: 컷씬이 카메라를 옮겨 놓은 뒤 다시 켜질 때 스냅 대신 부드럽게 제자리로.
    Vector3 blendPos; Quaternion blendRot; float blendUntil, blendDur;

    /// <summary>지금 카메라 포즈에서 팔로우 포즈로 seconds 동안 부드럽게 이어 붙인다(컷씬 끝에서 enabled=true 직전에 호출).</summary>
    public void BlendFrom(float seconds)
    {
        blendPos = transform.position; blendRot = transform.rotation;
        blendDur = Mathf.Max(0.01f, seconds); blendUntil = Time.time + blendDur;
    }

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
        // ★대상(테이블)이 NaN/폭발로 비정상 위치가 되면 카메라를 따라보내지 않는다(이전 위치 유지).
        //   안 그러면 카메라/오디오리스너가 non-finite가 되어 'IsFinite(distanceAlongView)','Invalid worldAABB' 어설션 폭주.
        if (!Sane(pivot)) return;
        Vector3 pos = pivot - rot * Vector3.forward * distance;
        if (!Sane(pos)) return;
        if (Time.time < blendUntil)
        {
            float u = Mathf.SmoothStep(0f, 1f, 1f - (blendUntil - Time.time) / blendDur);
            transform.SetPositionAndRotation(Vector3.Lerp(blendPos, pos, u), Quaternion.Slerp(blendRot, rot, u));
            return;
        }
        transform.SetPositionAndRotation(pos, rot);
    }

    // 유한하고 원점에서 너무 멀지 않은(폭발 아님) 위치인지
    static bool Sane(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)
            && v.sqrMagnitude < 1e8f;
    }

    void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
