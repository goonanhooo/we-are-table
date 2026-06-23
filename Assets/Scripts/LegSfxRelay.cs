using UnityEngine;

/// <summary>
/// 다리 피벗(Rigidbody)에 부착. 다리가 **다른 것(땅)** 에 닿으면 루트의 `TableSfx.PlayStep`을 호출해
/// Wood 효과음을 낸다. 충돌 콜백은 콜라이더(자식 Leg_*)가 붙은 Rigidbody의 GameObject(피벗)가 받는다.
/// </summary>
public class LegSfxRelay : MonoBehaviour
{
    TableSfx sfx;

    void Start() { sfx = GetComponentInParent<TableSfx>(); }

    void OnCollisionEnter(Collision c)
    {
        if (sfx == null) return;
        if (c.collider.transform.root == transform.root) return;   // 테이블 자기 부위끼리는 무시
        sfx.PlayStep(c.relativeVelocity.magnitude);
    }
}
