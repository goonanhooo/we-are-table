using UnityEngine;

/// <summary>Only the selected power consumes the shared Q/E-style action keys.</summary>
public static class AbilitySelection
{
    public static void Select(Component selected)
    {
        var root=selected.transform.root;
        var giraffe=root.GetComponentInChildren<GiraffeMode>(true);
        var tennis=root.GetComponentInChildren<TennisMode>(true);
        var ice=root.GetComponentInChildren<IcePower>(true);
        var squid=root.GetComponentInChildren<SquidMode>(true);
        if(giraffe!=null&&giraffe!=selected&&!giraffe.netVisualOnly)giraffe.active=false;
        if(tennis!=null&&tennis!=selected&&!tennis.netVisualOnly&&tennis.active)tennis.SetActive(false);
        if(ice!=null&&ice!=selected&&!ice.netVisualOnly&&ice.active)ice.SetActive(false);
        if(squid!=null&&squid!=selected&&!squid.netVisualOnly)squid.active=false;
    }
}
