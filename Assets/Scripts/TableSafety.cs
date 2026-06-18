using UnityEngine;

/// <summary>
/// 물리 폭발 방지/복구용 워치독.
/// Rigidbody의 위치/회전/속도가 무한(Inf)·NaN이 되면(=폭발), 마지막 정상 상태로 되돌리고 속도를 0으로 만든다.
/// → "Skipped updating the transform ... infinite" 에러가 떠도 게임이 깨지지 않고 그 상태 그대로 계속 진행.
/// 시작 시 씬의 모든 Rigidbody를 수집해 감시한다(테이블 본체 + 다리들 + 기타).
/// </summary>
public class TableSafety : MonoBehaviour
{
    // 특이점 탈출용 미세 이동 거리(월드 단위). 아주 작게 — 눈에 띄는 다른 움직임을 만들지 않음.
    const float NudgeAmount = 0.008f;

    Rigidbody[] bodies;
    Vector3[] lastPos;
    Quaternion[] lastRot;

    void Start()
    {
        bodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        lastPos = new Vector3[bodies.Length];
        lastRot = new Quaternion[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            lastPos[i] = bodies[i].position;
            lastRot[i] = bodies[i].rotation;
        }
    }

    static bool F(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    static bool F(Vector3 v) => F(v.x) && F(v.y) && F(v.z);
    static bool F(Quaternion q) => F(q.x) && F(q.y) && F(q.z) && F(q.w);

    void FixedUpdate()
    {
        if (bodies == null) return;
        for (int i = 0; i < bodies.Length; i++)
        {
            var rb = bodies[i];
            if (rb == null || rb.isKinematic) continue;

            bool ok = F(rb.position) && F(rb.rotation) && F(rb.linearVelocity) && F(rb.angularVelocity);
            if (ok)
            {
                // 정상 → 마지막 정상 상태 갱신
                lastPos[i] = rb.position;
                lastRot[i] = rb.rotation;
            }
            else
            {
                // 폭발 → 속도 제거 + 마지막 정상 위치에서 아주 미세하게 다른 곳으로 순간이동(해당 바디만).
                // 같은 위치로 되돌리면 특이점에서 못 빠져나와 매 프레임 다시 터진다(=로그 반복/멈춤).
                // 미세 랜덤 오프셋으로 특이점을 탈출시키되, 속도는 0이라 다른 움직임은 생기지 않는다.
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                Vector3 dir = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f);
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
                rb.position = lastPos[i] + dir.normalized * NudgeAmount;
                rb.rotation = lastRot[i];
                // lastPos는 갱신하지 않음(마지막 '정상' 위치를 앵커로 유지)
            }
        }
    }
}
