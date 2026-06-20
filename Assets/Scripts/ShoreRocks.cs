using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 용암 강이 바다와 맞닿는 '어귀'에만 각진 검은 바위(옵시디언) 덩어리들을 절차적으로 무리지어 만든다.
/// - 강 경로에서 지형이 물높이를 지나는 '용암 어귀'를 찾고, 그 해안선을 따라 짧게 바위들을 흩뿌린다.
/// - 각 바위 = 정이십면체를 노이즈로 찌그러뜨린 low-poly 페이셋 돌(매끈한 구 X, 각진 바위 O).
/// - 모두 한 메시로 합쳐 한 GO(ObsidianMat)로 그린다. 콜라이더 없음(시각). 원점·월드좌표.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ShoreRocks : MonoBehaviour
{
    public IslandTerrain island;
    [Range(3, 60)] public int rockCount = 16;
    [Tooltip("어귀 해안을 따라 바위가 퍼지는 길이")] public float spreadLength = 22f;
    [Tooltip("어귀 법선(바다↔육지) 방향 퍼짐")] public float spreadAcross = 6f;
    public float rockMin = 1.0f, rockMax = 2.9f;
    [Tooltip("바위 표면 울퉁불퉁(0=매끈, 0.5=거침)")] public float lumpiness = 0.32f;
    public int seed = 777;
    public float searchMin = 50f, searchMax = 240f;

    Mesh mesh;

    void OnEnable() { Rebuild(); }
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

    float H(int i, int k) { float s = Mathf.Sin(i * 12.9898f + k * 78.233f + seed * 0.137f) * 43758.5453f; return s - Mathf.Floor(s); }

    Vector2 FindLavaMouth()
    {
        float water = island.waterLevel;
        Vector2 last = island.RiverPoint(1f);
        for (float u = 0.2f; u <= 1f; u += 0.01f)
        {
            Vector2 p = island.RiverPoint(u);
            if (island.HeightAt(p.x, p.y) <= water + 0.4f) return p;
            last = p;
        }
        return last;
    }

    float ShoreRadius(Vector2 c, Vector2 dir)
    {
        float water = island.waterLevel;
        float prevR = searchMin;
        bool prevLand = island.HeightAt(c.x + dir.x * prevR, c.y + dir.y * prevR) > water;
        for (float r = searchMin + 3f; r <= searchMax; r += 3f)
        {
            bool land = island.HeightAt(c.x + dir.x * r, c.y + dir.y * r) > water;
            if (prevLand && !land)
            {
                float lo = prevR, hi = r;
                for (int b = 0; b < 9; b++) { float m = (lo + hi) * 0.5f; if (island.HeightAt(c.x + dir.x * m, c.y + dir.y * m) > water) lo = m; else hi = m; }
                return (lo + hi) * 0.5f;
            }
            prevR = r; prevLand = land;
        }
        return island.islandRadius;
    }

    // 정이십면체 (노이즈 찌그러뜨림 + 플랫셰이딩 → 각진 바위)
    static Vector3[] IcoVerts()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new[] {
            new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
            new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
            new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1)
        };
        for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
        return v;
    }
    static readonly int[] IcoTri = {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
        1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
        4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
    };

    void AppendRock(List<Vector3> V, List<Vector3> Nrm, List<int> T, Vector3 center, Vector3 scale, Quaternion rot, int rseed)
    {
        var ico = IcoVerts();
        var disp = new Vector3[ico.Length];
        for (int i = 0; i < ico.Length; i++)
        {
            Vector3 d = ico[i];
            float n = Mathf.PerlinNoise(d.x * 1.9f + rseed * 0.31f, d.y * 1.9f + d.z * 1.3f + rseed * 0.61f) - 0.5f;
            float rad = 1f + n * 2f * lumpiness;
            disp[i] = center + rot * Vector3.Scale(d * rad, scale);
        }
        for (int f = 0; f < IcoTri.Length; f += 3)
        {
            Vector3 a = disp[IcoTri[f]], b = disp[IcoTri[f + 1]], c = disp[IcoTri[f + 2]];
            Vector3 nm = Vector3.Cross(b - a, c - a).normalized;
            int baseI = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            Nrm.Add(nm); Nrm.Add(nm); Nrm.Add(nm);
            T.Add(baseI); T.Add(baseI + 1); T.Add(baseI + 2);
        }
    }

    void Rebuild()
    {
        if (island == null) { island = FindAnyObjectByType<IslandTerrain>(); if (island == null) return; }
        float water = island.waterLevel;
        Vector2 c = island.islandCenter;
        Vector2 mouth = FindLavaMouth();
        float mouthAng = Mathf.Atan2(mouth.y - c.y, mouth.x - c.x);
        Vector2 outDir = new Vector2(Mathf.Cos(mouthAng), Mathf.Sin(mouthAng));
        Vector2 tanDir = new Vector2(-outDir.y, outDir.x);
        float R = ShoreRadius(c, outDir);

        var V = new List<Vector3>(); var Nrm = new List<Vector3>(); var T = new List<int>();
        int N = Mathf.Clamp(rockCount, 1, 80);
        for (int i = 0; i < N; i++)
        {
            float along = (H(i, 1) - 0.5f) * spreadLength;
            float across = (H(i, 2) - 0.5f) * spreadAcross;
            // 호를 따라가도록 along 을 각도로 환산
            float ang = mouthAng + along / Mathf.Max(20f, R);
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float rr = ShoreRadius(c, dir);
            Vector2 xz = c + dir * rr + dir * across;     // 해안점 + 안팎 오프셋

            float size = Mathf.Lerp(rockMin, rockMax, H(i, 3));
            Vector3 scale = new Vector3(size * (0.85f + 0.5f * H(i, 4)), size * (0.55f + 0.4f * H(i, 5)), size * (0.85f + 0.5f * H(i, 6)));
            Quaternion rot = Quaternion.Euler((H(i, 7) - 0.5f) * 40f, H(i, 8) * 360f, (H(i, 9) - 0.5f) * 40f);
            float groundY = Mathf.Max(island.HeightAt(xz.x, xz.y), water);
            float cy = groundY + size * 0.15f;            // 살짝 박혀서 위로 솟음
            AppendRock(V, Nrm, T, new Vector3(xz.x, cy, xz.y), scale, rot, i * 7 + 1);
        }

        if (mesh == null) { mesh = new Mesh { name = "ShoreRocksMesh" }; mesh.hideFlags = HideFlags.DontSave; }
        mesh.Clear();
        mesh.indexFormat = (V.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(V);
        mesh.SetNormals(Nrm);
        mesh.SetTriangles(T, 0);
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        transform.position = Vector3.zero;
    }
}
