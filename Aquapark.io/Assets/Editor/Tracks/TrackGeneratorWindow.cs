using UnityEditor;
using UnityEngine;

/// <summary>
/// Aquapark > Level Generator
/// 1. Create Definitions: makes N TrackDefinition assets whose difficulty ramps from the first level to the last.
/// 2. Tweak any definition by hand (length, curves, slope, fountains, race speeds, tube shape...).
/// 3. Generate Prefabs: builds a level prefab from each definition and refreshes the LevelDatabase.
/// </summary>
public class TrackGeneratorWindow : EditorWindow
{
    [SerializeField] TrackBatchSettings settings = new TrackBatchSettings();
    [SerializeField] int generateFrom = 1;
    [SerializeField] int generateTo = 50;
    [SerializeField] int loadLevel = 1;
    [SerializeField] TrackDefinition singleDefinition;

    Vector2 scroll, logScroll;
    string log = "";
    bool showRamps = true;

    [MenuItem("Aquapark/Level Generator")]
    static void Open() => GetWindow<TrackGeneratorWindow>("Level Generator").minSize = new Vector2(420, 560);

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox("Step 1: create definitions. Step 2 (optional): edit any Level_XXX definition in " + TrackGenerator.DefinitionsDir + ". Step 3: generate prefabs.", MessageType.Info);

        // ---- 1. Definitions
        EditorGUILayout.LabelField("1. Create definitions", EditorStyles.boldLabel);
        settings.levelCount = Mathf.Max(1, EditorGUILayout.IntField("Number of levels", settings.levelCount));
        settings.firstLevel = Mathf.Max(1, EditorGUILayout.IntField("First level number", settings.firstLevel));
        settings.baseSeed = EditorGUILayout.IntField(new GUIContent("Base seed", "Change to get a completely different set of layouts."), settings.baseSeed);
        settings.overwriteExisting = EditorGUILayout.Toggle(new GUIContent("Overwrite existing", "Off = levels that already have a definition are left alone, so your hand edits are safe."), settings.overwriteExisting);
        settings.difficulty = EditorGUILayout.CurveField(new GUIContent("Difficulty curve", "Left = first level, right = last. Bottom = easy end of each range, top = hard end."), settings.difficulty, Color.cyan, new Rect(0, 0, 1, 1), GUILayout.Height(40));

        showRamps = EditorGUILayout.Foldout(showRamps, "Difficulty ramps (easy level -> hard level)", true);
        if (showRamps)
        {
            EditorGUI.indentLevel++;
            RangeField("Level time (s)", settings.levelTime);
            RangeField("Turn rate (°/m)", settings.turnRate);
            RangeField("Max swing (°)", settings.maxYaw);
            RangeField("Slope (°)", settings.slopeAngle);
            RangeField("Steep drops", settings.steepSections);
            RangeField("Steep angle (°)", settings.steepAngle);
            RangeField("Flat stretches", settings.flatSections);
            RangeField("Fountains", settings.fountains);
            RangeField("AI speed min", settings.aiSpeedMin);
            RangeField("AI speed max", settings.aiSpeedMax);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Loops", EditorStyles.miniBoldLabel);
            settings.loopsFromLevel = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Loops start at level", "Levels below this have no loops."), settings.loopsFromLevel));
            RangeField("Loops per level", settings.loops);
            RangeField("Loop radius (m)", settings.loopRadius);
            settings.loopGap = EditorGUILayout.FloatField(new GUIContent("Gap between laps (m)", "Vertical drop from one lap to the next."), settings.loopGap);
            EditorGUI.indentLevel--;
        }

        if (GUILayout.Button("Create / Update Definitions", GUILayout.Height(28)))
            Log(TrackBatch.CreateDefinitions(settings));

        EditorGUILayout.Space(10);

        // ---- 2. Generate
        EditorGUILayout.LabelField("2. Generate prefabs", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            generateFrom = Mathf.Max(1, EditorGUILayout.IntField("From level", generateFrom));
            generateTo = Mathf.Max(generateFrom, EditorGUILayout.IntField("To level", generateTo));
        }
        if (GUILayout.Button("Generate Prefabs (range)", GUILayout.Height(28)))
            Log(TrackBatch.GenerateRange(generateFrom, generateTo, true));
        if (GUILayout.Button("Generate ALL Definitions"))
            Log(TrackBatch.GenerateRange(1, int.MaxValue, true));

        EditorGUILayout.Space(10);

        // ---- 3. One level
        EditorGUILayout.LabelField("3. Single level / test in gameplay scene", EditorStyles.boldLabel);
        singleDefinition = (TrackDefinition)EditorGUILayout.ObjectField("Definition", singleDefinition, typeof(TrackDefinition), false);
        using (new EditorGUI.DisabledScope(singleDefinition == null))
        {
            if (GUILayout.Button("Generate This Definition"))
                Log(TrackBatch.GenerateRange(singleDefinition.levelNumber, singleDefinition.levelNumber, true));
        }
        loadLevel = Mathf.Max(1, EditorGUILayout.IntField("Level number", loadLevel));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load Into Gameplay Scene")) LoadLevel(loadLevel);
            if (GUILayout.Button("Select Prefab")) SelectPrefab(loadLevel);
            if (GUILayout.Button("Select Definition")) SelectDefinition(loadLevel);
        }

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Rebuild Level Database"))
        {
            var db = TrackGenerator.RebuildDatabase();
            Log($"LevelDatabase now lists {db.Count} levels.");
            Selection.activeObject = db;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MinHeight(140));
        EditorGUILayout.TextArea(log, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndScrollView();
    }

    static void RangeField(string label, RampRange r)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            r.easy = EditorGUILayout.FloatField(r.easy);
            EditorGUILayout.LabelField("→", GUILayout.Width(18));
            r.hard = EditorGUILayout.FloatField(r.hard);
        }
    }

    void LoadLevel(int number)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrackGenerator.PrefabPath(number));
        if (prefab == null) { Log($"Level {number:000} has no prefab yet. Generate it first."); return; }
        TrackLoader.LoadIntoScene(prefab, out string message);
        Log(message);
    }

    void SelectPrefab(int number)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrackGenerator.PrefabPath(number));
        if (prefab != null) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); }
        else Log($"Level {number:000} has no prefab yet.");
    }

    void SelectDefinition(int number)
    {
        var def = AssetDatabase.LoadAssetAtPath<TrackDefinition>($"{TrackGenerator.DefinitionsDir}/Level_{number:000}.asset");
        if (def != null) { Selection.activeObject = def; EditorGUIUtility.PingObject(def); singleDefinition = def; }
        else Log($"Level {number:000} has no definition yet.");
    }

    void Log(string message)
    {
        log = message + "\n\n" + log;
        if (log.Length > 20000) log = log.Substring(0, 20000);
        Repaint();
    }
}
