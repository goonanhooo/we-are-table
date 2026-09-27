using System.Collections.Generic;
using UnityEngine;

/// <summary>Camera-local visual occlusion: keep a dotted silhouette instead of disabling scenery.</summary>
[DefaultExecutionOrder(1200), DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CameraOcclusionFade : MonoBehaviour
{
    [Range(0.05f, 0.6f)] public float remainingCoverage = 0.20f;
    [Min(0.05f)] public float fadeSeconds = 0.25f;
    [Range(1f, 6f)] public float dotPixels = 3f;
    public Transform target;
    class Entry
    {
        public Renderer renderer;
        public Material[] original, faded;
        public float visibility = 1f, lastBlocked = -10f;
        public int wallFrame = -1;
    }
    readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
    readonly List<Renderer> remove = new List<Renderer>();
    MeshRenderer[] candidates;
    readonly Dictionary<Renderer, bool> eligibility = new Dictionary<Renderer, bool>();
    float nextScan;
    Shader shader;
    Camera view;
    CameraFollow follow;

    public static CameraOcclusionFade For(Camera camera)
    {
        if (camera == null) return null;
        var fade = camera.GetComponent<CameraOcclusionFade>();
        return fade != null ? fade : camera.gameObject.AddComponent<CameraOcclusionFade>();
    }
    void Awake()
    {
        view = GetComponent<Camera>(); follow = GetComponent<CameraFollow>();
        if (GetComponent<CameraGroundGuard>() == null) gameObject.AddComponent<CameraGroundGuard>();
        shader = Resources.Load<Shader>("Shaders/SceneryDither");
    }
    public void FadeWall(Renderer renderer)
    {
        if (!isActiveAndEnabled || renderer == null || !renderer.enabled || shader == null || CameraGroundGuard.IsGround(renderer)) return;
        var entry = GetEntry(renderer);
        if (entry != null) entry.wallFrame = Time.frameCount;
    }
    static bool Supported(Material material)
    {
        if (material == null || material.shader == null) return false;
        string name = material.shader.name;
        return (name == "Universal Render Pipeline/Lit" || name == "Universal Render Pipeline/Simple Lit"
            || name == "Universal Render Pipeline/Unlit" || name == "Custom/VertexColorLit"
            || name == "Custom/VertexColorCave" || name == "Custom/SceneryDither")
            && (!material.HasProperty("_Surface") || material.GetFloat("_Surface") < 0.5f);
    }
    bool Eligible(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
        if (target != null && renderer.transform.root == target.root) return false;
        if (eligibility.TryGetValue(renderer, out bool cached)) return cached;
        if (CameraGroundGuard.IsGround(renderer)) { eligibility[renderer] = false; return false; }
        if (entries.ContainsKey(renderer)) return true;
        var bounds = renderer.bounds;
        // Ground, ocean, and large terrain keep their normal collision/rendering.
        if (bounds.size.y < 0.45f || Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) > 45f) return false;
        foreach (var material in renderer.sharedMaterials) if (!Supported(material)) { eligibility[renderer] = false; return false; }
        bool supported = renderer.sharedMaterials.Length > 0; eligibility[renderer] = supported; return supported;
    }
    public bool CanFadeCollider(Collider collider)
    {
        if (!isActiveAndEnabled || shader == null || collider == null || CameraGroundGuard.IsGround(collider)) return false;
        var renderer = collider.GetComponent<Renderer>();
        if (renderer != null) return Eligible(renderer);
        // A palm's capsule often sits on its parent rather than its mesh.
        var children = collider.GetComponentsInChildren<MeshRenderer>();
        bool any = false;
        foreach (var child in children) { if (!Eligible(child)) return false; any = true; }
        return any;
    }
    Entry GetEntry(Renderer renderer)
    {
        if (CameraGroundGuard.IsGround(renderer)) return null;
        if (entries.TryGetValue(renderer, out var existing)) return existing;
        var original = renderer.sharedMaterials;
        if (original.Length == 0) return null;
        foreach (var source in original) if (!Supported(source)) return null;
        var entry = new Entry { renderer = renderer, original = original, faded = new Material[original.Length] };
        for (int i = 0; i < original.Length; i++)
        {
            var source = original[i]; var material = new Material(shader) { name = source.name + " (camera dither)", hideFlags = HideFlags.DontSave };
            foreach (var property in new[] { "_BaseMap", "_BumpMap", "_EmissionMap" })
                if (source.HasProperty(property)) { material.SetTexture(property, source.GetTexture(property)); material.SetTextureScale(property, source.GetTextureScale(property)); material.SetTextureOffset(property, source.GetTextureOffset(property)); }
            foreach (var property in new[] { "_BaseColor", "_EmissionColor", "_Tint" })
                if (source.HasProperty(property)) material.SetColor(property, source.GetColor(property));
            foreach (var property in new[] { "_Metallic", "_Smoothness", "_BumpScale", "_Cutoff", "_AlphaClip", "_Cull", "_AmbientBoost", "_MainLightScale", "_LightBoost", "_MaxBright" })
                if (source.HasProperty(property)) material.SetFloat(property, source.GetFloat(property));
            if (source.shader.name == "Custom/VertexColorCave") material.SetFloat("_Cull", 0f);
            material.SetFloat("_VertexColors", source.shader.name.StartsWith("Custom/VertexColor") ? 1f : 0f);
            material.SetFloat("_Unlit", source.shader.name.EndsWith("/Unlit") ? 1f : 0f);
            material.SetFloat("_UseNormal", source.IsKeywordEnabled("_NORMALMAP") ? 1f : 0f);
            material.SetFloat("_UseEmission", source.IsKeywordEnabled("_EMISSION") ? 1f : 0f);
            material.SetFloat("_Coverage", 1f);
            entry.faded[i] = material;
        }
        renderer.sharedMaterials = entry.faded;
        entries.Add(renderer, entry); return entry;
    }
    bool BlocksView(Renderer renderer)
    {
        var bounds = renderer.bounds;
        if (bounds.max.y < target.position.y + 0.25f) return false;
        Vector3 aim = target.position + Vector3.up * 0.5f;
        // Several sight lines catch foliage and wall edges without touching the entire scene.
        for (int i = 0; i < 5; i++)
        {
            Vector3 point = aim;
            if (i == 1) point += view.transform.right * 0.45f;
            if (i == 2) point -= view.transform.right * 0.45f;
            if (i == 3) point += Vector3.up * 0.45f;
            if (i == 4) point -= Vector3.up * 0.25f;
            Vector3 delta = point - transform.position; float distance = delta.magnitude;
            if (distance < 0.1f) continue;
            if (bounds.IntersectRay(new Ray(transform.position, delta / distance), out float hit) && hit < distance - 0.3f) return true;
        }
        return false;
    }
    void LateUpdate()
    {
        if (shader == null || view == null || view != Camera.main) { RestoreAll(); return; }
        if (follow != null && follow.target != null) target = follow.target;
        if (target == null) { var table = GameObject.Find("Table"); if (table != null) target = table.transform; }
        if (Time.unscaledTime >= nextScan)
        {
            eligibility.Clear();
            candidates = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            nextScan = Time.unscaledTime + 1f;
        }
        if (target != null && candidates != null) foreach (var renderer in candidates)
            if (Eligible(renderer) && BlocksView(renderer))
            {
                var entry = GetEntry(renderer); if (entry != null) entry.lastBlocked = Time.unscaledTime;
            }
        remove.Clear();
        foreach (var pair in entries)
        {
            var entry = pair.Value;
            if (entry.renderer == null || CameraGroundGuard.IsGround(entry.renderer)) { remove.Add(pair.Key); continue; }
            bool blocked = entry.wallFrame == Time.frameCount || Time.unscaledTime - entry.lastBlocked < 0.12f;
            entry.visibility = Mathf.MoveTowards(entry.visibility, blocked ? remainingCoverage : 1f, Time.unscaledDeltaTime / fadeSeconds);
            foreach (var material in entry.faded) { material.SetFloat("_Coverage", entry.visibility); material.SetFloat("_DotPixels", dotPixels); }
            if (!blocked && entry.visibility >= 1f) remove.Add(pair.Key);
        }
        foreach (var renderer in remove) { Restore(entries[renderer]); entries.Remove(renderer); }
    }
    void Restore(Entry entry)
    {
        if (entry.renderer != null) entry.renderer.sharedMaterials = entry.original;
        foreach (var material in entry.faded) if (material != null) Destroy(material);
    }
    void RestoreAll() { foreach (var entry in entries.Values) Restore(entry); entries.Clear(); }
    void OnDisable() { RestoreAll(); }
    void OnDestroy() { RestoreAll(); }
}
