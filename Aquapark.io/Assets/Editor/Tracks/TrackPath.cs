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
/// turns a full 360 degrees per turn and the slope is set so the track drops by LoopGap per lap. When the loop ends,
/// the track carries on exactly as it would have without it, so loops never change the rest of the layout.
/// </summary>
public class TrackPath
{
    public struct LoopInfo
    {
        public float start, length;   // metres along the finished track
        public float direction;       // +1 right, -1 left
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
    const float LoopSpacing = 60f;               // minimum track between two loops
    const float OverlapDistance = 12f;           // two track parts closer than this horizontally count as overlapping
    static readonly float[] LoopTable = BuildLoopTable();

    /// <summary>Length of the stretch of track a single loop uses, for the given settings.</summary>
    public static float LoopStretch(TrackDefinition d) => 2f * Mathf.PI * d.loopRadius * d.loopTurns / (1f - LoopRamp);

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
        int loopCount = d.loopCount;
        float stretch = loopCount > 0 ? LoopStretch(d) : 0f;
        float lengthBefore = L;
        L = FitLength(d, L);
        int segs = Mathf.Max(8, Mathf.CeilToInt(L / ds));
        L = segs * ds;

        var loops = new List<LoopInfo>();
        if (loopCount > 0)
        {
            LoopZone(d, L, out float zl, out float zh);
            float slot = (zh - zl) / loopCount;
            float lastDir = rng.NextDouble() < 0.5 ? -1f : 1f;
            for (int i = 0; i < loopCount; i++)
            {
                float dir;
                switch (d.loopDirection)
                {
                    case LoopDirection.Left: dir = -1f; break;
                    case LoopDirection.Right: dir = 1f; break;
                    case LoopDirection.Alternate: dir = -lastDir; break;
                    default: dir = rng.NextDouble() < 0.5 ? -1f : 1f; break;
                }
                lastDir = dir;
                float start = zl + slot * i + Mathf.Max(0f, slot - stretch) * Mathf.Lerp(0.15f, 0.85f, (float)rng.NextDouble());
                loops.Add(new LoopInfo { start = start, length = stretch, direction = dir });
            }
        }

        // Track length that is not inside a loop: the normal wandering layout lives in this "base" space.
        float Skipped(float s) { float k = 0f; foreach (var lp in loops) k += Mathf.Clamp(s - lp.start, 0f, lp.length); return k; }
        float ToBase(float s) => s - Skipped(s);
        float Lb = L - loops.Count * stretch;

        float a = Mathf.Min(d.startStraight, Lb * 0.15f);
        float b = Mathf.Max(a + 150f, Lb - d.finishStraight);
        float[] yawKeys = BuildYawKeys(d, rng, a, b, out float h);
        List<Section> sections = BuildSections(d, rng, L, Lb, b, ToBase);
        float[] noiseKeys = BuildNoiseKeys(d, rng, Lb);

        float loopPitch = Mathf.Asin(Mathf.Clamp(d.loopGap * gapScale / (2f * Mathf.PI * d.loopRadius), 0.02f, 0.5f)) * Mathf.Rad2Deg;

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
                yaw += lp.direction * 360f * d.loopTurns * LoopProfile((s - lp.start) / lp.length);
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
            foreach (var lp in loops)
            {
                float half = lp.length * 0.5f;
                float w = Smooth(Mathf.Clamp01((half - Mathf.Abs(s - (lp.start + half))) / (half * 0.3f)));
                p = Mathf.Lerp(p, loopPitch, w);
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
        hi = Mathf.Min(L * d.loopMaxPercent, L - d.finishStraight - 100f);
    }

    /// <summary>
    /// The shortest track length, at least the requested one, that fits every loop and still leaves enough normal
    /// track for the start and for the finish to come out heading +X.
    /// </summary>
    public static float FitLength(TrackDefinition d, float requested)
    {
        float L = requested;
        if (d.loopCount <= 0) return L;
        float stretch = LoopStretch(d);
        float need = d.loopCount * stretch + (d.loopCount - 1) * LoopSpacing;
        float minBase = d.startStraight + d.finishStraight + 150f;   // room left over for the normal layout
        for (int guard = 0; guard < 400; guard++)
        {
            LoopZone(d, L, out float zl, out float zh);
            if (zh - zl >= need && L - d.loopCount * stretch >= minBase) break;
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

    // Fraction of a loop's turn completed after u (0..1) of its length: constant turn rate in the middle, eased at both ends.
    static float[] BuildLoopTable()
    {
        const int n = 256;
        var table = new float[n + 1];
        float acc = 0f;
        for (int i = 1; i <= n; i++)
        {
            float u = i / (float)n;
            float rate = u < LoopRamp ? Smooth(u / LoopRamp) : u > 1f - LoopRamp ? Smooth((1f - u) / LoopRamp) : 1f;
            acc += rate / n;
            table[i] = acc;
        }
        for (int i = 0; i <= n; i++) table[i] /= acc;
        return table;
    }

    static float LoopProfile(float u)
    {
        float x = Mathf.Clamp01(u) * (LoopTable.Length - 1);
        int i = Mathf.Min((int)x, LoopTable.Length - 2);
        return Mathf.Lerp(LoopTable[i], LoopTable[i + 1], x - i);
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
    static List<Section> BuildSections(TrackDefinition d, System.Random rng, float L, float Lb, float finishStart, Func<float, float> toBase)
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
                    if (!clear) continue;
                    list.Add(new Section { center = c, half = half, target = target });
                    break;
                }
            }
        }

        // Fountains need calm ground, so make sure the fountain range holds enough flat stretches for all of them.
        int flatsInRange = Mathf.Max(d.flatSections, Mathf.CeilToInt(d.fountainCount / 2f));
        float flatHalf = Mathf.Max(d.flatSectionLength, 140f) * 0.5f;
        Place(flatsInRange, flatHalf, 2.5f, flatLo, flatHi);
        Place(d.steepSections, d.steepSectionLength * 0.5f, d.steepAngle, lo, hi);
        return list;
    }
}
