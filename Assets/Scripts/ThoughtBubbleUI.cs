using UnityEngine;

/// <summary>
/// ★2026-09-08 상상 말풍선(IMGUI) — codexcow의 방식을 그대로 옮김: SDF 구름 셰이더(`Resources/Shaders/ThoughtBubble`, 0=구름 16:9 / 1=퍼프)로
/// `Graphics.DrawTexture(rect, white, material)` 드로잉, 1920×1080 가상 GUI 공간(GUI.matrix 균일 스케일+가로 중앙), 앵커 = 월드 점을 매 프레임 화면 투영
/// (카메라를 돌리면 말풍선이 따라 움직임), 꼬리 = 앵커 바로 위에서 말풍선 아래까지 살짝 굽은 곡선 위의 작아지는 퍼프 5개.
/// 사용: OnGUI에서 `Begin()` → `GuiPoint(world)` → `DrawBubble(rect, alpha, text)` + `DrawTail(rect, anchor, alpha)` → `End()`.
/// </summary>
public static class ThoughtBubbleUI
{
    public const float VW = 1920f, VH = 1080f;
    static Material mat; static Matrix4x4 savedMatrix; static GUIStyle text, textSmall; static int textSize = -1;

    static Material Mat()
    {
        if (mat != null) return mat;
        var sh = Resources.Load<Shader>("Shaders/ThoughtBubble"); if (sh == null) sh = Shader.Find("Hidden/Table/ThoughtBubble");
        if (sh == null) return null;
        mat = new Material(sh) { name = "Thought Bubble Material", hideFlags = HideFlags.DontSave };
        return mat;
    }

    public static float Scale => Mathf.Min(Screen.width / VW, Screen.height / VH);

    /// <summary>가상 1920×1080 공간으로 GUI.matrix 설정(균일 스케일, 가로 중앙).</summary>
    public static void Begin()
    {
        savedMatrix = GUI.matrix; float s = Scale;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - VW * s) * 0.5f, 0f, 0f), Quaternion.identity, Vector3.one * s);
    }
    public static void End() { GUI.matrix = savedMatrix; }

    /// <summary>월드 점 → 가상 GUI 좌표(카메라 뒤면 false).</summary>
    public static bool GuiPoint(Camera cam, Vector3 world, out Vector2 gui)
    {
        gui = Vector2.zero; if (cam == null) return false;
        Vector3 sp = cam.WorldToScreenPoint(world); if (sp.z <= 0f) return false;
        float s = Scale; float ox = (Screen.width - VW * s) * 0.5f;
        gui = new Vector2((sp.x - ox) / s, (Screen.height - sp.y) / s);
        return true;
    }

    /// <summary>앵커 위·오른쪽(또는 왼쪽)에 말풍선 사각형 배치(화면 안 클램프).</summary>
    public static Rect PlaceRect(Vector2 anchor, float w, float h, bool right = true, float dx = 110f, float dy = 100f)
    {
        var r = right ? new Rect(anchor.x + dx, anchor.y - dy - h, w, h) : new Rect(anchor.x - dx - w, anchor.y - dy - h, w, h);
        r.x = Mathf.Clamp(r.x, 24f, VW - w - 24f); r.y = Mathf.Clamp(r.y, 70f, VH - h - 60f);
        return r;
    }

    public static float EaseOut(float v) { float i = 1f - Mathf.Clamp01(v); return 1f - i * i * i; }
    public static Rect ScaleFromCenter(Rect r, float k) { var c = r.center; r.width *= k; r.height *= k; r.center = c; return r; }

    /// <summary>SDF 말풍선 모양(shape 0=구름, 1=퍼프).</summary>
    public static void DrawShape(Rect rect, float shape, float alpha, Color fill)
    {
        if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f) return;
        var m = Mat(); if (m == null) return;
        float s = Scale;
        m.SetFloat("_Shape", shape); m.SetFloat("_UseTex", 0f); m.SetFloat("_HeightPx", rect.height * s); m.SetFloat("_OutlinePx", Mathf.Max(1.5f, 4f * s));
        m.SetFloat("_Fade", 0f); m.SetFloat("_Alpha", alpha); m.SetFloat("_TexBlend", 0f); m.SetColor("_FillColor", fill.linear);
        Graphics.DrawTexture(rect, Texture2D.whiteTexture, m, 0);
    }
    public static void DrawPuff(Vector2 center, float size, float alpha) => DrawShape(new Rect(center.x - size * 0.5f, center.y - size * 0.42f, size, size * 0.84f), 1f, alpha, new Color(0.97f, 0.99f, 1f, 1f));

    /// <summary>구름 말풍선 + 가운데 정렬 손글씨(2줄 가능). k = 크기 비율(기준 폭 560).</summary>
    public static void DrawBubble(Rect rect, float alpha, string body, int fontSize = 34)
    {
        DrawShape(rect, 0f, alpha, new Color(0.98f, 0.99f, 1f, 1f));
        float k = rect.width / 560f; int fs = Mathf.RoundToInt(fontSize * k);
        if (text == null || textSize != fs) { textSize = fs; text = HandUI.Style(fs, HandUI.Ink); text.wordWrap = true; }
        var prev = GUI.color; GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(rect.x + rect.width * 0.09f, rect.y + rect.height * 0.14f, rect.width * 0.82f, rect.height * 0.72f), body, text);
        GUI.color = prev;
    }

    /// <summary>꼬리: 앵커(머리) 바로 위에서 말풍선 근처까지 살짝 굽은 곡선 위 퍼프 5개(먼 쪽이 큼) — codexcow DrawCurvedTail.</summary>
    public static void DrawTail(Rect bubble, Vector2 anchor, float alpha, float k = 1f)
    {
        bool right = bubble.center.x >= anchor.x;
        Vector2 near = anchor + new Vector2(0f, -44f * k);
        Vector2 far;
        if (near.y > bubble.yMax + 40f * k)
            far = right ? new Vector2(bubble.x + bubble.width * 0.15f, bubble.yMax + 30f * k) : new Vector2(bubble.x + bubble.width * 0.85f, bubble.yMax + 30f * k);
        else
        {
            float fy = Mathf.Clamp(near.y, bubble.y + bubble.height * 0.35f, bubble.yMax - bubble.height * 0.22f);
            far = right ? new Vector2(bubble.x - 30f * k, fy) : new Vector2(bubble.xMax + 30f * k, fy);
        }
        Vector2 chord = far - near; float len = chord.magnitude;
        Vector2 perp = len > 1f ? new Vector2(-chord.y, chord.x) / len : Vector2.right;
        if (perp.y < 0f) perp = -perp;
        float bow = Mathf.Min(40f * k, len * 0.22f);
        float[] size = { 46f, 39f, 33f, 28f, 24f }; float[] ts = { 1f, 0.78f, 0.58f, 0.42f, 0.28f };
        for (int i = 0; i < size.Length; i++)
        {
            Vector2 at = Vector2.Lerp(near, far, ts[i]) + perp * bow * Mathf.Sin(ts[i] * Mathf.PI);
            DrawPuff(at, size[i] * k, alpha);
        }
    }
}
