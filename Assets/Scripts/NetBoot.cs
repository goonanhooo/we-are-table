using UnityEngine;

/// <summary>
/// 멀티 부트스트랩: 어느 씬에서 시작하든 NetworkManager(프리팹, Resources/NetworkManager)를
/// 1회 자동 생성한다(씬마다 배치할 필요 없음). NetworkManager는 자체 DontDestroyOnLoad.
/// </summary>
public static class NetBoot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null) return;
        var prefab = Resources.Load<GameObject>("NetworkManager");
        if (prefab != null){var go=Object.Instantiate(prefab);var nm=go.GetComponent<Unity.Netcode.NetworkManager>();if(nm!=null)nm.NetworkConfig.ProtocolVersion=3;}
        else Debug.LogWarning("[NetBoot] Resources/NetworkManager 프리팹 없음 — 멀티 비활성");
    }
}
