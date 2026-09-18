using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★2026-09-07 키캡 튜토리얼 표지판 프리팹 생성기 v2. Tools/UI/Build Key Tutorial Board → Assets/Prefabs/KeyTutorialBoard.prefab.
/// 키보드 전체 그림은 PIL이 직접 그린 한 장(`Resources/HandUI/keyboard_base.png`, 종이+전체 ANSI 자판 선+연한 글자)이고,
/// 같은 스크립트가 쓴 `keyboard_layout.json`(키 이름→px 사각형)으로 빈 RectTransform 자리(`Key_<InputSystem Key>` / `Deco_*`)를 만든다.
/// 눌러야 하는 키는 KeyTutorial이 폭에 맞는 템플릿(KeyTemplate=1u, KeyTemplate_Enter=2.25u, KeyTemplate_Shift=2.75u — `keycap_w1/enter/shift.png`)을 그 자리에 복제.
/// ★v9b(2026-09-18 사용자: "키보드 위 표시도 키보드랑 어울리게"): 제목/안내는 IMGUI 칩 대신 **같은 캔버스의 판(TitleTab 위 / NoteTab 아래)** —
///   `keyboard_plate.png`(자판 종이와 같은 색·테두리 5px·그림자, 9-슬라이스)라 표지판과 같이 기울고(원근)·둥실거리고·반투명해진다. 폭은 KeyTutorial이 글자에 맞춤.
/// 재질 `Guide/UI/KeyBoardOverlay.mat`(Custom/UIWorldOverlay, ZTest Always). 이미지 재생성 = scratch의 PIL 스크립트(CLAUDE.md 참조) → 이 메뉴 다시 실행.
/// </summary>
public static class KeyTutorialBoardBuilder
{
    const string PrefabPath = "Assets/Prefabs/KeyTutorialBoard.prefab";
    const string MatPath = "Assets/Guide/UI/KeyBoardOverlay.mat";
    const float BaseScale = 0.0019f;

    [System.Serializable] class KeyRect { public string name; public float x, y, w, h; }
    [System.Serializable] class LayoutFile { public int[] image; public KeyRect title; public KeyRect note; public List<KeyRect> keys; }

    static Sprite SpriteOf(string path, Vector4 border = default)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single || imp.mipmapEnabled || imp.spriteBorder != border))
        { imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.spriteBorder = border; imp.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 topLeft, Vector2 size)
    {
        var g = new GameObject(name, typeof(RectTransform)); g.transform.SetParent(parent, false);
        var rt = g.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y); rt.sizeDelta = size;
        return rt;
    }
    static Image Img(string name, Transform parent, Sprite sp, Material mat, Vector2 topLeft, Vector2 size, Color col)
    {
        var rt = Rect(name, parent, topLeft, size);
        var im = rt.gameObject.AddComponent<Image>(); im.sprite = sp; im.material = mat; im.color = col; im.raycastTarget = false; im.type = Image.Type.Simple;
        return im;
    }
    static Text Txt(string name, Transform parent, Font font, int size, Color col, Material mat, TextAnchor anchor, Vector2 topLeft, Vector2 size2, string text, FontStyle style = FontStyle.Normal)
    {
        var rt = Rect(name, parent, topLeft, size2);
        var t = rt.gameObject.AddComponent<Text>(); t.font = font; t.fontSize = size; t.color = col; t.material = mat; t.alignment = anchor; t.text = text; t.fontStyle = style; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    /// <summary>자판 스타일 판 + 가운데 글씨. anchor = 표지판 모서리, pivot = 판의 어느 모서리를 거기에 붙일지. 종이(Paper)보다 먼저 그려 그림자가 종이 밑으로 들어간다.</summary>
    static void Tab(string name, Transform parent, Sprite plate, Material mat, Font font, int fontSize, Color ink, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 pos)
    {
        var im = Img(name, parent, plate, mat, Vector2.zero, size, Color.white); im.type = Image.Type.Sliced;
        var rt = im.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var t = Txt("Text", im.transform, font, fontSize, ink, mat, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, "");
        var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
        trt.offsetMin = new Vector2(8f, 15f); trt.offsetMax = new Vector2(-15f, -8f);   // 판의 테두리 안(그림자 여백 제외) 가운데
        t.lineSpacing = 0.95f;
        im.transform.SetAsFirstSibling();   // 종이보다 먼저 그림 → 판 그림자가 종이 밑으로
        im.gameObject.SetActive(false);
    }

    [MenuItem("Tools/UI/Build Key Tutorial Board")]
    public static GameObject Build()
    {
        var layout = JsonUtility.FromJson<LayoutFile>(System.IO.File.ReadAllText("Assets/Resources/HandUI/keyboard_layout.json"));
        if (!AssetDatabase.IsValidFolder("Assets/Guide/UI")) AssetDatabase.CreateFolder("Assets/Guide", "UI");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("Custom/UIWorldOverlay")); AssetDatabase.CreateAsset(mat, MatPath); }
        var baseSp = SpriteOf("Assets/Resources/HandUI/keyboard_base.png");
        var cap1 = SpriteOf("Assets/Resources/HandUI/keycap_w1.png"); var capEnter = SpriteOf("Assets/Resources/HandUI/keycap_enter.png"); var capShift = SpriteOf("Assets/Resources/HandUI/keycap_shift.png");
        var gaeguB = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Gaegu-Bold.ttf"); var gaeguR = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Gaegu-Regular.ttf"); var nanum = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NanumGothic-Regular.ttf");
        Color ink = new Color(0.08f, 0.08f, 0.1f, 1f);
        var size = new Vector2(layout.image[0], layout.image[1]);

        var root = new GameObject("KeyTutorialBoard", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        var rrt = root.GetComponent<RectTransform>(); rrt.sizeDelta = size; rrt.pivot = new Vector2(0.5f, 0.5f); root.transform.localScale = Vector3.one * BaseScale;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 50;
        // 종이+자판 그림(한 장)
        var paper = Img("Paper", root.transform, baseSp, mat, Vector2.zero, size, Color.white);
        var prt = paper.GetComponent<RectTransform>(); prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.pivot = new Vector2(0.5f, 0.5f); prt.anchoredPosition = Vector2.zero; prt.sizeDelta = Vector2.zero;
        // 제목 판(표지판 위 왼쪽 정렬) / 안내 판(아래 왼쪽 정렬) — 자판 종이와 같은 스타일의 9-슬라이스 판 + 키캡 글씨와 같은 손글씨
        //   판 스프라이트 여백: 왼/위 8(자판 그림 여백과 같아 테두리가 일직선), 오른/아래 15(그림자). 표지판 테두리와 간격 16.
        var plate = SpriteOf("Assets/Resources/HandUI/keyboard_plate.png", new Vector4(42f, 49f, 49f, 42f));   // (left, bottom, right, top)
        Tab("TitleTab", root.transform, plate, mat, gaeguB, 124, ink, new Vector2(900f, 172f + 23f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, -6f));
        Tab("NoteTab", root.transform, plate, mat, gaeguB, 84, new Color(0.2f, 0.2f, 0.23f, 1f), new Vector2(1400f, 236f + 23f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 6f));
        // 키 자리(빈 RectTransform — 그림은 Paper에 이미 있음)
        var keys = Rect("Keys", root.transform, Vector2.zero, Vector2.zero);
        foreach (var k in layout.keys) Rect(k.name, keys, new Vector2(k.x, k.y), new Vector2(k.w, k.h));
        // 눌러야 하는 키 템플릿 3종(폭별) — 비활성
        (string, Sprite, float)[] tpls = { ("KeyTemplate", cap1, 88f), ("KeyTemplate_Enter", capEnter, 213f), ("KeyTemplate_Shift", capShift, 263f) };
        foreach (var t in tpls)
        {
            float pad = 10f; var im = Img(t.Item1, root.transform, t.Item2, mat, Vector2.zero, new Vector2(t.Item3 + pad * 2f, 88f + pad * 2f), Color.white);
            im.gameObject.AddComponent<CanvasGroup>();
            Txt("Label", im.transform, gaeguB, 44, ink, mat, TextAnchor.MiddleCenter, new Vector2(pad, pad), new Vector2(t.Item3, 88f), "W");
            Img("Bar", im.transform, null, mat, new Vector2(pad + t.Item3 * 0.16f, pad + 88f - 15f), new Vector2(t.Item3 * 0.68f, 7f), new Color(0.3f, 0.8f, 0.3f, 0.9f));
            im.gameObject.SetActive(false);
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[KeyTutorialBoardBuilder] built " + PrefabPath + " image=" + size + " keys=" + layout.keys.Count);
        return prefab;
    }
}
