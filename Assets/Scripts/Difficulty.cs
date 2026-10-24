using UnityEngine;

/// <summary>
/// ★2026-09-05 난이도. 시작 화면에서 고른다 — 1인: LevelSelect '난이도 선택' 단계, 멀티: 로비에서 호스트가 선택(NetLegLink 동기화).
/// - Normal(보통): 바로서기('1')가 맵당 3회(ArtSceneReset.poseResetMax — 정글 코인 더미로 최대치까지 회복).
/// - Coward(겁쟁이): 바로서기 무제한.
/// 로컬 값은 PlayerPrefs "difficulty"(0=보통, 1=겁쟁이). 멀티 세션 중엔 NetOverride(호스트 값)가 전원에서 우선.
/// </summary>
public static class Difficulty
{
    public enum Mode { Normal = 0, Coward = 1 }

    const string Key = "difficulty";
    static int cached = -1;
    static int netOverride = -1;

    /// <summary>이 기기에 저장된 선택(시작 화면 기본값).</summary>
    public static Mode Local
    {
        get
        {
            if (cached < 0) cached = Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, 1);
            return (Mode)cached;
        }
        set
        {
            cached = (int)value;
            PlayerPrefs.SetInt(Key, cached);
            PlayerPrefs.Save();
        }
    }

    /// <summary>멀티 세션 중 호스트가 고른 값(-1=없음). NetLegLink가 세팅/해제.</summary>
    public static int NetOverride
    {
        get => netOverride;
        set => netOverride = value < 0 ? -1 : Mathf.Clamp(value, 0, 1);
    }

    /// <summary>지금 게임에 적용되는 난이도.</summary>
    public static Mode Current => netOverride >= 0 ? (Mode)netOverride : Local;

    /// <summary>겁쟁이 난이도(바로서기 무제한) 여부.</summary>
    public static bool Coward => Current == Mode.Coward;

    /// <summary>표시 이름. plural = 멀티(로비)에서 "겁쟁이들을 위한".</summary>
    public static string Name(Mode m, bool plural)
    {
        if (m == Mode.Coward)
            return plural ? L.T("겁쟁이들을 위한 난이도", "For the cowards") : L.T("겁쟁이를 위한 난이도", "For cowards");
        return L.T("보통 난이도", "Normal");
    }

    public static string Desc(Mode m)
    {
        if (m == Mode.Coward)
            return L.T("바로서기(1) 무제한", "Unlimited stand-ups (1)", "起き上がり（1）無制限", "扶正（1）无限次");
        return L.T("바로서기(1) 맵마다 3회", "3 stand-ups per map (1)", "起き上がり（1）各マップ3回", "扶正（1）每张地图3次");
    }
}
