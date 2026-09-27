using UnityEngine;

/// <summary>Prepare missing power components without granting any ability.</summary>
public static class AbilityComponents
{
    public static void Ensure(GameObject table)
    {
        var ice=table.GetComponent<IcePower>();
        if(ice==null)ice=table.AddComponent<IcePower>();
        if(ice.iceMaterial==null)ice.iceMaterial=Resources.Load<Material>("InheritedIcePower");
        var nm=Unity.Netcode.NetworkManager.Singleton;
        ice.netVisualOnly=nm!=null&&nm.IsListening&&!nm.IsServer;
    }
}
