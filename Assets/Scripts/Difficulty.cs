using UnityEngine;

/// <summary>
/// ★2026-09-05 난이도. 시작 화면에서 고른다 — 1인: LevelSelect '난이도 선택' 단계, 멀티: 로비에서 호스트가 선택(NetLegLink 동기화).
/// - Normal(보통): 바로서기('1')가 맵당 3회(ArtSceneReset.poseResetMax) + 길에 놓인 링(PoseRing)을 먹으면 1회씩 누적 충전(상한 없음).
/// - Coward(겁쟁이): 바로서기 무제한, 링은 숨김.
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
            return plural ? L.T("겁쟁이들을 위한 난이도", "for the cowards") : L.T("겁쟁이를 위한 난이도", "for cowards");
        return L.T("보통 난이도", "normal");
    }

    public static string Desc(Mode m)
    {
        if (m == Mode.Coward)
            return L.T("바로서기(1 키)를 무한으로 쓸 수 있어요", "unlimited pose fixes (1 key)");
        return L.T("바로서기(1 키)는 맵마다 도넛 3개 — 길에 있는 링을 먹으면 하나씩 더 채워져요",
                   "3 donuts of pose fixes per map — rings on the way add one each");
    }
}
