using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bundled fonts; no dependency on fonts installed on the player's OS.</summary>
public static class LocaleFonts
{
    static readonly Font[] fonts = new Font[4];
    static Font hand;
    static readonly List<WeakReference<GUIStyle>> styles = new List<WeakReference<GUIStyle>>();
    public static Font Body
    {
        get
        {
            int i = (int)L.Current;
            if (fonts[i] == null) fonts[i] = Resources.Load<Font>(i == 2 ? "Fonts/CJK/NotoSansCJKjp-Regular" : i == 3 ? "Fonts/CJK/NotoSansCJKsc-Regular" : "Fonts/NanumGothic-Regular");
            return fonts[i];
        }
    }
    public static Font Hand
    {
        get
        {
            if ((int)L.Current >= 2) return Body;
            if (hand == null) hand = Resources.Load<Font>("Fonts/Gaegu-Bold");
            return hand != null ? hand : Body;
        }
    }
    /// <summary>★2026-11-01 말풍선 글씨: 굵지 않은 얇은 손글씨(Gaegu-Regular). 일본어·중국어는 본문 글꼴.</summary>
    public static Font Thin
    {
        get
        {
            if ((int)L.Current >= 2) return Body;
            if (thin == null) thin = Resources.Load<Font>("Fonts/Gaegu-Regular");
            return thin != null ? thin : Hand;
        }
    }
    static Font thin;
    static readonly List<WeakReference<GUIStyle>> thinStyles = new List<WeakReference<GUIStyle>>();
    public static void Track(GUIStyle style) { style.font = Hand; styles.Add(new WeakReference<GUIStyle>(style)); }
    public static void TrackThin(GUIStyle style) { style.font = Thin; thinStyles.Add(new WeakReference<GUIStyle>(style)); }
    public static void RefreshStyles()
    {
        for (int i = styles.Count - 1; i >= 0; i--)
            if (styles[i].TryGetTarget(out var style)) style.font = Hand; else styles.RemoveAt(i);
        for (int i = thinStyles.Count - 1; i >= 0; i--)
            if (thinStyles[i].TryGetTarget(out var style)) style.font = Thin; else thinStyles.RemoveAt(i);
    }
    public static void Apply()
    {
        if (Body == null) return;
        GUI.skin.font = Body;
        // GUIStyle copies can inherit an explicit font from Unity's editor skin.
        foreach (var style in new[] { GUI.skin.label, GUI.skin.button, GUI.skin.textField, GUI.skin.textArea, GUI.skin.box }) style.font = null;
    }
}
