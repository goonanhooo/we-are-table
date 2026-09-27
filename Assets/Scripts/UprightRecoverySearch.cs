using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Finds a supported, empty neutral pose in this table's physics scene.</summary>
public sealed class UprightRecoverySearch
{
    readonly Transform owner;
    readonly RaycastHit[] hits = new RaycastHit[128];
    readonly Collider[] overlaps = new Collider[128];
    readonly List<Transform> cages = new List<Transform>();
    readonly List<Bounds> closedAreas = new List<Bounds>();
    readonly List<Transform> closedRoots = new List<Transform>();
    readonly JungleStage jungle;
    Bounds envelope;
    Quaternion rotation;
    bool allowFloating;
    PhysicsScene PhysicsScene => owner.gameObject.scene.GetPhysicsScene();

    public UprightRecoverySearch(Transform table)
    {
        owner = table;
        foreach (var root in table.gameObject.scene.GetRootGameObjects())
        {
            if (jungle == null) jungle = root.GetComponentInChildren<JungleStage>(true);
            foreach (var button in root.GetComponentsInChildren<CageButton>(true))
                if (button.cage != null && !cages.Contains(button.cage)) cages.Add(button.cage);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "GiraffeCage" && !cages.Contains(t)) cages.Add(t);
        }
    }

    public void Begin(Bounds bounds, Quaternion facing, bool floating)
    {
        envelope = bounds; rotation = facing; allowFloating = floating;
        closedAreas.Clear(); closedRoots.Clear();
        foreach (var cage in cages)
        {
            if (cage == null || !cage.gameObject.activeInHierarchy) continue;
            bool any = false; Bounds area = default;
            foreach (var renderer in cage.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!any) { area = renderer.bounds; any = true; } else area.Encapsulate(renderer.bounds);
            }
            foreach (var collider in cage.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                if (!any) { area = collider.bounds; any = true; } else area.Encapsulate(collider.bounds);
            }
            if (any) { area.Expand(.1f); closedAreas.Add(area); closedRoots.Add(cage); }
        }
    }

    bool OtherSolid(Collider c) => c != null && c.enabled && !c.isTrigger
        && c.gameObject.activeInHierarchy && !c.transform.IsChildOf(owner);

    public bool Ground(Vector2 point, float fromY, float depth, out float height)
    {
        height = float.NegativeInfinity;
        int count = PhysicsScene.Raycast(new Vector3(point.x, fromY, point.y), Vector3.down,
            hits, depth, ~0, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++)
            if (OtherSolid(hits[i].collider) && hits[i].normal.y >= .55f)
                height = Mathf.Max(height, hits[i].point.y);
        return float.IsFinite(height);
    }

    public bool TryAt(Vector3 anchor, out Vector3 result)
        => Candidate(new Vector2(anchor.x, anchor.z), anchor.y + .05f, null, anchor.y, out result);

    public bool FindLastBank(out Vector3 result)
    {
        result = default;
        if (jungle == null || jungle.island == null) return false;
        var anchor = jungle.SafeUprightAnchor;
        float floor = jungle.island.HeightAt(anchor.x, anchor.z);
        return Find(anchor, 12f, false, Vector2.zero, out result, floor);
    }

    public bool Find(Vector3 anchor, float radius, bool requirePath, Vector2 bias, out Vector3 result, float? expectedFloor = null)
    {
        result = anchor;
        float fromY = expectedFloor.HasValue ? expectedFloor.Value + .25f : anchor.y + .05f;
        bool ground = Ground(new Vector2(anchor.x, anchor.z), fromY, 8f, out float level);
        float? reference = expectedFloor ?? (ground && (jungle == null || jungle.IsSafeUprightRecovery(anchor, 0f)) ? level : (float?)null);
        float heading = bias.sqrMagnitude > .01f ? Mathf.Atan2(bias.y, bias.x) : 0f;
        for (float distance = 0f; distance <= radius + .001f; distance += distance < 3f ? .3f : .65f)
        {
            int samples = distance == 0f ? 1 : Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * distance / .55f), 16, 128);
            for (int i = 0; i < samples; i++)
            {
                float angle = heading + i * 2f * Mathf.PI / samples;
                Vector2 point = new Vector2(anchor.x, anchor.z) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (!Candidate(point, fromY, reference, anchor.y, out var candidate, expectedFloor.HasValue ? .2f : .8f)) continue;
                if (requirePath && !ClearPath(anchor, candidate)) continue;
                result = candidate; return true;
            }
        }
        return false;
    }

    bool Candidate(Vector2 point, float fromY, float? reference, float floatingY, out Vector3 result, float floorTolerance = .8f)
    {
        result = default;
        bool ground = Ground(point, fromY, 8f, out float floor);
        if (!ground && !allowFloating) return false;
        if (ground)
        {
            if (reference.HasValue && Mathf.Abs(floor - reference.Value) > floorTolerance) return false;
            float low = floor, high = floor;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 local = new Vector3((corner & 1) == 0 ? envelope.min.x + .06f : envelope.max.x - .06f, 0f,
                    (corner & 2) == 0 ? envelope.min.z + .06f : envelope.max.z - .06f);
                var offset = rotation * local;
                if (!Ground(point + new Vector2(offset.x, offset.z), fromY, 8f, out float footFloor)) return false;
                low = Mathf.Min(low, footFloor); high = Mathf.Max(high, footFloor);
            }
            if (high - low > .65f || (reference.HasValue && Mathf.Abs(high - reference.Value) > floorTolerance)) return false;
            floor = high;
        }
        result = new Vector3(point.x, ground ? floor - envelope.min.y + .08f : floatingY, point.y);
        float radius = new Vector2(envelope.extents.x, envelope.extents.z).magnitude;
        if (jungle != null && !jungle.IsSafeUprightRecovery(result, radius)) return false;
        Vector3 center = result + rotation * envelope.center;
        // Exclude the entire cage interior, including empty spaces between its bars.
        var world = new Bounds(center, Vector3.zero);
        for (int corner = 0; corner < 8; corner++)
        {
            var sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
            world.Encapsulate(center + rotation * Vector3.Scale(envelope.extents + Vector3.one * .025f, sign));
        }
        foreach (var area in closedAreas) if (area.Intersects(world)) return false;
        int count = PhysicsScene.OverlapBox(center, envelope.extents + Vector3.one * .025f,
            overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++) if (OtherSolid(overlaps[i])) return false;
        return true;
    }

    bool Escaping(Collider c, Vector3 origin)
    {
        for (int i = 0; i < closedAreas.Count; i++)
            if (closedAreas[i].Contains(origin) && c.transform.IsChildOf(closedRoots[i])) return true;
        // Recover from an already embedded primitive, without treating ordinary walls
        // between us and the destination as passable.
        bool volume = c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider mesh && mesh.convex);
        return volume && c.bounds.Contains(origin) && (c.ClosestPoint(origin) - origin).sqrMagnitude < .000001f;
    }

    bool ClearPath(Vector3 origin, Vector3 destination)
    {
        for (int level = 0; level < (allowFloating ? 1 : 2); level++)
        {
            float y = level == 0 ? origin.y : destination.y + envelope.min.y + .25f;
            Vector3 start = new Vector3(origin.x, y, origin.z), end = new Vector3(destination.x, y, destination.z);
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < .0001f) continue;
            // Reverse cast catches surfaces when the forward ray begins inside geometry.
            for (int direction = 0; direction < 2; direction++)
            {
                int count = PhysicsScene.Raycast(direction == 0 ? start : end,
                    direction == 0 ? delta.normalized : -delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return false;
                for (int i = 0; i < count; i++)
                    if (OtherSolid(hits[i].collider) && !Escaping(hits[i].collider, origin)) return false;
            }
        }
        return true;
    }
}
