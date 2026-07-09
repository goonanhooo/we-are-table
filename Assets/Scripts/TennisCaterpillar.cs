using UnityEngine;

/// <summary>
/// ★ 테니스공 애벌레(2026-07-10, Climb 씬) — 다리 공과 동일 크기(반지름 0.17) 테니스공 8개가
/// 한 줄로 이어져 애벌레처럼 기어간다. 물리 없음(키네마틱 연출).
/// 움직임: 경로(폴리라인)를 머리가 전진하고, 각 공 i는 머리 뒤 i×spacing 지점을 따라가되
///  - 종방향 웨이브(bunchAmp·sin)로 몸이 늘었다 줄었다(자벌레 수축/신장)
///  - 같은 위상 웨이브로 살짝 들렸다 내려앉음(liftAmp) — 공 하나하나가 독립적으로 꿈틀댐
/// 경로 끝(도착 구덩이 안)에 몸 전체가 들어가면 스스로 파괴.
/// CaterpillarButton이 런타임에 생성(BuildAndGo).
/// </summary>
public class TennisCaterpillar : MonoBehaviour
{
    public enum Gait { Wave, Inchworm }

    public Gait gait = Gait.Wave;
    public float ballRadius = 0.17f;
    public int ballCount = 8;
    public float spacing = 0.30f;
    public float speed = 1.2f;
    public float bunchAmp = 0.07f;
    public float liftAmp = 0.06f;
    public float waveHz = 2.2f;
    public float phaseStep = 0.95f;

    [Header("자벌레(Inchworm) — 몸을 아치로 굽혔다 쫙 펴며 전진")]
    public float inchCycle = 1.2f;     // 한 걸음(굽힘+펴기) 시간
    public float extLen = 2.1f;        // 쫙 편 몸 길이
    public float archLen = 1.15f;      // 굽혔을 때 머리~꼬리 거리
    public float archAmp = 0.55f;      // 아치 최고 높이

    Vector3[] pts;
    float[] segLen;
    float totalLen;
    Transform[] balls;
    float headS;
    float t;

    /// <summary>경로/재질을 받아 공 8개를 만들고 출발.</summary>
    public void BuildAndGo(Vector3[] path, Material mat, float moveSpeed, Gait g = Gait.Wave)
    {
        pts = path;
        speed = moveSpeed;
        gait = g;
        if (g == Gait.Inchworm) inchCycle = Mathf.Max(0.5f, (extLen - archLen) / Mathf.Max(moveSpeed * 0.55f, 0.2f));
        segLen = new float[pts.Length - 1];
        totalLen = 0f;
        for (int i = 0; i < segLen.Length; i++)
        {
            segLen[i] = Vector3.Distance(pts[i], pts[i + 1]);
            totalLen += segLen[i];
        }
        balls = new Transform[ballCount];
        for (int i = 0; i < ballCount; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(b.GetComponent<Collider>());
            b.name = "Seg" + i;
            b.transform.SetParent(transform, false);
            b.transform.localScale = Vector3.one * (ballRadius * 2f);
            if (mat != null) b.GetComponent<MeshRenderer>().sharedMaterial = mat;
            balls[i] = b.transform;
        }
        headS = 0f;
        enabled = true;
    }

    Vector3 Sample(float s)
    {
        if (pts == null || pts.Length == 0) return transform.position;
        if (s <= 0f) return pts[0];
        for (int i = 0; i < segLen.Length; i++)
        {
            if (s <= segLen[i] || i == segLen.Length - 1)
                return Vector3.Lerp(pts[i], pts[i + 1], segLen[i] > 1e-5f ? Mathf.Clamp01(s / segLen[i]) : 1f);
            s -= segLen[i];
        }
        return pts[pts.Length - 1];
    }

    void Update()
    {
        if (balls == null) return;
        t += Time.deltaTime;
        if (gait == Gait.Wave) UpdateWave();
        else UpdateInchworm();
    }

    // ── 웨이브형: 몸 전체에 위상차 파도(수축/신장+들림)가 흐르며 등속 전진 ──
    void UpdateWave()
    {
        headS += speed * Time.deltaTime;
        float tailS = headS - (ballCount - 1) * spacing;
        if (tailS > totalLen + 0.3f) { Destroy(gameObject); return; }

        for (int i = 0; i < ballCount; i++)
        {
            float phase = t * waveHz * Mathf.PI * 2f - i * phaseStep;
            float s = headS - i * spacing + Mathf.Sin(phase) * bunchAmp;
            Vector3 p = Sample(s);
            p.y += Mathf.Max(0f, Mathf.Sin(phase)) * liftAmp;
            balls[i].position = p;
        }
    }

    // ── 자벌레형: ①꼬리를 끌어당기며 몸 가운데가 아치로 솟음 ②머리를 내밀며 쫙 펴짐 — 반복 전진 ──
    void UpdateInchworm()
    {
        float stride = extLen - archLen;
        int cyc = Mathf.FloorToInt(t / inchCycle);
        float u = (t - cyc * inchCycle) / inchCycle;
        float baseHead = cyc * stride + extLen;   // 이 걸음 시작 시점의 머리 위치(꼬리가 0에서 출발)

        float curLen;
        if (u < 0.5f)
        {
            float k = Mathf.SmoothStep(0f, 1f, u / 0.5f);     // 굽힘: 머리 고정, 꼬리 전진
            headS = baseHead;
            curLen = Mathf.Lerp(extLen, archLen, k);
        }
        else
        {
            float k = Mathf.SmoothStep(0f, 1f, (u - 0.5f) / 0.5f);   // 펴기: 꼬리 고정, 머리 전진
            headS = baseHead + stride * k;
            curLen = Mathf.Lerp(archLen, extLen, k);
        }
        float tailS = headS - curLen;
        if (tailS > totalLen + 0.3f) { Destroy(gameObject); return; }

        float arch = (extLen - curLen) / Mathf.Max(stride, 0.01f);   // 0=쫙 폄, 1=최대 굽힘
        for (int i = 0; i < ballCount; i++)
        {
            float f = i / (float)(ballCount - 1);
            Vector3 p = Sample(headS - curLen * f);
            p.y += arch * archAmp * Mathf.Sin(Mathf.PI * f);          // 가운데가 가장 높은 아치
            balls[i].position = p;
        }
    }
}
