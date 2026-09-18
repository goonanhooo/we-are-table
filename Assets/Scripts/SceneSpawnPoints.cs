using UnityEngine;

/// <summary>
/// ★2026-09-18 씬 안 '이름 붙은 시작 지점' — 한 씬을 여러 진입점으로 시작하기 위한 부품(빙산 = IceCavern의 눈사람 동굴에서 시작).
/// 사용: 씬에 GO "SpawnPoints"(이 컴포넌트) + 자식 빈 GO(이름 = 지점 id, 예 "SnowmanCave")를 두고,
/// 로드하는 쪽이 `NetScene.Load(scene, spawnId)`(→ SpawnOverride.Pending)로 지점을 지정하면 Awake에서 테이블 루트를 그 자리로 옮긴다.
/// - ArtSceneReset.Awake(initPos 캡처 — F1 재시작)보다 먼저 실행(DefaultExecutionOrder -200) → F1도 이 지점으로 돌아옴.
/// - PauseMenu '다시 시작'(NetScene.Reload)은 SpawnOverride.Current를 이어받아 같은 지점에서 다시 시작.
/// - 지정이 없으면(정글→동굴 같은 흐름 진입) 아무것도 안 함 = 씬에 놓인 TablePlayer 위치 그대로.
/// - 멀티 게스트는 건드리지 않음(테이블 위치는 호스트 NetworkTransform이 결정).
/// </summary>
public static class SpawnOverride
{
    /// <summary>다음 씬 로드에서 쓸 지점 id(NetScene.Load가 설정, SceneSpawnPoints.Awake가 소비).</summary>
    public static string Pending;
    /// <summary>현재 씬에 적용된 지점 id(없으면 null) — Reload 시 유지용.</summary>
    public static string Current;
}

[DefaultExecutionOrder(-200)]
public class SceneSpawnPoints : MonoBehaviour
{
    [Tooltip("지점 위 이 높이에서 시작(살짝 떨어져 착지)")] public float dropHeight = 1.0f;

    Transform picked; ArticulationBody root; ArtSceneReset asr;

    void Awake()
    {
        string id = SpawnOverride.Pending;
        SpawnOverride.Pending = null;
        SpawnOverride.Current = null;
        if (string.IsNullOrEmpty(id)) return;
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;   // 게스트: 호스트 위치를 받음
        picked = transform.Find(id);
        if (picked == null) { Debug.LogWarning("[SceneSpawnPoints] 지점 없음: " + id); return; }
        SpawnOverride.Current = id;
    }

    // ⚠️Awake에서 트랜스폼을 옮기면 안 됨: ArtSceneReset.Awake(MeasureGeometry)가 콜라이더 월드 bounds로 상판 높이를 재는데,
    //   ArticulationBody 콜라이더 bounds는 첫 물리 스텝 전엔 갱신되지 않아(SyncTransforms 무효) 원래 자리 기준 151m짜리 lift가 계산됐음(F1이 y247로 튐).
    //   → 원래 자리에서 측정을 끝낸 뒤 Start(실행순서 -200 = 다른 Start보다 먼저)에서 TeleportRoot로 옮기고 F1 기준점만 갈아끼운다.
    void Start()
    {
        if (picked == null) return;
        asr = Object.FindAnyObjectByType<ArtSceneReset>(FindObjectsInactive.Include);
        if (asr == null) { Debug.LogWarning("[SceneSpawnPoints] 테이블(ArtSceneReset) 없음"); return; }
        foreach (var ab in asr.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { root = ab; break; }
        if (root == null) return;
        Vector3 target = Target(); Quaternion rot = Quaternion.Euler(0f, picked.eulerAngles.y, 0f);
        // 프리팹 루트(TablePlayer)도 같이 옮겨 '루트 트랜스폼 위치'를 쓰는 코드와 어긋나지 않게 한 뒤, 관절 사슬은 TeleportRoot로 정확히
        var top = asr.transform.root; if (top != root.transform) top.position += target - root.transform.position;
        root.TeleportRoot(target, rot);
        root.linearVelocity = Vector3.zero; root.angularVelocity = Vector3.zero;
        asr.SetSpawn(target);
        // 지점에 SpawnPointGrant가 있으면 능력 상태를 흐름상 '여기 도착했을 때'와 같게 맞춤(예: 눈사람 동굴 = 테니스공 들고 도착)
        var grant = picked.GetComponent<SpawnPointGrant>();
        if (grant != null) grant.Apply(root.transform.root);
    }

    Vector3 Target() => picked.position + Vector3.up * dropHeight;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
        foreach (Transform c in transform) { Gizmos.DrawWireSphere(c.position, 0.6f); Gizmos.DrawLine(c.position, c.position + c.forward * 1.5f); }
    }
}
