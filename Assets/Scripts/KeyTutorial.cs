using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ★2026-09-07 떠 있는 키캡 튜토리얼(사용자 요청: 조작법 페이지 폐지 → 테이블 옆 3D 종이 표지판에 눌러야 하는 키만, 누르면 사라짐). Table.prefab 루트 부착(전 씬).
/// v5(2026-09-08): 연결선 삭제, 자판 그림에서 방향키 제거(4번째 다리는 [ ' ; Enter만 표시), '1'은 처음이 아니라 **뒤집혔을 때** 상상 말풍선과 함께
///   (v7: 표지판도 말풍선처럼 화면 고정 — 렌더 직전 카메라 기준 배치(boardDepth·boardGapVp·boardUpVp), 능력 키캡은 능력이 활성화되면 그룹째 사라짐(걷기 키만 키별), 걷기는 다리 하나씩(4키) 순서대로 — 움직이면 다음 다리, 다 하면 완료(PlayerPrefs tut.move.done) → 다시 안 뜸, 방향키/보조키/네트워크 입력 모두 인정, 표지판 폭 화면의 0.16, 키캡 면을 다리 색으로 물들임)
///   (v6: 말풍선은 codexcow 방식의 IMGUI SDF 구름 — `ThoughtBubbleUI`, 앵커=테이블 화면 투영, 퍼프 꼬리가 테이블까지, 첫 번째만 "남은 횟수는 오른쪽 위 동그라미" 줄 추가, "뒤집혔어요!" 배너 삭제)
///   ("1을 누르면 바로서기 / 남은 횟수는 오른쪽 위 동그라미") — 테니스공으로 벽에 붙은 채·우주(무중력)에선 안 뜸(ArtSceneReset.FlipPromptActive),
///   첫 튜토리얼에서 걷기 키를 처음 누르면 다리 색 표시(LegColorXray)를 켜고 표지판 아래 줄에 "ESC 설정에서 끌 수 있어요" 안내(1회, PlayerPrefs).
/// ★v9(2026-09-18 사용자: "빙산인데 기린의 힘이 떠 있다 — 표시·순서 시스템 정리"): **레슨 큐**로 재작성.
///   ① 능력 키는 '이 씬에서 새로 받았을 때만'(잠김→풀림 전환 = 컷씬 부여, 또는 SpawnPointGrant의 NotifyGranted) — 씬 시작부터 갖고 있던 능력은 다시 가르치지 않음
///   ② 한 번에 한 레슨만 보임: 뒤집힘 '1' > 기린 '2' → 늘리기/줄이기 > 테니스 붙기 → 떼기 > 얼음 '3' > 오징어 분사 > 걷기(다리 하나씩). 가려진 레슨은 사라지는 게 아니라 숨었다가 차례가 오면 다시 보임
///   ③ 제목/안내 글씨는 표지판 안(너무 작아 안 읽힘) 대신 표지판 위/아래의 **자판 스타일 판**(프리팹 TitleTab/NoteTab — 같은 캔버스라 같이 기울고 반투명, v9b), 표지판은 살짝 돌려(boardYawDeg) 테이블 쪽이 좁아 보이는 원근감
///   ④ v7에서 실수로 빠졌던 뒤집힘 상상 말풍선 OnGUI 복구
/// - `boardPrefab`(Assets/Prefabs/KeyTutorialBoard — Tools/UI/Build Key Tutorial Board) = 월드 캔버스 종이 표지판(PIL이 그린 전체 자판 그림 위에 KeyTemplate 복제).
///   테이블 왼쪽(카메라 기준)에 화면과 평행하게, 거리 보정 크기(화면 너비 screenWidthFraction), 반투명 종이, 둥실.
/// - 그룹: 걷기(내 다리 4방향 키+보조 키, 다리 색) · 기린 '2'→신축 키 · 얼음 '3' · 테니스 붙기/떼기 · 오징어 분사 · 뒤집힘 '1'(반복 가능).
/// - 멀티: 내 다리만(NetLegLink.IsMine / 호스트=빈 자리 포함), 오프라인은 TableInput.IsLocal.
/// ⚠️키 목록은 TableInput.Map / TennisMode Grab·Release / GiraffeMode Longer·Shorter와 같아야 함(여기 복제 — 바꾸면 같이).
/// </summary>
public class KeyTutorial : MonoBehaviour
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
    [Tooltip("★v9 이미 켜져 있는 능력의 토글 키 안내(예: 컷씬이 얼음을 켜 준 뒤의 '3')는 이만큼 보이면 스스로 사라짐")] public float infoSeconds = 12f;
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
    [Tooltip("기린을 켠 뒤 신축 키(E/Q Y/R O/U ]/P = −) 키캡을 띄울지(한 번 늘리면 전부 사라짐)")] public bool showStretch = true;
    [Tooltip("첫 튜토리얼에서 걷기 키를 처음 누르면 다리 색 표시를 켜고 설정 안내(PlayerPrefs로 1회)")] public bool legColorTutorial = true;

    // ── 키 배열(TableInput.Map과 동일) ──
    static readonly Key[][] Map = {
        new[] { Key.W, Key.S, Key.A, Key.D, Key.E, Key.Q },
        new[] { Key.T, Key.G, Key.F, Key.H, Key.Y, Key.R },
        new[] { Key.I, Key.K, Key.J, Key.L, Key.O, Key.U },
        new[] { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.RightBracket, Key.P },
    };
    static readonly Key[] GrabKeys = { Key.C, Key.N, Key.Period, Key.RightShift };
    static readonly Key[] ReleaseKeys = { Key.Z, Key.V, Key.M, Key.Slash };
    const string LegColorPref = "tut.legcolor.done", FlipPref = "tut.flip.done", MovePref = "tut.move.done";   // ★v7 걷기 16키는 한 번 다 누르면 다시는 안 뜸(능력 튜토리얼엔 능력 키만)

    static string LabelOf(Key k)
    {
        switch (k)
        {
            case Key.UpArrow: return "↑"; case Key.DownArrow: return "↓"; case Key.LeftArrow: return "←"; case Key.RightArrow: return "→";
            case Key.RightBracket: return "]"; case Key.LeftBracket: return "["; case Key.Minus: return "-"; case Key.Equals: return "=";
            case Key.Period: return "."; case Key.Slash: return "/"; case Key.Comma: return ","; case Key.RightShift: return "Shift";
            case Key.Digit1: return "1"; case Key.Digit2: return "2"; case Key.Digit3: return "3";
            case Key.Enter: return "Enter"; case Key.Semicolon: return ";"; case Key.Quote: return "'"; case Key.Backslash: return "\\"; case Key.LeftShift: return "Shift"; case Key.Space: return "";
            default: return k.ToString();
        }
    }
    static bool IsArrow(Key k) => k == Key.UpArrow || k == Key.DownArrow || k == Key.LeftArrow || k == Key.RightArrow;

    class Cap
    {
        public Key key; public string group; public GameObject go; public CanvasGroup cg; public RectTransform rt;
        public float alpha; public bool done; public float doneT, alphaAtDone;
        public bool passive;   // ★v9 참고용 키캡(눌러도 레슨이 끝나지 않음 — 예: 늘리기 레슨에 같이 뜨는 '2 = 끄기')
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
    }

    static readonly string[] LegNameKr = { "앞왼쪽 다리", "앞오른쪽 다리", "뒤왼쪽 다리", "뒤오른쪽 다리" }, LegNameEn = { "front-left leg", "front-right leg", "back-left leg", "back-right leg" };
    static readonly string[] Abilities = { "giraffe", "ice", "tennis", "squid" };

    readonly List<Cap> caps = new List<Cap>();
    readonly List<Lesson> lessons = new List<Lesson>();
    readonly HashSet<string> granted = new HashSet<string>();   // 이 씬에서 새로 받은 능력
    readonly HashSet<string> seenOff = new HashSet<string>();   // 이 씬에서 '없던' 적이 있는 능력(→ 이후 생기면 부여로 침)
    readonly bool[] legMine = new bool[4];   // 조작이 처음 가능해졌을 때 내 다리였던 것(이후 입력 소스가 바뀌어도 고정)
    bool legsCaptured, moveDone, releaseSeen, iceAtShow, stretchWithToggle; int prevStuck;
    Lesson cur, lGiraffe, lGrab; string visibleGroup, titleText = "", noteText = ""; float noteUntil = -1f;

    ArticulationBody root; TableInput ti; NetLegLink link; GiraffeMode giraffe; IcePower ice; TennisMode tennis; SquidMode squid; ArtSceneReset asr; LegColorXray xray;
    CameraFollow camFollow; bool menuScene; float readyT = -1f; bool legColorShown;
    GameObject board; CanvasGroup boardGroup; Transform keysRoot; GameObject template, templateEnter, templateShift; Text title, note; Font arrowFont;
    bool thoughtWant; float thoughtAlpha; string thoughtText = ""; bool thoughtRight = true;
    float bobPhase; Vector3 baseScale; Vector2 boardPx; Image paper; Rect boardVp = new Rect(0.1f, 0.5f, 0.16f, 0.12f);
    RectTransform titleTab, noteTab; Text titleTabText, noteTabText; int titleFont0, noteFont0; string titleShown, noteShown;

    void Start()
    {
        foreach (var ab in transform.root.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { root = ab; break; }
        ti = TableInput.Of(this);
        link = transform.root.GetComponentInChildren<NetLegLink>(true);
        giraffe = transform.root.GetComponentInChildren<GiraffeMode>(true);
        ice = transform.root.GetComponentInChildren<IcePower>(true);
        tennis = transform.root.GetComponentInChildren<TennisMode>(true);
        squid = transform.root.GetComponentInChildren<SquidMode>(true);
        asr = transform.root.GetComponentInChildren<ArtSceneReset>(true);
        xray = transform.root.GetComponentInChildren<LegColorXray>(true);
        menuScene = Object.FindAnyObjectByType<StartScreenMenu>() != null || Object.FindAnyObjectByType<NewStartScreenUI>() != null;
        moveDone = PlayerPrefs.GetInt(MovePref, 0) == 1;
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
            var canvas = board.GetComponent<Canvas>(); if (canvas != null) canvas.worldCamera = Camera.main;
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
            case "squid": return squid != null && squid.active && !squid.thrustLocked;
        }
        return false;
    }
    /// <summary>씬 시작 때 없던 능력이 생기면 '이 씬에서 받음'. 시작부터 갖고 있던 능력(예: 동굴의 기린)은 해당 없음 → 다시 가르치지 않는다.
    /// 첫 Update는 모든 Start(스테이지 스크립트의 잠금 설정) 뒤라 기준이 안전하다.</summary>
    void TrackGrants() { foreach (var id in Abilities) { if (!Has(id)) seenOff.Add(id); else if (seenOff.Contains(id)) granted.Add(id); } }
    /// <summary>스폰 지점이 시작과 동시에 쥐여 준 능력(SpawnPointGrant) — 잠김→풀림 전환이 없으므로 직접 알림.</summary>
    public void NotifyGranted(string id) { granted.Add(id); }
    bool Got(string id) => granted.Contains(id) && Has(id);
    bool SquidOn => squid != null && squid.active;   // 우주오징어 중엔 기린·얼음 금지(상호 배타) → 그 레슨도 쉼

    // ───────────────────────── 레슨 목록(순서 = 우선순위) ─────────────────────────
    Lesson AddLesson(string id, System.Func<bool> eligible, System.Func<bool> achieved, System.Func<string> hint, System.Action fill)
    {
        var l = new Lesson { id = id, eligible = eligible, achieved = achieved, hint = hint, fill = fill };
        lessons.Add(l); return l;
    }

    void BuildLessons()
    {
        // 기린: 꺼져 있으면 '2'부터, (컷씬이 이미 켜 줬으면) 바로 늘리기/줄이기 + 참고용 '2 = 끄기'
        lGiraffe = AddLesson("giraffe",
            () => showGiraffe && Got("giraffe") && !SquidOn && !giraffe.active,
            () => giraffe.active,
            () => L.T("2 = 기린의 힘", "2 = giraffe power"),
            () => Add(Key.Digit2, "giraffe", Color.white, false));
        var lStretch = AddLesson("stretch",
            () => showGiraffe && showStretch && Got("giraffe") && !SquidOn && giraffe.active,
            () => giraffe.AnyStretched,
            () => stretchWithToggle ? L.T("늘리기 / 줄이기 · 2 = 끄기", "stretch / shrink · 2 = off") : L.T("다리 늘리기 / 줄이기", "stretch / shrink legs"),
            () =>
            {
                for (int i = 0; i < 4; i++) if (legMine[i]) { Add(Map[i][4], "stretch", LegPalette.BySlot[i], true); Add(Map[i][5], "stretch", LegPalette.BySlot[i], true); }
                Add(Key.Equals, "stretch", Color.white, false); Add(Key.Minus, "stretch", Color.white, false);
                if (stretchWithToggle) Add(Key.Digit2, "stretch", Color.white, false, true);
            });
        lStretch.onStart = () => { stretchWithToggle = !lGiraffe.started; lGiraffe.finished = true; };

        // 테니스공: 붙기 → (붙은 뒤) 떼기
        lGrab = AddLesson("grab",
            () => showTennis && Got("tennis") && tennis.active,
            () => tennis.StuckMask != 0,
            () => L.T("테니스공 · 붙기", "tennis balls · stick"),
            () => { for (int i = 0; i < 4; i++) if (legMine[i]) Add(GrabKeys[i], "grab", LegPalette.BySlot[i], true); });
        AddLesson("release",
            () => showTennis && Got("tennis") && tennis.active && lGrab.finished && tennis.StuckMask != 0,
            () => releaseSeen,
            () => L.T("테니스공 · 떼기", "tennis balls · let go"),
            () => { for (int i = 0; i < 4; i++) if (legMine[i]) Add(ReleaseKeys[i], "release", LegPalette.BySlot[i], true); });

        // 얼음: 컷씬이 켜 준 채로 받으면 '끄기/켜기' 정보성 안내(잠깐 보이고 사라짐), 꺼진 채로 받으면 켤 때까지
        var lIce = AddLesson("ice",
            () => showIce && Got("ice") && !SquidOn,
            () => ice.active != iceAtShow,
            () => iceAtShow ? L.T("3 = 얼음 끄기 / 켜기", "3 = ice off / on") : L.T("3 = 얼음", "3 = ice"),
            () => Add(Key.Digit3, "ice", Color.white, false));
        lIce.onStart = () => { iceAtShow = ice.active; lIce.expire = iceAtShow ? infoSeconds : 0f; };

        // 우주오징어 분사
        AddLesson("squid",
            () => showSquid && Got("squid"),
            () => squid.JetMask != 0,
            () => L.T("발끝 분사", "thrust from feet"),
            () => { for (int i = 0; i < 4; i++) if (legMine[i]) Add(Map[i][4], "squid", LegPalette.BySlot[i], true); });

        // 걷기: 다리 하나씩(내 다리만), 한 번 끝내면(PlayerPrefs) 어느 맵에서도 다시 안 뜸. 무중력(오징어)에선 쉼
        for (int leg = 0; leg < 4; leg++)
        {
            int i = leg; string id = "move" + i;
            var l = AddLesson(id,
                () => showMove && !moveDone && legMine[i] && !SquidOn,
                () => { if (ti == null) return false; var li = ti.Get((LegControlSet)i); return li.up || li.down || li.left || li.right; },   // 방향키·보조키·게스트 RPC 모두 '그 다리 입력'
                () => L.T("걷기 · " + LegNameKr[i], "walk · " + LegNameEn[i]),
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
                OnFirstWalk();
                for (int k = 0; k < 4; k++) if (legMine[k] && !LessonById("move" + k).finished) return;
                moveDone = true; PlayerPrefs.SetInt(MovePref, 1); PlayerPrefs.Save();   // 내 다리를 전부 한 번씩 움직임 → 걷기 튜토리얼 완료
            };
        }
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
        TrackGrants();
        bool ok = Controllable();
        if (!ok) readyT = -1f; else if (readyT < 0f) readyT = Time.time;
        bool ready = ok && Time.time - readyT >= appearDelay;
        float dt = Time.deltaTime;
        if (ready && !legsCaptured) { legsCaptured = true; for (int i = 0; i < 4; i++) legMine[i] = Mine(i); }

        // 테니스 '떼기' 판정: 붙기 레슨을 끝낸 뒤 붙어 있던 다리가 하나라도 떨어지면
        int stuck = tennis != null ? tennis.StuckMask : 0;
        if (lGrab != null && lGrab.finished && (prevStuck & ~stuck) != 0) releaseSeen = true;
        prevStuck = stuck;

        // ① 시작된 레슨의 완료(상태)·만료 — 가려져 있는 레슨도 포함(조용히 끝남)
        if (ok) foreach (var l in lessons)
            if (l.started && !l.finished && ((l.achieved != null && l.achieved()) || (l.expire > 0f && l.visibleT >= l.expire))) Finish(l);

        // ② 지금 차례인 레슨 = 목록 순서에서 '안 끝났고 띄울 수 있는' 첫 번째. 새 레슨은 조작 가능 + appearDelay 뒤에만 시작
        if (ok)
        {
            cur = null;
            foreach (var l in lessons) { if (l.finished || !l.eligible()) continue; if (!l.started && !ready) continue; cur = l; break; }
            if (cur != null && !cur.started) StartLesson(cur);
        }

        // ③ 뒤집힘 → 상상 말풍선(+ 남은 횟수가 있으면 '1' 키캡). 벽 등반·우주는 ArtSceneReset.FlipPromptActive가 false. 뒤집힌 동안 다른 레슨은 숨음
        bool flip = showPoseReset && ok && asr != null && asr.FlipPromptActive;
        bool canReset = ArtSceneReset.Unlimited || (asr != null && asr.PoseResetsLeft > 0);
        if (flip && !thoughtWant)
        {
            bool first = PlayerPrefs.GetInt(FlipPref, 0) == 0;
            if (!canReset) thoughtText = L.T("바로서기를 다 썼어요…\nF1로 다시 시작", "no pose fixes left…\nrestart with F1");
            else if (first) thoughtText = ArtSceneReset.Unlimited
                ? L.T("뒤집혔어요!\n1을 누르면 바로서기\n(이 난이도에선 무제한)", "flipped over!\npress 1 to stand up\n(unlimited here)")
                : L.T("뒤집혔어요!\n1을 누르면 바로서기\n남은 횟수는 오른쪽 위 동그라미", "flipped over!\npress 1 to stand up\ntop-right circle = tries left");
            else thoughtText = L.T("뒤집혔어요!\n1을 누르면 바로서기", "flipped over!\npress 1 to stand up");
            if (first && canReset) { PlayerPrefs.SetInt(FlipPref, 1); PlayerPrefs.Save(); }
        }
        thoughtWant = flip;
        if (flip && canReset && !HasCap("pose")) Add(Key.Digit1, "pose", Color.white, false);
        if (!flip && HasCap("pose")) DismissGroup("pose");   // 스스로 일어났거나 상황 종료 → 지움

        visibleGroup = flip ? "pose" : (cur != null ? cur.id : null);
        if (ok && cur != null && visibleGroup == cur.id) cur.visibleT += dt;
        titleText = flip ? (HasCap("pose") ? L.T("1 = 바로서기", "1 = stand up") : "") : (cur != null ? cur.hintText : "");

        // ④ 키캡: 보이는 그룹만 알파 1(나머지는 숨음), 직접 누르면 그 레슨 완료
        var kb = Keyboard.current; string pressedGroup = null;
        for (int i = caps.Count - 1; i >= 0; i--)
        {
            var c = caps[i];
            if (!c.done)
            {
                bool vis = c.group == visibleGroup;
                c.alpha = Mathf.MoveTowards(c.alpha, vis ? 1f : 0f, dt / fadeSeconds);
                if (vis && ok && !c.passive && kb != null && kb[c.key].wasPressedThisFrame) pressedGroup = c.group;
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
        else if (pressedGroup != null) { var l = LessonById(pressedGroup); if (l != null) Finish(l); }

        // 제목/안내 판 내용(자판 스타일, 표지판 캔버스 소속 → 표지판과 함께 기울고 페이드)
        SetTab(titleTab, titleTabText, titleText, titleFont0, tabMaxWidthMul, ref titleShown);
        SetTab(noteTab, noteTabText, Time.time < noteUntil ? noteText : "", noteFont0, noteMaxWidthMul, ref noteShown);

        // ⑤ 표지판 전체 페이드(조작 가능 && 보이는 그룹에 키캡 있음)
        bool want = ok && HasCap(visibleGroup);
        if (want && !board.activeSelf) { board.SetActive(true); PoseBoard(); }
        if (board.activeSelf)
        {
            boardGroup.alpha = Mathf.MoveTowards(boardGroup.alpha, want ? 1f : 0f, dt / boardFadeSeconds);
            if (!want && boardGroup.alpha <= 0f) board.SetActive(false);
        }
        // 말풍선 페이드(그리기는 OnGUI)
        thoughtAlpha = Mathf.MoveTowards(thoughtAlpha, thoughtWant && ok ? 1f : 0f, dt / boardFadeSeconds);
    }

    /// <summary>첫 튜토리얼: 다리를 처음 움직이면 다리 색 표시를 켜고 설정 안내(1회, PlayerPrefs).</summary>
    void OnFirstWalk()
    {
        if (!legColorTutorial || legColorShown || PlayerPrefs.GetInt(LegColorPref, 0) == 1) return;
        legColorShown = true; PlayerPrefs.SetInt(LegColorPref, 1); PlayerPrefs.Save();
        if (xray != null && !xray.IsOn) xray.SetOn(true);
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
        txt.text = s; txt.fontSize = font0;
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
        if (menuScene || root == null || Event.current.type != EventType.Repaint) return;
        bool boardOn = board != null && board.activeSelf && boardGroup != null && boardGroup.alpha > 0.01f;
        bool drawThought = thoughtAlpha > 0.001f && !string.IsNullOrEmpty(thoughtText);
        if (!boardOn && !drawThought) return;
        var cam = Camera.main; if (cam == null) return;
        UiKit.ApplyKoreanFont();

        if (boardOn && titleTab == null)   // 프리팹에 자판 스타일 판이 없을 때만 IMGUI 칩
        {
            if (!string.IsNullOrEmpty(titleText)) DrawChip(titleText, true, titleSizeVp, boardGroup.alpha);
            if (Time.time < noteUntil && !string.IsNullOrEmpty(noteText)) DrawChip(noteText, false, noteSizeVp, boardGroup.alpha * Mathf.Clamp01(noteUntil - Time.time));
        }

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
                float k = thoughtSize.x / 560f;
                var tailAnchor = anchor + new Vector2(thoughtRight ? thoughtTailOffsetPx.x : -thoughtTailOffsetPx.x, -thoughtTailOffsetPx.y);
                ThoughtBubbleUI.DrawTail(rect, tailAnchor, thoughtAlpha, k);
                ThoughtBubbleUI.DrawBubble(rect, thoughtAlpha, thoughtText, 36);
            }
            ThoughtBubbleUI.End();
        }
    }

    // ★v7 표지판은 상상 말풍선처럼 '화면에 고정': 카메라가 다 움직인 뒤(렌더 직전) 카메라 기준으로 놓는다 — LateUpdate 순서 무관, 1프레임 지연 없음
    void OnEnable() { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera; }
    void OnDisable() { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera; }
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
        var lbl = go.transform.Find("Label"); if (lbl != null) { var tx = lbl.GetComponent<Text>(); tx.text = LabelOf(key); if ((IsArrow(key) || key == Key.Digit1 || key == Key.Digit2 || key == Key.Digit3) && arrowFont != null) tx.font = arrowFont; /* 화살표·숫자는 고딕(개구체 1이 I처럼 보임) */ if (tx.text.Length > 2) tx.fontSize = Mathf.RoundToInt(tx.fontSize * 0.62f); }
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
        caps.Add(new Cap { key = key, group = group, go = go, cg = cg, rt = rt, alpha = 0f, passive = passive });
    }

    /// <summary>검증용: 키를 누른 것으로 처리.</summary>
    public void DebugPress(Key key)
    {
        foreach (var c in caps) if (c.key == key && !c.done && !c.passive && c.group == visibleGroup)
        { if (c.group == "pose") DismissGroup("pose"); else { var l = LessonById(c.group); if (l != null) Finish(l); } return; }
    }
    /// <summary>지금 보이는 그룹의 키캡 수.</summary>
    public int VisibleCount { get { int n = 0; foreach (var c in caps) if (!c.done && c.group == visibleGroup) n++; return n; } }
    public string CurrentLesson => visibleGroup ?? "";
    public string TitleText => titleText;
    public string GrantedList => string.Join(",", granted);
    public bool BoardVisible => board != null && board.activeSelf && boardGroup != null && boardGroup.alpha > 0.5f;
    public bool ThoughtVisible => thoughtAlpha > 0.5f;
    public string ThoughtText => thoughtText;
}
