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

    LegColorXray xray;
    bool paused;
    bool showControls;      // 조작법 화면 표시 중
    int controlsPage;       // 현재 조작법 페이지
    int dragSlider;         // 드래그 중인 슬라이더: 0 없음 / 1 마스터 / 2 BGM / 3 SFX
    int openedFrame = -1;   // 메뉴를 연 프레임. 그 프레임의 클릭은 '바깥 클릭 닫기'로 처리하지 않음.

    GUIStyle titleStyle, btnStyle, labelStyle, centerStyle;
    Texture2D tex;

    const float PanelW = 380f, PanelH = 776f;
    Rect panel, titleRect, resumeBtn, volLabel, volSlider, bgmLabel, bgmSlider, sfxLabel, sfxSlider, colorBtn, controlsBtn, lettersBtn, restartBtn, cheatBtn, menuBtn, quitBtn;
    bool showLetters; int letterPage;   // 편지 열람 화면(조작법 화면과 같은 패턴)
    // 조작법 화면 레이아웃
    Rect ctrlTitle, ctrlImg, ctrlPageLbl, cPrev, cNext, cClose;

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
        bool g = GiraffeMode.Unlocked && controlsGiraffe != null;
        bool m = controlsMove != null;
        if (m && g) { names = new[] { "기본 조작", "기린의 힘" }; return new[] { controlsMove, controlsGiraffe }; }
        if (m)      { names = new[] { "기본 조작" };             return new[] { controlsMove }; }
        if (g)      { names = new[] { "기린의 힘" };             return new[] { controlsGiraffe }; }
        names = new string[0]; return new Texture2D[0];
    }

    void ComputeLayout()
    {
        float x = (Screen.width - PanelW) * 0.5f;
        float y = (Screen.height - PanelH) * 0.5f;
        panel = new Rect(x, y, PanelW, PanelH);

        float ix = x + 26f, iw = PanelW - 52f;
        float cy = y + 24f;
        titleRect   = new Rect(ix, cy, iw, 40f);   cy += 40f + 14f;
        resumeBtn   = new Rect(ix, cy, iw, 46f);   cy += 46f + 14f;
        volLabel    = new Rect(ix, cy, iw, 20f);   cy += 20f + 3f;
        volSlider   = new Rect(ix, cy, iw, 22f);   cy += 22f + 10f;
        bgmLabel    = new Rect(ix, cy, iw, 20f);   cy += 20f + 3f;
        bgmSlider   = new Rect(ix, cy, iw, 22f);   cy += 22f + 10f;
        sfxLabel    = new Rect(ix, cy, iw, 20f);   cy += 20f + 3f;
        sfxSlider   = new Rect(ix, cy, iw, 22f);   cy += 22f + 14f;
        colorBtn    = new Rect(ix, cy, iw, 46f);   cy += 46f + 12f;
        controlsBtn = new Rect(ix, cy, iw, 46f);   cy += 46f + 12f;
        lettersBtn  = new Rect(ix, cy, iw, 46f);   cy += 46f + 12f;
        restartBtn  = new Rect(ix, cy, iw, 46f);   cy += 46f + 12f;
        menuBtn     = new Rect(ix, cy, iw, 46f);   cy += 46f + 12f;
        quitBtn     = new Rect(ix, cy, iw, 46f);   cy += 46f + 12f;
        cheatBtn    = new Rect(ix, cy, iw, 46f);   // 맨 아래 '숨은' 버튼 — 호버해야만 보임
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
            else if (paused && showLetters) showLetters = false; // 편지 화면이면 메뉴로 복귀
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

        // 편지 화면: 페이지 넘김 / 닫기만 처리(조작법 화면과 동일 레이아웃 재사용)
        if (showLetters)
        {
            ComputeControlsLayout();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                string[] nm; var pg = LetterPages(out nm);
                if (cClose.Contains(gm)) showLetters = false;
                else if (pg.Length > 1 && cNext.Contains(gm)) letterPage = (letterPage + 1) % pg.Length;
                else if (pg.Length > 1 && cPrev.Contains(gm)) letterPage = (letterPage - 1 + pg.Length) % pg.Length;
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
            else if (lettersBtn.Contains(gm)) { showLetters = true; letterPage = 0; }
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
            openedFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 게임으로 복귀: 마우스 시점(CameraFollow)을 위해 다시 잠금
            dragSlider = 0;
            showControls = false;
            showLetters = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnGUI()
    {
        if (IsBackground() || !paused) return;
        EnsureStyles();
        Vector2 gm = GuiMouse();

        if (showControls) { DrawControlsScreen(gm); return; }
        if (showLetters) { DrawLettersScreen(gm); return; }

        ComputeLayout();

        // 화면 살짝 어둡게(흰 패널이 도드라지게)
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.40f));
        // 패널: 흰 배경 + 얇은 검은 테두리
        UiKit.Panel(panel, 0.97f);

        GUI.Label(titleRect, "PAUSED", titleStyle);

        DrawButton(resumeBtn, "Resume", gm);

        GUI.Label(volLabel, "Master   " + Mathf.RoundToInt(AudioListener.volume * 100f) + "%", labelStyle);
        DrawSlider(volSlider, AudioListener.volume);
        GUI.Label(bgmLabel, "BGM   " + Mathf.RoundToInt(Bgm.Volume * 100f) + "%", labelStyle);
        DrawSlider(bgmSlider, Bgm.Volume);
        GUI.Label(sfxLabel, "SFX (효과음)   " + Mathf.RoundToInt(Sfx.Volume * 100f) + "%", labelStyle);
        DrawSlider(sfxSlider, Sfx.Volume);

        bool on = xray != null && xray.IsOn;
        DrawButton(colorBtn, "Color: " + (on ? "ON" : "OFF"), gm);

        DrawButton(controlsBtn, "Controls (조작법)", gm);

        int found = 0;
        for (int i = 0; i < LetterSystem.Ids.Length; i++) if (LetterSystem.IsCollected(LetterSystem.Ids[i])) found++;
        DrawButton(lettersBtn, "Letters (편지)  " + found + "/" + LetterSystem.Ids.Length, gm);

        DrawButton(restartBtn, "Restart (다시 시작)", gm);

        DrawButton(menuBtn, "Main Menu", gm);

        DrawButton(quitBtn, "Quit (게임 종료)", gm);

        // 치트: 평소엔 안 보임 — 메뉴 맨 아래 빈 자리에 마우스를 올려야만 나타나는 숨은 버튼
        if (cheatBtn.Contains(gm))
            DrawButton(cheatBtn, "Cheat (치트): " + (CheatMode.Active ? "ON" : "OFF"), gm);
    }

    void DrawControlsScreen(Vector2 gm)
    {
        ComputeControlsLayout();
        string[] names; var pages = CtrlPages(out names);
        if (pages.Length == 0) { showControls = false; return; }
        controlsPage = Mathf.Clamp(controlsPage, 0, pages.Length - 1);

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, 0.97f));

        GUI.Label(ctrlTitle, "조작법 — " + names[controlsPage], titleStyle);
        if (pages[controlsPage] != null)
            GUI.DrawTexture(ctrlImg, pages[controlsPage], ScaleMode.ScaleToFit, true);

        GUI.Label(ctrlPageLbl, (controlsPage + 1) + " / " + pages.Length, centerStyle);

        if (pages.Length > 1)
        {
            DrawButton(cPrev, "◀ 이전", gm);
            DrawButton(cNext, "다음 ▶", gm);
        }
        DrawButton(cClose, "닫기", gm);
    }

    // 수집한 편지 페이지(텍스처+제목) 목록
    Texture2D[] LetterPages(out string[] names)
    {
        var texs = new System.Collections.Generic.List<Texture2D>();
        var nms = new System.Collections.Generic.List<string>();
        for (int i = 0; i < LetterSystem.Ids.Length; i++)
            if (LetterSystem.IsCollected(LetterSystem.Ids[i]))
            {
                texs.Add(LetterSystem.LoadImage(LetterSystem.Ids[i]));
                nms.Add(LetterSystem.Titles[i]);
            }
        names = nms.ToArray();
        return texs.ToArray();
    }

    void DrawLettersScreen(Vector2 gm)
    {
        ComputeControlsLayout();
        string[] names; var pages = LetterPages(out names);

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, 0.97f));

        if (pages.Length == 0)
        {
            GUI.Label(ctrlTitle, "편지", titleStyle);
            GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 40f), "아직 찾은 편지가 없어요 — 맵 곳곳에 떨어져 있어요!", centerStyle);
            DrawButton(cClose, "닫기", gm);
            return;
        }
        letterPage = Mathf.Clamp(letterPage, 0, pages.Length - 1);

        GUI.Label(ctrlTitle, names[letterPage], titleStyle);
        if (pages[letterPage] != null)
            GUI.DrawTexture(ctrlImg, pages[letterPage], ScaleMode.ScaleToFit, true);
        else
            GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 40f), "(이미지 없음 — Resources/Letters 에 텍스처 필요)", centerStyle);

        GUI.Label(ctrlPageLbl, (letterPage + 1) + " / " + pages.Length, centerStyle);
        if (pages.Length > 1)
        {
            DrawButton(cPrev, "◀ 이전", gm);
            DrawButton(cNext, "다음 ▶", gm);
        }
        DrawButton(cClose, "닫기", gm);
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
    }
}
