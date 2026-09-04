using UnityEngine;

/// <summary>
/// ★2026-09-05 바로서기 HUD — 손그림 도넛(Resources/HandUI/donut.png, scratchpad donut.py).
/// 우상단에 남은 횟수만큼 도넛을 한 줄로(기본 3개, 링을 먹어 3개 넘게도 늘어남), 쓰면 오른쪽부터 사라짐.
/// 기본 슬롯(3)보다 적으면 빈 자리는 흐린 도넛으로 남겨 '충전 가능'을 보여줌. 겁쟁이/치트 = 도넛 하나 + ∞.
/// 개수가 바뀌면 마지막 도넛이 통통 튐. 호스트/1인은 ArtSceneReset, 멀티 게스트는 NetStageSync가 호출(공용 드로어).
/// </summary>
public static class PoseDonutUI
{
    public const int MaxDrawn = 8;      // 한 줄에 그리는 최대 도넛 — 넘치면 마지막 칸에 +N
    public static float Size = 68f;     // 도넛 한 개 픽셀 크기
    const float Gap = 6f;

    static Texture2D donut;
    static bool tried;
    static GUIStyle numStyle, infStyle;
    static int lastCount = int.MinValue;
    static float bounceUntil;

    /// <summary>마지막으로 그린 줄의 아래쪽 y(픽셀) — 튜토리얼 안내문이 바로 아래에 붙을 때 사용.</summary>
    public static float RowBottom { get; private set; } = 74f;
    public static Rect RowRect { get; private set; }

    public static Texture2D Donut
    {
        get
        {
            if (donut == null && !tried) { tried = true; donut = Resources.Load<Texture2D>("HandUI/donut"); }
            return donut;
        }
    }

    /// <summary>텍스처가 없으면 false(호출 쪽 폴백).</summary>
    public static bool Available => Donut != null;

    /// <param name="count">남은 바로서기 횟수</param>
    /// <param name="unlimited">겁쟁이 난이도/치트 — 도넛 하나 + ∞</param>
    /// <param name="baseMax">기본 슬롯 수(빈 자리 흐리게 표시)</param>
    /// <param name="icon">모드 아이콘(기린/얼음) — 줄 왼쪽에</param>
    /// <param name="yTop">줄 위쪽 y(픽셀)</param>
    public static void Draw(int count, bool unlimited, int baseMax, Texture2D icon, float yTop = 14f)
    {
        var tex = Donut;
        if (tex == null) return;
        if (numStyle == null)
        {
            numStyle = HandUI.Style(30, HandUI.Ink, TextAnchor.MiddleCenter);
            infStyle = HandUI.Style(58, HandUI.Ink, TextAnchor.MiddleCenter, true);
        }

        // 개수 변화 → 통통(0.35s)
        if (lastCount != int.MinValue && lastCount != count) bounceUntil = Time.unscaledTime + 0.35f;
        lastCount = count;
        float bt = Mathf.Clamp01((bounceUntil - Time.unscaledTime) / 0.35f);   // 1→0
        float bounce = bt > 0f ? 1f + 0.28f * Mathf.Sin(bt * Mathf.PI) : 1f;

        float S = Size;
        int slots;
        if (unlimited) slots = 1;
        else slots = Mathf.Max(Mathf.Min(count, MaxDrawn), baseMax);
        float extraW = unlimited ? S * 0.9f : 0f;                         // ∞ 글자 자리
        float rowW = slots * S + (slots - 1) * Gap + extraW;
        float x0 = Screen.width - 18f - rowW;
        RowRect = new Rect(x0, yTop, rowW, S);
        RowBottom = yTop + S;

        if (icon != null)
        {
            float isz = S * 0.8f;
            GUI.DrawTexture(new Rect(x0 - 12f - isz, yTop + (S - isz) * 0.5f, isz, isz), icon, ScaleMode.ScaleToFit, true);
        }

        if (unlimited)
        {
            GUI.Label(new Rect(x0, yTop, extraW, S), "∞", infStyle);
            DrawDonut(new Rect(x0 + extraW, yTop, S, S), tex, 1f, bounce);
            return;
        }

        int drawn = Mathf.Min(count, MaxDrawn);
        bool overflow = count > MaxDrawn;
        for (int i = 0; i < slots; i++)
        {
            var r = new Rect(x0 + i * (S + Gap), yTop, S, S);
            bool filled = i < drawn;
            if (overflow && i == MaxDrawn - 1)
            {
                // 마지막 칸 = 도넛 + "+N"
                DrawDonut(r, tex, 1f, 1f);
                GUI.Label(new Rect(r.x, r.y + S * 0.62f, r.width, S * 0.5f), "+" + (count - (MaxDrawn - 1)), numStyle);
                continue;
            }
            bool last = filled && i == drawn - 1;
            DrawDonut(r, tex, filled ? 1f : 0.18f, last ? bounce : 1f);
        }
    }

    static void DrawDonut(Rect r, Texture2D tex, float alpha, float scale)
    {
        var pc = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        if (scale != 1f)
        {
            float w = r.width * scale, h = r.height * scale;
            r = new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
        }
        GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
        GUI.color = pc;
    }
}
