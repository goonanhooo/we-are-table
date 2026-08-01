using UnityEngine;

/// <summary>
/// ★2026-07-13 시작 화면 한/영 로컬라이즈(B안 리디자인). PlayerPrefs에 저장, 기본=한국어.
/// 범위: 시작 화면 메뉴(StartScreenMenu/LevelSelect/타이틀). 인게임 HUD는 범위 밖.
/// </summary>
public static class L
{
    const string Key = "lang.korean";
    static int cached = -1;

    public static bool Korean
    {
        get
        {
            if (cached < 0) cached = PlayerPrefs.GetInt(Key, 1);
            return cached == 1;
        }
        set
        {
            cached = value ? 1 : 0;
            PlayerPrefs.SetInt(Key, cached);
            PlayerPrefs.Save();
        }
    }

    public static string T(string kr, string en) => Korean ? kr : en;
}
