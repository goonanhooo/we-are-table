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

    [Tooltip("두께 축 이외 축에서 벽 범위 밖으로 이만큼 이상 벗어나면 숨김 대상 아님(멀리서 벽이 사라져 보이는 것 방지)")]
    public float lateralMargin = 1.0f;

    class W { public Renderer r; public int axis; public float innerCoord; public float sign; public Bounds b; }
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
            float sign = Mathf.Sign(centerA - wallA);               // 벽 → 실내 안쪽 방향(+/-)
            walls.Add(new W { r = r, axis = ax, innerCoord = wallA + sign * half, sign = sign, b = b });
        }
    }

    static float Axis(Vector3 v, int ax) => ax == 0 ? v.x : ax == 1 ? v.y : v.z;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        foreach (var w in walls)
        {
            if (w.r == null) continue;
            float ca = Axis(p, w.axis);
            bool beyond = (ca - w.innerCoord) * w.sign <= margin;   // 안쪽면 기준 벽 두께 속/바깥에 있나
            // 나머지 두 축에선 벽 범위 안에 있어야 숨김(플라토 등 멀리서 벽이 사라지는 것 방지)
            bool lateralOk = true;
            for (int ax = 0; ax < 3; ax++)
            {
                if (ax == w.axis) continue;
                float c = Axis(p, ax);
                if (c < Axis(w.b.min, ax) - lateralMargin || c > Axis(w.b.max, ax) + lateralMargin)
                { lateralOk = false; break; }
            }
            bool hide = beyond && lateralOk;
            if (w.r.enabled != !hide) w.r.enabled = !hide;
        }
    }
}
