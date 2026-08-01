using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ★2026-07-13 B안 시작 메뉴 — 좌측 세로 손글씨 텍스트 메뉴(구 나무 표지판 대체).
/// 항목: 시작하기(1인 스테이지 선택) / 함께하기(멀티 모드) / 설정(PauseMenu) / 나가기(웹 빌드에선 숨김).
/// 호버 = 부드러운 확대 + 손그림 밑줄(스윽), 클릭 = Mouse.current(PauseMenu 패턴 — GUI.Button 안 먹음).
/// 우상단 = 한/영 토글(L.Korean). 레벨선택/옵션이 열려 있으면 숨김.
/// </summary>
public class StartScreenMenu : MonoBehaviour
{
    [Tooltip("메뉴 왼쪽 여백(화면 비율)")] public float leftX = 0.09f;
    [Tooltip("첫 항목 세로 위치(화면 비율)")] public float topY = 0.44f;
    public float itemSpacing = 84f;
    public int fontSize = 46;

    LevelSelect levelSelect;
    PauseMenu pauseMenu;
    GUIStyle itemStyle, langStyle, verStyle;
    readonly float[] scale = { 1f, 1f, 1f, 1f };
    float ulWidth;   // 밑줄 스윽 애니메이션(0→목표 폭)
    int hover = -1;

    static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;
    int ItemCount => IsWeb ? 3 : 4;

    void Start()
    {
        levelSelect = Object.FindAnyObjectByType<LevelSelect>();
        pauseMenu = Object.FindAnyObjectByType<PauseMenu>();
    }

    bool MenuVisible =>
        !(levelSelect != null && levelSelect.Showing) &&
        !(pauseMenu != null && pauseMenu.IsOpen);

    string ItemLabel(int i)
    {
        switch (i)
        {
            case 0: return L.T("시작하기", "play");
            case 1: return L.T("함께하기", "play together");
            case 2: return L.T("설정", "options");
            default: return L.T("나가기", "quit");
        }
    }

    Rect ItemRect(int i)
    {
        float x = Screen.width * leftX;
        float y = Screen.height * topY + i * itemSpacing;
        return new Rect(x, y, 420f, 70f);
    }

    Rect LangRect() => new Rect(Screen.width - 190f, 18f, 172f, 44f);

    void Update()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (!MenuVisible) { hover = -1; return; }

        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 p = mouse.position.ReadValue();
        Vector2 gm = new Vector2(p.x, Screen.height - p.y);   // 좌하 원점 → GUI 좌상 원점

        hover = -1;
        for (int i = 0; i < ItemCount; i++)
            if (ItemRect(i).Contains(gm)) { hover = i; break; }

        // 확대 lerp(부드럽게)
        for (int i = 0; i < ItemCount; i++)
        {
            float target = hover == i ? 1.13f : 1f;
            scale[i] = Mathf.Lerp(scale[i], target, Time.unscaledDeltaTime * 12f);
        }
        ulWidth = Mathf.Lerp(ulWidth, hover >= 0 ? 1f : 0f, Time.unscaledDeltaTime * 14f);

        if (!mouse.leftButton.wasPressedThisFrame) return;
        if (LangRect().Contains(gm)) { L.Korean = !L.Korean; return; }
        if (hover < 0) return;
        switch (hover)
        {
            case 0: if (levelSelect != null) levelSelect.OpenStage(); break;    // 1인 → 스테이지 선택(뒤로=홈)
            case 1: if (levelSelect != null) levelSelect.OpenMulti(); break;    // 함께하기 → 멀티 모드
            case 2: if (pauseMenu != null) pauseMenu.OpenMenu(); break;
            case 3: Application.Quit(); break;
        }
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트(스킨 기본)
        if (!MenuVisible) return;
        if (itemStyle == null)
        {
            itemStyle = HandUI.Style(fontSize, HandUI.Ink, TextAnchor.MiddleLeft);
            langStyle = HandUI.Style(26, HandUI.InkSoft, TextAnchor.MiddleCenter);
            verStyle = HandUI.Style(18, new Color(0.55f, 0.55f, 0.55f, 1f), TextAnchor.MiddleLeft);
        }

        for (int i = 0; i < ItemCount; i++)
        {
            var r = ItemRect(i);
            var m = GUI.matrix;
            // 호버 확대 + 아주 살짝 기울임(손그림 감성)
            GUIUtility.ScaleAroundPivot(Vector2.one * scale[i], new Vector2(r.x, r.center.y));
            if (hover == i) GUIUtility.RotateAroundPivot(-1.2f, new Vector2(r.x, r.center.y));
            GUI.Label(r, ItemLabel(i), itemStyle);
            // 손그림 밑줄(호버 항목만, 스윽 등장)
            if (hover == i && HandUI.Underline != null && ulWidth > 0.05f)
            {
                float textW = itemStyle.CalcSize(new GUIContent(ItemLabel(i))).x;
                GUI.DrawTexture(new Rect(r.x, r.y + 56f, textW * ulWidth, 16f), HandUI.Underline, ScaleMode.StretchToFill);
            }
            GUI.matrix = m;
        }

        // 우상단 언어 토글(맨글씨 + 호버 밑줄 — 홈과 통일)
        var lr = LangRect();
        bool lh = lr.Contains(Event.current.mousePosition);
        GUI.Label(lr, L.T("한국어 → English", "English → 한국어"), langStyle);
        if (lh && HandUI.Underline != null)
            GUI.DrawTexture(new Rect(lr.x + 10f, lr.yMax - 8f, lr.width - 20f, 12f), HandUI.Underline, ScaleMode.StretchToFill);

        // 좌하단 버전(출시작 디테일)
        GUI.Label(new Rect(Screen.width * leftX, Screen.height - 34f, 300f, 24f), "v0.9", verStyle);
    }
}
