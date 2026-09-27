using UnityEngine;

/// <summary>
/// ★2026-07-14 코인 더미(정글) — 테이블이 닿으면 '자세 다시 잡기(바로서기)' 목숨 +1 하고 사라진다.
/// 감지는 LetterPickup과 같은 패턴(강체/관절 루트에 다리 컨트롤러가 있으면 플레이어 테이블).
/// 트리거 SphereCollider와 함께 코인 더미 루트에 부착. 대표 코인 하나는 빙글 회전(spin 자식 지정).
/// </summary>
public class CoinPickup : MonoBehaviour
{
    [Tooltip("빙글 도는 대표 코인(선택)")]
    public Transform spin;
    [Tooltip("회전 속도(도/초)")]
    public float spinSpeed = 120f;

    bool collected;
    Renderer[] visuals;
    Collider[] colliders;
    bool[] visualEnabled, colliderEnabled;
    bool hidden;

    void Awake()
    {
        visuals = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        visualEnabled = new bool[visuals.Length]; colliderEnabled = new bool[colliders.Length];
        for (int i = 0; i < visuals.Length; i++) visualEnabled[i] = visuals[i].enabled;
        for (int i = 0; i < colliders.Length; i++) colliderEnabled[i] = colliders[i].enabled;
        ApplyDifficulty();
    }

    void ApplyDifficulty()
    {
        if (visuals == null) return;
        hidden = Difficulty.Coward;
        for (int i = 0; i < visuals.Length; i++) if (visuals[i] != null) visuals[i].enabled = !hidden && visualEnabled[i];
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = !hidden && colliderEnabled[i];
    }

    void Update()
    {
        if (hidden != Difficulty.Coward) ApplyDifficulty();
        if (hidden) return;
        if (spin != null)
            spin.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || Difficulty.Coward || !CheckpointRuntime.Authority) return;
        Transform bodyRoot = other.attachedRigidbody != null ? other.attachedRigidbody.transform.root
                           : other.attachedArticulationBody != null ? other.attachedArticulationBody.transform.root : null;
        if (bodyRoot == null) return;
        if (bodyRoot.GetComponentInChildren<LegController>() == null &&
            bodyRoot.GetComponentInChildren<ArtLegController>() == null) return;

        var reset = bodyRoot.GetComponentInChildren<ArtSceneReset>();
        if (reset == null) reset = Object.FindAnyObjectByType<ArtSceneReset>();
        if (reset == null) return;

        collected = true;
        reset.AddPoseResets(1);   // 바로서기 목숨 +1(최대치에서 캡)
        gameObject.SetActive(false);
    }
}
