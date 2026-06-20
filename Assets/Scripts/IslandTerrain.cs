using UnityEngine;

/// <summary>
/// 둥글지만 자연스러운(노이즈 해안선) 섬 지형을 절차적으로 생성한다.
/// - 원형 falloff + Perlin fBm 으로 organic 해안/언덕.
/// - 멀리 보이는 화산(원뿔 + 분화구) 융기.
/// - 화산에서 흘러내려 길을 가로막는 용암 강의 도랑(trench)을 같은 경로로 파낸다.
/// - 높이/경사에 따라 모래·잔디·바위 정점색을 구워 Custom/VertexColorLit 로 그린다.
/// - MeshCollider 자동 부착(테이블이 위를 걸음). ExecuteAlways 라 에디터에서도 보이고 파라미터로 편집.
/// 좌표는 월드 기준(이 GameObject 는 원점에 둔다).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class IslandTerrain : MonoBehaviour
{
    [Header("지형 크기/해상도")]
    public float size = 700f;
    [Range(32, 480)] public int resolution = 170;   // 협곡 벽을 가파르게 표현하려면 높여야 함(격자간격 작아짐)

    [Header("섬 모양")]
    public Vector2 islandCenter = new Vector2(0f, 127f);
    public float islandRadius = 150f;   // 해안선(높이=물높이) 반경
    public float shoreWidth = 46f;      // 해안 falloff 폭
    public float landHeight = 6f;       // 기본 육지 높이(물 위)
    public float seabedDepth = 10f;     // 물 아래 바닥 깊이
    public float coastNoise = 34f;      // 해안선 일그러짐(자연스러움)
    public float hillHeight = 7f;
    public float hillScale = 0.012f;
    public float waterLevel = 0f;
    public int seed = 1234;

    [Header("스폰 착지 언덕 (초기 리스폰 지점)")]
    public Vector2 spawnHillCenter = new Vector2(0f, 0f);
    public float spawnHillRadius = 24f;
    public float spawnHillHeight = 5.5f;

    [Header("화산")]
    public Vector2 volcanoCenter = new Vector2(36f, 188f);
    public float volcanoRadius = 70f;
    public float volcanoHeight = 64f;
    public float craterRadius = 14f;
    public float craterDepth = 6f;
    [Tooltip("화산 콘 위에서의 용암 채널(협곡) 깊이. 깊은 슬롯 대신 경사면을 타고 흐르는 얕은 채널이 되게. 콘 밖(vd≥volcanoRadius)엔 영향 0 → 강/협곡 불변.")]
    public float volcanoChannelDepth = 5f;

    [Header("용암 강 (도랑)")]
    public Vector2[] riverPath = new Vector2[] {
        new Vector2(36f, 188f), new Vector2(20f, 120f), new Vector2(2f, 70f),
        new Vector2(0f, 46f), new Vector2(-22f, 8f), new Vector2(-60f, -38f), new Vector2(-120f, -90f)
    };
    public float riverWidth = 15f;
    public float riverDepth = 4.5f;

    [Header("용암 협곡 (canyon)")]
    [Tooltip("강이 흐르는 협곡 — 국소 지형보다 이만큼 아래로 바닥을 판다(깊은 골짜기).")]
    public float canyonDepth = 10f;
    [Tooltip("협곡 바닥(평평, 용암 자리)의 절반 폭. 좁을수록 건너기 쉬움. 용암/사망 폭과 맞춤.")]
    public float canyonFloorHalf = 3.5f;
    [Tooltip("협곡 림(가장자리)까지의 절반 폭. (rimHalf-floorHalf)=벽 수평폭 → 작을수록 벽이 가파름(거의 수직).")]
    public float canyonRimHalf = 6f;
    [Tooltip("협곡 경로가 좌우로 자연스럽게 굽이치는 양(폭은 유지=메안더).")]
    public float canyonRimNoise = 1.2f;

    /// <summary>협곡 림 절반 폭(노이즈 포함 대략 최대). 리스폰이 협곡 밖인지 판정에 사용.</summary>
    public float CanyonRimHalf => canyonRimHalf + canyonRimNoise;

    Mesh mesh;
    int hash, builtHash = -1;

    void OnEnable() { Rebuild(); }
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

    int ParamHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + size.GetHashCode();
            h = h * 31 + resolution;
            h = h * 31 + islandCenter.GetHashCode();
            h = h * 31 + islandRadius.GetHashCode();
            h = h * 31 + shoreWidth.GetHashCode();
            h = h * 31 + landHeight.GetHashCode();
            h = h * 31 + seabedDepth.GetHashCode();
            h = h * 31 + coastNoise.GetHashCode();
            h = h * 31 + hillHeight.GetHashCode() + hillScale.GetHashCode();
            h = h * 31 + spawnHillCenter.GetHashCode() + spawnHillRadius.GetHashCode() + spawnHillHeight.GetHashCode();
            h = h * 31 + seed;
            h = h * 31 + volcanoCenter.GetHashCode() + volcanoRadius.GetHashCode() + volcanoHeight.GetHashCode();
            h = h * 31 + craterRadius.GetHashCode() + craterDepth.GetHashCode() + volcanoChannelDepth.GetHashCode();
            h = h * 31 + riverWidth.GetHashCode() + riverDepth.GetHashCode();
            h = h * 31 + canyonDepth.GetHashCode() + canyonFloorHalf.GetHashCode() + canyonRimHalf.GetHashCode() + canyonRimNoise.GetHashCode();
            if (riverPath != null) foreach (var p in riverPath) h = h * 31 + p.GetHashCode();
            return h;
        }
    }

    void Rebuild()
    {
        hash = ParamHash();
        if (hash == builtHash && mesh != null) return;

        int res = Mathf.Clamp(resolution, 8, 220);
        int vside = res + 1;
        float half = size * 0.5f;
        float step = size / res;

        var verts = new Vector3[vside * vside];
        var cols = new Color[vside * vside];
        var tris = new int[res * res * 6];

        for (int z = 0; z < vside; z++)
        {
            for (int x = 0; x < vside; x++)
            {
                int i = z * vside + x;
                float wx = -half + x * step;
                float wz = -half + z * step;
                float h = HeightAt(wx, wz);
                verts[i] = new Vector3(wx, h, wz);
            }
        }
        // 경사 계산용으로 한 번 채운 뒤 색 결정
        for (int z = 0; z < vside; z++)
        {
            for (int x = 0; x < vside; x++)
            {
                int i = z * vside + x;
                float wx = verts[i].x, wz = verts[i].z, h = verts[i].y;
                float slope = Slope(verts, x, z, vside, step);
                cols[i] = VertexColor(wx, wz, h, slope);
            }
        }

        int t = 0;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int i = z * vside + x;
                tris[t++] = i; tris[t++] = i + vside; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + vside; tris[t++] = i + vside + 1;
            }
        }

        if (mesh == null) { mesh = new Mesh { name = "IslandMesh" }; mesh.hideFlags = HideFlags.DontSave; }
        mesh.Clear();
        mesh.indexFormat = (verts.Length > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.colors = cols;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        var mc = GetComponent<MeshCollider>();
        mc.sharedMesh = null;
        mc.sharedMesh = mesh;
        builtHash = hash;
    }

    float Slope(Vector3[] v, int x, int z, int vside, float step)
    {
        int xl = Mathf.Max(0, x - 1), xr = Mathf.Min(vside - 1, x + 1);
        int zd = Mathf.Max(0, z - 1), zu = Mathf.Min(vside - 1, z + 1);
        float dx = (v[z * vside + xr].y - v[z * vside + xl].y) / ((xr - xl) * step + 1e-4f);
        float dz = (v[zu * vside + x].y - v[zd * vside + x].y) / ((zu - zd) * step + 1e-4f);
        return Mathf.Sqrt(dx * dx + dz * dz); // 0=평평, 클수록 가파름
    }

    // GLSL식 smoothstep(edge0, edge1, x) → 0..1. (Unity Mathf.SmoothStep 은 의미가 달라서 직접 구현)
    static float SStep(float e0, float e1, float x)
    {
        if (Mathf.Abs(e1 - e0) < 1e-6f) return x < e0 ? 0f : 1f;
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    float Fbm(float x, float z)
    {
        float s = seed * 0.1234f;
        float v = 0f, a = 0.5f, f = 1f;
        for (int o = 0; o < 4; o++)
        {
            v += a * (Mathf.PerlinNoise(x * f + s, z * f + s) - 0.5f);
            f *= 2.03f; a *= 0.5f;
        }
        return v; // 대략 -0.5..0.5
    }

    /// <summary>월드 (x,z) 의 최종 지형 높이.</summary>
    public float HeightAt(float x, float z)
    {
        float dx = x - islandCenter.x, dz = z - islandCenter.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        // 해안선 일그러짐
        float coast = Fbm(x * 0.01f, z * 0.01f) * coastNoise;
        float dd = d + coast;
        // 안쪽(dd 작음)=1, 바깥(dd 큼)=0
        float landMask = 1f - SStep(islandRadius - shoreWidth * 0.5f, islandRadius + shoreWidth * 0.5f, dd);

        float seabed = waterLevel - seabedDepth;
        float h = Mathf.Lerp(seabed, waterLevel + landHeight, landMask);
        // 언덕
        h += hillHeight * Fbm(x * hillScale, z * hillScale) * landMask;

        // 스폰 착지 언덕(완만한 둔덕) — 초기 리스폰 지점을 약간 높게.
        if (spawnHillHeight != 0f && spawnHillRadius > 0.01f)
        {
            float shd = Mathf.Sqrt((x - spawnHillCenter.x) * (x - spawnHillCenter.x) + (z - spawnHillCenter.y) * (z - spawnHillCenter.y));
            float k = Mathf.Clamp01(1f - shd / spawnHillRadius);
            h += spawnHillHeight * (k * k * (3f - 2f * k)) * landMask;
        }

        // 화산 원뿔 + 평평한 바닥의 분화구 웅덩이
        float vdx = x - volcanoCenter.x, vdz = z - volcanoCenter.y;
        float vd = Mathf.Sqrt(vdx * vdx + vdz * vdz);
        float cone = Mathf.Clamp01(1f - vd / volcanoRadius);
        float volc = volcanoHeight * cone * cone;
        // 림 높이에서 craterDepth 만큼 내린 "평평한 바닥" → 용암이 고일 웅덩이
        float rimCone = Mathf.Clamp01(1f - craterRadius / volcanoRadius);
        float floorVolc = volcanoHeight * rimCone * rimCone - craterDepth;
        float inside = 1f - SStep(craterRadius * 0.65f, craterRadius, vd); // 안쪽 0.65R=1 → 림=0
        volc = Mathf.Lerp(volc, Mathf.Min(volc, floorVolc), inside);
        h += volc * landMask;

        // 용암 강 협곡(canyon): 강 중심으로 갈수록 국소 지형보다 canyonDepth 만큼 내려간 바닥 → 깊고 좁은 골짜기.
        // 바닥(floorHalf 안)은 평평(용암 자리), floorHalf~rimHalf 가 '벽'(폭=rimHalf-floorHalf, 작을수록 가파름).
        // 노이즈는 floor/rim 을 같은 양만큼 밀어 '폭은 유지하며 굽이치게'(메안더) → 림을 줄이면 실제로 좁아짐.
        float rd = DistanceToRiver(x, z);
        float edge = Fbm(x * 0.04f, z * 0.04f) * canyonRimNoise;
        float wall = Mathf.Max(0.6f, canyonRimHalf - canyonFloorHalf);  // 벽 수평폭
        float floorHalf = Mathf.Max(0.4f, canyonFloorHalf + edge);
        float rimHalf = floorHalf + wall;
        // ⚠️ 바다(원지형이 물높이 근처/아래)에선 협곡을 파지 않는다 → 바다로 인공 육교가 생기지 않게(용암도 바다로 안 뻗음).
        float landAmt = SStep(waterLevel + 0.5f, waterLevel + 4f, h);
        float carve = (1f - SStep(floorHalf, rimHalf, rd)) * landAmt; // 1=바닥 → 0=림 밖/바다
        // 화산 콘 위에선 협곡을 얕게(volcanoChannelDepth) → 정상 분화구에서 흘러나온 용암이
        // 깊은 슬롯이 아니라 경사면을 타고 흐르는 채널이 되어 자연스럽게 협곡으로 합류.
        // cone 은 vd≥volcanoRadius 에서 0 → 콘 밖에선 canyonDepth 그대로(강/협곡 형태 불변).
        float localCanyonDepth = Mathf.Lerp(canyonDepth, volcanoChannelDepth, cone);
        float floorTarget = Mathf.Max(h - localCanyonDepth, waterLevel + 1.0f); // 물 위 유지
        h = Mathf.Lerp(h, floorTarget, carve);

        return h;
    }

    Color VertexColor(float x, float z, float h, float slope)
    {
        Color sand = new Color(0.84f, 0.76f, 0.55f);
        Color grass = new Color(0.28f, 0.49f, 0.21f);
        Color grassDry = new Color(0.45f, 0.5f, 0.26f);
        Color rock = new Color(0.34f, 0.28f, 0.24f);
        Color scorch = new Color(0.16f, 0.09f, 0.07f);

        float rd = DistanceToRiver(x, z);
        if (rd < riverWidth * 0.75f)  // 용암 강 둑: 그을린 바위
            return Color.Lerp(scorch, rock, SStep(0f, riverWidth * 0.75f, rd));

        // 해변(낮은 곳) → 모래
        float beach = 1f - SStep(waterLevel + 0.2f, waterLevel + 2.2f, h);
        Color c = Color.Lerp(grass, sand, beach);
        // 마른 풀 변주
        c = Color.Lerp(c, grassDry, Mathf.Clamp01(Fbm(x * 0.05f, z * 0.05f) + 0.5f) * 0.35f * (1f - beach));
        // 경사/높은 곳 → 바위(화산 사면)
        float rocky = SStep(0.55f, 1.3f, slope);
        float high = SStep(volcanoHeight * 0.35f, volcanoHeight * 0.8f, h - waterLevel);
        c = Color.Lerp(c, rock, Mathf.Clamp01(Mathf.Max(rocky, high)));
        return c;
    }

    // ---------- 용암 강 경로 유틸 (지형/용암/사망 판정 공유) ----------
    public float DistanceToRiver(float x, float z)
    {
        if (riverPath == null || riverPath.Length < 2) return 1e9f;
        Vector2 p = new Vector2(x, z);
        float best = 1e9f;
        for (int i = 0; i < riverPath.Length - 1; i++)
            best = Mathf.Min(best, DistSegment(p, riverPath[i], riverPath[i + 1]));
        return best;
    }

    static float DistSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float len2 = ab.sqrMagnitude;
        float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>(x,z)에서 가장 가까운 강 중심선 위의 점(XZ). 용암 표면 높이 = HeightAt(이 점)+lift 로 쓰임.</summary>
    public Vector2 NearestRiverPoint(float x, float z)
    {
        if (riverPath == null || riverPath.Length == 0) return new Vector2(x, z);
        if (riverPath.Length == 1) return riverPath[0];
        Vector2 p = new Vector2(x, z);
        float best = 1e9f; Vector2 bestPt = riverPath[0];
        for (int i = 0; i < riverPath.Length - 1; i++)
        {
            Vector2 a = riverPath[i], b = riverPath[i + 1];
            Vector2 ab = b - a; float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 q = a + ab * t;
            float d = Vector2.Distance(p, q);
            if (d < best) { best = d; bestPt = q; }
        }
        return bestPt;
    }

    /// <summary>강 중심선의 어느 쪽인지 부호(+1/-1, 가장 가까운 구간 기준). '건너편'이 아닌 '원래 쪽' 리스폰 판정용.</summary>
    public float SignedSideOfRiver(float x, float z)
    {
        if (riverPath == null || riverPath.Length < 2) return 0f;
        Vector2 p = new Vector2(x, z);
        float best = 1e9f, sign = 0f;
        for (int i = 0; i < riverPath.Length - 1; i++)
        {
            Vector2 a = riverPath[i], b = riverPath[i + 1];
            Vector2 ab = b - a; float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            float d = Vector2.Distance(p, a + ab * t);
            if (d < best) { best = d; sign = Mathf.Sign(ab.x * (p.y - a.y) - ab.y * (p.x - a.x)); }
        }
        return sign;
    }

    /// <summary>강 중심선을 0..1 로 샘플(균등 보간, 끝점 포함).</summary>
    public Vector2 RiverPoint(float u)
    {
        if (riverPath == null || riverPath.Length == 0) return Vector2.zero;
        if (riverPath.Length == 1) return riverPath[0];
        u = Mathf.Clamp01(u);
        float f = u * (riverPath.Length - 1);
        int i = Mathf.Min((int)f, riverPath.Length - 2);
        return Vector2.Lerp(riverPath[i], riverPath[i + 1], f - i);
    }

    public int RiverSegments => (riverPath != null ? riverPath.Length : 0);
}
