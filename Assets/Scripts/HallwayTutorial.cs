using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★2026-08-17 홀웨이 텍스트 튜토리얼(사용자 요청): 컷씬 → 조작법 이미지(ControlsGuide)가 끝난 뒤
/// 손글씨 안내 3단계를 순서대로 보여준다.
///  1) 걷기 — 다리별 키 소개(테이블이 좀 걸으면 다음으로)
///  2) 바로서기 — 우상단 파이 소개 + 맵당 3회 제한(파이 바로 아래에 표시)
///  3) 다리 색 — ESC 설정에서 켤 수 있음(설정을 열면 즉시 완료)
/// 표시는 홀웨이 씬 전용(씬에 배치), 매 입장마다 나온다(첫 맵 = 튜토리얼 맵).
/// </summary>
public class HallwayTutorial : MonoBehaviour
{
    [Tooltip("조작법 이미지가 안 뜨는 경우를 대비한 시작 폴백(초)")] public float fallbackStart = 25f;

    int step;                 // 0=대기 1=걷기 2=바로서기 3=색 옵션 4=끝
    public int Step => step;  // 검증/디버그용 읽기 전용
    float stepT;              // 현재 단계 경과(스케일드 — 일시정지 중 멈춤)
    bool guideSeen;           // ControlsGuide가 켜진 것을 봤나
    float bornT;
    Transform table;
    Vector3 lastPos;
    float walked;             // 1단계: 누적 이동 거리(XZ)
    ControlsGuide guide;
    PauseMenu pause;
    GUIStyle txt;

    void Start()
    {
        bornT = Time.time;
        guide = Object.FindAnyObjectByType<ControlsGuide>(FindObjectsInactive.Include);
        pause = Object.FindAnyObjectByType<PauseMenu>();
        var b = GameObject.Find("Table");
        if (b != null) { table = b.transform; lastPos = table.position; }
    }

    void Update()
    {
        if (step >= 4) return;

        // 0단계: 조작법 이미지가 지나가길 기다림(안 뜨면 폴백 시간 후 시작)
        if (step == 0)
        {
            if (guide != null && guide.gameObject.activeSelf) guideSeen = true;
            bool guideDone = guideSeen && guide != null && !guide.gameObject.activeSelf;
            if (guideDone || Time.time - bornT > fallbackStart)
            {
                step = 1; stepT = 0f;
                if (table != null) lastPos = table.position;   // 컷씬 동안의 이동이 걷기 판정에 섞이지 않게
            }
            return;
        }

        if (Time.timeScale == 0f) return;   // 일시정지 중엔 멈춤(3단계 완료 판정은 아래에서)
        stepT += Time.deltaTime;

        var kb = Keyboard.current;
        switch (step)
        {
            case 1:   // 걷기 — 4m쯤 걸으면(최소 5초 표시) 또는 16초 후 다음
                if (table != null)
                {
                    Vector3 d = table.position - lastPos; d.y = 0f;
                    walked += d.magnitude; lastPos = table.position;
                }
                if ((stepT > 5f && walked > 4f) || stepT > 16f) Next();
                break;
            case 2:   // 바로서기 — 10초 또는 1 키 사용 시 다음
                if (stepT > 10f || (kb != null && kb.digit1Key.wasPressedThisFrame)) Next();
                break;
            case 3:   // 색 옵션 — 10초 후 끝(설정을 열어보면 즉시 완료)
                if (stepT > 10f) step = 4;
                break;
        }
        if (step == 3 && pause != null && pause.IsOpen) step = 4;   // 설정을 열었다 = 배움 완료
    }

    void Next() { step++; stepT = 0f; }

    void OnGUI()
    {
        if (step < 1 || step >= 4 || Time.timeScale == 0f) return;
        UiKit.ApplyKoreanFont();
        if (txt == null) txt = HandUI.Style(26, HandUI.Ink, TextAnchor.MiddleCenter);   // 로비 항목과 같은 손글씨 톤

        // 페이드 인/아웃(각 단계 앞뒤 0.4초)
        float a = Mathf.Clamp01(stepT / 0.4f);
        var pc = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, a);

        if (step == 1)
        {
            string s = L.T(
                "네 다리에는 각자 키가 있어요 — WASD · TFGH · IJKL · 방향키\n위아래 = 앞뒤로 휘두르기, 좌우 = 다리 돌리기. 걸어보세요!",
                "each leg has its own keys — WASD · TFGH · IJKL · arrows\nup/down = swing, left/right = spin. try walking!");
            Hint(BottomRect(s), s);
        }
        else if (step == 2)
        {
            string s = L.T(
                "오른쪽 위 파이 = 바로서기\n뒤집히면 1 키로 자세를 다시 잡아요 — 맵마다 3번뿐!",
                "the pie up here = pose fixes\npress 1 to stand back up — only 3 per map!");
            // 우상단 파이(지름 ~91px, y14~105) 바로 아래 — 크기는 텍스트 실측(잘림 방지)
            var sz = Measure(s);
            Hint(new Rect(Screen.width - sz.x - 14f, 112f, sz.x, sz.y), s);
        }
        else if (step == 3)
        {
            string s = L.T(
                "어느 다리가 어느 키인지 헷갈리면\nESC 설정에서 '다리 색 표시'를 켜보세요",
                "not sure which leg is which?\nturn on 'leg colors' in the ESC options");
            Hint(BottomRect(s), s);
        }

        GUI.color = pc;
    }

    /// <summary>텍스트 실측 크기(+여백) — 줄별 최대 폭 × 전체 높이.</summary>
    Vector2 Measure(string text)
    {
        float w = 0f;
        foreach (var line in text.Split('\n'))
            w = Mathf.Max(w, txt.CalcSize(new GUIContent(line)).x);
        w += 44f;
        float h = txt.CalcHeight(new GUIContent(text), w) + 20f;
        return new Vector2(w, h);
    }

    Rect BottomRect(string text)
    {
        var sz = Measure(text);
        return new Rect((Screen.width - sz.x) * 0.5f, Screen.height - sz.y - 46f, sz.x, sz.y);
    }

    // ★로비 손글씨 테마(사용자 요청): 배경 박스 없이 맨 손글씨 + 손그림 밑줄(HandUI.Underline)
    void Hint(Rect r, string text)
    {
        UiKit.Fill(r, new Color(1f, 1f, 1f, 0.55f));   // 3D 위 가독용으로만 아주 옅게
        GUI.Label(r, text, txt);
        if (HandUI.Underline != null)
        {
            float tw = Mathf.Min(r.width * 0.62f, 430f);
            GUI.DrawTexture(new Rect(r.center.x - tw * 0.5f, r.yMax - 9f, tw, 12f),
                HandUI.Underline, ScaleMode.StretchToFill);
        }
    }
}
