using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// ★2026-09-04 IceCavern 동굴 생성기(에디터 전용). 수평 동굴(H, 링 세움) + 수직 샤프트(V, 수평 링, 위는 돔)를 서로 잘라 합집합으로 만들고
/// 조각(청크)으로 나눠 저장, 종유석/석순/레일 첨탑/얼음 조각/발광 결정을 레이캐스트로 배치한다.
/// 사용: 메뉴 Tools/IceCavern/Rebuild Cavern (씬 IceCavern이 열려 있어야 함). 파라미터는 아래 상수/배열을 편집.
/// </summary>
public static class IceCavernBuilder
{
    static System.Random rng;
    static float R01() => (float)rng.NextDouble();
    static float RS(float a) => (R01() - 0.5f) * 2f * a;
    static Color Rock() { Color brown = new Color(0.42f, 0.32f, 0.24f), gray = new Color(0.44f, 0.42f, 0.40f), dark = new Color(0.30f, 0.25f, 0.21f); Color c = Color.Lerp(Color.Lerp(brown, gray, R01()), dark, R01() * 0.55f); float j = 1f + RS(0.10f); return new Color(c.r * j, c.g * j, c.b * j, 1f); }
    static Color Ice() { float j = 1f + RS(0.06f); return new Color(0.86f * j, 0.92f * j, Mathf.Min(1f, j), 1f); }
    static float PIce(Vector3 p) => Mathf.Clamp01(0.55f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-170f, -55f, p.z)) + 0.5f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-10f, 95f, p.y)));
    static Color ByPos(Vector3 p) => R01() < PIce(p) ? Ice() : Rock();

    class MB
    {
        public List<Vector3> v = new List<Vector3>(); public List<Color> c = new List<Color>(); public List<int> t = new List<int>();
        public void Tri(Vector3 a, Vector3 b, Vector3 d, Vector3 want, Color col) { if (Vector3.Dot(Vector3.Cross(b - a, d - a), want) < 0f) { var tmp = b; b = d; d = tmp; } int i = v.Count; v.Add(a); v.Add(b); v.Add(d); c.Add(col); c.Add(col); c.Add(col); t.Add(i); t.Add(i + 1); t.Add(i + 2); }
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 want, System.Func<Vector3, Color> col) { Tri(a, b, cc, want, col((a + b + cc) / 3f)); Tri(a, cc, d, want, col((a + cc + d) / 3f)); }
    }
    static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) { float t2 = t * t, t3 = t2 * t; return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3); }
    static float CRf(float p0, float p1, float p2, float p3, float t) { float t2 = t * t, t3 = t2 * t; return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3); }

    static int SplitMake(MB mb, string baseName, Transform parent, Material mat, System.Func<Vector3, string> bucket)
    {
        var groups = new Dictionary<string, List<int>>();
        for (int i = 0; i < mb.t.Count; i += 3) { Vector3 cen = (mb.v[mb.t[i]] + mb.v[mb.t[i + 1]] + mb.v[mb.t[i + 2]]) / 3f; string k = bucket(cen); if (!groups.ContainsKey(k)) groups[k] = new List<int>(); groups[k].Add(i); }
        int made = 0;
        foreach (var kv in groups)
        {
            var nv = new List<Vector3>(); var nc = new List<Color>(); var nt = new List<int>();
            foreach (int i in kv.Value) for (int k = 0; k < 3; k++) { nt.Add(nv.Count); nv.Add(mb.v[mb.t[i + k]]); nc.Add(mb.c[mb.t[i + k]]); }
            var m = new Mesh { name = baseName + "_" + kv.Key }; m.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(nv); m.SetColors(nc); m.SetTriangles(nt, 0); m.RecalculateNormals(); m.RecalculateBounds();
            if (!AssetDatabase.IsValidFolder("Assets/Guide/Cavern/Chunks")) AssetDatabase.CreateFolder("Assets/Guide/Cavern", "Chunks");
            string path = "Assets/Guide/Cavern/Chunks/" + m.name + ".asset";
            // ★같은 경로의 옛 에셋을 덮어쓰면 MeshCollider가 빈 바운즈로 쿠킹되는 경우가 있어 먼저 삭제
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path); var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var g = new GameObject(m.name); g.transform.SetParent(parent, false); g.AddComponent<MeshFilter>().sharedMesh = asset; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = g.AddComponent<MeshCollider>(); col.sharedMesh = asset;
            if (col.bounds.extents.sqrMagnitude < 0.01f) { col.sharedMesh = null; col.sharedMesh = asset; }
            if (col.bounds.extents.sqrMagnitude < 0.01f) Debug.LogWarning("[IceCavernBuilder] collider bounds empty: " + m.name);
            made++;
        }
        return made;
    }
    static void Cone(MB mb, Vector3 b, Vector3 dir, float len, float r0, float r1, int N, System.Func<Color> col, float jit)
    {
        Vector3 d = dir.normalized; Vector3 u1 = Vector3.Cross(d, Vector3.up); if (u1.sqrMagnitude < 0.01f) u1 = Vector3.Cross(d, Vector3.right); u1.Normalize(); Vector3 u2 = Vector3.Cross(d, u1);
        Vector3 tip = b + d * len; Vector3[] ra = new Vector3[N + 1], rb = new Vector3[N + 1];
        for (int i = 0; i <= N; i++) { float thh = Mathf.PI * 2f * i / N; float j0 = i == N ? 1f : 1f + RS(jit), j1 = i == N ? 1f : 1f + RS(jit); ra[i] = b + (u1 * Mathf.Cos(thh) + u2 * Mathf.Sin(thh)) * (r0 * j0); rb[i] = tip + (u1 * Mathf.Cos(thh) + u2 * Mathf.Sin(thh)) * (r1 * j1); }
        ra[N] = ra[0]; rb[N] = rb[0];
        for (int i = 0; i < N; i++) { Vector3 cen = (ra[i] + ra[i + 1] + rb[i] + rb[i + 1]) * 0.25f; Vector3 axisPt = b + d * Vector3.Dot(cen - b, d); Vector3 outw = cen - axisPt; var cc = col(); mb.Tri(ra[i], ra[i + 1], rb[i + 1], outw, cc); mb.Tri(ra[i], rb[i + 1], rb[i], outw, cc); }
        var tc = col(); for (int i = 0; i < N; i++) mb.Tri(tip, rb[i], rb[i + 1], d, tc);
    }

    // ── 파라미터(에디터에서 편집) ──
    public static Vector3[] HP = { new Vector3(0, -125, -392), new Vector3(0, -122, -362), new Vector3(0, -118, -310), new Vector3(0, -116, -255), new Vector3(0, -114, -200), new Vector3(0, -108, -150), new Vector3(0, -95, -118), new Vector3(0, -75, -96), new Vector3(0, -60, -78) };
    public static float[] HW = { 30, 70, 130, 160, 152, 125, 90, 55, 22 };
    public static float[] HH = { 30, 55, 85, 88, 90, 84, 70, 50, 28 };
    public const float FLOOR = -200f, FARZ = -370f, CLIPZ = -22f;
    public const float VZ = -48f, VBOT = -115f, VTOP = 108f;   // ★샤프트 위는 108에서 돔(113.6)으로 마감 — 지형(120-6) 아래, 깎아지른 모서리 없음
    static float Wv(float y) => Mathf.Lerp(40f, 13f, Mathf.Pow(Mathf.InverseLerp(VBOT, VTOP, y), 1.3f));
    static float Hv(float y) => Mathf.Lerp(48f, 27f, Mathf.Pow(Mathf.InverseLerp(VBOT, VTOP, y), 1.1f));

    [MenuItem("Tools/IceCavern/Rebuild Cavern")]
    public static void RebuildMenu() { Debug.Log(Build()); }

    public static string Build() { string a = BuildTube(); if (a.StartsWith("playing")) return a; string b = BuildDecor(); return a + b; }

    public static string BuildTube()
    {
        if (Application.isPlaying) return "playing";
        rng = new System.Random(37);
        var sb = new StringBuilder();
        var im = Object.FindAnyObjectByType<IceMountain>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Guide/Cavern/CaveRockMat.mat");
        var root = GameObject.Find("Cavern"); if (root == null) root = new GameObject("Cavern");
        var del = new List<GameObject>();
        foreach (Transform c in root.transform) if (c.name.StartsWith("CaveTube_") || c.name.StartsWith("Stalactites") || c.name == "Decor" || c.name == "Crystals") del.Add(c.gameObject);
        foreach (var g in del) Object.DestroyImmediate(g);
        var lg = root.transform.Find("CavernLights"); if (lg != null) { var dl = new List<GameObject>(); foreach (Transform c in lg) if (c.name.StartsWith("Crystal_")) dl.Add(c.gameObject); foreach (var g in dl) Object.DestroyImmediate(g); }

        System.Func<float, float, float> terrainMin = (x, z) => { float h = im.HeightAt(x, Mathf.Min(z, -22.5f)); for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; h = Mathf.Min(h, im.HeightAt(x + 4f * Mathf.Cos(a), Mathf.Min(z + 4f * Mathf.Sin(a), -22.5f))); } return h; };
        System.Func<float, float, float> floorY = (x, z) => FLOOR + 5f * Mathf.Sin(0.04f * x + 1f) * Mathf.Sin(0.05f * z) + 3f * Mathf.Sin(0.11f * x) * Mathf.Cos(0.09f * z);

        // H 중심선 샘플 + z 조회표
        var cl = new List<Vector3>(); var cw = new List<float>(); var chh = new List<float>();
        for (int i = 0; i < HP.Length - 1; i++) { int i0 = Mathf.Max(0, i - 1), i3 = Mathf.Min(HP.Length - 1, i + 2); for (int k = 0; k < 40; k++) { float t = k / 40f; cl.Add(CR(HP[i0], HP[i], HP[i + 1], HP[i3], t)); cw.Add(Mathf.Max(6f, CRf(HW[i0], HW[i], HW[i + 1], HW[i3], t))); chh.Add(Mathf.Max(6f, CRf(HH[i0], HH[i], HH[i + 1], HH[i3], t))); } }
        cl.Add(HP[HP.Length - 1]); cw.Add(HW[HW.Length - 1]); chh.Add(HH[HH.Length - 1]);
        int ZN = 400; float[] tcy = new float[ZN], tw = new float[ZN], th = new float[ZN]; for (int i = 0; i < ZN; i++) tw[i] = -1f;
        for (int i = 0; i < cl.Count; i++) { int zi = Mathf.Clamp(Mathf.RoundToInt(cl[i].z + 400f), 0, ZN - 1); tcy[zi] = cl[i].y; tw[zi] = cw[i]; th[zi] = chh[i]; }
        for (int i = 1; i < ZN; i++) if (tw[i] < 0f && tw[i - 1] > 0f) { tw[i] = tw[i - 1]; th[i] = th[i - 1]; tcy[i] = tcy[i - 1]; }
        System.Func<Vector3, bool> insideH = p => { if (p.z < FARZ || p.z > -78f) return false; int zi = Mathf.Clamp(Mathf.RoundToInt(p.z + 400f), 0, ZN - 1); if (tw[zi] <= 0f) return false; float ex = p.x / (tw[zi] * 0.97f), ey = (p.y - tcy[zi]) / (th[zi] * 0.97f); return ex * ex + ey * ey < 1f; };
        System.Func<Vector3, bool> insideV = p => { if (p.y < VBOT || p.y > VTOP + 6f || p.z > CLIPZ + 1f) return false; float yy = Mathf.Min(p.y, VTOP); float ex = p.x / (Wv(yy) * 0.97f), ez = (p.z - VZ) / (Hv(yy) * 0.97f); return ex * ex + ez * ez < 1f; };

        var rc = new List<Vector3>(); var rw = new List<float>(); var rhh = new List<float>(); float accum = 999f;
        for (int i = 0; i < cl.Count; i++) { if (i > 0) accum += (cl[i] - cl[i - 1]).magnitude; if (accum < 5f && i != cl.Count - 1) continue; accum = 0f; rc.Add(cl[i]); rw.Add(cw[i]); rhh.Add(chh[i]); }
        const int NA = 64; int NR = rc.Count;
        Vector3[,] ring = new Vector3[NR, NA + 1]; Vector3[,] ringU = new Vector3[NR, NA + 1];
        for (int k = 0; k < NR; k++)
        {
            for (int i = 0; i <= NA; i++)
            {
                float phi = Mathf.PI * 2f * (i % NA) / NA;
                float n = 1f + 0.11f * Mathf.Sin(3f * phi + 0.7f * k) + 0.07f * Mathf.Sin(7f * phi + 1.3f * k) + 0.04f * Mathf.Sin(13f * phi + 0.4f * k) + (i == NA ? 0f : RS(0.03f));
                if (i == NA) n = 1f + 0.11f * Mathf.Sin(0.7f * k) + 0.07f * Mathf.Sin(1.3f * k) + 0.04f * Mathf.Sin(0.4f * k);
                Vector3 p = rc[k] + Vector3.right * (rw[k] * n * Mathf.Cos(phi)) + Vector3.up * (rhh[k] * n * Mathf.Sin(phi));
                ringU[k, i] = p;
                float tm = terrainMin(p.x, p.z) - 6f; if (p.y > tm) p.y = tm;
                float fy = floorY(p.x, p.z); if (p.y < fy) p.y = fy;
                if (p.z < FARZ) p.z = FARZ + (i == NA ? 0f : R01() * 0.5f);
                ring[k, i] = p;
            }
            ring[k, NA] = ring[k, 0]; ringU[k, NA] = ringU[k, 0];
        }
        var tube = new MB(); int skipAlc = 0, skipV = 0;
        for (int k = 0; k < NR - 1; k++)
            for (int i = 0; i < NA; i++)
            {
                Vector3 a = ring[k, i], b = ring[k, i + 1], c = ring[k + 1, i + 1], d = ring[k + 1, i];
                Vector3 cen = (a + b + c + d) * 0.25f; float minZ = Mathf.Min(a.z, b.z, c.z, d.z);
                if (minZ <= FARZ + 0.6f && Mathf.Abs(cen.x) < 26f && cen.y > -141f && cen.y < -100f) { skipAlc++; continue; }
                if (insideV(cen)) { skipV++; continue; }
                Vector3 cenU = (ringU[k, i] + ringU[k, i + 1] + ringU[k + 1, i + 1] + ringU[k + 1, i]) * 0.25f;
                tube.Quad(a, b, c, d, (rc[k] + rc[k + 1]) * 0.5f - cenU, ByPos);
            }
        int hTris = tube.t.Count / 3;

        // V 링: 바닥→VTOP 3.5m 간격, 그 위 돔 링 2개(반지름 축소) + 꼭대기 부채꼴
        var vr = new List<float>(); var vs = new List<float>();
        for (float y = VBOT; y < VTOP; y += 3.5f) { vr.Add(y); vs.Add(1f); }
        vr.Add(VTOP); vs.Add(1f); vr.Add(VTOP + 2.6f); vs.Add(0.62f); vr.Add(VTOP + 4.6f); vs.Add(0.3f);
        int NV = vr.Count; Vector3[,] vring = new Vector3[NV, NA + 1]; Vector3[,] vringU = new Vector3[NV, NA + 1];
        for (int k = 0; k < NV; k++)
        {
            float y = vr[k]; float yy = Mathf.Min(y, VTOP); float sc = vs[k];
            for (int i = 0; i <= NA; i++)
            {
                float phi = Mathf.PI * 2f * (i % NA) / NA;
                float n = 1f + 0.10f * Mathf.Sin(3f * phi + 0.9f * k) + 0.06f * Mathf.Sin(7f * phi + 1.7f * k) + (i == NA ? 0f : RS(0.03f));
                if (i == NA) n = 1f + 0.10f * Mathf.Sin(0.9f * k) + 0.06f * Mathf.Sin(1.7f * k);
                Vector3 p = new Vector3(Wv(yy) * sc * n * Mathf.Cos(phi), y + (k == 0 || k >= NV - 3 ? 0f : RS(0.6f)), VZ + Hv(yy) * sc * n * Mathf.Sin(phi));
                vringU[k, i] = p;
                float tm = terrainMin(p.x, p.z) - 6f; if (p.y > tm) p.y = tm;
                if (p.z > CLIPZ) p.z = CLIPZ - (i == NA ? 0f : R01() * 0.5f);
                vring[k, i] = p;
            }
            vring[k, NA] = vring[k, 0]; vringU[k, NA] = vringU[k, 0];
        }
        int skipH = 0, skipDoor = 0;
        for (int k = 0; k < NV - 1; k++)
            for (int i = 0; i < NA; i++)
            {
                Vector3 a = vring[k, i], b = vring[k, i + 1], c = vring[k + 1, i + 1], d = vring[k + 1, i];
                Vector3 cen = (a + b + c + d) * 0.25f; float minZ = Mathf.Min(a.z, b.z, c.z, d.z), maxZ = Mathf.Max(a.z, b.z, c.z, d.z);
                if (insideH(cen)) { skipH++; continue; }
                if (maxZ >= CLIPZ - 0.6f && minZ >= CLIPZ - 2.5f && Mathf.Abs(cen.x) < 6.5f && cen.y > 91f && cen.y < 108f) { skipDoor++; continue; }
                Vector3 cenU = (vringU[k, i] + vringU[k, i + 1] + vringU[k + 1, i + 1] + vringU[k + 1, i]) * 0.25f;
                tube.Quad(a, b, c, d, new Vector3(0f, Mathf.Min(cenU.y, VTOP), VZ) - cenU, ByPos);
            }
        { Vector3 cen0 = new Vector3(0f, VBOT, VZ); for (int i = 0; i < NA; i++) { Vector3 a = vring[0, i], b = vring[0, i + 1]; if (insideH((cen0 + a + b) / 3f)) continue; tube.Tri(cen0, a, b, Vector3.up, Rock()); } }
        { float ty = 999f; for (int i = 0; i < NA; i++) ty = Mathf.Min(ty, vring[NV - 1, i].y); Vector3 cen = new Vector3(0f, Mathf.Min(ty + 1.2f, 113.6f), VZ); for (int i = 0; i < NA; i++) tube.Tri(cen, vring[NV - 1, i], vring[NV - 1, i + 1], Vector3.down, Ice()); }
        sb.Append("H rings=" + NR + " tris=" + hTris + " skipInsideV=" + skipV + "; V rings=" + NV + " tris=" + (tube.t.Count / 3 - hTris) + " skipInsideH=" + skipH + " skipDoor=" + skipDoor + "\n");
        System.Func<Vector3, string> bucket = p => p.z < -90f ? "z" + Mathf.FloorToInt((p.z + 400f) / 40f) : "y" + Mathf.FloorToInt((p.y + 220f) / 45f);
        sb.Append("tube chunks=" + SplitMake(tube, "CaveTube", root.transform, mat, bucket) + "\n");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return sb.ToString();
    }

    /// <summary>장식(첨탑/종유석/석순/얼음 조각/결정+라이트) — 튜브 콜라이더에 레이캐스트. 튜브 생성 후 별도 호출(콜라이더 쿠킹 확정 뒤).</summary>
    public static string BuildDecor()
    {
        if (Application.isPlaying) return "playing";
        rng = new System.Random(41);
        var sb = new StringBuilder();
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Guide/Cavern/CaveRockMat.mat");
        var root = GameObject.Find("Cavern");
        var lg = root.transform.Find("CavernLights");
        System.Func<Vector3, string> bucket = p => p.z < -90f ? "z" + Mathf.FloorToInt((p.z + 400f) / 40f) : "y" + Mathf.FloorToInt((p.y + 220f) / 45f);
        { var del = new List<GameObject>(); foreach (Transform c in root.transform) if (c.name == "Decor" || c.name == "Crystals") del.Add(c.gameObject); foreach (var g in del) Object.DestroyImmediate(g);
          if (lg != null) { var dl = new List<GameObject>(); foreach (Transform c in lg) if (c.name.StartsWith("Crystal_")) dl.Add(c.gameObject); foreach (var g in dl) Object.DestroyImmediate(g); } }
        // 콜라이더 바운즈 재확인(빈 것은 재할당)
        int emptyCols = 0; foreach (Transform c in root.transform) if (c.name.StartsWith("CaveTube_")) { var mc = c.GetComponent<MeshCollider>(); if (mc != null && mc.bounds.extents.sqrMagnitude < 0.01f) { var mm = mc.sharedMesh; mc.sharedMesh = null; mc.sharedMesh = mm; if (mc.bounds.extents.sqrMagnitude < 0.01f) emptyCols++; } }
        sb.Append("emptyColliders=" + emptyCols + "\n");
        Physics.SyncTransforms();
        var decor = new GameObject("Decor"); decor.transform.SetParent(root.transform, false);
        RaycastHit rh;
        System.Func<Collider, bool> isTube = c => c != null && c.name.StartsWith("CaveTube_");
        var trA = GameObject.Find("Track_A").GetComponent<RailTrack>(); var trB = GameObject.Find("Track_B").GetComponent<RailTrack>();
        var railPts = new List<Vector3>(); for (int i = 0; i <= 150; i++) { railPts.Add(trA.PosAt(i / 150f)); railPts.Add(trB.PosAt(i / 150f)); }
        System.Func<Vector3, float> railDistXZ = p => { float m = 9999f; foreach (var q in railPts) { float d = new Vector2(p.x - q.x, p.z - q.z).magnitude; if (d < m) m = d; } return m; };
        var sup = new MB(); int spires = 0;
        foreach (var tr in new[] { trA, trB }) { float step = 42f / tr.Length; for (float u = 0.12f; u < 0.98f; u += step) { Vector3 p = tr.PosAt(u); if (p.z > -125f) break; if (!Physics.Raycast(p + Vector3.down * 1.5f, Vector3.down, out rh, 300f) || !isTube(rh.collider)) continue; Vector3 baseP = rh.point + Vector3.down * 2f; Cone(sup, baseP, Vector3.up, (p.y - 0.6f) - baseP.y, Mathf.Lerp(4f, 7f, R01()), 0.9f, 7, Rock, 0.12f); spires++; } }
        SplitMake(sup, "RailSpires", decor.transform, mat, p => "all");
        var sta = new MB(); int stal = 0, stam = 0;
        for (int k = 0; k < 1000 && stal < 140; k++)
        {
            float x = RS(170f), z = Mathf.Lerp(-360f, -70f, R01()); Vector3 o = new Vector3(x, -150f, z);
            if (!Physics.Raycast(o, Vector3.up, out rh, 400f) || !isTube(rh.collider)) continue;
            if (rh.point.y < -60f && z > -120f) continue;
            float maxLen = Mathf.Min(70f, (rh.point.y - (-150f)) * 0.55f); if (railDistXZ(rh.point) < 16f) maxLen = Mathf.Min(maxLen, 10f);
            float len = Mathf.Lerp(8f, maxLen, Mathf.Pow(R01(), 1.6f)); if (len < 6f) continue;
            Cone(sta, rh.point + Vector3.up * 1.5f, Vector3.down, len, Mathf.Lerp(2.5f, 9f, R01()) * (len / 40f + 0.5f), 0.4f, 6, Rock, 0.15f); stal++;
        }
        for (int k = 0; k < 1000 && stam < 90; k++)
        {
            float x = RS(170f), z = Mathf.Lerp(-360f, -130f, R01()); Vector3 o = new Vector3(x, -120f, z);
            if (!Physics.Raycast(o, Vector3.down, out rh, 200f) || !isTube(rh.collider)) continue;
            if (Mathf.Abs(x) < 30f && z < -345f) continue;
            float len = Mathf.Lerp(6f, 55f, Mathf.Pow(R01(), 1.7f));
            Cone(sta, rh.point + Vector3.down * 1.5f, Vector3.up, len, Mathf.Lerp(3f, 12f, R01()) * (len / 40f + 0.5f), 0.6f, 6, Rock, 0.15f); stam++;
        }
        SplitMake(sta, "Stalactites", decor.transform, mat, bucket);
        Physics.SyncTransforms();
        var shm = new MB(); int shards = 0;
        for (int k = 0; k < 800 && shards < 170; k++)
        {
            float z = Mathf.Lerp(-170f, -30f, Mathf.Pow(R01(), 0.7f));
            Vector3 o = z > -60f ? new Vector3(RS(6f), Mathf.Lerp(-10f, 105f, R01()), Mathf.Clamp(z, -60f, -30f)) : new Vector3(RS(10f), Mathf.Lerp(-90f, -40f, R01()) + (z > -100f ? 30f : 0f), z);
            Vector3 dir = new Vector3(RS(1f), RS(1f), RS(1f)).normalized; if (dir.sqrMagnitude < 0.1f) continue;
            if (!Physics.Raycast(o, dir, out rh, 200f) || !isTube(rh.collider)) continue;
            if (railDistXZ(rh.point) < 5f && Mathf.Abs(rh.point.y - o.y) < 8f) continue;
            float size = Mathf.Lerp(1.5f, 7f, R01()); Vector3 inward = rh.normal;
            Vector3 u1 = Vector3.Cross(inward, Vector3.up); if (u1.sqrMagnitude < 0.01f) u1 = Vector3.right; u1.Normalize(); Vector3 u2 = Vector3.Cross(inward, u1);
            float bw = size * 0.55f; Vector3 p = rh.point - inward * 0.3f;
            Vector3 b0 = p + u1 * bw + u2 * RS(bw * 0.5f), b1 = p + (u1 * -0.5f + u2 * 0.87f) * bw, b2 = p + (u1 * -0.5f - u2 * 0.87f) * bw, apex = p + inward * size + u1 * RS(size * 0.3f) + u2 * RS(size * 0.3f);
            Vector3 cen = (b0 + b1 + b2 + apex) * 0.25f;
            shm.Tri(b0, b1, apex, (b0 + b1 + apex) / 3f - cen, Ice()); shm.Tri(b1, b2, apex, (b1 + b2 + apex) / 3f - cen, Ice()); shm.Tri(b2, b0, apex, (b2 + b0 + apex) / 3f - cen, Ice());
            shards++;
        }
        SplitMake(shm, "IceShards", decor.transform, mat, p => "all");
        // 결정 군집 + 라이트
        var amber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Guide/Cavern/CavernCrystalAmber.mat"); var iceC = AssetDatabase.LoadAssetAtPath<Material>("Assets/Guide/Cavern/CavernCrystalIce.mat");
        var cry = new GameObject("Crystals"); cry.transform.SetParent(root.transform, false);
        var vA = new List<Vector3>(); var tA = new List<int>(); var vI = new List<Vector3>(); var tI = new List<int>(); int placed = 0;
        Color warm = new Color(1f, 0.78f, 0.55f), cold = new Color(0.70f, 0.85f, 1f);
        for (int k = 0; k < 500 && placed < 26; k++)
        {
            float z = Mathf.Lerp(-355f, -40f, R01());
            Vector3 o = z > -70f ? new Vector3(RS(5f), Mathf.Lerp(-20f, 100f, R01()), Mathf.Clamp(z, -60f, -30f)) : new Vector3(RS(20f), Mathf.Lerp(-120f, -60f, R01()) + (z > -110f ? 30f : 0f), z);
            Vector3 dir = new Vector3(RS(1f), RS(1f) - 0.35f, RS(1f)).normalized;
            if (!Physics.Raycast(o, dir, out rh, 260f) || !(isTube(rh.collider) || rh.collider.name.StartsWith("Stalactites"))) continue;
            bool ice = rh.point.z > -170f && R01() < Mathf.InverseLerp(-170f, -60f, rh.point.z) + 0.2f;
            float size = Mathf.Lerp(1.2f, 3.2f, R01()); var vv = ice ? vI : vA; var tt = ice ? tI : tA;
            Vector3 up = rh.normal; Vector3 u1 = Vector3.Cross(up, Vector3.forward); if (u1.sqrMagnitude < 0.01f) u1 = Vector3.right; u1.Normalize(); Vector3 u2 = Vector3.Cross(up, u1); Vector3 pp = rh.point + up * 0.2f;
            for (int q = 0; q < 3; q++)
            {
                float s = size * Mathf.Lerp(0.5f, 1f, R01()); Vector3 oo = pp + u1 * RS(size * 0.5f) + u2 * RS(size * 0.5f); Vector3 dd = (up + u1 * RS(0.5f) + u2 * RS(0.5f)).normalized;
                Vector3 a = oo + u1 * s * 0.35f, b = oo + (u1 * -0.18f + u2 * 0.3f) * s, c = oo + (u1 * -0.18f - u2 * 0.3f) * s, apex = oo + dd * s * 1.6f; Vector3 cen = (a + b + c + apex) * 0.25f;
                foreach (var tri in new[] { new[] { a, b, apex }, new[] { b, c, apex }, new[] { c, a, apex } }) { Vector3 A = tri[0], B = tri[1], C = tri[2]; bool flip = Vector3.Dot(Vector3.Cross(B - A, C - A), (A + B + C) / 3f - cen) < 0f; int i0 = vv.Count; vv.Add(A); vv.Add(flip ? C : B); vv.Add(flip ? B : C); tt.Add(i0); tt.Add(i0 + 1); tt.Add(i0 + 2); }
            }
            if (lg != null) { var L = new GameObject("Crystal_" + placed).AddComponent<Light>(); L.transform.SetParent(lg, false); L.transform.position = rh.point + rh.normal * (size * 1.2f); L.type = LightType.Point; L.range = Mathf.Lerp(45f, 80f, R01()); L.intensity = Mathf.Lerp(0.9f, 1.6f, R01()); L.color = ice ? cold : warm; }
            placed++;
        }
        System.Action<string, List<Vector3>, List<int>, Material> mk = (nm, v, t, mm) => { if (v.Count == 0) return; var m = new Mesh { name = nm }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); AssetDatabase.CreateAsset(m, "Assets/Guide/Cavern/" + nm + ".asset"); var asset = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Guide/Cavern/" + nm + ".asset"); var g = new GameObject(nm); g.transform.SetParent(cry.transform, false); g.AddComponent<MeshFilter>().sharedMesh = asset; g.AddComponent<MeshRenderer>().sharedMaterial = mm; };
        mk("CrystalsAmber", vA, tA, amber); mk("CrystalsIce", vI, tI, iceC);
        sb.Append("spires=" + spires + " stalactites=" + stal + " stalagmites=" + stam + " shards=" + shards + " crystals=" + placed + "\n");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return sb.ToString();
    }
}
