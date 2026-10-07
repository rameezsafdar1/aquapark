using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Colourful water-park props for the menu lobby (striped umbrellas, sun loungers, stacked ring floats, a beach ball),
/// built from code-generated meshes so the lobby does not look like the reference game's beach hut.
/// Assets: Assets/Tracks/Props/Poolside (meshes, materials, prefabs). Menu: Aquapark > Tracks > Add Poolside Props To Lobby.
/// </summary>
public static class LobbyProps
{
    public const string GroupName = "Poolside Props";
    const string Dir = "Assets/Tracks/Props/Poolside";
    const string BallPrefab = "Assets/Tracks/Props/Ball_10.prefab";

    // Lobby deck (local space): top of the boards and the far deck strip (the side facing the menu camera).
    const float DeckTop = 1.3f;
    const float FarDeckInner = 5.03f, FarDeckOuter = 10.03f;

    static readonly Color Coral = Hex("FF6F61"), White = Hex("F7FAFC"), Sunny = Hex("FFD23F"), Aqua = Hex("33C3DB"),
        Teal = Hex("1E8FB3"), Pink = Hex("FF8CC6");

    [MenuItem("Aquapark/Tracks/Add Poolside Props To Lobby")]
    public static void AddToLobbyPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(LobbyBuilder.PrefabPath);
        try
        {
            Add(root);
            PrefabUtility.SaveAsPrefabAsset(root, LobbyBuilder.PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("Poolside props added to " + LobbyBuilder.PrefabPath);
    }

    /// <summary>Adds (or replaces) the "Poolside Props" group under the lobby root.</summary>
    public static void Add(GameObject lobbyRoot)
    {
        var old = lobbyRoot.transform.Find(GroupName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var parasolA = Parasol("Parasol_CoralWhite", Coral, White);
        var parasolB = Parasol("Parasol_AquaYellow", Aqua, Sunny);
        var loungerA = Lounger("SunLounger_Aqua", Aqua);
        var loungerB = Lounger("SunLounger_Coral", Coral);
        var ringCoral = Ring("RingFloat_Coral", Coral);
        var ringSunny = Ring("RingFloat_Yellow", Sunny);
        var ringPink = Ring("RingFloat_Pink", Pink);
        var ball = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefab);

        var group = new GameObject(GroupName).transform;
        group.SetParent(lobbyRoot.transform, false);
        float mid = (FarDeckInner + FarDeckOuter) * 0.5f;

        // Main corner where the old hut stood: umbrella over two loungers facing the pool, rings stacked beside them.
        Place(parasolA, group, new Vector3(21.5f, DeckTop, mid + 0.9f), 15f);
        Place(loungerA, group, new Vector3(20.2f, DeckTop, mid - 0.2f), 8f);
        Place(loungerB, group, new Vector3(22.8f, DeckTop, mid - 0.2f), -8f);
        Place(ringCoral, group, new Vector3(25.6f, DeckTop + 0.22f, mid + 0.9f), 0f);
        Place(ringSunny, group, new Vector3(25.75f, DeckTop + 0.66f, mid + 0.8f), 35f, 4f);
        Place(ringPink, group, new Vector3(25.5f, DeckTop + 1.1f, mid + 0.95f), 70f, -5f);

        // A second, smaller set further back for colour.
        var small = Place(parasolB, group, new Vector3(13.5f, DeckTop, mid + 1.2f), -20f);
        small.localScale = Vector3.one * 0.85f;
        Place(ringSunny, group, new Vector3(15.6f, DeckTop + 0.22f, mid - 0.6f), 10f);
        if (ball != null)
        {
            var b = Place(ball, group, new Vector3(18.0f, DeckTop, mid - 1.3f), 0f);
            FitHeight(b.gameObject, 0.7f);
        }
    }

    static Transform Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float tilt = 0f)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(tilt, yaw, 0f);
        return go.transform;
    }

    // Scales an object so its renderers are the given height, with its bottom resting on its current position.
    static void FitHeight(GameObject go, float height)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y < 1e-4f) return;
        go.transform.localScale *= height / b.size.y;
        b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.position += Vector3.up * (go.transform.position.y - b.min.y);
    }

    // ---------------------------------------------------------------- prefabs

    static GameObject Parasol(string name, Color a, Color b)
    {
        string path = $"{Dir}/{name}.prefab";
        var root = new GameObject(name);
        try
        {
            const float height = 3.6f, radius = 2.1f;
            Part(root, "Pole", Builtin("Cylinder"), Mat("Pole", White), new Vector3(0f, height * 0.5f, 0f), new Vector3(0.09f, height * 0.5f, 0.09f));
            var canopy = new GameObject("Canopy");
            canopy.transform.SetParent(root.transform, false);
            canopy.transform.localPosition = new Vector3(0f, height - 0.55f, 0f);
            canopy.AddComponent<MeshFilter>().sharedMesh = CanopyMesh(radius, 0.75f, 12);
            canopy.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mat(ColorName(a), a), Mat(ColorName(b), b) };
            Part(root, "Top", Builtin("Sphere"), Mat(ColorName(a), a), new Vector3(0f, height + 0.22f, 0f), Vector3.one * 0.22f);
            return Save(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static GameObject Lounger(string name, Color cushion)
    {
        string path = $"{Dir}/{name}.prefab";
        var root = new GameObject(name);
        try
        {
            var cube = Builtin("Cube");
            var frame = Mat("Frame", White);
            var pad = Mat(ColorName(cushion), cushion);
            // Long axis along local Z, head (backrest) at +Z, feet toward -Z.
            const float len = 2.0f, wid = 0.75f, seat = 0.38f;
            foreach (var x in new[] { -0.32f, 0.32f })
                foreach (var z in new[] { -0.85f, 0.85f })
                    Part(root, "Leg", cube, frame, new Vector3(x, seat * 0.5f, z), new Vector3(0.07f, seat, 0.07f));
            Part(root, "Frame", cube, frame, new Vector3(0f, seat, -0.15f), new Vector3(wid, 0.08f, len * 0.75f));
            Part(root, "Seat Pad", cube, pad, new Vector3(0f, seat + 0.08f, -0.15f), new Vector3(wid - 0.08f, 0.09f, len * 0.75f - 0.06f));
            var back = Part(root, "Backrest", cube, pad, Vector3.zero, new Vector3(wid - 0.08f, 0.09f, 0.75f));
            back.localPosition = new Vector3(0f, seat + 0.33f, 0.72f);
            back.localRotation = Quaternion.Euler(-38f, 0f, 0f);
            return Save(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static GameObject Ring(string name, Color color)
    {
        string path = $"{Dir}/{name}.prefab";
        var root = new GameObject(name);
        try
        {
            var ring = new GameObject("Ring");
            ring.transform.SetParent(root.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = TorusMesh(0.48f, 0.2f, 28, 12);
            // Two materials: the ring colour and white stripes, like an inflatable.
            ring.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mat(ColorName(color), color), Mat("Pole", White) };
            return Save(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static Transform Part(GameObject root, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static GameObject Save(GameObject root, string path)
    {
        TrackGenerator.EnsureFolder(Dir);
        return PrefabUtility.SaveAsPrefabAsset(root, path);
    }

    // ---------------------------------------------------------------- meshes

    // Umbrella canopy: a shallow cone split into alternating coloured panels (submesh 0 / 1), visible from both sides.
    static Mesh CanopyMesh(float radius, float rise, int panels)
    {
        string path = $"{Dir}/Canopy_{panels}.asset";
        var mesh = LoadOrNew(path);
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var tris = new[] { new List<int>(), new List<int>() };
        for (int i = 0; i < panels; i++)
        {
            float a0 = i * Mathf.PI * 2f / panels, a1 = (i + 1) * Mathf.PI * 2f / panels;
            var apex = new Vector3(0f, rise, 0f);
            var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
            var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
            var mid = (p0 + p1) * 0.5f * 0.93f + Vector3.up * 0.02f;   // slight scallop between ribs
            var t = tris[i % 2];
            AddTri(v, n, t, apex, p1, mid);
            AddTri(v, n, t, apex, mid, p0);
            AddTri(v, n, t, apex, mid, p1);   // underside
            AddTri(v, n, t, apex, p0, mid);
        }
        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(tris[0], 0);
        mesh.SetTriangles(tris[1], 1);
        mesh.RecalculateBounds();
        return SaveMesh(mesh, path);
    }

    static void AddTri(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = Vector3.Cross(b - a, c - a).normalized;
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        n.Add(normal); n.Add(normal); n.Add(normal);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    // Ring float lying flat; every other quarter of the tube goes to submesh 1 (white stripes).
    static Mesh TorusMesh(float major, float minor, int segs, int sides)
    {
        string path = $"{Dir}/RingFloat.asset";
        var mesh = LoadOrNew(path);
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        for (int i = 0; i <= segs; i++)
        {
            float u = i * Mathf.PI * 2f / segs;
            var center = new Vector3(Mathf.Cos(u) * major, 0f, Mathf.Sin(u) * major);
            var outward = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
            for (int j = 0; j <= sides; j++)
            {
                float w = j * Mathf.PI * 2f / sides;
                var dir = outward * Mathf.Cos(w) + Vector3.up * Mathf.Sin(w);
                v.Add(center + dir * minor);
                n.Add(dir);
            }
        }
        var colour = new List<int>();
        var stripes = new List<int>();
        int stripe = segs / 8;   // 8 alternating sections
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                var t = (i / Mathf.Max(1, stripe)) % 2 == 0 ? colour : stripes;
                t.Add(a); t.Add(a + 1); t.Add(b);
                t.Add(b); t.Add(a + 1); t.Add(b + 1);
            }
        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(colour, 0);
        mesh.SetTriangles(stripes, 1);
        mesh.RecalculateBounds();
        return SaveMesh(mesh, path);
    }

    static Mesh LoadOrNew(string path) => AssetDatabase.LoadAssetAtPath<Mesh>(path) ?? new Mesh();

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        TrackGenerator.EnsureFolder(Dir);
        if (!AssetDatabase.Contains(mesh)) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static Mesh Builtin(string name) => Resources.GetBuiltinResource<Mesh>(name + ".fbx");

    // ---------------------------------------------------------------- materials

    static Material Mat(string name, Color color)
    {
        string path = $"{Dir}/Mat_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            TrackGenerator.EnsureFolder(Dir);
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Glossiness", 0.35f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static string ColorName(Color c)
    {
        if (c == Coral) return "Coral";
        if (c == Sunny) return "Yellow";
        if (c == Aqua) return "Aqua";
        if (c == Teal) return "Teal";
        if (c == Pink) return "Pink";
        return "White";
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
