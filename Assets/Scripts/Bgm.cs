using UnityEngine;

/// <summary>
/// 씬 배경음악(BGM). 루프 재생. 음량은 옵션 메뉴(PauseMenu)의 BGM 볼륨 슬라이더가 `Bgm.Volume`(전역 static)로 조절.
/// Volume 은 static 이라 씬이 바뀌어도 유지된다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Bgm : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float startVolume = 0.5f;

    public static float Volume = 0.5f;   // 전역 BGM 볼륨(모든 옵션 메뉴 공유)
    static bool inited;
    static AudioSource active;

    void Start()
    {
        if (!inited) { Volume = startVolume; inited = true; }   // 첫 실행 시 초기값
        active = GetComponent<AudioSource>();
        active.clip = clip;
        active.loop = true;
        active.playOnAwake = false;
        active.spatialBlend = 0f;   // 2D
        Apply();
        if (clip != null) active.Play();
    }

    void Update() { Apply(); }   // 슬라이더 변경 즉시 반영

    public static void Apply() { if (active != null) active.volume = Volume; }
}
