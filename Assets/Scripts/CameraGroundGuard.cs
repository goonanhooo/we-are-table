using UnityEngine;

/// <summary>Solid floors stay opaque and stop the final camera pose, including cutscene blends.</summary>
[DefaultExecutionOrder(1100), DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CameraGroundGuard : MonoBehaviour
{
    [Min(0.05f)] public float clearance = 0.3f;
    Camera view;
    CameraFollow follow;
    CameraOcclusionFade fade;
    HallwayStage hallway;
    Vector3 previous;
    bool hasPrevious;

    static bool GroundName(string value)
    {
        string n = value.ToLowerInvariant();
        return n.Contains("floor") || n.Contains("ground") || n.Contains("terrain")
            || n.Contains("platform") || n.Contains("ramp") || n == "trapl" || n == "trapr";
    }
    public static bool IsGround(Renderer renderer)
    {
        if (renderer == null) return false;
        if (GroundName(renderer.name) || renderer.GetComponent<IslandTerrain>() != null
            || renderer.GetComponent<IceMountain>() != null) return true;
        string n = renderer.name.ToLowerInvariant();
        if (n.Contains("ceiling") || n.Contains("roof")) return false;
        // Wide, horizontal slabs can be much thicker than the previous 0.45 m cutoff.
        Vector3 size = renderer.bounds.size;
        return renderer.GetComponent<Collider>() != null
            && Mathf.Min(size.x, size.z) >= 1.5f && size.y <= Mathf.Min(size.x, size.z) * 0.35f;
    }
    public static bool IsGround(Collider collider)
    {
        if (collider == null) return false;
        if (collider is TerrainCollider || GroundName(collider.name)
            || collider.GetComponent<IslandTerrain>() != null || collider.GetComponent<IceMountain>() != null) return true;
        return IsGround(collider.GetComponent<Renderer>());
    }
    void Awake()
    {
        view = GetComponent<Camera>(); follow = GetComponent<CameraFollow>();
        fade = GetComponent<CameraOcclusionFade>(); hallway = GetComponent<HallwayStage>();
    }
    bool GroundHit(RaycastHit hit, Transform player)
    {
        return hit.collider != null && hit.collider.transform.root != player.root
            && hit.normal.y > 0.35f && IsGround(hit.collider);
    }
    public float SafeRadius => Mathf.Max(clearance, (view != null ? view.nearClipPlane : .3f) + .05f);
    public bool TrySupportHeight(Transform player, float depth, int mask, out float y)
    {
        y = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(player.position + Vector3.up * 2f, Vector3.down,
            depth, mask, QueryTriggerInteraction.Ignore))
            if (GroundHit(hit, player)) y = Mathf.Max(y, hit.point.y);
        return !float.IsNegativeInfinity(y);
    }
    void LateUpdate()
    {
        if (view != Camera.main) { hasPrevious = false; return; }
        if (follow == null) follow = GetComponent<CameraFollow>();
        Transform player = follow != null ? follow.target : fade != null ? fade.target : null;
        if (player == null) { var table = GameObject.Find("Table"); if (table != null) player = table.transform; }
        // The scripted fall through opened trapdoors is a real transition into the sky.
        if (player == null || (hallway != null && hallway.IsFalling)) { hasPrevious = false; return; }
        Vector3 pos = transform.position;
        if (!float.IsFinite(pos.x) || !float.IsFinite(pos.y) || !float.IsFinite(pos.z)) return;
        Vector3 requested = pos;
        Vector3 focus = follow != null && follow.enabled
            ? follow.FocusPosition
            : pos + transform.forward * Mathf.Max(1f, Vector3.Dot(player.position - pos, transform.forward));
        float radius = SafeRadius;
        int mask = follow != null ? follow.collisionMask.value : ~0;
        Vector3 motion = pos - previous;
        // Catch fast downward motion and blended camera poses before they cross a floor.
        if (hasPrevious && motion.sqrMagnitude > 0.000001f && motion.sqrMagnitude < 2500f)
        {
            float distance = motion.magnitude;
            foreach (var hit in Physics.SphereCastAll(previous, radius, motion / distance, distance, mask, QueryTriggerInteraction.Ignore))
                if (GroundHit(hit, player) && hit.distance > 0f)
                    pos.y = Mathf.Max(pos.y, hit.point.y + radius);
        }
        // Also recover an initial/teleported pose already below a floor. Use actual collider
        // surfaces, not a global Y clamp, so lower rooms and holes remain usable.
        float top = Mathf.Max(pos.y + radius, player.position.y + 2f);
        var origin = new Vector3(pos.x, top, pos.z);
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, top - pos.y + radius, mask, QueryTriggerInteraction.Ignore))
            if (GroundHit(hit, player)) pos.y = Mathf.Max(pos.y, hit.point.y + radius);
        // Orbiting behind a faded wall can put the camera beyond the floor's footprint.
        // Keep it above the player's supporting floor too, rather than exposing its underside.
        float supportDepth = follow != null ? follow.distance + 4f : 18f;
        if (TrySupportHeight(player, supportDepth, mask, out float supportY))
            pos.y = Mathf.Max(pos.y, supportY + radius);
        transform.position = pos;
        if ((pos - requested).sqrMagnitude > 0.000001f && (focus - pos).sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(focus - pos, Vector3.up);
        previous = pos; hasPrevious = true;
    }
}
