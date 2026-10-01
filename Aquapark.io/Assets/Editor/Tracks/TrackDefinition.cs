using UnityEngine;

public enum LoopDirection { Random, Left, Right, Alternate }

/// <summary>
/// Every setting needed to generate one level. Create them in bulk from Aquapark > Level Generator,
/// tweak any of them by hand, then regenerate the prefab.
/// </summary>
[CreateAssetMenu(menuName = "Aquapark/Track Definition", fileName = "Level_001")]
public class TrackDefinition : ScriptableObject
{
    [Header("General")]
    public int levelNumber = 1;
    [Tooltip("Same seed + same settings = identical track. Change it to reroll the layout.")]
    public int seed = 1;

    [Header("Path - size")]
    [Tooltip("Total track length in metres. The player starts with a 5 s boost (about +100 m) and then runs at 25 m/s, so 850 m is about 30 s and 1225 m is about 45 s. Loops make the track longer if they do not fit.")]
    [Min(300f)] public float length = 1000f;
    [Tooltip("Distance between spline control points. Keep it small (about 12) so tight loops stay round.")]
    [Range(10f, 60f)] public float pointSpacing = 12f;
    [Tooltip("Straight, gentle run at the very start (metres).")]
    [Min(0f)] public float startStraight = 40f;
    [Tooltip("Straight run into the finish line, heading exactly along +X (metres). Needed so the end dive works.")]
    [Min(40f)] public float finishStraight = 120f;

    [Header("Path - turns")]
    [Tooltip("Strongest turning rate in degrees per metre. 0.15 = lazy bends, 0.4 = snaking. Keep under ~0.6.")]
    [Range(0f, 0.8f)] public float turnRate = 0.25f;
    [Tooltip("Metres between direction changes. Smaller = more zig-zag.")]
    [Min(60f)] public float turnChangeLength = 220f;
    [Tooltip("How far the track may swing away from straight ahead, in degrees. Keep under 85 so it never doubles back.")]
    [Range(0f, 85f)] public float maxYawDegrees = 60f;

    [Header("Path - slope")]
    [Tooltip("Typical downhill angle in degrees.")]
    [Range(3f, 40f)] public float slopeAngle = 12f;
    [Tooltip("Random wobble added to the slope (degrees).")]
    [Range(0f, 15f)] public float slopeVariation = 4f;
    [Tooltip("Number of steep drops.")]
    [Range(0, 12)] public int steepSections = 3;
    [Range(15f, 60f)] public float steepAngle = 35f;
    [Min(40f)] public float steepSectionLength = 140f;
    [Tooltip("Number of nearly flat stretches. Flat stretches are added automatically so every fountain has calm ground; this sets the minimum.")]
    [Range(0, 12)] public int flatSections = 2;
    [Min(40f)] public float flatSectionLength = 120f;
    [Tooltip("Absolute limit for the slope in degrees.")]
    [Range(20f, 60f)] public float maxPitch = 50f;
    [Tooltip("Slope of the last stretch before the finish line.")]
    [Range(3f, 40f)] public float finishPitch = 18f;

    [Header("Loops (spirals where the track passes over itself)")]
    [Tooltip("How many spiral loops. Players can jump off the edge of an upper lap and land on the lap below.")]
    [Range(0, 6)] public int loopCount = 0;
    [Tooltip("Loops are placed inside this part of the track (fraction of its length).")]
    [Range(0f, 1f)] public float loopMinPercent = 0.1f;
    [Range(0f, 1f)] public float loopMaxPercent = 0.92f;
    [Tooltip("Radius of the circle in metres. Smaller = tighter, shorter, harder. One lap is about 2 x pi x radius, so 40 m is about 280 m of track. The tube is ~11 m wide, so keep this above ~30.")]
    [Range(30f, 200f)] public float loopRadius = 45f;
    [Tooltip("1 = one full circle, 2 = a double spiral.")]
    [Range(1, 3)] public int loopTurns = 1;
    [Tooltip("Vertical drop from one lap to the next (metres). This is the height of the jump down onto the lower lap. 30 is a good balance between a visible, safe gap and a fall that is not too long.")]
    [Range(15f, 80f)] public float loopGap = 30f;
    public LoopDirection loopDirection = LoopDirection.Alternate;
    [Tooltip("The generator measures the real gap between laps. If it is smaller than this, the loop gap is widened automatically.")]
    [Range(8f, 40f)] public float loopMinClearance = 14f;

    [Header("Banking")]
    [Tooltip("Degrees of roll per degree/metre of turning. 0 = no banking.")]
    [Range(0f, 120f)] public float bankGain = 60f;
    [Range(0f, 45f)] public float maxBank = 30f;

    [Header("Tube")]
    [Tooltip("Flat floor half-width. Should be about the water half-width (2).")]
    [Range(1f, 3f)] public float floorHalfWidth = 1.9f;
    [Tooltip("Radius of the curved wall. Bigger = wider, flatter tube.")]
    [Range(1f, 6f)] public float wallRadius = 3.2f;
    [Tooltip("How far the wall curves up, in degrees (90 = vertical).")]
    [Range(30f, 90f)] public float wallSweepDegrees = 62f;
    [Range(0.2f, 1.5f)] public float wallThickness = 0.7f;
    [Tooltip("Length of one extruded tube piece in metres. Shorter = smoother bends but more triangles. Tight loops need about 3.5.")]
    [Range(2f, 12f)] public float tubeSegmentLength = 3.5f;
    [Tooltip("Leave empty to use the default slide material.")]
    public Material tubeMaterial;

    [Header("Fountains (jump pads)")]
    [Range(0, 20)] public int fountainCount = 3;
    [Range(0f, 1f)] public float fountainMinPercent = 0.2f;
    [Range(0f, 1f)] public float fountainMaxPercent = 0.7f;
    [Tooltip("Minimum distance between two fountains (metres).")]
    [Min(20f)] public float fountainMinSpacing = 80f;
    [Tooltip("Fountains are only placed where the slope is gentler than this (degrees), so the landing is safe.")]
    [Range(0f, 40f)] public float fountainMaxPitch = 14f;
    [Tooltip("Fountains are only placed where the track turns slower than this (degrees per metre).")]
    [Range(0f, 0.6f)] public float fountainMaxTurnRate = 0.2f;
    [Tooltip("Leave empty to use the default Jump Fountain prefab.")]
    public GameObject fountainPrefab;

    [Header("Race")]
    public float playerSpeed = 25f;
    public float aiSpeedMin = 23f;
    public float aiSpeedMax = 30f;
    [Tooltip("Gap between racers at the start, in metres.")]
    [Min(1f)] public float aiStartSpacing = 15f;
    [Tooltip("How long the player's start speed boost lasts (seconds).")]
    public float startBoostDuration = 5f;
}
