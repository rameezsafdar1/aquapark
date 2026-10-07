using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates the centre line of a track from a TrackDefinition: positions, banking normals and the
/// per-point yaw / pitch needed to place fountains and validate the result. Pure maths, fully seeded.
///
/// Conventions: the track starts at the origin heading +X and finishes heading exactly +X.
/// Positive yaw turns right. Pitch is the downhill angle in degrees (positive = going down).
///
/// Loops are spliced into the track: while a loop is running, the normal wandering of the track is paused, the heading
/// follows the loop's own shape and the slope is set so the track drops by LoopGap between two passes over the same spot.
/// Every shape ends with the same heading it started with (net turn of 0 or whole circles), so when the loop ends the track
/// carries on exactly as it would have without it, and loops never change the rest of the layout.
/// Shapes (see LoopShape): round or oval spirals whose laps stack on top of each other, an S-crossover that weaves back
/// under itself, an S-spiral (spiral one way, then the other) and a figure-8. A rider who jumps off an upper part lands on a
/// lower one.
/// </summary>
public class TrackPath
{
    public struct LoopInfo
    {
        public float start, length;   // metres along the finished track
        public float direction;       // +1 right, -1 left (S shapes and figure-8 start this way, then swap)
        public LoopShape shape;
        public float radius, straight;
        public int laps;
        public float overlap;         // track distance between two passes over the same spot; sets the slope
        public float[] yawTable;      // signed heading change in degrees after u = 0..1 of the loop
    }

    public Vector3[] points;
    public Vector3[] normals;
    public float[] yawDeg;        // heading deviation from +X (unwrapped: includes loop turns)
    public float[] pitchDeg;      // downhill angle
    public float[] turnRate;      // degrees of yaw per metre
    public bool[] inLoop;
    public float spacing;
    public readonly List<LoopInfo> loops = new List<LoopInfo>();

    public int Count => points.Length;
    public float Length => (points.Length - 1) * spacing;
    public Vector3 End => points[points.Length - 1];

    // Stats for the generation report.
    public float maxTurnRate, maxPitchUsed, totalDrop, minTurnRadius, maxBank;
    public float minClearance = float.PositiveInfinity;   // smallest vertical gap where the track passes over itself
    public float loopGapUsed;
    public int gapRetries;
    public float lengthAdded;

    struct Section { public float center, half, target; }

    const float LoopRamp = 0.1f;                 // share of a loop spent easing the turn rate in and out
    const float LoopSpacing = 220f;              // minimum track between two loops (room for a steep drop or a fountain in between)
    const float EndRoom = 240f;                  // extra track kept free after the last loop so a steep drop fits before the finish
    const float SectionLoopMargin = 25f;         // steep drops / flat stretches stay this far from a loop (loops set their own slope)
    const float OverlapDistance = 12f;           // two track parts closer than this horizontally count as overlapping
    const float CrossoverSwing = 140f;           // S-crossover: how far the heading swings each way (degrees); >120 makes it cross itself
    const float SSpiralLink = 40f;               // S-spiral: straight between its two spirals
    const float SSpiralScale = 0.8f;             // S-spiral radius relative to the loop radius (it is 3 laps long in total)
    const float MinShapeBend = 30f;              // S-crossover / S-spiral never bend tighter than this radius (the tube pinches below ~25 m)
    const float MaxLoopsLength = 1800f;          // all loops of a level together; keeps levels to about 3.2 km (~110 s without jumps)

    public static TrackPath Generate(TrackDefinition d)
    {
        float gapScale = 1f;
        TrackPath path = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            path = Build(d, gapScale);
            path.gapRetries = attempt;
            if (path.loops.Count == 0 || path.minClearance >= d.loopMinClearance) break;
            gapScale *= 1.25f;   // laps too close together: widen the gap and try again
        }
        return path;
    }

    static TrackPath Build(TrackDefinition d, float gapScale)
    {
        var rng = new System.Random(d.seed);
        float ds = d.pointSpacing;
        float L = d.length;

        // ---- Loop layout. Make the track longer if the loops do not fit.
        var loops = PlanLoops(d);
        float loopsLength = 0f;
        foreach (var lp in loops) loopsLength += lp.length;
        float lengthBefore = L;
        L = FitLength(d, L);
        int segs = Mathf.Max(8, Mathf.CeilToInt(L / ds));
        L = segs * ds;

        if (loops.Count > 0)
        {
            // Spread the spare room randomly over the gaps before, between and after the loops.
            LoopZone(d, L, out float zl, out float zh);
            float free = Mathf.Max(0f, zh - zl - loopsLength - (loops.Count - 1) * LoopSpacing);
            var weights = new float[loops.Count + 1];
            float wSum = 0f;
            for (int i = 0; i < weights.Length; i++) { weights[i] = Mathf.Lerp(0.2f, 1f, (float)rng.NextDouble()); wSum += weights[i]; }
            float cursor = zl;
            for (int i = 0; i < loops.Count; i++)
            {
                cursor += free * weights[i] / wSum + (i > 0 ? LoopSpacing : 0f);
                var lp = loops[i];
                lp.start = cursor;
                loops[i] = lp;
                cursor += lp.length;
            }
        }

        // Track length that is not inside a loop: the normal wandering layout lives in this "base" space.
        float Skipped(float s) { float k = 0f; foreach (var lp in loops) k += Mathf.Clamp(s - lp.start, 0f, lp.length); return k; }
        float ToBase(float s) => s - Skipped(s);
        float Lb = L - loopsLength;

        float a = Mathf.Min(d.startStraight, Lb * 0.15f);
        float b = Mathf.Max(a + 150f, Lb - d.finishStraight);
        float[] yawKeys = BuildYawKeys(d, rng, a, b, out float h);
        var loopPoints = new List<float>();   // where each loop sits in base space (a single point there)
        foreach (var lp in loops) loopPoints.Add(ToBase(lp.start));
        List<Section> sections = BuildSections(d, rng, L, Lb, b, ToBase, loopPoints);
        float[] noiseKeys = BuildNoiseKeys(d, rng, Lb);

        // Each loop's slope makes the track drop by the loop gap between two passes over the same spot.
        var loopPitch = new float[loops.Count];
        for (int i = 0; i < loops.Count; i++)
            loopPitch[i] = Mathf.Asin(Mathf.Clamp(d.loopGap * gapScale / loops[i].overlap, 0.02f, 0.5f)) * Mathf.Rad2Deg;

        float BaseYaw(float bs)
        {
            if (bs <= a || bs >= b) return 0f;
            float t = (bs - a) / h;
            int k = Mathf.Min((int)t, yawKeys.Length - 2);
            return Mathf.Lerp(yawKeys[k], yawKeys[k + 1], Smooth(t - k));
        }

        float YawAt(float s)
        {
            float yaw = BaseYaw(ToBase(s));
            foreach (var lp in loops)
            {
                if (s <= lp.start) continue;
                yaw += LoopProfile(lp.yawTable, (s - lp.start) / lp.length);
            }
            return yaw;
        }

        float PitchAt(float s)
        {
            float bs = ToBase(s);
            float t = bs / 150f;
            int k = Mathf.Clamp((int)t, 0, noiseKeys.Length - 2);
            float noise = Mathf.Lerp(noiseKeys[k], noiseKeys[k + 1], Smooth(Mathf.Clamp01(t - k)));
            float p = d.slopeAngle + noise;

            // Gentle ramp-in at the start.
            p = Mathf.Lerp(d.slopeAngle * 0.6f, p, Smooth(Mathf.Clamp01(bs / (d.startStraight + 60f))));

            foreach (var sec in sections)
            {
                float w = Smooth(Mathf.Clamp01((sec.half - Mathf.Abs(bs - sec.center)) / (sec.half * 0.4f)));
                p = Mathf.Lerp(p, sec.target, w);
            }

            // Settle to the finish slope.
            p = Mathf.Lerp(p, d.finishPitch, Smooth(Mathf.Clamp01((bs - (b - 200f)) / 200f)));

            // Loops fix the slope so each lap drops by the loop gap.
            for (int i = 0; i < loops.Count; i++)
            {
                var lp = loops[i];
                float half = lp.length * 0.5f;
                float w = Smooth(Mathf.Clamp01((half - Mathf.Abs(s - (lp.start + half))) / (half * 0.3f)));
                p = Mathf.Lerp(p, loopPitch[i], w);
            }
            return Mathf.Clamp(p, 1f, d.maxPitch);
        }

        var path = new TrackPath
        {
            points = new Vector3[segs + 1],
            normals = new Vector3[segs + 1],
            yawDeg = new float[segs + 1],
            pitchDeg = new float[segs + 1],
            turnRate = new float[segs + 1],
            inLoop = new bool[segs + 1],
            spacing = ds,
            loopGapUsed = d.loopGap * gapScale,
            lengthAdded = L - lengthBefore,
        };
        path.loops.AddRange(loops);

        // Integrate the centre line using the heading at the middle of each step.
        path.points[0] = Vector3.zero;
        for (int i = 0; i < segs; i++)
        {
            float s = (i + 0.5f) * ds;
            path.points[i + 1] = path.points[i] + Direction(YawAt(s), PitchAt(s)) * ds;
        }

        for (int i = 0; i <= segs; i++)
        {
            float s = i * ds;
            float yaw = YawAt(s), pitch = PitchAt(s);
            float rate = (YawAt(s + ds * 0.5f) - YawAt(s - ds * 0.5f)) / ds;
            path.yawDeg[i] = yaw;
            path.pitchDeg[i] = pitch;
            path.turnRate[i] = rate;
            foreach (var lp in loops) if (s >= lp.start - 30f && s <= lp.start + lp.length + 30f) path.inLoop[i] = true;

            Vector3 fwd = Direction(yaw, pitch);
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right);
            float roll = Mathf.Clamp(rate * d.bankGain, -d.maxBank, d.maxBank);
            path.normals[i] = Quaternion.AngleAxis(roll, fwd) * up;

            path.maxTurnRate = Mathf.Max(path.maxTurnRate, Mathf.Abs(rate));
            path.maxPitchUsed = Mathf.Max(path.maxPitchUsed, pitch);
            path.maxBank = Mathf.Max(path.maxBank, Mathf.Abs(roll));
        }

        path.totalDrop = path.points[0].y - path.End.y;
        path.minTurnRadius = path.maxTurnRate > 0.0001f ? 57.2958f / path.maxTurnRate : float.PositiveInfinity;
        path.minClearance = MeasureClearance(path);
        return path;
    }

    /// <summary>
    /// True if a rider launched straight ahead from point i (fountain or edge jump) comes down on a lower part of the track,
    /// e.g. the next lap of a spiral. Uses the same air movement as Locomotion: up speed jumpUp, then the fall speeds up by
    /// gravity per second, while moving forward at glideSpeed. The landing spot is worked out for each lower part's own drop.
    /// </summary>
    public bool LandsOnLowerTrack(int i, float glideSpeed, float gravity, float jumpUp, float tolerance = 4f, float minDrop = 10f, float maxDrop = 80f)
    {
        Vector3 fwd = i < Count - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1];
        fwd.y = 0f;
        fwd.Normalize();
        int skip = Mathf.CeilToInt(60f / spacing);
        for (int j = i + skip; j < Count - 1; j++)
        {
            float drop = points[i].y - points[j].y;
            if (drop < minDrop || drop > maxDrop) continue;
            // Time to fall that far: drop = -jumpUp*t + gravity*t^2/2.
            float t = (jumpUp + Mathf.Sqrt(jumpUp * jumpUp + 2f * gravity * drop)) / gravity;
            Vector3 land = points[i] + fwd * (glideSpeed * t);
            if (DistanceXZ(land, points[j], points[j + 1]) <= tolerance) return true;
        }
        return false;
    }

    static float DistanceXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = new Vector2(b.x - a.x, b.z - a.z);
        var ap = new Vector2(p.x - a.x, p.z - a.z);
        float k = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
        return (ap - ab * k).magnitude;
    }

    /// <summary>Index-space lookup: position along the path for a 0..1 percent.</summary>
    public int IndexAtPercent(float percent) => Mathf.Clamp(Mathf.RoundToInt(percent * (Count - 1)), 0, Count - 1);

    // Smallest vertical distance between two parts of the track that are far apart along the track
    // but close together on the ground, i.e. one lap passing over another.
    static float MeasureClearance(TrackPath path)
    {
        const int sub = 4;
        var dense = new List<Vector3>();
        for (int i = 0; i < path.points.Length - 1; i++)
            for (int k = 0; k < sub; k++)
                dense.Add(Vector3.Lerp(path.points[i], path.points[i + 1], k / (float)sub));
        dense.Add(path.End);

        float denseSpacing = path.spacing / sub;
        int skip = Mathf.CeilToInt(150f / denseSpacing);
        float best = float.PositiveInfinity;
        float sq = OverlapDistance * OverlapDistance;
        for (int i = 0; i < dense.Count; i++)
            for (int j = i + skip; j < dense.Count; j++)
            {
                float dx = dense[i].x - dense[j].x, dz = dense[i].z - dense[j].z;
                if (dx * dx + dz * dz > sq) continue;
                float dy = Mathf.Abs(dense[i].y - dense[j].y);
                if (dy < best) best = dy;
            }
        return best;
    }

    static void LoopZone(TrackDefinition d, float L, out float lo, out float hi)
    {
        lo = Mathf.Max(d.startStraight + 100f, L * d.loopMinPercent);
        hi = Mathf.Min(L * d.loopMaxPercent, L - d.finishStraight - 100f - (d.steepSections > 0 ? EndRoom : 0f));
    }

    /// <summary>
    /// The shortest track length, at least the requested one, that fits every loop and still leaves enough normal
    /// track for the start and for the finish to come out heading +X.
    /// </summary>
    public static float FitLength(TrackDefinition d, float requested)
    {
        float L = requested;
        var plan = PlanLoops(d);
        if (plan.Count == 0) return L;
        float total = 0f;
        foreach (var lp in plan) total += lp.length;
        float need = total + (plan.Count - 1) * LoopSpacing;
        float minBase = d.startStraight + d.finishStraight + 150f;   // room left over for the normal layout
        for (int guard = 0; guard < 600; guard++)
        {
            LoopZone(d, L, out float zl, out float zh);
            if (zh - zl >= need && L - total >= minBase) break;
            L += 10f;
        }
        return L;
    }

    static Vector3 Direction(float yawDeg, float pitchDeg)
    {
        float y = yawDeg * Mathf.Deg2Rad, p = pitchDeg * Mathf.Deg2Rad;
        float cp = Mathf.Cos(p);
        return new Vector3(cp * Mathf.Cos(y), -Mathf.Sin(p), -cp * Mathf.Sin(y));
    }

    static float Smooth(float x) => x * x * (3f - 2f * x);

    /// <summary>
    /// Picks the shape, size and direction of every loop. Uses its own seeded random, so FitLength and Build always agree.
    /// The same shape never comes twice in a row (when another one is allowed), and a level has at most one S-crossover
    /// because it is the longest shape.
    /// </summary>
    public static List<LoopInfo> PlanLoops(TrackDefinition d)
    {
        var list = new List<LoopInfo>();
        if (d.loopCount <= 0) return list;
        var rng = new System.Random(d.seed * 7349 + 11);
        float lastDir = rng.NextDouble() < 0.5 ? -1f : 1f;
        int lastShape = -1;
        bool usedCrossover = false;
        float used = 0f;
        for (int i = 0; i < d.loopCount; i++)
        {
            var w = new[] { d.roundSpiralWeight, d.ovalSpiralWeight, d.sCrossoverWeight, d.figureEightWeight, d.sSpiralWeight };
            // Shapes that would push the level past the loop length budget are left out, while there is a choice.
            float radius = d.loopRadius * (1f + ((float)rng.NextDouble() * 2f - 1f) * d.loopSizeVariation);
            float straight = Mathf.Max(20f, d.loopStraight) * Mathf.Lerp(0.6f, 1.4f, (float)rng.NextDouble());
            float budget = (MaxLoopsLength - used) / (d.loopCount - i);   // fair share for this loop and the ones after it
            for (int k = 0; k < w.Length; k++)
                if (MakeLoop((LoopShape)k, radius, straight, Mathf.Max(1, d.loopTurns), 1f).length > budget * 1.3f) w[k] = 0f;
            if (usedCrossover) w[(int)LoopShape.SCrossover] = 0f;
            float others = 0f;
            for (int k = 0; k < w.Length; k++) if (k != lastShape) others += Mathf.Max(0f, w[k]);
            if (lastShape >= 0 && others > 0f) w[lastShape] = 0f;
            float sum = 0f;
            foreach (float x in w) sum += Mathf.Max(0f, x);
            LoopShape shape;
            if (sum <= 0f) shape = d.loopStraight > 0f && d.ovalSpiralWeight > 0f ? LoopShape.OvalSpiral : LoopShape.RoundSpiral;   // nothing fits / all weights off
            else
            {
                float pick = (float)rng.NextDouble() * sum;
                int k = 0;
                while (k < w.Length - 1 && (pick -= Mathf.Max(0f, w[k])) > 0f) k++;
                shape = (LoopShape)k;
            }
            lastShape = (int)shape;
            if (shape == LoopShape.SCrossover) usedCrossover = true;

            float dir;
            switch (d.loopDirection)
            {
                case LoopDirection.Left: dir = -1f; break;
                case LoopDirection.Right: dir = 1f; break;
                case LoopDirection.Alternate: dir = -lastDir; break;
                default: dir = rng.NextDouble() < 0.5 ? -1f : 1f; break;
            }
            lastDir = dir;

            var loop = MakeLoop(shape, radius, straight, Mathf.Max(1, d.loopTurns), dir);
            used += loop.length;
            list.Add(loop);
        }
        return list;
    }

    static LoopInfo MakeLoop(LoopShape shape, float r, float straight, int laps, float dir)
    {
        var lp = new LoopInfo { shape = shape, radius = r, direction = dir, laps = laps };
        float circle = 2f * Mathf.PI * r;
        switch (shape)
        {
            case LoopShape.RoundSpiral:
            case LoopShape.OvalSpiral:
            {
                // Laps of half circle, straight, half circle, straight (no straights when round), stacked on top of each other.
                if (shape == LoopShape.RoundSpiral) straight = 0f;
                float lap = circle + 2f * straight;
                float arc = Mathf.PI * r / lap;   // share of a lap spent on one half circle
                lp.straight = straight;
                lp.length = lap * laps / (1f - LoopRamp);
                lp.overlap = lap;
                lp.yawTable = TurnTable(lp.length, new Turn(lp.length, dir * 360f * laps, u =>
                {
                    float half = Mathf.Repeat(Mathf.Repeat(u * laps, 1f) * 2f, 1f);   // position inside one "half circle + straight"
                    return half > 2f * arc ? 0f : 1f;
                }));
                break;
            }
            case LoopShape.FigureEight:
            {
                // A full circle one way, then a full circle the other way: the two circles touch and the track crosses itself.
                float one = circle / (1f - LoopRamp);
                lp.laps = 1;
                lp.length = 2f * one;
                lp.overlap = circle;
                lp.yawTable = TurnTable(lp.length, new Turn(one, dir * 360f), new Turn(one, -dir * 360f));
                break;
            }
            case LoopShape.SSpiral:
            {
                // One and a half laps one way, a short straight, one and a half laps the other way: a big S with stacked ends.
                r = Mathf.Max(r * SSpiralScale, MinShapeBend);
                lp.radius = r;
                circle = 2f * Mathf.PI * r;
                float spiral = 1.5f * circle / (1f - LoopRamp);
                lp.laps = 1;
                lp.straight = SSpiralLink;
                lp.length = 2f * spiral + SSpiralLink;
                lp.overlap = circle;
                lp.yawTable = TurnTable(lp.length, new Turn(spiral, dir * 540f), new Turn(SSpiralLink, 0f), new Turn(spiral, -dir * 540f));
                break;
            }
            case LoopShape.SCrossover:
            {
                // The heading swings past 90 degrees each way (two full waves), so the track curls back and weaves under itself.
                // The wavelength is chosen so the tightest bend has a radius of 0.8 x the loop radius (at least MinShapeBend).
                float wave = Mathf.Max(0.8f * r, MinShapeBend) * 2f * Mathf.PI * CrossoverSwing / 57.2958f;
                lp.laps = 2;
                lp.length = 2f * wave;
                lp.overlap = wave * 0.5f;
                int n = Mathf.Max(64, Mathf.CeilToInt(lp.length));
                lp.yawTable = new float[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    float u = i / (float)n;
                    float env = u < 0.2f ? Smooth(u / 0.2f) : u > 0.8f ? Smooth((1f - u) / 0.2f) : 1f;
                    lp.yawTable[i] = dir * CrossoverSwing * Mathf.Sin(2f * Mathf.PI * 2f * u) * env;
                }
                break;
            }
        }
        return lp;
    }

    struct Turn
    {
        public float length, yaw;
        public Func<float, float> mask;   // 0..1 multiplier on the turn rate along the turn (null = 1); 0 makes a straight
        public Turn(float length, float yaw, Func<float, float> mask = null) { this.length = length; this.yaw = yaw; this.mask = mask; }
    }

    // Heading table (degrees, 1 m resolution) for turns placed end to end. Each turn spreads its yaw over its length with a
    // constant turn rate that is eased in and out at both ends, so turns blend smoothly into each other.
    static float[] TurnTable(float totalLength, params Turn[] turns)
    {
        int n = Mathf.Max(64, Mathf.CeilToInt(totalLength));
        var table = new float[n + 1];
        float segStart = 0f, yawBase = 0f;
        int i0 = 0;
        for (int t = 0; t < turns.Length; t++)
        {
            var turn = turns[t];
            int i1 = t == turns.Length - 1 ? n : Mathf.Clamp(Mathf.RoundToInt((segStart + turn.length) / totalLength * n), i0, n);
            var raw = new float[i1 - i0];
            float sum = 0f;
            for (int i = i0; i < i1; i++)
            {
                float u = Mathf.Clamp01(((i + 0.5f) / n * totalLength - segStart) / turn.length);
                float rate = u < LoopRamp ? Smooth(u / LoopRamp) : u > 1f - LoopRamp ? Smooth((1f - u) / LoopRamp) : 1f;
                if (turn.mask != null) rate *= turn.mask(u);
                raw[i - i0] = rate;
                sum += rate;
            }
            float acc = 0f;
            for (int i = i0; i < i1; i++)
            {
                if (sum > 0f) acc += raw[i - i0] / sum * turn.yaw;
                table[i + 1] = yawBase + acc;
            }
            yawBase += turn.yaw;
            segStart += turn.length;
            i0 = i1;
        }
        return table;
    }

    static float LoopProfile(float[] table, float u)
    {
        float x = Mathf.Clamp01(u) * (table.Length - 1);
        int i = Mathf.Min((int)x, table.Length - 2);
        return Mathf.Lerp(table[i], table[i + 1], x - i);
    }

    // Yaw is a smooth curve through random key angles; start and end keys are 0 so the track begins and finishes straight.
    static float[] BuildYawKeys(TrackDefinition d, System.Random rng, float a, float b, out float h)
    {
        int m = Mathf.Max(2, Mathf.RoundToInt((b - a) / d.turnChangeLength));
        h = (b - a) / m;
        float step = d.turnRate * h / 1.5f;   // smoothstep peaks at 1.5x the average rate
        var keys = new float[m + 1];
        float lastDir = rng.NextDouble() < 0.5 ? -1f : 1f;
        for (int k = 1; k < m; k++)
        {
            float limit = Mathf.Min(d.maxYawDegrees, step * (m - k));   // must still be able to come back to 0
            float dir = rng.NextDouble() < 0.65 ? -lastDir : lastDir;
            float mag = step * (0.5f + 0.5f * (float)rng.NextDouble());
            float v = keys[k - 1] + dir * mag;
            if (Mathf.Abs(v) > limit) { dir = -dir; v = keys[k - 1] + dir * mag; }
            keys[k] = Mathf.Clamp(v, -limit, limit);
            lastDir = dir;
        }
        return keys;
    }

    static float[] BuildNoiseKeys(TrackDefinition d, System.Random rng, float Lb)
    {
        int n = Mathf.CeilToInt(Lb / 150f) + 2;
        var keys = new float[n];
        for (int i = 0; i < n; i++) keys[i] = ((float)rng.NextDouble() * 2f - 1f) * d.slopeVariation;
        return keys;
    }

    // Steep drops anywhere in the middle; flat stretches preferably inside the fountain range. All in base space.
    static List<Section> BuildSections(TrackDefinition d, System.Random rng, float L, float Lb, float finishStart, Func<float, float> toBase, List<float> loopPoints)
    {
        var list = new List<Section>();
        float lo = Mathf.Max(d.startStraight + 100f, Lb * 0.08f);
        float hi = finishStart - 260f;
        if (hi <= lo) return list;

        float flatLo = lo, flatHi = hi;
        if (d.fountainCount > 0)
        {
            flatLo = Mathf.Clamp(toBase(L * d.fountainMinPercent), lo, hi);
            flatHi = Mathf.Clamp(toBase(L * d.fountainMaxPercent), flatLo, hi);
        }

        void Place(int count, float half, float target, float from, float to)
        {
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    float c = Mathf.Lerp(from, to, (float)rng.NextDouble());
                    bool clear = true;
                    foreach (var s in list)
                        if (Mathf.Abs(s.center - c) < s.half + half + 40f) { clear = false; break; }
                    // A section must not run into a loop: the loop would flatten it to its own slope.
                    foreach (float lp in loopPoints)
                        if (Mathf.Abs(lp - c) < half + SectionLoopMargin) { clear = false; break; }
                    if (!clear) continue;
                    list.Add(new Section { center = c, half = half, target = target });
                    break;
                }
            }
        }

        // Steep drops first (players love them), then flat stretches so fountains have calm ground. Fountains can also sit on
        // upper loop laps, so flats that no longer fit are not a problem.
        Place(d.steepSections, d.steepSectionLength * 0.5f, d.steepAngle, lo, hi);
        int flatsInRange = Mathf.Max(d.flatSections, Mathf.CeilToInt(d.fountainCount / 2f));
        float flatHalf = Mathf.Max(d.flatSectionLength, 140f) * 0.5f;
        Place(flatsInRange, flatHalf, 2.5f, flatLo, flatHi);
        return list;
    }
}
