using UnityEngine;

/// <summary>경주 게이트 트리거 → RaceStage 전달(1=반환, 2=결승). 판정은 서버(RaceStage)에서.</summary>
public class RaceGateRelay : MonoBehaviour
{
    public RaceStage stage;
    public int gateId = 1;
    void OnTriggerEnter(Collider other) { if (stage != null) stage.OnGate(gateId, other); }
}
