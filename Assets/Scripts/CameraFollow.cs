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
    [Tooltip("★2026-09-06 커서가 풀렸을 때(알트탭/스팀 오버레이/컷씬 뒤) 게임 화면을 클릭하면 다시 잠가 마우스 시점 회전 복구 — 멀티에서 각자 자기 마우스로 시점 조작")]
    public bool clickToRelock = true;

    [Header("★2026-09-04 카메라 충돌 — 벽/경사면 안으로 파고들지 않게(구체 캐스트로 당김)")]
    public bool collide = true;
    public float collisionRadius = 0.3f;
    public LayerMask collisionMask = ~0;
    [Tooltip("막힘이 풀릴 때 원래 거리로 돌아가는 속도(m/s)")] public float collisionRecover = 8f;
    float curDist = -1f;

    /// <summary>[검증/컷씬용] 시점 각도 직접 설정.</summary>
    public void SetYawPitch(float y, float p) { yaw = y; pitch = Mathf.Clamp(p, minPitch, maxPitch); }
    public float Yaw => yaw;

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

    PauseMenu pauseMenu; bool pauseMenuLooked;

    /// <summary>커서를 다시 잠가도 되는 상태인가 — 메뉴/편지/로비 같은 클릭 UI가 열려 있으면 안 됨.</summary>
    public bool CanRelock()
    {
        if (!lockCursor || !clickToRelock) return false;
        if (!Application.isFocused) return false;
        if (Time.timeScale == 0f) return false;                       // 일시정지 메뉴
        if (LetterOverlay.IsOpen) return false;                        // 편지 오버레이
        if (!pauseMenuLooked) { pauseMenu = FindAnyObjectByType<PauseMenu>(); pauseMenuLooked = true; }
        if (pauseMenu != null && pauseMenu.IsOpen) return false;
        if (NetDebugPanel.LobbyOpen) return false;                     // 멀티 로비(다리 선택 클릭 UI)
        if (FindAnyObjectByType<StartScreenMenu>() != null) return false;   // 시작 화면(커서 항상 표시)
        return true;
    }

    void LateUpdate()
    {
        // ESC는 PauseMenu(일시정지)가 담당. 여기서 커서를 건드리지 않는다.
        if (target == null) return;

        // ★커서가 풀려 있으면(알트탭/오버레이/컷씬) 게임 화면 클릭으로 다시 잠금 — 각 클라이언트가 자기 마우스로 시점 회전
        Mouse mouse = Mouse.current;
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame && CanRelock())
            SetCursor(true);

        // 커서가 잠겨 있을 때만 마우스로 시점 회전
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
        float useDist = distance;
        if (collide)
        {
            // 피벗에서 카메라 방향으로 구체 캐스트 — 테이블 자신은 제외, 트리거 제외
            Vector3 dir = -(rot * Vector3.forward);
            float blocked = distance;
            var hits = Physics.SphereCastAll(pivot, collisionRadius, dir, distance, collisionMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.transform.root == target.root) continue;
                if (h.distance <= 0f) continue;   // 시작점부터 겹침(피벗이 이미 벽 속) — 무시
                if (h.distance < blocked) blocked = h.distance;
            }
            if (curDist < 0f) curDist = distance;
            curDist = blocked < curDist ? blocked : Mathf.MoveTowards(curDist, blocked, collisionRecover * Time.deltaTime);
            useDist = Mathf.Max(0.6f, curDist);
        }
        Vector3 pos = pivot - rot * Vector3.forward * useDist;
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
