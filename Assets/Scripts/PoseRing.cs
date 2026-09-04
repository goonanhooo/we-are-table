using UnityEngine;

/// <summary>
/// ★2026-09-05 바로서기 충전 링(보통 난이도 전용). 맵 클리어 경로에 씬 오브젝트로 배치 —
/// 각 씬 "PoseRings" 루트 아래 Assets/Prefabs/PoseRing.prefab 인스턴스(Ring_00, Ring_01, …, 이름순 = 멀티 비트 인덱스).
/// 테이블 크기(지름 ~1.6m)의 금색 토러스가 세로로 서서 빙글 돌며 살짝 떠다니고, 테이블이 닿으면
/// ArtSceneReset.AddPoseResets(1)(누적 — 도넛 3개 넘게도 충전) 후 사라진다.
/// 겁쟁이 난이도(Difficulty.Coward)에선 렌더러/콜라이더를 꺼 안 보임(매 프레임 검사 — 멀티 난이도 값이 늦게 와도 반영).
/// 멀티: 판정은 호스트 물리에서만 일어남(게스트 테이블은 바디 없는 콜라이더라 트리거 안 뜸) —
/// NetStageSync가 수집 비트를 발행하고 게스트는 CollectRemote()로 숨긴다.
/// </summary>
public class PoseRing : MonoBehaviour
{
    [Tooltip("회전 속도(도/초, 세로로 선 링이 수직축으로 돎)")]
    public float spinSpeed = 90f;
    [Tooltip("위아래 둥실 진폭(m)")]
    public float bobAmp = 0.10f;
    public float bobSpeed = 1.6f;

    bool collected;
    Renderer rend;
    Collider col;
    Vector3 basePos;
    float phase;

    /// <summary>이미 먹었는지(멀티 발행용).</summary>
    public bool Collected => collected;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        col = GetComponent<Collider>();
        basePos = transform.position;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        bool show = !collected && !Difficulty.Coward;
        if (rend != null && rend.enabled != show) rend.enabled = show;
        if (col != null && col.enabled != show) col.enabled = show;
        if (!show) return;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobAmp);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || Difficulty.Coward) return;
        // 플레이어 테이블 판별(LetterPickup/CoinPickup 패턴): 강체/관절 루트에 다리 컨트롤러가 있어야 함
        Transform bodyRoot = other.attachedRigidbody != null ? other.attachedRigidbody.transform.root
                           : other.attachedArticulationBody != null ? other.attachedArticulationBody.transform.root : null;
        if (bodyRoot == null) return;
        if (bodyRoot.GetComponentInChildren<LegController>() == null &&
            bodyRoot.GetComponentInChildren<ArtLegController>() == null) return;

        var reset = bodyRoot.GetComponentInChildren<ArtSceneReset>();
        if (reset == null) reset = Object.FindAnyObjectByType<ArtSceneReset>();
        if (reset == null) return;

        collected = true;
        reset.AddPoseResets(1);   // 도넛 +1(누적, 상한 없음)
    }

    /// <summary>멀티 게스트: 호스트가 먹은 링을 소리 없이 숨김(카운트는 NetStageSync가 따로 동기화).</summary>
    public void CollectRemote() => collected = true;
}
