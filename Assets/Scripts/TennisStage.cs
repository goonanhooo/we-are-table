using UnityEngine;

/// <summary>
/// 등반 씬(Climb.unity) 진행 스크립트 — SpaceStage 패턴.
/// 입장 시 테니스공 모드 부여 + 기린 잠금(신축 키 E/Y/O/]가 '붙기' 키와 같아 충돌 방지).
/// </summary>
public class TennisStage : MonoBehaviour
{
    void Start()
    {
        var t = FindAnyObjectByType<TennisMode>();
        if (t != null) { t.unlocked = true; t.active = true; }
        var g = FindAnyObjectByType<GiraffeMode>();
        if (g != null) g.locked = true;
    }
}
