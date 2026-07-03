using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 시작 화면에서 Play 표지판을 누르면 열리는 '레벨 선택' 오버레이.
/// 흐름: ① 스테이지 썸네일 선택 → ② 모드 선택(1인 플레이 / 로컬 멀티 / 온라인 멀티)
///       → ③ 온라인이면 방 만들기(방코드 발급, 선택 맵으로 이동) 또는 방코드 입력 참가(호스트 맵으로 자동 이동).
/// 1인 플레이와 로컬 멀티(한 키보드 4인)는 완전히 동일 — 버튼만 구분.
/// EventSystem 없이 OnGUI로 그리고 클릭 처리(표지판 클릭 방식과 일관).
/// 방코드 타이핑은 New Input System(Keyboard.current)으로 직접 캡처.
/// </summary>
public class LevelSelect : MonoBehaviour
{
    [System.Serializable]
    public class Level { public string label; public string scene; public string thumb; }

    public Level[] levels =
    {
        new Level { label = "시작",      scene = "Hallway",     thumb = "Thumbs/hallway" },
        new Level { label = "정글",      scene = "Jungle",      thumb = "Thumbs/jungle"  },
        new Level { label = "눈 덮인 산", scene = "IceMountain", thumb = "Thumbs/ice"     },
    };

    public bool Showing { get; private set; }

    int step;            // 0=스테이지 선택, 1=모드 선택, 2=온라인 메뉴
    int selIndex = -1;   // 선택된 스테이지
    string codeInput = "";
    bool typing;         // 방코드 입력 중
    bool busy;           // 방 생성/참가 진행 중

    Texture2D[] thumbs;
    GUIStyle labelStyle, titleStyle, backStyle, btnStyle, smallStyle;
    Texture2D white;

    void Start()
    {
        thumbs = new Texture2D[levels.Length];
        for (int i = 0; i < levels.Length; i++)
            thumbs[i] = Resources.Load<Texture2D>(levels[i].thumb);
        white = Texture2D.whiteTexture;
    }

    public void Open() { Showing = true; step = 0; selIndex = -1; typing = false; }
    public void Close() { Showing = false; typing = false; }

    void Update()
    {
        // 방코드 타이핑(온라인 메뉴에서만) — IMGUI TextField 대신 직접 캡처
        if (!Showing || step != 2 || !typing) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        for (Key k = Key.A; k <= Key.Z; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 8) codeInput += k.ToString();
        for (Key k = Key.Digit1; k <= Key.Digit0; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 8) codeInput += (k == Key.Digit0 ? "0" : ((int)k - (int)Key.Digit1 + 1).ToString());
        if (kb.backspaceKey.wasPressedThisFrame && codeInput.Length > 0) codeInput = codeInput.Substring(0, codeInput.Length - 1);
        if (kb.enterKey.wasPressedThisFrame && codeInput.Length >= 4) JoinRoom();
    }

    // 온라인: 방 만들기 → 성공 시 선택한 맵으로(세션 상태라 게스트도 자동 동행)
    async void HostAndLoad()
    {
        if (busy || selIndex < 0) return;
        busy = true;
        bool ok = await NetSession.HostAsync();
        busy = false;
        if (ok)
        {
            Time.timeScale = 1f;
            Close();
            NetScene.Load(levels[selIndex].scene);
        }
    }

    // 온라인: 방코드 참가 → 성공 시 NGO가 호스트의 씬으로 자동 전환
    async void JoinRoom()
    {
        if (busy || codeInput.Length < 4) return;
        busy = true;
        typing = false;
        bool ok = await NetSession.JoinAsync(codeInput);
        busy = false;
        if (ok) Close();
    }

    void EnsureStyles()
    {
        if (labelStyle != null) return;
        Color ink = new Color(0.08f, 0.08f, 0.08f, 1f);
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        labelStyle.normal.textColor = ink;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = ink;
        backStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        backStyle.normal.textColor = ink;
        btnStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btnStyle.normal.textColor = ink;
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        smallStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    }

    void Rect2(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = p; }

    /// <summary>흰 배경+검은 테두리+검은 글씨 버튼(클릭은 투명 GUI.Button). disabled 시 흐리게.</summary>
    bool WhiteBtn(Rect r, string text, GUIStyle style, bool enabled = true)
    {
        UiKit.Panel(r);
        bool hover = r.Contains(Event.current.mousePosition);
        if (!enabled) UiKit.Fill(r, new Color(1f, 1f, 1f, 0.55f));
        else if (hover) UiKit.Fill(r, new Color(0f, 0f, 0f, 0.10f));
        GUI.Label(r, text, style);
        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    /// <summary>제목/안내문 뒤에 흰 칩을 깔고 글자를 그린다(어두운 딤 위 가독성).</summary>
    void ChipLabel(float y, float w, float h, string text, GUIStyle style)
    {
        var r = UiKit.Chip(Screen.width * 0.5f, y, w, h);
        GUI.Label(r, text, style);
    }

    void OnGUI()
    {
        if (!Showing) return;
        EnsureStyles();

        // 살짝 어둡게 깔기(흰 칩/버튼이 도드라지게)
        Rect2(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));

        // ESC = 한 단계 뒤로
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            if (typing) typing = false;
            else if (step > 0) step--;
            else Close();
            return;
        }

        if (step == 0) DrawStageSelect();
        else if (step == 1) DrawModeSelect();
        else DrawOnlineMenu();
    }

    // ── ① 스테이지 선택(기존 UI) ──
    void DrawStageSelect()
    {
        ChipLabel(Screen.height * 0.5f - 175f, 340f, 50f, "스테이지 선택", titleStyle);

        int n = levels.Length;
        float gap = 36f;
        float maxW = (Screen.width - gap * (n + 1));
        float tw = Mathf.Min(300f, maxW / n);
        float th = tw * 0.625f;            // 320x200 비율
        float totalW = n * tw + (n - 1) * gap;
        float x0 = (Screen.width - totalW) * 0.5f;
        float y = (Screen.height - th) * 0.5f - 10f;

        for (int i = 0; i < n; i++)
        {
            float x = x0 + i * (tw + gap);
            Rect thumbR = new Rect(x, y, tw, th);
            bool hover = thumbR.Contains(Event.current.mousePosition);

            // 흰 프레임 + 얇은 검은 테두리(hover 시 테두리 두껍게)
            float b = hover ? 4f : 2f;
            Rect2(new Rect(x - 4f - b, y - 4f - b, tw + 8f + b * 2f, th + 8f + b * 2f), Color.black);
            Rect2(new Rect(x - 4f, y - 4f, tw + 8f, th + 8f), Color.white);
            if (thumbs[i] != null) GUI.DrawTexture(thumbR, thumbs[i], ScaleMode.ScaleAndCrop);
            else Rect2(thumbR, new Color(0.92f, 0.92f, 0.92f, 1f));
            var nameChip = UiKit.Chip(x + tw * 0.5f, y + th + 12f, Mathf.Min(tw, 170f), 32f);
            GUI.Label(nameChip, levels[i].label, labelStyle);

            if (GUI.Button(thumbR, GUIContent.none, GUIStyle.none))
            {
                selIndex = i;
                step = 1;   // → 모드 선택
            }
        }

        if (WhiteBtn(new Rect(Screen.width * 0.5f - 60f, y + th + 60f, 120f, 40f), "뒤로", backStyle)) Close();
    }

    // ── ② 모드 선택: 1인 / 로컬 멀티(동일) / 온라인 멀티 ──
    void DrawModeSelect()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        ChipLabel(cy - 170f, 560f, 44f, levels[selIndex].label + " — 어떻게 플레이할까요?", titleStyle);

        float bw = 320f, bh = 56f, gap = 18f;
        float y = cy - 96f;

        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), "1인 플레이", btnStyle)) LoadLocal();
        y += bh + gap;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), "로컬 멀티 (한 키보드 최대 4인)", btnStyle)) LoadLocal();
        y += bh + gap;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), "온라인 멀티 (방 코드)", btnStyle)) { step = 2; codeInput = ""; typing = false; }
        y += bh + gap + 8f;
        if (WhiteBtn(new Rect(cx - 60f, y, 120f, 40f), "뒤로", backStyle)) step = 0;
    }

    void LoadLocal()
    {
        Time.timeScale = 1f;
        Close();
        NetScene.Load(levels[selIndex].scene);
    }

    // ── ③ 온라인 메뉴: 방 만들기 / 코드 참가 ──
    void DrawOnlineMenu()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        ChipLabel(cy - 190f, 480f, 44f, "온라인 멀티 — " + levels[selIndex].label, titleStyle);

        float bw = 340f, bh = 56f;
        float y = cy - 120f;

        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), busy ? "처리 중…" : "방 만들기 (호스트)", btnStyle, !busy)) HostAndLoad();
        var hint1 = UiKit.Chip(cx, y + bh + 6f, 420f, 24f);
        GUI.Label(hint1, "방을 만들면 코드가 발급돼요 — 친구에게 알려주세요", smallStyle);

        y += bh + 52f;
        // 방코드 입력칸(클릭 → 타이핑 모드) — 흰 배경, 입력 중엔 옅은 회색
        Rect codeR = new Rect(cx - bw * 0.5f, y, bw, 46f);
        UiKit.Panel(codeR);
        if (typing) UiKit.Fill(codeR, new Color(0f, 0f, 0f, 0.08f));
        GUI.Label(codeR, codeInput.Length > 0 ? codeInput : (typing ? "코드 입력 중… (엔터=참가)" : "여기 클릭 후 친구의 방 코드 입력"), labelStyle);
        if (GUI.Button(codeR, GUIContent.none, GUIStyle.none)) typing = true;

        y += 54f;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), busy ? "처리 중…" : "참가", btnStyle, !busy && codeInput.Length >= 4)) JoinRoom();

        y += bh + 14f;
        if (!string.IsNullOrEmpty(NetSession.Status))
        {
            var st = UiKit.Chip(cx, y, 420f, 26f);
            GUI.Label(st, NetSession.Status, smallStyle);
        }

        y += 36f;
        if (WhiteBtn(new Rect(cx - 60f, y, 120f, 40f), "뒤로", backStyle)) { step = 1; typing = false; }
    }
}
