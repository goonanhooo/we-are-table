using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맵 타이머(2026-07-08): 씬 시작부터 시간을 재서 상단 중앙 칩으로 표시하고,
/// GoalTrigger가 Clear()를 부르면 정지 + 클리어 오버레이(이번 시간/최고 기록).
/// 최고 기록은 PlayerPrefs("besttime_<씬이름>")에 영구 저장.
/// 씬 GO로 배치(Track/Moon — 다른 맵도 이 컴포넌트+GoalTrigger만 놓으면 재사용).
/// </summary>
public class StageTimer : MonoBehaviour
{
    public static StageTimer Instance { get; private set; }
    /// <summary>★2026-09-05 컷씬 동안 칩 숨김(정적 — 컷씬이 켜고 끔, Awake에서 초기화).</summary>
    public static bool Suppress;

    [Tooltip("클리어 오버레이에 표시할 맵 이름(비면 씬 이름).")]
    public string mapLabel = "";

    float startTime;
    bool cleared;
    float clearTime;
    bool newBest;
    float best = -1f;
    bool checkpoint;

    public bool Cleared => cleared;
    public bool CheckpointPassed => checkpoint;
    public void PassCheckpoint() { checkpoint = true; }

    void Awake()
    {
        Instance = this;
        Suppress = false;
        startTime = Time.time;
        string k = Key();
        if (PlayerPrefs.HasKey(k)) best = PlayerPrefs.GetFloat(k);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>컷씬이 끝나고 조작이 시작될 때 0부터 다시 재기.</summary>
    public void RestartClock() { if (!cleared) startTime = Time.time; }

    string Key() => "besttime_" + SceneManager.GetActiveScene().name;
    string Label() => string.IsNullOrEmpty(mapLabel) ? SceneManager.GetActiveScene().name : mapLabel;

    public void Clear()
    {
        if (cleared) return;
        cleared = true;
        clearTime = Time.time - startTime;
        if (best < 0f || clearTime < best)
        {
            best = clearTime; newBest = true;
            PlayerPrefs.SetFloat(Key(), best);
            PlayerPrefs.Save();
        }
    }

    public static string Fmt(float t)
    {
        int m = (int)(t / 60f);
        return string.Format("{0}:{1:00.00}", m, t - m * 60);
    }

    void OnGUI()
    {
        UiKit.ApplyKoreanFont();   // 웹(WebGL) 한글 폰트
        if (Suppress && !cleared) return;
        float t = cleared ? clearTime : Time.time - startTime;
        var chip = UiKit.Chip(Screen.width * 0.5f, 10f, 150f, 34f);
        GUI.Label(chip, Fmt(t), UiKit.Btn);
        if (!cleared) return;

        float w = 460f, h = 180f;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.26f, w, h);
        UiKit.Panel(r, 0.97f);
        GUI.Label(new Rect(r.x, r.y + 16f, w, 42f), "CLEAR!", UiKit.Title);
        GUI.Label(new Rect(r.x, r.y + 70f, w, 30f), Label() + "  —  " + Fmt(clearTime), UiKit.Btn);
        GUI.Label(new Rect(r.x, r.y + 108f, w, 26f), newBest ? "★ 신기록!" : "최고 기록  " + Fmt(best), UiKit.Center);
        GUI.Label(new Rect(r.x, r.y + 142f, w, 24f), "계속 돌아다녀도 돼요 · ESC = 메뉴", UiKit.Small);
    }
}
