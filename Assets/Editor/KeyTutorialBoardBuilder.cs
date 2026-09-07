using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ★2026-09-07 키캡 튜토리얼 표지판 프리팹 생성기. Tools/UI/Build Key Tutorial Board → Assets/Prefabs/KeyTutorialBoard.prefab.
/// 월드 캔버스(스케일 0.0019 → 약 3.6×1.25m) 종이 판 위에 **전체 QWERTY 자판을 선(outline)으로**, 눌러야 하는 키는 KeyTutorial이
/// KeyTemplate(채운 키캡+글자+다리 색 띠)을 같은 자리에 복제해 올린다. 재질 `Guide/UI/KeyBoardOverlay.mat`(Custom/UIWorldOverlay, ZTest Always).
/// 키 GO 이름 = "Key_" + InputSystem Key 이름(KeyTutorial이 이름으로 자리 조회). 장식 키(Tab/Caps/Enter/Space…)는 "Deco_*".
/// </summary>
public static class KeyTutorialBoardBuilder
{
    const string PrefabPath = "Assets/Prefabs/KeyTutorialBoard.prefab";
    const string MatPath = "Assets/Guide/UI/KeyBoardOverlay.mat";
    public const float Pitch = 100f, KeySize = 86f;
    public static readonly Vector2 BoardSize = new Vector2(1900f, 660f);
    public static readonly Vector2 KeysOrigin = new Vector2(70f, -118f);   // 왼쪽 위 기준(px)

    struct K { public string name; public string label; public float col; public int row; public float w; public bool arrowFont; public K(string n, string l, float c, int r, float w = 1f, bool a = false) { name = n; label = l; col = c; row = r; this.w = w; arrowFont = a; } }

    static List<K> Layout()
    {
        var L = new List<K>();
        L.Add(new K("Deco_Backquote", "`", 0f, 0));
        string[] digits = { "Digit1", "Digit2", "Digit3", "Digit4", "Digit5", "Digit6", "Digit7", "Digit8", "Digit9", "Digit0" };
        for (int i = 0; i < 10; i++) L.Add(new K("Key_" + digits[i], ((i + 1) % 10).ToString(), 1 + i, 0));
        L.Add(new K("Key_Minus", "-", 11f, 0)); L.Add(new K("Key_Equals", "=", 12f, 0)); L.Add(new K("Deco_Backspace", "⌫", 13.65f, 0, 2.3f, true));
        L.Add(new K("Deco_Tab", "Tab", 0.2f, 1, 1.4f));
        string q = "QWERTYUIOP"; for (int i = 0; i < 10; i++) L.Add(new K("Key_" + q[i], q[i].ToString(), 1.5f + i, 1));
        L.Add(new K("Key_LeftBracket", "[", 11.5f, 1)); L.Add(new K("Key_RightBracket", "]", 12.5f, 1)); L.Add(new K("Deco_Backslash", "\\", 13.85f, 1, 1.7f));
        L.Add(new K("Deco_CapsLock", "Caps", 0.32f, 2, 1.65f));
        string a = "ASDFGHJKL"; for (int i = 0; i < 9; i++) L.Add(new K("Key_" + a[i], a[i].ToString(), 1.75f + i, 2));
        L.Add(new K("Key_Semicolon", ";", 10.75f, 2)); L.Add(new K("Key_Quote", "'", 11.75f, 2)); L.Add(new K("Deco_Enter", "↵", 13.35f, 2, 2.3f, true));
        L.Add(new K("Deco_LeftShift", "Shift", 0.55f, 3, 2.1f));
        string z = "ZXCVBNM"; for (int i = 0; i < 7; i++) L.Add(new K("Key_" + z[i], z[i].ToString(), 2.25f + i, 3));
        L.Add(new K("Key_Comma", ",", 9.25f, 3)); L.Add(new K("Key_Period", ".", 10.25f, 3)); L.Add(new K("Key_Slash", "/", 11.25f, 3)); L.Add(new K("Key_RightShift", "Shift", 13.15f, 3, 2.7f));
        L.Add(new K("Deco_Space", "", 6.6f, 4, 6.4f));
        L.Add(new K("Key_UpArrow", "↑", 15.5f, 3, 1f, true)); L.Add(new K("Key_LeftArrow", "←", 14.5f, 4, 1f, true)); L.Add(new K("Key_DownArrow", "↓", 15.5f, 4, 1f, true)); L.Add(new K("Key_RightArrow", "→", 16.5f, 4, 1f, true));
        return L;
    }

    static Sprite SpriteOf(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single || imp.mipmapEnabled))
        { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Image Img(string name, Transform parent, Sprite sp, Material mat, Vector2 anchoredPos, Vector2 size, Color col)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        g.transform.SetParent(parent, false);
        var rt = g.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
        var im = g.GetComponent<Image>(); im.sprite = sp; im.material = mat; im.color = col; im.raycastTarget = false; im.type = Image.Type.Simple; im.preserveAspect = false;
        return im;
    }
    static Text Txt(string name, Transform parent, Font font, int size, Color col, Material mat, TextAnchor anchor, Vector2 anchoredPos, Vector2 sizeD, string text, FontStyle style = FontStyle.Normal)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        g.transform.SetParent(parent, false);
        var rt = g.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeD;
        var t = g.GetComponent<Text>(); t.font = font; t.fontSize = size; t.color = col; t.material = mat; t.alignment = anchor; t.text = text; t.fontStyle = style; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    [MenuItem("Tools/UI/Build Key Tutorial Board")]
    public static GameObject Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Guide/UI")) AssetDatabase.CreateFolder("Assets/Guide", "UI");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("Custom/UIWorldOverlay")); AssetDatabase.CreateAsset(mat, MatPath); }
        var capSp = SpriteOf("Assets/Resources/HandUI/keycap.png"); var outSp = SpriteOf("Assets/Resources/HandUI/keyoutline.png"); var boardSp = SpriteOf("Assets/Resources/HandUI/board.png");
        var gaeguB = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Gaegu-Bold.ttf"); var gaeguR = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Gaegu-Regular.ttf"); var nanum = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NanumGothic-Regular.ttf");
        Color ink = new Color(0.09f, 0.09f, 0.09f, 1f), faint = new Color(0.2f, 0.2f, 0.22f, 0.38f), lineCol = new Color(0.22f, 0.22f, 0.24f, 0.55f);

        var root = new GameObject("KeyTutorialBoard", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        var rrt = root.GetComponent<RectTransform>(); rrt.sizeDelta = BoardSize; rrt.pivot = new Vector2(0.5f, 0.5f); root.transform.localScale = Vector3.one * 0.0019f;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 50;
        // 종이 판
        var paper = Img("Paper", root.transform, boardSp, mat, Vector2.zero, BoardSize, new Color(1f, 1f, 1f, 0.97f));
        var prt = paper.GetComponent<RectTransform>(); prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.pivot = new Vector2(0.5f, 0.5f); prt.anchoredPosition = Vector2.zero; prt.sizeDelta = Vector2.zero;
        // 제목(힌트 한 줄)
        Txt("Title", root.transform, gaeguB, 46, ink, mat, TextAnchor.MiddleLeft, new Vector2(KeysOrigin.x + 6f, -40f), new Vector2(BoardSize.x - 140f, 60f), "");
        // 자판 선(outline) 전체
        var keys = new GameObject("Keys", typeof(RectTransform)); keys.transform.SetParent(root.transform, false);
        var krt = keys.GetComponent<RectTransform>(); krt.anchorMin = krt.anchorMax = new Vector2(0f, 1f); krt.pivot = new Vector2(0f, 1f); krt.anchoredPosition = KeysOrigin; krt.sizeDelta = Vector2.zero;
        foreach (var k in Layout())
        {
            float w = KeySize + (k.w - 1f) * Pitch;
            var im = Img(k.name, keys.transform, outSp, mat, new Vector2(k.col * Pitch, -k.row * Pitch), new Vector2(w, KeySize), lineCol);
            if (k.w > 1.01f) im.type = Image.Type.Sliced;   // 넓은 키는 9-slice로 테두리 유지
            if (!string.IsNullOrEmpty(k.label))
                Txt("Label", im.transform, k.arrowFont ? nanum : gaeguR, k.label.Length > 2 ? 22 : 30, faint, mat, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(w, KeySize), k.label);
        }
        // 눌러야 하는 키 템플릿(비활성)
        var tpl = Img("KeyTemplate", root.transform, capSp, mat, Vector2.zero, new Vector2(KeySize, KeySize), Color.white);
        tpl.gameObject.AddComponent<CanvasGroup>();
        Txt("Label", tpl.transform, gaeguB, 42, ink, mat, TextAnchor.MiddleCenter, new Vector2(0f, 2f), new Vector2(KeySize, KeySize), "W");
        var bar = Img("Bar", tpl.transform, null, mat, new Vector2(KeySize * 0.16f, -(KeySize - 15f)), new Vector2(KeySize * 0.68f, 7f), new Color(0.3f, 0.8f, 0.3f, 0.9f));
        tpl.gameObject.SetActive(false);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[KeyTutorialBoardBuilder] built " + PrefabPath + " keys=" + Layout().Count);
        return prefab;
    }
}
