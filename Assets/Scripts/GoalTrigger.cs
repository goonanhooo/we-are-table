using UnityEngine;

/// <summary>
/// 골 트리거: 테이블이 들어오면 StageTimer.Clear() (트리거 콜라이더 필요).
/// requireCheckpoint=true면 CheckpointTrigger를 먼저 지나야 인정(트랙 역주행/제자리 클리어 방지).
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("체크포인트(반대편) 통과 후에만 클리어 인정 — 트랙 한 바퀴 강제.")]
    public bool requireCheckpoint;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<TableInput>() == null) return;   // 테이블만
        var t = StageTimer.Instance;
        if (t == null || t.Cleared) return;
        if (requireCheckpoint && !t.CheckpointPassed) return;
        t.Clear();
    }
}
