using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Shared anatomical mapping. Slots always represent the four keyboard groups.</summary>
public static class LegLayout
{
    public static readonly string[] MeshNames = { "Leg_BL", "Leg_BR", "Leg_FL", "Leg_FR" };
    public static readonly string[] NamesKr = { "왼쪽 뒷다리", "오른쪽 뒷다리", "왼쪽 앞다리", "오른쪽 앞다리" };
    public static readonly string[] NamesEn = { "back-left leg", "back-right leg", "front-left leg", "front-right leg" };
    public static readonly string[] KeyNames = { "WASD", "TFGH", "IJKL", "[ ; ' Enter" };
    public static string Name(int slot) => L.T(NamesKr[slot], NamesEn[slot]);
    public static string Keys(int slot) => KeyNames[slot];
    public static int SlotOf(Transform t)
    {
        for (int i=0;i<4;i++) if(t.name==MeshNames[i] || t.name==MeshNames[i]+"_Pivot" || t.name=="LegYaw_"+MeshNames[i].Substring(4)) return i;
        return -1;
    }
    public static Transform Find(Transform root,int slot)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t.name==MeshNames[slot])return t;
        return null;
    }
    public static Vector3 Center(Transform leg)
    {
        var r=leg != null ? leg.GetComponent<Renderer>() : null;
        return r != null ? r.bounds.center : leg != null ? leg.position : Vector3.zero;
    }
    public static void Apply(Transform t,ref LegControlSet set,ref Key up,ref Key down,ref Key left,ref Key right)
    {
        int slot=SlotOf(t);if(slot<0)return;
        set=(LegControlSet)slot;
        // The alternate cluster belongs to the arrow-key leg, even in older saved scenes.
        up=slot==3?Key.LeftBracket:Key.None;down=slot==3?Key.Quote:Key.None;
        left=slot==3?Key.Semicolon:Key.None;right=slot==3?Key.Enter:Key.None;
    }
    // Kitchen sockets are authored in anatomical FL, FR, BL, BR order.
    public static int SocketIndex(int slot) => (slot+2)%4;
}
