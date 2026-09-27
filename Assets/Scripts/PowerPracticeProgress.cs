using UnityEngine;

/// <summary>One visible action at a time: fresh input, observed effect, then release.</summary>
public sealed class PowerPracticeProgress
{
    readonly float[] durations;
    bool armed, attempted, effectSeen;
    float heldTime, releaseTime;
    public int Stage { get; private set; }
    public bool Complete => Stage >= durations.Length;
    public bool Satisfied => attempted && effectSeen && heldTime >= durations[Mathf.Min(Stage,durations.Length-1)];
    public PowerPracticeProgress(params float[] requiredHoldSeconds) { durations = requiredHoldSeconds; }
    public void Suspend() { armed = attempted = effectSeen = false; heldTime = releaseTime = 0f; }
    public bool Tick(float dt, bool held, bool effect, bool releasedEffect = true)
    {
        if (Complete) return false;
        if (!armed) { if (!held) armed = true; return false; }
        if (held) { attempted = true; heldTime += Mathf.Max(0f,dt); releaseTime = 0f; }
        else if (attempted) releaseTime += Mathf.Max(0f,dt);
        if (attempted) effectSeen |= effect;
        // Wait for the actual mode/attachment to arrive, including a delayed server response.
        if (!held && Satisfied && releasedEffect) { Stage++; Suspend(); return true; }
        if (!held && releaseTime > 1f) Suspend();
        return false;
    }
}
