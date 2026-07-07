using UnityEngine;

/// <summary>
/// 땅에 두는 '누름 버튼'(★모멘터리 = 위에 있는 동안만 켜짐). 평소 빨강+올라옴,
/// ★테이블의 어떤 부위든(다리·윗판) 버튼 기둥 안에 있으면 초록+내려감 → 벗어나면 즉시 빨강.
/// ★판정(2026-07-08 재작성, 사용자 "주변 말고 딱 그 네모칸만 + 아래까지"):
///   수평 = 캡의 실제 네모 풋프린트(lossyScale/회전 그대로 — touchRadius 폐기),
///   수직 = 캡 아래 belowWindow(발이 버튼 높이보다 살짝 아래/바닥이어도 눌림) ~ 위 heightWindow.
/// GiraffeGate가 배열의 버튼이 '동시에' 전부 Pressed일 때만 문을 연다.
/// </summary>
public class GiraffeTouchButton : MonoBehaviour
{
    [Tooltip("(구버전 — 미사용. 수평 판정은 캡의 실제 네모 풋프린트)")]
    public float touchRadius = 0.9f;
    [Tooltip("버튼 위 기둥의 높이(m) — 이 높이까지는 위에 떠 있어도 눌림(기린 상판 포함).")]
    public float heightWindow = 6f;
    [Tooltip("버튼 아래쪽 판정 깊이(m) — 발이 캡보다 아래(바닥 높이)에 있어도 눌림.")]
    public float belowWindow = 3f;
    [Tooltip("(구버전 필드 — 현재 미사용. '어느 부위든 위에 있으면 눌림'으로 변경됨)")]
    public bool requireStretched = true;
    public float stretchThreshold = 1.0f;
    public Color upColor = new Color(0.9f, 0.15f, 0.15f);   // 평소(안 눌림)=빨강
    public Color downColor = new Color(0.2f, 0.85f, 0.3f);  // 위에 있는 동안=초록
    [Tooltip("눌릴 때 내려가는 깊이(월드 m).")]
    public float pressDepth = 0.12f;
    public float pressSpeed = 8f;

    /// <summary>지금 눌려 있는가(매 프레임 갱신 — 벗어나면 false).</summary>
    public bool Pressed { get; private set; }

    Renderer rend;
    Material mat;
    Transform tableRoot;
    Vector3 restPos;
    readonly Collider[] hits = new Collider[16];

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) { mat = rend.material; SetColor(upColor); }
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
        // 캡의 '딱 그 네모칸' 수직 기둥(아래 belowWindow ~ 위 heightWindow)에
        // 테이블 콜라이더(다리/윗판 아무거나)가 있는지 — 모멘터리
        bool touching = false;
        if (tableRoot != null)
        {
            Vector3 center = restPos + Vector3.up * ((heightWindow - belowWindow) * 0.5f);
            var half = new Vector3(Mathf.Abs(transform.lossyScale.x) * 0.5f,
                                   (heightWindow + belowWindow) * 0.5f,
                                   Mathf.Abs(transform.lossyScale.z) * 0.5f);
            var orient = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);   // 창고 yaw 회전 반영(네모 정확히)
            int n = Physics.OverlapBoxNonAlloc(center, half, hits, orient, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hits[i] != null && hits[i].transform.root == tableRoot) { touching = true; break; }
        }
        if (touching != Pressed)
        {
            Pressed = touching;
            SetColor(Pressed ? downColor : upColor);
        }
        // 눌리는 동안만 내려가고, 벗어나면 다시 올라옴
        Vector3 target = restPos - (Pressed ? Vector3.up * pressDepth : Vector3.zero);
        transform.position = Vector3.MoveTowards(transform.position, target, pressSpeed * Time.deltaTime);
    }
}
