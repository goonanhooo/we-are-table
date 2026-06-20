using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바다 표면 메시 생성기 — **방사형 LOD 디스크**(중심 촘촘, 바깥으로 갈수록 링이 기하급수로 듬성).
/// - 균일 그리드(예전 3000²·9만 정점)는 멀리 있는 물도 같은 밀도라 낭비였음 → 디스크는 같은 시각품질을 ~1/6 정점으로.
/// - 반경(maxRadius)을 크게 잡아도 먼 링이 듬성해서 정점이 적음 → **아주 높은 곳에서 사방을 봐도 수평선까지 물이 참.**
/// - 플레이 중엔 카메라 XZ를 따라다녀(followCamera) 어디서든 발밑이 촘촘하고 수평선까지 덮음.
///   파도는 셰이더가 '월드 XZ'로 계산하므로 메시가 움직여도 파도는 월드에 고정(스위밍 없음).
/// - 셰이더(Custom/OceanWater): UV·메시노멀 안 씀(positionWS만), Cull Off → 토폴로지 자유.
/// 콜라이더 없음(시각 레이어). ExecuteAlways.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class OceanWater : MonoBehaviour
{
    [Tooltip("디스크 최대 반경(월드). 높은 곳에서 수평선까지 덮으려면 크게. (먼 링은 듬성해 정점 부담 적음)")]
    public float maxRadius = 8000f;
    [Tooltip("중심 첫 링 반경 = 근거리 격자 크기 기준")]
    public float centerStep = 5f;
    [Tooltip("링 반경 증가율(1에 가까울수록 촘촘·정점↑). 1.07≈근거리 10유닛 격자, 정점 ~1.5만")]
    public float ringGrowth = 1.07f;
    [Range(16, 360)] public int angularSegments = 144;
    [Tooltip("플레이 중 카메라 XZ를 따라다녀(어디서든 수평선까지 덮음). 파도는 월드 고정이라 안 밀림.")]
    public bool followCamera = true;

    Mesh mesh;
    float bSize = -1f, bStep = -1f, bGrow = -1f; int bAng = -1;

    void OnEnable() { Rebuild(); }
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

    void LateUpdate()
    {
        if (!followCamera || !Application.isPlaying) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        Vector3 p = transform.position;
        transform.position = new Vector3(c.x, p.y, c.z);   // XZ만 추적, 물높이(y) 유지
    }

    void Rebuild()
    {
        if (bSize == maxRadius && bStep == centerStep && bGrow == ringGrowth && bAng == angularSegments && mesh != null) return;

        int A = Mathf.Clamp(angularSegments, 8, 512);
        float step = Mathf.Max(0.5f, centerStep);
        float g = Mathf.Clamp(ringGrowth, 1.005f, 2f);
        float R = Mathf.Max(step * 4f, maxRadius);

        // 링 반경들(기하급수)
        var radii = new List<float>();
        for (float r = step; r < R; r *= g) radii.Add(r);
        radii.Add(R);

        var verts = new List<Vector3>(radii.Count * A + 1);
        var tris = new List<int>(radii.Count * A * 6);
        verts.Add(Vector3.zero);   // 중심(인덱스 0)

        int prevStart = -1;
        for (int k = 0; k < radii.Count; k++)
        {
            int start = verts.Count;
            float rad = radii[k];
            for (int a = 0; a < A; a++)
            {
                float ang = (a / (float)A) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad));
            }
            if (k == 0)
            {
                for (int a = 0; a < A; a++)   // 중심 팬
                {
                    int i0 = start + a, i1 = start + (a + 1) % A;
                    tris.Add(0); tris.Add(i0); tris.Add(i1);
                }
            }
            else
            {
                for (int a = 0; a < A; a++)   // 링 사이 쿼드 스트립
                {
                    int p0 = prevStart + a, p1 = prevStart + (a + 1) % A;
                    int c0 = start + a, c1 = start + (a + 1) % A;
                    tris.Add(p0); tris.Add(c0); tris.Add(c1);
                    tris.Add(p0); tris.Add(c1); tris.Add(p1);
                }
            }
            prevStart = start;
        }

        if (mesh == null) { mesh = new Mesh { name = "OceanRadialLOD" }; mesh.hideFlags = HideFlags.DontSave; }
        mesh.Clear();
        mesh.indexFormat = (verts.Count > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        // 파도가 화면 밖 컬링 안 되게 바운즈 크게(반경 전체 + 파고 여유).
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(R * 2f, 60f, R * 2f));

        GetComponent<MeshFilter>().sharedMesh = mesh;
        bSize = maxRadius; bStep = centerStep; bGrow = ringGrowth; bAng = angularSegments;
    }
}
