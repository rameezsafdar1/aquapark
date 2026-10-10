using UnityEngine;

/// <summary>
/// A small, fast up-and-down jitter of a racer's character while riding the slide, scaled by speed, so it feels fast.
/// Layered on top of the slide placement: call Remove before SlideSurface moves the model and Apply after it,
/// or Reset when the racer is not on the slide (in the air, finished).
/// </summary>
[System.Serializable]
public class RideBob
{
    [Tooltip("Up and down jitter in metres at the reference speed.")]
    public float amplitude = 0.1f;
    [Tooltip("Jitters per second.")]
    public float frequency = 11f;
    [Tooltip("Speed (m/s) at which the full amplitude is used; slower is calmer, faster a little stronger.")]
    public float referenceSpeed = 27.5f;

    private float applied;   // local units added last frame
    private float seed = -1f;

    public void Remove(Transform model)
    {
        if (applied != 0f)
        {
            Vector3 p = model.localPosition;
            p.y -= applied;
            model.localPosition = p;
            applied = 0f;
        }
    }

    public void Apply(Transform model, float speed)
    {
        if (seed < 0f)
        {
            seed = Random.value * 100f;
        }

        float t = Time.time * frequency;
        // a sine for the steady rhythm plus noise so it does not look mechanical, and racers are not in sync
        float wave = 0.6f * Mathf.Sin((t + seed) * Mathf.PI * 2f) + 0.4f * (Mathf.PerlinNoise(t * 0.8f, seed) * 2f - 1f);
        float strength = Mathf.Clamp(speed / Mathf.Max(1f, referenceSpeed), 0f, 1.5f);
        applied = wave * amplitude * strength / model.parent.lossyScale.y;
        Vector3 p = model.localPosition;
        p.y += applied;
        model.localPosition = p;
    }

    /// <summary>Forget the last jitter without moving the model (something else has already reset it).</summary>
    public void Reset()
    {
        applied = 0f;
    }
}

/// <summary>
/// Keeps a racer's character model on the slide's real surface: when the model moves sideways, its height and tilt
/// follow the tube under it (flat floor, then the curved wall) instead of a fixed straight-line formula.
/// Used by the player (Locomotion) and the AI racers (AiMover).
/// </summary>
public static class SlideSurface
{
    // Rays start this far above the spline and reach this far down, so they find the floor and the wall
    // but never the lap above in a loop (laps are at least 14 m apart).
    private const float RayStart = 3f;
    private const float RayLength = 6f;
    // How quickly the model eases to the surface (higher = snappier). Smooths out the tube mesh's flat facets.
    private const float Smoothing = 12f;

    /// <summary>
    /// Height (metres along root.up, relative to the floor under the spline) and tilt (degrees around root.forward)
    /// of the slide surface at <paramref name="lateral"/> metres to the side. False when no slide is found.
    /// </summary>
    public static bool Sample(Transform root, float lateral, LayerMask slideMask, out float rise, out float tilt)
    {
        rise = 0f;
        tilt = 0f;
        Vector3 up = root.up;

        if (!Physics.Raycast(root.position + up * RayStart, -up, out RaycastHit center, RayLength, slideMask, QueryTriggerInteraction.Ignore) ||
            !Physics.Raycast(root.position + root.right * lateral + up * RayStart, -up, out RaycastHit side, RayLength, slideMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        rise = Vector3.Dot(side.point - center.point, up);
        tilt = Vector3.SignedAngle(up, Vector3.ProjectOnPlane(side.normal, root.forward), root.forward);
        return true;
    }

    /// <summary>
    /// Eases the model's local height and Z tilt toward the surface under it. The sideways (X) position is left to the caller.
    /// Call from LateUpdate while the racer is on the slide, so it runs after the sideways tweens.
    /// <paramref name="leanAngle"/> adds a lean toward the edge, growing to full at <paramref name="leanReach"/> metres to the side,
    /// so the racer still tilts on the flat floor. The tilt used is whichever is larger, the lean or the slope, never both added.
    /// <paramref name="heightOffset"/> (metres along root.up) is added to the surface height, e.g. to keep a scaled-up ring at the waterline.
    /// </summary>
    public static void Follow(Transform root, Transform model, LayerMask slideMask, float leanAngle = 0f, float leanReach = 1f, float heightOffset = 0f)
    {
        float lateral = Vector3.Dot(model.position - root.position, root.right);
        if (!Sample(root, lateral, slideMask, out float rise, out float tilt))
        {
            return;
        }

        Apply(model, lateral, rise + heightOffset, tilt, leanAngle, leanReach);
    }

    /// <summary>
    /// For racers that float on the water instead of following the floor: the water is a flat sheet across the tube,
    /// so the model is held at a fixed <paramref name="height"/> (metres along root.up) and only its tilt follows the
    /// slide under it. One raycast per call. Lean works as in <see cref="Follow"/>.
    /// A tilted ring dips one edge, so the model is raised by <paramref name="tiltLift"/> × sin(tilt) to keep that edge
    /// at the waterline; pass the ring's half-width in metres.
    /// </summary>
    public static void Float(Transform root, Transform model, LayerMask slideMask, float height, float tiltLift, float leanAngle = 0f, float leanReach = 1f)
    {
        Vector3 up = root.up;
        float lateral = Vector3.Dot(model.position - root.position, root.right);
        float tilt = 0f;
        if (Physics.Raycast(root.position + root.right * lateral + up * RayStart, -up, out RaycastHit side, RayLength, slideMask, QueryTriggerInteraction.Ignore))
        {
            tilt = Vector3.SignedAngle(up, Vector3.ProjectOnPlane(side.normal, root.forward), root.forward);
        }

        Apply(model, lateral, height, tilt, leanAngle, leanReach, tiltLift);
    }

    // Applies the edge lean (whichever is larger, lean or slope) and eases the model's local height and Z tilt.
    private static void Apply(Transform model, float lateral, float height, float tilt, float leanAngle, float leanReach, float tiltLift = 0f)
    {
        float lean = leanAngle * Mathf.Clamp01(Mathf.Abs(lateral) / Mathf.Max(0.01f, leanReach));
        if (lean > Mathf.Abs(tilt))
        {
            tilt = Mathf.Sign(lateral) * lean;
        }

        height += tiltLift * Mathf.Sin(Mathf.Abs(tilt) * Mathf.Deg2Rad);

        float t = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);

        Vector3 pos = model.localPosition;
        pos.y = Mathf.Lerp(pos.y, height / model.parent.lossyScale.y, t);
        model.localPosition = pos;

        float z = Mathf.LerpAngle(model.localEulerAngles.z, tilt, t);
        model.localRotation = Quaternion.Euler(0f, 0f, z);
    }
}
