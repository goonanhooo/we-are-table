using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 좁은 복도용 벽 시스루(Hallway): 카메라가 벽 바깥(또는 벽 두께 속)에 있으면 그 벽의 렌더러만 끈다.
/// 콜라이더/물리는 그대로 — 보이기만 뚫림. 카메라가 안으로 돌아오면 다시 보임.
/// 축 정렬 박스 벽 전용: 각 벽의 '두께 축'(가장 얇은 축)과 안쪽 방향을 복도 전체 중심 기준으로 자동 계산.
/// </summary>
public class WallSeeThrough : MonoBehaviour
{
    [Tooltip("벽 오브젝트 이름(이름으로 씬에서 찾음)")]
    public string[] wallNames = { "Wall_L", "Wall_R", "Wall_Back", "Wall_Front", "Ceiling" };
    [Tooltip("안쪽 판정 여유(m) — 카메라가 안쪽면에서 이만큼 들어와 있어야 벽이 보임")]
    public float margin = 0.15f;

    class W { public Renderer r; public int axis; public float innerCoord; public float sign; }
    readonly List<W> walls = new List<W>();

    void Start()
    {
        var found = new List<Renderer>();
        Bounds all = new Bounds(); bool any = false;
        foreach (var nm in wallNames)
        {
            var go = GameObject.Find(nm);
            if (go == null) continue;
            var r = go.GetComponent<Renderer>();
            if (r == null) continue;
            found.Add(r);
            if (!any) { all = r.bounds; any = true; } else all.Encapsulate(r.bounds);
        }
        foreach (var r in found)
        {
            var b = r.bounds;
            int ax = 0; float min = b.size.x;                       // 두께 축 = 가장 얇은 축
            if (b.size.y < min) { ax = 1; min = b.size.y; }
            if (b.size.z < min) ax = 2;
            float centerA = ax == 0 ? all.center.x : ax == 1 ? all.center.y : all.center.z;
            float wallA = ax == 0 ? b.center.x : ax == 1 ? b.center.y : b.center.z;
            float half = ax == 0 ? b.extents.x : ax == 1 ? b.extents.y : b.extents.z;
            float sign = Mathf.Sign(centerA - wallA);               // 벽 → 복도 안쪽 방향(+/-)
            walls.Add(new W { r = r, axis = ax, innerCoord = wallA + sign * half, sign = sign });
        }
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        foreach (var w in walls)
        {
            if (w.r == null) continue;
            float ca = w.axis == 0 ? p.x : w.axis == 1 ? p.y : p.z;
            bool inside = (ca - w.innerCoord) * w.sign > margin;    // 안쪽면보다 margin 이상 안으로 들어와 있나
            if (w.r.enabled != inside) w.r.enabled = inside;
        }
    }
}
