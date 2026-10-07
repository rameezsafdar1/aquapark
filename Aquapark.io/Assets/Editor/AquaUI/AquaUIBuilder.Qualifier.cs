using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Beach Cup qualifier (shown after a won race): banner, round pips, racer name labels, YOU tag, and its stage assets.</summary>
public static partial class AquaUIBuilder
{
    private const string QualifierDir = "Assets/UI/Qualifier";

    private static QualifierScreen BuildQualifier(Transform root)
    {
        MakePanelRoot(root, "Qualifier", out RectTransform go);
        var q = go.gameObject.AddComponent<QualifierScreen>();

        // Names float over the 3D racers; QualifierScreen moves them every frame.
        RectTransform labels = Container(go, "Labels");
        q.labelLayer = labels;
        q.nameTemplate = Lilita(labels, "NameTemplate", "Name", 13, White, new Frame(0, 0, 140, 22), null, TextAlignmentOptions.Center, TextStyle.Outline(2.5f, 1.5f));
        CenterAnchor(q.nameTemplate.rectTransform);
        q.nameTemplate.gameObject.SetActive(false);

        Frame tagFrame = new Frame(0, 0, 64, 30);
        RectTransform tag = NewRect(labels, "YouTag", tagFrame, null);
        CenterAnchor(tag);
        Gen(tag, "btn_green", tagFrame, "Bg", tagFrame, true);
        Lilita(tag, "Text", "YOU", 18, White, tagFrame, tagFrame, TextAlignmentOptions.Center, TextStyle.Outline(2f, 1.5f));
        q.youTag = tag;

        // Banner: ribbon with the cup name, round text and one pip per round.
        Frame ribbon = new Frame(25, 46, 340, 84);
        RectTransform banner = NewRect(go, "Banner", ribbon, null);
        SetImage(banner, Spr("res_ribbon"), false);
        // Thin outline + spacing: a heavy outline at this size makes the letters run into each other.
        q.titleText = Lilita(banner, "Title", "BEACH CUP", 36, Hex("#FFF36B"), new Frame(65, 56, 260, 50), ribbon, TextAlignmentOptions.Center, TextStyle.Outline(2.2f, 2.5f), true);
        q.titleText.characterSpacing = 4f;

        q.roundText = Lilita(go, "Round", "ROUND 1/3", 20, White, new Frame(95, 134, 200, 26), null, TextAlignmentOptions.Center, TextStyle.Outline(3f, 2.5f));
        // Round bars: navy frame with slightly rounded corners, coloured fill inside (the fill is what gets tinted).
        Sprite rounded = QualifierRoundedSprite();
        q.roundPips = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            Frame pip = new Frame(122 + i * 50, 162, 46, 16);
            Image frame = SetImage(NewRect(go, "Pip" + i, pip, null), rounded, false);
            Slice(frame);
            frame.color = Navy;
            Frame inner = Grow(pip, -2f);
            Image fill = SetImage(NewRect(frame.transform, "Fill", inner, pip), rounded, false);
            Slice(fill);
            fill.pixelsPerUnitMultiplier = 150f;   // smaller corners inside, so the frame looks even all round
            q.roundPips[i] = fill;
        }

        q.resultText = Lilita(go, "Result", "QUALIFIED!", 50, Hex("#FFF36B"), new Frame(20, 752, 350, 64), null, TextAlignmentOptions.Center, TextStyle.Outline(3f, 3.5f), true);
        q.resultText.characterSpacing = 5f;
        q.resultText.gameObject.SetActive(false);

        // Stage assets and what the racers are copied from.
        q.player = Object.FindFirstObjectByType<PlayerEffects>(FindObjectsInactive.Include);
        q.floatController = QualifierFloatController();
        q.waterMaterial = QualifierWaterMaterial(q.waterColor);
        q.rippleMaterial = QualifierRippleMaterial();
        q.splashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NamuFX/StylizedWaterEffects/Prefabs/Water_Splash_Multiple.prefab");
        q.drownVoices = new[]
        {
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/ahh.mp3"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/oaaaen.mp3"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SFX/Funny scream sound effect.mp3")
        };
        return q;
    }

    private static void CenterAnchor(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>One looping state: the treading-water clip the player also uses in the pool.</summary>
    private static RuntimeAnimatorController QualifierFloatController()
    {
        string path = QualifierDir + "/QualifierFloat.controller";
        var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
        if (existing != null)
        {
            return existing;
        }

        EnsureFolder(QualifierDir);
        AnimationClip tread = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/4- Animations/Treading Water.fbx"))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
            {
                tread = clip;
            }
        }

        return UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(path, tread);
    }

    private static Material QualifierWaterMaterial(Color color)
    {
        string path = QualifierDir + "/QualifierWater.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        EnsureFolder(QualifierDir);
        material = new Material(Shader.Find("Unlit/Color")) { name = "QualifierWater" };
        material.SetColor("_Color", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>Transparent unlit ring under each floatie (the fountain's foam ring texture).</summary>
    private static Material QualifierRippleMaterial()
    {
        string path = QualifierDir + "/QualifierRipple.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        EnsureFolder(QualifierDir);
        // Same shader as the fountain's foam; QualifierScreen fades each ring through _TintColor.
        material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")) { name = "QualifierRipple" };
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/VFX/WaterFountain/FoamRing.png"));
        material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.3f));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>A white rectangle with small rounded corners (4 design px), 9-sliced like the other generated pieces.</summary>
    private static Sprite QualifierRoundedSprite()
    {
        string path = QualifierDir + "/rounded_rect.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        EnsureFolder(QualifierDir);
        const int size = 36;       // 12 design px at 3 px per design px
        const float radius = 12f;  // 4 design px
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance outside the rounded rectangle, anti-aliased over one pixel.
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) - radius;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
            }
        }

        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 3f;
        importer.spriteBorder = new Vector4(radius, radius, radius, radius);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }

    /// <summary>Adds (or replaces) only the qualifier in the open scene's UI, without rebuilding the rest.</summary>
    [MenuItem("Aquapark/UI/Add Qualifier To Open Scene")]
    public static void AddQualifierMenu()
    {
        Debug.Log(AddQualifier());
    }

    public static string AddQualifier()
    {
        LoadInfo();
        LoadFonts();
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui == null || lilita == null)
        {
            return "Open gameplay.unity with the Aqua UI built (and fonts imported) first.";
        }

        Transform old = ui.transform.Find("Qualifier");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        ui.qualifier = BuildQualifier(ui.transform);
        // Above the results and popups, under the toast.
        Transform toast = ui.toast != null ? ui.toast.transform : null;
        ui.qualifier.transform.SetSiblingIndex(toast != null ? toast.GetSiblingIndex() : ui.transform.childCount - 1);
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        return "Qualifier added. Save the scene to keep it.";
    }
}
