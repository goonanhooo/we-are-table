using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ESC로 일시정지. IMGUI로 메뉴를 그리되, 클릭/슬라이더는 New Input System(Mouse.current)으로
/// 직접 처리한다(이 프로젝트는 New Input System 전용이라 GUI.Button 클릭이 안 먹음).
/// 메뉴: Resume / Volume 슬라이더 3개 / Color on-off / Controls(조작법) / Main Menu(StartScreen).
/// 조작법 화면은 **현재 해금된 조작 이미지만** 페이지로 보여준다(기본 조작 + 기린 힘 해금 시 기린 조작).
/// 시작 화면 additive 배경에서는 동작하지 않음(활성 씬일 때만).
/// </summary>
[RequireComponent(typeof(LegColorXray))]
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Main Menu로 돌아갈 때 로드할 시작 화면 씬")]
    public string startScene = "StartScreen";

    [Header("조작법 이미지(프리팹에 할당)")]
    [Tooltip("기본 조작(이동) 이미지 — 항상 해금")]
    public Texture2D controlsMove;
    [Tooltip("기린의 힘 조작 이미지 — GiraffeMode.Unlocked 일 때만 표시")]
    public Texture2D controlsGiraffe;
    [Tooltip("우주오징어 조작 이미지 — SquidMode.Unlocked 일 때만 표시")]
    public Texture2D controlsSquid;

    LegColorXray xray;
    bool paused;
    bool showControls;      // 조작법 화면 표시 중
    int controlsPage;       // 현재 조작법 페이지
    int dragSlider;         // 드래그 중인 슬라이더: 0 없음 / 1 마스터 / 2 BGM / 3 SFX
    int openedFrame = -1;   // 메뉴를 연 프레임. 그 프레임의 클릭은 '바깥 클릭 닫기'로 처리하지 않음.

    GUIStyle titleStyle, btnStyle, labelStyle, centerStyle;
    GUIStyle handTitle, handItem, handLabel, handItemC, handTitleC, handPage;   // ★손글씨(시작 화면+인게임)
    Texture2D tex;

    // ★2026-07-14 시작 화면(StartScreenMenu 존재)에선 가운데 창 대신 왼쪽 손글씨 메뉴 계층으로(사용자 요청)
    bool handMode;

    const float PanelW = 420f, PanelH = 664f;
    Rect panel, titleRect, resumeBtn, volLabel, volSlider, bgmLabel, bgmSlider, sfxLabel, sfxSlider, colorBtn, controlsBtn, restartBtn, cheatBtn, menuBtn, quitBtn;
    // 조작법 화면 레이아웃
    Rect ctrlTitle, ctrlImg, ctrlPageLbl, cPrev, cNext, cClose;

    static readonly Rect Off = new Rect(-9999f, -9999f, 0f, 0f);   // 손글씨 모드에서 숨길 버튼

    void Awake() => xray = GetComponent<LegColorXray>();

    /// <summary>외부(시작화면 Option 표지판 등)에서 옵션 메뉴를 연다.</summary>
    public void OpenMenu() => SetPaused(true);
    public bool IsOpen => paused;

    // 시작 화면에서 SampleScene을 additive 배경으로 띄운 경우, 이 테이블은 비활성(배경) 씬 소속.
    bool IsBackground() => gameObject.scene != SceneManager.GetActiveScene();

    static float Frac(Rect s, Vector2 gm) => Mathf.Clamp01((gm.x - s.x) / s.width);

    // 현재 해금된 조작법 페이지(이미지 + 이름) 목록
    Texture2D[] CtrlPages(out string[] names)
    {
        var texs = new System.Collections.Generic.List<Texture2D>();
        var nms = new System.Collections.Generic.List<string>();
        if (controlsMove != null) { texs.Add(controlsMove); nms.Add("기본 조작"); }
        if (GiraffeMode.Unlocked && controlsGiraffe != null) { texs.Add(controlsGiraffe); nms.Add("기린의 힘"); }
        if (SquidMode.Unlocked && controlsSquid != null) { texs.Add(controlsSquid); nms.Add("우주오징어"); }
        names = nms.ToArray(); return texs.ToArray();
    }

    // ★2026-07-14 인게임도 손그림 흰 종이 박스 + 손글씨 항목(사용자 요청) — 시작 화면과 같은 룩,
    //   다만 게임 화면 위라 중앙 박스+딤 유지. 항목 순서: 계속하기 먼저(복귀가 제일 중요).
    void ComputeLayout()
    {
        if (handMode) { ComputeHandLayout(); return; }

        float x = (Screen.width - PanelW) * 0.5f;
        float y = (Screen.height - PanelH) * 0.5f;
        panel = new Rect(x, y, PanelW, PanelH);

        float ix = x + 34f, iw = PanelW - 68f;
        float cy = y + 28f;
        titleRect   = new Rect(ix, cy, iw, 40f);   cy += 52f;
        resumeBtn   = new Rect(ix, cy, iw, 46f);   cy += 56f;
        volLabel    = new Rect(ix, cy, iw, 26f);   cy += 28f;
        volSlider   = new Rect(ix, cy, iw, 20f);   cy += 34f;
        bgmLabel    = new Rect(ix, cy, iw, 26f);   cy += 28f;
        bgmSlider   = new Rect(ix, cy, iw, 20f);   cy += 34f;
        sfxLabel    = new Rect(ix, cy, iw, 26f);   cy += 28f;
        sfxSlider   = new Rect(ix, cy, iw, 20f);   cy += 34f;
        colorBtn    = new Rect(ix, cy, iw, 46f);   cy += 54f;
        controlsBtn = new Rect(ix, cy, iw, 46f);   cy += 54f;
        restartBtn  = new Rect(ix, cy, iw, 46f);   cy += 54f;
        menuBtn     = new Rect(ix, cy, iw, 46f);   cy += 54f;
        quitBtn     = new Rect(ix, cy, iw, 46f);   cy += 54f;
        cheatBtn    = new Rect(ix, cy, iw, 46f);   // 맨 아래 '숨은' 버튼 — 호버해야만 보임
    }

    /// <summary>★손글씨 모드(시작 화면): 홈 메뉴와 같은 왼쪽 열 — 가운데 창 없음.
    /// Update의 클릭/드래그 판정이 같은 Rect 필드를 쓰므로 위치만 바꾸면 로직 공유.</summary>
    void ComputeHandLayout()
    {
        float lx = Screen.width * 0.09f;
        float cy = Screen.height * 0.34f;   // 타이틀 그래픽 아래(LevelSelect와 같은 기준대)
        float sw = 340f;   // 슬라이더 폭

        panel = Off;
        titleRect  = new Rect(lx, cy, 500f, 44f);  cy += 56f;
        volLabel   = new Rect(lx, cy, sw, 30f);    cy += 32f;
        volSlider  = new Rect(lx, cy, sw, 24f);    cy += 40f;
        bgmLabel   = new Rect(lx, cy, sw, 30f);    cy += 32f;
        bgmSlider  = new Rect(lx, cy, sw, 24f);    cy += 40f;
        sfxLabel   = new Rect(lx, cy, sw, 30f);    cy += 32f;
        sfxSlider  = new Rect(lx, cy, sw, 24f);    cy += 48f;
        colorBtn   = new Rect(lx, cy, 420f, 50f);  cy += 58f;
        controlsBtn = new Rect(lx, cy, 420f, 50f); cy += 58f;
        resumeBtn  = new Rect(lx, cy, 150f, 50f);  cy += 58f;   // = 뒤로(닫기)
        cheatBtn   = new Rect(lx, cy, 420f, 44f);               // 숨은 치트(호버 시만 표시)
        restartBtn = Off; menuBtn = Off; quitBtn = Off;          // 시작 화면에선 의미 없음
    }

    void ComputeControlsLayout()
    {
        float cx = Screen.width * 0.5f;
        float by = Screen.height - 56f;                 // 하단 버튼 행
        cClose = new Rect(cx - 70f, by, 140f, 44f);
        cPrev  = new Rect(cx - 230f, by, 140f, 44f);
        cNext  = new Rect(cx + 90f, by, 140f, 44f);
        ctrlTitle   = new Rect(0f, 16f, Screen.width, 40f);
        ctrlPageLbl = new Rect(cx - 120f, by - 30f, 240f, 24f);

        // 이미지(16:9) 화면에 맞춤
        float top = 62f, bottom = by - 36f;
        float availH = bottom - top, availW = Screen.width - 48f;
        float ar = 16f / 9f;
        float w = availW, h = w / ar;
        if (h > availH) { h = availH; w = h * ar; }
        ctrlImg = new Rect((Screen.width - w) * 0.5f, top + (availH - h) * 0.5f, w, h);
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
        if (LetterOverlay.IsOpen) return;   // 편지 오버레이가 열려 있으면 ESC는 그쪽이 먼저 처리

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (paused && showControls) showControls = false; // 조작법 화면이면 메뉴로 복귀
            else SetPaused(!paused);
        }

        if (!paused) return;

        var mouse = Mouse.current;
        if (mouse == null) return;
        if (Time.frameCount == openedFrame) return; // 연 프레임의 클릭은 무시(즉시 닫힘 방지)
        Vector2 gm = GuiMouse();

        // 조작법 화면: 페이지 넘김 / 닫기만 처리
        if (showControls)
        {
            ComputeControlsLayout();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                string[] nm; var pg = CtrlPages(out nm);
                if (cClose.Contains(gm)) showControls = false;
                else if (pg.Length > 1 && cNext.Contains(gm)) controlsPage = (controlsPage + 1) % pg.Length;
                else if (pg.Length > 1 && cPrev.Contains(gm)) controlsPage = (controlsPage - 1 + pg.Length) % pg.Length;
            }
            return;
        }

        ComputeLayout();

        // 3개 볼륨 슬라이더 드래그 (마스터 / BGM / SFX)
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (volSlider.Contains(gm)) dragSlider = 1;
            else if (bgmSlider.Contains(gm)) dragSlider = 2;
            else if (sfxSlider.Contains(gm)) dragSlider = 3;
        }
        if (!mouse.leftButton.isPressed) dragSlider = 0;
        if (dragSlider == 1) AudioListener.volume = Frac(volSlider, gm);
        else if (dragSlider == 2) { Bgm.Volume = Frac(bgmSlider, gm); Bgm.Apply(); }
        else if (dragSlider == 3) Sfx.Volume = Frac(sfxSlider, gm);

        // 버튼 클릭. 버튼·슬라이더가 아닌 바깥 아무데나 클릭하면 창을 닫는다.
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (resumeBtn.Contains(gm)) SetPaused(false);
            else if (colorBtn.Contains(gm)) { if (xray != null) xray.Toggle(); }
            else if (controlsBtn.Contains(gm)) { showControls = true; controlsPage = 0; }
            else if (restartBtn.Contains(gm))
            {
                Time.timeScale = 1f;
                NetScene.Reload(); // 현재 씬 처음부터(멀티: 호스트만 유효 — 전원 재시작)
            }
            else if (cheatBtn.Contains(gm)) { CheatMode.Active = !CheatMode.Active; }
            else if (menuBtn.Contains(gm))
            {
                Time.timeScale = 1f;
                NetScene.LeaveToLocal(startScene);   // 멀티: 세션에서 나만 빠져나가 메뉴로(호스트면 세션 종료)
            }
            else if (quitBtn.Contains(gm))
            {
                Time.timeScale = 1f;
                Application.Quit();                  // 빌드: 게임 종료
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;   // 에디터: 플레이 정지
#endif
            }
            else if (!volSlider.Contains(gm) && !bgmSlider.Contains(gm) && !sfxSlider.Contains(gm))
                SetPaused(false); // 바깥/패널 빈 곳 클릭 → 닫기
        }
    }

    void SetPaused(bool p)
    {
        paused = p;
        Time.timeScale = p ? 0f : 1f;
        if (p)
        {
            // ★시작 화면(홈 메뉴 존재)이면 손글씨 왼쪽 열 모드
            handMode = Object.FindAnyObjectByType<StartScreenMenu>() != null;
            openedFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 게임으로 복귀: 마우스 시점(CameraFollow)을 위해 다시 잠금(시작 화면은 StartScreenMenu가 매 프레임 해제)
            dragSlider = 0;
            showControls = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (IsBackground() || !paused) return;
        EnsureStyles();
        Vector2 gm = GuiMouse();

        if (showControls) { DrawControlsScreen(gm); return; }

        ComputeLayout();

        DrawHandMenu(gm);
    }

    /// <summary>★손글씨 옵션(두 모드 공통 룩): 시작 화면=왼쪽 열 맨글씨, 인게임=딤+손그림 흰 종이 박스.
    /// 클릭 판정은 Update가 같은 Rect로 처리하므로 여기선 그리기만.</summary>
    void DrawHandMenu(Vector2 gm)
    {
        if (!handMode)
        {
            // 게임 화면 위: 살짝 어둡게 + 손그림 흰 종이 박스
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.40f));
            if (HandUI.BoxPanel != null) HandUI.Paper(panel, HandUI.BoxPanel, 0.97f);
            else UiKit.Panel(panel, 0.97f);
        }

        GUI.Label(titleRect, L.T("설정", "options"), handTitle);

        if (!handMode) HandItem(resumeBtn, L.T("계속하기", "resume"), gm);

        GUI.Label(volLabel, L.T("소리", "master") + "   " + Mathf.RoundToInt(AudioListener.volume * 100f) + "%", handLabel);
        HandSlider(volSlider, AudioListener.volume);
        GUI.Label(bgmLabel, L.T("배경음", "music") + "   " + Mathf.RoundToInt(Bgm.Volume * 100f) + "%", handLabel);
        HandSlider(bgmSlider, Bgm.Volume);
        GUI.Label(sfxLabel, L.T("효과음", "sfx") + "   " + Mathf.RoundToInt(Sfx.Volume * 100f) + "%", handLabel);
        HandSlider(sfxSlider, Sfx.Volume);

        bool on = xray != null && xray.IsOn;
        HandItem(colorBtn, L.T("다리 색 표시: ", "leg colors: ") + (on ? L.T("켜짐", "on") : L.T("꺼짐", "off")), gm);
        HandItem(controlsBtn, L.T("조작법", "controls"), gm);

        if (!handMode)
        {
            HandItem(restartBtn, L.T("다시 시작", "restart"), gm);
            HandItem(menuBtn, L.T("메인 메뉴", "main menu"), gm);
            HandItem(quitBtn, L.T("게임 종료", "quit game"), gm);
        }
        else HandItem(resumeBtn, L.T("뒤로", "back"), gm);

        // 숨은 치트: 맨 아래 빈 자리에 호버해야만 보임(기존과 동일)
        if (cheatBtn.Contains(gm))
            HandItem(cheatBtn, L.T("치트: ", "cheat: ") + (CheatMode.Active ? "ON" : "OFF"), gm);
    }

    /// <summary>손글씨 메뉴 항목: 맨글씨 + 호버 시 살짝 기울임+밑줄(홈/LevelSelect와 동일 룩, 색 변화 없음).</summary>
    void HandItem(Rect r, string text, Vector2 gm, GUIStyle style = null)
    {
        if (style == null) style = handItem;
        bool hover = r.Contains(gm);
        var m = GUI.matrix;
        if (hover) GUIUtility.RotateAroundPivot(-1.2f, new Vector2(r.x, r.center.y));
        GUI.Label(r, text, style);
        if (hover && HandUI.Underline != null)
        {
            float tw2 = Mathf.Min(style.CalcSize(new GUIContent(text)).x, r.width);
            float ux = style.alignment == TextAnchor.MiddleCenter ? r.center.x - tw2 * 0.5f : r.x;
            GUI.DrawTexture(new Rect(ux, r.yMax - 8f, tw2, 14f), HandUI.Underline, ScaleMode.StretchToFill);
        }
        GUI.matrix = m;
    }

    /// <summary>손그림 슬라이더: 연한 밑줄 트랙 + 값만큼 진한 밑줄 채움.</summary>
    void HandSlider(Rect r, float v)
    {
        if (HandUI.Underline == null) { UiKit.Slider(r, v); return; }
        v = Mathf.Clamp01(v);
        float ty = r.y + r.height * 0.5f - 7f;
        var pc = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        GUI.DrawTexture(new Rect(r.x, ty, r.width, 14f), HandUI.Underline, ScaleMode.StretchToFill);
        GUI.color = pc;
        if (v > 0.01f)
        {
            GUI.BeginGroup(new Rect(r.x, r.y, r.width * v, r.height));
            GUI.DrawTexture(new Rect(0f, r.height * 0.5f - 7f, r.width, 14f), HandUI.Underline, ScaleMode.StretchToFill);
            GUI.EndGroup();
        }
    }

    void DrawControlsScreen(Vector2 gm)
    {
        ComputeControlsLayout();
        string[] names; var pages = CtrlPages(out names);
        if (pages.Length == 0) { showControls = false; return; }
        controlsPage = Mathf.Clamp(controlsPage, 0, pages.Length - 1);

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, 0.97f));

        GUI.Label(ctrlTitle, L.T("조작법 — ", "controls — ") + names[controlsPage], handTitleC);
        if (pages[controlsPage] != null)
            GUI.DrawTexture(ctrlImg, pages[controlsPage], ScaleMode.ScaleToFit, true);

        GUI.Label(ctrlPageLbl, (controlsPage + 1) + " / " + pages.Length, handPage);

        if (pages.Length > 1)
        {
            HandItem(cPrev, L.T("◀ 이전", "◀ prev"), gm, handItemC);
            HandItem(cNext, L.T("다음 ▶", "next ▶"), gm, handItemC);
        }
        HandItem(cClose, L.T("닫기", "close"), gm, handItemC);
    }

    void DrawButton(Rect r, string label, Vector2 gm)
    {
        UiKit.Button(r, label, r.Contains(gm));   // 흰 배경+검은 테두리+검은 글씨(hover=옅은 회색)
    }

    void DrawSlider(Rect r, float v) => UiKit.Slider(r, v);

    void Fill(Rect r, Color c) => UiKit.Fill(r, c);

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        tex = Texture2D.whiteTexture;
        // 통일 스타일: 검은 글씨(UiKit과 동일 톤)
        Color ink = new Color(0.08f, 0.08f, 0.08f, 1f);

        titleStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = ink;

        btnStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btnStyle.normal.textColor = ink;

        labelStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        labelStyle.normal.textColor = ink;

        centerStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 17, alignment = TextAnchor.MiddleCenter };
        centerStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        // ★손글씨 스타일(시작 화면 + 인게임 공통) — 홈/LevelSelect와 같은 톤
        handTitle = HandUI.Style(28, HandUI.InkSoft, TextAnchor.MiddleLeft);
        handItem = HandUI.Style(30, HandUI.Ink, TextAnchor.MiddleLeft);
        handLabel = HandUI.Style(22, HandUI.Ink, TextAnchor.MiddleLeft);
        handItemC = HandUI.Style(28, HandUI.Ink, TextAnchor.MiddleCenter);
        handTitleC = HandUI.Style(32, HandUI.Ink, TextAnchor.MiddleCenter);
        handPage = HandUI.Style(20, HandUI.InkSoft, TextAnchor.MiddleCenter);
    }
}
