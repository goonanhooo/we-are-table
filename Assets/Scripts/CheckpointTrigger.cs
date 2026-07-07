using UnityEngine;

/// <summary>중간 체크포인트(트리거) — 테이블이 지나면 StageTimer에 표시. GoalTrigger.requireCheckpoint와 짝.</summary>
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<TableInput>() == null) return;
        if (StageTimer.Instance != null) StageTimer.Instance.PassCheckpoint();
    }
}
