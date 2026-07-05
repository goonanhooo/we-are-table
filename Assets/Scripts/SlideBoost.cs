using UnityEngine;

/// <summary>
/// 슬라이드 부스터(IceMountain): 얼음 모드로 채널 구간을 내려가는 동안 목표 속도까지 가속 유지.
/// 관절 테이블은 U채널에서 벽 접촉/드라이브로 에너지를 크게 잃어(실측 ~80%) 킥커를 못 넘음 —
/// 얼음 슬라이드다운 '쭉 가속' 감각 + 킥커 점프 보장용. 얼음 모드(IcePower.active)일 때만 작동(게이트 유지).
/// </summary>
public class SlideBoost : MonoBehaviour
{
    [Header("부스트 구간(world) — 채널 하강부(킥커 전까지)")]
    public float zMin = 43f, zMax = 87f;
    public float xHalf = 3.6f;
    public float yMax = 27f;
    [Header("가속")]
    public float targetSpeed = 18f;    // 이 속도까지 끌어올림(킥커 rise 8.6 기준 exit ~12m/s)
    public float accel = 20f;          // m/s^2
    [Tooltip("채널 하강 방향(정규화됨)")]
    public Vector3 downhill = new Vector3(0f, -0.41f, 0.91f);

    ArticulationBody root;
    IcePower ice;

    void Start()
    {
        foreach (var ab in FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None))
            if (ab.isRoot) { root = ab; break; }
        ice = FindAnyObjectByType<IcePower>();
        downhill = downhill.normalized;
    }

    void FixedUpdate()
    {
        if (root == null || ice == null || !ice.active) return;
        Vector3 p = root.transform.position;
        if (p.z < zMin || p.z > zMax || Mathf.Abs(p.x) > xHalf || p.y > yMax) return;
        var v = root.linearVelocity;
        if (v.z < 0.3f) return;                       // 앞으로 가는 중에만
        if (v.magnitude >= targetSpeed) return;
        root.AddForce(downhill * accel, ForceMode.Acceleration);
    }
}
