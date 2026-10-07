using Dreamteck.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the menu lobby: a short straight tube with water, a tiled pool deck on both sides and poolside props.
/// Prefab: Assets/Tracks/Lobby/MenuLobby.prefab. Not a playable level.
/// </summary>
public static class LobbyBuilder
{
    public const string LobbyDir = TrackGenerator.RootDir + "/Lobby";
    public const string PrefabPath = LobbyDir + "/MenuLobby.prefab";
    public static readonly Vector3 SceneOffset = new Vector3(-500f, 0f, 0f);   // far from where levels are built (they start at the origin and head +X)

    const float PointSpacing = 5f;

    [MenuItem("Aquapark/Tracks/Build Menu Lobby")]
    public static void BuildMenu() => Debug.Log(Build(50f).report);

    public struct Result { public bool ok; public GameObject prefab; public string report; }

    public static Result Build(float length)
    {
        var splineTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(TrackGenerator.TemplatesDir + "/TrackSpline.prefab");
        if (splineTemplate == null) return new Result { report = "Missing " + TrackGenerator.TemplatesDir + "/TrackSpline.prefab" };
        TrackGenerator.EnsureFolder(LobbyDir);

        // Same tube shape as the real levels.
        var d = ScriptableObject.CreateInstance<TrackDefinition>();
        var settings = new TubeProfileBuilder.Settings
        {
            floorY = TrackGenerator.FloorY,
            floorHalfWidth = d.floorHalfWidth,
            radius = d.wallRadius,
            sweepDegrees = d.wallSweepDegrees,
            thickness = d.wallThickness,
            lipExtension = 0f,
            arcSteps = 7,
            outerSteps = 4,
            capSteps = 4,
        };
        var segmentMesh = TubeProfileBuilder.CreateOrUpdateAsset(TrackGenerator.SegmentMeshPath(settings), settings);
        var tubeMaterial = d.tubeMaterial != null ? d.tubeMaterial : AssetDatabase.LoadAssetAtPath<Material>(TrackGenerator.DefaultTubeMaterial);

        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("MenuLobby");
            SceneManager.MoveGameObjectToScene(root, scene);

            // ---- Spline + water ribbon (straight, flat, heading +X).
            var splineGo = (GameObject)PrefabUtility.InstantiatePrefab(splineTemplate, scene);
            PrefabUtility.UnpackPrefabInstance(splineGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            splineGo.name = "Spline";
            splineGo.transform.SetParent(root.transform, false);
            var computer = splineGo.GetComponent<SplineComputer>();
            int count = Mathf.Max(2, Mathf.RoundToInt(length / PointSpacing) + 1);
            var pts = new SplinePoint[count];
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(i * length / (count - 1), 0f, 0f);
                pts[i] = new SplinePoint(p, p, Vector3.up, 1f, Color.white);
            }
            computer.SetPoints(pts, SplineComputer.Space.World);
            computer.type = Spline.Type.CatmullRom;
            computer.RebuildImmediate();

            // ---- Tube
            int pieces = Mathf.Max(1, Mathf.RoundToInt(length / d.tubeSegmentLength));
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

            // ---- Wooden deck along the whole tube.
            var deck = StartDeck.Settings.Default;
            deck.length = length;
            StartDeck.Add(root, computer, TrackGenerator.FloorY, d.floorHalfWidth, d.wallRadius, d.wallSweepDegrees, deck);

            // ---- Colourful water-park props on the far deck.
            LobbyProps.Add(root);

            var lobby = root.AddComponent<MenuLobby>();           

            // Generated meshes are rebuilt at load time, so they are not stored in the prefab.
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !EditorUtility.IsPersistent(mf.sharedMesh)) mf.sharedMesh = null;
            foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                if (mc.sharedMesh != null && !EditorUtility.IsPersistent(mc.sharedMesh)) mc.sharedMesh = null;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(d);
            return new Result { ok = prefab != null, prefab = prefab, report = $"Menu lobby built: {length:F0} m straight tube + deck at {PrefabPath}" };
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>Places the lobby prefab in the open scene and assigns it to the GameManager. Does not save the scene.</summary>
    public static string InstallInOpenScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return "Build the lobby first.";
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null) return "No GameManager in the open scene.";

        var existing = Object.FindFirstObjectByType<MenuLobby>(FindObjectsInactive.Include);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        instance.transform.position = SceneOffset;
        var lobby = instance.GetComponent<MenuLobby>();

        var so = new SerializedObject(gm);
        so.FindProperty("lobby").objectReferenceValue = lobby;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return $"Lobby placed at {SceneOffset} and assigned to GameManager.";
    }
}
