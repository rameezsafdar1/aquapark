using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the one-segment half-pipe cross-section mesh that a Dreamteck SplineMesh extrudes along a track spline.
/// Cross-section lies in X (right) / Y (up) of the spline sample, the segment runs along Z (-0.5 .. 0.5).
/// Shape: flat floor under the water, blending into a circular-arc wall that leans outward, capped by a rounded lip.
/// </summary>
public static class TubeProfileBuilder
{
    public const string MeshFolder = "Assets/Tracks/Meshes";
    public const string DefaultMeshPath = MeshFolder + "/TubeSegment.asset";

    [System.Serializable]
    public struct Settings
    {
        public float floorY;          // top of the floor, just below the water ribbon
        public float floorHalfWidth;  // flat part of the floor, roughly the water half-width
        public float radius;          // radius of the wall arc
        public float sweepDegrees;    // how far the arc turns up (90 = vertical wall)
        public float thickness;       // wall / floor thickness, also the diameter of the rounded lip
        public float lipExtension;    // straight wall section after the arc, along its tangent
        public int arcSteps;          // segments along the inner arc
        public int outerSteps;        // segments along the outer arc
        public int capSteps;          // segments in the rounded lip

        public static Settings Default => new Settings
        {
            floorY = -0.15f,
            floorHalfWidth = 1.9f,
            radius = 3.2f,
            sweepDegrees = 62f,
            thickness = 0.7f,
            lipExtension = 0f,
            arcSteps = 7,
            outerSteps = 4,
            capSteps = 4,
        };
    }

    [MenuItem("Aquapark/Tracks/Create Tube Segment Mesh")]
    public static void CreateDefaultAsset()
    {
        CreateOrUpdateAsset(DefaultMeshPath, Settings.Default);
    }

    public static Mesh CreateOrUpdateAsset(string path, Settings s)
    {
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        var built = Build(s);
        built.name = System.IO.Path.GetFileNameWithoutExtension(path);
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(built, path);
            return built;
        }
        existing.Clear();
        EditorUtility.CopySerialized(built, existing);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }

    /// <summary>Cross-section outline, counter-clockwise, as (x, y).</summary>
    public static List<Vector2> BuildRing(Settings s)
    {
        float t = s.thickness;
        float sweep = s.sweepDegrees * Mathf.Deg2Rad;
        var right = new List<Vector2>();

        // Underside centre, then the outer arc (inner arc pushed out by the thickness).
        right.Add(new Vector2(0f, s.floorY - t));
        for (int i = 0; i <= s.outerSteps; i++)
        {
            float phi = sweep * i / s.outerSteps;
            right.Add(ArcPoint(s, phi) + new Vector2(Mathf.Sin(phi), -Mathf.Cos(phi)) * t);
        }

        // Straight wall section after the arc, along its tangent.
        Vector2 tangent = new Vector2(Mathf.Cos(sweep), Mathf.Sin(sweep));
        Vector2 outward = new Vector2(Mathf.Sin(sweep), -Mathf.Cos(sweep));
        Vector2 inner = ArcPoint(s, sweep) + tangent * s.lipExtension;
        Vector2 outer = inner + outward * t;
        if (s.lipExtension > 0f) right.Add(outer);

        // Rounded lip: semicircle from the outer end to the inner end, bulging along the wall tangent.
        Vector2 mid = (inner + outer) * 0.5f;
        Vector2 toOuter = (outer - mid).normalized;
        for (int i = 1; i < s.capSteps; i++)
        {
            float a = Mathf.PI * i / s.capSteps;
            right.Add(mid + (toOuter * Mathf.Cos(a) + tangent * Mathf.Sin(a)) * (t * 0.5f));
        }

        // Inner wall back down to the floor.
        if (s.lipExtension > 0f) right.Add(inner);
        for (int i = s.arcSteps; i >= 0; i--)
            right.Add(ArcPoint(s, sweep * i / s.arcSteps));

        var ring = new List<Vector2>(right);
        // Floor centre, then mirror everything except the underside centre for the left half.
        ring.Add(new Vector2(0f, s.floorY));
        for (int i = right.Count - 1; i >= 1; i--)
            ring.Add(new Vector2(-right[i].x, right[i].y));
        return ring;
    }

    // Inner surface of the wall: arc starting at the end of the flat floor, tangent to it.
    static Vector2 ArcPoint(Settings s, float phi)
    {
        return new Vector2(s.floorHalfWidth + s.radius * Mathf.Sin(phi), s.floorY + s.radius * (1f - Mathf.Cos(phi)));
    }

    public static Mesh Build(Settings s)
    {
        var ring = BuildRing(s);
        RemoveDuplicates(ring);

        // Make sure the outline is counter-clockwise so outward normals are (dy, -dx).
        float area = 0f;
        for (int i = 0; i < ring.Count; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count];
            area += a.x * b.y - b.x * a.y;
        }
        if (area < 0f) ring.Reverse();

        int n = ring.Count;
        var edgeNormal = new Vector2[n];
        float[] cumulative = new float[n + 1];
        for (int i = 0; i < n; i++)
        {
            var d = ring[(i + 1) % n] - ring[i];
            cumulative[i + 1] = cumulative[i] + d.magnitude;
            edgeNormal[i] = new Vector2(d.y, -d.x).normalized;
        }
        float total = cumulative[n];

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // One pair of vertices (back/front) per ring point when the surface is smooth there, two pairs on hard corners.
        const float z0 = -0.5f, z1 = 0.5f;
        int AddPair(int pointIndex, Vector2 normal, float u)
        {
            Vector2 p = ring[pointIndex % n];
            int b = verts.Count;
            verts.Add(new Vector3(p.x, p.y, z0)); norms.Add(normal); uvs.Add(new Vector2(u, 0f));
            verts.Add(new Vector3(p.x, p.y, z1)); norms.Add(normal); uvs.Add(new Vector2(u, 1f));
            return b;
        }

        var startPair = new int[n];
        var endPair = new int[n];
        var sharedPair = new int[n];
        for (int i = 0; i < n; i++)
        {
            int prev = (i + n - 1) % n;
            bool smooth = Vector2.Angle(edgeNormal[prev], edgeNormal[i]) <= 50f;
            float u = cumulative[i] / total;
            if (smooth)
            {
                var m = (edgeNormal[prev] + edgeNormal[i]).normalized;
                sharedPair[i] = AddPair(i, m, u);
                startPair[i] = sharedPair[i];
                endPair[prev] = sharedPair[i];
            }
            else
            {
                startPair[i] = AddPair(i, edgeNormal[i], u);
                endPair[prev] = AddPair(i, edgeNormal[prev], i == 0 ? 1f : u);
            }
        }

        for (int i = 0; i < n; i++)
        {
            int a = startPair[i], b = endPair[i];
            // a0 = a, a1 = a+1, b0 = b, b1 = b+1 ; winding gives outward normal (dy, -dx).
            tris.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
        }

        var mesh = new Mesh { name = "TubeSegment" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void RemoveDuplicates(List<Vector2> ring)
    {
        for (int i = ring.Count - 1; i >= 0; i--)
        {
            int next = (i + 1) % ring.Count;
            if (next != i && (ring[i] - ring[next]).sqrMagnitude < 1e-8f) ring.RemoveAt(i);
        }
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
