using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dreamteck.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds one level prefab from a TrackDefinition:
/// spline + water ribbon, half-pipe tube, fountains, end zone (pool, finish line, end jump) and a LevelConfig.
/// Generated meshes are NOT stored in the prefab - Dreamteck rebuilds them when the level loads - so level files stay small.
/// </summary>
public static class TrackGenerator
{
    public const string RootDir = "Assets/Tracks";
    public const string TemplatesDir = RootDir + "/Templates";
    public const string LevelsDir = RootDir + "/Levels";
    public const string DefinitionsDir = RootDir + "/Definitions";
    public const string DatabasePath = RootDir + "/LevelDatabase.asset";

    public const string DefaultTubeMaterial = "Assets/3D/level0/Materials/Road.mat";
    const string DefaultFountain = "Assets/5- Prefabs/Jump Fountain.prefab";
    // Top of the tube floor relative to the spline. The landing raycast starts at the spline point, so the floor needs
    // clearance below it, otherwise sharp slope changes can lift the floor above the ray origin.
    public const float FloorY = -0.25f;
    const int MaxMeshVertices = 64000;   // Dreamteck meshes use 16-bit indices

    public struct Result
    {
        public bool ok;
        public GameObject prefab;
        public string path;
        public string report;
    }

    public static string PrefabPath(int levelNumber) => $"{LevelsDir}/Level_{levelNumber:000}.prefab";

    public static Result Generate(TrackDefinition d)
    {
        var splineTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatesDir + "/TrackSpline.prefab");
        var endTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatesDir + "/EndZone.prefab");
        if (splineTemplate == null || endTemplate == null)
            return Fail("Missing templates in " + TemplatesDir + " (TrackSpline.prefab / EndZone.prefab).");

        var fountainPrefab = d.fountainPrefab != null ? d.fountainPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFountain);
        if (d.fountainCount > 0 && fountainPrefab == null)
            return Fail("Fountain prefab not found: " + DefaultFountain);

        var tubeMaterial = d.tubeMaterial != null ? d.tubeMaterial : AssetDatabase.LoadAssetAtPath<Material>(DefaultTubeMaterial);
        var warnings = new List<string>();
        var path = TrackPath.Generate(d);
        EnsureFolder(LevelsDir);

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject($"Level_{d.levelNumber:000}");
            SceneManager.MoveGameObjectToScene(root, scene);

            // ---- Spline + water ribbon (from the template taken from the hand-built level).
            var splineGo = (GameObject)PrefabUtility.InstantiatePrefab(splineTemplate, scene);
            PrefabUtility.UnpackPrefabInstance(splineGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            splineGo.name = "Spline";
            splineGo.transform.SetParent(root.transform, false);
            var computer = splineGo.GetComponent<SplineComputer>();
            computer.SetPoints(BuildSplinePoints(path), SplineComputer.Space.World);
            computer.type = Spline.Type.CatmullRom;
            computer.RebuildImmediate();

            // ---- Tube
            var settings = new TubeProfileBuilder.Settings
            {
                floorY = FloorY,
                floorHalfWidth = d.floorHalfWidth,
                radius = d.wallRadius,
                sweepDegrees = d.wallSweepDegrees,
                thickness = d.wallThickness,
                lipExtension = 0f,
                arcSteps = 7,
                outerSteps = 4,
                capSteps = 4,
            };
            var segmentMesh = TubeProfileBuilder.CreateOrUpdateAsset(SegmentMeshPath(settings), settings);
            int pieces = Mathf.Max(1, Mathf.RoundToInt(path.Length / d.tubeSegmentLength));
            int maxPieces = MaxMeshVertices / Mathf.Max(1, segmentMesh.vertexCount);
            if (pieces > maxPieces)
            {
                warnings.Add($"Tube would need {pieces} pieces ({pieces * segmentMesh.vertexCount} vertices). Clamped to {maxPieces}; raise Tube Segment Length or shorten the track.");
                pieces = maxPieces;
            }

            var waterMesh = splineGo.GetComponent<SplineMesh>();
            waterMesh.GetChannel(0).count = pieces;

            var tubeGo = new GameObject("Slide Tube");
            tubeGo.layer = LayerMask.NameToLayer("Slide");
            tubeGo.transform.SetParent(root.transform, false);
            tubeGo.AddComponent<MeshFilter>();
            tubeGo.AddComponent<MeshRenderer>().sharedMaterial = tubeMaterial;
            var tube = tubeGo.AddComponent<SplineMesh>();
            tube.spline = computer;
            var channel = tube.GetChannel(0);
            channel.name = "Tube";
            channel.type = SplineMesh.Channel.Type.Extrude;
            channel.autoCount = false;
            channel.AddMesh(segmentMesh);
            channel.count = pieces;
            tubeGo.AddComponent<MeshCollider>();

            tube.RebuildImmediate();
            waterMesh.RebuildImmediate();
            int tubeVerts = tubeGo.GetComponent<MeshFilter>().sharedMesh != null ? tubeGo.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0;
            int waterVerts = splineGo.GetComponent<MeshFilter>().sharedMesh != null ? splineGo.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0;

            // ---- Fountains
            var fountainsRoot = new GameObject("Fountains");
            fountainsRoot.transform.SetParent(root.transform, false);
            var fountainPoints = PlaceFountains(d, path, warnings);
            foreach (int index in fountainPoints)
            {
                var f = (GameObject)PrefabUtility.InstantiatePrefab(fountainPrefab, scene);
                f.transform.SetParent(fountainsRoot.transform, false);
                f.transform.position = path.points[index];
            }

            // ---- End zone
            var endGo = (GameObject)PrefabUtility.InstantiatePrefab(endTemplate, scene);
            endGo.transform.SetParent(root.transform, false);
            endGo.transform.position = path.End;

            // ---- Config
            var cfg = root.AddComponent<LevelConfig>();
            cfg.levelNumber = d.levelNumber;
            cfg.trackLength = path.Length;
            cfg.mainSpline = computer;
            cfg.endSpline = endGo.transform.Find("End Jump").GetComponent<SplineComputer>();
            cfg.finishLine = endGo.transform.Find("Finish line");
            cfg.endCamAnchor = endGo.transform.Find("EndCamAnchor");
            cfg.playerSpeed = d.playerSpeed;
            cfg.aiSpeedMin = d.aiSpeedMin;
            cfg.aiSpeedMax = d.aiSpeedMax;
            cfg.aiStartSpacingPercent = d.aiStartSpacing / path.Length;
            cfg.startBoostDuration = d.startBoostDuration;

            // ---- Validation warnings
            if (path.minTurnRadius < 25f)
                warnings.Add($"Tightest bend has a radius of {path.minTurnRadius:F0} m - the tube may pinch. Lower Turn Rate.");
            if (path.maxPitchUsed > d.maxPitch - 0.5f)
                warnings.Add($"Slope hit the Max Pitch limit ({d.maxPitch}°).");
            if (path.loops.Count > 0 && path.minClearance < d.loopMinClearance)
                warnings.Add($"Loops only have {path.minClearance:F0} m between laps (wanted {d.loopMinClearance:F0} m) even after widening. Raise Loop Gap or Loop Radius.");
            if (path.lengthAdded > d.pointSpacing + 1f)   // up to one point spacing is just rounding
                warnings.Add($"Track made {path.lengthAdded:F0} m longer than the definition so the loops fit. Raise Length in the definition to match.");

            // ---- Strip generated meshes so they are rebuilt at load time instead of bloating the prefab.
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !EditorUtility.IsPersistent(mf.sharedMesh)) mf.sharedMesh = null;
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                if (mc.sharedMesh != null && !EditorUtility.IsPersistent(mc.sharedMesh)) mc.sharedMesh = null;

            string prefabPath = PrefabPath(d.levelNumber);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            var sb = new StringBuilder();
            sb.AppendLine($"Level {d.levelNumber:000}  seed {d.seed}");
            sb.AppendLine($"  length {path.Length:F0} m (about {(path.Length - 100f) / d.playerSpeed:F0} s for the player), drop {path.totalDrop:F0} m (avg slope {Mathf.Asin(path.totalDrop / path.Length) * Mathf.Rad2Deg:F1}°), max slope {path.maxPitchUsed:F0}°");
            sb.AppendLine($"  max turn {path.maxTurnRate:F2} °/m (min radius {path.minTurnRadius:F0} m), max bank {path.maxBank:F0}°");
            sb.AppendLine($"  tube {pieces} pieces / {tubeVerts} verts, water {waterVerts} verts");
            if (path.loops.Count > 0)
            {
                string where = string.Join(", ", path.loops.Select(l => $"{(100f * l.start / path.Length):F0}-{(100f * (l.start + l.length) / path.Length):F0}% ({(l.direction > 0 ? "right" : "left")})"));
                sb.AppendLine($"  loops {path.loops.Count}: {where}; radius {d.loopRadius:F0} m, gap {path.loopGapUsed:F0} m (measured clearance {path.minClearance:F0} m{(path.gapRetries > 0 ? $", widened {path.gapRetries}x" : "")})");
            }
            sb.AppendLine($"  fountains at {string.Join(", ", fountainPoints.Select(i => (100f * i / (path.Count - 1)).ToString("F0") + "%"))}");
            foreach (var w in warnings) sb.AppendLine("  WARNING: " + w);
            return new Result { ok = prefab != null, prefab = prefab, path = prefabPath, report = sb.ToString() };
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>Re-reads every level prefab and writes them, in level order, into the LevelDatabase asset.</summary>
    public static LevelDatabase RebuildDatabase()
    {
        EnsureFolder(RootDir);
        var configs = AssetDatabase.FindAssets("t:Prefab", new[] { LevelsDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Select(p => p != null ? p.GetComponent<LevelConfig>() : null)
            .Where(c => c != null)
            .OrderBy(c => c.levelNumber)
            .ToArray();

        var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<LevelDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }
        db.levels = configs;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        return db;
    }

    static SplinePoint[] BuildSplinePoints(TrackPath path)
    {
        var pts = new SplinePoint[path.Count];
        for (int i = 0; i < pts.Length; i++)
            pts[i] = new SplinePoint(path.points[i], path.points[i], path.normals[i], 1f, Color.white);
        return pts;
    }

    // Spread fountains across the allowed range, only on gentle, straight-ish stretches.
    static List<int> PlaceFountains(TrackDefinition d, TrackPath path, List<string> warnings)
    {
        var result = new List<int>();
        if (d.fountainCount <= 0) return result;

        var rng = new System.Random(d.seed * 7919 + 13);
        int minGap = Mathf.CeilToInt(d.fountainMinSpacing / path.spacing);

        // Candidate spots: the fountain range, minus any loops. If loops eat too much of it, use most of the track instead.
        List<int> Allowed(float from, float to)
        {
            var list = new List<int>();
            for (int i = path.IndexAtPercent(from); i <= path.IndexAtPercent(to); i++)
                if (!path.inLoop[i]) list.Add(i);
            return list;
        }
        var allowed = Allowed(d.fountainMinPercent, d.fountainMaxPercent);
        if (allowed.Count < d.fountainCount * (minGap + 1))
            allowed = Allowed(0.08f, 0.92f);
        if (allowed.Count < d.fountainCount)
        {
            warnings.Add("Not enough room outside the loops for that many fountains.");
            return result;
        }

        // Spread the fountains evenly over the candidate spots.
        float cell = allowed.Count / (float)d.fountainCount;
        for (int j = 0; j < d.fountainCount; j++)
        {
            int lo = Mathf.RoundToInt(j * cell), hi = Mathf.Min(allowed.Count - 1, Mathf.RoundToInt((j + 1) * cell) - 1);
            float target = lo + (hi - lo) * (0.2f + 0.6f * (float)rng.NextDouble());
            int best = -1; float bestScore = float.MaxValue;
            int closest = -1; float closestBadness = float.MaxValue;   // fallback: least over the limits
            for (int k = lo; k <= hi; k++)
            {
                int i = allowed[k];
                if (result.Any(r => Mathf.Abs(r - i) < minGap)) continue;
                float pitchOver = Mathf.Max(0f, path.pitchDeg[i] / Mathf.Max(0.1f, d.fountainMaxPitch) - 1f);
                float turnOver = Mathf.Max(0f, Mathf.Abs(path.turnRate[i]) / Mathf.Max(0.01f, d.fountainMaxTurnRate) - 1f);
                float badness = pitchOver + turnOver;
                if (badness < closestBadness) { closestBadness = badness; closest = i; }
                float score = Mathf.Abs(k - target);
                if (badness <= 0f && score < bestScore) { bestScore = score; best = i; }
            }
            if (best < 0)
            {
                if (closest < 0) { warnings.Add($"Fountain {j + 1}: no room left in its part of the range, skipped."); continue; }
                best = closest;
                warnings.Add($"Fountain {j + 1}: no ideal spot, used the closest match ({path.pitchDeg[best]:F0}° slope, {Mathf.Abs(path.turnRate[best]):F2} °/m turn). Lower Fountain Count, add Flat Sections, or loosen the fountain limits.");
            }
            result.Add(best);
        }
        result.Sort();
        return result;
    }

    // Segments are shared between levels that use the same tube shape.
    public static string SegmentMeshPath(TubeProfileBuilder.Settings s)
    {
        string key = string.Format(CultureInfo.InvariantCulture, "f{0:0.00}_r{1:0.00}_s{2:0}_t{3:0.00}_y{4:0.00}", s.floorHalfWidth, s.radius, s.sweepDegrees, s.thickness, -s.floorY);
        return $"{TubeProfileBuilder.MeshFolder}/TubeSegment_{key}.asset";
    }

    static Result Fail(string message) => new Result { ok = false, report = message };

    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
