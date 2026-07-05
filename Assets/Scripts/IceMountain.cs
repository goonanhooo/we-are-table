using UnityEngine;

/// <summary>
/// 각진(저폴리 결정형) 눈 쌓인 설산/빙산 지형을 절차적으로 생성한다. (정글 IslandTerrain 의 설산 버전)
/// - 원형 falloff(빙산 외곽) + ridged 노이즈로 날카로운 능선, 중앙으로 솟는 큰 봉우리.
/// - ★flat shading(삼각형마다 정점 분리)으로 면이 또렷한 '얼음 결정/각진' 느낌.
/// - 약한 terrace(계단)로 얼음 단(shelf) 느낌.
/// - 높이/경사로 눈(흰색)·얼음(푸른색)·그늘 정점색을 구워 Custom/VertexColorLit 로 그린다.
/// - 높은 곳에 평평한 '스폰 대지(plateau)' — 책상이 하늘에서 떨어져 시작하는 자리.
/// - MeshCollider 자동. ExecuteAlways 라 에디터에서도 보이고 파라미터로 편집. 좌표는 월드 기준(이 GO 는 원점).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class IceMountain : MonoBehaviour
{
    [Header("지형 크기/해상도")]
    public float size = 700f;
    [Range(32, 400)] public int resolution = 130;   // flat shading 이라 정점수=res²×6. 낮을수록 면이 크고 더 각짐(저폴리).
    [Tooltip("전체 균등 축소(1=원본). 0.5면 정글과 비슷한 월드 크기. 노이즈 패턴 보존.")]
    public float worldScale = 0.5f;

    [Header("빙산 모양")]
    public Vector2 center = new Vector2(0f, 0f);
    public float icebergRadius = 150f;   // 외곽(물높이) 반경
    public float shoreWidth = 24f;       // 해안 falloff (좁을수록 가파른 빙벽)
    public float baseHeight = 8f;        // 물 위 기본 빙붕 높이
    public float seabedDepth = 16f;      // 물 아래 깊이
    public float coastNoise = 30f;       // 외곽선 들쭉날쭉(각진 빙산)
    public float waterLevel = 0f;
    public int seed = 7777;

    [Header("봉우리(설산)")]
    public Vector2 peakCenter = new Vector2(0f, 0f);
    public float peakHeight = 80f;       // design (≈40 월드 @0.5) — 큰 설산
    public float peakRadius = 150f;      // 봉우리 영향 반경
    [Range(1f, 3f)] public float peakSharp = 1.5f;   // 클수록 뾰족

    [Header("각진 능선/결정 (ridged)")]
    public float ridgeAmp = 22f;         // 능선 높이
    public float ridgeScale = 0.018f;    // 작을수록 큰 능선
    public float jitterAmp = 4f;         // 정점 소규모 들쑥(각진 표면 강조)
    public float jitterScale = 0.09f;

    [Header("얼음 단 (terrace)")]
    [Tooltip("계단 높이(design). 0이면 끔. 각진 빙붕 단 느낌.")]
    public float terraceStep = 7f;
    [Range(0f, 1f)] public float terraceAmount = 0.35f;

    [Header("스폰 대지 (높고 평평 — 책상 낙하 시작점)")]
    public bool spawnPlateau = true;
    public Vector2 plateauCenter = new Vector2(0f, 64f);   // 봉우리 북쪽 어깨
    public float plateauRadius = 26f;
    public float plateauHeight = 52f;     // design 절대 높이(평평) — 높은 곳
    public float plateauEdge = 8f;        // 가장자리 blend 폭(작을수록 또렷한 단)

    [Header("동굴 (산속 파냄 — 직사각 풋프린트 안 지형을 floor로 내림. 천장/지붕은 별도 메시)")]
    public bool cave = true;
    public Vector2 caveCenter = new Vector2(0f, 34f);  // design (world (0,17))
    public float caveHalfX = 38f;     // design 절반 폭(world 19) — 넓게
    public float caveHalfZ = 28f;     // design 절반 깊이
    public float caveFloor = 52f;     // design 바닥 높이(= plateauHeight, world 26)
    public float caveEdge = 6f;       // 벽 blend(작을수록 가파른 얼음 벽)

    [Header("슬라이드 트렌치 (슬라이드-빙산 겹침 방지 — world 좌표)")]
    [Tooltip("슬라이드 복도 아래 지형을 하강선 아래로 '파내기만' 해 슬라이드와 겹치지 않게.")]
    public bool slideTrench = true;
    public float trenchHalfXWorld = 5f;    // world 반폭(안쪽 완전 카브)
    public float trenchEdgeWorld = 3.5f;   // world 바깥 blend(클수록 벽 완만)
    public float trenchZStartWorld = 42f;  // 시작 z(플라토와 연결)
    public float trenchZEndWorld = 74f;    // 끝 z(이후엔 지형이 이미 낮아 카브 불필요)
    public float trenchZBlend = 3f;        // z 양끝 blend
    public float trenchTopWorld = 26f;     // 시작 지점 카브 높이(= 플라토 y26)
    public float trenchSlope = 0.53f;      // world z당 하강량(슬라이드 하강에 맞춤)
    [Tooltip("하강선 최저 높이(world) — 계곡/킥커 구간을 평평하게 파려면 계곡보다 살짝 낮게. -999=클램프 없음")]
    public float trenchFloorWorld = -999f; // 하강선이 이 값 밑으로는 안 내려감(계곡 바닥 평탄화)

    [Header("눈사람 방 정사각 홈 (머리가 굴러 나가지 않게 — world 좌표)")]
    public bool snowPit = false;
    public float snowPitXWorld = 0f;        // 홈 중심(world)
    public float snowPitZWorld = 8.5f;
    public float snowPitHalfWorld = 6.5f;   // 정사각 반변
    public float snowPitEdgeWorld = 1.6f;   // 가장자리 경사 blend
    public float snowPitDepthWorld = 1.1f;  // 깊이

    [Header("로우폴리 패치워크(면별 미세 색 변화 — 평평한 방바닥/플라토도 폴리곤 결)")]
    [Range(0f, 0.3f)] public float faceColorJitter = 0.10f;

    Mesh mesh;       // 렌더용 flat-shaded(정점 분리)
    Mesh colMesh;    // 콜라이더용 welded 그리드(분리 메시를 콜라이더로 쓰면 degenerate→물리 NaN/크래시)
    int builtHash = -1;

    void OnEnable() { Rebuild(); }
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

    int ParamHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + size.GetHashCode(); h = h * 31 + resolution; h = h * 31 + worldScale.GetHashCode();
            h = h * 31 + center.GetHashCode() + icebergRadius.GetHashCode() + shoreWidth.GetHashCode();
            h = h * 31 + baseHeight.GetHashCode() + seabedDepth.GetHashCode() + coastNoise.GetHashCode() + waterLevel.GetHashCode() + seed;
            h = h * 31 + peakCenter.GetHashCode() + peakHeight.GetHashCode() + peakRadius.GetHashCode() + peakSharp.GetHashCode();
            h = h * 31 + ridgeAmp.GetHashCode() + ridgeScale.GetHashCode() + jitterAmp.GetHashCode() + jitterScale.GetHashCode();
            h = h * 31 + terraceStep.GetHashCode() + terraceAmount.GetHashCode();
            h = h * 31 + (spawnPlateau ? 1 : 0) + plateauCenter.GetHashCode() + plateauRadius.GetHashCode() + plateauHeight.GetHashCode() + plateauEdge.GetHashCode();
            h = h * 31 + (cave ? 1 : 0) + caveCenter.GetHashCode() + caveHalfX.GetHashCode() + caveHalfZ.GetHashCode() + caveFloor.GetHashCode() + caveEdge.GetHashCode();
            h = h * 31 + (slideTrench ? 1 : 0) + trenchHalfXWorld.GetHashCode() + trenchEdgeWorld.GetHashCode() + trenchZStartWorld.GetHashCode() + trenchZEndWorld.GetHashCode() + trenchZBlend.GetHashCode() + trenchTopWorld.GetHashCode() + trenchSlope.GetHashCode() + trenchFloorWorld.GetHashCode();
            h = h * 31 + (snowPit ? 1 : 0) + snowPitXWorld.GetHashCode() + snowPitZWorld.GetHashCode() + snowPitHalfWorld.GetHashCode() + snowPitEdgeWorld.GetHashCode() + snowPitDepthWorld.GetHashCode();
            h = h * 31 + faceColorJitter.GetHashCode();
            return h;
        }
    }

    void Rebuild()
    {
        int hash = ParamHash();
        if (hash == builtHash && mesh != null) return;

        int res = Mathf.Clamp(resolution, 8, 400);
        int vside = res + 1;
        float half = size * 0.5f;
        float step = size / res;

        // 1) 격자 높이/색 (design 좌표)
        var grid = new Vector3[vside * vside];
        for (int z = 0; z < vside; z++)
            for (int x = 0; x < vside; x++)
            {
                float wx = -half + x * step, wz = -half + z * step;
                grid[z * vside + x] = new Vector3(wx, HeightDesign(wx, wz), wz);
            }

        // 2) flat shading: 삼각형마다 정점 3개 분리 → 면별 노멀(각진 면)
        int quads = res * res;
        var verts = new Vector3[quads * 6];
        var cols = new Color[quads * 6];
        var tris = new int[quads * 6];
        int vi = 0;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int i = z * vside + x;
                Vector3 a = grid[i], b = grid[i + vside], c = grid[i + 1], d = grid[i + vside + 1];
                AddTri(verts, cols, tris, ref vi, a, b, c);
                AddTri(verts, cols, tris, ref vi, c, b, d);
            }

        // 3) worldScale 균등 축소
        if (worldScale != 1f)
            for (int k = 0; k < verts.Length; k++) verts[k] *= worldScale;

        if (mesh == null) { mesh = new Mesh { name = "IceMountainMesh" }; mesh.hideFlags = HideFlags.DontSave; }
        mesh.Clear();
        mesh.indexFormat = (verts.Length > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.colors = cols;
        mesh.triangles = tris;
        mesh.RecalculateNormals();   // flat(정점 분리)이라 면별 노멀
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        // 콜라이더: welded 그리드 메시(렌더용 flat split 메시를 콜라이더로 쓰면 degenerate 삼각형 →
        //  접촉 노멀 깨짐(IsNormalized/det) → 물리 NaN/폭발/크래시. 정글 IslandTerrain 처럼 welded 사용).
        float ws = (worldScale != 1f) ? worldScale : 1f;
        var cverts = new Vector3[grid.Length];
        for (int k = 0; k < grid.Length; k++) cverts[k] = grid[k] * ws;
        var ctris = new int[res * res * 6];
        int ct = 0;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int gi = z * vside + x;
                ctris[ct++] = gi; ctris[ct++] = gi + vside; ctris[ct++] = gi + 1;
                ctris[ct++] = gi + 1; ctris[ct++] = gi + vside; ctris[ct++] = gi + vside + 1;
            }
        if (colMesh == null) { colMesh = new Mesh { name = "IceColliderMesh" }; colMesh.hideFlags = HideFlags.DontSave; }
        colMesh.Clear();
        colMesh.indexFormat = (cverts.Length > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        colMesh.vertices = cverts; colMesh.triangles = ctris;
        colMesh.RecalculateNormals(); colMesh.RecalculateBounds();
        var mc = GetComponent<MeshCollider>();
        mc.sharedMesh = null; mc.sharedMesh = colMesh;
        builtHash = hash;
    }

    void AddTri(Vector3[] v, Color[] c, int[] t, ref int vi, Vector3 a, Vector3 b, Vector3 d)
    {
        Vector3 n = Vector3.Cross(b - a, d - a);
        float slope = 1f - Mathf.Clamp01(n.normalized.y);   // 0=평평, 1=수직
        Vector3 ctr = (a + b + d) / 3f;
        Color col = VertexColor(ctr.x, ctr.z, ctr.y, slope);
        // ★면별 미세 색 변화(결정적 해시) — 조명이 같은 평평한 바닥도 폴리곤별로 톤이 달라 로우폴리 결이 살아남
        if (faceColorJitter > 0f)
        {
            float fh = Mathf.Sin(ctr.x * 12.9898f + ctr.z * 78.233f) * 43758.5453f;
            fh -= Mathf.Floor(fh);
            float tint = 1f - faceColorJitter * 0.5f + fh * faceColorJitter;
            col = new Color(Mathf.Clamp01(col.r * tint), Mathf.Clamp01(col.g * tint),
                            Mathf.Clamp01(col.b * (1f - faceColorJitter * 0.25f + fh * faceColorJitter * 0.5f)), 1f);
        }
        v[vi] = a; c[vi] = col; t[vi] = vi; vi++;
        v[vi] = b; c[vi] = col; t[vi] = vi; vi++;
        v[vi] = d; c[vi] = col; t[vi] = vi; vi++;
    }

    static float SStep(float e0, float e1, float x)
    {
        if (Mathf.Abs(e1 - e0) < 1e-6f) return x < e0 ? 0f : 1f;
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    float Fbm(float x, float z)
    {
        float s = seed * 0.1234f, v = 0f, a = 0.5f, f = 1f;
        for (int o = 0; o < 4; o++) { v += a * (Mathf.PerlinNoise(x * f + s, z * f + s) - 0.5f); f *= 2.03f; a *= 0.5f; }
        return v; // ~-0.5..0.5
    }

    // ridged: 날카로운 능선(0..1, 능선에서 1)
    float Ridged(float x, float z)
    {
        float s = seed * 0.077f, v = 0f, a = 0.5f, f = 1f;
        for (int o = 0; o < 4; o++)
        {
            float n = Mathf.PerlinNoise(x * f + s, z * f + s);
            n = 1f - Mathf.Abs(2f * n - 1f);   // 능선
            v += a * n * n; f *= 2.07f; a *= 0.5f;
        }
        return v; // ~0..1
    }

    /// <summary>월드 (x,z) 의 최종 지형 높이(worldScale 반영). 외부(스폰/슬라이드 배치)는 이걸 사용.</summary>
    public float HeightAt(float x, float z) => worldScale * HeightDesign(x / worldScale, z / worldScale);

    float HeightDesign(float x, float z)
    {
        float dx = x - center.x, dz = z - center.y;
        float d = Mathf.Sqrt(dx * dx + dz * dz);
        float coast = (Ridged(x * 0.012f, z * 0.012f) - 0.4f) * coastNoise;   // 들쭉날쭉 빙산 외곽
        float dd = d + coast;
        float landMask = 1f - SStep(icebergRadius - shoreWidth * 0.5f, icebergRadius + shoreWidth * 0.5f, dd);

        float seabed = waterLevel - seabedDepth;
        float h = Mathf.Lerp(seabed, waterLevel + baseHeight, landMask);

        // 봉우리(중앙으로 솟음)
        float pdx = x - peakCenter.x, pdz = z - peakCenter.y;
        float pd = Mathf.Sqrt(pdx * pdx + pdz * pdz);
        float m = Mathf.Clamp01(1f - pd / peakRadius);
        float mountain = peakHeight * Mathf.Pow(m, peakSharp);

        // 각진 능선
        float ridges = ridgeAmp * (Ridged(x * ridgeScale, z * ridgeScale) - 0.25f);
        // 소규모 들쑥(각진 표면)
        float jitter = jitterAmp * Fbm(x * jitterScale, z * jitterScale);

        h += (mountain + ridges + jitter) * landMask;

        // 얼음 단(terrace) — 육지에서만 약하게
        if (terraceStep > 0.01f && terraceAmount > 0.001f)
        {
            float q = Mathf.Round(h / terraceStep) * terraceStep;
            h = Mathf.Lerp(h, q, terraceAmount * landMask);
        }

        // 스폰 대지(높고 평평) — 또렷한 단
        if (spawnPlateau && plateauRadius > 0.01f)
        {
            float ld = Mathf.Sqrt((x - plateauCenter.x) * (x - plateauCenter.x) + (z - plateauCenter.y) * (z - plateauCenter.y));
            float k = 1f - SStep(plateauRadius - plateauEdge, plateauRadius, ld); // 안=1, 가장자리=0
            h = Mathf.Lerp(h, plateauHeight, k);
        }

        // 동굴: 직사각 풋프린트 안의 지형을 caveFloor 로 '파내기만'(이미 낮으면 유지). 옆/뒤는 산이 남아 벽이 됨.
        if (cave)
        {
            float ax = Mathf.Abs(x - caveCenter.x), az = Mathf.Abs(z - caveCenter.y);
            float kx = 1f - SStep(caveHalfX - caveEdge, caveHalfX, ax);
            float kz = 1f - SStep(caveHalfZ - caveEdge, caveHalfZ, az);
            float kc = kx * kz;
            if (caveFloor < h) h = Mathf.Lerp(h, caveFloor, kc);
        }

        // 슬라이드 트렌치: 슬라이드 복도 아래 지형을 하강선(target) 아래로 파내기만(낮추기 전용).
        // HeightDesign 은 design 좌표라 world 로 환산해 계산 후 다시 design 높이로 되돌린다.
        if (slideTrench)
        {
            float wx = x * worldScale, wz = z * worldScale;   // world 좌표
            if (wz > trenchZStartWorld - trenchZBlend && wz < trenchZEndWorld + trenchZBlend)
            {
                float kx = 1f - SStep(trenchHalfXWorld, trenchHalfXWorld + trenchEdgeWorld, Mathf.Abs(wx));
                float kzA = SStep(trenchZStartWorld - trenchZBlend, trenchZStartWorld, wz);      // 시작 blend
                float kzB = 1f - SStep(trenchZEndWorld, trenchZEndWorld + trenchZBlend, wz);     // 끝 blend
                float k = kx * kzA * kzB;
                float targetWorld = trenchTopWorld - trenchSlope * (wz - trenchZStartWorld);     // 슬라이드 따라 하강
                if (targetWorld < trenchFloorWorld) targetWorld = trenchFloorWorld;              // 계곡 바닥 평탄화
                float targetDesign = targetWorld / worldScale;
                float carved = Mathf.Lerp(h, targetDesign, k);
                if (carved < h) h = carved;   // 낮추기만(솟은 빙산 제거)
            }
        }

        // 눈사람 방 정사각 홈: 방 가운데를 움푹하게 — 머리(공)가 굴러 나가지 않는 우리(정사각=체비셰프 거리).
        if (snowPit)
        {
            float pwx = x * worldScale, pwz = z * worldScale;
            float cheb = Mathf.Max(Mathf.Abs(pwx - snowPitXWorld), Mathf.Abs(pwz - snowPitZWorld));
            if (cheb < snowPitHalfWorld + snowPitEdgeWorld)
            {
                float k = 1f - SStep(snowPitHalfWorld - snowPitEdgeWorld, snowPitHalfWorld + snowPitEdgeWorld, cheb);
                h -= (snowPitDepthWorld / worldScale) * k;
            }
        }

        return h;
    }

    Color VertexColor(float x, float z, float h, float slope)
    {
        Color snow = new Color(0.95f, 0.97f, 1.0f);     // 밝은 눈
        Color snowShade = new Color(0.78f, 0.85f, 0.95f); // 푸른 그늘 눈
        Color ice = new Color(0.60f, 0.78f, 0.92f);     // 얼음(노출 경사)
        Color iceDeep = new Color(0.40f, 0.62f, 0.82f); // 깊은 크레바스 푸른빛
        Color wet = new Color(0.55f, 0.72f, 0.85f);     // 물가 젖은 얼음

        // 경사 → 얼음 노출
        float steep = SStep(0.35f, 0.8f, slope);
        Color c = Color.Lerp(snow, ice, steep);
        // 매우 가파른 곳 → 깊은 푸른 얼음
        c = Color.Lerp(c, iceDeep, SStep(0.75f, 1.0f, slope));
        // 물가(낮은 곳) → 젖은 얼음/푸른빛
        float low = 1f - SStep(waterLevel + 0.4f, waterLevel + 6f, h);
        c = Color.Lerp(c, wet, low * 0.7f);
        // 미세 그늘 변주
        c = Color.Lerp(c, snowShade, Mathf.Clamp01(Fbm(x * 0.05f, z * 0.05f) + 0.5f) * 0.2f * (1f - steep));
        return c;
    }
}
