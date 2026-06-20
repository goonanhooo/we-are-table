using UnityEngine;

/// <summary>
/// IslandTerrain 이 파 놓은 용암 강 도랑을 따라 흐르는 용암 리본 메시 + 분화구 용암 웅덩이를 만든다.
/// - 지형의 강 경로(IslandTerrain.RiverPoint)와 바닥 높이(HeightAt)를 그대로 따라가 도랑에 정확히 얹힌다.
/// - Custom/Lava 머티리얼(흐르는 이미시브 HDR). ExecuteAlways 라 에디터에서도 보임.
/// 콜라이더 없음(시각 전용). 사망 판정은 JungleStage 가 IslandTerrain.DistanceToRiver 로 처리.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class LavaRiver : MonoBehaviour
{
    public IslandTerrain island;
    [Tooltip("리본 폭(보통 강 폭보다 약간 좁게)")]
    public float width = 14f;
    [Tooltip("지형 위로 띄우는 높이. 도랑(riverDepth)을 채워 용암이 지면에 붙게 함")]
    public float lift = 4f;
    [Range(8, 200)] public int samples = 90;
    public float craterPoolRadius = 12f;
    [Tooltip("분화구 웅덩이가 크레이터 바닥에서 차오른 높이(용암 호수)")]
    public float craterPoolLift = 4f;
    [Tooltip("강이 분화구 바닥에서 상승값(lift)까지 넘쳐오르는 구간(경로 비율)")]
    public float craterBlend = 0.13f;

    Mesh mesh;

    /// <summary>용암이 바다에 닿아 끝나는 경로 비율(0..1). 옵시디언 마개 위치 산출용.</summary>
    public float SeaEndU { get; private set; } = 1f;
    public Vector2 SeaEndPoint => island != null ? island.RiverPoint(SeaEndU) : Vector2.zero;

    void OnEnable() { Rebuild(); }
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

    void Rebuild()
    {
        if (island == null)
        {
            island = FindAnyObjectByType<IslandTerrain>();
            if (island == null) return;
        }
        int segN = island.RiverSegments;
        if (segN < 2) return;

        int n = Mathf.Clamp(samples, 2, 200);
        // 리본: n 샘플 × 2(좌우). + 분화구 웅덩이(팬, 가운데+테두리 16).
        int ringN = 16;
        var verts = new Vector3[n * 2 + (ringN + 1)];
        var uvs = new Vector2[verts.Length];
        var tris = new int[(n - 1) * 6 + ringN * 3];

        // 분화구 바닥에 앉히는 높이(메시는 로컬 → transform 의 Y 상승분을 보정해서 절대 높이로 맞춘다).
        // 이렇게 하면 강 전체를 Y로 올려도 분화구 웅덩이만 크레이터 바닥에 정확히 박힌다.
        float tY = transform.position.y;
        Vector2 crater = island.RiverPoint(0f);
        float craterLocalY = island.HeightAt(crater.x, crater.y) + craterPoolLift - tY;

        // 용암이 바다에 닿기 전까지만(코스트에서 끝 → 바다로 안 뻗음). 바닥이 물 위인 마지막 경로 비율.
        float uMax = 1f;
        for (int s = 1; s <= 80; s++)
        {
            float uu = s / 80f;
            Vector2 cc = island.RiverPoint(uu);
            if (island.HeightAt(cc.x, cc.y) <= island.waterLevel + 0.4f) { uMax = uu; break; }
        }
        uMax = Mathf.Clamp(uMax, 0.1f, 1f);
        SeaEndU = uMax;

        for (int i = 0; i < n; i++)
        {
            float u = (float)i / (n - 1) * uMax;
            Vector2 c = island.RiverPoint(u);
            Vector2 cN = island.RiverPoint(Mathf.Clamp01(u + 0.01f));
            Vector2 dir = (cN - c);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
            dir.Normalize();
            Vector2 nrm = new Vector2(-dir.y, dir.x); // 좌우 법선
            // 분화구(u=0)에선 크레이터 바닥에 앉고, craterBlend 구간에 걸쳐 상승(lift) 강 높이로 넘쳐오름
            float normalLocalY = island.HeightAt(c.x, c.y) + lift;
            float blend = craterBlend > 1e-4f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / craterBlend)) : 1f;
            float bedY = Mathf.Lerp(craterLocalY, normalLocalY, blend);
            Vector2 l = c + nrm * (width * 0.5f);
            Vector2 r = c - nrm * (width * 0.5f);
            verts[i * 2] = new Vector3(l.x, bedY, l.y);
            verts[i * 2 + 1] = new Vector3(r.x, bedY, r.y);
            uvs[i * 2] = new Vector2(0f, u * 20f);
            uvs[i * 2 + 1] = new Vector2(1f, u * 20f);
        }
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 2, b = i * 2 + 1, c2 = (i + 1) * 2, d = (i + 1) * 2 + 1;
            tris[t++] = a; tris[t++] = c2; tris[t++] = b;
            tris[t++] = b; tris[t++] = c2; tris[t++] = d;
        }

        // 분화구 용암 웅덩이(평면 팬) — 크레이터 바닥에 정확히 앉힘(transform Y 보정된 craterLocalY)
        int baseV = n * 2;
        verts[baseV] = new Vector3(crater.x, craterLocalY, crater.y);
        uvs[baseV] = new Vector2(0.5f, 0.5f);
        for (int k = 0; k < ringN; k++)
        {
            float ang = (float)k / ringN * Mathf.PI * 2f;
            verts[baseV + 1 + k] = new Vector3(crater.x + Mathf.Cos(ang) * craterPoolRadius, craterLocalY, crater.y + Mathf.Sin(ang) * craterPoolRadius);
            uvs[baseV + 1 + k] = new Vector2(0.5f + 0.5f * Mathf.Cos(ang), 0.5f + 0.5f * Mathf.Sin(ang));
        }
        for (int k = 0; k < ringN; k++)
        {
            tris[t++] = baseV;
            tris[t++] = baseV + 1 + k;
            tris[t++] = baseV + 1 + (k + 1) % ringN;
        }

        if (mesh == null) { mesh = new Mesh { name = "LavaRiverMesh" }; mesh.hideFlags = HideFlags.DontSave; }
        mesh.Clear();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}
