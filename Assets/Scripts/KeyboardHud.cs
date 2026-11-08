using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★2026-11-01 오른쪽 아래 반투명 미니 키보드 — 키마다 그 키가 움직이는 다리 색으로 칠해 보여 준다
/// (이동 키 = 진하게, 다리 능력 키 E/Q·Y/R·O/U·]/P = 기린·테니스·오징어 중 하나라도 얻었으면 옅게).
/// 클릭하면 조작법 화면(PauseMenu.OpenControls — 예전 키보드 그림)이 바로 열린다.
/// 클릭은 커서가 보일 때만 된다(마우스로 시점을 돌리는 맵에서는 커서가 잠겨 있음).
/// Table.prefab 루트에 붙어 있음 — 크기·위치·투명도는 인스펙터에서.
/// </summary>
[DisallowMultipleComponent]
public class KeyboardHud : MonoBehaviour
{
    [Tooltip("미니 키보드 폭(1600×900 기준 px)")] public float width = 240f;
    [Tooltip("화면 오른쪽·아래 가장자리와의 간격(1600×900 기준 px)")] public float margin = 22f;
    [Tooltip("평소 투명도")] [Range(0f, 1f)] public float idleAlpha = .62f;
    [Tooltip("마우스를 올렸을 때 투명도")] [Range(0f, 1f)] public float hoverAlpha = .95f;

    /// <summary>지금 화면 아래에서 미니 키보드가 차지하는 높이(1600×900 기준 px) — 건너뛰기 표시가 이 위로 올라간다.</summary>
    public static float ReservedHeight { get; private set; }

    // 자판 그림(1620×710)에서 키가 있는 부분만 잘라 쓴다.
    static readonly Rect Crop = new Rect(40f, 92f, 1528f, 524f);
    static readonly string[][] MoveKeys = { new[] { "W", "A", "S", "D" }, new[] { "T", "F", "G", "H" }, new[] { "I", "J", "K", "L" }, new[] { "LeftBracket", "Semicolon", "Quote", "Enter" } };
    static readonly string[][] MoveLabels = { new[] { "W", "A", "S", "D" }, new[] { "T", "F", "G", "H" }, new[] { "I", "J", "K", "L" }, new[] { "[", ";", "'", "Enter" } };
    static readonly string[][] PowerKeys = { new[] { "E", "Q" }, new[] { "Y", "R" }, new[] { "O", "U" }, new[] { "RightBracket", "P" } };
    static readonly string[][] PowerLabels = { new[] { "E", "Q" }, new[] { "Y", "R" }, new[] { "O", "U" }, new[] { "]", "P" } };

    PauseMenu pause; HallwayStage hallway; bool menu, reserving;
    float fade, hoverT; bool hover;
    GUIStyle keyStyle;

    void Start()
    {
        pause = GetComponent<PauseMenu>();
        hallway = FindAnyObjectByType<HallwayStage>();
        menu = FindAnyObjectByType<StartScreenMenu>() != null || FindAnyObjectByType<NewStartScreenUI>() != null;
    }

    bool LocalPlayer()
    {
        var camera = Camera.main; if (camera == null) return false;
        var follow = camera.GetComponent<CameraFollow>();
        return follow == null || follow.target == null || follow.target.root == transform.root;
    }

    static float Scale => Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
    float BoxHeight => width * Crop.height / Crop.width + 16f;   // 자판 + 위아래 여백(★2026-11-08 아래 글씨 줄 없앰 — 그림만)
    Rect Box() { float s = Scale; return new Rect(Screen.width / s - margin - width - 16f, Screen.height / s - margin - BoxHeight, width + 16f, BoxHeight); }

    void Update()
    {
        if (menu || !LocalPlayer()) return;
        bool show = (hallway == null || hallway.IntroFinished) && AbilityHud.CanReadInput(this);
        fade = Mathf.MoveTowards(fade, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        reserving = fade > .05f; ReservedHeight = reserving ? BoxHeight + margin : 0f;

        var mouse = Mouse.current;
        bool clickable = fade > .6f && Cursor.visible && Cursor.lockState != CursorLockMode.Locked && mouse != null;
        Vector2 at = Vector2.zero;
        if (clickable) { var p = mouse.position.ReadValue(); at = new Vector2(p.x, Screen.height - p.y) / Scale; }
        hover = clickable && Box().Contains(at);
        hoverT = Mathf.MoveTowards(hoverT, hover ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        if (hover && mouse.leftButton.wasPressedThisFrame && pause != null) pause.OpenControls();   // CameraFollow는 일시정지 중이라 커서를 다시 잠그지 않음
    }

    void OnDisable() { if (reserving) ReservedHeight = 0f; reserving = false; }

    void OnGUI()
    {
        if (menu || fade <= 0.001f || Event.current.type != EventType.Repaint || Time.timeScale <= 0f || LetterOverlay.IsOpen || !LocalPlayer()) return;
        var image = LocalizedControlGuide.KeyboardImage; if (image == null) return;
        UiKit.ApplyKoreanFont();
        if (keyStyle == null) keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
        keyStyle.font = LocaleFonts.Body;
        var matrix = GUI.matrix; var old = GUI.color; int depth = GUI.depth; GUI.depth = -10;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);
        float a = fade * Mathf.Lerp(idleAlpha, hoverAlpha, hoverT);
        var box = Box(); box.y -= 3f * hoverT;   // 마우스를 올리면 살짝 떠오름
        Round(box, new Color(1f, 1f, 1f, .55f * a), 12f);
        GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Lin(new Color(.16f, .17f, .18f, .35f * a)), 1.5f, 12f);

        var img = new Rect(box.x + 8f, box.y + 8f, width, width * Crop.height / Crop.width);
        GUI.color = new Color(1f, 1f, 1f, .8f * a);
        GUI.DrawTextureWithTexCoords(img, image, new Rect(Crop.x / 1620f, 1f - (Crop.y + Crop.height) / 710f, Crop.width / 1620f, Crop.height / 710f));
        GUI.color = Color.white;

        bool powers = GiraffeMode.Unlocked || TennisMode.Unlocked || SquidMode.Unlocked;
        for (int slot = 0; slot < 4; slot++)
        {
            Color c = LegPalette.BySlot[slot];
            for (int n = 0; n < 4; n++) Paint(img, MoveKeys[slot][n], MoveLabels[slot][n], Color.Lerp(Color.white, c, .62f), Color.Lerp(c, Color.black, .25f), a);
            if (powers) for (int n = 0; n < 2; n++) Paint(img, PowerKeys[slot][n], PowerLabels[slot][n], Color.Lerp(Color.white, c, .26f), c, a * .8f);
        }

        GUI.color = old; GUI.matrix = matrix; GUI.depth = depth;
    }

    void Paint(Rect img, string key, string label, Color fill, Color border, float a)
    {
        if (!LocalizedControlGuide.KeyboardKey(key, out var px)) return;
        float s = img.width / Crop.width;
        var r = new Rect(img.x + (px.x - Crop.x) * s, img.y + (px.y - Crop.y) * s, px.width * s, px.height * s);
        border.a = a; fill.a = a;
        Round(r, border, 4f); Round(new Rect(r.x + 1.5f, r.y + 1.5f, r.width - 3f, r.height - 3f), fill, 3f);
        keyStyle.fontSize = Mathf.Max(6, Mathf.RoundToInt(r.height * (label.Length > 1 ? .36f : .6f)));   // 키 크기에 맞춤
        keyStyle.normal.textColor = keyStyle.hover.textColor = new Color(.08f, .09f, .1f, a);
        GUI.Label(new Rect(r.x, r.y - 1f, r.width, r.height), label, keyStyle);
    }

    static void Round(Rect r, Color c, float radius) => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Lin(c), 0, radius);
    static Color Lin(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
}
