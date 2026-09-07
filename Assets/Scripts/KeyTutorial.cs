using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★2026-09-07 떠 있는 키캡 튜토리얼(사용자 요청: "조작법 페이지 전부 없애고, 테이블 옆에 눌러야 하는 자판만 떠 있게 —
/// 정사각 키캡에 키 이름, 그 키를 누르면 사라짐, 모든 능력이 같은 방식"). Table.prefab 루트 부착(전 씬).
/// - 실제 키보드 배열(QWERTY 좌표) 위에 '지금 눌러야 하는 키'만 키캡으로 그린다(IMGUI, 테이블 옆 월드 앵커를 화면 투영, 거리 비례 크기).
/// - 그룹은 조건이 처음 참이 될 때 나타남: 걷기(내 다리 4방향 키, 다리 색) · 바로서기 '1'(넘어지면 · 3번까지) · 기린 '2'→신축 키 · 얼음 '3' · 테니스 붙기/떼기 · 오징어 분사.
///   키를 누르면 그 키캡만 팝+페이드로 사라짐. 컷씬(CameraFollow 꺼짐)·일시정지·동결 중엔 숨김(사라지진 않음).
/// - 멀티: 내 다리만(NetLegLink.IsMine / 호스트=빈 자리 포함), 오프라인은 TableInput.IsLocal. 1인1테이블도 내 테이블만.
/// - '1'은 서 있는 채로 눌러 지우면 충전을 돌려줌(튜토리얼 때문에 한 번을 낭비하지 않게) — 넘어져서 누르면 정상 소모.
/// ⚠️키 목록은 TableInput.Map / TennisMode Grab·Release / GiraffeMode Longer·Shorter와 같아야 함(여기 복제 — 바꾸면 같이).
/// </summary>
public class KeyTutorial : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("키캡 한 변(px, 기준 거리에서)")] public float capSize = 54f;
    public float capGap = 6f;
    [Tooltip("이 거리(m)에서 capSize — 멀면 작아짐")] public float refDistance = 6f;
    public float minScale = 0.6f, maxScale = 1.15f;
    [Tooltip("키보드 앵커: 테이블 기준 카메라 오른쪽(x)·위(y)·앞(z)")] public Vector3 anchorOffset = new Vector3(1.6f, 1.15f, 0f);
    [Tooltip("조작 가능해진 뒤 이만큼 있다가 등장")] public float appearDelay = 0.7f;
    public float fadeSeconds = 0.35f;
    [Tooltip("키캡 텍스처(Resources/HandUI/keycap) — 비면 종이 상자 폴백")] public Texture2D keycapTex;
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

    // ── QWERTY 좌표(행, 열) ──
    static readonly Dictionary<Key, Vector2> Layout = new Dictionary<Key, Vector2>
    {
        { Key.Digit1, new Vector2(1f, 0) }, { Key.Digit2, new Vector2(2f, 0) }, { Key.Digit3, new Vector2(3f, 0) }, { Key.Minus, new Vector2(11f, 0) }, { Key.Equals, new Vector2(12f, 0) },
        { Key.Q, new Vector2(1.5f, 1) }, { Key.W, new Vector2(2.5f, 1) }, { Key.E, new Vector2(3.5f, 1) }, { Key.R, new Vector2(4.5f, 1) }, { Key.T, new Vector2(5.5f, 1) }, { Key.Y, new Vector2(6.5f, 1) }, { Key.U, new Vector2(7.5f, 1) }, { Key.I, new Vector2(8.5f, 1) }, { Key.O, new Vector2(9.5f, 1) }, { Key.P, new Vector2(10.5f, 1) }, { Key.LeftBracket, new Vector2(11.5f, 1) }, { Key.RightBracket, new Vector2(12.5f, 1) },
        { Key.A, new Vector2(1.75f, 2) }, { Key.S, new Vector2(2.75f, 2) }, { Key.D, new Vector2(3.75f, 2) }, { Key.F, new Vector2(4.75f, 2) }, { Key.G, new Vector2(5.75f, 2) }, { Key.H, new Vector2(6.75f, 2) }, { Key.J, new Vector2(7.75f, 2) }, { Key.K, new Vector2(8.75f, 2) }, { Key.L, new Vector2(9.75f, 2) },
        { Key.Z, new Vector2(2.25f, 3) }, { Key.X, new Vector2(3.25f, 3) }, { Key.C, new Vector2(4.25f, 3) }, { Key.V, new Vector2(5.25f, 3) }, { Key.B, new Vector2(6.25f, 3) }, { Key.N, new Vector2(7.25f, 3) }, { Key.M, new Vector2(8.25f, 3) }, { Key.Comma, new Vector2(9.25f, 3) }, { Key.Period, new Vector2(10.25f, 3) }, { Key.Slash, new Vector2(11.25f, 3) }, { Key.RightShift, new Vector2(12.4f, 3) },
        { Key.UpArrow, new Vector2(15.2f, 2) }, { Key.LeftArrow, new Vector2(14.2f, 3) }, { Key.DownArrow, new Vector2(15.2f, 3) }, { Key.RightArrow, new Vector2(16.2f, 3) },
    };

    static string LabelOf(Key k)
    {
        switch (k)
        {
            case Key.UpArrow: return "↑"; case Key.DownArrow: return "↓"; case Key.LeftArrow: return "←"; case Key.RightArrow: return "→";
            case Key.RightBracket: return "]"; case Key.LeftBracket: return "["; case Key.Minus: return "-"; case Key.Equals: return "=";
            case Key.Period: return "."; case Key.Slash: return "/"; case Key.Comma: return ","; case Key.RightShift: return "Shift";
            case Key.Digit1: return "1"; case Key.Digit2: return "2"; case Key.Digit3: return "3";
            default: return k.ToString();
        }
    }

    class Cap
    {
        public Key key; public string label; public Vector2 pos; public Color tint; public bool hasTint; public string group;
        public float alpha; public bool done; public float doneT;
    }

    readonly List<Cap> caps = new List<Cap>();
    readonly HashSet<string> shown = new HashSet<string>();   // 이미 등장한 그룹
    readonly Dictionary<string, string> groupHint = new Dictionary<string, string>();

    ArticulationBody root; TableInput ti; NetLegLink link; GiraffeMode giraffe; IcePower ice; TennisMode tennis; SquidMode squid; ArtSceneReset asr;
    CameraFollow camFollow; bool menuScene; float readyT = -1f; bool giraffeKeysShown;
    GUIStyle capStyle, arrowStyle, hintStyle; int capStyleSize = -1;

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
        if (keycapTex == null) keycapTex = Resources.Load<Texture2D>("HandUI/keycap");
    }

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
        if (menuScene) return;
        bool ok = Controllable();
        if (!ok) { readyT = -1f; return; }
        if (readyT < 0f) readyT = Time.time;
        bool ready = Time.time - readyT >= appearDelay;

        // 그룹 조건 → 등장
        if (ready)
        {
            if (showMove) AddGroup("move", L.T("걷기", "walk"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) for (int k = 0; k < 4; k++) Add(Map[i][k], "move", LegPalette.BySlot[i]); });
            if (showPoseReset) AddGroup("pose", ArtSceneReset.Unlimited ? L.T("넘어지면 1 (무제한)", "fell over? press 1 (unlimited)") : L.T("넘어지면 1 · 맵마다 3번", "fell over? press 1 · 3 per map"), () => Add(Key.Digit1, "pose", Color.white, false));
            if (showGiraffe && giraffe != null && !giraffe.locked) AddGroup("giraffe", L.T("2 = 기린의 힘", "2 = giraffe power"), () => Add(Key.Digit2, "giraffe", Color.white, false));
            if (showGiraffe && giraffe != null && giraffe.active && !giraffeKeysShown)
            {
                giraffeKeysShown = true;
                AddGroup("stretch", L.T("다리 늘리기 / 줄이기", "stretch / shrink legs"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) { Add(Map[i][4], "stretch", LegPalette.BySlot[i]); Add(Map[i][5], "stretch", LegPalette.BySlot[i]); } Add(Key.Equals, "stretch", Color.white, false); Add(Key.Minus, "stretch", Color.white, false); });
            }
            if (showIce && ice != null && ice.unlocked) AddGroup("ice", L.T("3 = 얼음", "3 = ice"), () => Add(Key.Digit3, "ice", Color.white, false));
            if (showTennis && tennis != null && (tennis.unlocked || tennis.active)) AddGroup("tennis", L.T("붙기 / 떼기", "stick / let go"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) { Add(GrabKeys[i], "tennis", LegPalette.BySlot[i]); Add(ReleaseKeys[i], "tennis", LegPalette.BySlot[i]); } });
            if (showSquid && squid != null && squid.active && !squid.thrustLocked) AddGroup("squid", L.T("발끝 분사", "thrust from feet"), () => { for (int i = 0; i < 4; i++) if (Mine(i)) Add(Map[i][4], "squid", LegPalette.BySlot[i]); });
        }

        // 키 입력 → 해당 키캡 사라짐
        var kb = Keyboard.current;
        float dt = Time.deltaTime;
        for (int i = caps.Count - 1; i >= 0; i--)
        {
            var c = caps[i];
            if (!c.done)
            {
                c.alpha = Mathf.MoveTowards(c.alpha, 1f, dt / fadeSeconds);
                if (kb != null && kb[c.key].wasPressedThisFrame)
                {
                    c.done = true; c.doneT = Time.time;
                    if (c.group == "pose" && asr != null && root != null && root.transform.up.y > 0.8f && !ArtSceneReset.Unlimited)
                    { asr.AddPoseResets(1); ArtSceneReset.PoseResetCount = Mathf.Max(0, ArtSceneReset.PoseResetCount - 1); }   // 서 있는 채 눌러 지운 것 — 충전 반환
                }
            }
            else if (Time.time - c.doneT > fadeSeconds) caps.RemoveAt(i);
        }
    }

    void AddGroup(string name, string hint, System.Action fill)
    {
        if (shown.Contains(name)) return;
        shown.Add(name); groupHint[name] = hint; fill();
    }

    void Add(Key key, string group, Color tint, bool hasTint = true)
    {
        if (!Layout.TryGetValue(key, out var pos)) return;
        foreach (var c in caps) if (c.key == key && !c.done) return;   // 이미 떠 있음(다른 그룹이 같은 키)
        caps.Add(new Cap { key = key, label = LabelOf(key), pos = pos, tint = tint, hasTint = hasTint, group = group, alpha = 0f });
    }

    /// <summary>검증용: 키를 누른 것으로 처리.</summary>
    public void DebugPress(Key key) { foreach (var c in caps) if (c.key == key && !c.done) { c.done = true; c.doneT = Time.time; } }
    public int VisibleCount { get { int n = 0; foreach (var c in caps) if (!c.done) n++; return n; } }

    void OnGUI()
    {
        if (menuScene || caps.Count == 0 || !Controllable()) return;
        var cam = Camera.main; if (cam == null || root == null) return;
        UiKit.ApplyKoreanFont();
        Vector3 tp = root.transform.position;
        Vector3 anchor = tp + cam.transform.right * anchorOffset.x + Vector3.up * anchorOffset.y + cam.transform.forward * anchorOffset.z;
        Vector3 sp = cam.WorldToScreenPoint(anchor);
        if (sp.z <= 0.1f) return;
        float dist = Vector3.Distance(cam.transform.position, anchor);
        float scale = Mathf.Clamp(refDistance / Mathf.Max(0.5f, dist), minScale, maxScale) * (Screen.height / 1080f);
        float pitch = (capSize + capGap) * scale, size = capSize * scale;

        // 떠 있는 키들의 배열 범위 → 클러스터 크기(사라진 키도 자리는 유지 — 키캡이 밀리지 않게)
        float minC = float.MaxValue, maxC = float.MinValue, minR = float.MaxValue, maxR = float.MinValue;
        foreach (var c in caps) { minC = Mathf.Min(minC, c.pos.x); maxC = Mathf.Max(maxC, c.pos.x); minR = Mathf.Min(minR, c.pos.y); maxR = Mathf.Max(maxR, c.pos.y); }
        float w = (maxC - minC) * pitch + size, h = (maxR - minR) * pitch + size;
        float x0 = sp.x, y0 = Screen.height - sp.y - h * 0.5f;
        x0 = Mathf.Clamp(x0, 12f, Screen.width - w - 12f); y0 = Mathf.Clamp(y0, 12f + 30f * scale, Screen.height - h - 12f);

        int fs = Mathf.RoundToInt(22f * scale);
        if (capStyle == null || capStyleSize != fs)
        {
            capStyleSize = fs;
            capStyle = HandUI.Style(fs, HandUI.Ink);
            arrowStyle = new GUIStyle(capStyle); arrowStyle.font = null; arrowStyle.fontSize = Mathf.RoundToInt(fs * 1.05f);   // 화살표 글리프는 한글 폰트(나눔고딕)에 있음
            hintStyle = HandUI.Style(Mathf.RoundToInt(19f * scale), HandUI.Ink, TextAnchor.MiddleLeft);
        }
        var prev = GUI.color;
        foreach (var c in caps)
        {
            float a = c.done ? 1f - Mathf.Clamp01((Time.time - c.doneT) / fadeSeconds) : c.alpha;
            if (a <= 0.01f) continue;
            float pop = c.done ? 1f + 0.25f * (1f - a) : 1f;
            float s = size * pop;
            var r = new Rect(x0 + (c.pos.x - minC) * pitch + (size - s) * 0.5f, y0 + (c.pos.y - minR) * pitch + (size - s) * 0.5f, s, s);
            GUI.color = new Color(1f, 1f, 1f, a);
            if (keycapTex != null) GUI.DrawTexture(r, keycapTex, ScaleMode.StretchToFill, true);
            else { UiKit.Fill(r, new Color(1f, 1f, 1f, 0.96f)); UiKit.Fill(new Rect(r.x, r.y, r.width, 2f), Color.black); UiKit.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), Color.black); UiKit.Fill(new Rect(r.x, r.y, 2f, r.height), Color.black); UiKit.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), Color.black); }
            if (c.hasTint) { var t = c.tint; t.a = 0.9f * a; UiKit.Fill(new Rect(r.x + s * 0.16f, r.yMax - s * 0.2f, s * 0.68f, s * 0.07f), t); }   // 다리 색 띠
            bool arrow = c.key == Key.UpArrow || c.key == Key.DownArrow || c.key == Key.LeftArrow || c.key == Key.RightArrow;
            var st = arrow ? arrowStyle : capStyle;
            if (c.label.Length > 2) { var small = new GUIStyle(st); small.fontSize = Mathf.RoundToInt(fs * 0.55f); GUI.Label(new Rect(r.x, r.y - s * 0.04f, r.width, r.height), c.label, small); }
            else GUI.Label(new Rect(r.x, r.y - s * 0.04f, r.width, r.height), c.label, st);
        }
        // 힌트 한 줄(떠 있는 그룹들)
        var parts = new List<string>();
        foreach (var kv in groupHint) { bool any = false; foreach (var c in caps) if (c.group == kv.Key && !c.done) { any = true; break; } if (any) parts.Add(kv.Value); }
        if (parts.Count > 0) { GUI.color = new Color(1f, 1f, 1f, 1f); GUI.Label(new Rect(x0, y0 - 30f * scale, Mathf.Max(w, 520f * scale), 28f * scale), string.Join("  ·  ", parts), hintStyle); }
        GUI.color = prev;
    }
}
