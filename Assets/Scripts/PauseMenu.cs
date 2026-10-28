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
    [Tooltip("테니스공의 힘 조작 이미지 — TennisMode.Unlocked 일 때만 표시")]
    public Texture2D controlsTennis;
    public Texture2D controlsSnowman;

    LegColorXray xray;
    bool paused;
    public static bool InputBlocked {get;private set;}
    /// <summary>설정의 '다리 다시 고르기'가 열려 있는지 — 폰 화면이 큰 다리 버튼을 띄운다.</summary>
    public static bool LegsScreenOpen {get;private set;}
    bool showControls;      // 조작법 화면 표시 중
    bool showLegs;          // 다리 다시 고르기 화면 표시 중(온라인 게임 중에만)
    bool showMaps;          // 치트: 맵 고르기 화면 표시 중
    readonly NicknameTyping nick = new NicknameTyping();
    LobbyTablePreview legsPreview;   // 다리 다시 고르기 화면 가운데의 지금 테이블
    NetLegLink cachedLink;
    int controlsPage;       // 현재 조작법 페이지
    int dragSlider;         // 드래그 중인 슬라이더: 0 없음 / 1 마스터 / 2 BGM / 3 SFX
    int openedFrame = -1;   // 메뉴를 연 프레임. 그 프레임의 클릭은 '바깥 클릭 닫기'로 처리하지 않음.

    // 시작 화면(StartScreenMenu 존재)인지 — 그때는 계속하기/다시 시작/메인 메뉴/종료가 없다.
    bool handMode;

    // ★2026-10-25 '지우개 대소동' 규칙으로 개편(사용자: "옵션 메뉴 박스는 더 키워도 됨"):
    //   1600×900 캔버스(JourneyMenuUI) 가운데 큰 종이 창 1088×752, 왼쪽 열 = 소리·화면, 오른쪽 열 = 언어·조작법·게임.
    //   클릭/드래그 판정은 Update가 같은 Rect 필드로(캔버스 좌표) 처리하고 OnGUI는 그리기만 한다.
    //   다리 다시 고르기 화면은 가운데에 테이블이 들어가서 더 넓다.
    public static readonly Rect PanelMenu = new Rect(256f, 42f, 1088f, 816f), PanelLegs = new Rect(160f, 42f, 1280f, 816f);
    Rect Panel = PanelMenu;
    Rect legsBtn, legsBack, legsTable, nickLabel, nickField, nickSave, nameTagBtn, mapsBtn, mapsBack, mapsNext;
    readonly Rect[] mapCards = new Rect[5];
    // 치트 맵 고르기: 이야기 순서대로(방·폰 연결은 그대로 두고 씬만 바꾼다)
    static readonly string[] MapScenes = { "Hallway", "Jungle", "IceCavern", "Space", "Moon" };
    static string MapName(int i) => i switch
    {
        0 => L.T("복도", "Hallway", "廊下", "走廊"),
        1 => L.T("정글", "Jungle", "ジャングル", "丛林"),
        2 => L.T("얼음 동굴", "Ice cave", "氷の洞窟", "冰洞"),
        3 => L.T("우주", "Space", "宇宙", "太空"),
        _ => L.T("달", "Moon", "月", "月球"),
    };
    static int CurrentMap()
    {
        string scene = SceneManager.GetActiveScene().name;
        return scene == "Moon_CozyKitchen" ? 4 : System.Array.IndexOf(MapScenes, scene);
    }
    readonly Rect[] legCards = new Rect[4];
    Rect titleRect, resumeBtn, volLabel, volSlider, bgmLabel, bgmSlider, sfxLabel, sfxSlider, colorBtn, controlsBtn, langBtn, displayBtn, restartBtn, cheatBtn, menuBtn, quitBtn;
    // 조작법 화면 레이아웃
    Rect ctrlImg, cPrev, cNext, cClose;

    static readonly Rect Off = new Rect(-9999f, -9999f, 0f, 0f);   // 이 화면에 없는 버튼

    void Awake() => xray = GetComponent<LegColorXray>();

    /// <summary>외부(시작화면 설정 버튼 등)에서 옵션 메뉴를 연다.</summary>
    public void OpenMenu() => SetPaused(true);
    public bool IsOpen => paused;

    NetLegLink Link
    {
        get
        {
            if (cachedLink == null) cachedLink = FindAnyObjectByType<NetLegLink>();
            return cachedLink;
        }
    }
    /// <summary>온라인 게임 중(로비 아님, 1인 1테이블 대전 아님)이면 설정에서 다리를 다시 고를 수 있다.</summary>
    bool LegsAvailable
    {
        get
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return !handMode && nm != null && nm.IsListening && Link != null && Link.IsSpawned && Link.Started && FindAnyObjectByType<PerTableMode>() == null;
        }
    }

    /// <summary>치트가 켜져 있고 씬을 바꿀 권한이 있으면(호스트·혼자 하기) 맵을 골라 바로 이동할 수 있다.</summary>
    bool MapsAvailable => !handMode && CheatMode.Active && CheckpointRuntime.Authority && FindAnyObjectByType<PerTableMode>() == null;

    // 시작 화면에서 SampleScene을 additive 배경으로 띄운 경우, 이 테이블은 비활성(배경) 씬 소속.
    bool IsBackground() => gameObject.scene != SceneManager.GetActiveScene();

    static float Frac(Rect s, Vector2 gm) => Mathf.Clamp01((gm.x - s.x) / s.width);
    static Rect Grab(Rect slider) => new Rect(slider.x - 16f, slider.y - 16f, slider.width + 32f, slider.height + 32f);   // 슬라이더는 위아래로 넉넉히 잡힘

    // 현재 해금된 조작법 페이지(이미지 + 이름) 목록
    Texture2D[] CtrlPages(out string[] names)
    {
        var texs = new System.Collections.Generic.List<Texture2D>();
        var nms = new System.Collections.Generic.List<string>();
        if (controlsMove != null) { texs.Add(controlsMove); nms.Add(L.T("기본 조작", "Basic moves")); }
        if (GiraffeMode.Unlocked && controlsGiraffe != null) { texs.Add(controlsGiraffe); nms.Add(L.T("기린의 힘", "Giraffe power")); }
        if (TennisMode.Unlocked && controlsTennis != null) { texs.Add(controlsTennis); nms.Add(L.T("테니스공의 힘", "Tennis power")); }
        if(controlsSnowman==null)controlsSnowman=ControlGuideAssets.Load("ControlsSnowman");
        var ice=GetComponent<IcePower>();
        if(ice!=null && ice.unlocked && controlsSnowman!=null){texs.Add(controlsSnowman);nms.Add(L.T("눈사람의 힘","Snowman power"));}
        if (SquidMode.Unlocked && controlsSquid != null) { texs.Add(controlsSquid); nms.Add(L.T("우주오징어", "Space squid")); }
        names = nms.ToArray(); return texs.ToArray();
    }

    void ComputeLayout()
    {
        const float S1 = JourneyMenuUI.S1, S2 = JourneyMenuUI.S2, S4 = JourneyMenuUI.S4, S5 = JourneyMenuUI.S5, HS = JourneyMenuUI.HS, HM = JourneyMenuUI.HM;
        bool legs = LegsAvailable;
        Panel = PanelMenu;
        float x = Panel.x + S5, w = (Panel.width - 3 * S5) * .5f, rx = x + w + S5;
        titleRect = new Rect(x, Panel.y + S4, Panel.width - 2 * S5, 52f);
        float top = Panel.y + 120f;   // 제목 아래
        // 왼쪽 열: 소리 3개 + 화면 2개
        float cy = top;
        volLabel = new Rect(x, cy, w, 40f); volSlider = new Rect(x + S1, cy + 48f, w - 2 * S1, 32f); cy += 88f;
        bgmLabel = new Rect(x, cy, w, 40f); bgmSlider = new Rect(x + S1, cy + 48f, w - 2 * S1, 32f); cy += 88f;
        sfxLabel = new Rect(x, cy, w, 40f); sfxSlider = new Rect(x + S1, cy + 48f, w - 2 * S1, 32f); cy += 88f + S2;
        colorBtn = new Rect(x, cy, w, HS); cy += HS + S2;
        nameTagBtn = new Rect(x, cy, w, HS); cy += HS + S2;
        displayBtn = new Rect(x, cy, w, HS);
        // 오른쪽 열: 언어 · 조작법 · (인게임) 다시 시작 · 메인 메뉴 · 게임 종료
        cy = top;
        langBtn = new Rect(rx, cy, w, HS); cy += HS + S2;
        controlsBtn = new Rect(rx, cy, w, HS); cy += HS + S2;
        if (legs) { legsBtn = new Rect(rx, cy, w, HS); cy += HS + S2; } else legsBtn = Off;
        cy += legs ? 0f : S4;
        if (handMode) { restartBtn = Off; menuBtn = Off; quitBtn = Off; }
        else
        {
            restartBtn = new Rect(rx, cy, w, HS); cy += HS + S2;
            menuBtn = new Rect(rx, cy, w, HS); cy += HS + S2;
            quitBtn = new Rect(rx, cy, w, HS);
        }
        // 아래 줄: 시작 화면=왼쪽 '뒤로' / 인게임=오른쪽 '계속하기'(주요)
        float by = Panel.yMax - S5 - HM;
        resumeBtn = handMode ? new Rect(x, by, 320f, HM) : new Rect(Panel.xMax - S5 - 400f, by, 400f, HM);
        cheatBtn = handMode ? new Rect(x + 320f + S4, by, 320f, HM) : new Rect(x, by, 320f, HM);   // 숨은 치트(호버해야만 보임)
        mapsBtn = MapsAvailable ? new Rect(cheatBtn.xMax + S4, by, resumeBtn.x - S4 - (cheatBtn.xMax + S4), HM) : Off;
    }

    void ComputeControlsLayout()
    {
        const float S4 = JourneyMenuUI.S4, HM = JourneyMenuUI.HM;
        float by = 900f - S4 - HM;
        cClose = new Rect(80f, by, 280f, HM);
        cPrev = new Rect(1600f - 80f - 88f - JourneyMenuUI.S2 - 88f, by, 88f, HM);
        cNext = new Rect(1600f - 80f - 88f, by, 88f, HM);
        // 이미지(16:9)를 제목 아래~버튼 위에 맞춤
        float top = 144f, bottom = by - S4, availW = 1440f, availH = bottom - top;
        float h = availH, wdt = h * 16f / 9f; if (wdt > availW) { wdt = availW; h = wdt * 9f / 16f; }
        ctrlImg = new Rect((1600f - wdt) * .5f, top + (availH - h) * .5f, wdt, h);
    }

    /// <summary>다리 다시 고르기 화면: 위 줄 = 앞다리 두 개, 아래 줄 = 뒷다리 두 개(테이블을 위에서 본 배치) + 닉네임 칸.</summary>
    void ComputeLegsLayout()
    {
        const float S1 = JourneyMenuUI.S1, S2 = JourneyMenuUI.S2, S4 = JourneyMenuUI.S4, S5 = JourneyMenuUI.S5, HS = JourneyMenuUI.HS, HM = JourneyMenuUI.HM;
        Panel = PanelLegs;
        float x = Panel.x + S5, inner = Panel.width - 2 * S5, w = 336f, rx = x + inner - w;
        titleRect = new Rect(x, Panel.y + S4, inner, 52f);
        float top = Panel.y + 120f, h = 184f;
        for (int i = 0; i < 4; i++) legCards[i] = new Rect(i % 2 == 0 ? x : rx, i >= 2 ? top : top + h + S2, w, h);
        // 가운데: 지금 테이블(미리보기 그림 비율 그대로)
        float tw = rx - S4 - (x + w + S4), th = tw / (legsPreview != null ? legsPreview.Aspect : 1.41f), cy = top + h + S2 * .5f;
        legsTable = new Rect(x + w + S4, cy - th * .5f, tw, th);
        float ny = top + 2 * h + S2 + S4;
        nickLabel = new Rect(x, ny, 160f, HS);
        nickField = new Rect(x + 160f + S2, ny, inner - 160f - 240f - 2 * S2, HS);
        nickSave = new Rect(nickField.xMax + S2, ny, 240f, HS);
        legsBack = new Rect(x, Panel.yMax - S5 - HM, 320f, HM);
    }

    /// <summary>맵 고르기 화면: 맵 다섯 개(3 + 2) + 아래 줄 뒤로 / 다음 맵으로.</summary>
    void ComputeMapsLayout()
    {
        const float S2 = JourneyMenuUI.S2, S4 = JourneyMenuUI.S4, S5 = JourneyMenuUI.S5, HM = JourneyMenuUI.HM;
        Panel = PanelMenu;
        float x = Panel.x + S5, inner = Panel.width - 2 * S5, w = (inner - 2 * S4) / 3f, h = 176f, top = Panel.y + 120f;
        titleRect = new Rect(x, Panel.y + S4, inner, 52f);
        for (int i = 0; i < 5; i++) mapCards[i] = new Rect(x + (i % 3) * (w + S4), top + (i / 3) * (h + S2 + S4), w, h);
        float by = Panel.yMax - S5 - HM;
        mapsBack = new Rect(x, by, 320f, HM);
        mapsNext = new Rect(Panel.xMax - S5 - 400f, by, 400f, HM);
    }
    void LoadMap(int i)
    {
        if (i < 0 || i >= MapScenes.Length || !CheckpointRuntime.Authority) return;
        SetPaused(false);
        Progress.SkipNextClear = true;   // 치트 이동은 클리어로 남기지 않음
        NetScene.Load(MapScenes[i]);      // 온라인 = 호스트가 전원을 함께 옮김(방·폰 연결 유지)
    }

    void OpenLegs()
    {
        showLegs = true; LegsScreenOpen = true;
        nick.Begin(NetLegLink.LocalNickname);
        // 열 때마다 지금 모습(다리 길이·각도)을 다시 복사
        legsPreview ??= new LobbyTablePreview(-12000f);
        legsPreview.Dispose();
        if (Link != null)
        {
            legsPreview.Initialize(Link.transform, Board(Link.transform), true);
            legsPreview.SetBackground(JourneyMenuUI.PaperColor);
            legsPreview.SetVisible(true);
        }
    }
    void CloseLegs()
    {
        showLegs = false; LegsScreenOpen = false;
        nick.End();
        legsPreview?.Dispose();
    }
    static Transform Board(Transform table)
    {
        foreach (var ab in table.GetComponentsInChildren<ArticulationBody>())
            if (ab.isRoot) return ab.transform;
        return table;
    }
    bool NickDirty
    {
        get
        {
            string v = NetLegLink.CleanPlayerName(nick.Shown);
            return v.Length > 0 && v != NetLegLink.LocalNickname;
        }
    }
    void SaveNick()
    {
        if (!NickDirty || Link == null) return;
        Link.SetMyName(nick.Shown);
        nick.Begin(NetLegLink.LocalNickname);   // 다듬어진 이름으로 칸을 다시 채움
    }

    // 마우스 = JourneyMenuUI 캔버스 좌표(그리기와 같은 좌표계)
    static Vector2 GuiMouse() => JourneyMenuUI.MousePosition;

    void Update()
    {
        if (IsBackground()) return;
        if (LetterOverlay.IsOpen) return;   // 편지 오버레이가 열려 있으면 ESC는 그쪽이 먼저 처리

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            // ESC has one owner: child menu -> lobby -> pause menu.
            if (!paused)
            {
                var select = FindAnyObjectByType<LevelSelect>();
                if (select != null && select.Showing) { select.Back(); return; }
                var lobby = FindAnyObjectByType<NetDebugPanel>();
                if (lobby != null && lobby.HandleBack()) return;
            }
            if (paused && showControls) showControls = false; // 조작법 화면이면 메뉴로 복귀
            else if (paused && showLegs) CloseLegs();         // 다리 고르기 화면이면 메뉴로 복귀
            else if (paused && showMaps) showMaps = false;    // 맵 고르기 화면이면 메뉴로 복귀
            else SetPaused(!paused);
            return;
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

        if (showMaps)
        {
            if (!MapsAvailable) { showMaps = false; return; }
            ComputeMapsLayout();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                int here = CurrentMap();
                if (mapsBack.Contains(gm)) showMaps = false;
                else if (mapsNext.Contains(gm)) { if (here >= 0 && here < MapScenes.Length - 1) LoadMap(here + 1); }
                else
                {
                    bool hit = false;
                    for (int i = 0; i < mapCards.Length; i++) if (mapCards[i].Contains(gm)) { hit = true; LoadMap(i); break; }
                    if (!hit && !Panel.Contains(gm)) SetPaused(false);
                }
            }
            return;
        }

        if (showLegs)
        {
            if (!LegsAvailable) { CloseLegs(); return; }   // 게임이 끝났거나 세션이 사라짐
            ComputeLegsLayout();
            if (kb != null && kb.backspaceKey.wasPressedThisFrame) nick.Backspace();
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) SaveNick();
            float sc = JourneyMenuUI.Scale;
            nick.SetCursor(JourneyMenuUI.Origin + new Vector2(nickField.x + JourneyMenuUI.S3, nickField.yMax) * sc);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (legsBack.Contains(gm)) CloseLegs();
                else if (nickSave.Contains(gm)) SaveNick();
                else
                {
                    bool hit = false;
                    for (int i = 0; i < 4; i++)
                    {
                        bool onLeg = legsPreview != null && legsPreview.Ready && legsPreview.LegBounds(i, legsTable).Contains(gm);
                        if (!legCards[i].Contains(gm) && !onLeg) continue;
                        hit = true;
                        if (Link.IsMine(i) || Link.OwnerOf(i) == -2) Link.ClaimLegRpc((byte)i);
                    }
                    if (!hit && !Panel.Contains(gm)) SetPaused(false);
                }
            }
            return;
        }

        ComputeLayout();

        // 3개 볼륨 슬라이더 드래그 (마스터 / BGM / SFX)
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (Grab(volSlider).Contains(gm)) dragSlider = 1;
            else if (Grab(bgmSlider).Contains(gm)) dragSlider = 2;
            else if (Grab(sfxSlider).Contains(gm)) dragSlider = 3;
        }
        if (!mouse.leftButton.isPressed) dragSlider = 0;
        if (dragSlider == 1) AudioListener.volume = Frac(volSlider, gm);
        else if (dragSlider == 2) { Bgm.Volume = Frac(bgmSlider, gm); Bgm.Apply(); }
        else if (dragSlider == 3) Sfx.Volume = Frac(sfxSlider, gm);

        // 버튼 클릭. 창 바깥을 클릭하면 닫는다(창 안 빈 곳은 그대로).
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (resumeBtn.Contains(gm)) SetPaused(false);
            else if (colorBtn.Contains(gm)) { if (xray != null) xray.Toggle(); }
            else if (nameTagBtn.Contains(gm)) LegNameTags.Visible = !LegNameTags.Visible;
            else if (controlsBtn.Contains(gm)) { showControls = true; controlsPage = 0; }
            else if (legsBtn.Contains(gm)) OpenLegs();
            else if (mapsBtn.Contains(gm)) showMaps = true;
            else if (langBtn.Contains(gm)) { LanguageSelector.HandleClick(langBtn, gm); }
            else if (displayBtn.Contains(gm)) { DisplayMode.Toggle(); }
            else if (restartBtn.Contains(gm))
            {
                if(!CheckpointRuntime.Authority)return;
                SetPaused(false);
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
            else if (!Panel.Contains(gm)) SetPaused(false); // 창 바깥 클릭 → 닫기
        }
    }

    void SetPaused(bool p)
    {
        paused = p;
        InputBlocked=p;
        var nm=Unity.Netcode.NetworkManager.Singleton;
        if(nm==null||!nm.IsListening)Time.timeScale=p?0f:1f;
        if (p)
        {
            // 시작 화면(홈 메뉴 존재)이면 '뒤로'만, 인게임이면 계속하기·다시 시작·메인 메뉴·종료까지
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
            showMaps = false;
            CloseLegs();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (IsBackground() || !paused) return;
        Vector2 gm = GuiMouse();
        // 바탕: 시작 화면=종이색, 인게임=게임 화면 위 살짝 어둡게(미끄러지지 않음)
        JourneyMenuUI.Begin();
        if (handMode) JourneyMenuUI.Backdrop(.97f); else JourneyMenuUI.Fill(new Rect(-400, -400, 2400, 1700), new Color(0f, 0f, 0f, .38f));
        JourneyMenuUI.End();
        JourneyMenuUI.BeginModal("pause", showControls ? "controls" : showLegs ? "legs" : showMaps ? "maps" : "menu");
        try
        {
            if (showControls) { DrawControlsScreen(gm); return; }
            if (showLegs) { ComputeLegsLayout(); DrawLegsScreen(); return; }
            if (showMaps) { ComputeMapsLayout(); DrawMapsScreen(); return; }
            ComputeLayout(); DrawMenu(gm);
        }
        finally { JourneyMenuUI.End(); }
    }

    void DrawMenu(Vector2 gm)
    {
        JourneyMenuUI.Box(Panel, JourneyMenuUI.Kind.Sheet);
        JourneyMenuUI.Text(titleRect, L.T("설정", "Options", "設定", "设置"), JourneyMenuUI.TL);

        SliderRow(volLabel, volSlider, L.T("소리", "Master", "音量", "总音量"), AudioListener.volume);
        SliderRow(bgmLabel, bgmSlider, L.T("배경음", "Music", "BGM", "音乐"), Bgm.Volume);
        SliderRow(sfxLabel, sfxSlider, L.T("효과음", "Effects", "効果音", "音效"), Sfx.Volume);

        bool on = xray != null && xray.IsOn;
        ToggleRow(colorBtn, L.T("다리 색 표시", "Leg colors", "脚の色表示", "桌腿颜色"), on);
        ToggleRow(nameTagBtn, L.T("닉네임 표시", "Name tags", "ニックネーム表示", "显示昵称"), LegNameTags.Visible);
        JourneyMenuUI.ButtonVisual(displayBtn, DisplayMode.Label());
        LanguageSelector.Draw(langBtn, gm);
        JourneyMenuUI.ButtonVisual(controlsBtn, L.T("조작법 보기", "Controls", "操作方法", "操作说明"));
        if (legsBtn.width > 0f) JourneyMenuUI.ButtonVisual(legsBtn, L.T("다리 다시 고르기", "Change legs", "脚を選び直す", "重新选择桌腿"));
        if (mapsBtn.width > 0f) JourneyMenuUI.ButtonVisual(mapsBtn, L.T("맵 고르기", "Maps", "マップ", "地图"));
        if (!handMode)
        {
            JourneyMenuUI.ButtonVisual(restartBtn, L.T("이 지점부터 다시 시작", "Restart from checkpoint", "地点からやり直す", "从检查点重来"), CheckpointRuntime.Authority);
            JourneyMenuUI.ButtonVisual(menuBtn, L.T("메인 메뉴", "Main menu", "メインメニュー", "主菜单"));
            JourneyMenuUI.ButtonVisual(quitBtn, L.T("게임 종료", "Quit game", "ゲーム終了", "退出游戏"));
            JourneyMenuUI.ButtonVisual(resumeBtn, L.T("계속하기", "Resume", "再開", "继续"), true, true);
        }
        else BackVisual(resumeBtn);

        // 숨은 치트: 빈 자리에 호버해야만 보임(기존과 동일)
        if (cheatBtn.Contains(gm))
            JourneyMenuUI.Text(cheatBtn, L.T("치트: ", "Cheat: ", "チート: ", "作弊: ") + (CheatMode.Active ? "ON" : "OFF"), JourneyMenuUI.TXS, JourneyMenuUI.Muted, TextAnchor.MiddleCenter);
    }

    void DrawMapsScreen()
    {
        const float S2 = JourneyMenuUI.S2, S3 = JourneyMenuUI.S3;
        JourneyMenuUI.Box(Panel, JourneyMenuUI.Kind.Sheet);
        JourneyMenuUI.Text(titleRect, L.T("맵 고르기", "Choose a map", "マップを選ぶ", "选择地图"), JourneyMenuUI.TL);
        int here = CurrentMap();
        for (int i = 0; i < mapCards.Length; i++)
        {
            var r = mapCards[i]; bool now = i == here;
            JourneyMenuUI.BeginCard(r, true, now);
            JourneyMenuUI.Literal(new Rect(r.x + S3, r.y + S2, 80f, 40f), (i + 1).ToString("00"), JourneyMenuUI.TXS, JourneyMenuUI.Muted);
            if (now)
            {
                string tag = L.T("지금", "Now", "いま", "当前");
                float bw = JourneyMenuUI.TextWidth(tag, JourneyMenuUI.TXS) + 2 * S2;
                var badge = new Rect(r.xMax - S3 - bw, r.y + S2, bw, 40f);
                JourneyMenuUI.Box(badge, JourneyMenuUI.Kind.Primary);
                JourneyMenuUI.Literal(badge, tag, JourneyMenuUI.TXS, JourneyMenuUI.White, TextAnchor.MiddleCenter);
            }
            JourneyMenuUI.Text(new Rect(r.x + S3, r.y + 56f, r.width - 2 * S3, r.height - 72f), MapName(i), JourneyMenuUI.TL, JourneyMenuUI.Ink, TextAnchor.MiddleCenter);
            JourneyMenuUI.EndCard();
        }
        bool hasNext = here >= 0 && here < MapScenes.Length - 1;
        JourneyMenuUI.ButtonVisual(mapsNext, L.T("다음 맵으로", "Next map", "次のマップへ", "下一张地图"), hasNext, true);
        BackVisual(mapsBack);
    }

    void DrawLegsScreen()
    {
        JourneyMenuUI.Box(Panel, JourneyMenuUI.Kind.Sheet);
        JourneyMenuUI.Text(titleRect, L.T("다리 다시 고르기", "Change legs", "脚を選び直す", "重新选择桌腿"), JourneyMenuUI.TL);
        var link = Link;
        if (link == null) { BackVisual(legsBack); return; }
        int hover = -1; var mouse = JourneyMenuUI.MousePosition;
        bool ready = legsPreview != null && legsPreview.Ready;
        for (int i = 0; i < 4; i++) if (legCards[i].Contains(mouse) || (ready && legsPreview.LegBounds(i, legsTable).Contains(mouse))) hover = i;
        if (ready) legsPreview.Draw(legsTable, link, hover);
        for (int i = 0; i < 4; i++)
        {
            bool mine = link.IsMine(i), can = mine || link.OwnerOf(i) == -2;
            if (ready)
            {
                // 카드 → 그림 속 다리: 회색 선(내 다리 = 잉크)
                var r = legCards[i]; var target = legsPreview.LegPoint(i, legsTable);
                var from = new Vector2(i % 2 == 0 ? r.xMax : r.x, r.center.y);
                var bend = new Vector2(i % 2 == 0 ? from.x + JourneyMenuUI.S3 : from.x - JourneyMenuUI.S3, from.y);
                Color line = mine ? JourneyMenuUI.Ink : JourneyMenuUI.LineSoft;
                JourneyMenuUI.Line(from, bend, line, 2); JourneyMenuUI.Line(bend, target, line, 2);
            }
            JourneyMenuUI.BeginCard(legCards[i], can, mine);
            NetDebugPanel.DrawLegCard(legCards[i], link, i);
            JourneyMenuUI.EndCard();
        }
        JourneyMenuUI.Text(nickLabel, L.T("닉네임", "Nickname", "ニックネーム", "昵称"), JourneyMenuUI.TS);
        JourneyMenuUI.Field(nickField, nick.Shown, L.T("닉네임", "Nickname", "ニックネーム", "昵称"));
        bool dirty = NickDirty, saved = !dirty && NetLegLink.CleanPlayerName(nick.Shown).Length > 0;
        JourneyMenuUI.ButtonVisual(nickSave, saved ? L.T("저장됨", "Saved", "保存済み", "已保存") : L.T("저장", "Save", "保存", "保存"), dirty, dirty);
        BackVisual(legsBack);
    }

    static void SliderRow(Rect label, Rect slider, string name, float value)
    {
        JourneyMenuUI.Text(label, name, JourneyMenuUI.TS);
        JourneyMenuUI.Text(label, Mathf.RoundToInt(value * 100f) + "%", JourneyMenuUI.TXS, JourneyMenuUI.Muted, TextAnchor.MiddleRight);
        JourneyMenuUI.Slider(slider, value);
    }

    /// <summary>켜짐/꺼짐 줄: 보통 버튼 + 오른쪽 작은 스위치(켜짐=검정 채움).</summary>
    static void ToggleRow(Rect r, string name, bool on)
    {
        bool hover = JourneyMenuUI.Hover(r); float lift = JourneyMenuUI.Lift(r, hover);
        JourneyMenuUI.Box(r, JourneyMenuUI.Kind.Card, false, lift);
        var c = new Rect(r.x, r.y - lift, r.width, r.height);
        JourneyMenuUI.Text(new Rect(c.x + JourneyMenuUI.S3, c.y, c.width - 160f, c.height), name, JourneyMenuUI.TS);
        var sw = new Rect(c.xMax - JourneyMenuUI.S3 - 88f, c.center.y - 20f, 88f, 40f);
        JourneyMenuUI.RoundFill(sw, on ? JourneyMenuUI.Ink : JourneyMenuUI.LineSoft, 20f);
        JourneyMenuUI.RoundFill(new Rect(on ? sw.xMax - 36f : sw.x + 4f, sw.y + 4f, 32f, 32f), JourneyMenuUI.White, 16f);
        JourneyMenuUI.Text(new Rect(sw.x - 120f, c.y, 112f, c.height), on ? L.T("켜짐", "On", "オン", "开") : L.T("꺼짐", "Off", "オフ", "关"), JourneyMenuUI.TXS, JourneyMenuUI.Muted, TextAnchor.MiddleRight);
    }

    static void BackVisual(Rect r)
    {
        bool hover = JourneyMenuUI.Hover(r); float lift = JourneyMenuUI.Lift(r, hover);
        JourneyMenuUI.Box(r, JourneyMenuUI.Kind.Card, false, lift);
        var lr = new Rect(r.x, r.y - lift, r.width, r.height); JourneyMenuUI.BackArrow(lr, JourneyMenuUI.Ink);
        JourneyMenuUI.Text(new Rect(lr.x + 76f, lr.y, lr.width - 100f, lr.height), JourneyMenuUI.Back, JourneyMenuUI.TM, null, TextAnchor.MiddleCenter);
    }

    void DrawControlsScreen(Vector2 gm)
    {
        ComputeControlsLayout();
        string[] names; var pages = CtrlPages(out names);
        if (pages.Length == 0) { showControls = false; return; }
        controlsPage = Mathf.Clamp(controlsPage, 0, pages.Length - 1);

        JourneyMenuUI.Heading(names[controlsPage]);
        var frame = new Rect(ctrlImg.x - JourneyMenuUI.S2, ctrlImg.y - JourneyMenuUI.S2, ctrlImg.width + 2 * JourneyMenuUI.S2, ctrlImg.height + 2 * JourneyMenuUI.S2);
        JourneyMenuUI.Box(frame, JourneyMenuUI.Kind.Card);
        if (pages[controlsPage] != null) LocalizedControlGuide.Draw(ctrlImg, pages[controlsPage]);

        if (pages.Length > 1)
        {
            JourneyMenuUI.ArrowVisual(cPrev, -1);
            JourneyMenuUI.ArrowVisual(cNext, 1);
            JourneyMenuUI.Literal(new Rect(cPrev.x - 200f, cPrev.y, 184f, cPrev.height), (controlsPage + 1) + " / " + pages.Length, JourneyMenuUI.TXS, JourneyMenuUI.Muted, TextAnchor.MiddleRight);
        }
        bool hover = JourneyMenuUI.Hover(cClose); float lift = JourneyMenuUI.Lift(cClose, hover);
        JourneyMenuUI.Box(cClose, JourneyMenuUI.Kind.Card, false, lift);
        var lr = new Rect(cClose.x, cClose.y - lift, cClose.width, cClose.height); JourneyMenuUI.BackArrow(lr, JourneyMenuUI.Ink);
        JourneyMenuUI.Text(new Rect(lr.x + 76f, lr.y, lr.width - 100f, lr.height), L.T("닫기", "Close", "閉じる", "关闭"), JourneyMenuUI.TM, null, TextAnchor.MiddleCenter);
    }

    void OnDestroy(){nick.End();legsPreview?.Dispose();if(paused){InputBlocked=false;if(Time.timeScale==0)Time.timeScale=1;}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetInputBlock(){InputBlocked=false;LegsScreenOpen=false;}

}
