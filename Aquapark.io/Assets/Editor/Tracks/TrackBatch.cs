using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>A value that moves from its easy-level setting to its hard-level setting.</summary>
[System.Serializable]
public class RampRange
{
    public float easy, hard;
    public RampRange(float easy, float hard) { this.easy = easy; this.hard = hard; }
    public float At(float t) => Mathf.Lerp(easy, hard, t);
}

/// <summary>How a batch of level definitions is created: how many, and how difficulty ramps across them.</summary>
[System.Serializable]
public class TrackBatchSettings
{
    public int levelCount = 50;
    public int firstLevel = 1;
    public int baseSeed = 1000;
    public bool overwriteExisting = false;
    public AnimationCurve difficulty = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("How long a level should take, in seconds. Converted to track length from the player speed.")]
    public RampRange levelTime = new RampRange(50f, 65f);
    public RampRange turnRate = new RampRange(0.12f, 0.35f);
    public RampRange maxYaw = new RampRange(35f, 70f);
    public RampRange slopeAngle = new RampRange(10f, 15f);
    public RampRange steepSections = new RampRange(2f, 4f);
    public RampRange steepAngle = new RampRange(30f, 40f);
    [Tooltip("Length of each steep drop (m).")]
    public float steepSectionLength = 110f;
    public RampRange flatSections = new RampRange(3f, 2f);
    public RampRange fountains = new RampRange(1f, 2f);
    public RampRange aiSpeedMin = new RampRange(23.1f, 27.5f);
    public RampRange aiSpeedMax = new RampRange(29.7f, 34.1f);

    [Tooltip("Levels below this number have no loops.")]
    public int loopsFromLevel = 2;
    public RampRange loops = new RampRange(2f, 3f);
    public RampRange loopRadius = new RampRange(44f, 38f);
    [Tooltip("Laps per loop. 2+ stacks the laps on top of each other, so jumping off an upper lap lands on the one below.")]
    public RampRange loopTurns = new RampRange(2f, 2f);
    [Tooltip("Typical straight on each side of an oval spiral lap (m). Each oval varies 0.6-1.4x around this.")]
    public RampRange loopStraight = new RampRange(25f, 45f);
    [Tooltip("How often each loop shape is picked (0 = never). The same shape never comes twice in a row; at most one S-crossover per level.")]
    public float roundSpiralWeight = 1f;
    public float ovalSpiralWeight = 1f;
    public float sCrossoverWeight = 1f;
    public float figureEightWeight = 0.6f;
    public float sSpiralWeight = 0.8f;
    [Tooltip("Each loop's radius varies by up to this much (0.15 = +-15%).")]
    public float loopSizeVariation = 0.15f;
    [Tooltip("No fountain closer to the start than this (m).")]
    public float fountainMinDistance = 250f;
    [Tooltip("Vertical drop between laps in metres.")]
    public float loopGap = 30f;
}

/// <summary>The actual work behind the Level Generator window. No UI in here.</summary>
public static class TrackBatch
{
    public static string CreateDefinitions(TrackBatchSettings s)
    {
        TrackGenerator.EnsureFolder(TrackGenerator.DefinitionsDir);
        int created = 0, updated = 0, skipped = 0;
        try
        {
            for (int i = 0; i < s.levelCount; i++)
            {
                int number = s.firstLevel + i;
                string path = $"{TrackGenerator.DefinitionsDir}/Level_{number:000}.asset";
                var def = AssetDatabase.LoadAssetAtPath<TrackDefinition>(path);
                if (def != null && !s.overwriteExisting) { skipped++; continue; }

                bool isNew = def == null;
                if (isNew) def = ScriptableObject.CreateInstance<TrackDefinition>();

                float t = s.difficulty.Evaluate(s.levelCount > 1 ? i / (s.levelCount - 1f) : 0f);
                Fill(s, def, number, t);

                if (isNew) { AssetDatabase.CreateAsset(def, path); created++; }
                else { EditorUtility.SetDirty(def); updated++; }
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
        }
        return $"Definitions: {created} created, {updated} updated, {skipped} skipped (already existed).";
    }

    static void Fill(TrackBatchSettings s, TrackDefinition d, int number, float t)
    {
        int seed = s.baseSeed + number * 101;
        var rng = new System.Random(seed);
        float Jitter(float amount) => 1f + ((float)rng.NextDouble() * 2f - 1f) * amount;

        d.levelNumber = number;
        d.seed = seed;

        // Loops (from the configured level onwards, at least one).
        d.loopCount = number >= s.loopsFromLevel ? Mathf.Max(1, Mathf.RoundToInt(s.loops.At(t))) : 0;
        d.loopRadius = Mathf.Round(s.loopRadius.At(t) * Jitter(0.08f));
        // Three spirals only fit in a ~65 s level when each one is tight.
        if (d.loopCount >= 3) d.loopRadius = Mathf.Min(d.loopRadius, 35f);
        d.loopTurns = Mathf.Clamp(Mathf.RoundToInt(s.loopTurns.At(t)), 1, 3);
        d.loopStraight = Mathf.Round(s.loopStraight.At(t) * Jitter(0.2f));
        if (d.loopCount >= 3) d.loopStraight = Mathf.Min(d.loopStraight, 25f);
        d.roundSpiralWeight = s.roundSpiralWeight;
        d.ovalSpiralWeight = s.ovalSpiralWeight;
        d.sCrossoverWeight = s.sCrossoverWeight;
        d.figureEightWeight = s.figureEightWeight;
        d.sSpiralWeight = s.sSpiralWeight;
        d.loopSizeVariation = s.loopSizeVariation;
        d.loopGap = s.loopGap;
        d.loopDirection = LoopDirection.Alternate;

        // Tighter sampling and shorter tube pieces keep small loops round and smooth.
        d.pointSpacing = 12f;
        d.tubeSegmentLength = 3.5f;
        d.fountainMinSpacing = 80f;
        d.fountainMinDistance = s.fountainMinDistance;

        // Length from the wanted level time: 5 s start boost (+20 m/s, about 100 m) then the normal speed.
        float seconds = s.levelTime.At(t) * Jitter(0.04f);
        d.length = Mathf.Round((d.playerSpeed * seconds + 100f) / 10f) * 10f;
        d.turnRate = s.turnRate.At(t) * Jitter(0.15f);
        d.maxYawDegrees = Mathf.Min(85f, s.maxYaw.At(t) * Jitter(0.1f));
        d.turnChangeLength = Mathf.Round(Mathf.Lerp(300f, 180f, t) * Jitter(0.15f));
        d.slopeAngle = s.slopeAngle.At(t) * Jitter(0.1f);
        d.steepSections = Mathf.RoundToInt(s.steepSections.At(t));
        d.steepAngle = s.steepAngle.At(t) * Jitter(0.08f);
        d.steepSectionLength = s.steepSectionLength;
        d.flatSections = Mathf.RoundToInt(s.flatSections.At(t));
        d.fountainCount = Mathf.RoundToInt(s.fountains.At(t));
        d.aiSpeedMin = Mathf.Round(s.aiSpeedMin.At(t) * 2f) / 2f;
        d.aiSpeedMax = Mathf.Max(d.aiSpeedMin + 2f, Mathf.Round(s.aiSpeedMax.At(t) * 2f) / 2f);
        d.length = TrackPath.FitLength(d, d.length);   // loops that do not fit make the level longer (needs every setting above)
    }

    public static TrackDefinition[] FindDefinitions()
    {
        return AssetDatabase.FindAssets("t:TrackDefinition", new[] { TrackGenerator.DefinitionsDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<TrackDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null)
            .OrderBy(d => d.levelNumber)
            .ToArray();
    }

    /// <summary>Generates prefabs for every definition whose level number is in [from, to] and refreshes the database.</summary>
    public static string GenerateRange(int from, int to, bool showProgress)
    {
        var defs = FindDefinitions().Where(d => d.levelNumber >= from && d.levelNumber <= to).ToArray();
        if (defs.Length == 0) return "No definitions found in that range. Create them first.";

        var sb = new StringBuilder();
        int ok = 0, failed = 0;
        try
        {
            for (int i = 0; i < defs.Length; i++)
            {
                if (showProgress && EditorUtility.DisplayCancelableProgressBar("Generating levels", $"Level {defs[i].levelNumber:000}  ({i + 1}/{defs.Length})", (float)i / defs.Length))
                {
                    sb.AppendLine("Cancelled.");
                    break;
                }
                var r = TrackGenerator.Generate(defs[i]);
                if (r.ok) ok++; else failed++;
                sb.AppendLine(r.ok ? r.report : $"Level {defs[i].levelNumber:000} FAILED: {r.report}");
            }
        }
        finally
        {
            if (showProgress) EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        var db = TrackGenerator.RebuildDatabase();
        return $"Generated {ok} level(s), {failed} failed. LevelDatabase lists {db.Count}.\n{sb}";
    }
}
