using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Raise the house shell while keeping floor sockets, furniture and ending markers in place.</summary>
public static class MoonHouseCeilingUpgrade
{
    public const float Raise = 1.2f;
    static void Move(Transform t, float amount) { if (t != null) t.localPosition += Vector3.up * amount; }
    static void Taller(Transform t, float amount, float bottomRaise = 0f)
    {
        if (t == null) return;
        var scale = t.localScale; scale.y += amount - bottomRaise; t.localScale = scale;
        Move(t, (amount + bottomRaise) * .5f);
    }
    public static bool ApplyToHouse(Transform house)
    {
        bool cozy = house.name == "House_CozyKitchen";
        var back = house.Find("WallBack"); if (back == null) return false;
        float delta = (cozy ? 3.5f : 3f) + Raise - back.localScale.y;
        if (delta <= .001f) return false;
        string[] walls = cozy ? new[]{"WallBack","WallLeft","WallRight","FrontDoorPier","FrontMiddlePier","FrontRightPier","WindowUpperWall"}
            : new[]{"WallBack","WallLeft","WallRight","WallFrontL","WallFrontR"};
        foreach (var name in walls) Taller(house.Find(name), delta);
        float doorRaise = delta * .5f;
        Taller(house.Find(cozy ? "DoorLintel" : "Lintel"), delta, doorRaise);
        foreach (Transform child in house)
            if (child.name == "RoofL" || child.name == "RoofR" || child.name == "Ridge" || child.name.StartsWith("Gable")
                || child.name == "CeilingBeam" || child.name == "Chimney" || child.name == "ChimneyCap" || child.name == "ChimneySmoke") Move(child, delta);
        var door = house.Find(cozy ? "OpenDoor" : "DoorHinge");
        if (door != null)
        {
            var leaf = door.Find(cozy ? "DoorLeaf" : "Door");
            if (leaf != null)
            {
                float ratio = (leaf.localScale.y + doorRaise) / leaf.localScale.y;
                Taller(leaf, doorRaise);
                foreach (Transform child in door)
                    if (child != leaf)
                    {
                        var pos = child.localPosition; pos.y *= ratio; child.localPosition = pos;
                        if (child.name == "DoorPanel") { var scale = child.localScale; scale.y *= ratio; child.localScale = scale; }
                    }
            }
        }
        var kitchen = house.Find("Kitchen");
        float lampRaise = delta * .4f;
        if (kitchen != null && cozy)
        {
            var pendant = kitchen.Find("Pendant");Move(pendant, lampRaise);
            var cord = pendant != null ? pendant.Find("Cord") : null;
            if (cord != null) { Move(cord, (delta - lampRaise) * .5f); var scale = cord.localScale; scale.y += (delta - lampRaise) * .5f; cord.localScale = scale; }
            Move(kitchen.Find("TableLamplight"), lampRaise);
        }
        else if (kitchen != null)
        {
            var cord = kitchen.Find("LampCord");
            if (cord != null) { Move(cord, (delta + lampRaise) * .5f); var scale = cord.localScale; scale.y += (delta - lampRaise) * .5f; cord.localScale = scale; }
            Move(kitchen.Find("LampShade"), lampRaise); Move(kitchen.Find("LampBulb"), lampRaise); Move(kitchen.Find("LampLight"), lampRaise);
        }
        return true;
    }
    [MenuItem("Tools/Moon/Raise House Ceilings")]
    public static void ApplyScenes()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop play first.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Save scene edits first.");
        string original = SceneManager.GetActiveScene().path;
        try
        {
            foreach (string name in new[]{"Moon","Moon_CozyKitchen"})
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                var house = GameObject.Find(name == "Moon" ? "House" : "House_CozyKitchen");
                if (house == null) throw new System.InvalidOperationException("Missing house: " + name);
                if (ApplyToHouse(house.transform)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                Debug.Log(name + " wall height=" + house.transform.Find("WallBack").localScale.y);
            }
        }
        finally { if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original); }
    }
}
