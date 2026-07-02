using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ③ '근본 해결' 프로토타입 — 주저앉지/터지지 않는 테이블.
/// 관절(HingeJoint)·가는 다리 콜라이더를 전부 없애고, **상판만 동적 Rigidbody**로 두고
/// 네 모서리에서 아래로 레이캐스트 → **스프링-댐퍼 서스펜션**으로 상판을 rideHeight 높이에 띄워 세운다.
///  - 스프링이 항상 위로 밀어올려 **절대 주저앉지 않음**(다리가 꺾여 풀썩 X).
///  - 조인트/고세장비 콜라이더가 없어 솔버 NaN이 안 나 **절대 안 터짐**.
///  - 다리는 시각용 메시 — 매 프레임 모서리→지면 접점으로 늘려 '서 있는 다리'처럼 보이게.
/// 이동: WASD(카메라 기준). 평지/경사/단차에서 흔들리되 넘어지지 않게 자세도 잡는다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SuspensionTable : MonoBehaviour
{
    [Header("서스펜션(주저앉음 방지)")]
    public float rideHeight = 0.8f;       // 모서리가 지면 위로 뜨는 높이(=다리 길이)
    public float springStrength = 240f;   // 모서리당 스프링 세기
    public float springDamper = 26f;      // 진동 감쇠(클수록 덜 출렁)
    public float rayExtra = 0.7f;         // rideHeight 너머 지면 탐지 거리(공중 판정)
    public Vector2 cornerHalf = new Vector2(0.58f, 0.58f);
    public LayerMask groundMask = ~0;

    [Header("자세(넘어짐 방지)")]
    public float uprightStrength = 18f;
    public float uprightDamper = 4f;

    [Header("이동")]
    public float moveForce = 16f;
    public float maxSpeed = 5.5f;

    [Header("시각용 다리(4개, FR/FL/BR/BL 순)")]
    public Transform[] legMeshes;

    Rigidbody rb;
    Vector3[] localCorners;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = 12f;       // 안전(터질 일은 없지만 과회전 방지)
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        localCorners = new[]
        {
            new Vector3( cornerHalf.x, 0f,  cornerHalf.y), // FR
            new Vector3(-cornerHalf.x, 0f,  cornerHalf.y), // FL
            new Vector3( cornerHalf.x, 0f, -cornerHalf.y), // BR
            new Vector3(-cornerHalf.x, 0f, -cornerHalf.y), // BL
        };
    }

    // 모서리 아래 지면까지 거리(자기 콜라이더는 건너뜀). 못 찾으면 false.
    bool GroundBelow(Vector3 corner, out float dist)
    {
        dist = rideHeight;
        Vector3 origin = corner + Vector3.up * 0.15f;
        float maxDist = rideHeight + rayExtra + 0.15f;
        var hits = Physics.RaycastAll(origin, Vector3.down, maxDist, groundMask, QueryTriggerInteraction.Ignore);
        float best = Mathf.Infinity; bool found = false;
        foreach (var h in hits)
        {
            if (h.collider.transform.root == transform.root) continue;  // ★상판 자기 콜라이더 제외
            if (h.distance < best) { best = h.distance; found = true; }
        }
        if (found) { dist = best - 0.15f; return true; }
        return false;
    }

    void FixedUpdate()
    {
        // --- 4모서리 서스펜션 ---
        foreach (var lc in localCorners)
        {
            Vector3 cw = transform.TransformPoint(lc);
            if (GroundBelow(cw, out float d))
            {
                float comp = rideHeight - d;                       // +면 너무 낮음(밀어올림)
                float vUp = Vector3.Dot(rb.GetPointVelocity(cw), Vector3.up);
                float f = comp * springStrength - vUp * springDamper;
                if (f < 0f) f *= 0.4f;                             // 당기는 힘은 약하게(지면에 안 들러붙게)
                rb.AddForceAtPosition(Vector3.up * f, cw, ForceMode.Force);
            }
        }

        // --- 자세 유지(상판 up → 월드 up) ---
        Vector3 up = transform.up;
        Vector3 axis = Vector3.Cross(up, Vector3.up);
        rb.AddTorque(axis * uprightStrength - rb.angularVelocity * uprightDamper, ForceMode.Acceleration);

        // --- 이동(카메라 기준 WASD) ---
        var kb = Keyboard.current;
        if (kb != null)
        {
            var cam = Camera.main;
            Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = cam ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 wish = Vector3.zero;
            if (kb.wKey.isPressed) wish += fwd;
            if (kb.sKey.isPressed) wish -= fwd;
            if (kb.dKey.isPressed) wish += right;
            if (kb.aKey.isPressed) wish -= right;
            if (wish.sqrMagnitude > 0.01f)
            {
                wish.Normalize();
                Vector3 hv = rb.linearVelocity; hv.y = 0f;
                if (hv.magnitude < maxSpeed)
                    rb.AddForce(wish * moveForce, ForceMode.Acceleration);
            }
        }
    }

    // 다리 메시를 매 프레임 모서리→지면 접점으로 늘려 '서 있는 다리'처럼.
    void LateUpdate()
    {
        if (legMeshes == null) return;
        for (int i = 0; i < legMeshes.Length && i < localCorners.Length; i++)
        {
            var leg = legMeshes[i];
            if (leg == null) continue;
            Vector3 cw = transform.TransformPoint(localCorners[i]);
            float len = rideHeight;
            if (GroundBelow(cw, out float d))
                len = Mathf.Clamp(d, 0.1f, rideHeight + rayExtra);
            // 모서리에서 아래로 len 만큼 내려가는 막대(월드 수직)
            leg.position = cw - Vector3.up * (len * 0.5f);
            leg.rotation = Quaternion.identity;
            Vector3 s = leg.localScale; s.y = len; leg.localScale = s;
        }
    }
}
