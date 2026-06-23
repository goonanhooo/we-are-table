using UnityEngine;

/// <summary>
/// 테이블 다리가 땅에 닿을 때(걷는 발소리/마찰) Wood1·Wood2 중 **랜덤**으로 효과음 재생.
/// 테이블 루트에 부착. 각 다리(Leg_*_Pivot)의 `LegSfxRelay`가 충돌 시 `PlayStep`을 호출한다.
/// 음량은 충격 세기에 비례 × `Sfx.Volume`(옵션 SFX 슬라이더). 너무 자주 안 나게 쿨다운.
/// </summary>
public class TableSfx : MonoBehaviour
{
    public AudioClip[] woodClips;          // Wood1, Wood2
    [Range(0f, 1f)] public float baseVolume = 0.55f;
    public float minInterval = 0.07f;      // 연속 발생 쿨다운(초)
    public float minImpact = 0.6f;         // 이 속도 미만 접촉은 무시
    public float fullImpact = 4f;          // 이 속도에서 최대 음량

    AudioSource src;
    float lastTime = -10f;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;             // 2D
    }

    /// <summary>다리 충돌 시 호출. impact = 상대 충돌 속도 크기.</summary>
    public void PlayStep(float impact)
    {
        if (woodClips == null || woodClips.Length == 0) return;
        if (impact < minImpact) return;
        if (Time.time - lastTime < minInterval) return;
        lastTime = Time.time;

        var clip = woodClips[Random.Range(0, woodClips.Length)];
        if (clip == null) return;
        float vol = baseVolume * Mathf.Clamp01(impact / fullImpact) * Sfx.Volume;
        src.PlayOneShot(clip, Mathf.Clamp01(vol));
    }
}
