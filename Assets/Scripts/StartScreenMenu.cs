using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 시작 메뉴 — 왼쪽 세로 버튼 4개(시작하기=주요 검정 / 함께하기·설정·나가기=보통, 웹에선 나가기 숨김).
/// ★2026-10-25 '지우개 대소동' 규칙: 손그림 끓는 테두리 상자 + 호버 시 떠오름, 색 없음(JourneyMenuUI 공용).
/// 언어 선택은 설정 메뉴에서만 제공.
/// </summary>
public class StartScreenMenu : MonoBehaviour
{
    [Tooltip("버튼 왼쪽 위(1600×900 캔버스 단위)")] public Vector2 origin = new Vector2(112f, 400f);
    public float buttonWidth = 416f;

    LevelSelect levelSelect;
    PauseMenu pauseMenu;

    static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;
    int ItemCount => IsWeb ? 3 : 4;

    void Start()
    {
        levelSelect = Object.FindAnyObjectByType<LevelSelect>();
        pauseMenu = Object.FindAnyObjectByType<PauseMenu>();
    }

    // ★멀티 세션(로비) 중엔 홈 메뉴 숨김 — 로비가 화면을 이어받는다(2026-07-14)
    static bool InNetSession
    {
        get
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm != null && (nm.IsHost || nm.IsConnectedClient);
        }
    }

    public bool HomeVisible => MenuVisible;
    bool MenuVisible => !LetterOverlay.IsOpen &&
        !(levelSelect != null && levelSelect.Showing) &&
        !(pauseMenu != null && pauseMenu.IsOpen) &&
        !InNetSession;

    string ItemLabel(int i)
    {
        switch (i)
        {
            case 0: return L.T("시작하기", "Play", "はじめる", "开始");
            case 1: return L.T("함께하기", "Play together", "いっしょに遊ぶ", "一起玩");
            case 2: return L.T("설정", "Options", "設定", "设置");
            default: return L.T("나가기", "Quit", "終了", "退出");
        }
    }

    public Rect ItemRect(int i) => new Rect(origin.x, origin.y + i * (JourneyMenuUI.HM + JourneyMenuUI.S2), buttonWidth, JourneyMenuUI.HM);

    void Update()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        JourneyMenuUI.PollInput();
    }

    void OnGUI()
    {
        if (LetterOverlay.IsOpen || !MenuVisible) return;
        JourneyMenuUI.Begin("home", "home");
        try
        {
            for (int i = 0; i < ItemCount; i++)
            {
                if (!JourneyMenuUI.Button(ItemRect(i), ItemLabel(i), true, i == 0)) continue;
                switch (i)
                {
                    case 0: if (levelSelect != null) levelSelect.OpenStage(); break;    // 1인 → 스테이지 선택(뒤로=홈)
                    case 1: if (levelSelect != null) levelSelect.OpenMulti(); break;    // 함께하기 → 멀티 모드
                    case 2: if (pauseMenu != null) pauseMenu.OpenMenu(); break;
                    case 3:
                        Application.Quit();                  // ★나가기 = 게임 종료(사용자 요청)
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;   // 에디터: 플레이 정지
#endif
                        break;
                }
            }
            // 좌하단 버전(출시작 디테일)
            JourneyMenuUI.Note(new Rect(origin.x, 900f - JourneyMenuUI.S5 - 32f, 300f, 32f), "v0.9");
        }
        finally { JourneyMenuUI.End(); }
    }
}
