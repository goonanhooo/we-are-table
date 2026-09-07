using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★2026-09-07 키캡 튜토리얼 표지판 프리팹 생성기 v2. Tools/UI/Build Key Tutorial Board → Assets/Prefabs/KeyTutorialBoard.prefab.
/// 키보드 전체 그림은 PIL이 직접 그린 한 장(`Resources/HandUI/keyboard_base.png`, 종이+전체 ANSI 자판 선+연한 글자)이고,
/// 같은 스크립트가 쓴 `keyboard_layout.json`(키 이름→px 사각형)으로 빈 RectTransform 자리(`Key_<InputSystem Key>` / `Deco_*`)를 만든다.
/// 눌러야 하는 키는 KeyTutorial이 폭에 맞는 템플릿(KeyTemplate=1u, KeyTemplate_Enter=2.25u, KeyTemplate_Shift=2.75u — `keycap_w1/enter/shift.png`)을 그 자리에 복제.
/// 재질 `Guide/UI/KeyBoardOverlay.mat`(Custom/UIWorldOverlay, ZTest Always). 이미지 재생성 = scratch의 PIL 스크립트(CLAUDE.md 참조) → 이 메뉴 다시 실행.
/// </summary>
public static class KeyTutorialBoardBuilder
{
    const string PrefabPath = "Assets/Prefabs/KeyTutorialBoard.prefab";
    const string MatPath = "Assets/Guide/UI/KeyBoardOverlay.mat";
    const float BaseScale = 0.0019f;

    [System.Serializable] class KeyRect { public string name; public float x, y, w, h; }
    [System.Serializable] class LayoutFile { public int[] image; public KeyRect title; public List<KeyRect> keys; }

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

    [MenuItem("Tools/UI/Build Key Tutorial Board")]
    public static GameObject Build()
    {
        var layout = JsonUtility.FromJson<LayoutFile>(System.IO.File.ReadAllText("Assets/Resources/HandUI/keyboard_layout.json"));
        if (!AssetDatabase.IsValidFolder("Assets/Guide/UI")) AssetDatabase.CreateFolder("Assets/Guide", "UI");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("Custom/UIWorldOverlay")); AssetDatabase.CreateAsset(mat, MatPath); }
        var baseSp = SpriteOf("Assets/Resources/HandUI/keyboard_base.png");
        var cap1 = SpriteOf("Assets/Resources/HandUI/keycap_w1.png"); var capEnter = SpriteOf("Assets/Resources/HandUI/keycap_enter.png"); var capShift = SpriteOf("Assets/Resources/HandUI/keycap_shift.png");
        var gaeguB = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Gaegu-Bold.ttf"); var nanum = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NanumGothic-Regular.ttf");
        Color ink = new Color(0.08f, 0.08f, 0.1f, 1f);
        var size = new Vector2(layout.image[0], layout.image[1]);

        var root = new GameObject("KeyTutorialBoard", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        var rrt = root.GetComponent<RectTransform>(); rrt.sizeDelta = size; rrt.pivot = new Vector2(0.5f, 0.5f); root.transform.localScale = Vector3.one * BaseScale;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 50;
        // 종이+자판 그림(한 장)
        var paper = Img("Paper", root.transform, baseSp, mat, Vector2.zero, size, Color.white);
        var prt = paper.GetComponent<RectTransform>(); prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.pivot = new Vector2(0.5f, 0.5f); prt.anchoredPosition = Vector2.zero; prt.sizeDelta = Vector2.zero;
        // 제목(힌트 한 줄, 손글씨)
        Txt("Title", root.transform, gaeguB, 58, ink, mat, TextAnchor.MiddleLeft, new Vector2(layout.title.x, layout.title.y), new Vector2(layout.title.w, layout.title.h), "");
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
        // 테이블↔표지판 연결선(LineRenderer, 월드 좌표는 KeyTutorial이 매 프레임 지정)
        var tetherMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Guide/UI/KeyBoardTether.mat");
        if (tetherMat == null) { tetherMat = new Material(Shader.Find("Custom/XrayLine")); tetherMat.SetColor("_Color", Color.white); AssetDatabase.CreateAsset(tetherMat, "Assets/Guide/UI/KeyBoardTether.mat"); }
        var tetherGo = new GameObject("Tether", typeof(LineRenderer)); tetherGo.transform.SetParent(root.transform, false);
        var lr = tetherGo.GetComponent<LineRenderer>(); lr.sharedMaterial = tetherMat; lr.useWorldSpace = true; lr.positionCount = 3; lr.startWidth = 0.018f; lr.endWidth = 0.011f; lr.numCapVertices = 4; lr.numCornerVertices = 4; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
        lr.startColor = lr.endColor = new Color(0.1f, 0.1f, 0.13f, 0.55f);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[KeyTutorialBoardBuilder] built " + PrefabPath + " image=" + size + " keys=" + layout.keys.Count);
        return prefab;
    }
}
