using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wooden boardwalk on both sides of the tube for the first metres of a level (the start area seen in the main menu).
/// Built from plain cubes laid along the spline, so level prefabs store no generated meshes.
/// </summary>
public static class StartDeck
{
    public const string ObjectName = "Start Deck";
    const string WoodMaterialPath = "Assets/Tracks/Templates/DeckTiles.mat";          // colourful pool-deck tiles (was wooden planks)
    const string PlankTexturePath = "Assets/Tracks/Textures/DeckPoolTiles.png";

    public struct Settings
    {
        public float length;       // metres of deck along the track, starting at the start line
        public float behind;       // extra deck behind the start line
        public float width;        // width of each side deck
        public float thickness;    // height of the deck boards
        public float pieceLength;  // length of one cube along the track
        public Material material;

        public static Settings Default => new Settings { length = 25f, behind = 3f, width = 5f, thickness = 0.8f, pieceLength = 5.6f };
    }

    /// <summary>Adds (or replaces) the deck under <paramref name="root"/>. Tube shape values come from the definition / generator.</summary>
    public static GameObject Add(GameObject root, SplineComputer computer, float floorY, float floorHalfWidth, float wallRadius, float sweepDegrees, Settings s)
    {
        var old = root.transform.Find(ObjectName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        if (s.material == null) s.material = LoadOrCreateMaterial();

        // Top of the deck sits just under the tube lip; the inner edge tucks in behind the lip so there is no gap.
        float sweep = sweepDegrees * Mathf.Deg2Rad;
        float lipHeight = floorY + wallRadius * (1f - Mathf.Cos(sweep));
        float lipX = floorHalfWidth + wallRadius * Mathf.Sin(sweep);
        float top = lipHeight - 0.15f;
        float innerX = lipX + 0.3f;

        var deckRoot = new GameObject(ObjectName);
        deckRoot.transform.SetParent(root.transform, false);

        float total = s.length + s.behind;
        int pieces = Mathf.Max(1, Mathf.CeilToInt(total / s.pieceLength));
        float step = total / pieces;
        for (int i = 0; i < pieces; i++)
        {
            float t = -s.behind + step * (i + 0.5f);   // distance along the track of this piece's centre
            Vector3 pos; Quaternion rot;
            SplineSample start = computer.Evaluate(0.0);
            if (t < 0f)
            {
                rot = start.rotation;
                pos = start.position + start.forward * t;
            }
            else
            {
                double percent = computer.Travel(0.0, t);
                SplineSample at = computer.Evaluate(percent);
                rot = at.rotation;
                pos = at.position;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = side < 0 ? "Deck Left" : "Deck Right";
                Object.DestroyImmediate(board.GetComponent<Collider>());
                board.transform.SetParent(deckRoot.transform, false);
                board.transform.rotation = rot;
                board.transform.position = pos + rot * new Vector3(side * (innerX + s.width * 0.5f), top - s.thickness * 0.5f, 0f);
                board.transform.localScale = new Vector3(s.width, s.thickness, step + 0.15f);
                if (s.material != null) board.GetComponent<MeshRenderer>().sharedMaterial = s.material;
            }
        }
        return deckRoot;
    }

    public static Material LoadOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        if (mat != null) return mat;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PlankTexturePath);
        var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(shader) { name = "DeckTiles", mainTexture = tex };
        mat.SetFloat("_Glossiness", 0.15f);
        AssetDatabase.CreateAsset(mat, WoodMaterialPath);
        return mat;
    }

    [MenuItem("Aquapark/Tracks/Remove Start Decks From All Levels")]
    public static void RemoveAll() => Debug.Log(RemoveFromAllLevels());

    /// <summary>The deck now only belongs to the menu lobby, so this strips it from every level prefab.</summary>
    public static string RemoveFromAllLevels()
    {
        int removed = 0, total = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { TrackGenerator.LevelsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                total++;
                var deck = root.transform.Find(ObjectName);
                if (deck == null) continue;
                Object.DestroyImmediate(deck.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                removed++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return $"Start deck removed from {removed} of {total} level prefabs.";
    }

    /// <summary>Largest sideways distance of the spline from the straight line through its first point and heading, over the first metres.</summary>
    public static float StraightDeviation(SplineComputer computer, float metres)
    {
        SplineSample start = computer.Evaluate(0.0);
        Vector3 flatFwd = new Vector3(start.forward.x, 0f, start.forward.z).normalized;
        float worst = 0f;
        for (float t = 0f; t <= metres; t += 1f)
        {
            double p = computer.Travel(0.0, t);
            SplineSample s = computer.Evaluate(p);
            Vector3 rel = s.position - start.position;
            Vector3 flat = new Vector3(rel.x, 0f, rel.z);
            worst = Mathf.Max(worst, (flat - Vector3.Project(flat, flatFwd)).magnitude);
        }
        return worst;
    }
}
