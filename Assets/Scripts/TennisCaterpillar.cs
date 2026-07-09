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
    public float ballRadius = 0.17f;
    public int ballCount = 8;
    public float spacing = 0.30f;
    public float speed = 1.2f;
    public float bunchAmp = 0.07f;
    public float liftAmp = 0.06f;
    public float waveHz = 2.2f;
    public float phaseStep = 0.95f;

    Vector3[] pts;
    float[] segLen;
    float totalLen;
    Transform[] balls;
    float headS;
    float t;

    /// <summary>경로/재질을 받아 공 8개를 만들고 출발.</summary>
    public void BuildAndGo(Vector3[] path, Material mat, float moveSpeed)
    {
        pts = path;
        speed = moveSpeed;
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
        headS += speed * Time.deltaTime;

        float tailS = headS - (ballCount - 1) * spacing;
        if (tailS > totalLen + 0.3f) { Destroy(gameObject); return; }   // 몸 전체 도착 → 소멸

        for (int i = 0; i < ballCount; i++)
        {
            float phase = t * waveHz * Mathf.PI * 2f - i * phaseStep;
            float s = headS - i * spacing + Mathf.Sin(phase) * bunchAmp;   // 자벌레 수축/신장
            Vector3 p = Sample(s);
            p.y += Mathf.Max(0f, Mathf.Sin(phase)) * liftAmp;              // 꿈틀 들림
            balls[i].position = p;
        }
    }
}
