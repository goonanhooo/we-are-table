using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ★2026-09-07 떠 있는 키캡 튜토리얼(사용자 요청: "조작법 페이지 전부 없애고, 테이블 옆에 눌러야 하는 자판만 떠 있게 —
/// 정사각 키캡에 키 이름, 그 키를 누르면 사라짐, 모든 능력이 같은 방식" → v2 "나머지 자판도 선으로, 실제 3D 공중 표지판처럼"). Table.prefab 루트 부착(전 씬).
/// - `boardPrefab`(Assets/Prefabs/KeyTutorialBoard — Tools/UI/Build Key Tutorial Board로 생성) = **월드 캔버스 종이 표지판**: 전체 QWERTY 자판이 선(outline)으로 그려져 있고,
///   지금 눌러야 하는 키만 그 자리에 채운 키캡(KeyTemplate 복제: 글자 + 다리 색 띠)이 올라간다. 표지판은 테이블 옆(카메라 오른쪽·위)에 떠서 카메라를 향해 서고 살짝 둥실거린다.
///   재질은 ZTest Always(벽에 안 가림)지만 원근·빌보드라 3D 공간에 떠 있는 표지판처럼 보임.
/// - 그룹은 조건이 처음 참이 될 때 나타남: 걷기(내 다리 4방향 키, 다리 색) · 바로서기 '1'(넘어지면 · 3번까지) · 기린 '2'→신축 키 · 얼음 '3' · 테니스 붙기/떼기 · 오징어 분사.
///   키를 누르면 그 키캡만 팝+페이드로 사라짐(선 자판은 남음). 모든 키캡이 사라지면 표지판도 사라짐. 컷씬(CameraFollow 꺼짐)·일시정지·동결 중엔 표지판 페이드 아웃.
/// - 멀티: 내 다리만(NetLegLink.IsMine / 호스트=빈 자리 포함), 오프라인은 TableInput.IsLocal. 1인1테이블도 내 테이블만.
/// - '1'은 서 있는 채로 눌러 지우면 충전을 돌려줌(튜토리얼 때문에 한 번을 낭비하지 않게) — 넘어져서 누르면 정상 소모.
/// ⚠️키 목록은 TableInput.Map / TennisMode Grab·Release / GiraffeMode Longer·Shorter와 같아야 함(여기 복제 — 바꾸면 같이).
/// </summary>
public class KeyTutorial : MonoBehaviour
{
    [Header("표지판")]
    [Tooltip("월드 캔버스 표지판 프리팹(KeyTutorialBoard)")] public GameObject boardPrefab;
    [Tooltip("표지판 왼쪽 가장자리가 테이블 중심에서 카메라 오른쪽으로 떨어진 거리(m)")] public float sideGap = 1.1f;
    [Tooltip("표지판 아래 가장자리 높이(테이블 루트 기준 m)")] public float bottomHeight = 1.15f;
    [Tooltip("카메라 쪽으로 당기는 거리(m)")] public float towardCamera = 0.8f;
    [Tooltip("표지판이 화면 너비에서 차지할 비율 — 거리에 맞춰 크기를 조절(튜토리얼이라 항상 읽히게)")] public float screenWidthFraction = 0.42f;
    [Tooltip("거리 보정 배율 범위(프리팹 기본 크기 기준)")] public float minScaleMul = 0.7f, maxScaleMul = 2.4f;
    [Tooltip("둥실거림 진폭(m)/속도")] public float bobAmp = 0.03f, bobSpeed = 1.3f;
    [Tooltip("카메라를 향할 때 위아래 기울기 비율(0=완전 직립)")] public float pitchFollow = 0.35f;
    [Tooltip("조작 가능해진 뒤 이만큼 있다가 등장")] public float appearDelay = 0.7f;
    public float fadeSeconds = 0.35f;
    public float boardFadeSeconds = 0.5f;
    [Header("그룹 켜기")]
    public bool showMove = true, showPoseReset = true, showGiraffe = true, showIce = true, showTennis = true, showSquid = true;

    // ── 키 배열(TableInput.Map과 동일) ──
    static readonly Key[][] Map = {
        new[] { Key.W, Key.S, Key.A, Key.D, Key.E, Key.Q },
        new[] { Key.T, Key.G, Key.F, Key.H, Key.Y, Key.R },
        new[] { Key.I, Key.K, Key.J, Key.L, Key.O, Key.U },
        new[] { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.RightBracket, Key.P },
    };
    static readonly Key[] GrabKeys = { Key.C, Key.N, Key.Period, Key.RightShift };
    static readonly Key[] ReleaseKeys = { Key.Z, Key.V, Key.M, Key.Slash };

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
    readonly HashSet<string> shown = new HashSet<string>();   // 이미 등장한 그룹
    readonly Dictionary<string, string> groupHint = new Dictionary<string, string>();

    ArticulationBody root; TableInput ti; NetLegLink link; GiraffeMode giraffe; IcePower ice; TennisMode tennis; SquidMode squid; ArtSceneReset asr;
    CameraFollow camFollow; bool menuScene; float readyT = -1f; bool giraffeKeysShown;
    GameObject board; CanvasGroup boardGroup; Transform keysRoot; GameObject template, templateEnter, templateShift; Text title; Font arrowFont;
    float bobPhase; Vector3 baseScale; Vector2 boardPx;

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
            var tt = board.transform.Find("Title"); title = tt != null ? tt.GetComponent<Text>() : null;
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

    void Update()
    {
        if (menuScene || board == null) return;
        bool ok = Controllable();
        if (!ok) readyT = -1f; else if (readyT < 0f) readyT = Time.time;
        bool ready = ok && Time.time - readyT >= appearDelay;

        // 그룹 조건 → 등장
        if (ready)
        {
            if (showMove) AddGroup("move", L.T("걷기", "walk"), () => {
                for (int i = 0; i < 4; i++) if (Mine(i)) { for (int k = 0; k < 4; k++) Add(Map[i][k], "move", LegPalette.BySlot[i], true); }
                // 보조 키(방향키 다리의 [ ' ; Enter 등 — ArtLegController에 등록된 것)도 같은 색으로
                foreach (var lc in transform.root.GetComponentsInChildren<ArtLegController>(true)) { int i = (int)lc.controlSet; if (i < 0 || i > 3 || !Mine(i)) continue; foreach (var ak in new[] { lc.altUp, lc.altDown, lc.altLeft, lc.altRight }) if (ak != Key.None) Add(ak, "move", LegPalette.BySlot[i], true); }
            });
            if (showPoseReset) AddGroup("pose", ArtSceneReset.Unlimited ? L.T("넘어지면 1 (무제한)", "fell over? press 1 (unlimited)") : L.T("넘어지면 1 · 맵마다 3번", "fell over? press 1 · 3 per map"), () => Add(Key.Digit1, "pose", Color.white, false));
            if (showGiraffe && giraffe != null && !giraffe.locked) AddGroup("giraffe", L.T("2 = 기린의 힘", "2 = giraffe power"), () => Add(Key.Digit2, "giraffe", Color.white, false));
            if (showGiraffe && giraffe != null && giraffe.active && !giraffeKeysShown)
            {
                giraffeKeysShown = true;
                AddGroup("stretch", L.T("다리 늘리기 / 줄이기", "stretch / shrink legs"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) { Add(Map[i][4], "stretch", LegPalette.BySlot[i], true); Add(Map[i][5], "stretch", LegPalette.BySlot[i], true); } Add(Key.Equals, "stretch", Color.white, false); Add(Key.Minus, "stretch", Color.white, false); });
            }
            if (showIce && ice != null && ice.unlocked) AddGroup("ice", L.T("3 = 얼음", "3 = ice"), () => Add(Key.Digit3, "ice", Color.white, false));
            if (showTennis && tennis != null && (tennis.unlocked || tennis.active)) AddGroup("tennis", L.T("붙기 / 떼기", "stick / let go"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) { Add(GrabKeys[i], "tennis", LegPalette.BySlot[i], true); Add(ReleaseKeys[i], "tennis", LegPalette.BySlot[i], true); } });
            if (showSquid && squid != null && squid.active && !squid.thrustLocked) AddGroup("squid", L.T("발끝 분사", "thrust from feet"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) Add(Map[i][4], "squid", LegPalette.BySlot[i], true); });
        }

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
                    if (c.group == "pose" && asr != null && root != null && root.transform.up.y > 0.8f && !ArtSceneReset.Unlimited)
                    { asr.AddPoseResets(1); ArtSceneReset.PoseResetCount = Mathf.Max(0, ArtSceneReset.PoseResetCount - 1); }   // 서 있는 채 눌러 지운 것 — 충전 반환
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

        // 제목 = 떠 있는 그룹들의 힌트
        if (title != null)
        {
            var parts = new List<string>();
            foreach (var kv in groupHint) { bool any = false; foreach (var c in caps) if (c.group == kv.Key && !c.done) { any = true; break; } if (any) parts.Add(kv.Value); }
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
    }

    void LateUpdate() { if (board != null && board.activeSelf) PoseBoard(); }

    /// <summary>테이블 옆(카메라 기준 오른쪽·위)에 떠서 카메라를 향해 서고 둥실거림.</summary>
    void PoseBoard()
    {
        var cam = Camera.main; if (cam == null || root == null) return;
        Vector3 tp = root.transform.position;
        Vector3 fwd = cam.transform.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float t = Time.time * bobSpeed + bobPhase;
        // 거리 보정 크기: 화면 너비의 screenWidthFraction을 차지하도록(3D 표지판이되 튜토리얼이라 항상 읽히게)
        Vector3 approx = tp + right * (sideGap + 1.5f) + Vector3.up * (bottomHeight + 0.6f) - fwd * towardCamera;
        float d = Mathf.Max(1f, Vector3.Distance(cam.transform.position, approx));
        float visibleW = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
        float baseW = boardPx.x * baseScale.x; float mul = Mathf.Clamp(visibleW * screenWidthFraction / Mathf.Max(0.01f, baseW), minScaleMul, maxScaleMul);
        board.transform.localScale = baseScale * mul;
        float bw = baseW * mul, bh = boardPx.y * baseScale.y * mul;
        Vector3 pos = tp + right * (sideGap + bw * 0.5f) + Vector3.up * (bottomHeight + bh * 0.5f + Mathf.Sin(t) * bobAmp) - fwd * towardCamera;
        // 화면 가장자리 클램프: 표지판 오른쪽 끝이 화면 밖이면 왼쪽(테이블 위)으로 당김 — 튜토리얼 키가 잘리지 않게
        Vector3 camRight = cam.transform.right;
        Vector3 edgeR = pos + camRight * (bw * 0.5f), edgeL = pos - camRight * (bw * 0.5f);
        Vector3 vpR = cam.WorldToViewportPoint(edgeR), vpL = cam.WorldToViewportPoint(edgeL);
        if (vpR.z > 0f && vpR.x > 0.97f) pos -= camRight * ((vpR.x - 0.97f) * visibleW);
        else if (vpL.z > 0f && vpL.x < 0.03f) pos += camRight * ((0.03f - vpL.x) * visibleW);
        Vector3 dir = pos - cam.transform.position; dir.y *= pitchFollow; if (dir.sqrMagnitude < 1e-4f) dir = fwd;
        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * 1.2f);
        board.transform.SetPositionAndRotation(pos, rot);
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
        var slot = keysRoot.Find("Key_" + key.ToString()); if (slot == null) return;
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
    public void DebugPress(Key key) { foreach (var c in caps) if (c.key == key && !c.done) { c.done = true; c.doneT = Time.time; } }
    public int VisibleCount { get { int n = 0; foreach (var c in caps) if (!c.done) n++; return n; } }
    public bool BoardVisible => board != null && board.activeSelf && boardGroup != null && boardGroup.alpha > 0.5f;
}
