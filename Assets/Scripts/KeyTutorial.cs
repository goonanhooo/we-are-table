using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ★2026-09-07 떠 있는 키캡 튜토리얼(사용자 요청: 조작법 페이지 폐지 → 테이블 옆 3D 종이 표지판에 눌러야 하는 키만, 누르면 사라짐). Table.prefab 루트 부착(전 씬).
/// v5(2026-09-08): 연결선 삭제, 자판 그림에서 방향키 제거(4번째 다리는 [ ' ; Enter만 표시), '1'은 처음이 아니라 **뒤집혔을 때** 상상 말풍선과 함께
///   (v7: 표지판도 말풍선처럼 화면 고정 — 렌더 직전 카메라 기준 배치(boardDepth·boardGapVp·boardUpVp), 능력 키캡은 능력이 활성화되면 그룹째 사라짐(걷기 키만 키별), 걷기 16키는 한 번 완료하면(PlayerPrefs tut.move.done) 다시 안 뜸 — 능력 튜토리얼엔 능력 키만)
///   (v6: 말풍선은 codexcow 방식의 IMGUI SDF 구름 — `ThoughtBubbleUI`, 앵커=테이블 화면 투영, 퍼프 꼬리가 테이블까지, 첫 번째만 "남은 횟수는 오른쪽 위 동그라미" 줄 추가, "뒤집혔어요!" 배너 삭제)
///   ("1을 누르면 바로서기 / 남은 횟수는 오른쪽 위 동그라미") — 테니스공으로 벽에 붙은 채·우주(무중력)에선 안 뜸(ArtSceneReset.FlipPromptActive),
///   첫 튜토리얼에서 걷기 키를 처음 누르면 다리 색 표시(LegColorXray)를 켜고 표지판 아래 줄에 "ESC 설정에서 끌 수 있어요" 안내(1회, PlayerPrefs).
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
    [Tooltip("★v7 표지판 안쪽 가장자리와 테이블 화면점 사이 간격(화면 너비 비율)")] public float boardGapVp = 0.04f;
    [Tooltip("★v7 표지판 아래 가장자리가 테이블 화면점보다 위로 떨어진 거리(화면 높이 비율)")] public float boardUpVp = 0.10f;
    [Tooltip("★v7 표지판을 카메라 앞 이 거리(m)에 둔다 — 화면과 평행·화면 고정(상상 말풍선처럼)")] public float boardDepth = 4f;
    [Tooltip("표지판이 화면 너비에서 차지할 비율")] public float screenWidthFraction = 0.32f;
    [Tooltip("종이 투명도(0~1)")] public float paperAlpha = 0.74f;
    [Tooltip("둥실거림 진폭(화면 높이 비율)/속도")] public float bobAmpVp = 0.004f, bobSpeed = 1.3f;
    [Tooltip("조작 가능해진 뒤 이만큼 있다가 등장")] public float appearDelay = 0.7f;
    public float fadeSeconds = 0.35f;
    public float boardFadeSeconds = 0.5f;
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
        public float alpha; public bool done; public float doneT;
    }

    readonly List<Cap> caps = new List<Cap>();
    readonly HashSet<string> shown = new HashSet<string>();   // 이미 등장한 그룹(뒤집힘 '1'은 제외 — 반복)
    readonly Dictionary<string, string> groupHint = new Dictionary<string, string>();

    ArticulationBody root; TableInput ti; NetLegLink link; GiraffeMode giraffe; IcePower ice; TennisMode tennis; SquidMode squid; ArtSceneReset asr; LegColorXray xray;
    CameraFollow camFollow; bool menuScene; float readyT = -1f; bool giraffeKeysShown; bool legColorShown; bool moveGroupPressed;
    GameObject board; CanvasGroup boardGroup; Transform keysRoot; GameObject template, templateEnter, templateShift; Text title, note; Font arrowFont;
    bool thoughtWant; float thoughtAlpha; string thoughtText = ""; bool thoughtRight = true;
    float bobPhase; Vector3 baseScale; Vector2 boardPx; Image paper;

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
            var tt = board.transform.Find("Title"); title = tt != null ? tt.GetComponent<Text>() : null;
            var nt = board.transform.Find("Note"); note = nt != null ? nt.GetComponent<Text>() : null; if (note != null) note.text = "";
            var canvas = board.GetComponent<Canvas>(); if (canvas != null) canvas.worldCamera = Camera.main;
            arrowFont = Resources.Load<Font>("Fonts/NanumGothic-Regular");
            board.SetActive(false);
        }
        else if (!menuScene) Debug.LogWarning("[KeyTutorial] boardPrefab 없음 — 튜토리얼 표지판 생략");
    }

    void OnDestroy() { if (board != null) Destroy(board); }

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

    bool HasCap(string group) { foreach (var c in caps) if (c.group == group && !c.done) return true; return false; }
    void DismissGroup(string group) { foreach (var c in caps) if (c.group == group && !c.done) { c.done = true; c.doneT = Time.time; } }

    void Update()
    {
        if (menuScene || board == null) return;
        bool ok = Controllable();
        if (!ok) readyT = -1f; else if (readyT < 0f) readyT = Time.time;
        bool ready = ok && Time.time - readyT >= appearDelay;

        // 그룹 조건 → 등장(1회)
        if (ready)
        {
            if (showMove && PlayerPrefs.GetInt(MovePref, 0) == 0) AddGroup("move", L.T("걷기", "walk"), () => {
                for (int i = 0; i < 4; i++) if (Mine(i)) { for (int k = 0; k < 4; k++) Add(Map[i][k], "move", LegPalette.BySlot[i], true); }   // 자판 그림에 없는 키(방향키)는 자동 생략
                foreach (var lc in transform.root.GetComponentsInChildren<ArtLegController>(true)) { int i = (int)lc.controlSet; if (i < 0 || i > 3 || !Mine(i)) continue; foreach (var ak in new[] { lc.altUp, lc.altDown, lc.altLeft, lc.altRight }) if (ak != Key.None) Add(ak, "move", LegPalette.BySlot[i], true); }
            });
            bool squidOn = squid != null && squid.active;   // ★2026-09-08 우주오징어 중엔 기린·얼음 키 안내 없음(상호 배타)
            if (showGiraffe && giraffe != null && !giraffe.locked && !squidOn) AddGroup("giraffe", L.T("2 = 기린의 힘", "2 = giraffe power"), () => Add(Key.Digit2, "giraffe", Color.white, false));
            if (showStretch && showGiraffe && giraffe != null && giraffe.active && !giraffeKeysShown)
            {
                giraffeKeysShown = true;
                AddGroup("stretch", L.T("다리 늘리기 / 줄이기", "stretch / shrink legs"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) { Add(Map[i][4], "stretch", LegPalette.BySlot[i], true); Add(Map[i][5], "stretch", LegPalette.BySlot[i], true); } Add(Key.Equals, "stretch", Color.white, false); Add(Key.Minus, "stretch", Color.white, false); });
            }
            if (showIce && ice != null && ice.unlocked && !squidOn) AddGroup("ice", L.T("3 = 얼음", "3 = ice"), () => Add(Key.Digit3, "ice", Color.white, false));
            if (showTennis && tennis != null && (tennis.unlocked || tennis.active)) AddGroup("tennis", L.T("붙기 / 떼기", "stick / let go"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) { Add(GrabKeys[i], "tennis", LegPalette.BySlot[i], true); Add(ReleaseKeys[i], "tennis", LegPalette.BySlot[i], true); } });
            if (showSquid && squid != null && squid.active && !squid.thrustLocked) AddGroup("squid", L.T("발끝 분사", "thrust from feet"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) Add(Map[i][4], "squid", LegPalette.BySlot[i], true); });
        }

        // 뒤집힘 → 상상 말풍선(+ 남은 횟수가 있으면 '1' 키캡). 벽 등반·우주는 ArtSceneReset.FlipPromptActive가 false
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
        if (flip && canReset && !HasCap("pose")) { groupHint["pose"] = L.T("1 = 바로서기", "1 = stand up"); Add(Key.Digit1, "pose", Color.white, false); }
        if (!flip && HasCap("pose")) foreach (var c in caps) if (c.group == "pose" && !c.done) { c.done = true; c.doneT = Time.time; }   // 스스로 일어났거나 상황 종료 → 지움

        // ★v7 능력이 '활성화'되면(키를 누르든 컷씬/멀티 입력이든) 그 능력의 키캡 그룹 전체가 사라짐 — 걷기 키는 제외(키별)
        if (giraffe != null && giraffe.active) DismissGroup("giraffe");
        if (giraffe != null && giraffe.AnyStretched) DismissGroup("stretch");
        if (ice != null && ice.active) DismissGroup("ice");
        if (tennis != null && tennis.StuckMask != 0) DismissGroup("tennis");
        if (squid != null && squid.JetMask != 0) DismissGroup("squid");

        // 키 입력 → 해당 키캡 사라짐(팝+페이드)
        var kb = Keyboard.current;
        float dt = Time.deltaTime;
        for (int i = caps.Count - 1; i >= 0; i--)
        {
            var c = caps[i];
            if (!c.done)
            {
                c.alpha = Mathf.MoveTowards(c.alpha, 1f, dt / fadeSeconds);
                if (ok && kb != null && kb[c.key].wasPressedThisFrame)
                {
                    c.done = true; c.doneT = Time.time;
                    if (c.group == "move") OnMovePressed();
                    else DismissGroup(c.group);   // ★v7 능력 키는 하나만 눌러도(=능력 발동) 그룹 전체가 사라짐(걷기만 키별)
                }
                if (c.cg != null) { c.cg.alpha = c.alpha; c.rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, c.alpha); }
            }
            else
            {
                float e = Mathf.Clamp01((Time.time - c.doneT) / fadeSeconds);
                if (c.cg != null) { c.cg.alpha = 1f - e; c.rt.localScale = Vector3.one * (1f + 0.35f * e); }
                if (e >= 1f) { if (c.go != null) Destroy(c.go); caps.RemoveAt(i); }
            }
        }

        // ★v7 걷기 튜토리얼 완료(떴던 걷기 키캡을 전부 누름) → 저장. 이후 어느 맵에서도 걷기 키는 안 뜨고 능력 키만 뜬다
        if (shown.Contains("move") && !HasCap("move") && PlayerPrefs.GetInt(MovePref, 0) == 0) { PlayerPrefs.SetInt(MovePref, 1); PlayerPrefs.Save(); }

        // 제목 = 떠 있는 그룹들의 힌트
        if (title != null)
        {
            var parts = new List<string>();
            foreach (var kv in groupHint) if (HasCap(kv.Key)) parts.Add(kv.Value);
            string s = string.Join("   ·   ", parts); if (title.text != s) title.text = s;
        }

        // 표지판 전체 페이드(조작 가능 && 키캡 있음)
        bool want = ok && caps.Count > 0;
        if (want && !board.activeSelf) { board.SetActive(true); PoseBoard(); }
        if (board.activeSelf)
        {
            boardGroup.alpha = Mathf.MoveTowards(boardGroup.alpha, want ? 1f : 0f, dt / boardFadeSeconds);
            if (!want && boardGroup.alpha <= 0f) board.SetActive(false);
        }
        // 말풍선 페이드(그리기는 OnGUI)
        thoughtAlpha = Mathf.MoveTowards(thoughtAlpha, thoughtWant && ok ? 1f : 0f, dt / boardFadeSeconds);
    }

    /// <summary>첫 튜토리얼: 걷기 키를 처음 누르면 다리 색 표시를 켜고 설정 안내(1회).</summary>
    void OnMovePressed()
    {
        if (moveGroupPressed) return; moveGroupPressed = true;
        if (!legColorTutorial || legColorShown || PlayerPrefs.GetInt(LegColorPref, 0) == 1) return;
        legColorShown = true; PlayerPrefs.SetInt(LegColorPref, 1); PlayerPrefs.Save();
        if (xray != null && !xray.IsOn) xray.SetOn(true);
        if (note != null) note.text = L.T("다리마다 자기 색이 켜졌어요 — 키 색과 같아요. ESC 설정에서 끌 수 있어요.", "each leg now shows its color — same as the keys. turn it off in ESC settings.");
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
        cy = Mathf.Clamp(cy, 0.04f + hhVp, 0.96f - hhVp);
        Vector3 pos = cam.ViewportToWorldPoint(new Vector3(cx, cy, depth));
        board.transform.SetPositionAndRotation(pos, cam.transform.rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * 0.8f));
    }

    void AddGroup(string name, string hint, System.Action fill)
    {
        if (shown.Contains(name)) return;
        shown.Add(name); groupHint[name] = hint; fill();
    }

    void Add(Key key, string group, Color tint, bool hasTint)
    {
        if (template == null || keysRoot == null) return;
        foreach (var c in caps) if (c.key == key && !c.done) return;   // 이미 떠 있음(다른 그룹이 같은 키)
        var slot = keysRoot.Find("Key_" + key.ToString()); if (slot == null) return;   // 자판 그림에 없는 키(방향키 등)
        var srt = slot.GetComponent<RectTransform>();
        var tplGo = srt.sizeDelta.x > 250f ? templateShift : srt.sizeDelta.x > 150f ? templateEnter : template;
        var go = Instantiate(tplGo, keysRoot); go.name = "Cap_" + key; go.SetActive(true);
        var rt = go.GetComponent<RectTransform>(); var trt = tplGo.GetComponent<RectTransform>();
        float pad = (trt.sizeDelta.y - srt.sizeDelta.y) * 0.5f;   // 템플릿 여백(그림자)만큼 바깥으로
        rt.anchoredPosition = srt.anchoredPosition + new Vector2(-pad, pad); rt.sizeDelta = trt.sizeDelta; rt.SetAsLastSibling();
        var lbl = go.transform.Find("Label"); if (lbl != null) { var tx = lbl.GetComponent<Text>(); tx.text = LabelOf(key); if ((IsArrow(key) || key == Key.Digit1 || key == Key.Digit2 || key == Key.Digit3) && arrowFont != null) tx.font = arrowFont; /* 화살표·숫자는 고딕(개구체 1이 I처럼 보임) */ if (tx.text.Length > 2) tx.fontSize = Mathf.RoundToInt(tx.fontSize * 0.62f); }
        var bar = go.transform.Find("Bar"); if (bar != null) { if (hasTint) { var im = bar.GetComponent<Image>(); var col = tint; col.a = 0.9f; im.color = col; } else bar.gameObject.SetActive(false); }
        var cg = go.GetComponent<CanvasGroup>(); if (cg == null) cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f;
        rt.localScale = Vector3.one * 0.6f;
        caps.Add(new Cap { key = key, group = group, go = go, cg = cg, rt = rt, alpha = 0f });
    }

    /// <summary>검증용: 키를 누른 것으로 처리.</summary>
    public void DebugPress(Key key) { foreach (var c in caps) if (c.key == key && !c.done) { c.done = true; c.doneT = Time.time; if (c.group == "move") OnMovePressed(); } }
    public int VisibleCount { get { int n = 0; foreach (var c in caps) if (!c.done) n++; return n; } }
    public bool BoardVisible => board != null && board.activeSelf && boardGroup != null && boardGroup.alpha > 0.5f;
    public bool ThoughtVisible => thoughtAlpha > 0.5f;
    public string ThoughtText => thoughtText;
}
