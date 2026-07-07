using UnityEngine;

/// <summary>
/// 달 씬: 저중력(지구의 ~1/6) 적용. ⚠️Physics.gravity는 '전역'이라 씬을 나가거나
/// 플레이를 멈추면 반드시 원복해야 함 — OnDestroy에서 복구(씬 전환/플레이 종료 모두 커버).
/// </summary>
public class MoonStage : MonoBehaviour
{
    [Tooltip("달 중력(m/s²). 지구 -9.81의 약 1/6.")]
    public float moonGravity = -1.62f;

    Vector3 orig;

    void Awake()
    {
        orig = Physics.gravity;
        Physics.gravity = new Vector3(0f, moonGravity, 0f);
    }

    void OnDestroy()
    {
        Physics.gravity = orig;
    }
}
