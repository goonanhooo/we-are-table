using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>Localizes authored canvas/world labels, including inactive help panels.</summary>
[DefaultExecutionOrder(1000)]
public class LocaleTextBindings : MonoBehaviour
{
    readonly List<Text> ui = new List<Text>();
    readonly List<TMP_Text> tmp = new List<TMP_Text>();
    readonly List<TextMesh> meshes = new List<TextMesh>();
    readonly Dictionary<Font, TMP_FontAsset> tmpFonts = new Dictionary<Font, TMP_FontAsset>();
    float scanAt; int version = -1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<LocaleTextBindings>() != null) return;
        var go = new GameObject("Localized text"); DontDestroyOnLoad(go); go.AddComponent<LocaleTextBindings>();
    }
    void OnEnable() { SceneManager.sceneLoaded += Loaded; Scan(); }
    void OnDisable() { SceneManager.sceneLoaded -= Loaded; }
    void Loaded(Scene s, LoadSceneMode m) { Scan(); }
    void Scan()
    {
        ui.Clear(); tmp.Clear(); meshes.Clear();
        ui.AddRange(FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        tmp.AddRange(FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        meshes.AddRange(FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        scanAt = Time.unscaledTime + 2f; version = -1;
    }
    void LateUpdate()
    {
        if (Time.unscaledTime >= scanAt) Scan();
        bool changed = version != L.Version; version = L.Version;
        foreach (var t in ui) if (t != null) { t.text = L.Text(t.text); if (changed) t.font = LocaleFonts.Body; }
        foreach (var t in meshes) if (t != null)
        {
            t.text = L.Text(t.text);
            if (changed) { t.font = LocaleFonts.Body; var r = t.GetComponent<Renderer>(); if (r != null && t.font != null) r.sharedMaterial = t.font.material; }
        }
        foreach (var t in tmp) if (t != null)
        {
            t.text = L.Text(t.text);
            if (!changed || LocaleFonts.Body == null) continue;
            if (!tmpFonts.TryGetValue(LocaleFonts.Body, out var f))
            {
                f = TMP_FontAsset.CreateFontAsset(LocaleFonts.Body, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                f.isMultiAtlasTexturesEnabled = true; tmpFonts[LocaleFonts.Body] = f;
            }
            t.font = f;
        }
    }
}
