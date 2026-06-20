using UnityEngine;

/// <summary>
/// Hallway 컷씬 초반(테이블 정지) — 테이블이 상상하는 듯한 만화 말풍선 이미지를
/// **꼬리 끝(가장 작은 puff) → 중간 → 큰 꼬리 → 본체** 순서로 0.6초 간격으로 스르륵(페이드) 등장.
/// 전부 다 뜬 뒤 `holdSeconds`(3초) 대기 → **말풍선 전체가 서서히 페이드아웃** → 완전히 사라지면 `IsGone=true`.
/// 카메라(HallwayStage)는 `IsGone`이 될 때까지 정지해 기다렸다가 그제서야 의자로 팬한다.
/// 말풍선은 씬에 직접 배치된 **편집 가능한 월드 캔버스**(런타임 생성 아님). 이 스크립트는 연출만 담당.
/// </summary>
public class ThoughtReveal : MonoBehaviour
{
    [Tooltip("등장 순서대로: [0]꼬리끝(가장 작음) [1]중간 [2]큰 꼬리 [3]본체")]
    public CanvasGroup[] layers;

    [Header("타이밍")]
    public float stagger = 0.6f;       // 각 단계 등장 간격
    public float revealDur = 0.45f;    // 각 단계 페이드인 시간
    public float holdSeconds = 3f;     // 전부 다 뜬 후 카메라가 기다리는 시간
    public float fadeOutDur = 1.5f;    // 전체가 서서히 사라지는 시간

    float t0;
    /// <summary>말풍선이 완전히 사라졌는지(카메라가 이 뒤에 움직임).</summary>
    public bool IsGone { get; private set; }

    void OnEnable()
    {
        t0 = Time.time;
        IsGone = false;
        if (layers != null) foreach (var l in layers) if (l) l.alpha = 0f;
    }

    void LateUpdate()
    {
        if (layers == null || layers.Length == 0) return;
        float now = Time.time - t0;

        float revealEnd = (layers.Length - 1) * stagger + revealDur;  // 전부 다 뜨는 시점
        float fadeStart = revealEnd + holdSeconds;                    // 대기 후 페이드아웃 시작
        float fade = now < fadeStart ? 1f : Mathf.Clamp01(1f - (now - fadeStart) / Mathf.Max(0.01f, fadeOutDur));

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            float r = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - i * stagger) / revealDur));
            layers[i].alpha = r * fade;
        }

        if (now >= fadeStart + fadeOutDur)
        {
            IsGone = true;
            gameObject.SetActive(false);
        }
    }
}
