using UnityEngine;

/// <summary>설정 메뉴의 언어 고르기: [‹] 현재 언어 [›]. 좌표는 JourneyMenuUI 캔버스(1600×900) 단위.</summary>
public static class LanguageSelector
{
    const float ArrowW = 88f;
    static Rect Choices(Rect row, bool label)
    {
        if (label) { float width = Mathf.Min(176f, row.width * .3f); row.x += width; row.width -= width; }
        return row;
    }
    public static Rect PreviousRect(Rect row, bool label = false)
    { row = Choices(row, label); return new Rect(row.x, row.y, ArrowW, row.height); }
    public static Rect NextRect(Rect row, bool label = false)
    { row = Choices(row, label); return new Rect(row.xMax - ArrowW, row.y, ArrowW, row.height); }
    public static bool HandleClick(Rect row, Vector2 pointer, bool label = false)
    {
        if (PreviousRect(row, label).Contains(pointer)) { L.Cycle(-1); return true; }
        if (NextRect(row, label).Contains(pointer)) { L.Cycle(1); return true; }
        return false;
    }
    public static void Draw(Rect row, Vector2 pointer, bool label = false)
    {
        Rect choices = Choices(row, label);
        if (label) JourneyMenuUI.Text(new Rect(row.x, row.y, choices.x - row.x - JourneyMenuUI.S2, row.height), L.T("언어", "Language", "言語", "语言"), JourneyMenuUI.TS);
        var name = new Rect(choices.x + ArrowW + JourneyMenuUI.S1, row.y, choices.width - 2 * (ArrowW + JourneyMenuUI.S1), row.height);
        JourneyMenuUI.Box(name, JourneyMenuUI.Kind.Soft);
        JourneyMenuUI.Literal(name, L.NativeName, JourneyMenuUI.TS, JourneyMenuUI.Ink, TextAnchor.MiddleCenter);
        JourneyMenuUI.ArrowVisual(PreviousRect(row, label), -1);
        JourneyMenuUI.ArrowVisual(NextRect(row, label), 1);
    }
}
