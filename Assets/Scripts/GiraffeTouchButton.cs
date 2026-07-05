using UnityEngine;

/// <summary>
/// 땅에 두는 '누름 버튼'(★모멘터리 = 위에 있는 동안만 켜짐). 평소 빨강+올라옴,
/// ★테이블의 어떤 부위든(다리·윗판) 버튼 '위쪽 기둥' 안에 있으면 초록+내려감 → 벗어나면 즉시 빨강.
/// 판정 = 버튼 위 수직 박스(반경 touchRadius × 높이 heightWindow) OverlapBox에 테이블 콜라이더가 걸리는지.
/// GiraffeGate가 배열의 버튼이 '동시에' 전부 Pressed일 때만 문을 연다.
/// </summary>
public class GiraffeTouchButton : MonoBehaviour
{
    [Tooltip("버튼 위 기둥의 수평 반경(m) — 이 안에 테이블 부위가 있으면 눌림.")]
    public float touchRadius = 0.9f;
    [Tooltip("버튼 위 기둥의 높이(m) — 이 높이까지는 위에 떠 있어도 눌림(기린 상판 포함).")]
    public float heightWindow = 6f;
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
        // 버튼 위 수직 기둥에 테이블 콜라이더(다리/윗판 아무거나)가 있는지 — 모멘터리
        bool touching = false;
        if (tableRoot != null)
        {
            Vector3 center = restPos + Vector3.up * (heightWindow * 0.5f);
            var half = new Vector3(touchRadius, heightWindow * 0.5f, touchRadius);
            int n = Physics.OverlapBoxNonAlloc(center, half, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
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
