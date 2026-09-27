using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shared thought-bubble tutorials. Hallway walking tracks every direction for one leg at a time.
/// Newly granted powers require fresh input, the real effect, then key release; completed lessons
/// never return when switching modes. Acquisitions, cutscenes and pause suspend practice.
/// Multiplayer teaches only owned legs, while +/- controls all giraffe legs together.
/// Keep key mappings aligned with TableInput, GiraffeMode and TennisMode.
/// </summary>
public partial class KeyTutorial : MonoBehaviour
{
    [Header("표지판")]
    [Tooltip("월드 캔버스 표지판 프리팹(KeyTutorialBoard)")] public GameObject boardPrefab;
    [Tooltip("표지판을 테이블 왼쪽(화면 기준)에 둘지")] public bool leftSide = true;
    [Tooltip("★v7 표지판 안쪽 가장자리와 테이블 화면점 사이 간격(화면 너비 비율) — v8: 0.04→0.10(책상에서 더 떨어뜨림)")] public float boardGapVp = 0.10f;
    [Tooltip("★v7 표지판 아래 가장자리가 테이블 화면점보다 위로 떨어진 거리(화면 높이 비율)")] public float boardUpVp = 0.12f;
    [Tooltip("★v8 원근감: 테이블 쪽(화면 안쪽) 가장자리를 이 각도만큼 뒤로 돌려 그쪽이 살짝 좁아 보이게(가운데 소실점 느낌). 0 = 화면과 완전 평행")] public float boardYawDeg = 16f;
    [Tooltip("★v7 표지판을 카메라 앞 이 거리(m)에 둔다 — 화면과 평행·화면 고정(상상 말풍선처럼)")] public float boardDepth = 4f;
    [Tooltip("표지판이 화면 너비에서 차지할 비율(★v7c 0.32→0.16, 사용자: 절반으로)")] public float screenWidthFraction = 0.16f;
    [Tooltip("종이 투명도(0~1)")] public float paperAlpha = 0.74f;
    [Tooltip("둥실거림 진폭(화면 높이 비율)/속도")] public float bobAmpVp = 0.004f, bobSpeed = 1.3f;
    [Tooltip("조작 가능해진 뒤 이만큼 있다가 등장")] public float appearDelay = 0.7f;
    public float fadeSeconds = 0.35f;
    public float boardFadeSeconds = 0.5f;
    [HideInInspector] public float infoSeconds = 12f; // Legacy serialized field; power practice never expires.
    [Tooltip("(프리팹에 TitleTab이 없을 때의 폴백 IMGUI 칩) 제목 글씨 크기(화면 높이 비율) / 안내 줄 크기")] public float titleSizeVp = 0.026f, noteSizeVp = 0.019f;
    [Tooltip("아래 안내 판(다리 색 안내) 유지 시간")] public float noteSeconds = 9f;
    [Tooltip("★v9b 제목/안내 판 글씨의 화면상 최소 크기(px) — 창이 작아 표지판이 작아지면 판만 키워 읽히게")] public float titleMinPx = 20f, noteMinPx = 14f;
    [Tooltip("★v9b 판의 최대 폭(표지판 폭의 배수) — 넘으면 글씨를 줄임 / 글자 양옆 여백(표지판 px)")] public float tabMaxWidthMul = 1.0f, noteMaxWidthMul = 1.3f, tabPadX = 56f;
    [Header("뒤집힘 말풍선(IMGUI, codexcow 방식)")]
    [Tooltip("말풍선 크기(1920×1080 가상 px)")] public Vector2 thoughtSize = new Vector2(400f, 226f);
    [Tooltip("앵커(테이블 루트+위)에서 말풍선까지 오프셋(가상 px)")] public Vector2 thoughtOffsetPx = new Vector2(110f, 100f);
    [Tooltip("앵커 높이(테이블 루트 기준 m)")] public float thoughtAnchorUp = 0.35f;
    [Tooltip("꼬리 끝을 테이블과 안 겹치게 말풍선 쪽 위로 밀어내는 오프셋(가상 px: x=말풍선 쪽, y=위)")] public Vector2 thoughtTailOffsetPx = new Vector2(70f, 70f);
    [Header("그룹 켜기")]
    public bool showMove = true, showPoseReset = true, showGiraffe = true, showIce = true, showTennis = true, showSquid = true;
    [HideInInspector] public bool showStretch = true; // Legacy field; showGiraffe controls the complete practice sequence.
    [Tooltip("첫 튜토리얼에서 걷기 키를 처음 누르면 다리 색 표시를 켜고 설정 안내(PlayerPrefs로 1회)")] public bool legColorTutorial = true;

    // ── 키 배열(TableInput.Map과 동일) ──
    static readonly Key[][] Map = {
        new[] { Key.W, Key.S, Key.A, Key.D, Key.E, Key.Q },
        new[] { Key.T, Key.G, Key.F, Key.H, Key.Y, Key.R },
        new[] { Key.I, Key.K, Key.J, Key.L, Key.O, Key.U },
        new[] { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.RightBracket, Key.P },
    };
    static readonly Key[] GrabKeys = { Key.E, Key.Y, Key.O, Key.RightBracket };
    static readonly Key[] ReleaseKeys = { Key.Q, Key.R, Key.U, Key.P };
    const string LegColorPref = "tut.legcolor.done", FlipPref = "tut.flip.done", MovePref = "tut.move.done";   // ★v7 걷기 16키는 한 번 다 누르면 다시는 안 뜸(능력 튜토리얼엔 능력 키만)

    static string LabelOf(Key k)
    {
        switch (k)
        {
            case Key.UpArrow: return "↑"; case Key.DownArrow: return "↓"; case Key.LeftArrow: return "←"; case Key.RightArrow: return "→";
            case Key.RightBracket: return "]"; case Key.LeftBracket: return "["; case Key.Minus: return "−"; case Key.Equals: return "+";
            case Key.Period: return "."; case Key.Slash: return "/"; case Key.Comma: return ","; case Key.RightShift: return "Shift";
            case Key.Digit1: return "1"; case Key.Digit2: return "2"; case Key.Digit3: return "3"; case Key.Digit4: return "4"; case Key.Digit5: return "5";
            case Key.Enter: return "Enter"; case Key.Semicolon: return ";"; case Key.Quote: return "'"; case Key.Backslash: return "\\"; case Key.LeftShift: return "Shift"; case Key.Space: return "";
            default: return k.ToString();
        }
    }
    static bool IsArrow(Key k) => k == Key.UpArrow || k == Key.DownArrow || k == Key.LeftArrow || k == Key.RightArrow;

    class Cap
    {
        public Key key; public string group; public GameObject go; public CanvasGroup cg; public RectTransform rt;
        public float alpha; public bool done; public float doneT, alphaAtDone;
        public Color tint; public bool hasTint;
        public bool passive;   // ★v9 참고용 키캡(눌러도 레슨이 끝나지 않음 — 예: 늘리기 레슨에 같이 뜨는 '2 = 끄기')
        public int practiceAction = -1;
    }

    /// <summary>★v9 레슨 = 표지판에 한 번에 하나만 뜨는 안내 단위. 목록 순서 = 우선순위.</summary>
    class Lesson
    {
        public string id;                    // 키캡 그룹 이름과 같음
        public System.Func<bool> eligible;   // 지금 띄울 수 있는가(이 씬에서 받은 능력 + 현재 상태)
        public System.Func<bool> achieved;   // 상태로 본 완료 — 키를 직접 안 눌러도(방향키·게스트 RPC·컷씬) 인정
        public System.Func<string> hint;     // 제목 칩 문구(레슨이 시작될 때 1회 평가)
        public System.Action fill, onStart, onFinish;
        public float expire;                 // >0: 보인 시간이 이만큼 지나면 스스로 끝남(정보성 안내)
        public bool started, finished; public float visibleT; public string hintText = "";
        public string power;
        public int ownerLeg = -1;
        public PowerPracticeProgress practice;
        public System.Func<int,bool> held, effect;
        public System.Func<string> caption;
        public System.Action<int> beginAction;
        public System.Func<bool> skipIf;
        public System.Func<int,bool> releaseEffect;
    }

    static readonly string[] LegNameKr = LegLayout.NamesKr, LegNameEn = LegLayout.NamesEn;
    static readonly int[] MoveOrder = { 0, 1, 2, 3 }; // Back-left, back-right, front-left, front-right.
    static readonly string[] Abilities = { "giraffe", "ice", "tennis", "squid" };

    readonly List<Cap> caps = new List<Cap>();
    readonly List<Lesson> lessons = new List<Lesson>();
    readonly HashSet<string> granted = new HashSet<string>();   // 이 씬에서 새로 받은 능력
    readonly HashSet<string> seenOff = new HashSet<string>();   // 이 씬에서 '없던' 적이 있는 능력(→ 이후 생기면 부여로 침)
    readonly bool[] legMine = new bool[4];   // 조작이 처음 가능해졌을 때 내 다리였던 것(이후 입력 소스가 바뀌어도 고정)
    bool legsCaptured, moveDone;
    Lesson cur; string visibleGroup, titleText = "", noteText = ""; float noteUntil = -1f;

    ArticulationBody root; TableInput ti; NetLegLink link; GiraffeMode giraffe; IcePower ice; TennisMode tennis; SquidMode squid; ArtSceneReset asr; LegColorXray xray;
    CameraFollow camFollow; bool menuScene; float readyT = -1f; bool legColorShown;
    GameObject board; CanvasGroup boardGroup; Transform keysRoot; GameObject template, templateEnter, templateShift; Text title, note; Font arrowFont;
    bool thoughtWant; float thoughtAlpha; string thoughtText = ""; bool thoughtRight = true;
    float bobPhase; Vector3 baseScale; Vector2 boardPx; Image paper; Rect boardVp = new Rect(0.1f, 0.5f, 0.16f, 0.12f);
    RectTransform titleTab, noteTab; Text titleTabText, noteTabText; int titleFont0, noteFont0; string titleShown, noteShown;

    HallwayStage hallway;
    float hallwayAlpha;
    readonly int[] hallwayKeyMasks = new int[4];
    readonly float[] hallwayAllPressedAt = { -1f, -1f, -1f, -1f };
    float hallwayCompletionRemaining;
    public Rect HallwayBubbleRect { get; private set; }
    public Vector2 HallwayTailTarget { get; private set; }
    public int HallwayKeyMask(int leg) => leg >= 0 && leg < 4 ? hallwayKeyMasks[leg] : 0;
    GUIStyle hallwayHeading, hallwayFooter, hallwayKey;
    MeshRenderer[] hallwayTableMeshes;

    void Awake() { AbilityComponents.Ensure(gameObject); }

    void Start()
    {
        ThoughtBubbleUI.Prewarm();
        foreach (var ab in transform.root.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { root = ab; break; }
        if (GetComponent<AbilityHud>() == null) gameObject.AddComponent<AbilityHud>();
        ti = TableInput.Of(this);
        hallway = Object.FindAnyObjectByType<HallwayStage>();
        hallwayTableMeshes = transform.root.GetComponentsInChildren<MeshRenderer>(true);
        link = transform.root.GetComponentInChildren<NetLegLink>(true);
        giraffe = transform.root.GetComponentInChildren<GiraffeMode>(true);
        ice = transform.root.GetComponentInChildren<IcePower>(true);
        tennis = transform.root.GetComponentInChildren<TennisMode>(true);
        squid = transform.root.GetComponentInChildren<SquidMode>(true);
        asr = transform.root.GetComponentInChildren<ArtSceneReset>(true);
        xray = transform.root.GetComponentInChildren<LegColorXray>(true);
        menuScene = Object.FindAnyObjectByType<StartScreenMenu>() != null || Object.FindAnyObjectByType<NewStartScreenUI>() != null;
        // The opening hallway always teaches its controls, even after an earlier completed run.
        moveDone = hallway == null && PlayerPrefs.GetInt(MovePref, 0) == 1;
        if (hallway != null && xray != null && !CheatMode.Active) xray.SetTutorialMask(0);
        bobPhase = Random.value * 6.28f;
        if (boardPrefab == null) boardPrefab = Resources.Load<GameObject>("KeyTutorialBoard");
        if (!menuScene && boardPrefab != null)
        {
            board = Instantiate(boardPrefab); board.name = "KeyTutorialBoard";
            boardGroup = board.GetComponent<CanvasGroup>(); if (boardGroup == null) boardGroup = board.AddComponent<CanvasGroup>();
            boardGroup.alpha = 0f;
            var kt = board.transform.Find("Keys"); keysRoot = kt != null ? kt : board.transform;
            var tp = board.transform.Find("KeyTemplate"); template = tp != null ? tp.gameObject : null;
            var te = board.transform.Find("KeyTemplate_Enter"); templateEnter = te != null ? te.gameObject : template;
            var ts = board.transform.Find("KeyTemplate_Shift"); templateShift = ts != null ? ts.gameObject : template;
            baseScale = board.transform.localScale; boardPx = board.GetComponent<RectTransform>().sizeDelta;
            var pp = board.transform.Find("Paper"); paper = pp != null ? pp.GetComponent<Image>() : null;
            if (paper != null) { var pc = paper.color; pc.a = paperAlpha; paper.color = pc; }
            // ★v9 표지판 안 제목/안내 글씨는 비움(화면 너비 0.16짜리 표지판에선 6px라 안 읽힘) — OnGUI 손글씨 칩이 대신함
            var tt = board.transform.Find("Title"); title = tt != null ? tt.GetComponent<Text>() : null; if (title != null) title.text = "";
            var nt = board.transform.Find("Note"); note = nt != null ? nt.GetComponent<Text>() : null; if (note != null) note.text = "";
            // ★v9b 자판 스타일 제목/안내 판(있으면 IMGUI 칩 대신 사용) — 종이와 같은 투명도
            titleTab = board.transform.Find("TitleTab") as RectTransform; noteTab = board.transform.Find("NoteTab") as RectTransform;
            if (titleTab != null) { titleTabText = titleTab.GetComponentInChildren<Text>(true); titleFont0 = titleTabText != null ? titleTabText.fontSize : 124; SetAlpha(titleTab.GetComponent<Image>(), paperAlpha); titleTab.gameObject.SetActive(false); }
            if (noteTab != null) { noteTabText = noteTab.GetComponentInChildren<Text>(true); noteFont0 = noteTabText != null ? noteTabText.fontSize : 84; SetAlpha(noteTab.GetComponent<Image>(), paperAlpha); noteTab.gameObject.SetActive(false); }
            var canvas = board.GetComponent<Canvas>(); if (canvas != null) { canvas.worldCamera = Camera.main; canvas.enabled = false; } // Layout registry only; live tutorial draws inside the shared thought bubble.
            arrowFont = Resources.Load<Font>("Fonts/NanumGothic-Regular");
            board.SetActive(false);
            BuildLessons();
        }
        else if (!menuScene) Debug.LogWarning("[KeyTutorial] boardPrefab 없음 — 튜토리얼 표지판 생략");
    }

    void OnDestroy() { if (board != null) Destroy(board); }

    // ───────────────────────── 능력 부여 추적 ─────────────────────────
    /// <summary>지금 이 능력을 '갖고' 있는가(켜짐 여부와 무관).</summary>
    bool Has(string id)
    {
        switch (id)
        {
            case "giraffe": return giraffe != null && !giraffe.locked;
            case "ice": return ice != null && ice.unlocked;
            case "tennis": return tennis != null && (tennis.unlocked || tennis.active);
            case "squid": return squid != null && (squid.unlocked || squid.active) && !squid.thrustLocked;
        }
        return false;
    }
    /// <summary>씬 시작 때 없던 능력이 생기면 '이 씬에서 받음'. 시작부터 갖고 있던 능력(예: 동굴의 기린)은 해당 없음 → 다시 가르치지 않는다.
    /// 첫 Update는 모든 Start(스테이지 스크립트의 잠금 설정) 뒤라 기준이 안전하다.</summary>
    bool cheatPowersSuppressed;
    public void SuppressCheatPowerTutorials() { cheatPowersSuppressed=true;granted.Clear();seenOff.Clear(); }
    void TrackGrants() { if(cheatPowersSuppressed)return; foreach (var id in Abilities) { if (!Has(id)) seenOff.Add(id); else if (seenOff.Contains(id)) granted.Add(id); } }
    /// <summary>스폰 지점이 시작과 동시에 쥐여 준 능력(SpawnPointGrant) — 잠김→풀림 전환이 없으므로 직접 알림.</summary>
    public void NotifyGranted(string id) { if(!CheatMode.Active && !cheatPowersSuppressed)granted.Add(id); }
    bool Got(string id) => !cheatPowersSuppressed && granted.Contains(id) && Has(id);
    bool SquidOn => squid != null && squid.active;   // 우주오징어 중엔 기린·얼음 금지(상호 배타) → 그 레슨도 쉼

    // ───────────────────────── 레슨 목록(순서 = 우선순위) ─────────────────────────
    Lesson AddLesson(string id, System.Func<bool> eligible, System.Func<bool> achieved, System.Func<string> hint, System.Action fill)
    {
        var l = new Lesson { id = id, eligible = eligible, achieved = achieved, hint = hint, fill = fill };
        lessons.Add(l); return l;
    }

    void BuildLessons()
    {
        BuildPowerLessons();

        // 걷기: 복도 입장마다 다리 하나씩 안내. 다른 맵에서는 완료 기록을 존중. 무중력에서는 쉼.
        for (int leg = 0; leg < 4; leg++)
        {
            int i = MoveOrder[leg]; string id = "move" + i;
            var l = AddLesson(id,
                () => showMove && !moveDone && legMine[i] && !SquidOn,
                () => MoveAchieved(id, i),   // 방향키·보조키·게스트 RPC 모두 '그 다리 입력'
                () => L.F("걷기 · {0}", "Walk · {0}", LegLayout.Name(i)),
                () =>
                {
                    for (int k = 0; k < 4; k++) Add(Map[i][k], id, LegPalette.BySlot[i], true);   // 자판 그림에 없는 키(방향키)는 자동 생략
                    foreach (var lc in transform.root.GetComponentsInChildren<ArtLegController>(true))
                    {
                        if ((int)lc.controlSet != i) continue;
                        var alts = new[] { lc.altUp, lc.altDown, lc.altLeft, lc.altRight };
                        for (int k = 0; k < 4; k++) if (alts[k] != Key.None) Add(alts[k], id, LegPalette.BySlot[i], true);
                    }
                });
            l.onFinish = () =>
            {
                if (hallway == null) OnFirstWalk();
                for (int k = 0; k < 4; k++) if (legMine[k] && !LessonById("move" + k).finished) return;
                moveDone = true; PlayerPrefs.SetInt(MovePref, 1); PlayerPrefs.Save();
                if (hallway != null)
                {
                    if (xray != null) { xray.SetTutorialMask(-1); xray.SetOn(true); }
                    legColorShown = true; PlayerPrefs.SetInt(LegColorPref, 1); PlayerPrefs.Save();
                    hallwayCompletionRemaining = noteSeconds;
                }
            };
        }
    }

    bool MoveAchieved(string id, int leg)
    {
        if (ti == null) return false;
        var input = ti.Get((LegControlSet)leg);
        bool held = input.up || input.down || input.left || input.right;
        if (hallway == null) return held;
        // Only the visible leg accumulates input; other legs and paused/hidden lessons cannot skip ahead.
        if (cur == null || cur.id != id || thoughtWant || hallwayAlpha < .5f) return false;
        int mask = (input.up ? 1 : 0) | (input.down ? 2 : 0) | (input.left ? 4 : 0) | (input.right ? 8 : 0);
        hallwayKeyMasks[leg] |= mask;
        if (hallwayKeyMasks[leg] != 15) return false;
        if (hallwayAllPressedAt[leg] < 0f) hallwayAllPressedAt[leg] = Time.time;
        return !held && Time.time - hallwayAllPressedAt[leg] >= .35f;
    }

    Lesson LessonById(string id) { foreach (var l in lessons) if (l.id == id) return l; return null; }
    void StartLesson(Lesson l) { l.started = true; l.onStart?.Invoke(); l.hintText = l.hint != null ? l.hint() : ""; l.fill?.Invoke(); }
    void Finish(Lesson l) { if (l.finished) return; l.finished = true; DismissGroup(l.id); l.onFinish?.Invoke(); }

    /// <summary>이 다리(키셋)가 이 클라이언트의 것인가.</summary>
    bool Mine(int set)
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening && link != null && link.IsSpawned)
        {
            long o = link.OwnerOf(set);
            return nm.IsServer ? (o == -1 || o == -2) : link.IsMine(set);
        }
        return ti == null || ti.IsLocal((LegControlSet)set);
    }

    /// <summary>지금 조작 가능한 상태(컷씬/일시정지/동결 아님).</summary>
    bool Controllable()
    {
        if (menuScene || Time.timeScale == 0f) return false;
        if (hallway != null && !hallway.IntroFinished) return false;
        var abilityHud=GetComponent<AbilityHud>();
        if (abilityHud!=null && abilityHud.IsPresenting) return false;
        if (root != null && root.immovable) return false;
        var cam = Camera.main; if (cam == null) return false;
        if (camFollow == null) camFollow = cam.GetComponent<CameraFollow>();
        if (camFollow != null && !camFollow.enabled) return false;
        if (LetterOverlay.IsOpen) return false;
        return true;
    }

    bool HasCap(string group) { if (group == null) return false; foreach (var c in caps) if (c.group == group && !c.done) return true; return false; }
    void DismissGroup(string group) { foreach (var c in caps) if (c.group == group && !c.done) { c.done = true; c.doneT = Time.time; c.alphaAtDone = c.alpha; } }

    void Update()
    {
        if (menuScene || board == null) return;
        // Paused game time cannot advance a fade. Hide immediately and retain lesson progress.
        if (CheatMode.Active || Time.timeScale <= 0f || PauseMenu.InputBlocked || LetterOverlay.IsOpen || (GetComponent<AbilityHud>()?.IsPresenting ?? false))
        {
            if (CheatMode.Active && xray != null) xray.SetTutorialMask(-1);
            if (boardGroup != null) boardGroup.alpha = 0f;
            board.SetActive(false);
            thoughtAlpha = hallwayAlpha = 0f;
            readyT = -1f;
            cur?.practice?.Suspend();
            return;
        }
        TrackGrants();
        bool ok = Controllable();
        if (!ok) { readyT = -1f; cur?.practice?.Suspend(); } else if (readyT < 0f) readyT = Time.time;
        float dt = Time.deltaTime;
        bool ready = ok && Time.time - readyT >= appearDelay;
        if (ready && !legsCaptured) { legsCaptured = true; for (int i = 0; i < 4; i++) legMine[i] = Mine(i); }

        // ① 시작된 레슨의 완료(상태)·만료 — 가려져 있는 레슨도 포함(조용히 끝남)
        if (ok) foreach (var l in lessons)
            if (l.practice == null && l.started && !l.finished && ((l.achieved != null && l.achieved()) || (l.expire > 0f && l.visibleT >= l.expire))) Finish(l);

        // ② 지금 차례인 레슨 = 목록 순서에서 '안 끝났고 띄울 수 있는' 첫 번째. 새 레슨은 조작 가능 + appearDelay 뒤에만 시작
        if (ok)
        {
            cur = null;
            foreach (var l in lessons)
            {
                if (l.finished || !l.eligible()) continue;
                if (!l.started && !ready) continue;
                // Optional setup is skipped once, so switching powers later cannot revive it.
                if (!l.started && l.skipIf != null && l.skipIf()) { Finish(l); continue; }
                cur = l; break;
            }
            if (cur != null && !cur.started) StartLesson(cur);
        }

        if (hallway != null && hallway.IntroFinished && xray != null && !moveDone)
            xray.SetTutorialMask(cur != null && cur.id.StartsWith("move") ? 1 << (cur.id[cur.id.Length-1]-'0') : 0);
        if (ok && hallwayCompletionRemaining > 0f) hallwayCompletionRemaining = Mathf.Max(0f, hallwayCompletionRemaining - dt);

        // ③ 뒤집힘 → 상상 말풍선(+ 남은 횟수가 있으면 '1' 키캡). 벽 등반·우주는 ArtSceneReset.FlipPromptActive가 false. 뒤집힌 동안 다른 레슨은 숨음
        bool flip = showPoseReset && ok && asr != null && asr.FlipPromptActive;
        bool canReset = ArtSceneReset.Unlimited || (asr != null && asr.PoseResetsLeft > 0);
        if (flip && !thoughtWant)
        {
            bool first = PlayerPrefs.GetInt(FlipPref, 0) == 0;
            if (!canReset) thoughtText = L.T("바로서기를 다 썼어요…\nF1로 다시 시작", "no pose fixes left…\nrestart with F1");
            else if (first) thoughtText = ArtSceneReset.Unlimited
                ? L.T("뒤집혔어요!\n1을 누르면 바로서기\n(이 난이도에선 무제한)", "flipped over!\npress 1 to stand up\n(unlimited here)")
                : L.T("뒤집혔어요!\n1을 누르면 바로서기\n남은 횟수는 왼쪽 위 동그라미", "flipped over!\npress 1 to stand up\ntop-left circle = tries left");
            else thoughtText = L.T("뒤집혔어요!\n1을 누르면 바로서기", "flipped over!\npress 1 to stand up");
            if (first && canReset) { PlayerPrefs.SetInt(FlipPref, 1); PlayerPrefs.Save(); }
        }
        thoughtWant = flip;
        if (flip && canReset && !HasCap("pose")) Add(Key.Digit1, "pose", Color.white, false);
        if (!flip && HasCap("pose")) DismissGroup("pose");   // 스스로 일어났거나 상황 종료 → 지움

        visibleGroup = flip ? "pose" : (cur != null ? cur.id : null);
        if (ok && cur != null && visibleGroup == cur.id) cur.visibleT += dt;
        if (cur != null && cur.practice != null && !cur.finished)
        {
            if (ok && !flip && boardGroup.alpha > .5f && cur.visibleT > .2f)
            {
                int action = cur.practice.Stage;
                if (cur.practice.Tick(dt,cur.held(action),cur.effect(action),cur.releaseEffect==null||cur.releaseEffect(action)))
                {if(cur.practice.Complete)Finish(cur);else cur.beginAction?.Invoke(cur.practice.Stage);}
            }
            else cur.practice.Suspend();
        }
        titleText = flip ? (HasCap("pose") ? L.T("1 = 바로서기", "1 = stand up") : "") : (cur != null && cur.hint != null ? cur.hint() : "");

        // ④ 키캡: 보이는 그룹만 알파 1(나머지는 숨음), 직접 누르면 그 레슨 완료
        var kb = Keyboard.current; string pressedGroup = null;
        for (int i = caps.Count - 1; i >= 0; i--)
        {
            var c = caps[i];
            if (!c.done)
            {
                bool vis = c.group == visibleGroup;
                c.alpha = Mathf.MoveTowards(c.alpha, vis ? 1f : 0f, dt / fadeSeconds);
                if (vis && ok && !(hallway != null && c.group.StartsWith("move")) && !c.passive && kb != null && kb[c.key].wasPressedThisFrame) pressedGroup = c.group;
                if (c.cg != null) { c.cg.alpha = c.alpha; c.rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, c.alpha); }
            }
            else
            {
                float e = Mathf.Clamp01((Time.time - c.doneT) / fadeSeconds);
                if (c.cg != null) { c.cg.alpha = c.alphaAtDone * (1f - e); c.rt.localScale = Vector3.one * (1f + 0.35f * e); }   // 숨어 있던 키캡은 번쩍이지 않고 조용히 사라짐
                if (e >= 1f) { if (c.go != null) Destroy(c.go); caps.RemoveAt(i); }
            }
        }
        if (pressedGroup == "pose") DismissGroup("pose");
        else if (pressedGroup != null)
        {
            var l = LessonById(pressedGroup);
            // A key press is an attempt: attaching/releasing/stretching must actually succeed.
            if (l != null && l.practice == null && (l.achieved == null || l.achieved())) Finish(l);
        }

        // 제목/안내 판 내용(자판 스타일, 표지판 캔버스 소속 → 표지판과 함께 기울고 페이드)
        SetTab(titleTab, titleTabText, titleText, titleFont0, tabMaxWidthMul, ref titleShown);
        SetTab(noteTab, noteTabText, Time.time < noteUntil ? noteText : "", noteFont0, noteMaxWidthMul, ref noteShown);

        // ⑤ 표지판 전체 페이드(조작 가능 && 보이는 그룹에 키캡 있음)
        bool hallwayMove = hallway != null && cur != null && cur.id.StartsWith("move");
        bool want = ok && HasCap(visibleGroup) && !(hallwayMove && !flip);
        if (want && !board.activeSelf) { board.SetActive(true); PoseBoard(); }
        if (board.activeSelf)
        {
            boardGroup.alpha = Mathf.MoveTowards(boardGroup.alpha, want ? 1f : 0f, dt / boardFadeSeconds);
            if (!want && boardGroup.alpha <= 0f) board.SetActive(false);
        }
        bool hallwayWant = hallway != null && hallway.IntroFinished && !hallway.IsFalling && !flip && ok
            && (hallwayMove || hallwayCompletionRemaining > 0f);
        hallwayAlpha = Mathf.MoveTowards(hallwayAlpha, hallwayWant ? 1f : 0f, dt / boardFadeSeconds);
        // 말풍선 페이드(그리기는 OnGUI)
        thoughtAlpha = Mathf.MoveTowards(thoughtAlpha, thoughtWant && ok ? 1f : 0f, dt / boardFadeSeconds);
    }

    /// <summary>첫 튜토리얼: 다리를 처음 움직이면 다리 색 표시를 켜고 설정 안내(1회, PlayerPrefs).</summary>
    void OnFirstWalk()
    {
        if (!legColorTutorial || legColorShown || PlayerPrefs.GetInt(LegColorPref, 0) == 1) return;
        legColorShown = true; PlayerPrefs.SetInt(LegColorPref, 1); PlayerPrefs.Save();
        if (xray != null && !PlayerPrefs.HasKey(LegColorXray.PreferenceKey)) xray.SetOn(true);
        if (hallway != null) return;
        noteText = L.T("다리마다 자기 색이 켜졌어요 (키 색과 같아요)\nESC 설정에서 끌 수 있어요", "each leg now shows its color (same as the keys)\nturn it off in ESC settings");
        noteUntil = Time.time + noteSeconds;
    }

    static void SetAlpha(Image im, float a) { if (im == null) return; var c = im.color; c.a = a; im.color = c; }

    /// <summary>★v9b 자판 스타일 판에 글자를 넣고 폭을 글자에 맞춘다(최대 폭을 넘으면 글씨를 줄임). 빈 문자열이면 판을 숨김.</summary>
    void SetTab(RectTransform tab, Text txt, string s, int font0, float maxWidthMul, ref string shown)
    {
        if (tab == null || txt == null) return;
        if (string.IsNullOrEmpty(s)) { if (tab.gameObject.activeSelf) tab.gameObject.SetActive(false); shown = null; return; }
        if (!tab.gameObject.activeSelf) tab.gameObject.SetActive(true);
        if (shown == s) return; shown = s;
        txt.font = LocaleFonts.Body; txt.text = s; txt.fontSize = font0;
        float inner = Mathf.Max(200f, boardPx.x * maxWidthMul - tabPadX * 2f - 23f), pw = txt.preferredWidth;
        if (pw > inner) { txt.fontSize = Mathf.Max(24, Mathf.FloorToInt(font0 * inner / pw)); pw = txt.preferredWidth; }
        tab.sizeDelta = new Vector2(pw + tabPadX * 2f + 23f, tab.sizeDelta.y);   // 23 = 판 스프라이트의 좌(8)+우(15) 여백
    }

    // ───────────────────────── IMGUI: (폴백) 제목 칩 · 안내 줄 / 뒤집힘 말풍선 ─────────────────────────
    readonly Dictionary<int, GUIStyle> chipStyles = new Dictionary<int, GUIStyle>();
    GUIStyle ChipStyle(int size) { if (!chipStyles.TryGetValue(size, out var st) || st == null) { st = HandUI.Style(size, HandUI.Ink); chipStyles[size] = st; } return st; }

    /// <summary>표지판 위(above)/아래에 흰 종이 칩 + 손글씨. 표지판 화면 사각형(boardVp)은 PoseBoard가 갱신.</summary>
    void DrawChip(string text, bool above, float sizeVp, float alpha)
    {
        int fs = Mathf.Clamp(Mathf.RoundToInt(Screen.height * sizeVp), 13, 46);
        var st = ChipStyle(fs);
        Vector2 ts = st.CalcSize(new GUIContent(text));
        float w = ts.x + fs * 1.5f, h = ts.y + fs * 0.45f, gap = fs * 0.3f;
        float cx = (boardVp.x + boardVp.width * 0.5f) * Screen.width;
        float yTop = (1f - (boardVp.y + boardVp.height)) * Screen.height, yBot = (1f - boardVp.y) * Screen.height;
        float y = above ? yTop - h - gap : yBot + gap;
        if (above && y < 6f) y = yBot + gap;
        var r = new Rect(Mathf.Clamp(cx - w * 0.5f, 6f, Mathf.Max(6f, Screen.width - w - 6f)), y, w, h);
        UiKit.Fill(new Rect(r.x + r.width * 0.018f, r.y + r.height * 0.06f, r.width * 0.964f, r.height * 0.88f), new Color(1f, 1f, 1f, 0.9f * alpha));
        var prev = GUI.color; GUI.color = new Color(1f, 1f, 1f, alpha);
        if (HandUI.BoxWide != null) GUI.DrawTexture(r, HandUI.BoxWide, ScaleMode.StretchToFill);
        GUI.Label(r, text, st);
        GUI.color = prev;
    }

    void OnGUI()
    {
        if (CheatMode.Active || menuScene || root == null || Time.timeScale <= 0f || LetterOverlay.IsOpen || Event.current.type != EventType.Repaint) return;
        if (hallway != null && !thoughtWant && hallwayAlpha > 0.001f) DrawHallwayThought();
        bool boardOn = board != null && board.activeSelf && boardGroup != null && boardGroup.alpha > 0.01f;
        bool drawThought = thoughtAlpha > 0.001f && !string.IsNullOrEmpty(thoughtText);
        if (!boardOn && !drawThought) return;
        var cam = Camera.main; if (cam == null) return;
        UiKit.ApplyKoreanFont();

        if (boardOn && !drawThought && cur != null && !cur.finished) DrawLessonThought();

        if (drawThought)
        {
            ThoughtBubbleUI.Begin();
            if (ThoughtBubbleUI.GuiPoint(cam, root.transform.position + Vector3.up * thoughtAnchorUp, out var anchor))
            {
                // 화면 오른쪽 여유가 없으면 왼쪽으로
                thoughtRight = anchor.x + thoughtOffsetPx.x + thoughtSize.x < ThoughtBubbleUI.VW - 24f || anchor.x < ThoughtBubbleUI.VW * 0.5f;
                var rect = ThoughtBubbleUI.PlaceRect(anchor, thoughtSize.x, thoughtSize.y, thoughtRight, thoughtOffsetPx.x, thoughtOffsetPx.y);
                float grow = Mathf.Lerp(0.7f, 1f, ThoughtBubbleUI.EaseOut(thoughtAlpha));
                rect = ThoughtBubbleUI.ScaleFromCenter(rect, grow);
                var tailAnchor = anchor + new Vector2(thoughtRight ? thoughtTailOffsetPx.x : -thoughtTailOffsetPx.x, -thoughtTailOffsetPx.y);
                rect=ThoughtBubbleUI.FitTail(rect,tailAnchor);
                ThoughtBubbleUI.DrawTail(rect, tailAnchor, thoughtAlpha);
                ThoughtBubbleUI.DrawBubble(rect, thoughtAlpha, thoughtText, 36);
            }
            ThoughtBubbleUI.End();
        }
    }

    // 기존 상상 구름과 손글씨를 그대로 사용하며, 키캡도 구름 안에서 입력에 반응한다.
    void DrawHallwayThought()
    {
        var cam = Camera.main;
        if (cam == null || Time.timeScale == 0f || LetterOverlay.IsOpen || !hallway.IntroFinished || (moveDone && hallwayCompletionRemaining <= 0f)) return;
        bool move = cur != null && cur.id.StartsWith("move");
        if(!move && hallwayCompletionRemaining<=0f)return;
        ThoughtBubbleUI.Begin();
        int slot=move?cur.id[cur.id.Length-1]-'0':0;
        var mesh=LegLayout.Find(transform.root,slot);
        Vector2 anchor=new Vector2(960,650);
        Rect targetBounds=mesh!=null?ThoughtTailLayout.TargetBounds(cam,mesh):new Rect(anchor.x-32,anchor.y-32,64,64);
        anchor=targetBounds.center;
        const float width=540f;float height=ThoughtBubbleUI.WideHeight(width);
        bool left=slot%2==0;
        var rect=new Rect(left?anchor.x-width-100f:anchor.x+100f,anchor.y-height-(slot<2?25f:165f),width,height);
        rect.x=Mathf.Clamp(rect.x,24f,ThoughtBubbleUI.VW-width-24f);
        rect.y=Mathf.Clamp(rect.y,140f,ThoughtBubbleUI.VH-height-24f);
        if(hallwayCompletionRemaining>0f)rect=new Rect(100,180,720,ThoughtBubbleUI.WideHeight(720));
        rect=ThoughtBubbleUI.FitTail(rect,targetBounds);
        HallwayBubbleRect=rect;HallwayTailTarget=anchor;
        ThoughtBubbleUI.DrawPointingTail(rect,anchor,hallwayAlpha);
        if (hallwayCompletionRemaining > 0f)
        {
            ThoughtBubbleUI.DrawWideBubble(rect, hallwayAlpha,
                L.T("이제 네 다리의 색이 모두 켜졌어!\nESC → 옵션에서\n다리 색 표시를 끌 수 있어.",
                    "All four leg colors are on!\nTurn them off in\nESC → Options."), 24);
        }
        else
        {
            ThoughtBubbleUI.DrawWideBubble(rect, hallwayAlpha, "");
            if (hallwayHeading == null)
            {
                hallwayHeading = HandUI.Style(26, Color.black, bold:true); hallwayHeading.wordWrap = true;
                hallwayFooter = HandUI.Style(18, Color.black, bold:true); hallwayFooter.wordWrap = true;
                hallwayKey = HandUI.Style(24, Color.black);
                if (arrowFont != null) hallwayKey.font = arrowFont;
            }
            int leg = cur.id[cur.id.Length - 1] - '0';
            int pressedCount = 0; for (int d=0;d<4;d++) if ((hallwayKeyMasks[leg] & (1<<d)) != 0) pressedCount++;
            bool touched = pressedCount > 0;
            string heading = touched ? L.T("네 방향을 모두 움직여 보자!", "Try all four directions!")
                : L.F("{0}를 모두 눌러 보자!", "Try every {0} key!", LegLayout.Keys(leg));
            var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, hallwayAlpha);
            Rect content=ThoughtBubbleUI.WideContent(rect);
            ThoughtBubbleUI.FittedLabel(new Rect(content.x,content.y,content.width,28),heading,hallwayHeading,26);
            Key[] keys = { Map[leg][0], Map[leg][1], Map[leg][2], Map[leg][3] };
            // 네 번째 다리는 기존 표지판에서 사용하던 보조 키 배치를 유지한다.
            if (leg == 3)
                foreach (var lc in transform.root.GetComponentsInChildren<ArtLegController>(true))
                    if ((int)lc.controlSet == leg && lc.altUp != Key.None && lc.altDown != Key.None && lc.altLeft != Key.None && lc.altRight != Key.None)
                        keys = new[] { lc.altUp, lc.altDown, lc.altLeft, lc.altRight };
            float cx=content.center.x, keyTop=content.y+29f;
            const float size=32f,gap=4f;
            DrawThoughtKey(new Rect(cx-size*.5f,keyTop,size,size),keys[0],leg,0);
            DrawThoughtKey(new Rect(cx-size*1.5f-gap,keyTop+size+gap,size,size),keys[2],leg,2);
            DrawThoughtKey(new Rect(cx-size*.5f,keyTop+size+gap,size,size),keys[1],leg,1);
            DrawThoughtKey(new Rect(cx+size*.5f+gap,keyTop+size+gap,size,size),keys[3],leg,3);
            string footer=LegLayout.Name(leg)+" · "+pressedCount+"/4 · "+(pressedCount==4?L.T("손을 떼면 다음 다리","release to continue"):L.T("네 방향","press every key"));
            ThoughtBubbleUI.FittedLabel(new Rect(content.x,content.yMax-20f,content.width,20f),footer,hallwayFooter,22);
            GUI.color = old;
        }
        ThoughtBubbleUI.End();
    }

    bool TryHallwayTableRect(Camera cam, out Rect rect)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        if (hallwayTableMeshes != null) foreach (var mesh in hallwayTableMeshes)
        {
            if (mesh == null || !mesh.enabled || !mesh.gameObject.activeInHierarchy) continue;
            var bounds = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                if (!ThoughtBubbleUI.GuiPoint(cam, corner, out var point)) continue;
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return !float.IsInfinity(min.x) && rect.xMax > 0f && rect.xMin < ThoughtBubbleUI.VW
            && rect.yMax > 0f && rect.yMin < ThoughtBubbleUI.VH;
    }

    void DrawHallwayTail(Rect bubble, Vector2 end)
    {
        ThoughtBubbleUI.DrawTail(bubble,end,hallwayAlpha);
    }

    GUIStyle lessonHeading,lessonCaption,lessonKey;
    string placedLesson;
    Rect placedTable,placedFocus,placedBubble;
    float placeAfter;
    public Rect PowerBubbleRect { get; private set; }
    public Vector2 PowerTailTarget { get; private set; }
    readonly List<Cap> thoughtCaps=new List<Cap>();
    void DrawLessonThought()
    {
        var cam=Camera.main;if(cam==null||cur==null)return;
        ThoughtBubbleUI.Begin();
        if(!TryHallwayTableRect(cam,out var tableRect)) { ThoughtBubbleUI.End();return; }
        Rect focus=tableRect;
        if(cur.ownerLeg>=0)
        {
            var leg=LegLayout.Find(transform.root,cur.ownerLeg);
            if(leg!=null)focus=ThoughtTailLayout.TargetBounds(cam,leg);
        }
        Vector2 end=focus.center;
        float width=cur.ownerLeg>=0?660:cur.id=="stretchAll"?740:cur.id=="iceSlide"?720:680;
        bool preferLeft=cur.ownerLeg<0 || cur.ownerLeg%2==0;
        if(placedLesson!=cur.id || Time.unscaledTime>=placeAfter &&
            ((placedTable.center-tableRect.center).sqrMagnitude>36 || (placedTable.size-tableRect.size).sqrMagnitude>64 || (placedFocus.center-focus.center).sqrMagnitude>36))
        {
            placedBubble=PowerBubbleLayout.Place(tableRect,focus,width,preferLeft);
            placedTable=tableRect;placedFocus=focus;placedLesson=cur.id;placeAfter=Time.unscaledTime+.10f;
        }
        var r=placedBubble;
        if(!PowerBubbleLayout.Clear(r,tableRect,end))
            r=placedBubble=PowerBubbleLayout.Place(tableRect,focus,width,preferLeft);
        PowerBubbleRect=r;PowerTailTarget=end;
        float alpha=boardGroup.alpha;
        ThoughtBubbleUI.DrawTail(r,end,alpha);ThoughtBubbleUI.DrawWideBubble(r,alpha);
        if(lessonHeading==null){lessonHeading=HandUI.Style(22,Color.black,bold:true);lessonHeading.wordWrap=true;lessonCaption=HandUI.Style(18,Color.black,bold:true);lessonCaption.wordWrap=true;lessonKey=HandUI.Style(24,Color.black);if(arrowFont!=null)lessonKey.font=arrowFont;}
        var old=GUI.color;GUI.color=new Color(1,1,1,alpha);
        Rect content=ThoughtBubbleUI.WideContent(r);
        ThoughtBubbleUI.FittedLabel(new Rect(content.x,content.y,content.width,32),titleText,lessonHeading,26);
        thoughtCaps.Clear();foreach(var cap in caps)if(!cap.done&&cap.group==visibleGroup)thoughtCaps.Add(cap);
        int count=thoughtCaps.Count,columns=Mathf.Min(6,count),rows=Mathf.CeilToInt(count/6f);
        float keySize=cur.practice!=null?48:40,gap=cur.practice!=null?14:8;
        float top=content.y+38;
        for(int n=0;n<count;n++)
        {
            int row=n/6,index=n%6,inRow=Mathf.Min(6,count-row*6);
            float left=r.center.x-(inRow*keySize+(inRow-1)*gap)*.5f;
            DrawLiveKey(new Rect(left+index*(keySize+gap),top+row*(keySize+gap),keySize,keySize),thoughtCaps[n],alpha);
        }
        string hint=cur.caption!=null?cur.caption():P("표시된 키를 눌러 해 보자","Try the highlighted keys","表示されたキーを押そう","试试标出的按键");
        GUI.color=new Color(1,1,1,alpha);ThoughtBubbleUI.FittedLabel(new Rect(content.x,content.yMax-40,content.width,40),hint,lessonCaption,22);
        GUI.color=old;ThoughtBubbleUI.End();
    }
    void DrawLiveKey(Rect r,Cap cap,float alpha)
    {
        bool pressed=Keyboard.current!=null&&Keyboard.current[cap.key].isPressed;if(pressed)r.y+=3;
        bool completed=cur!=null&&cur.practice!=null&&cap.practiceAction>=0&&cur.practice.Stage>cap.practiceAction;
        bool current=cur!=null&&cur.practice!=null&&cap.practiceAction==cur.practice.Stage;
        Color tint=completed?new Color(.66f,.86f,.60f):cap.hasTint?Color.Lerp(Color.white,cap.tint,pressed?.65f:current?.4f:.18f):Color.white;
        var old=GUI.color;GUI.color=new Color(tint.r,tint.g,tint.b,alpha);HandUI.Paper(r,HandUI.BoxTile);
        tint.a=alpha;UiKit.Fill(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),tint);
        if(cap.hasTint){var stripe=cap.tint;stripe.a=alpha;UiKit.Fill(new Rect(r.x+7,r.yMax-8,r.width-14,4),stripe);}
        GUI.color=new Color(1,1,1,cap.passive?alpha*.5f:alpha);string key=LabelOf(cap.key);lessonKey.fontSize=key.Length>2?16:cur!=null&&cur.id=="stretchAll"&&!cap.passive?38:28;GUI.Label(r,key,lessonKey);GUI.color=old;
    }

    void DrawThoughtKey(Rect r, Key key, int leg, int direction)
    {
        var input = ti != null ? ti.Get((LegControlSet)leg) : default;
        bool pressed = direction == 0 ? input.up : direction == 1 ? input.down : direction == 2 ? input.left : input.right;
        if (pressed) r.y += 4f;
        bool completed = (hallwayKeyMasks[leg] & (1 << direction)) != 0;
        Color tint = Color.Lerp(Color.white, LegPalette.BySlot[leg], completed ? (pressed ? .9f : .7f) : .16f);
        var old = GUI.color;GUI.color=new Color(1,1,1,hallwayAlpha);
        UiKit.Fill(r,Color.black);
        UiKit.Fill(new Rect(r.x+1.5f,r.y+1.5f,r.width-3f,r.height-3f),tint);
        UiKit.Fill(new Rect(r.x+3f,r.yMax-5f,r.width-6f,3f),LegPalette.BySlot[leg]);
        GUI.color = new Color(1f, 1f, 1f, hallwayAlpha);
        int oldSize = hallwayKey.fontSize; hallwayKey.fontSize = key == Key.Enter ? 12 : 23;
        GUI.Label(r, LabelOf(key), hallwayKey); hallwayKey.fontSize = oldSize;
        GUI.color = old;
    }

    // ★v7 표지판은 상상 말풍선처럼 '화면에 고정': 카메라가 다 움직인 뒤(렌더 직전) 카메라 기준으로 놓는다 — LateUpdate 순서 무관, 1프레임 지연 없음
    void OnEnable() { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera; }
    void OnDisable() { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera; if (xray != null) xray.SetTutorialMask(-1); }
    void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
    {
        if (board == null || !board.activeSelf || cam == null || cam.cameraType != CameraType.Game || cam != Camera.main) return;
        PoseBoard(cam);
    }

    void PoseBoard() { var cam = Camera.main; if (cam != null) PoseBoard(cam); }

    /// <summary>★v7 화면 공간 배치(상상 말풍선과 같은 방식): 테이블의 화면 투영점 왼쪽·위에 화면 비율로 놓고, 카메라 앞 boardDepth에
    /// 화면과 평행하게 둔다 → 시점을 돌리면 화면에 붙은 채 테이블을 따라간다. 크기는 화면 너비의 screenWidthFraction.</summary>
    void PoseBoard(Camera cam)
    {
        if (root == null) return;
        float t = Time.time * bobSpeed + bobPhase;
        float depth = Mathf.Max(cam.nearClipPlane + 0.5f, boardDepth);
        float visibleH = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), visibleW = visibleH * cam.aspect;
        float baseW = boardPx.x * baseScale.x, baseH = boardPx.y * baseScale.y;
        float mul = visibleW * screenWidthFraction / Mathf.Max(0.01f, baseW);
        board.transform.localScale = baseScale * mul;
        float hwVp = screenWidthFraction * 0.5f, hhVp = baseH * mul / visibleH * 0.5f;
        // 테이블 화면점(카메라 뒤면 화면 중앙 아래쪽으로 폴백)
        Vector3 vp = cam.WorldToViewportPoint(root.transform.position + Vector3.up * 0.4f);
        if (vp.z <= 0f) vp = new Vector3(0.5f, 0.35f, 1f);
        float side = leftSide ? -1f : 1f;
        float cx = vp.x + side * (boardGapVp + hwVp);
        float cy = vp.y + boardUpVp + hhVp + Mathf.Sin(t) * bobAmpVp;
        // 화면 안 클램프(가장자리 여백 2%/4%)
        cx = Mathf.Clamp(cx, 0.02f + hwVp, 0.98f - hwVp);
        // ★v9b 판 글씨가 화면에서 너무 작아지지 않게(작은 창) 판만 키움 — 판의 피벗이 표지판 모서리라 붙은 채로 커짐
        float unitPx = screenWidthFraction * Screen.width / Mathf.Max(1f, boardPx.x);   // 표지판 1px의 화면 px
        float tabK = Mathf.Max(1f, titleMinPx / Mathf.Max(0.01f, titleFont0 * unitPx));
        if (titleTab != null) titleTab.localScale = Vector3.one * tabK;
        if (noteTab != null) noteTab.localScale = Vector3.one * Mathf.Max(1f, noteMinPx / Mathf.Max(0.01f, noteFont0 * unitPx));
        float tabTopVp = titleTab != null ? hhVp * 2f * (titleTab.sizeDelta.y * tabK / Mathf.Max(1f, boardPx.y)) : 0f;   // 제목 판이 표지판 위로 차지하는 높이
        cy = Mathf.Clamp(cy, 0.04f + hhVp, 0.96f - hhVp - tabTopVp);
        boardVp = new Rect(cx - hwVp, cy - hhVp, hwVp * 2f, hhVp * 2f);   // 제목 칩/안내 줄(OnGUI) 배치용
        Vector3 pos = cam.ViewportToWorldPoint(new Vector3(cx, cy, depth));
        // v8: 화면 고정은 그대로, 카메라 기준 Y로 살짝 돌려 안쪽(테이블 쪽) 가장자리가 멀어지게 → 그쪽이 좁아 보이는 가벼운 원근감
        //   (Euler(0,+θ,0)은 오른쪽 가장자리를 카메라 쪽으로 당김 → 왼쪽 배치(side=-1)는 -θ라야 오른쪽이 멀어짐)
        board.transform.SetPositionAndRotation(pos, cam.transform.rotation * Quaternion.Euler(0f, side * boardYawDeg, Mathf.Sin(t * 0.7f) * 0.8f));
    }

    void Add(Key key, string group, Color tint, bool hasTint, bool passive = false)
    {
        if (template == null || keysRoot == null) return;
        foreach (var c in caps) if (c.key == key && c.group == group && !c.done) return;   // 같은 레슨에 이미 있음
        var slot = keysRoot.Find("Key_" + key.ToString()); if (slot == null) return;   // 자판 그림에 없는 키(방향키 등)
        var srt = slot.GetComponent<RectTransform>();
        var tplGo = srt.sizeDelta.x > 250f ? templateShift : srt.sizeDelta.x > 150f ? templateEnter : template;
        var go = Instantiate(tplGo, keysRoot); go.name = "Cap_" + key; go.SetActive(true);
        var rt = go.GetComponent<RectTransform>(); var trt = tplGo.GetComponent<RectTransform>();
        float pad = (trt.sizeDelta.y - srt.sizeDelta.y) * 0.5f;   // 템플릿 여백(그림자)만큼 바깥으로
        rt.anchoredPosition = srt.anchoredPosition + new Vector2(-pad, pad); rt.sizeDelta = trt.sizeDelta; rt.SetAsLastSibling();
        var lbl = go.transform.Find("Label"); if (lbl != null) { var tx = lbl.GetComponent<Text>(); tx.text = LabelOf(key); if ((IsArrow(key) || key == Key.Digit1 || key == Key.Digit2 || key == Key.Digit3 || key == Key.Digit4 || key == Key.Digit5) && arrowFont != null) tx.font = arrowFont; /* 화살표·숫자는 고딕(개구체 1이 I처럼 보임) */ if (tx.text.Length > 2) tx.fontSize = Mathf.RoundToInt(tx.fontSize * 0.62f); }
        // ★v7d 다리 색이 잘 보이게: 키캡 면 전체를 그 색으로 옅게 물들이고(파스텔), 아래 띠는 더 두껍고 진하게
        var capImg = go.GetComponent<Image>(); if (capImg != null) capImg.color = hasTint ? Color.Lerp(Color.white, tint, 0.55f) : Color.white;
        var bar = go.transform.Find("Bar");
        if (bar != null)
        {
            if (hasTint)
            {
                var im = bar.GetComponent<Image>(); var col = tint; col.a = 1f; im.color = col;
                var brt = bar.GetComponent<RectTransform>(); float bw = srt.sizeDelta.x, bh = srt.sizeDelta.y, nh = 14f;
                brt.anchoredPosition = new Vector2(pad + bw * 0.08f, -(pad + bh - 4f - nh)); brt.sizeDelta = new Vector2(bw * 0.84f, nh);   // 키 면 아래쪽 4px 위에 14px 띠
            }
            else bar.gameObject.SetActive(false);
        }
        var cg = go.GetComponent<CanvasGroup>(); if (cg == null) cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f;
        rt.localScale = Vector3.one * 0.6f;
        caps.Add(new Cap { key = key, group = group, go = go, cg = cg, rt = rt, alpha = 0f, passive = passive, tint = tint, hasTint = hasTint });
    }

    /// <summary>검증용: 키를 누른 것으로 처리.</summary>
    public void DebugPress(Key key)
    {
        foreach (var c in caps) if (c.key == key && !c.done && !c.passive && c.group == visibleGroup)
        { if (c.group == "pose") DismissGroup("pose"); else { var l = LessonById(c.group); if (l != null && l.practice == null) Finish(l); } return; }
    }
    /// <summary>지금 보이는 그룹의 키캡 수.</summary>
    public int VisibleCount { get { int n = 0; foreach (var c in caps) if (!c.done && c.group == visibleGroup) n++; return n; } }
    public string CurrentLesson => visibleGroup ?? "";
    public string TitleText => titleText;
    public int PracticeStage => cur?.practice?.Stage ?? -1;
    public string PracticeCaption => cur?.caption!=null?cur.caption():"";
    public string GrantedList => string.Join(",", granted);
    public bool BoardVisible => board != null && board.activeSelf && boardGroup != null && boardGroup.alpha > 0.5f;
    public bool ThoughtVisible => thoughtAlpha > 0.5f;
    public string ThoughtText => thoughtText;
    public void RestoreKnownPowers(int mask)
    {
        string[] names={"giraffe","tennis","ice","squid"};
        for(int i=0;i<4;i++)if((mask&(1<<i))!=0){seenOff.Remove(names[i]);granted.Remove(names[i]);}
    }

}
