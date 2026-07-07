using UnityEngine;

/// <summary>
/// 모험 맵(Adventure.unity) 진행 스크립트.
/// 시작 시 능력을 전부 잠가서(기린 locked, 얼음/오징어는 원래 잠김) 코스의 AbilityTotem 순서대로 얻게 한다.
/// 협곡(무중력 구간) 통과는 SquidTotem이 SpaceStage처럼 오징어를 켠다.
/// </summary>
public class AdventureStage : MonoBehaviour
{
    void Start()
    {
        var g = FindAnyObjectByType<GiraffeMode>();
        if (g != null) { g.locked = true; g.active = false; }
    }
}
