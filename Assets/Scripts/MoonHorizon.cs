using System.Collections.Generic;
using UnityEngine;

/// <summary>Extends the authored terrain into a curved lunar horizon without moving the playable map.</summary>
public sealed class MoonHorizon : MonoBehaviour
{
    Mesh mesh;
    public static void Ensure(GameObject terrain)
    {
        if (terrain == null || terrain.transform.Find("MoonHorizon") != null) return;
        var filter = terrain.GetComponent<MeshFilter>();
        var renderer = terrain.GetComponent<MeshRenderer>();
        if (filter == null || renderer == null || filter.sharedMesh == null) return;
        var go = new GameObject("MoonHorizon");
        go.transform.SetParent(terrain.transform, false);
        go.layer = terrain.layer;
        go.AddComponent<MoonHorizon>().Build(filter.sharedMesh, renderer.sharedMaterial);
    }
    void Build(Mesh source, Material material)
    {
        var points = source.vertices;
        var colors = source.colors;
        var normals = source.normals;
        var bounds = source.bounds;
        var edge = new List<int>();
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 p = points[i];
            if (Mathf.Abs(p.x - bounds.min.x) < .01f || Mathf.Abs(p.x - bounds.max.x) < .01f ||
                Mathf.Abs(p.z - bounds.min.z) < .01f || Mathf.Abs(p.z - bounds.max.z) < .01f) edge.Add(i);
        }
        Vector3 center = bounds.center;
        edge.Sort((a, b) => Mathf.Atan2(points[a].z - center.z, points[a].x - center.x)
            .CompareTo(Mathf.Atan2(points[b].z - center.z, points[b].x - center.x)));
        float[] rings = { 0f, 12f, 32f, 64f, 110f, 180f, 280f, 420f, 650f, 1000f, 1600f, 2500f, 4000f, 7000f, 12000f };
        int count = edge.Count;
        if (count < 3) return;
        Color surfaceColor = new Color(.53f, .53f, .55f);
        if (colors.Length == points.Length)
        {
            surfaceColor = Color.clear;
            foreach (int index in edge) surfaceColor += colors[index];
            surfaceColor /= count;
        }
        var vertices = new Vector3[count * rings.Length];
        var vertexColors = new Color[vertices.Length];
        var triangles = new int[(rings.Length - 1) * count * 6];
        for (int ring = 0; ring < rings.Length; ring++)
        {
            float distance = rings[ring];
            float blend = Mathf.SmoothStep(0f, 1f, distance / 180f);
            for (int i = 0; i < count; i++)
            {
                int index = ring * count + i;
                Vector3 original = points[edge[i]];
                Vector3 radial = new Vector3(original.x - center.x, 0f, original.z - center.z).normalized;
                Vector3 p = original + radial * distance;
                float dunes = (Mathf.PerlinNoise(p.x * .003f + 37f, p.z * .003f + 11f) - .5f) * 12f;
                // A broad planet curve makes the distant surface disappear behind the horizon.
                p.y = Mathf.Lerp(original.y, dunes, blend) - distance * distance / 32000f;
                vertices[index] = p;
                Color seam = colors.Length == points.Length ? colors[edge[i]] : surfaceColor;
                float grain = 1f + (Mathf.PerlinNoise(p.x * .025f + 5f, p.z * .025f + 17f) - .5f) * .10f;
                Color distant = surfaceColor * grain; distant.a = surfaceColor.a;
                vertexColors[index] = Color.Lerp(seam, distant, blend);
                if (ring == 0) continue;
                int previous = (ring - 1) * count + i;
                int next = (i + 1) % count;
                int t = ((ring - 1) * count + i) * 6;
                triangles[t] = previous; triangles[t + 1] = (ring - 1) * count + next; triangles[t + 2] = index;
                triangles[t + 3] = index; triangles[t + 4] = (ring - 1) * count + next; triangles[t + 5] = ring * count + next;
            }
        }
        mesh = new Mesh { name = "Moon continuous horizon" };
        mesh.vertices = vertices; mesh.colors = vertexColors; mesh.triangles = triangles;
        mesh.RecalculateNormals();
        if (normals.Length == points.Length)
        {
            var joinedNormals = mesh.normals;
            for (int i = 0; i < count; i++) joinedNormals[i] = normals[edge[i]];
            mesh.normals = joinedNormals;
        }
        mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        // The visible extension is also solid if the player walks beyond the original terrain.
        gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
