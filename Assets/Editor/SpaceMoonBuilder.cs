using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ★2026-09-05 우주 맵의 큰 달 메시 생성기(매끈한 구체 버전). Tools/Space/Rebuild Moon Mesh.
/// 정이십면체 6단 분할(40,962 정점) + 정점 공유(스무스 노멀) + 부드러운 크레이터(사발+림) + 저주파 '바다' 얼룩 + 미세 노이즈.
/// 정점색 = baseColor(달 맵 지형 톤) × 밝기 변주. 로우폴리(면 분리) 아님 — 달 맵의 자연스러운 둥근 느낌.
/// 에셋: Assets/Guide/Space/MoonMesh.asset (기존 것 삭제 후 생성 — MeshCollider bounds 0 함정 회피), 씬 Moon MeshFilter 재할당.
/// </summary>
public static class SpaceMoonBuilder
{
    public const string AssetPath = "Assets/Guide/Space/MoonMesh.asset";

    [MenuItem("Tools/Space/Rebuild Moon Mesh")]
    static void Menu() { Build(new Color(0.80f, 0.76f, 0.62f), 6, 20260905, true); }

    /// <param name="baseColor">기본 정점색</param><param name="subdiv">이십면체 분할 단계(5=10k, 6=41k 정점)</param>
    public static Mesh Build(Color baseColor, int subdiv, int seed, bool assignToScene)
    {
        var rng = new System.Random(seed);
        // 1) 이십면체
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var verts = new List<Vector3> {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        var tris = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        var cache = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            var m = ((verts[a] + verts[b]) * 0.5f).normalized; verts.Add(m); cache[key] = verts.Count - 1; return verts.Count - 1;
        }
        for (int s = 0; s < subdiv; s++)
        {
            var nt = new List<int>(tris.Count * 4);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris = nt;
        }
        // 2) 크레이터(방향, 각반경, 깊이) — 큰 것 몇 개 + 중간 + 작은 것 다수
        var craters = new List<Vector4>();
        void AddCraters(int n, float rMin, float rMax, float depthK)
        {
            for (int i = 0; i < n; i++)
            {
                var d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
                float r = Mathf.Lerp(rMin, rMax, (float)rng.NextDouble());
                craters.Add(new Vector4(d.x, d.y, d.z, r)); craters.Add(new Vector4(depthK * r, 0, 0, 0));   // 짝: [dir,r], [depth]
            }
        }
        AddCraters(6, 0.16f, 0.26f, 0.10f);
        AddCraters(18, 0.07f, 0.14f, 0.12f);
        AddCraters(60, 0.025f, 0.06f, 0.14f);
        // '바다'(어두운 저지대 얼룩) 중심들
        var maria = new List<Vector4>();
        for (int i = 0; i < 5; i++) { var d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized; maria.Add(new Vector4(d.x, d.y, d.z, Mathf.Lerp(0.35f, 0.6f, (float)rng.NextDouble()))); }
        float nOff = seed % 1000 * 0.37f;

        var pos = new Vector3[verts.Count]; var cols = new Color[verts.Count];
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            float h = 0f, floorK = 0f, rimK = 0f;
            for (int c = 0; c < craters.Count; c += 2)
            {
                var cd = new Vector3(craters[c].x, craters[c].y, craters[c].z); float r = craters[c].w; float depth = craters[c + 1].x;
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(v, cd), -1f, 1f));
                if (ang > r * 1.6f) continue;
                float u = ang / r;
                if (u < 1f) { float bowl = 1f - u * u; h -= depth * bowl * bowl; floorK = Mathf.Max(floorK, bowl); }
                float rim = Mathf.Exp(-Mathf.Pow((u - 1.05f) / 0.22f, 2f)); h += depth * 0.32f * rim; rimK = Mathf.Max(rimK, rim);
            }
            // 미세 기복(펄린 2옥타브) — 매끈하되 살짝 울퉁불퉁
            float n1 = Mathf.PerlinNoise(v.x * 6f + nOff, v.y * 6f + v.z * 3f + nOff) - 0.5f;
            float n2 = Mathf.PerlinNoise(v.z * 14f + nOff * 2f, v.x * 14f - v.y * 7f) - 0.5f;
            h += n1 * 0.010f + n2 * 0.004f;
            pos[i] = v * (1f + h);
            // 색: 기본 톤 × (바다 어둡게, 크레이터 바닥 어둡게, 림 밝게, 노이즈 변주)
            float mariaK = 0f;
            foreach (var m in maria) { float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(v, new Vector3(m.x, m.y, m.z)), -1f, 1f)); float e = Mathf.Clamp01(1f - ang / m.w); float wob = Mathf.PerlinNoise(v.x * 4f + m.w * 9f, v.z * 4f + m.y * 5f) * 0.6f + 0.4f; mariaK = Mathf.Max(mariaK, Mathf.SmoothStep(0f, 1f, e) * wob); }
            float bright = 1f + (n1 * 0.16f + n2 * 0.08f) - mariaK * 0.14f - floorK * 0.10f + rimK * 0.06f;
            var col = baseColor * bright;
            col.a = 1f;
            cols[i] = col;
        }
        var mesh = new Mesh { name = "MoonMesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = pos; mesh.triangles = tris.ToArray(); mesh.colors = cols;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
        if (old != null) AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(mesh, AssetPath); AssetDatabase.SaveAssets();
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
        if (assignToScene)
        {
            var moon = GameObject.Find("Moon");
            if (moon != null)
            {
                var mf = moon.GetComponent<MeshFilter>(); if (mf != null) mf.sharedMesh = saved;
                var mc = moon.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = saved;
                EditorUtility.SetDirty(moon);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(moon.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(moon.scene);
            }
        }
        Debug.Log("[SpaceMoonBuilder] verts=" + saved.vertexCount + " tris=" + saved.triangles.Length / 3 + " color=" + baseColor);
        return saved;
    }
}
