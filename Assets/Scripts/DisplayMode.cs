using UnityEngine;

/// <summary>
/// ★2026-08-17 화면 모드(사용자 요청 "실제 게임처럼"): 기본 = 보더리스 전체화면(모니터 네이티브 해상도
/// = 어떤 화면 비율이든 자동 대응). 설정에서 창 모드 전환 가능(1600×900, 크기 조절 허용) + PlayerPrefs 유지.
/// 에디터에선 게임 뷰라 해상도 변경이 무의미 — 저장만 하고 적용은 빌드에서.
/// </summary>
public static class DisplayMode
{
    const string Key = "display.windowed";

    public static bool Windowed
    {
        get => PlayerPrefs.GetInt(Key, 0) == 1;
        private set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => Apply(Windowed);

    public static void Toggle() => Apply(!Windowed);

    public static void Apply(bool windowed)
    {
        Windowed = windowed;
        if (Application.isEditor) return;
        if (windowed)
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
        else
            Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
    }

    /// <summary>설정 메뉴 표기: "화면: 전체화면 / display: fullscreen"</summary>
    public static string Label()
        => L.T("화면: ", "Display: ") + (Windowed ? L.T("창 모드", "windowed") : L.T("전체화면", "fullscreen"));
}
