using UnityEngine;

/// <summary>The same previous/current/next language control on the home and pause menus.</summary>
public static class LanguageSelector
{
    static GUIStyle nameStyle, arrowStyle, labelStyle;
    static Rect Choices(Rect row, bool label)
    {
        if (label) { float width = Mathf.Min(104f, row.width * .28f); row.x += width; row.width -= width; }
        return row;
    }
    public static Rect PreviousRect(Rect row, bool label = false)
    { row = Choices(row, label); return new Rect(row.x, row.y, 44f, row.height); }
    public static Rect NextRect(Rect row, bool label = false)
    { row = Choices(row, label); return new Rect(row.xMax - 44f, row.y, 44f, row.height); }
    public static bool HandleClick(Rect row, Vector2 pointer, bool label = false)
    {
        if (PreviousRect(row, label).Contains(pointer)) { L.Cycle(-1); return true; }
        if (NextRect(row, label).Contains(pointer)) { L.Cycle(1); return true; }
        return false;
    }
    public static void Draw(Rect row, Vector2 pointer, bool label = false)
    {
        if (nameStyle == null)
        {
            nameStyle = HandUI.Style(28, HandUI.Ink, TextAnchor.MiddleCenter);
            arrowStyle = HandUI.Style(30, HandUI.Ink, TextAnchor.MiddleCenter);
            labelStyle = HandUI.Style(24, HandUI.InkSoft, TextAnchor.MiddleLeft);
            nameStyle.wordWrap = arrowStyle.wordWrap = labelStyle.wordWrap = false;
        }
        Rect choices = Choices(row, label);
        if (label) GUI.Label(new Rect(row.x, row.y, choices.x - row.x, row.height), L.T("언어", "language", "言語", "语言"), labelStyle);
        var nameRect = new Rect(choices.x + 44f, row.y, choices.width - 88f, row.height);
        var content = new GUIContent(L.NativeName);
        nameStyle.fontSize = 28;
        while (nameStyle.fontSize > 18 && nameStyle.CalcSize(content).x > nameRect.width - 8f) nameStyle.fontSize--;
        GUI.Label(nameRect, content, nameStyle);
        Arrow(PreviousRect(row, label), "<", pointer);
        Arrow(NextRect(row, label), ">", pointer);
    }
    static void Arrow(Rect rect, string text, Vector2 pointer)
    {
        bool hover = rect.Contains(pointer);
        arrowStyle.fontSize = hover ? 34 : 30;
        GUI.Label(rect, text, arrowStyle);
        if (hover && HandUI.Underline != null)
            GUI.DrawTexture(new Rect(rect.x + 10f, rect.yMax - 8f, rect.width - 20f, 10f), HandUI.Underline);
    }
}
