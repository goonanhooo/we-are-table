using UnityEngine;

/// <summary>
/// ★2026-07-13 손그림 UI 헬퍼(B안) — 개구체(Gaegu) 폰트 + PIL로 생성한 손그림 테두리/밑줄 텍스처.
/// 에셋: Resources/Fonts/Gaegu-Bold, Resources/HandUI/box_wide·box_panel·box_tile·underline.
/// 시작 화면 계열(StartScreenMenu/LevelSelect)에서 사용 — 인게임 HUD는 기존 UiKit 유지.
/// </summary>
public static class HandUI
{
    static Font gaegu;
    public static Font Gaegu
    {
        get { if (gaegu == null) gaegu = Resources.Load<Font>("Fonts/Gaegu-Bold"); return gaegu; }
    }

    static Texture2D boxWide, boxPanel, boxTile, underline;
    public static Texture2D BoxWide  { get { if (boxWide == null)  boxWide  = Resources.Load<Texture2D>("HandUI/box_wide");  return boxWide; } }
    public static Texture2D BoxPanel { get { if (boxPanel == null) boxPanel = Resources.Load<Texture2D>("HandUI/box_panel"); return boxPanel; } }
    public static Texture2D BoxTile  { get { if (boxTile == null)  boxTile  = Resources.Load<Texture2D>("HandUI/box_tile");  return boxTile; } }
    public static Texture2D Underline{ get { if (underline == null) underline = Resources.Load<Texture2D>("HandUI/underline"); return underline; } }

    public static readonly Color Ink = new Color(0.09f, 0.09f, 0.09f, 1f);
    public static readonly Color InkSoft = new Color(0.32f, 0.32f, 0.32f, 1f);

    /// <summary>흰 종이 + 손그림 테두리(박스 텍스처 스트레치).</summary>
    public static void Paper(Rect r, Texture2D box, float bgAlpha = 0.96f)
    {
        UiKit.Fill(r, new Color(1f, 1f, 1f, bgAlpha));
        if (box != null) GUI.DrawTexture(r, box, ScaleMode.StretchToFill);
    }

    /// <summary>손글씨 스타일 생성(OnGUI 안에서만).
    /// ⚠️hover/active 색도 normal과 동일하게 고정 — 스킨 label의 hover 색이 상속되면
    /// 마우스만 올려도 글자가 흐려진다(사용자 "호버로 색이 흐려져" 원인, 2026-07-14).</summary>
    public static GUIStyle Style(int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, bool bold = false)
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor };
        if (Gaegu != null) st.font = Gaegu;
        if (bold) st.fontStyle = FontStyle.Bold;
        st.normal.textColor = color;
        st.hover.textColor = color;
        st.active.textColor = color;
        st.focused.textColor = color;
        st.hover.background = null;
        st.active.background = null;
        return st;
    }
}
