using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ★2026-09-04 광차 선로(IceCavern). 경유점(자식 Transform, 씬에서 드래그 편집)을 Catmull-Rom으로 잇고
/// 호 길이로 샘플링(pts/cum). PosAt(u)는 u∈[0,1]=길이 비율 → 광차는 항상 등속.
/// 선로 시각(레일 2줄 + 침목)은 [ContextMenu 선로 다시 만들기]로 자식 MeshFilter에 생성(메시는 에셋으로 저장 권장).
/// 끊긴 선로 = 이 트랙이 그냥 끝남(옆에 다음 트랙의 시작 광차가 나란히 대기).
/// </summary>
[ExecuteAlways]
public class RailTrack : MonoBehaviour
{
    [Tooltip("경유점(순서대로). 비어 있으면 자식 중 'P'로 시작하는 오브젝트를 순서대로 사용")] public Transform[] points;
    public int samplesPerSegment = 24;
    [Header("선로 시각")]
    public float gauge = 1.4f, railSize = 0.14f, tieEvery = 1.6f;
    public Material railMat, tieMat;
    public MeshFilter railMesh, tieMesh;
    [HideInInspector] public Vector3[] pts;
    [HideInInspector] public float[] cum;

    public float Length => cum != null && cum.Length > 0 ? cum[cum.Length - 1] : 0f;

    Vector3[] Way()
    {
        var list = new List<Vector3>();
        if (points != null && points.Length >= 2) { foreach (var t in points) if (t != null) list.Add(t.position); }
        else foreach (Transform c in transform) if (c.name.StartsWith("P")) list.Add(c.position);
        return list.ToArray();
    }
    static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
    /// <summary>경유점 → 폴리라인/누적 길이 샘플링.</summary>
    public void Sample()
    {
        var w = Way();
        if (w.Length < 2) { pts = new[] { transform.position, transform.position + Vector3.forward }; cum = new[] { 0f, 1f }; return; }
        var list = new List<Vector3>();
        for (int i = 0; i < w.Length - 1; i++)
        {
            Vector3 p0 = w[Mathf.Max(0, i - 1)], p1 = w[i], p2 = w[i + 1], p3 = w[Mathf.Min(w.Length - 1, i + 2)];
            for (int k = 0; k < samplesPerSegment; k++) list.Add(CR(p0, p1, p2, p3, k / (float)samplesPerSegment));
        }
        list.Add(w[w.Length - 1]);
        pts = list.ToArray(); cum = new float[pts.Length]; cum[0] = 0f;
        for (int i = 1; i < pts.Length; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;
    }
    void Ensure() { if (pts == null || pts.Length < 2 || cum == null || cum.Length != pts.Length) Sample(); }
    public Vector3 PosAt(float u)
    {
        Ensure(); float s = Mathf.Clamp01(u) * Length;
        int lo = 0, hi = cum.Length - 1;
        while (hi - lo > 1) { int mid = (lo + hi) / 2; if (cum[mid] <= s) lo = mid; else hi = mid; }
        float seg = cum[hi] - cum[lo]; float t = seg > 1e-6f ? (s - cum[lo]) / seg : 0f;
        return Vector3.Lerp(pts[lo], pts[hi], t);
    }
    public Vector3 TangentAt(float u)
    {
        const float d = 0.002f;
        Vector3 v = PosAt(Mathf.Min(1f, u + d)) - PosAt(Mathf.Max(0f, u - d));
        return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
    }
    public float DuForDistance(float u, float ds) => Length > 1e-4f ? ds / Length : 0f;

    [ContextMenu("선로 다시 만들기")]
    public void Rebuild()
    {
        Sample();
        var rails = new List<Vector3>(); var rcol = new List<Color>(); var rtri = new List<int>();
        var ties = new List<Vector3>(); var tcol = new List<Color>(); var ttri = new List<int>();
        float acc = 0f;
        Color rc = new Color(0.30f, 0.29f, 0.30f), tc = new Color(0.36f, 0.27f, 0.18f);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 p0 = pts[i], p1 = pts[i + 1];
            Vector3 t = (p1 - p0).normalized; if (t.sqrMagnitude < 0.5f) continue;
            Vector3 side = Vector3.Cross(Vector3.up, t); if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
            Vector3 up = Vector3.Cross(t, side).normalized;
            foreach (float sgn in new[] { -1f, 1f })
                AddBoxStrip(rails, rcol, rtri, p0 + side * (sgn * gauge * 0.5f), p1 + side * (sgn * gauge * 0.5f), side, up, railSize, railSize, rc);
            acc += (p1 - p0).magnitude;
            if (acc >= tieEvery)
            {
                acc = 0f;
                Vector3 a = p0 - side * (gauge * 0.5f + 0.35f) - up * 0.1f, b = p0 + side * (gauge * 0.5f + 0.35f) - up * 0.1f;
                AddBoxStrip(ties, tcol, ttri, a, b, t, up, 0.22f, 0.08f, tc);
            }
        }
        railMesh = EnsureMF("RailMesh", railMesh, railMat);
        tieMesh = EnsureMF("TieMesh", tieMesh, tieMat);
        Apply(railMesh, rails, rcol, rtri); Apply(tieMesh, ties, tcol, ttri);
    }
    MeshFilter EnsureMF(string name, MeshFilter mf, Material mat)
    {
        // ※ Unity 오브젝트에 ?? 연산자 금지(가짜 null) — 명시적 null 검사
        if (mf == null)
        {
            var t = transform.Find(name);
            if (t == null) { var g = new GameObject(name); g.transform.SetParent(transform, false); t = g.transform; }
            mf = t.GetComponent<MeshFilter>(); if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
        }
        var mr = mf.GetComponent<MeshRenderer>(); if (mr == null) mr = mf.gameObject.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;
        return mf;
    }
    static void Apply(MeshFilter mf, List<Vector3> v, List<Color> c, List<int> t)
    {
        var m = mf.sharedMesh; if (m == null) m = new UnityEngine.Mesh { name = mf.name };
        m.Clear(); m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        mf.sharedMesh = m;
    }
    // 직선 구간 상자(4면, 끝면 없음) — 삼각형별 정점 분리, 단색. ★와인딩 검증됨(윗면 법선 +y)
    static void AddBoxStrip(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 side, Vector3 up, float w, float h, Color col)
    {
        Vector3 s = side * (w * 0.5f), u = up * h;
        Vector3[] q0 = { a - s, a + s, a + s + u, a - s + u };
        Vector3[] q1 = { b - s, b + s, b + s + u, b - s + u };
        int[][] faces = { new[] { 2, 3 }, new[] { 3, 0 }, new[] { 0, 1 }, new[] { 1, 2 } };
        foreach (var f in faces)
        {
            Vector3 p0 = q0[f[0]], p1 = q0[f[1]], p2 = q1[f[1]], p3 = q1[f[0]];
            int i = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p0); v.Add(p2); v.Add(p3);
            for (int k = 0; k < 6; k++) c.Add(col);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i + 3); t.Add(i + 4); t.Add(i + 5);
        }
    }
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Vector3 p = PosAt(0f);
        for (int i = 1; i <= 200; i++) { Vector3 q = PosAt(i / 200f); Gizmos.DrawLine(p, q); p = q; }
        var w = Way(); Gizmos.color = Color.cyan; foreach (var x in w) Gizmos.DrawWireSphere(x, 1f);
    }
#endif
}
