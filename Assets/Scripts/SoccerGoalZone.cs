using UnityEngine;

/// <summary>축구 골대 트리거 → SoccerStage 전달. goalTeam = 이 골대를 지키는 팀(0=파랑, 1=빨강).</summary>
public class SoccerGoalZone : MonoBehaviour
{
    public SoccerStage stage;
    public int goalTeam;
    void OnTriggerEnter(Collider other) { if (stage != null) stage.OnGoal(goalTeam, other); }
}
