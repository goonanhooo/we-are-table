using UnityEngine;

/// <summary>
/// ★2026-09-04 광차. 키네마틱 Rigidbody — RailRide가 u를 정해 주면 선로 위 위치/자세로 MovePosition.
/// 테니스공(FixedJoint)이 이 광차의 콜라이더에 붙으면 Attached. uStart~uEnd 구간만 다님(끊긴 선로 앞에서 멈춤).
/// </summary>
public class MineCart : MonoBehaviour
{
    public RailTrack track;
    [Tooltip("이 광차가 다니는 u 구간")] public float uStart = 0f, uEnd = 1f;
    [Tooltip("레일 윗면에서 광차 바닥 윗면까지 높이")] public float floorHeight = 0.3f;
    [HideInInspector] public float u;
    Rigidbody rb;

    public bool Attached
    {
        get { foreach (var j in GetComponents<FixedJoint>()) if (j != null && (j.connectedArticulationBody != null || j.connectedBody != null)) return true; return false; }
    }
    public bool AtEnd => u >= uEnd - 1e-4f;

    void Awake() { rb = GetComponent<Rigidbody>(); if (rb != null) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate; } u = Mathf.Clamp(u, uStart, uEnd); Place(true); }

    public void Place(bool immediate = false)
    {
        if (track == null) return;
        Vector3 p = track.PosAt(u) + Vector3.up * floorHeight;
        Quaternion r = Quaternion.LookRotation(track.TangentAt(u), Vector3.up);
        if (rb != null && Application.isPlaying && !immediate) { rb.MovePosition(p); rb.MoveRotation(r); }
        else transform.SetPositionAndRotation(p, r);
    }
}
