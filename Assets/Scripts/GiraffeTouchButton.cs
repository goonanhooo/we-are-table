using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 땅에 두는 '발 누름 버튼'(★모멘터리 = 밟는 동안만 켜짐). 평소 빨강+올라옴, 지라프로 늘어난 다리의 발이
/// 밟는 동안만 초록+내려감 → 발을 떼면 즉시 빨강으로 되돌아감. GiraffeGate가 4개가 '동시에' Pressed일 때만 문을 연다.
/// requireStretched=true면 늘어난 다리(기린)의 발만 인식.
/// </summary>
public class GiraffeTouchButton : MonoBehaviour
{
    [Tooltip("발이 이 거리 안에 있는 동안 눌림.")]
    public float touchRadius = 0.9f;
    [Tooltip("true면 늘어난 다리(길이>stretchThreshold)의 발만 인식 = 지라프 전용.")]
    public bool requireStretched = true;
    public float stretchThreshold = 1.0f;
    public Color upColor = new Color(0.9f, 0.15f, 0.15f);   // 평소(안 눌림)=빨강
    public Color downColor = new Color(0.2f, 0.85f, 0.3f);  // 밟는 동안=초록
    [Tooltip("눌릴 때 내려가는 깊이(월드 m).")]
    public float pressDepth = 0.12f;
    public float pressSpeed = 8f;

    /// <summary>지금 밟혀 있는가(매 프레임 갱신 — 떼면 false).</summary>
    public bool Pressed { get; private set; }

    static readonly string[] LegMeshNames = { "Leg_FR", "Leg_FL", "Leg_BR", "Leg_BL" };
    Renderer rend;
    Material mat;
    Transform[] feet;
    Vector3 restPos;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) { mat = rend.material; SetColor(upColor); }
        restPos = transform.position;
        var t = GameObject.Find("Table");
        if (t == null) t = GameObject.Find("TablePlayer");
        if (t != null)
        {
            var root = t.transform.root;
            var list = new List<Transform>();
            foreach (var nm in LegMeshNames) { var l = FindDeep(root, nm); if (l != null) list.Add(l); }
            feet = list.ToArray();
        }
    }

    Vector3 Foot(Transform leg) => leg.position - leg.up * (leg.lossyScale.y * 0.5f);

    void SetColor(Color c)
    {
        if (mat == null) return;
        mat.color = c;
        if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", c * 0.6f); }
    }

    void Update()
    {
        // 현재 닿아있는지 매 프레임 판정(모멘터리)
        bool touching = false;
        if (feet != null)
        {
            Vector3 p = transform.position;
            foreach (var leg in feet)
            {
                if (leg == null) continue;
                if (requireStretched && leg.lossyScale.y < stretchThreshold) continue;
                if (Vector3.Distance(Foot(leg), p) < touchRadius) { touching = true; break; }
            }
        }
        if (touching != Pressed)
        {
            Pressed = touching;
            SetColor(Pressed ? downColor : upColor);
        }
        // 눌리는 동안만 내려가고, 떼면 다시 올라옴
        Vector3 target = restPos - (Pressed ? Vector3.up * pressDepth : Vector3.zero);
        transform.position = Vector3.MoveTowards(transform.position, target, pressSpeed * Time.deltaTime);
    }

    static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        foreach (Transform c in r) { var f = FindDeep(c, n); if (f != null) return f; }
        return null;
    }
}
