using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ESC로 일시정지. IMGUI로 메뉴를 그리되, 클릭/슬라이더는 New Input System(Mouse.current)으로
/// 직접 처리한다(이 프로젝트는 New Input System 전용이라 GUI.Button 클릭이 안 먹음).
/// 메뉴: Resume / Volume 슬라이더 / Color on-off(LegColorXray) / Main Menu(StartScreen).
/// 시작 화면 additive 배경에서는 동작하지 않음(활성 씬일 때만).
/// </summary>
[RequireComponent(typeof(LegColorXray))]
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Main Menu로 돌아갈 때 로드할 시작 화면 씬")]
    public string startScene = "StartScreen";

    LegColorXray xray;
    bool paused;
    bool draggingVolume;
    int openedFrame = -1;   // 메뉴를 연 프레임. 그 프레임의 클릭은 '바깥 클릭 닫기'로 처리하지 않음.

    GUIStyle titleStyle, btnStyle, labelStyle;
    Texture2D tex;

    const float PanelW = 380f, PanelH = 360f;
    Rect panel, titleRect, resumeBtn, volLabel, volSlider, colorBtn, menuBtn;

    void Awake() => xray = GetComponent<LegColorXray>();

    /// <summary>외부(시작화면 Option 표지판 등)에서 옵션 메뉴를 연다.</summary>
    public void OpenMenu() => SetPaused(true);
    public bool IsOpen => paused;

    // 시작 화면에서 SampleScene을 additive 배경으로 띄운 경우, 이 테이블은 비활성(배경) 씬 소속.
    bool IsBackground() => gameObject.scene != SceneManager.GetActiveScene();

    void ComputeLayout()
    {
        float x = (Screen.width - PanelW) * 0.5f;
        float y = (Screen.height - PanelH) * 0.5f;
        panel = new Rect(x, y, PanelW, PanelH);

        float ix = x + 26f, iw = PanelW - 52f;
        float cy = y + 24f;
        titleRect = new Rect(ix, cy, iw, 40f);  cy += 40f + 16f;
        resumeBtn = new Rect(ix, cy, iw, 46f);   cy += 46f + 16f;
        volLabel  = new Rect(ix, cy, iw, 22f);   cy += 22f + 4f;
        volSlider = new Rect(ix, cy, iw, 24f);   cy += 24f + 16f;
        colorBtn  = new Rect(ix, cy, iw, 46f);   cy += 46f + 12f;
        menuBtn   = new Rect(ix, cy, iw, 46f);
    }

    // Input System 마우스 좌표(좌하 원점) → GUI 좌표(좌상 원점)
    Vector2 GuiMouse()
    {
        var m = Mouse.current;
        if (m == null) return new Vector2(-1f, -1f);
        Vector2 p = m.position.ReadValue();
        return new Vector2(p.x, Screen.height - p.y);
    }

    void Update()
    {
        if (IsBackground()) return;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) SetPaused(!paused);

        if (!paused) return;

        ComputeLayout();
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (Time.frameCount == openedFrame) return; // 연 프레임의 클릭은 무시(즉시 닫힘 방지)
        Vector2 gm = GuiMouse();

        // 볼륨 슬라이더 드래그
        if (mouse.leftButton.wasPressedThisFrame && volSlider.Contains(gm)) draggingVolume = true;
        if (!mouse.leftButton.isPressed) draggingVolume = false;
        if (draggingVolume)
            AudioListener.volume = Mathf.Clamp01((gm.x - volSlider.x) / volSlider.width);

        // 버튼 클릭. 버튼·슬라이더가 아닌 바깥 아무데나 클릭하면 창을 닫는다.
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (resumeBtn.Contains(gm)) SetPaused(false);
            else if (colorBtn.Contains(gm)) { if (xray != null) xray.Toggle(); }
            else if (menuBtn.Contains(gm))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(startScene, LoadSceneMode.Single);
            }
            else if (!volSlider.Contains(gm)) SetPaused(false); // 바깥/패널 빈 곳 클릭 → 닫기
        }
    }

    void SetPaused(bool p)
    {
        paused = p;
        Time.timeScale = p ? 0f : 1f;
        if (p)
        {
            openedFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 게임으로 복귀: 마우스 시점(CameraFollow)을 위해 다시 잠금
            draggingVolume = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnGUI()
    {
        if (IsBackground() || !paused) return;
        EnsureStyles();
        ComputeLayout();
        Vector2 gm = GuiMouse();

        // 화면 어둡게
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
        // 패널
        Fill(panel, new Color(0.12f, 0.12f, 0.14f, 0.97f));

        GUI.Label(titleRect, "PAUSED", titleStyle);

        DrawButton(resumeBtn, "Resume", gm);

        GUI.Label(volLabel, "Volume   " + Mathf.RoundToInt(AudioListener.volume * 100f) + "%", labelStyle);
        DrawSlider(volSlider, AudioListener.volume);

        bool on = xray != null && xray.IsOn;
        DrawButton(colorBtn, "Color: " + (on ? "ON" : "OFF"), gm);

        DrawButton(menuBtn, "Main Menu", gm);
    }

    void DrawButton(Rect r, string label, Vector2 gm)
    {
        bool hover = r.Contains(gm);
        Fill(r, hover ? new Color(0.30f, 0.55f, 0.95f, 1f) : new Color(0.22f, 0.32f, 0.45f, 1f));
        GUI.Label(r, label, btnStyle);
    }

    void DrawSlider(Rect r, float v)
    {
        v = Mathf.Clamp01(v);
        Fill(r, new Color(0.25f, 0.25f, 0.28f, 1f));                       // 트랙
        Fill(new Rect(r.x, r.y, r.width * v, r.height), new Color(0.30f, 0.65f, 1f, 1f)); // 채움
        Fill(new Rect(r.x + r.width * v - 4f, r.y - 2f, 8f, r.height + 4f), Color.white);  // 손잡이
    }

    void Fill(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, tex);
        GUI.color = prev;
    }

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        tex = Texture2D.whiteTexture;

        titleStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = Color.white;

        btnStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btnStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        labelStyle.normal.textColor = Color.white;
    }
}
