using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 시작 화면에서 Play 표지판을 누르면 열리는 오버레이.
/// ★흐름(2026-07-07 개편): ① 모드 선택(1인 / 로컬 멀티 / 방 만들기 / 코드로 참가)이 먼저
///   - 1인·로컬 → ② 스테이지 썸네일 선택 → 즉시 로드(기존과 동일)
///   - 방 만들기 → 방코드 발급 후 '게임에 안 들어가고' 시작 화면에 로비(NetDebugPanel)가 뜸
///     — 로비에서 사람 모으고 다리(여러 개 가능)/맵 고른 뒤 호스트가 [게임 시작]
///   - 코드로 참가 → ② 코드 입력(자동 타이핑) → 참가 성공 시 로비 합류
/// EventSystem 없이 OnGUI로 그리고 클릭 처리(표지판 클릭 방식과 일관).
/// 방코드 타이핑은 New Input System(Keyboard.current)으로 직접 캡처 —
/// ⚠️입력칸은 '클릭해서 포커스' 없이 화면이 열리면 바로 타이핑 활성(클릭 안 먹어 글자가 안 쳐지던 문제 제거),
///   호버 색 전환 없음 + 크고 진한 글자(가독성).
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
        new Level { label = "우주",      scene = "Space",       thumb = "Thumbs/space"   },   // ⚠️씬 인스턴스(StartScreen) 직렬화 배열도 갱신 필요(직렬화 함정)
        new Level { label = "트랙",      scene = "Track",       thumb = "Thumbs/track"   },
        new Level { label = "달",        scene = "Moon",        thumb = "Thumbs/moon"    },
        new Level { label = "모험",      scene = "Adventure",   thumb = "Thumbs/adventure" },
        new Level { label = "등반",      scene = "Climb",       thumb = "Thumbs/climb"   },
    };

    public bool Showing { get; private set; }

    [Tooltip("★맵 선택 미니어처(갤럭시 스타일 회전 섬) — 있으면 스크린샷 대신 라이브 3D 미니어처 표시")]
    public MapMiniatures miniatures;
    [Tooltip("★맵 선택 3D 프리뷰 전환 연출 — 호버=등장/퇴장, 클릭=확정 연출 후 로드")]
    public MapSelectPreview preview;
    int previewHover = -1;
    bool loadPending;

    int step;            // 0=모드 선택, 1=스테이지 선택(1인/로컬), 2=방코드 입력(참가)
    string codeInput = "";
    bool busy;           // 방 생성/참가 진행 중

    Texture2D[] thumbs;
    GUIStyle labelStyle, titleStyle, backStyle, btnStyle, smallStyle, codeStyle;
    Texture2D white;

    void Start()
    {
        thumbs = new Texture2D[levels.Length];
        for (int i = 0; i < levels.Length; i++)
            thumbs[i] = Resources.Load<Texture2D>(levels[i].thumb);
        white = Texture2D.whiteTexture;
    }

    public void Open()
    {
        // 세션 중(로비/인게임)엔 다시 안 연다 — 로비 창(NetDebugPanel)과 겹침 방지
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && (nm.IsHost || nm.IsConnectedClient)) return;
        Showing = true; step = 0; codeInput = "";
    }
    public void Close()
    {
        Showing = false;
        if (preview != null) preview.HideAll();
        previewHover = -1;
    }

    void Update()
    {
        // 방코드 타이핑 — ★참가 화면이 열려 있으면 항상 활성(클릭 포커스 불필요)
        if (!Showing || step != 2 || busy) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        for (Key k = Key.A; k <= Key.Z; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 8) codeInput += k.ToString();
        for (Key k = Key.Digit1; k <= Key.Digit0; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 8) codeInput += (k == Key.Digit0 ? "0" : ((int)k - (int)Key.Digit1 + 1).ToString());
        if (kb.backspaceKey.wasPressedThisFrame && codeInput.Length > 0) codeInput = codeInput.Substring(0, codeInput.Length - 1);
        if (kb.enterKey.wasPressedThisFrame && codeInput.Length >= 4) JoinRoom();
    }

    // 온라인: 방 만들기 → ★게임에 안 들어가고 시작 화면에 로비가 뜸(사람 모은 뒤 로비에서 맵+시작)
    async void HostRoom()
    {
        if (busy) return;
        busy = true;
        NetLegLink.ResetSessionCache();   // 새 세션 — 이전 세션의 배정/시작 캐시 제거
        bool ok = await NetSession.HostAsync();
        busy = false;
        if (ok)
        {
            Time.timeScale = 1f;
            Close();   // 오버레이 닫힘 → NetDebugPanel 로비(중앙 창)가 이어받음
        }
    }

    // 온라인: 방코드 참가 → 성공 시 로비 합류(호스트가 시작하면 NGO가 씬 자동 전환)
    async void JoinRoom()
    {
        if (busy || codeInput.Length < 4) return;
        busy = true;
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
        // 방코드 입력칸 전용: 크고 진한 검은 글자(흰 배경 고정 — 호버/포커스 색 변화 없음)
        codeStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        codeStyle.normal.textColor = Color.black;
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
            if (step > 0) step = 0;
            else Close();
            return;
        }

        if (step == 0) DrawModeSelect();
        else if (step == 1) DrawStageSelect();
        else DrawJoin();

        // 스테이지 선택 화면이 아니면 프리뷰 퇴장
        if (preview != null && step != 1 && Event.current.type == EventType.Repaint)
        {
            preview.HideAll();
            previewHover = -1;
        }
    }

    void OnDisable()
    {
        if (preview != null) preview.HideAll();
        previewHover = -1;
    }

    // ── ② 스테이지 선택(1인/로컬 — 클릭 즉시 로드) ──
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

            // ★호버 = 3D 프리뷰 전환(방향 = 인덱스 증감)
            if (hover && preview != null && !loadPending && i != previewHover)
            {
                int dir = (previewHover >= 0 && i < previewHover) ? -1 : 1;
                preview.Show(i, dir);
                previewHover = i;
            }

            // 흰 프레임 + 얇은 검은 테두리(hover 시 테두리 두껍게)
            float b = hover ? 4f : 2f;
            Rect2(new Rect(x - 4f - b, y - 4f - b, tw + 8f + b * 2f, th + 8f + b * 2f), Color.black);
            Rect2(new Rect(x - 4f, y - 4f, tw + 8f, th + 8f), Color.white);
            // ★미니어처 우선: 살아있는 3D 회전 섬(RT). 없으면 기존 스크린샷 폴백.
            Texture mini = null;
            if (miniatures != null) { miniatures.MarkVisible(); mini = miniatures.RT(i); }
            if (mini != null) GUI.DrawTexture(thumbR, mini, ScaleMode.ScaleAndCrop);
            else if (thumbs[i] != null) GUI.DrawTexture(thumbR, thumbs[i], ScaleMode.ScaleAndCrop);
            else Rect2(thumbR, new Color(0.92f, 0.92f, 0.92f, 1f));
            var nameChip = UiKit.Chip(x + tw * 0.5f, y + th + 12f, Mathf.Min(tw, 170f), 32f);
            GUI.Label(nameChip, levels[i].label, labelStyle);

            // ★클릭 = 확정 연출(펄스→꺼짐) 후 기존 로드. 프리뷰 없으면 즉시 로드(폴백)
            if (GUI.Button(thumbR, GUIContent.none, GUIStyle.none) && !loadPending)
            {
                if (preview != null && !preview.Busy)
                {
                    loadPending = true;
                    int captured = i;
                    preview.Show(i, 1);               // 다른 타일 프리뷰 중이었어도 클릭한 맵으로
                    preview.Confirm(() => { loadPending = false; LoadLocal(captured); });
                }
                else if (preview == null) LoadLocal(i);
            }
        }

        if (WhiteBtn(new Rect(Screen.width * 0.5f - 60f, y + th + 60f, 120f, 40f), "뒤로", backStyle)) step = 0;
    }

    // ── ① 모드 선택(첫 화면): 1인 / 로컬 멀티 / 방 만들기 / 코드 참가 ──
    void DrawModeSelect()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        ChipLabel(cy - 208f, 560f, 44f, "어떻게 플레이할까요?", titleStyle);

        float bw = 360f, bh = 56f, gap = 18f;
        float y = cy - 134f;

        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), "1인 플레이", btnStyle)) step = 1;
        y += bh + gap;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), "로컬 멀티 (한 키보드 최대 4인)", btnStyle)) step = 1;
        y += bh + gap;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), busy ? "방 만드는 중…" : "온라인 — 방 만들기 (호스트)", btnStyle, !busy)) HostRoom();
        var hint = UiKit.Chip(cx, y + bh + 4f, 460f, 24f);
        GUI.Label(hint, "방을 만들면 여기서 코드가 발급되고, 사람을 모은 뒤 게임을 시작해요", smallStyle);
        y += bh + gap + 26f;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), "온라인 — 코드로 참가", btnStyle, !busy)) { step = 2; codeInput = ""; }
        y += bh + gap + 8f;
        if (!string.IsNullOrEmpty(NetSession.Status))
        {
            var st = UiKit.Chip(cx, y, 420f, 26f);
            GUI.Label(st, NetSession.Status, smallStyle);
            y += 34f;
        }
        if (WhiteBtn(new Rect(cx - 60f, y, 120f, 40f), "뒤로", backStyle)) Close();
    }

    void LoadLocal(int i)
    {
        Time.timeScale = 1f;
        Close();
        NetScene.Load(levels[i].scene);
    }

    // ── ③ 코드로 참가: 화면이 열리면 바로 타이핑 활성 ──
    void DrawJoin()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        ChipLabel(cy - 172f, 480f, 44f, "방 코드로 참가", titleStyle);

        float bw = 340f, bh = 56f;
        float y = cy - 104f;

        // ★방코드 입력칸: 흰 배경 고정(호버/포커스 색 전환 없음) + 크고 진한 검은 글자 + 깜빡이 커서.
        //   클릭 포커스 없이 이 화면에선 항상 타이핑 활성(Update가 캡처).
        Rect codeR = new Rect(cx - bw * 0.5f, y, bw, 56f);
        Rect2(new Rect(codeR.x - 2f, codeR.y - 2f, codeR.width + 4f, codeR.height + 4f), Color.black);
        Rect2(codeR, Color.white);
        bool blink = Time.unscaledTime % 1f < 0.55f;
        GUI.Label(codeR, codeInput + (busy ? "" : (blink ? "|" : " ")), codeStyle);
        var hint1 = UiKit.Chip(cx, y + 62f, 420f, 24f);
        GUI.Label(hint1, "친구의 방 코드를 입력하세요 (엔터 = 참가)", smallStyle);

        y += 104f;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), busy ? "참가 중…" : "참가", btnStyle, !busy && codeInput.Length >= 4)) JoinRoom();

        y += bh + 14f;
        if (!string.IsNullOrEmpty(NetSession.Status))
        {
            var st = UiKit.Chip(cx, y, 420f, 26f);
            GUI.Label(st, NetSession.Status, smallStyle);
        }

        y += 36f;
        if (WhiteBtn(new Rect(cx - 60f, y, 120f, 40f), "뒤로", backStyle)) step = 0;
    }
}
