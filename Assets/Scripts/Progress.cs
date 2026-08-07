using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ★2026-07-14 맵 진행(클리어) 시스템 — 클리어 기준 = "다음 씬으로 가면 클리어"(사용자 정의).
/// 게임 맵에서 다른 게임 맵으로 씬이 넘어가면 이전 맵을 클리어 처리(PlayerPrefs).
/// 같은 씬 재로드(F1 다시 시작)나 시작 화면 복귀는 클리어가 아니다.
/// 스테이지 선택은 IsUnlocked로 잠금: 시작(Hallway)만 처음부터, 이후는 이전 맵 클리어 시 해금.
/// </summary>
public static class Progress
{
    // 진행 체인(스테이지 선택 기본 목록 순서) — ★2026-07-15 액자 맵(FrameHall)을 정글과 빙산 사이에 삽입
    static readonly string[] Chain = { "Hallway", "Jungle", "FrameHall", "IceMountain" };
    static readonly string[] MenuScenes = { "StartScreen", "NewStartScreen" };

    static string prevScene;

    public static bool IsCleared(string scene) => PlayerPrefs.GetInt("clear." + scene, 0) == 1;

    public static void MarkCleared(string scene)
    {
        if (IsCleared(scene)) return;
        PlayerPrefs.SetInt("clear." + scene, 1);
        PlayerPrefs.Save();
        Debug.Log("[Progress] cleared: " + scene);
    }

    /// <summary>체인 첫 맵과 체인 밖 맵은 항상 열림, 나머지는 이전 맵 클리어 시.</summary>
    public static bool IsUnlocked(string scene)
    {
        int i = System.Array.IndexOf(Chain, scene);
        if (i <= 0) return true;
        return IsCleared(Chain[i - 1]);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        prevScene = SceneManager.GetActiveScene().name;
        SceneManager.sceneLoaded += OnLoaded;
    }

    static void OnLoaded(Scene s, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        string cur = s.name;
        // 게임 맵 → '다른' 게임 맵 = 이전 맵 클리어 (재시작·메뉴 복귀 제외)
        if (prevScene != cur
            && System.Array.IndexOf(Chain, prevScene) >= 0
            && System.Array.IndexOf(MenuScenes, cur) < 0)
            MarkCleared(prevScene);
        prevScene = cur;
    }
}
