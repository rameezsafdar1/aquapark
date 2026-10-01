using System.Linq;
using Dreamteck.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Drops a generated level prefab into the open gameplay scene and re-points the player, AI, game manager and
/// end camera at it, so the level can be played straight away from the editor.
/// Earlier levels under "== Levels" are only deactivated, never deleted. Previously loaded generated levels are replaced.
/// </summary>
public static class TrackLoader
{
    public static bool LoadIntoScene(GameObject levelPrefab, out string message)
    {
        var prefabConfig = levelPrefab != null ? levelPrefab.GetComponent<LevelConfig>() : null;
        if (prefabConfig == null) { message = "That prefab has no LevelConfig."; return false; }

        var levelsRoot = GameObject.Find("== Levels");
        var loco = Object.FindFirstObjectByType<Locomotion>();
        var aiManager = Object.FindFirstObjectByType<AiManager>();
        var gameManager = Object.FindFirstObjectByType<GameManager>();
        if (levelsRoot == null || loco == null || aiManager == null || gameManager == null)
        {
            message = "Open the gameplay scene first (needs '== Levels', Player, AiManager and GameManager).";
            return false;
        }

        for (int i = levelsRoot.transform.childCount - 1; i >= 0; i--)
        {
            var child = levelsRoot.transform.GetChild(i).gameObject;
            if (child.GetComponent<LevelConfig>() != null) Undo.DestroyObjectImmediate(child);
            else if (child.activeSelf) { Undo.RecordObject(child, "Deactivate level"); child.SetActive(false); }
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(levelPrefab);
        Undo.RegisterCreatedObjectUndo(instance, "Load level");
        instance.transform.SetParent(levelsRoot.transform, false);
        var cfg = instance.GetComponent<LevelConfig>();

        // Player
        var follower = loco.splineFollower;
        Undo.RecordObject(follower, "Load level");
        follower.spline = cfg.mainSpline;
        follower.followSpeed = cfg.playerSpeed;

        // AI + race settings
        var so = new SerializedObject(aiManager);
        var agents = so.FindProperty("allAgents");
        for (int i = 0; i < agents.arraySize; i++)
        {
            var f = agents.GetArrayElementAtIndex(i).objectReferenceValue as SplineFollower;
            if (f == null) continue;
            Undo.RecordObject(f, "Load level");
            f.spline = cfg.mainSpline;
        }
        so.FindProperty("minSpeed").floatValue = cfg.aiSpeedMin;
        so.FindProperty("maxSpeed").floatValue = cfg.aiSpeedMax;
        so.FindProperty("startSpacing").floatValue = cfg.aiStartSpacingPercent;
        so.FindProperty("startBoostDuration").floatValue = cfg.startBoostDuration;
        so.ApplyModifiedProperties();

        // Game manager: where the AI go after the finish line
        var gmSo = new SerializedObject(gameManager);
        gmSo.FindProperty("endSpline").objectReferenceValue = cfg.endSpline;
        gmSo.ApplyModifiedProperties();

        // End camera (inactive in the scene, so search including inactive objects)
        var endCam = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "End Cam" && t.gameObject.scene.IsValid());
        if (endCam != null && cfg.endCamAnchor != null)
        {
            Undo.RecordObject(endCam, "Load level");
            endCam.SetPositionAndRotation(cfg.endCamAnchor.position, cfg.endCamAnchor.rotation);
        }

        // Park the player at the start so the scene view shows something sensible.
        var start = cfg.mainSpline.Evaluate(0.0);
        Undo.RecordObject(loco.transform, "Load level");
        loco.transform.SetPositionAndRotation(start.position, start.rotation);

        EditorSceneManager.MarkSceneDirty(instance.scene);
        Selection.activeGameObject = instance;
        message = $"Loaded {levelPrefab.name} ({cfg.trackLength:F0} m). Press Play to try it. Don't save the scene with a loaded level if you want to keep gameplay.unity small: generated meshes get embedded in the scene file.";
        return true;
    }
}
