using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Dynamic framing for the race follow camera (sits on "Follow Cam" next to the Cinemachine components):
/// - In the air the field of view widens for a sense of speed and to show the landing area.
/// - On downhill slopes the camera rises, pulls back a little and aims further down the track, scaled by steepness.
/// - A short shake when the player lands back on the slide or bumps into an AI racer (ShakeLanding / ShakeBump).
/// Outside a race, or on flat track, everything eases back to the values set in the Inspector.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class FollowCamDirector : MonoBehaviour
{
    [Header("In the air")]
    [Tooltip("Degrees added to the field of view while the player is off the slide.")]
    public float airFovBoost = 12f;
    [Tooltip("How fast the field of view widens when leaving the slide (higher = faster).")]
    public float fovInSpeed = 2.5f;
    [Tooltip("How fast it returns after landing.")]
    public float fovOutSpeed = 4f;

    [Header("Downhill")]
    [Tooltip("Slope (degrees) where the downhill framing starts.")]
    public float slopeStart = 8f;
    [Tooltip("Slope (degrees) where it reaches full effect.")]
    public float slopeFull = 32f;
    [Tooltip("Extra camera offset at full effect (target space: y = up, z = back is negative).")]
    public Vector3 downhillOffset = new Vector3(0f, 2.5f, -1.5f);
    [Tooltip("How far ahead of the player (metres) the camera aims at full effect.")]
    public float downhillLookAhead = 6f;
    [Tooltip("How fast the camera moves into and out of the downhill framing.")]
    public float framingSpeed = 1.5f;

    [Header("Shake")]
    [Tooltip("Shake strength when the player lands back on the slide (0-1).")]
    [Range(0f, 1f)] public float landingShake = 0.7f;
    [Tooltip("Shake strength when the player bumps into, or is bumped by, an AI racer (0-1).")]
    [Range(0f, 1f)] public float bumpShake = 0.5f;
    [Tooltip("How far (metres) the aim point jitters at full shake.")]
    public float shakeAmplitude = 0.8f;
    [Tooltip("Camera roll (degrees) at full shake.")]
    public float shakeRoll = 4f;
    public float shakeFrequency = 18f;
    [Tooltip("How fast the shake dies out (per second).")]
    public float shakeDecay = 1.4f;

    private static FollowCamDirector instance;
    private float trauma;          // 0..1, decays; the shake uses trauma² so small hits stay subtle
    private float baseDutch;

    private CinemachineCamera cam;
    private CinemachineFollow follow;
    private CinemachineRotationComposer composer;
    private Locomotion player;

    private float baseFov;
    private Vector3 baseOffset;
    private Vector3 baseTargetOffset;
    private float fovBlend;       // 0..1, eased
    private float downhillBlend;  // 0..1, eased

    private void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
        follow = GetComponent<CinemachineFollow>();
        composer = GetComponent<CinemachineRotationComposer>();
        baseFov = cam.Lens.FieldOfView;
        baseDutch = cam.Lens.Dutch;
        if (follow != null) baseOffset = follow.FollowOffset;
        if (composer != null) baseTargetOffset = composer.TargetOffset;
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>Short camera shake for the player landing back on the slide.</summary>
    public static void ShakeLanding()
    {
        if (instance != null) instance.AddTrauma(instance.landingShake);
    }

    /// <summary>Short camera shake for the player bumping into, or being bumped by, an AI racer.</summary>
    public static void ShakeBump()
    {
        if (instance != null) instance.AddTrauma(instance.bumpShake);
    }

    private void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    private void Update()
    {
        if (player == null && cam.Target.TrackingTarget != null)
        {
            player = cam.Target.TrackingTarget.GetComponent<Locomotion>();
        }

        GameManager gm = GameManager.Instance;
        bool racing = player != null && gm != null && gm.gameStarted && !gm.gameOver;

        // Targets: 0 or 1 for the air boost, 0..1 by steepness for the downhill framing (only while on the slide).
        float wantFov = racing && player.IsInAir ? 1f : 0f;
        float wantDownhill = 0f;
        if (racing && !player.IsInAir)
        {
            float slope = Mathf.Asin(Mathf.Clamp(-player.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;   // + = downhill
            wantDownhill = Mathf.InverseLerp(slopeStart, slopeFull, slope);
        }

        fovBlend = Ease(fovBlend, wantFov, wantFov > fovBlend ? fovInSpeed : fovOutSpeed);
        downhillBlend = Ease(downhillBlend, wantDownhill, framingSpeed);

        // Shake: Perlin noise on the aim point (the composer has no damping, so it stays crisp) plus a little roll.
        trauma = Mathf.Max(0f, trauma - shakeDecay * Time.deltaTime);
        float s = trauma * trauma;
        float n = Time.time * shakeFrequency;
        Vector3 shake = new Vector3(Mathf.PerlinNoise(n, 0.1f) * 2f - 1f, Mathf.PerlinNoise(0.7f, n) * 2f - 1f, 0f) * (shakeAmplitude * s);
        cam.Lens.Dutch = baseDutch + (Mathf.PerlinNoise(n, 3.3f) * 2f - 1f) * shakeRoll * s;

        cam.Lens.FieldOfView = baseFov + airFovBoost * Smooth(fovBlend);
        float d = Smooth(downhillBlend);
        if (follow != null)
        {
            follow.FollowOffset = baseOffset + downhillOffset * d;
        }

        if (composer != null)
        {
            composer.TargetOffset = baseTargetOffset + Vector3.forward * (downhillLookAhead * d) + shake;
        }
    }

    private static float Ease(float current, float target, float speed)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
