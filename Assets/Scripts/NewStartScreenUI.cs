using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★ 새 시작 화면(2026-07-10) — '크래프트 디오라마' UI. 기존 StartScreen의 메뉴 플로우를 그대로 따름:
///   홈(플레이/옵션) → 모드 선택(1인/로컬/방 만들기/코드 참가) → 스테이지 선택 | 코드 입력 | 옵션(볼륨)
/// 스타일: 코르크보드+핀 폴라로이드(스테이지), 나무 보드+마스킹테이프 종이 카드(버튼),
///   연필 대시 트랙+나무 노브(슬라이더). 전부 IMGUI 플랫 드로잉(텍스처 굽기 없음 — 색+테두리+기울임).
/// 연출: 화면 전환 시 위에서 톡 떨어지는 등장(easeOutBack), 카드 호버=기울기 펴짐+살짝 확대.
/// 온라인: NetSession 직접 호출(LevelSelect와 동일 패턴). 방 만들기 성공 → 이 UI 숨김 →
///   NetworkManager 프리팹의 NetDebugPanel 로비가 이어받음(세션 중엔 OnGUI 정지).
/// </summary>
public class NewStartScreenUI : MonoBehaviour
{
    enum Step { Home, Mode, Stage, Join, Options }

    [System.Serializable]
    public class Level { public string label; public string scene; public string thumb; public bool wip; }
    public Level[] levels =
    {
        new Level { label = "시작",     scene = "Hallway",     thumb = "Thumbs/hallway" },
        new Level { label = "정글",     scene = "Jungle",      thumb = "Thumbs/jungle"  },
        new Level { label = "빙산",     scene = "IceMountain", thumb = "Thumbs/ice"     },
        new Level { label = "우주",     scene = "Space",       thumb = "Thumbs/space",  wip = true },
        new Level { label = "달",       scene = "Moon",        thumb = "Thumbs/moon",   wip = true },
        new Level { label = "테니스공", scene = "Climb",       thumb = "Thumbs/climb",  wip = true },
    };

    [Header("연출")]
    [SerializeField] float enterTime = 0.32f;
    [SerializeField] float dropPixels = 70f;
    [SerializeField] float hoverScale = 1.045f;
    [Tooltip("패널 중심 X(화면 비율) — 디오라마 테이블(우측)과 안 겹치게 왼쪽 하얀 공간에")]
    [SerializeField] float panelCenterX = 0.40f;

    // 팔레트(크래프트)
    static readonly Color Cork = new Color(0.788f, 0.627f, 0.42f);
    static readonly Color CorkEdge = new Color(0.541f, 0.386f, 0.22f);
    static readonly Color Paper = new Color(0.984f, 0.965f, 0.91f);
    static readonly Color Ink = new Color(0.29f, 0.22f, 0.15f);
    static readonly Color Tape = new Color(0.922f, 0.859f, 0.663f, 0.88f);
    static readonly Color Wood = new Color(0.663f, 0.482f, 0.306f);
    static readonly Color WoodEdge = new Color(0.43f, 0.306f, 0.173f);
    static readonly Color[] PinColors = {
        new Color(0.85f, 0.35f, 0.19f), new Color(0.22f, 0.54f, 0.87f),
        new Color(0.11f, 0.62f, 0.46f), new Color(0.91f, 0.72f, 0.15f) };

    Step step = Step.Home;
    float stepT;
    string codeInput = "";
    bool busy;
    int dragSlider = -1;
    Texture2D[] thumbs;
    GUIStyle title, cardBtn, small, code, polaLabel;

    void Start()
    {
        thumbs = new Texture2D[levels.Length];
        for (int i = 0; i < levels.Length; i++)
            thumbs[i] = Resources.Load<Texture2D>(levels[i].thumb);
        stepT = Time.unscaledTime;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void SetStep(Step s)
    {
        step = s;
        stepT = Time.unscaledTime;
    }

    /// <summary>테스트/외부 진입용(0=홈 1=모드 2=스테이지 3=코드 4=옵션).</summary>
    public void GoTo(int s) => SetStep((Step)Mathf.Clamp(s, 0, 4));

    void Update()
    {
        // 코드 타이핑(참가 화면 열려 있으면 항상 활성 — LevelSelect 패턴)
        if (step != Step.Join || busy) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        for (Key k = Key.A; k <= Key.Z; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 8) codeInput += k.ToString();
        for (Key k = Key.Digit1; k <= Key.Digit0; k++)
            if (kb[k].wasPressedThisFrame && codeInput.Length < 8) codeInput += (k == Key.Digit0 ? "0" : ((int)k - (int)Key.Digit1 + 1).ToString());
        if (kb.backspaceKey.wasPressedThisFrame && codeInput.Length > 0) codeInput = codeInput.Substring(0, codeInput.Length - 1);
        if (kb.enterKey.wasPressedThisFrame && codeInput.Length >= 4) JoinRoom();
    }

    async void HostRoom()
    {
        if (busy) return;
        busy = true;
        NetLegLink.ResetSessionCache();
        bool ok = await NetSession.HostAsync();
        busy = false;
        if (ok) { Time.timeScale = 1f; SetStep(Step.Home); }   // 로비(NetDebugPanel)가 이어받음
    }

    async void JoinRoom()
    {
        if (busy || codeInput.Length < 4) return;
        busy = true;
        bool ok = await NetSession.JoinAsync(codeInput);
        busy = false;
        if (ok) SetStep(Step.Home);
    }

    // ── 드로잉 헬퍼 ──────────────────────────────────────────────────────
    static void Fill(Rect r, Color c)
    {
        var p = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = p;
    }

    static void Bordered(Rect r, Color fill, Color edge, float w)
    {
        Fill(new Rect(r.x - w, r.y - w, r.width + w * 2f, r.height + w * 2f), edge);
        Fill(r, fill);
    }

    void EnsureStyles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = Ink;
        cardBtn = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        cardBtn.normal.textColor = Ink;
        small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        small.normal.textColor = new Color(0.42f, 0.34f, 0.26f);
        code = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        code.normal.textColor = Ink;
        polaLabel = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        polaLabel.normal.textColor = Ink;
    }

    float EnterU() => Mathf.Clamp01((Time.unscaledTime - stepT) / enterTime);
    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
    float DropOffset() => (1f - EaseOutBack(EnterU())) * -dropPixels;

    // 기울어진 카드 버튼(호버=펴짐+확대). 리턴=클릭.
    bool CraftCard(Rect r, string label, float tiltDeg, bool enabled = true, GUIStyle style = null)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        float tilt = hover ? 0f : tiltDeg;
        float sc = hover ? hoverScale : 1f;
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(tilt, r.center);
        GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), r.center);
        Bordered(r, enabled ? Paper : new Color(0.9f, 0.88f, 0.83f), Ink, 2f);
        // 마스킹테이프(위 중앙)
        var tapeR = new Rect(r.center.x - 26f, r.y - 8f, 52f, 14f);
        var tm = GUI.matrix;
        GUIUtility.RotateAroundPivot(tiltDeg >= 0 ? -4f : 4f, tapeR.center);
        Fill(tapeR, Tape);
        GUI.matrix = tm;
        GUIUtility.RotateAroundPivot(tilt, r.center);
        GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), r.center);
        GUI.Label(r, label, style ?? cardBtn);
        GUI.matrix = m;
        return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
    }

    // 나무 팻말 라벨(제목)
    void WoodTitle(float cx, float y, float w, string text)
    {
        var r = new Rect(cx - w * 0.5f, y, w, 52f);
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(-1.2f, r.center);
        Bordered(r, Paper, Ink, 2.5f);
        GUI.Label(r, text, title);
        GUI.matrix = m;
    }

    // ── OnGUI ───────────────────────────────────────────────────────────
    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        // 세션 중(로비/인게임 미니)은 NetDebugPanel에 양보
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening) return;
        EnsureStyles();

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            if (step == Step.Mode || step == Step.Options) SetStep(Step.Home);
            else if (step == Step.Stage || step == Step.Join) SetStep(Step.Mode);
            Event.current.Use();
        }

        switch (step)
        {
            case Step.Home: DrawHome(); break;
            case Step.Mode: DrawMode(); break;
            case Step.Stage: DrawStage(); break;
            case Step.Join: DrawJoin(); break;
            case Step.Options: DrawOptions(); break;
        }
    }

    void DrawHome()
    {
        // 타이틀은 좌상단 손글씨(기존 Canvas)가 담당 — 여기선 카드 2장만
        float cx = Screen.width * panelCenterX, cy = Screen.height * 0.52f + DropOffset();
        if (CraftCard(new Rect(cx - 150f, cy - 90f, 300f, 76f), "플레이", -1.6f)) SetStep(Step.Mode);
        if (CraftCard(new Rect(cx - 150f, cy + 12f, 300f, 76f), "옵션", 1.3f)) SetStep(Step.Options);
    }

    void DrawMode()
    {
        float cx = Screen.width * panelCenterX, cy = Screen.height * 0.5f + DropOffset();
        // 나무 보드
        var board = new Rect(cx - 250f, cy - 205f, 500f, 410f);
        Bordered(board, Wood, WoodEdge, 5f);
        WoodTitle(cx, board.y - 26f, 340f, "어떻게 플레이할까?");

        float bw = 380f, bh = 62f, y = board.y + 52f;
        if (CraftCard(new Rect(cx - bw * 0.5f, y, bw, bh), "1인 플레이", -0.9f)) SetStep(Step.Stage);
        y += bh + 22f;
        if (CraftCard(new Rect(cx - bw * 0.5f, y, bw, bh), "로컬 멀티 (한 키보드 4인)", 0.8f)) SetStep(Step.Stage);
        y += bh + 22f;
        if (CraftCard(new Rect(cx - bw * 0.5f, y, bw, bh), busy ? "방 만드는 중…" : "온라인 — 방 만들기", -0.7f, !busy)) HostRoom();
        y += bh + 22f;
        if (CraftCard(new Rect(cx - bw * 0.5f, y, bw, bh), "온라인 — 코드로 참가", 1.1f, !busy)) SetStep(Step.Join);

        if (!string.IsNullOrEmpty(NetSession.Status))
            GUI.Label(new Rect(cx - 220f, board.yMax + 12f, 440f, 24f), NetSession.Status, small);
    }

    void DrawStage()
    {
        float cx = Screen.width * 0.46f, cyOff = DropOffset();
        int n = levels.Length;
        int cols = 3, rows = (n + cols - 1) / cols;   // 3×2: 윗줄=플레이 가능, 아랫줄=구현중
        float pw = Mathf.Min(200f, (Screen.width - 160f) / cols - 26f);
        float ph = pw * 0.72f + 34f;
        float gridW = cols * (pw + 26f) - 26f;
        float gridH = rows * (ph + 30f) - 30f;
        // 코르크보드
        var board = new Rect(cx - gridW * 0.5f - 34f, Screen.height * 0.5f - gridH * 0.5f - 48f + cyOff, gridW + 68f, gridH + 96f);
        Bordered(board, Cork, CorkEdge, 6f);
        WoodTitle(cx, board.y - 26f, 300f, "스테이지 선택");

        float u = EnterU();
        for (int i = 0; i < n; i++)
        {
            int cxi = i % cols, cyi = i / cols;
            // 폴라로이드 스태거 등장(카드마다 딜레이)
            float su = Mathf.Clamp01((Time.unscaledTime - stepT - i * 0.035f) / enterTime);
            float drop = (1f - EaseOutBack(su)) * -46f;
            float x = board.x + 34f + cxi * (pw + 26f);
            float y = board.y + 58f + cyi * (ph + 30f) + drop;
            var frame = new Rect(x, y, pw, ph);
            bool hover = frame.Contains(Event.current.mousePosition);
            float tilt = hover ? 0f : ((i * 37 % 5) - 2) * 1.15f;   // 고정 랜덤 기울기
            float sc = hover ? hoverScale : 1f;

            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(tilt, frame.center);
            GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), frame.center);
            Bordered(frame, Color.white, hover ? Ink : new Color(0.83f, 0.79f, 0.7f), hover ? 2.5f : 1.5f);
            var photo = new Rect(x + 8f, y + 8f, pw - 16f, pw * 0.72f - 16f + 8f);
            if (thumbs[i] != null) GUI.DrawTexture(photo, thumbs[i], ScaleMode.ScaleAndCrop);
            else Fill(photo, new Color(0.9f, 0.9f, 0.88f));
            GUI.Label(new Rect(x, photo.yMax + 2f, pw, 24f), levels[i].label, polaLabel);
            // (구현중) 스티커 — 사진 좌상단에 노란 테이프 배지
            if (levels[i].wip)
            {
                var badge = new Rect(x + 10f, y + 16f, 76f, 24f);
                var bm = GUI.matrix;
                GUIUtility.RotateAroundPivot(-7f, badge.center);
                Bordered(badge, new Color(1f, 0.93f, 0.62f, 0.96f), new Color(0.72f, 0.6f, 0.28f), 1.5f);
                GUI.Label(badge, "구현중", small);
                GUI.matrix = bm;
            }
            // 핀
            var pin = new Rect(frame.center.x - 6f, y - 6f, 12f, 12f);
            Fill(pin, PinColors[i % PinColors.Length]);
            GUI.matrix = m;

            if (GUI.Button(frame, GUIContent.none, GUIStyle.none))
            {
                Time.timeScale = 1f;
                NetScene.Load(levels[i].scene);
            }
        }
        if (CraftCard(new Rect(cx - 70f, board.yMax + 14f, 140f, 46f), "뒤로", 0.8f, true, polaLabel)) SetStep(Step.Mode);
    }

    void DrawJoin()
    {
        float cx = Screen.width * panelCenterX, cy = Screen.height * 0.5f + DropOffset();
        var board = new Rect(cx - 240f, cy - 150f, 480f, 300f);
        Bordered(board, Wood, WoodEdge, 5f);
        WoodTitle(cx, board.y - 26f, 320f, "방 코드로 참가");

        // 코드 입력 종이(흰 스티커)
        var codeR = new Rect(cx - 170f, board.y + 56f, 340f, 62f);
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(-0.6f, codeR.center);
        Bordered(codeR, Color.white, Ink, 2.5f);
        bool blink = Time.unscaledTime % 1f < 0.55f;
        GUI.Label(codeR, codeInput + (busy ? "" : (blink ? "|" : " ")), code);
        GUI.matrix = m;
        GUI.Label(new Rect(cx - 200f, codeR.yMax + 10f, 400f, 22f), "친구의 방 코드를 입력하세요 (엔터 = 참가)", small);

        if (CraftCard(new Rect(cx - 130f, board.y + 172f, 260f, 56f), busy ? "참가 중…" : "참가", 0.7f, !busy && codeInput.Length >= 4)) JoinRoom();
        if (!string.IsNullOrEmpty(NetSession.Status))
            GUI.Label(new Rect(cx - 220f, board.yMax - 34f, 440f, 24f), NetSession.Status, small);
        if (CraftCard(new Rect(cx - 70f, board.yMax + 14f, 140f, 46f), "뒤로", -0.8f, true, polaLabel)) SetStep(Step.Mode);
    }

    void DrawOptions()
    {
        float cx = Screen.width * panelCenterX, cy = Screen.height * 0.5f + DropOffset();
        var board = new Rect(cx - 250f, cy - 160f, 500f, 320f);
        Bordered(board, Wood, WoodEdge, 5f);
        WoodTitle(cx, board.y - 26f, 220f, "옵션");

        DrawVolume(0, new Rect(board.x + 40f, board.y + 56f, board.width - 80f, 52f), "전체", AudioListener.volume, v => AudioListener.volume = v);
        DrawVolume(1, new Rect(board.x + 40f, board.y + 128f, board.width - 80f, 52f), "음악", Bgm.Volume, v => Bgm.Volume = v);
        DrawVolume(2, new Rect(board.x + 40f, board.y + 200f, board.width - 80f, 52f), "효과음", Sfx.Volume, v => Sfx.Volume = v);

        if (CraftCard(new Rect(cx - 70f, board.yMax + 14f, 140f, 46f), "뒤로", 0.9f, true, polaLabel)) SetStep(Step.Home);
    }

    // 연필 대시 트랙 + 나무 노브 슬라이더(종이 카드 위)
    void DrawVolume(int idx, Rect r, string label, float value, System.Action<float> set)
    {
        Bordered(r, Paper, Ink, 1.8f);
        GUI.Label(new Rect(r.x + 12f, r.y, 70f, r.height), label, polaLabel);
        var track = new Rect(r.x + 92f, r.center.y - 1.5f, r.width - 116f, 3f);
        // 대시 트랙
        float dashW = 9f, gap = 6f;
        for (float x = track.x; x < track.xMax - dashW; x += dashW + gap)
            Fill(new Rect(x, track.y, dashW, track.height), Ink);
        // 노브
        float t = Mathf.Clamp01(value);
        var knob = new Rect(track.x + t * (track.width - 16f), track.center.y - 10f, 16f, 20f);
        Bordered(knob, Wood, Ink, 1.5f);

        // 드래그(Mouse.current — IMGUI 클릭 불안정 회피, PauseMenu 패턴)
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();
        Vector2 gm = new Vector2(mp.x, Screen.height - mp.y);
        var hit = new Rect(track.x - 10f, r.y, track.width + 20f, r.height);
        if (mouse.leftButton.wasPressedThisFrame && hit.Contains(gm)) dragSlider = idx;
        if (!mouse.leftButton.isPressed) { if (dragSlider == idx) dragSlider = -1; }
        if (dragSlider == idx)
            set(Mathf.Clamp01((gm.x - track.x) / (track.width - 16f)));
    }
}
