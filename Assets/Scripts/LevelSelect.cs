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

    public Level[] levels =   // ⚠️씬 인스턴스(StartScreen) 직렬화 배열도 갱신 필요(직렬화 함정)
    {
        new Level { label = "시작", scene = "Hallway",     thumb = "Thumbs/hallway" },
        new Level { label = "정글", scene = "Jungle",      thumb = "Thumbs/jungle"  },
        new Level { label = "빙산", scene = "IceMountain", thumb = "Thumbs/ice"     },
    };
    [Tooltip("'구현중인 맵들' 버튼을 눌러야 보이는 목록")]
    public Level[] wipLevels =
    {
        new Level { label = "우주", scene = "Space", thumb = "Thumbs/space" },
        new Level { label = "등반", scene = "Climb", thumb = "Thumbs/climb" },
        new Level { label = "달",   scene = "Moon",  thumb = "Thumbs/moon"  },
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

    Texture2D[] thumbs, wipThumbs;
    bool showWip;        // 스테이지 선택에서 '구현중인 맵들' 목록 표시 중
    GUIStyle labelStyle, titleStyle, backStyle, btnStyle, smallStyle, codeStyle;
    Texture2D white;

    void Start()
    {
        thumbs = new Texture2D[levels.Length];
        for (int i = 0; i < levels.Length; i++)
            thumbs[i] = Resources.Load<Texture2D>(levels[i].thumb);
        wipThumbs = new Texture2D[wipLevels.Length];
        for (int i = 0; i < wipLevels.Length; i++)
            wipThumbs[i] = Resources.Load<Texture2D>(wipLevels[i].thumb);
        white = Texture2D.whiteTexture;
    }

    bool multiOnly;      // ★B안: '함께하기'로 열면 모드 선택에서 1인 버튼 숨김

    public void Open()
    {
        // 세션 중(로비/인게임)엔 다시 안 연다 — 로비 창(NetDebugPanel)과 겹침 방지
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && (nm.IsHost || nm.IsConnectedClient)) return;
        Showing = true; step = 0; codeInput = ""; showWip = false; multiOnly = false;
    }

    /// <summary>★B안 '함께하기': 멀티 옵션만 있는 모드 선택으로 열기.</summary>
    public void OpenMulti()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && (nm.IsHost || nm.IsConnectedClient)) return;
        Showing = true; step = 0; codeInput = ""; showWip = false; multiOnly = true;
    }

    /// <summary>맵 이름 한/영(레이블은 씬에 한글 직렬화 — 영문은 씬 이름 매핑).</summary>
    static string MapLabel(Level lv)
    {
        if (L.Korean) return lv.label;
        switch (lv.scene)
        {
            case "Hallway": return "start";
            case "Jungle": return "jungle";
            case "IceMountain": return "iceberg";
            case "Space": return "space";
            case "Climb": return "climb";
            case "Moon": return "moon";
            default: return lv.scene.ToLower();
        }
    }
    public void Close()
    {
        Showing = false;
        if (preview != null) preview.HideAll();
        previewHover = -1;
    }

    /// <summary>테스트/외부 진입용: 0=모드 선택, 1=스테이지 선택(wip=구현중 목록), 2=코드 참가.</summary>
    public void GoTo(int s, bool wip = false)
    {
        Showing = true;
        step = Mathf.Clamp(s, 0, 2);
        showWip = wip;
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
        // ★B안 손그림 리스킨(2026-07-13): 전 스타일 개구체(Gaegu) — 손글씨 폰트라 사이즈 상향
        labelStyle = HandUI.Style(26, HandUI.Ink);
        titleStyle = HandUI.Style(42, HandUI.Ink);
        backStyle = HandUI.Style(26, HandUI.Ink);
        btnStyle = HandUI.Style(30, HandUI.Ink);
        smallStyle = HandUI.Style(20, HandUI.InkSoft);
        // 방코드 입력칸 전용: 크고 진한 검은 글자(흰 배경 고정 — 호버/포커스 색 변화 없음)
        codeStyle = HandUI.Style(38, Color.black);
    }

    void Rect2(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = p; }

    /// <summary>손그림 버튼(★B안): 흰 종이+손그림 테두리, 호버=살짝 기울며 옅은 음영. disabled 시 흐리게.</summary>
    bool WhiteBtn(Rect r, string text, GUIStyle style, bool enabled = true)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        var m = GUI.matrix;
        if (hover) GUIUtility.RotateAroundPivot(-0.8f, r.center);
        HandUI.Paper(r, HandUI.BoxWide, enabled ? 0.97f : 0.6f);
        if (hover) UiKit.Fill(new Rect(r.x + 6f, r.y + 6f, r.width - 12f, r.height - 12f), new Color(0f, 0f, 0f, 0.06f));
        GUI.Label(r, text, style);
        GUI.matrix = m;
        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    /// <summary>제목/안내문 — 흰 종이+손그림 테두리 칩(★B안).</summary>
    void ChipLabel(float y, float w, float h, string text, GUIStyle style)
    {
        var r = new Rect(Screen.width * 0.5f - w * 0.5f, y, w, h);
        HandUI.Paper(r, HandUI.BoxWide, 0.97f);
        GUI.Label(r, text, style);
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
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
    // 기본 = levels(시작/정글/빙산), '구현중인 맵들' 버튼 = wipLevels(우주/등반/달)로 전환.
    void DrawStageSelect()
    {
        var arr = showWip ? wipLevels : levels;
        var tex = showWip ? wipThumbs : thumbs;
        ChipLabel(Screen.height * 0.5f - (showWip ? 216f : 180f), 400f, 56f,
            showWip ? L.T("구현중인 맵들", "maps in progress") : L.T("스테이지 선택", "choose a stage"), titleStyle);
        if (showWip)
            ChipLabel(Screen.height * 0.5f - 158f, 470f, 34f,
                L.T("아직 만드는 중이라 미완성일 수 있어요", "still under construction — may be rough"), smallStyle);

        int n = arr.Length;
        float gap = 36f;
        float maxW = (Screen.width - gap * (n + 1) - 200f);   // 우측 '구현중' 버튼 자리 확보
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

            // ★호버 = 3D 프리뷰 전환(방향 = 인덱스 증감) — 기본 목록에서만(인덱스가 미니어처 순서와 일치)
            if (!showWip && hover && preview != null && !loadPending && i != previewHover)
            {
                int dir = (previewHover >= 0 && i < previewHover) ? -1 : 1;
                preview.Show(i, dir);
                previewHover = i;
            }

            // ★B안: 흰 종이 여백 + 손그림 프레임(hover 시 살짝 커지고 기울어짐)
            float grow = hover ? 5f : 0f;
            var frame = new Rect(x - 9f - grow, y - 9f - grow, tw + 18f + grow * 2f, th + 18f + grow * 2f);
            var tm = GUI.matrix;
            if (hover) GUIUtility.RotateAroundPivot(-1.1f, frame.center);
            Rect2(frame, Color.white);
            if (HandUI.BoxTile != null) GUI.DrawTexture(frame, HandUI.BoxTile, ScaleMode.StretchToFill);
            // ★미니어처 우선: 살아있는 3D 회전 섬(RT). 없으면 기존 스크린샷 폴백.
            Texture mini = null;
            if (!showWip && miniatures != null) { miniatures.MarkVisible(); mini = miniatures.RT(i); }
            if (mini != null) GUI.DrawTexture(thumbR, mini, ScaleMode.ScaleAndCrop);
            else if (tex[i] != null) GUI.DrawTexture(thumbR, tex[i], ScaleMode.ScaleAndCrop);
            else Rect2(thumbR, new Color(0.92f, 0.92f, 0.92f, 1f));
            GUI.matrix = tm;
            var chipW = Mathf.Min(tw, 180f);
            var nameChip = new Rect(x + tw * 0.5f - chipW * 0.5f, y + th + 14f, chipW, 40f);
            HandUI.Paper(nameChip, HandUI.BoxWide, 0.97f);
            GUI.Label(nameChip, MapLabel(arr[i]), labelStyle);

            // ★클릭 = 확정 연출(펄스→꺼짐) 후 기존 로드. 프리뷰 없으면 즉시 로드(폴백)
            if (GUI.Button(thumbR, GUIContent.none, GUIStyle.none) && !loadPending)
            {
                if (!showWip && preview != null && !preview.Busy)
                {
                    loadPending = true;
                    var captured = arr[i];
                    preview.Show(i, 1);               // 다른 타일 프리뷰 중이었어도 클릭한 맵으로
                    preview.Confirm(() => { loadPending = false; LoadLocal(captured); });
                }
                else if (showWip || preview == null) LoadLocal(arr[i]);
            }
        }

        // 우측 '구현중인 맵들' 진입 버튼(기본 목록에서만)
        if (!showWip)
        {
            var wipR = new Rect(x0 + totalW + gap + 8f, y + th * 0.5f - 40f, 180f, 80f);
            if (WhiteBtn(wipR, L.T("구현중인\n맵들 ▶", "wip\nmaps ▶"), backStyle)) { showWip = true; if (preview != null) preview.HideAll(); previewHover = -1; }
        }

        if (WhiteBtn(new Rect(Screen.width * 0.5f - 70f, y + th + 66f, 140f, 48f), L.T("뒤로", "back"), backStyle))
        {
            if (showWip) showWip = false;
            else step = 0;
        }
    }

    // ── ① 모드 선택(첫 화면): 1인 / 로컬 멀티 / 방 만들기 / 코드 참가 ──
    void DrawModeSelect()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        ChipLabel(cy - 214f, 560f, 56f,
            multiOnly ? L.T("함께하기", "play together") : L.T("어떻게 플레이할까요?", "how do you want to play?"),
            titleStyle);

        float bw = 420f, bh = 62f, gap = 18f;
        float y = cy - 134f;

        if (!multiOnly)
        {
            if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), L.T("1인 플레이", "solo"), btnStyle)) step = 1;
            y += bh + gap;
        }
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), L.T("로컬 멀티 (한 키보드 최대 4인)", "local co-op (one keyboard, up to 4)"), btnStyle)) step = 1;
        y += bh + gap;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh),
            busy ? L.T("방 만드는 중…", "creating room…") : L.T("온라인 — 방 만들기 (호스트)", "online — host a room"), btnStyle, !busy)) HostRoom();
        var hint = new Rect(cx - 250f, y + bh + 4f, 500f, 30f);
        GUI.Label(hint, L.T("방을 만들면 여기서 코드가 발급되고, 사람을 모은 뒤 게임을 시작해요",
                            "hosting gives you a room code — gather friends, then start"), smallStyle);
        y += bh + gap + 30f;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh), L.T("온라인 — 코드로 참가", "online — join with a code"), btnStyle, !busy)) { step = 2; codeInput = ""; }
        y += bh + gap + 8f;
        if (!string.IsNullOrEmpty(NetSession.Status))
        {
            GUI.Label(new Rect(cx - 210f, y, 420f, 26f), NetSession.Status, smallStyle);
            y += 34f;
        }
        if (WhiteBtn(new Rect(cx - 70f, y, 140f, 48f), L.T("뒤로", "back"), backStyle)) Close();
    }

    void LoadLocal(Level lv)
    {
        Time.timeScale = 1f;
        Close();
        NetScene.Load(lv.scene);
    }

    // ── ③ 코드로 참가: 화면이 열리면 바로 타이핑 활성 ──
    void DrawJoin()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        ChipLabel(cy - 176f, 480f, 56f, L.T("방 코드로 참가", "join with a room code"), titleStyle);

        float bw = 360f, bh = 62f;
        float y = cy - 104f;

        // ★방코드 입력칸(손그림 종이): 흰 배경 고정 + 크고 진한 검은 글자 + 깜빡이 커서.
        //   클릭 포커스 없이 이 화면에선 항상 타이핑 활성(Update가 캡처).
        Rect codeR = new Rect(cx - bw * 0.5f, y, bw, 64f);
        HandUI.Paper(codeR, HandUI.BoxWide, 1f);
        bool blink = Time.unscaledTime % 1f < 0.55f;
        GUI.Label(codeR, codeInput + (busy ? "" : (blink ? "|" : " ")), codeStyle);
        GUI.Label(new Rect(cx - 230f, y + 70f, 460f, 28f),
            L.T("친구의 방 코드를 입력하세요 (엔터 = 참가)", "type your friend's room code (enter = join)"), smallStyle);

        y += 112f;
        if (WhiteBtn(new Rect(cx - bw * 0.5f, y, bw, bh),
            busy ? L.T("참가 중…", "joining…") : L.T("참가", "join"), btnStyle, !busy && codeInput.Length >= 4)) JoinRoom();

        y += bh + 14f;
        if (!string.IsNullOrEmpty(NetSession.Status))
        {
            var st = UiKit.Chip(cx, y, 420f, 26f);
            GUI.Label(st, NetSession.Status, smallStyle);
        }

        y += 36f;
        if (WhiteBtn(new Rect(cx - 70f, y, 140f, 48f), L.T("뒤로", "back"), backStyle)) step = 0;
    }
}
