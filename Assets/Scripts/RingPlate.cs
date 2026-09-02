using UnityEngine;

/// <summary>
/// ★2026-09-03 액자 맵 '밟으면 안 되는' 도넛 버튼(사용자 요청). 화분 둘레의 넓은 링(화분 지름의 ~4배)으로,
/// 평소 초록, 테이블의 어떤 부위든(다리·윗판) 링 윗면에 닿아 있으면 빨강. 모멘터리(벗어나면 즉시 초록).
/// 판정: 링 윗면 위 touchHeight(얇게 — 기린 자세로 다리를 살짝만 들어도 빠지게) ~ 아래 belowWindow 슬랩 안의
/// 테이블 콜라이더 중, 수평 바운즈가 [innerRadius, outerRadius] 고리와 겹치는 것이 있으면 눌림.
/// GiraffeTouchButton(안쪽 밟아야 하는 버튼)과 짝 — FrameHallStage가 '안쪽 눌림 && 링 안 눌림' 유지 시 해결.
/// </summary>
public class RingPlate : MonoBehaviour
{
    [Tooltip("링 안쪽 반지름(화분 바깥벽보다 살짝 크게)")] public float innerRadius = 0.78f;
    [Tooltip("링 바깥 반지름")] public float outerRadius = 2.8f;
    [Tooltip("링 윗면 위로 이 높이 안에 닿아 있어야 눌림(작게 — 다리를 살짝 들면 풀림)")] public float touchHeight = 0.12f;
    [Tooltip("링 윗면 아래 판정 깊이")] public float belowWindow = 0.25f;
    public Color idleColor = new Color(0.2f, 0.85f, 0.3f);      // 평소=초록(밟으면 안 됨)
    public Color pressedColor = new Color(0.9f, 0.15f, 0.15f);  // 눌림=빨강
    [Tooltip("눌릴 때 내려가는 깊이(m)")] public float pressDepth = 0.05f;
    public float pressSpeed = 8f;

    /// <summary>지금 눌려 있는가(매 프레임 갱신).</summary>
    public bool Pressed { get; private set; }
    /// <summary>마지막으로 링을 누른 테이블 부위 이름(디버그).</summary>
    public string LastToucher { get; private set; } = "";

    Renderer rend;
    Material mat;
    Transform tableRoot;
    Vector3 restPos;
    float topY;
    readonly Collider[] hits = new Collider[32];

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) { mat = rend.material; SetColor(idleColor); topY = rend.bounds.max.y; }
        else topY = transform.position.y;
        restPos = transform.position;
        var t = GameObject.Find("Table");
        if (t == null) t = GameObject.Find("TablePlayer");
        if (t != null) tableRoot = t.transform.root;
    }

    void SetColor(Color c)
    {
        if (mat == null) return;
        mat.color = c;
        if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", c * 0.6f); }
    }

    void Update()
    {
        if (tableRoot == null) { var t = GameObject.Find("Table"); if (t != null) tableRoot = t.transform.root; }
        bool touching = false;
        if (tableRoot != null)
        {
            float top = topY;   // 링이 내려가도 판정 기준은 원래 윗면
            Vector3 center = new Vector3(restPos.x, top + (touchHeight - belowWindow) * 0.5f, restPos.z);
            Vector3 half = new Vector3(outerRadius, (touchHeight + belowWindow) * 0.5f, outerRadius);
            int n = Physics.OverlapBoxNonAlloc(center, half, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                if (c == null || c.transform.root != tableRoot) continue;
                var b = c.bounds;
                float d = new Vector2(b.center.x - restPos.x, b.center.z - restPos.z).magnitude;
                float r = 0.5f * Mathf.Max(b.size.x, b.size.z);
                if (d + r > innerRadius && d - r < outerRadius) { touching = true; LastToucher = c.name; break; }
            }
        }
        if (touching != Pressed)
        {
            Pressed = touching;
            SetColor(Pressed ? pressedColor : idleColor);
        }
        Vector3 target = restPos - (Pressed ? Vector3.up * pressDepth : Vector3.zero);
        transform.position = Vector3.MoveTowards(transform.position, target, pressSpeed * Time.deltaTime);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = idleColor;
        UnityEditor.Handles.color = idleColor;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, outerRadius);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, innerRadius);
    }
#endif
}
