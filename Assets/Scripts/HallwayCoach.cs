using System.Collections;
using UnityEngine;

/// <summary>
/// ★2026-11-01 복도 호랑이 조교 테이블(사용자 요청):
/// 걷기 안내가 끝난 뒤 stuckSeconds(5분) 동안 복도 끝(goalZ)까지 못 가면 화면에 흰 선택지 상자 두 개가 뜬다.
///   "호랑이 조교 테이블의 도움을 받겠다." → 하얗게 바뀌며 별도의 하얀 방(CoachRoom)으로 옮겨 가고,
///     빨간 조교 모자 테이블이 위에서 떨어져 "어이!" → 같은 방향으로 돌아서 뒷다리를 천천히 접고(W·T) 미는(S·G)
///     개구리 점프를 옆에서 보여 준다 → 직접 해 보게 한 뒤(jumpsNeeded번) 위로 올라가 사라지고 → 원래 자리로 돌아온다.
///   "혼자, 혹은 우리의 힘으로 해결하겠다." → 그냥 닫힘.
/// 조교·방·자리는 전부 씬 오브젝트(Hallway.unity의 DrillCoachEvent 아래) — 여기서는 참조만 한다.
/// </summary>
public class HallwayCoach : MonoBehaviour
{
    [Header("씬 오브젝트")]
    [Tooltip("조교 테이블 루트(처음엔 숨김)")] public Transform coach;
    [Tooltip("조교의 뒷다리 피벗 2개(접고 미는 시범)")] public Transform[] coachBackLegs = new Transform[2];
    [Tooltip("조교의 앞다리 피벗 2개(뛸 때 살짝 따라 움직임)")] public Transform[] coachFrontLegs = new Transform[2];
    [Tooltip("하얀 방에서 플레이어 테이블이 설 자리")] public Transform playerSpot;
    [Tooltip("하얀 방에서 조교가 떨어질 자리")] public Transform coachSpot;

    [Header("언제 나오나")]
    [Tooltip("걷기 안내가 끝난 뒤 이만큼(초) 헤매면 선택지")] public float stuckSeconds = 300f;
    [Tooltip("이 Z까지 가면 잘 가고 있는 것 → 안 나옴(복도 함정 앞)")] public float goalZ = 2f;

    [Header("카메라(옆에서 — 두 테이블이 나란히, 조교 다리는 옆모습)")]
    public float cameraSideDistance = 3.6f, cameraBackShift = 0.8f, cameraUp = 1.2f;

    [Header("연출")]
    public float lineSeconds = 2.4f, fadeSeconds = .5f, dropHeight = 3.6f;
    [Tooltip("시범: 뒷다리를 천천히 접는 시간 / 미는 시간")] public float slowFoldSeconds = 1.3f, slowPushSeconds = 1.1f;
    public float hopHeight = 0.3f, hopDistance = 0.55f;
    [Tooltip("시범에서 뒷다리를 접는 각도")] public float foldDegrees = 42f;
    [Tooltip("실습: 개구리 점프를 이만큼 해야 끝")] public int jumpsNeeded = 2;
    [Tooltip("실습을 못 끝내도 이만큼 지나면 끝(막지 않음)")] public float practiceTimeout = 30f;
    [Tooltip("끝나면 이만큼 위로 올라가며 사라짐(m)")] public float leaveRise = 6f;

    enum Phase { Waiting, Choice, Cutscene, Practice, Leaving, Done }
    Phase phase = Phase.Waiting;
    HallwayStage hallway; KeyTutorial tutorial; TableInput input; Transform board; ArtSceneReset reset; Camera cam;
    float watchStart = -1f;
    string line = ""; float lineAlpha, fade, choiceAlpha;
    int jumps; bool folded; float foldT, up0T = -1f, up1T = -1f, down0T = -1f, down1T = -1f, practiceStart;
    bool prevUp0, prevUp1, prevDown0, prevDown1; float cheerUntil;
    Quaternion[] backRest, frontRest;
    Vector3 returnPos, returnCamForward; CursorLockMode cursorBefore; bool cursorVisibleBefore;
    GUIStyle keyStyle, footStyle;

    public bool Done => phase == Phase.Done;
    public int Jumps => jumps;
    public string PhaseName => phase.ToString();

    void Start()
    {
        hallway = FindAnyObjectByType<HallwayStage>();
        tutorial = FindAnyObjectByType<KeyTutorial>();
        input = FindAnyObjectByType<TableInput>();
        reset = FindAnyObjectByType<ArtSceneReset>();
        if (reset != null) foreach (var ab in reset.GetComponentsInChildren<ArticulationBody>(true)) if (ab.isRoot) { board = ab.transform; break; }
        if (coach != null) coach.gameObject.SetActive(false);
        backRest = Rest(coachBackLegs); frontRest = Rest(coachFrontLegs);
        if (coach == null || board == null || playerSpot == null || coachSpot == null) { Debug.LogWarning("[HallwayCoach] 씬 오브젝트 참조가 비어 있어 조교 이벤트를 끔"); phase = Phase.Done; }
        if (!CheckpointRuntime.Authority) phase = Phase.Done;   // 온라인 게스트는 테이블을 옮길 수 없음 — 호스트·혼자 하기만
    }

    void OnDisable() { if (hallway != null) hallway.CameraHeld = false; }   // 도중에 꺼져도 카메라가 멈춘 채 남지 않게

    static Quaternion[] Rest(Transform[] legs)
    {
        var r = new Quaternion[legs != null ? legs.Length : 0];
        for (int i = 0; i < r.Length; i++) r[i] = legs[i] != null ? legs[i].localRotation : Quaternion.identity;
        return r;
    }

    void Update()
    {
        if (phase == Phase.Done && fade <= 0f) return;
        lineAlpha = Mathf.MoveTowards(lineAlpha, string.IsNullOrEmpty(line) || PauseMenu.InputBlocked ? 0f : 1f, Time.deltaTime / .25f);
        choiceAlpha = Mathf.MoveTowards(choiceAlpha, phase == Phase.Choice && !PauseMenu.InputBlocked ? 1f : 0f, Time.deltaTime / .25f);
        if (phase == Phase.Waiting) Watch();
        else if (phase == Phase.Choice) { JourneyMenuUI.PollInput(); ShowCursor(); }
        else if (Skippable && SkipHint.Pressed()) SkipLesson();   // 스페이스: 수업을 끊고 원래 자리로
        else if (phase == Phase.Practice) Practice();
    }

    // ── 1) 기다리기: 걷기 안내가 끝난 뒤 복도 끝까지 못 가고 헤매는지 본다
    void Watch()
    {
        if (CheatMode.Active || hallway == null || !hallway.IntroFinished || hallway.IsFalling) { watchStart = -1f; return; }
        if (tutorial != null && !tutorial.MoveDone) { watchStart = -1f; return; }
        if (board.position.z >= goalZ) { phase = Phase.Done; return; }   // 잘 가고 있음 → 안 나옴
        if (watchStart < 0f) { watchStart = Time.time; return; }
        if (Time.time - watchStart >= stuckSeconds) OpenChoice();
    }

    // ── 2) 선택지: 화면 가운데 흰 상자 두 개(마우스로 고름)
    void OpenChoice()
    {
        phase = Phase.Choice;
        Freeze(true);
        cursorBefore = Cursor.lockState; cursorVisibleBefore = Cursor.visible;
        ShowCursor();
    }
    static void ShowCursor() { if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    void CloseChoice()
    {
        Cursor.lockState = cursorBefore; Cursor.visible = cursorVisibleBefore;
        JourneyMenuUI.BlockClick();
    }
    void Pick(bool help)
    {
        CloseChoice();
        if (help) StartCoroutine(Lesson());
        else { Freeze(false); phase = Phase.Done; }   // 혼자(또는 우리끼리) 해결 → 조교 안 나옴
    }

    // ── 3) 하얀 방으로 → 조교가 떨어져 "어이!" → 같은 방향으로 돌아 뒷다리 시범 → 실습
    IEnumerator Lesson()
    {
        phase = Phase.Cutscene;
        cam = Camera.main;
        if (hallway != null) hallway.CameraHeld = true;   // 복도는 HallwayStage가 카메라를 직접 따라 움직임 → 잠시 멈춤
        returnPos = board.position;
        returnCamForward = cam != null ? Flat(cam.transform.forward).normalized : Vector3.forward;
        yield return FadeTo(1f);
        Vector3 fwd = Flat(playerSpot.forward).sqrMagnitude > .01f ? Flat(playerSpot.forward).normalized : Vector3.forward;
        MoveTable(playerSpot.position, fwd);
        Freeze(true);
        Vector3 land = coachSpot.position;
        // 카메라: 옆에서 두 테이블을 나란히 — ⚠️옮긴 그 프레임엔 board.position이 아직 예전 자리라 playerSpot 기준으로
        Vector3 mid = Vector3.Lerp(playerSpot.position, land, .5f), right = Vector3.Cross(Vector3.up, fwd);
        Vector3 camPos = mid + right * cameraSideDistance - fwd * cameraBackShift + Vector3.up * cameraUp;
        if (cam != null) cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(mid + fwd * .4f + Vector3.up * .45f - camPos));
        yield return FadeTo(0f);

        // 위에서 뚝 떨어져 착지(살짝 눌렸다 펴짐)
        coach.SetPositionAndRotation(land + Vector3.up * dropHeight, Quaternion.LookRotation(-fwd));
        coach.gameObject.SetActive(true); SetFold(0f, 0f);
        for (float t = 0; t < 1f; t += Time.deltaTime / .55f) { coach.position = land + Vector3.up * dropHeight * (1f - t * t); yield return null; }
        for (float t = 0; t < 1f; t += Time.deltaTime / .25f) { float k = Mathf.Sin(t * Mathf.PI); coach.position = land + Vector3.down * .05f * k; SetFold(-.25f * k, 0f); yield return null; }
        coach.position = land; SetFold(0f, 0f);

        yield return Say(L.T("어이!", "Hey!", "おい！", "喂！"), lineSeconds * .7f);
        // 돌아서서 플레이어와 같은 방향 → 뒷다리가 플레이어 눈앞, 옆 카메라엔 다리 옆모습
        line = L.T("뒷다리로 개구리처럼 뛰는 거다!", "Leap like a frog with your back legs!", "後ろ脚でカエルのように跳ぶんだ！", "用后腿像青蛙一样跳！");
        yield return Turn(Quaternion.LookRotation(fwd), .8f);
        yield return Wait(lineSeconds - .8f);
        // 천천히 시범: 접기 → 밀기, 한 번 더(조금 빠르게)
        for (int n = 0; n < 2; n++)
        {
            float slow = n == 0 ? 1f : .7f;
            line = L.T("W와 T로 뒷다리를 접고,", "Fold the back legs with W and T,", "WとTで後ろ脚をたたみ、", "用 W 和 T 收起后腿，");
            yield return Fold(1f, slowFoldSeconds * slow);
            yield return Wait(.6f * slow);
            line = L.T("S와 G로 힘껏 밀어!", "then push hard with S and G!", "SとGで思いきり押せ！", "再用 S 和 G 用力推！");
            yield return Push(coach.position + fwd * hopDistance, slowPushSeconds * slow);
            yield return Wait(.5f * slow);
        }
        line = "";
        var toPlayer = Flat(board.position - coach.position);
        yield return Turn(Quaternion.LookRotation(toPlayer.sqrMagnitude > .01f ? toPlayer : -fwd), .5f);

        // 조작을 돌려주고 실습(카메라는 테이블 추적으로)
        Freeze(false);
        if (hallway != null) hallway.CameraHeld = false;
        jumps = 0; folded = false; up0T = up1T = down0T = down1T = -1f; practiceStart = Time.time;
        phase = Phase.Practice;
    }

    // ── 4) 실습: 뒷다리 두 개를 같이 접고(W·T) → 같이 밀기(S·G) = 개구리 점프 1번
    void Practice()
    {
        if (input == null) { StartCoroutine(Leave(false)); return; }
        float t = Time.time;
        var a = input.Get((LegControlSet)0); var b = input.Get((LegControlSet)1);
        if (a.up && !prevUp0) up0T = t; if (b.up && !prevUp1) up1T = t;
        if (a.down && !prevDown0) down0T = t; if (b.down && !prevDown1) down1T = t;
        prevUp0 = a.up; prevUp1 = b.up; prevDown0 = a.down; prevDown1 = b.down;
        bool together(float x, float y) => x > 0f && y > 0f && Mathf.Abs(x - y) <= .5f;
        if (!folded && (together(up0T, up1T) || (a.up && b.up))) { folded = true; foldT = t; }
        if (folded && down0T > foldT && down1T > foldT && (together(down0T, down1T) || (a.down && b.down)))
        {
            jumps++; folded = false; up0T = up1T = down0T = down1T = -1f; cheerUntil = t + 1.2f;
            if (jumps >= jumpsNeeded) { StartCoroutine(Leave(true)); return; }
        }
        if (folded && t - foldT > 4f) folded = false;   // 너무 오래 접고만 있으면 다시
        var look = Flat(board.position - coach.position);
        if (look.sqrMagnitude > .01f) coach.rotation = Quaternion.Slerp(coach.rotation, Quaternion.LookRotation(look), Time.deltaTime * 4f);
        if (t - practiceStart > practiceTimeout) StartCoroutine(Leave(false));
    }

    bool Skippable => (phase == Phase.Cutscene || phase == Phase.Practice || phase == Phase.Leaving) && !returning;
    bool returning;
    void SkipLesson()
    {
        StopAllCoroutines();
        line = ""; SetFold(0f, 0f);
        if (coach != null) coach.gameObject.SetActive(false);
        StartCoroutine(ReturnHome());
    }

    // ── 5) 칭찬 → 위로 올라가며 사라짐 → 하얗게 → 원래 자리로
    IEnumerator Leave(bool success)
    {
        phase = Phase.Leaving;
        line = success ? L.T("좋아! 그렇게 앞으로 가는 거다!", "Good! That's how you move forward!", "よし！そうやって前に進むんだ！", "很好！就这样往前走！")
                       : L.T("좋아, 계속 연습해 봐!", "Alright, keep practicing!", "よし、練習を続けろ！", "好，继续练习！");
        yield return Wait(lineSeconds);
        line = "";
        Vector3 from = coach.position; Quaternion rot = coach.rotation;
        for (float t = 0; t < 1f; t += Time.deltaTime / 1.3f)
        {
            float e = t * t;   // 천천히 떠올랐다가 빠르게 위로
            coach.position = from + Vector3.up * leaveRise * e;
            coach.rotation = rot * Quaternion.Euler(0f, 360f * e, 0f);
            SetFold(0f, Mathf.Sin(t * Mathf.PI) * .4f);
            yield return null;
        }
        coach.gameObject.SetActive(false);
        yield return Wait(.4f);
        yield return ReturnHome();
    }

    /// <summary>하얗게 → 원래 자리로 옮김 → 복도 추적 카메라 → 밝아짐.</summary>
    IEnumerator ReturnHome()
    {
        returning = true;
        if (hallway != null) hallway.CameraHeld = true;
        yield return FadeTo(1f);
        MoveTable(returnPos, returnCamForward);
        if (cam != null) cam.transform.SetPositionAndRotation(returnPos + new Vector3(0f, 2.0f, -4.6f), Quaternion.LookRotation(new Vector3(0f, -1.7f, 7.6f)));   // 복도 추적 카메라 자리(옮긴 프레임이라 returnPos 기준)
        if (hallway != null) hallway.CameraHeld = false;
        phase = Phase.Done;
        yield return FadeTo(0f);
    }

    /// <summary>테이블을 그 자리로 옮겨 똑바로 세운다(바라보는 방향 = 카메라 방향이라 카메라를 먼저 맞춤).</summary>
    void MoveTable(Vector3 at, Vector3 facing)
    {
        Freeze(false);
        if (cam != null) cam.transform.rotation = Quaternion.LookRotation(facing);
        reset.ResetPoseAtXZ(at.x, at.z);
    }

    // ── 연출 도우미 ──
    IEnumerator FadeTo(float target) { for (float t = 0; t < 1f; t += Time.deltaTime / fadeSeconds) { fade = Mathf.MoveTowards(fade, target, Time.deltaTime / fadeSeconds); yield return null; } fade = target; }
    IEnumerator Say(string text, float seconds) { line = text; yield return Wait(seconds); }
    static IEnumerator Wait(float seconds) { for (float t = 0; t < seconds; t += Time.deltaTime) yield return null; }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    IEnumerator Turn(Quaternion to, float seconds)
    {
        Quaternion from = coach.rotation;
        for (float t = 0; t < 1f; t += Time.deltaTime / seconds) { coach.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t)); yield return null; }
        coach.rotation = to;
    }

    /// <summary>접힌 뒷다리를 천천히 뒤로 밀며 앞으로 폴짝(포물선) — 진짜 테이블이 미는 모습처럼.</summary>
    IEnumerator Push(Vector3 to, float seconds)
    {
        Vector3 from = coach.position;
        for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
        {
            float push = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 1.6f));             // 다리가 먼저 뒤로 밀고
            float move = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .25f) / .75f));     // 몸이 뒤따라 앞으로
            coach.position = Vector3.Lerp(from, to, move) + Vector3.up * (4f * hopHeight * move * (1f - move));
            SetFold(Mathf.Lerp(1f, -.35f, push) * (1f - Mathf.Clamp01((t - .7f) / .3f)), Mathf.Sin(move * Mathf.PI) * .5f);
            yield return null;
        }
        coach.position = to; SetFold(0f, 0f);
    }

    IEnumerator Fold(float target, float seconds)
    {
        for (float t = 0; t < 1f; t += Time.deltaTime / seconds) { SetFold(Mathf.SmoothStep(0, target, t), 0f); yield return null; }
        SetFold(target, 0f);
    }

    /// <summary>back = 뒷다리 접힘(1 = 발끝이 몸 아래 앞쪽, 음수 = 뒤로 민 상태), front = 앞다리 앞으로 뻗음(0~1).</summary>
    void SetFold(float back, float front)
    {
        for (int i = 0; i < coachBackLegs.Length; i++) if (coachBackLegs[i] != null) coachBackLegs[i].localRotation = backRest[i] * Quaternion.Euler(-foldDegrees * back, 0f, 0f);
        for (int i = 0; i < coachFrontLegs.Length; i++) if (coachFrontLegs[i] != null) coachFrontLegs[i].localRotation = frontRest[i] * Quaternion.Euler(-foldDegrees * .6f * front, 0f, 0f);
    }

    void Freeze(bool on)
    {
        if (!CheckpointRuntime.Authority || board == null) return;
        var ab = board.GetComponent<ArticulationBody>(); if (ab == null) return;
        ab.immovable = on;
        if (on) { ab.linearVelocity = Vector3.zero; ab.angularVelocity = Vector3.zero; }
    }

    // ── 화면 ──
    void OnGUI()
    {
        GUI.depth = -1000;   // 선택지·하얀 전환이 다른 화면(튜토리얼 말풍선 등)보다 위에
        if (phase == Phase.Choice) DrawChoice();
        if (Event.current.type != EventType.Repaint) return;
        DrawCoachBubble();
        if (Skippable) SkipHint.Draw();
        if (fade > .001f) UiKit.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, fade));
    }

    /// <summary>선택지 = 화면 가운데 흰 상자 두 개(마우스를 올리면 살짝 떠오름, 누르면 고름).</summary>
    void DrawChoice()
    {
        if (PauseMenu.InputBlocked) return;
        UiKit.ApplyKoreanFont();
        JourneyMenuUI.Begin();
        try
        {
            JourneyMenuUI.Fill(new Rect(-400, -400, 2400, 1700), new Color(0f, 0f, 0f, .22f * choiceAlpha));
            var help = new Rect(420f, 330f, 760f, 104f);
            var alone = new Rect(420f, 466f, 760f, 104f);
            bool can = phase == Phase.Choice && choiceAlpha > .6f;
            JourneyMenuUI.BeginCard(help);
            JourneyMenuUI.Text(help, L.T("호랑이 조교 테이블의 도움을 받겠다.", "I'll get help from the tiger drill table.", "トラ教官テーブルの助けを借りる。", "接受老虎教官桌子的帮助。"), JourneyMenuUI.TM, null, TextAnchor.MiddleCenter);
            JourneyMenuUI.EndCard();
            JourneyMenuUI.BeginCard(alone);
            JourneyMenuUI.Text(alone, L.T("혼자, 혹은 우리의 힘으로 해결하겠다.", "I'll solve it alone, or with all of us.", "ひとりで、あるいはみんなの力で解決する。", "靠自己，或靠我们大家来解决。"), JourneyMenuUI.TM, null, TextAnchor.MiddleCenter);
            JourneyMenuUI.EndCard();
            if (can && JourneyMenuUI.Click(help)) Pick(true);
            else if (can && JourneyMenuUI.Click(alone)) Pick(false);
        }
        finally { JourneyMenuUI.End(); }
    }

    // ── 조교 말풍선(머리 위) ──
    void DrawCoachBubble()
    {
        if ((phase != Phase.Cutscene && phase != Phase.Practice && phase != Phase.Leaving) || coach == null || !coach.gameObject.activeInHierarchy) return;
        bool practice = phase == Phase.Practice;
        float alpha = practice ? (PauseMenu.InputBlocked ? 0f : 1f) : lineAlpha;
        if (alpha <= .01f) return;
        var c = Camera.main; if (c == null) return;
        UiKit.ApplyKoreanFont();
        ThoughtBubbleUI.Begin();
        if (ThoughtBubbleUI.GuiPoint(c, coach.position + Vector3.up * 1.25f, out var head))
        {
            float width = practice ? 660f : 640f, height = ThoughtBubbleUI.WideHeight(width);
            var rect = PlaceBubble(c, head, width, height);
            ThoughtBubbleUI.DrawPointingTail(rect, head, alpha);
            if (!practice) ThoughtBubbleUI.DrawWideBubble(rect, alpha, line, 34);
            else DrawPracticeBubble(rect, alpha);
        }
        ThoughtBubbleUI.End();
    }

    /// <summary>조교 머리 위쪽에 두되, 플레이어 테이블(실습 중 보여야 함)을 가리지 않는 자리를 고른다.</summary>
    Rect PlaceBubble(Camera c, Vector2 head, float width, float height)
    {
        Rect avoid = new Rect(-1, -1, 0, 0);
        if (board != null && ThoughtBubbleUI.GuiPoint(c, board.position + Vector3.up * .4f, out var table)) avoid = new Rect(table.x - 260f, table.y - 200f, 520f, 380f);
        var tries = new[]
        {
            new Vector2(head.x - width * .5f, head.y - height - 110f),   // 바로 위
            new Vector2(head.x - width - 80f, head.y - height - 60f),    // 왼쪽 위
            new Vector2(head.x + 80f, head.y - height - 60f),            // 오른쪽 위
            new Vector2(head.x - width - 120f, head.y - height * .5f),   // 왼쪽
        };
        Rect best = default; bool found = false;
        foreach (var p in tries)
        {
            var r = new Rect(Mathf.Clamp(p.x, 24f, ThoughtBubbleUI.VW - width - 24f), Mathf.Clamp(p.y, 90f, ThoughtBubbleUI.VH - height - 24f), width, height);
            if (!found) { best = r; found = true; }
            if (!r.Overlaps(avoid) && !r.Contains(head)) { best = r; break; }
        }
        return best;
    }

    void DrawPracticeBubble(Rect rect, float alpha)
    {
        ThoughtBubbleUI.DrawWideBubble(rect, alpha);
        if (keyStyle == null) { keyStyle = HandUI.Style(24, Color.black, thin: true); footStyle = HandUI.Style(20, Color.black, thin: true); footStyle.wordWrap = true; }
        var content = ThoughtBubbleUI.WideContent(rect);
        var old = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
        string head = Time.time < cheerUntil ? L.T("좋아! 한 번 더!", "Nice! Once more!", "いいぞ！もう一回！", "很好！再来一次！")
                                             : L.T("해 봐! 개구리 점프", "Your turn! Frog jump", "やってみろ！カエル跳び", "试试看！青蛙跳");
        ThoughtBubbleUI.FittedLabel(new Rect(content.x, content.y, content.width, 30), head, footStyle, 24);
        var a = input != null ? input.Get((LegControlSet)0) : default; var b = input != null ? input.Get((LegControlSet)1) : default;
        float size = 40f, y = content.y + 38f, cx = content.center.x;
        // 접기(W T) → 밀기(S G)
        DrawKey(new Rect(cx - 190f, y, size, size), "W", 0, a.up, alpha);
        DrawKey(new Rect(cx - 144f, y, size, size), "T", 1, b.up, alpha);
        ThoughtBubbleUI.FittedLabel(new Rect(cx - 96f, y, 72f, size), folded ? L.T("접음!", "folded!", "たたんだ！", "收好了！") : L.T("접기", "fold", "たたむ", "收起"), footStyle, 20);
        DrawKey(new Rect(cx + 6f, y, size, size), "S", 0, a.down, alpha);
        DrawKey(new Rect(cx + 52f, y, size, size), "G", 1, b.down, alpha);
        ThoughtBubbleUI.FittedLabel(new Rect(cx + 100f, y, 72f, size), L.T("밀기", "push", "押す", "推"), footStyle, 20);
        ThoughtBubbleUI.FittedLabel(new Rect(content.x, content.yMax - 28f, content.width, 28f), jumps + " / " + jumpsNeeded, footStyle, 22);
        GUI.color = old;
    }

    void DrawKey(Rect r, string label, int slot, bool pressed, float alpha)
    {
        if (pressed) r.y += 3f;
        Color tint = Color.Lerp(Color.white, LegPalette.BySlot[slot], pressed ? .7f : .2f);
        var old = GUI.color; GUI.color = new Color(tint.r, tint.g, tint.b, alpha); HandUI.Paper(r, HandUI.BoxTile);
        tint.a = alpha; UiKit.Fill(new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10), tint);
        var stripe = LegPalette.BySlot[slot]; stripe.a = alpha; UiKit.Fill(new Rect(r.x + 7, r.yMax - 8, r.width - 14, 4), stripe);
        GUI.color = new Color(1, 1, 1, alpha); GUI.Label(r, label, keyStyle); GUI.color = old;
    }

    void OnDrawGizmosSelected()
    {
        if (playerSpot != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireCube(playerSpot.position + Vector3.up * .4f, new Vector3(1.4f, .8f, 1.4f)); }
        if (coachSpot != null) { Gizmos.color = Color.red; Gizmos.DrawWireCube(coachSpot.position + Vector3.up * .4f, new Vector3(1.4f, .8f, 1.4f)); }
    }
}
