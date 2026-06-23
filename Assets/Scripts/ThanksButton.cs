using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 창고 안의 버튼. 테이블(다리)이 닿으면(트리거) 한 번 발동 →
/// 캡이 눌리고, 주변에서 **폭죽**이 터지며, 숨겨져 있던 뒷벽 **감사 글자**가 서서히 나타난다.
/// 글자(thanksText)는 평소 알파 0(숨김)이며 발동 시 페이드인.
/// </summary>
public class ThanksButton : MonoBehaviour
{
    [Tooltip("뒷벽 감사 글자 렌더러(처음 숨김 → 발동 시 나타남)")]
    public Renderer thanksText;
    [Tooltip("눌리는 버튼 캡(시각 피드백)")]
    public Transform cap;

    public float revealDur = 1.4f;
    public float fireworksSeconds = 6f;

    Transform tableRoot;
    bool fired;
    Material textMat;
    Material fwMat;
    Texture2D dot;
    Material capMat;
    Vector3 capRest;

    static readonly Color[] Palette = {
        new Color(1f,0.3f,0.3f), new Color(1f,0.8f,0.2f), new Color(0.3f,0.8f,1f),
        new Color(0.4f,1f,0.4f), new Color(1f,0.4f,0.9f), new Color(0.7f,0.5f,1f), Color.white,
    };

    void Start()
    {
        var t = GameObject.Find("Table");
        if (t != null) tableRoot = t.transform.root;
        if (thanksText != null) { textMat = thanksText.material; SetTextAlpha(0f); }   // 처음 숨김
        if (cap != null) { capRest = cap.localPosition; var cr = cap.GetComponent<Renderer>(); if (cr) capMat = cr.material; }
    }

    void Update()
    {
        // 발동 전: 캡을 은은하게 점멸(주목 유도)
        if (!fired && capMat != null && capMat.HasProperty("_EmissionColor"))
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            capMat.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.25f) * (0.6f + 1.4f * k));
        }
    }

    void SetTextAlpha(float a)
    {
        if (textMat == null) return;
        Color c = textMat.HasProperty("_BaseColor") ? textMat.GetColor("_BaseColor") : textMat.color;
        c.a = a;
        if (textMat.HasProperty("_BaseColor")) textMat.SetColor("_BaseColor", c);
        textMat.color = c;
    }

    void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;                                                   // 정적 지오메트리 무시
        if (rb.transform.root.GetComponentInChildren<LegController>() == null) return; // 테이블만
        fired = true;
        StartCoroutine(PressCap());
        StartCoroutine(Reveal());
        StartCoroutine(Fireworks());
    }

    IEnumerator PressCap()
    {
        if (cap == null) yield break;
        Vector3 down = capRest + new Vector3(0f, -0.18f, 0f);
        for (float t = 0; t < 0.09f; t += Time.deltaTime) { cap.localPosition = Vector3.Lerp(capRest, down, t / 0.09f); yield return null; }
        for (float t = 0; t < 0.16f; t += Time.deltaTime) { cap.localPosition = Vector3.Lerp(down, capRest, t / 0.16f); yield return null; }
        cap.localPosition = capRest;
        if (capMat != null && capMat.HasProperty("_EmissionColor")) capMat.SetColor("_EmissionColor", new Color(0.4f, 1f, 0.4f) * 2f); // 눌림=초록
    }

    IEnumerator Reveal()
    {
        for (float t = 0; t < revealDur; t += Time.deltaTime) { SetTextAlpha(Mathf.SmoothStep(0f, 1f, t / revealDur)); yield return null; }
        SetTextAlpha(1f);
    }

    IEnumerator Fireworks()
    {
        EnsureFwAssets();
        Vector3 c = transform.position;
        for (float t = 0; t < fireworksSeconds; )
        {
            int n = Random.Range(1, 3);
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = c + new Vector3(Random.Range(-15f, 15f), Random.Range(6f, 22f), Random.Range(-22f, 16f));
                SpawnBurst(pos, Palette[Random.Range(0, Palette.Length)]);
            }
            float wait = Random.Range(0.28f, 0.55f);
            yield return new WaitForSeconds(wait);
            t += wait;
        }
    }

    void EnsureFwAssets()
    {
        if (dot == null) dot = SoftDot();
        if (fwMat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            fwMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            if (fwMat.HasProperty("_BaseMap")) fwMat.SetTexture("_BaseMap", dot);
            if (fwMat.HasProperty("_BaseColor")) fwMat.SetColor("_BaseColor", Color.white);
            fwMat.mainTexture = dot;
            fwMat.SetFloat("_Surface", 1f);
            fwMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            fwMat.SetInt("_DstBlend", (int)BlendMode.One);   // 가산(additive) 발광
            fwMat.SetInt("_ZWrite", 0);
            fwMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fwMat.renderQueue = (int)RenderQueue.Transparent;
        }
    }

    void SpawnBurst(Vector3 pos, Color color)
    {
        var go = new GameObject("FW");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();

        var main = ps.main;
        main.loop = false; main.duration = 1.6f; main.startLifetime = 1.4f;
        main.startSpeed = 9f; main.startSize = 0.5f; main.gravityModifier = 0.5f;
        main.maxParticles = 260; main.startColor = color;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)150) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.05f;

        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = fwMat;
        r.shadowCastingMode = ShadowCastingMode.Off;

        ps.Play();
        Destroy(go, 3.2f);
    }

    static Texture2D SoftDot()
    {
        int S = 64; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;     // 0 중심 ~ 1 가장자리
                float a = Mathf.Clamp01(1f - d); a = a * a;
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels(px); t.Apply();
        return t;
    }
}
