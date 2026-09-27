using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Absorb upward stepping rebound without adding horizontal drag or changing gravity.</summary>
[DefaultExecutionOrder(20)]
public sealed class TableGroundedWeight : MonoBehaviour
{
    [Tooltip("접지 직후 위로 튀는 속도의 감쇠. 수평 속도와 다리 모터 속도는 바꾸지 않는다.")]
    public float reboundDamping = 3f;
    public float maxDownAcceleration = 12f;
    ArticulationBody root;
    BoxCollider[] feet;
    Transform owner;
    TennisMode tennis;
    SquidMode squid;
    float[] legLengths;
    float mass, supportMemory;
    readonly RaycastHit[] hits = new RaycastHit[24];

    void Awake()
    {
        mass = 0f;
        owner = transform.root;
        foreach (var body in owner.GetComponentsInChildren<ArticulationBody>(true))
        {
            if (body.isRoot) root = body;
            mass += body.mass;
        }
        var legs = owner.GetComponentsInChildren<ArtLegController>(true);
        feet = new BoxCollider[legs.Length];
        legLengths = new float[legs.Length];
        for (int i = 0; i < legs.Length; i++)
        {
            feet[i] = legs[i].GetComponentInChildren<BoxCollider>(true);
            if (feet[i] != null) legLengths[i] = feet[i].transform.localScale.y;
        }
        tennis = owner.GetComponentInChildren<TennisMode>(true);
        squid = owner.GetComponentInChildren<SquidMode>(true);
    }

    void OnDisable() { supportMemory = 0f; }

    void FixedUpdate()
    {
        bool extending = false;
        for (int i = 0; i < feet.Length; i++)
        {
            if (feet[i] == null) continue;
            float length = feet[i].transform.localScale.y;
            extending |= length > legLengths[i] + .0001f;
            legLengths[i] = length;
        }
        if (root == null || root.immovable || !root.useGravity || !CheckpointRuntime.Authority
            || (squid != null && squid.active) || (tennis != null && tennis.active && tennis.StuckMask != 0)
            || root.transform.up.y < .35f)
        { supportMemory = 0f; return; }
        bool supported = HasFootSupport();
        supportMemory = supported ? .10f : Mathf.Max(0f, supportMemory - Time.fixedDeltaTime);
        if (supportMemory <= 0f) return;
        // Lengthening is an intentional lift, not unwanted rebound.
        if (extending) return;
        float rising = root.linearVelocity.y - .35f;
        if (rising <= 0f) return;
        root.AddForce(Vector3.down * (mass * Mathf.Min(maxDownAcceleration, rising * reboundDamping)), ForceMode.Force);
    }

    bool HasFootSupport()
    {
        var physics = gameObject.scene.GetPhysicsScene();
        foreach (var foot in feet)
        {
            if (foot == null || !foot.enabled || !foot.gameObject.activeInHierarchy) continue;
            Vector3 tip = foot.transform.TransformPoint(foot.center + Vector3.down * foot.size.y * .5f);
            int count = physics.Raycast(tip + Vector3.up * .10f, Vector3.down, hits, .26f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(owner) && hits[i].normal.y >= .55f) return true;
        }
        return false;
    }
}
