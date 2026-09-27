using UnityEngine;

/// <summary>
/// 월드에 떨어져 있는 편지(봉투 더미). 테이블(다리 포함)이 닿으면 화면에 편지를 띄우고 수집 처리.
/// 트리거 콜라이더 필요. ★한 번 먹으면 봉투는 사라짐(세션 넘어도 — 이미 수집한 편지는 Start에서 숨김).
/// 재열람은 메뉴(PauseMenu > Letters)에서.
/// 살짝 떠서 위아래로 흔들리는 연출(눈에 띄게) — 시각 자식(visual)만 움직여 콜라이더는 고정.
/// </summary>
public class LetterPickup : MonoBehaviour
{
    [Tooltip("편지 id = Resources/Letters/<id>.png 텍스처 이름 (예: Letter1)")]
    public string letterId = "Letter1";
    [Tooltip("위아래 흔들림 폭(m). 0이면 고정")]
    public float bobAmount = 0.06f;
    public float bobSpeed = 1.6f;
    [Tooltip("재열람 쿨다운(초) — 닿은 채로 연속 발동 방지")]
    public float cooldown = 1.5f;

    Transform visual;    // 흔들림 연출용 자식(이름 "Visual"), 없으면 자기 자신 고정
    Vector3 visualBase;
    float lastShow = -999f;

    void Awake() { if (!LetterSystem.Enabled) gameObject.SetActive(false); }

    void Start()
    {
        if (!LetterSystem.Enabled) { gameObject.SetActive(false); return; }
        // 이미 수집한 편지는 다시 안 나타남(재열람은 메뉴에서)
        if (LetterSystem.IsCollected(letterId)) { gameObject.SetActive(false); return; }
        var v = transform.Find("Visual");
        if (v != null) { visual = v; visualBase = v.localPosition; }
    }

    void Update()
    {
        if (visual != null && bobAmount > 0f)
        {
            var p = visualBase;
            p.y += (Mathf.Sin(Time.time * bobSpeed * Mathf.PI) * 0.5f + 0.5f) * bobAmount;
            visual.localPosition = p;
            visual.localRotation = Quaternion.Euler(0f, Time.time * 25f, 0f);   // 천천히 회전(발견성)
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if(!LetterSystem.Enabled || !CheckpointRuntime.Authority)return;
        if (Time.unscaledTime - lastShow < cooldown) return;
        Transform bodyRoot = other.attachedRigidbody != null ? other.attachedRigidbody.transform.root
                           : other.attachedArticulationBody != null ? other.attachedArticulationBody.transform.root : null;
        if (bodyRoot == null) return;
        // 테이블(상판/다리)인지 판별 — 루트에 다리 컨트롤러(힌지판 또는 art판)가 있으면 플레이어 테이블
        if (bodyRoot.GetComponentInChildren<LegController>() == null &&
            bodyRoot.GetComponentInChildren<ArtLegController>() == null) return;
        lastShow = Time.unscaledTime;
        LetterSystem.Collect(letterId);
        LetterOverlay.Show(letterId);
        gameObject.SetActive(false);   // ★한 번 먹으면 봉투 사라짐
    }
}
