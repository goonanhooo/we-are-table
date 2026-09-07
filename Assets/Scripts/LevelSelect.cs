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
    [Tooltip("★2026-09-08 이전 맵을 클리어해야 다음 맵이 열리는 진행 잠금(끄면 플로우의 모든 맵 바로 선택 가능). 기본 끔.")] public bool requireProgress = false;
    [System.Serializable]
    public class Level { public string label; public string scene; public string thumb; }

    public Level[] levels =   // ⚠️씬 인스턴스(StartScreen) 직렬화 배열도 갱신 필요(직렬화 함정)
    {
        // ★2026-09-06 전체 흐름 순서: 복도→정글→동굴(광차)→빙산(눈사람 동굴 시작)→우주→달 (Progress.Chain과 동일)
        new Level { label = "복도", scene = "Hallway",     thumb = "Thumbs/hallway" },
        new Level { label = "정글", scene = "Jungle",      thumb = "Thumbs/jungle"  },
        new Level { label = "동굴", scene = "IceCavern",   thumb = "Thumbs/cavern"  },
        new Level { label = "빙산", scene = "IceMountain", thumb = "Thumbs/ice"     },
        new Level { label = "우주", scene = "Space",       thumb = "Thumbs/space"   },
        new Level { label = "달",   scene = "Moon",        thumb = "Thumbs/moon"    },
    };
    [Tooltip("'구현중인 맵들' 버튼을 눌러야 보이는 목록")]
    public Level[] wipLevels = { };   // ★2026-09-06 비움 — 우주/달은 기본 목록으로, 등반/트랙/모험은 흐름 밖(메뉴 미노출)

    public bool Showing { get; private set; }

    [Tooltip("★맵 선택 미니어처(갤럭시 스타일 회전 섬) — 있으면 스크린샷 대신 라이브 3D 미니어처 표시")]
    public MapMiniatures miniatures;
    [Tooltip("★맵 선택 3D 프리뷰 전환 연출 — 호버=등장/퇴장, 클릭=확정 연출 후 로드")]
    public MapSelectPreview preview;
    int previewHover = -1;
    bool loadPending;

    int step;            // 0=모드 선택, 1=스테이지 선택(1인/로컬), 2=방코드 입력(참가), 3=난이도 선택(★2026-09-05 — 1인 스테이지 전에)
    string codeInput = "";
    bool busy;           // 방 생성/참가 진행 중
    string busyMsg = ""; // ★로딩 화면 문구(방 만드는 중/참가 중) — busy 동안 전용 화면 표시(2026-07-14)

    Texture2D[] thumbs, wipThumbs;
    bool showWip;        // 스테이지 선택에서 '구현중인 맵들' 목록 표시 중
    GUIStyle labelStyle, titleStyle, backStyle, btnStyle, smallStyle, codeStyle, nameStyle;
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
    bool directStage;    // ★'시작하기'로 스테이지에 바로 진입 — 뒤로/ESC가 모드 선택이 아니라 홈으로

    public void Open()
    {
        // 세션 중(로비/인게임)엔 다시 안 연다 — 로비 창(NetDebugPanel)과 겹침 방지
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && (nm.IsHost || nm.IsConnectedClient)) return;
        Showing = true; step = 0; codeInput = ""; showWip = false; multiOnly = false; directStage = false;
    }

    /// <summary>★B안 '시작하기': 난이도 선택 → 스테이지 선택(뒤로 = 홈).</summary>
    public void OpenStage()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && (nm.IsHost || nm.IsConnectedClient)) return;
        Showing = true; step = 3; codeInput = ""; showWip = false; multiOnly = false; directStage = true;
    }

    /// <summary>★B안 '함께하기': 멀티 옵션만 있는 모드 선택으로 열기.</summary>
    public void OpenMulti()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && (nm.IsHost || nm.IsConnectedClient)) return;
        Showing = true; step = 0; codeInput = ""; showWip = false; multiOnly = true; directStage = false;
    }

    /// <summary>스테이지의 '뒤로': 구현중 목록이면 기본 목록으로, 아니면 난이도 선택으로.</summary>
    void StageBack()
    {
        if (showWip) showWip = false;
        else step = 3;
    }

    /// <summary>난이도의 '뒤로': 직행 진입이면 홈으로, 아니면 모드 선택으로.</summary>
    void DiffBack()
    {
        if (directStage) Close();
        else step = 0;
    }

    /// <summary>맵 이름 한/영(레이블은 씬에 한글 직렬화 — 영문은 씬 이름 매핑).</summary>
    static string MapLabel(Level lv)
    {
        if (L.Korean) return lv.label;
        switch (lv.scene)
        {
            case "Hallway": return "hallway";
            case "Jungle": return "jungle";
            case "IceCavern": return "cave";
            case "FrameHall": return "gallery";
            case "TennisCourt": return "tennis court";
            case "IceMountain": return "iceberg";
            case "Space": return "space";
            case "Climb": return "climb";
            case "Moon": return "moon";
            default: return lv.scene.ToLower();
        }
    }
    /// <summary>3D 프리뷰/미니어처 인덱스(기존 3맵 순서) — 없는 맵(액자 등)은 -1 = 정적 썸네일만.</summary>
    static int PreviewIndex(string scene)
    {
        switch (scene)
        {
            case "Hallway": return 0;
            case "Jungle": return 1;
            case "IceMountain": return 2;
            default: return -1;
        }
    }

    public void Close()
    {
        Showing = false;
        if (preview != null) preview.HideAll();
        previewHover = -1;
    }

    /// <summary>테스트/외부 진입용: 0=모드 선택, 1=스테이지 선택(wip=구현중 목록), 2=코드 참가, 3=난이도 선택.</summary>
    public void GoTo(int s, bool wip = false)
    {
        Showing = true;
        step = Mathf.Clamp(s, 0, 3);
        showWip = wip;
    }

    void Update()
    {
        // 방코드 타이핑 — ★참가 화면이 열려 있으면 항상 활성(클릭 포커스 불필요)
        if (!Showing || step != 2 || busy) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        // 코드 길이: Unity Relay 6자리 / 스팀 로비 번호 ~18자리 — 최대 20자
        for (Key k = Key.A; k <= Key.Z; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 20) codeInput += k.ToString();
        for (Key k = Key.Digit1; k <= Key.Digit0; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 20) codeInput += (k == Key.Digit0 ? "0" : ((int)k - (int)Key.Digit1 + 1).ToString());
        for (Key k = Key.Numpad0; k <= Key.Numpad9; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 20) codeInput += ((int)k - (int)Key.Numpad0).ToString();
        // 스팀 로비 번호는 길어서 붙여넣기 지원(Ctrl/Cmd+V) — 영숫자만 채택
        if (kb.vKey.wasPressedThisFrame && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed))
        {
            string clip = GUIUtility.systemCopyBuffer ?? "";
            foreach (char c in clip)
                if (char.IsLetterOrDigit(c) && codeInput.Length < 20) codeInput += char.ToUpperInvariant(c);
        }
        if (kb.backspaceKey.wasPressedThisFrame && codeInput.Length > 0) codeInput = codeInput.Substring(0, codeInput.Length - 1);
        if (kb.enterKey.wasPressedThisFrame && codeInput.Length >= 4) JoinRoom();
    }

    // 온라인: 방 만들기 → ★게임에 안 들어가고 시작 화면에 로비가 뜸(사람 모은 뒤 로비에서 맵+시작)
    async void HostRoom()
    {
        if (busy) return;
        busy = true;
        busyMsg = L.T("방 만드는 중", "creating room");
        NetLegLink.ResetSessionCache();   // 새 세션 — 이전 세션의 배정/시작 캐시 제거
        bool ok = await NetSession.HostAsync();
        busy = false;
        if (ok)
        {
            Time.timeScale = 1f;
            Close();   // 오버레이 닫힘 → NetDebugPanel 로비가 이어받음
        }
    }

    // 온라인: 방코드 참가 → 성공 시 로비 합류(호스트가 시작하면 NGO가 씬 자동 전환)
    async void JoinRoom()
    {
        if (busy || codeInput.Length < 4) return;
        busy = true;
        busyMsg = L.T("참가 중", "joining");
        bool ok = await NetSession.JoinAsync(codeInput);
        busy = false;
        if (ok) Close();
    }

    void EnsureStyles()
    {
        if (labelStyle != null) return;
        // ★B안 손그림 리스킨(2026-07-13): 전 스타일 개구체(Gaegu) — 손글씨 폰트라 사이즈 상향
        // ★홈 화면과 통일(박스 없는 손글씨 좌측 정렬)
        labelStyle = HandUI.Style(26, HandUI.Ink);
        // ★2026-07-14 제목은 메뉴 항목과 구분(사용자 요청): 작고 연한 회색 = 호버/클릭 안 되는 안내 글씨
        titleStyle = HandUI.Style(28, HandUI.InkSoft, TextAnchor.MiddleLeft);
        backStyle = HandUI.Style(30, HandUI.Ink, TextAnchor.MiddleLeft);
        btnStyle = HandUI.Style(32, HandUI.Ink, TextAnchor.MiddleLeft);
        smallStyle = HandUI.Style(20, HandUI.InkSoft, TextAnchor.MiddleLeft);
        nameStyle = HandUI.Style(34, HandUI.Ink, TextAnchor.MiddleLeft);
        // 방코드 입력칸: 밑줄 위에 쓰는 스타일(좌측 정렬)
        codeStyle = HandUI.Style(40, Color.black, TextAnchor.MiddleLeft);
    }

    void Rect2(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = p; }

    /// <summary>★홈 화면과 통일된 텍스트 메뉴 항목(2026-07-13, 사용자 요청): 박스 없음 —
    /// 맨 손글씨 + 호버 시 살짝 기울며 손그림 밑줄 스윽. disabled 시 흐리게.</summary>
    bool WhiteBtn(Rect r, string text, GUIStyle style, bool enabled = true)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        var m = GUI.matrix;
        if (hover) GUIUtility.RotateAroundPivot(-1.2f, new Vector2(r.x, r.center.y));
        var c = style.normal.textColor;
        if (!enabled) style.normal.textColor = new Color(c.r, c.g, c.b, 0.35f);
        GUI.Label(r, text, style);
        style.normal.textColor = c;
        if (hover && HandUI.Underline != null)
        {
            float textW = Mathf.Min(style.CalcSize(new GUIContent(text)).x, r.width);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 10f, textW, 14f), HandUI.Underline, ScaleMode.StretchToFill);
        }
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

        // ★B안: 딤 없음 — 흰 배경 위 왼쪽 열에 그대로 그린다(홈 메뉴 자리를 하위 메뉴가 교대)

        // ★로딩 중(방 만들기/참가 네트워크 대기): 다른 메뉴 전부 숨기고 로딩 문구만 — 겹침/오클릭 방지
        if (busy)
        {
            float blx = Screen.width * 0.09f;
            float by = Screen.height * 0.40f;
            int dots = (int)(Time.unscaledTime * 2f) % 4;
            GUI.Label(new Rect(blx, by, 700f, 60f), busyMsg + new string('.', dots), nameStyle);
            GUI.Label(new Rect(blx, by + 64f, 700f, 30f),
                L.T("잠시만 기다려 주세요", "just a moment"), smallStyle);
            return;
        }

        // ESC = 한 단계 뒤로(시작하기 직행이면 홈으로)
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            if (step == 1) StageBack();
            else if (step == 3) DiffBack();
            else if (step > 0) step = 0;
            else Close();
            return;
        }

        if (step == 0) DrawModeSelect();
        else if (step == 1) DrawStageSelect();
        else if (step == 3) DrawDifficulty();
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
    // ★B안: 왼쪽 열 세로 목록(썸네일 + 이름) — 홈 메뉴 자리를 하위 메뉴가 그대로 이어받는다.
    void DrawStageSelect()
    {
        var arr = showWip ? wipLevels : levels;
        var tex = showWip ? wipThumbs : thumbs;
        float lx = Screen.width * 0.09f;                 // 홈 메뉴와 같은 왼쪽 기준
        float y = Screen.height * 0.36f;                 // 타이틀 그래픽 아래에서 시작

        GUI.Label(new Rect(lx, y, 700f, 56f),
            showWip ? L.T("구현중인 맵들", "maps in progress") : L.T("스테이지 선택", "choose a stage"), titleStyle);
        y += 64f;
        if (showWip)
        {
            GUI.Label(new Rect(lx, y, 620f, 28f), L.T("아직 만드는 중이라 미완성일 수 있어요", "still under construction — may be rough"), smallStyle);
            y += 32f;
        }
        y += 8f;

        // ★2026-09-08 6맵이 한 열에 안 들어감(1080p에서도 5·6번째와 '뒤로'가 화면 밖) → 5개 이상이면 2열 격자, 행 높이는 남은 높이에 맞춰 축소(웹 960×600 포함)
        float rowGap = 14f;
        int n = arr.Length, cols = n > 4 ? 2 : 1, rows = Mathf.CeilToInt(n / (float)cols);
        float availH = Screen.height - y - 70f;   // 아래 '뒤로' 버튼 자리
        float pitch = availH / Mathf.Max(1, rows);
        float th = Mathf.Clamp(pitch - 18f - rowGap, 40f, Mathf.Min(122f, Screen.height * 0.11f));
        float tw = th * 1.6f;
        float nameW = cols == 2 ? 170f : 210f, colW = tw + 18f + nameW + 30f;
        float y0 = y;

        for (int i = 0; i < arr.Length; i++)
        {
            float cx = lx + (i / rows) * colW; y = y0 + (i % rows) * (th + 18f + rowGap);
            // ★2026-07-14 진행 잠금(사용자: 처음엔 '시작'만): 이전 맵 클리어 전엔 실루엣+??? — 치트는 전부 해금
            bool locked = requireProgress && !showWip && !CheatMode.Active && !Progress.IsUnlocked(arr[i].scene);   // ★2026-09-08 기본 전부 열림(사용자: 플로우의 모든 맵 바로 선택)
            var frame0 = new Rect(cx, y, tw + 18f, th + 18f);
            var rowR = new Rect(cx, y, tw + 18f + nameW, th + 18f);   // 이름까지 클릭 영역
            bool hover = !locked && rowR.Contains(Event.current.mousePosition);

            // ★호버 = 3D 프리뷰 전환 — 프리뷰 섬이 있는 맵만(씬 이름→프리뷰 인덱스 매핑)
            int pv = PreviewIndex(arr[i].scene);
            if (!showWip && hover && preview != null && !loadPending && pv >= 0 && pv != previewHover)
            {
                int dir = (previewHover >= 0 && pv < previewHover) ? -1 : 1;
                preview.Show(pv, dir);
                previewHover = pv;
            }

            float grow = hover ? 4f : 0f;
            var frame = new Rect(frame0.x - grow, frame0.y - grow, frame0.width + grow * 2f, frame0.height + grow * 2f);
            var thumbR = new Rect(frame.x + 9f + grow, frame.y + 9f + grow, tw, th);
            var tm = GUI.matrix;
            if (hover) GUIUtility.RotateAroundPivot(-1.0f, frame.center);
            Rect2(frame, Color.white);
            if (HandUI.BoxTile != null) GUI.DrawTexture(frame, HandUI.BoxTile, ScaleMode.StretchToFill);
            // ★미니어처 우선: 살아있는 3D 회전 섬(RT). 없으면 기존 스크린샷 폴백. 잠금 맵=검은 실루엣.
            if (locked)
            {
                var pc = GUI.color;
                GUI.color = new Color(0.10f, 0.10f, 0.10f, 1f);   // 어둡게 곱해 실루엣만 남김
                if (tex[i] != null) GUI.DrawTexture(thumbR, tex[i], ScaleMode.ScaleAndCrop);
                else GUI.DrawTexture(thumbR, white, ScaleMode.StretchToFill);
                GUI.color = pc;
            }
            else
            {
                Texture mini = null;
                int mi = PreviewIndex(arr[i].scene);
                if (!showWip && miniatures != null && mi >= 0) { miniatures.MarkVisible(); mini = miniatures.RT(mi); }
                if (mini != null) GUI.DrawTexture(thumbR, mini, ScaleMode.ScaleAndCrop);
                else if (tex[i] != null) GUI.DrawTexture(thumbR, tex[i], ScaleMode.ScaleAndCrop);
                else Rect2(thumbR, new Color(0.92f, 0.92f, 0.92f, 1f));
            }
            GUI.matrix = tm;

            // 이름: 썸네일 오른쪽에 손글씨(호버 시 밑줄). 잠금 맵은 ???
            var nameR = new Rect(frame0.xMax + 18f, y + frame0.height * 0.5f - 24f, nameW - 10f, 48f);
            string nm = locked ? "???" : MapLabel(arr[i]);
            GUI.Label(nameR, nm, nameStyle);
            if (hover && HandUI.Underline != null)
            {
                float textW = nameStyle.CalcSize(new GUIContent(nm)).x;
                GUI.DrawTexture(new Rect(nameR.x, nameR.yMax - 6f, textW, 14f), HandUI.Underline, ScaleMode.StretchToFill);
            }

            // ★클릭 = 확정 연출(펄스→꺼짐) 후 기존 로드. 프리뷰 없으면 즉시 로드(폴백). 잠금 맵은 클릭 불가
            if (!locked && GUI.Button(rowR, GUIContent.none, GUIStyle.none) && !loadPending)
            {
                int cpv = PreviewIndex(arr[i].scene);
                if (!showWip && preview != null && !preview.Busy && cpv >= 0)
                {
                    loadPending = true;
                    var captured = arr[i];
                    preview.Show(cpv, 1);             // 다른 타일 프리뷰 중이었어도 클릭한 맵으로
                    preview.Confirm(() => { loadPending = false; LoadLocal(captured); });
                }
                else LoadLocal(arr[i]);
            }
        }
        y = y0 + rows * (th + 18f + rowGap) + 8f;
        if (!showWip)
        {
            if (wipLevels.Length > 0 && WhiteBtn(new Rect(lx, y, 250f, 52f), L.T("구현중인 맵들 ▶", "wip maps ▶"), backStyle))
            { showWip = true; if (preview != null) preview.HideAll(); previewHover = -1; }
            y += 62f;
        }
        if (WhiteBtn(new Rect(lx, y, 150f, 50f), L.T("뒤로", "back"), backStyle)) StageBack();
    }

    // ── ① 모드 선택: 로컬 멀티 / 방 만들기 / 코드 참가 (★B안: 왼쪽 열) ──
    void DrawModeSelect()
    {
        float lx = Screen.width * 0.09f;
        float y = Screen.height * 0.36f;

        GUI.Label(new Rect(lx, y, 760f, 56f),
            multiOnly ? L.T("함께하기", "play together") : L.T("어떻게 플레이할까요?", "how do you want to play?"), titleStyle);
        y += 74f;

        float bw = 440f, bh = 60f, gap = 16f;
        if (!multiOnly)
        {
            if (WhiteBtn(new Rect(lx, y, bw, bh), L.T("1인 플레이", "solo"), btnStyle)) step = 3;   // 난이도 → 스테이지
            y += bh + gap;
        }
        // ★2026-07-14 로컬 멀티 항목 삭제(사용자 요청) — 한 키보드 협동은 그냥 시작하기로 하면 됨
        if (WhiteBtn(new Rect(lx, y, bw, bh),
            busy ? L.T("방 만드는 중…", "creating room…") : L.T("온라인 — 방 만들기 (호스트)", "online — host a room"), btnStyle, !busy)) HostRoom();
        y += bh + 6f;
        GUI.Label(new Rect(lx, y, 640f, 28f), L.T("방을 만들면 여기서 코드가 발급되고, 사람을 모은 뒤 게임을 시작해요",
                            "hosting gives you a room code — gather friends, then start"), smallStyle);
        y += 36f;
        if (WhiteBtn(new Rect(lx, y, bw, bh), L.T("온라인 — 코드로 참가", "online — join with a code"), btnStyle, !busy)) { step = 2; codeInput = ""; }
        y += bh + gap;
        if (!string.IsNullOrEmpty(NetSession.Status))
        {
            GUI.Label(new Rect(lx, y, 560f, 26f), NetSession.Status, smallStyle);
            y += 34f;
        }
        if (WhiteBtn(new Rect(lx, y, 150f, 50f), L.T("뒤로", "back"), backStyle)) Close();
    }

    void LoadLocal(Level lv)
    {
        Time.timeScale = 1f;
        Close();
        NetScene.Load(lv.scene);
    }

    // ── ④ 난이도 선택(★2026-09-05, 1인 스테이지 전에): 겁쟁이(바로서기 무제한) / 보통(3개 + 링 충전) ──
    static readonly Difficulty.Mode[] DiffOrder = { Difficulty.Mode.Coward, Difficulty.Mode.Normal };
    void DrawDifficulty()
    {
        float lx = Screen.width * 0.09f;
        float y = Screen.height * 0.36f;
        GUI.Label(new Rect(lx, y, 700f, 56f), L.T("난이도 선택", "choose a difficulty"), titleStyle);
        y += 74f;

        float bw = 520f, bh = 60f;
        var cur = Difficulty.Local;
        foreach (var m in DiffOrder)
        {
            var r = new Rect(lx, y, bw, bh);
            string name = Difficulty.Name(m, false);
            bool hover = r.Contains(Event.current.mousePosition);
            if (WhiteBtn(r, name, btnStyle)) { Difficulty.Local = m; step = 1; }
            // 지난번에 고른 난이도 = 밑줄 고정(호버 중엔 호버 밑줄이 대신)
            if (m == cur && !hover && HandUI.Underline != null)
            {
                float tw = Mathf.Min(btnStyle.CalcSize(new GUIContent(name)).x, bw);
                GUI.DrawTexture(new Rect(r.x, r.yMax - 10f, tw, 14f), HandUI.Underline, ScaleMode.StretchToFill);
            }
            y += bh + 2f;
            GUI.Label(new Rect(lx, y, 820f, 28f), Difficulty.Desc(m), smallStyle);
            y += 46f;
        }
        y += 8f;
        if (WhiteBtn(new Rect(lx, y, 150f, 50f), L.T("뒤로", "back"), backStyle)) DiffBack();
    }

    // ── ③ 코드로 참가: 화면이 열리면 바로 타이핑 활성 ──
    void DrawJoin()
    {
        float lx = Screen.width * 0.09f;
        float y = Screen.height * 0.36f;
        GUI.Label(new Rect(lx, y, 700f, 56f), L.T("방 코드로 참가", "join with a room code"), titleStyle);
        y += 74f;

        float bw = 380f, bh = 60f;
        // ★방코드 = 손그림 밑줄 위에 쓰는 스타일(박스 없음 — 홈과 통일).
        //   클릭 포커스 없이 이 화면에선 항상 타이핑 활성(Update가 캡처).
        Rect codeR = new Rect(lx, y, bw, 56f);
        bool blink = Time.unscaledTime % 1f < 0.55f;
        GUI.Label(codeR, codeInput + (busy ? "" : (blink ? "|" : " ")), codeStyle);
        if (HandUI.Underline != null)
            GUI.DrawTexture(new Rect(lx, codeR.yMax - 4f, bw, 16f), HandUI.Underline, ScaleMode.StretchToFill);
        GUI.Label(new Rect(lx, y + 70f, 560f, 28f),
            L.T("친구의 방 코드를 입력하세요 (엔터 = 참가)", "type your friend's room code (enter = join)"), smallStyle);

        y += 112f;
        if (WhiteBtn(new Rect(lx, y, bw, bh),
            busy ? L.T("참가 중…", "joining…") : L.T("참가", "join"), btnStyle, !busy && codeInput.Length >= 4)) JoinRoom();

        y += bh + 14f;
        if (!string.IsNullOrEmpty(NetSession.Status))
        {
            GUI.Label(new Rect(lx, y, 560f, 26f), NetSession.Status, smallStyle);
        }

        y += 36f;
        if (WhiteBtn(new Rect(lx, y, 150f, 50f), L.T("뒤로", "back"), backStyle)) step = 0;
    }
}
