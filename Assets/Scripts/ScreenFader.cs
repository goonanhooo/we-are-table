using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 전화면 검정 페이드 오버레이 — 씬 전환을 부드럽게. 정적 API, 자동 생성·영속(DontDestroyOnLoad).
/// 사용처: 정글 구멍 낙하(GiraffeGate) — 기둥 속에서 서서히 어두워짐 → 씬 로드 동안 완전 검정 유지 →
/// 빙산에서 잠깐 검정 유지 후 서서히 밝아짐. unscaled 시간이라 timeScale 무관.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    static ScreenFader inst;
    float alpha;          // 0=투명, 1=완전 검정
    float target;
    float speed = 10f;    // 초당 alpha 변화량
    bool fadeInOnLoad;
    float fadeInDelay, fadeInSeconds;
    float pendingDelay;

    static ScreenFader Get()
    {
        if (inst == null)
        {
            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<ScreenFader>();
        }
        return inst;
    }

    /// <summary>서서히 어두워짐.</summary>
    public static void FadeOut(float seconds)
    { var f = Get(); f.target = 1f; f.speed = 1f / Mathf.Max(0.05f, seconds); }

    /// <summary>서서히 밝아짐.</summary>
    public static void FadeIn(float seconds)
    { var f = Get(); f.target = 0f; f.speed = 1f / Mathf.Max(0.05f, seconds); }

    /// <summary>다음 씬 로드 후: 완전 검정으로 delay초 유지 → seconds 동안 페이드 인.</summary>
    public static void FadeInAfterNextLoad(float delay, float seconds)
    {
        var f = Get();
        f.fadeInOnLoad = true; f.fadeInDelay = delay; f.fadeInSeconds = seconds;
        SceneManager.sceneLoaded -= f.OnLoaded;
        SceneManager.sceneLoaded += f.OnLoaded;
    }

    void OnLoaded(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnLoaded;
        if (!fadeInOnLoad) return;
        fadeInOnLoad = false;
        alpha = 1f; target = 1f;                       // 로드 직후 완전 검정 유지
        pendingDelay = fadeInDelay;
        speed = 1f / Mathf.Max(0.05f, fadeInSeconds);  // delay 끝나면 이 속도로 밝아짐
    }

    void Update()
    {
        if (pendingDelay > 0f)
        {
            pendingDelay -= Time.unscaledDeltaTime;
            if (pendingDelay <= 0f) target = 0f;
        }
        alpha = Mathf.MoveTowards(alpha, target, speed * Time.unscaledDeltaTime);
    }

    void OnGUI()
    {
        if (alpha <= 0.001f) return;
        GUI.depth = -10000;   // 다른 OnGUI(배너/조작법)보다 위에
        var pc = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = pc;
    }
}
