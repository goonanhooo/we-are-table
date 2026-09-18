using UnityEngine;

/// <summary>
/// ★2026-09-18 SceneSpawnPoints의 지점 자식에 붙여, 그 지점에서 시작할 때 '흐름상 여기 도착했을 때' 갖고 있어야 할 능력 상태를 맞춘다.
/// 예: IceCavern "SnowmanCave" = 광차를 타고 테니스공 모드로 도착하는 곳 → 테니스공 부여(눈사람 머리를 기린+테니스공으로 들어 올리는 퍼즐에 필요).
/// </summary>
public class SpawnPointGrant : MonoBehaviour
{
    [Tooltip("테니스공 모드(붙기/떼기) 부여 + 켜기")] public bool tennisBall;
    [Tooltip("기린의 힘 잠금 해제(2번 키로 켤 수 있게)")] public bool giraffeUnlock;
    [Tooltip("얼음 능력 해금(3번 키)")] public bool icePower;

    public void Apply(Transform tableRoot)
    {
        if (tableRoot == null) return;
        // 스폰과 동시에 쥐여 주는 능력은 '잠김→풀림' 전환이 없어 KeyTutorial이 부여를 못 알아챔 → 직접 알려 그 키 안내가 뜨게 한다
        var tut = tableRoot.GetComponentInChildren<KeyTutorial>(true);
        if (tennisBall) { var t = tableRoot.GetComponentInChildren<TennisMode>(true); if (t != null) { t.unlocked = true; t.active = true; if (tut != null) tut.NotifyGranted("tennis"); } }
        if (giraffeUnlock) { var g = tableRoot.GetComponentInChildren<GiraffeMode>(true); if (g != null) { g.locked = false; GiraffeMode.Unlocked = true; if (tut != null) tut.NotifyGranted("giraffe"); } }
        if (icePower) { var i = tableRoot.GetComponentInChildren<IcePower>(true); if (i != null) { i.unlocked = true; if (tut != null) tut.NotifyGranted("ice"); } }
    }
}
