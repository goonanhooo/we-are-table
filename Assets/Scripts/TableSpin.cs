using UnityEngine;

/// <summary>
/// 좌우 키 = 테이블 몸통을 '자기 수직축' 기준으로 제자리 회전(2026-07-05, 사용자 요청).
/// 다리별 yaw 관절 스핀(구면 관절 — 원뿔 느낌·±175 한계·분해 특이점)을 대체.
/// 루트 ArticulationBody에 각속도 서보 토크만 걸므로 관절 리밋/분해가 전혀 개입하지 않음
/// = 물리적으로 가장 안전한 조작(크래시 리스크 최소). 네 키셋의 좌우 입력을 합산(멀티 4인 공용).
/// 입력 없으면 토크 0(강제 정지 없음 — 관성/마찰이 자연 감속).
/// </summary>
public class TableSpin : MonoBehaviour
{
    [Tooltip("입력 1일 때 목표 yaw 각속도(도/초)")]
    public float spinRate = 70f;
    [Tooltip("서보 게인(rad/s 오차당 N·m)")]
    public float gain = 280f;   // 실측: 발바닥 면접촉 비틀림 마찰(실효 ~115N·m)을 이기려면 고게인 필요
    [Tooltip("토크 상한(N·m) — 접지 마찰 모멘트(질량7×μ1×반경0.82≈56)를 이기고 돌 만큼")]
    public float maxTorque = 170f;
    [Tooltip("직립도(up·worldUp)가 이 값 미만이면 스핀 안 함(넘어진/기운 상태에서 헛돌림 방지)")]
    public float minUpright = 0.55f;

    ArticulationBody root;
    TableInput input;
    ArtLegController[] legs;

    void Start()
    {
        foreach (var ab in GetComponentsInChildren<ArticulationBody>(true))
            if (ab.isRoot) { root = ab; break; }
        input = GetComponent<TableInput>();
        legs = GetComponentsInChildren<ArtLegController>(true);
    }

    void FixedUpdate()
    {
        if (root == null || input == null || legs == null || legs.Length == 0) return;
        float spin = 0f;
        foreach (var l in legs)
        {
            if (l == null) continue;
            var inp = input.Get(l.controlSet);
            // 왼쪽 키 = 왼쪽으로 선회(위에서 봤을 때 반시계 = Unity +up 기준 음의 각속도)
            spin += (inp.right ? 1f : 0f) - (inp.left ? 1f : 0f);
        }
        spin = Mathf.Clamp(spin, -1f, 1f);
        if (spin == 0f) return;

        // ★월드 수직축 기준(몸통 축이 아니라) — root.up 토크는 기울면 수평 성분이 생겨
        //   '기울수록 더 기울이는' 되먹임으로 넘어짐(실측 tilt 0.79). 수직 토크는 절대 못 넘어뜨림.
        float upright = Vector3.Dot(root.transform.up, Vector3.up);
        if (upright < minUpright) return;                       // 누운 상태에선 헛돌림 방지
        float target = spin * spinRate * Mathf.Deg2Rad * upright;   // 기울수록 살살
        float current = Vector3.Dot(root.angularVelocity, Vector3.up);
        float torque = Mathf.Clamp((target - current) * gain, -maxTorque, maxTorque) * upright;
        root.AddTorque(Vector3.up * torque);
    }
}
