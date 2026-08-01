using UnityEngine;

/// <summary>
/// 공용 UI 룩(IMGUI): ★얇은 검은 테두리 + 하얀 배경 + 검은 글씨★ 로 전 UI 통일.
/// 모든 OnGUI 화면(PauseMenu/LevelSelect/NetDebugPanel/배너/편지/치트 힌트)이 이 헬퍼를 쓴다.
/// ⚠️ GUIStyle은 OnGUI 안에서만 생성 가능 — 프로퍼티 접근도 OnGUI 안에서.
/// </summary>
public static class UiKit
{
    public const float Border = 2f;   // '얇은' 검은 테두리 두께(px)
    static readonly Color Ink = new Color(0.08f, 0.08f, 0.08f, 1f);

    // ★WebGL 한글 폰트(2026-07-12): 웹엔 OS 폰트가 없어 기본 폰트로는 한글이 전부 사라짐(□/공백).
    //   내장 나눔고딕(Resources/Fonts)을 GUI.skin에 꽂는다 — skin은 세션 공유라 한 번만 적용돼도
    //   모든 OnGUI(캐시된 GUIStyle 포함: font==null이면 skin 폰트로 폴백)가 한글을 그린다.
    //   각 UI의 OnGUI 첫 줄에서 호출(참조 비교 가드라 매 프레임 호출해도 공짜).
    static Font kfont;
    static bool kfontTried;
    public static void ApplyKoreanFont()
    {
        if (kfont == null)
        {
            if (kfontTried) return;
            kfontTried = true;
            kfont = Resources.Load<Font>("Fonts/NanumGothic-Regular");
            if (kfont == null) { Debug.LogWarning("[UiKit] 한글 폰트 로드 실패 — Resources/Fonts/NanumGothic-Regular"); return; }
        }
        if (GUI.skin.font != kfont) GUI.skin.font = kfont;
    }

    static GUIStyle title, btn, label, center, small;
    public static GUIStyle Title { get { Ensure(); return title; } }
    public static GUIStyle Btn { get { Ensure(); return btn; } }
    public static GUIStyle Label { get { Ensure(); return label; } }
    public static GUIStyle Center { get { Ensure(); return center; } }
    public static GUIStyle Small { get { Ensure(); return small; } }

    /// <summary>흰 배경 + 검은 테두리 패널.</summary>
    public static void Panel(Rect r) => Panel(r, 1f);
    public static void Panel(Rect r, float bgAlpha)
    {
        Fill(new Rect(r.x - Border, r.y - Border, r.width + Border * 2f, r.height + Border * 2f), Color.black);
        Fill(r, new Color(1f, 1f, 1f, bgAlpha));
    }

    /// <summary>버튼: 흰 배경+검은 테두리+검은 글씨. ★2026-07-14 호버 색 변화 전면 제거(사용자 요청).</summary>
    public static void Button(Rect r, string text, bool hover)
    {
        Panel(r);
        GUI.Label(r, text, Btn);
    }

    /// <summary>슬라이더: 흰 트랙+검은 테두리, 검은 채움.</summary>
    public static void Slider(Rect r, float v)
    {
        v = Mathf.Clamp01(v);
        Panel(r);
        Fill(new Rect(r.x, r.y, r.width * v, r.height), new Color(0.15f, 0.15f, 0.15f, 1f));
    }

    /// <summary>제목/안내문을 어두운 배경 위에 얹을 때 쓰는 흰 칩(가운데 정렬 폭 지정).</summary>
    public static Rect Chip(float centerX, float y, float w, float h)
    {
        var r = new Rect(centerX - w * 0.5f, y, w, h);
        Panel(r, 0.97f);
        return r;
    }

    public static void Fill(Rect r, Color c)
    {
        var p = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = p;
    }

    static void Ensure()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = Ink;
        btn = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btn.normal.textColor = Ink;
        label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        label.normal.textColor = Ink;
        center = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter };
        center.normal.textColor = Ink;
        small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        small.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    }
}
