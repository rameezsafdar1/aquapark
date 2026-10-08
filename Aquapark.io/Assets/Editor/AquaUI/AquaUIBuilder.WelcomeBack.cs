using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class AquaUIBuilder
{
    public const string WelcomeSourceDir = "Assets/2D/Subs Screen";
    public const string WelcomeDir = "Assets/UI/WelcomeBack";
    private const string WelcomeSheet = WelcomeSourceDir + "/Aqua Park Waterpark UI Sprite Sheet (1).png";
    private const string WelcomeWater = WelcomeSourceDir + "/gradient.png";

    // Sprite names in the sheet (Unity's automatic slice names).
    private const string WelcomeTitleSprite = "Aqua Park Waterpark UI Sprite Sheet (1)_3";
    private const string WelcomeRowSprite = "Aqua Park Waterpark UI Sprite Sheet (1)_7";
    private const string WelcomeCheckSprite = "Aqua Park Waterpark UI Sprite Sheet (1)_2";
    private const string WelcomeContinueSprite = "Aqua Park Waterpark UI Sprite Sheet (1)_8";

    // The water picture's size; the layout below is in its pixels (the mockup 'Waterpark Welcome Back UI.png' has the same size).
    private const float WaterW = 1068f, WaterH = 1472f;

    // Its top fades out between these heights (share of its height from the top) so the video shows above the splash.
    private const float WaterFadeStart = 0.22f, WaterOpaqueFrom = 0.355f;

    private static readonly Color WelcomeNavy = new Color32(0x0a, 0x2f, 0x7a, 255);

    /// <summary>
    /// The new VIP offer: gameplay video on top, the water picture with the characters at the bottom (its top made
    /// transparent here), title, two tick options and Continue placed as shares of the water picture, so the whole
    /// thing keeps the mockup's proportions on every screen. Art: Assets/2D/Subs Screen (pieces cut into Assets/UI/WelcomeBack).
    /// </summary>
    private static WelcomeBackScreen BuildWelcomeBack(Transform root)
    {
        Sprite water = MakeWelcomeArt(out Sprite box, out Sprite bar, out Sprite shadeSprite);

        MakePanelRoot(root, "WelcomeBack", out RectTransform page);
        var screen = page.gameObject.AddComponent<WelcomeBackScreen>();

        // Solid colour until the video's first frame; also stops taps reaching the menu.
        Image backdrop = SetImage(Container(page, "Backdrop"), null, true);
        backdrop.color = WelcomeNavy;

        RectTransform videoRect = Container(page, "Video");
        screen.video = videoRect.gameObject.AddComponent<RawImage>();
        screen.video.raycastTarget = false;
        screen.video.enabled = false;
        screen.clip = FindShowcaseClip();

        // Water: full width, pinned to the bottom, height from its aspect.
        RectTransform waterRect = Container(page, "Water");
        waterRect.anchorMin = new Vector2(0f, 0f);
        waterRect.anchorMax = new Vector2(1f, 0f);
        waterRect.pivot = new Vector2(0.5f, 0f);
        waterRect.sizeDelta = new Vector2(0f, DW * WaterH / WaterW);
        waterRect.anchoredPosition = Vector2.zero;
        SetImage(waterRect, water, false);
        var fitter = waterRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        fitter.aspectRatio = WaterW / WaterH;
        screen.water = waterRect;
        screen.opaqueFrom = WaterOpaqueFrom;

        Image title = SetImage(OnWater(waterRect, "Title", 194.5f, 402.5f, 873.5f, 581.5f), SheetSprite(WelcomeTitleSprite), false);
        title.preserveAspect = true;

        screen.options = new WelcomeBackScreen.Option[2];
        for (int i = 0; i < 2; i++)
        {
            float dy = 125f * i;
            var option = new WelcomeBackScreen.Option();
            RectTransform row = OnWater(waterRect, "Option" + (i + 1), 55f, 968f + dy, 1015f, 1053f + dy);
            Image hit = SetImage(row, null, true);
            hit.color = new Color(1f, 1f, 1f, 0f);
            option.button = MakeButton(row.gameObject, hit, false);

            SetImage(OnWater(row, "Box", 55f, 968f + dy, 139f, 1053f + dy, row), box, false);
            Image barImage = SetImage(OnWater(row, "Bar", 152f, 970f + dy, 1015f, 1050f + dy, row), bar, false);
            barImage.color = new Color(1f, 1f, 1f, 0.55f);   // frosted, as in the mockup

            Image check = SetImage(OnWater(row, "Check", 66f, 976.5f + dy, 128f, 1044.5f + dy, row), SheetSprite(WelcomeCheckSprite), false);
            check.preserveAspect = true;
            option.check = check.gameObject;
            option.check.SetActive(i == 0);

            // Auto-sized up to a large cap so it grows with the bar on wider screens.
            option.label = Lilita(row, "Label", "get 20 set " + (i + 1) + " waterpark rides for INR 819/weekly recurring subscription with 3 day trial",
                40, White, new Frame(0, 0, 10, 10), null, TextAlignmentOptions.MidlineLeft, TextStyle.Outline(5f, 4f), true);
            option.label.textWrappingMode = TextWrappingModes.Normal;   // two lines
            option.label.lineSpacing = -8f;
            OnWater(option.label.rectTransform, 182f, 978f + dy, 985f, 1042f + dy, row);
            screen.options[i] = option;
        }

        Image cont = SetImage(OnWater(waterRect, "Continue", 135f, 1215.5f, 937f, 1376.5f), SheetSprite(WelcomeContinueSprite), false);
        cont.preserveAspect = true;
        screen.continueButton = Tappable(cont);

        // The water is lifted to make room for the small print: continue it below with its mirror image (seamless at the edge).
        RectTransform below = Container(waterRect, "WaterBelow");
        below.anchorMin = new Vector2(0f, -1f);
        below.anchorMax = new Vector2(1f, 0f);
        below.localScale = new Vector3(1f, -1f, 1f);
        below.SetAsFirstSibling();
        SetImage(below, water, false);

        // Small print at the bottom, on a navy shade so it stays readable over the water.
        RectTransform shade = Container(page, "LegalShade");
        shade.anchorMin = new Vector2(0f, 0f);
        shade.anchorMax = new Vector2(1f, 0f);
        shade.pivot = new Vector2(0.5f, 0f);
        shade.anchoredPosition = Vector2.zero;
        shade.sizeDelta = new Vector2(0f, screen.legalSpace + 40f);
        SetImage(shade, shadeSprite, false);

        TextMeshProUGUI legal = Fredoka(page, "Legal", "", 10.5f, White, new Frame(0, 0, 10, 10), null, TextAlignmentOptions.Bottom);
        RectTransform lr = legal.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f);
        lr.anchorMax = new Vector2(1f, 0f);
        lr.pivot = new Vector2(0.5f, 0f);
        lr.anchoredPosition = new Vector2(0f, 8f);
        lr.sizeDelta = new Vector2(-32f, screen.legalSpace - 14f);
        legal.textWrappingMode = TextWrappingModes.Normal;
        legal.enableAutoSizing = true;
        legal.fontSizeMin = 7f;
        legal.fontSizeMax = 10.5f;
        legal.lineSpacing = -4f;
        legal.raycastTarget = true;   // for the two links
        legal.gameObject.AddComponent<TextLinks>();
        screen.legalText = legal;
        screen.RefreshLegal();

        AddWelcomeClose(screen);

        screen.Layout();
        page.gameObject.SetActive(false);   // UIManager opens it; keep the scene showing the menu
        return screen;
    }

    /// <summary>
    /// The close button: a plain white X (no button behind it), top right, drawn above everything else on the panel.
    /// It appears closeDelay seconds after the screen opens. Replaces an existing one; touches nothing else on the panel.
    /// </summary>
    public static Button AddWelcomeClose(WelcomeBackScreen screen)
    {
        screen.closeButton = MakeCloseX(screen.transform);
        EditorUtility.SetDirty(screen);
        return screen.closeButton;
    }

    /// <summary>
    /// A plain X close button in the top-right corner (same spot as the VIP screen's X): an invisible 56 px tap area
    /// with the 28 px icon in it. Replaces the panel's existing "Close" child.
    /// </summary>
    private static Button MakeCloseX(Transform panel)
    {
        Transform old = panel.Find("Close");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        RectTransform rt = Container(panel, "Close");
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(56f, 56f);
        rt.anchoredPosition = new Vector2(-36f, -54f);
        rt.SetAsLastSibling();
        Image hit = SetImage(rt, null, true);
        hit.color = new Color(1f, 1f, 1f, 0f);

        RectTransform icon = Container(rt, "Icon");
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = new Vector2(28f, 28f);
        icon.anchoredPosition = Vector2.zero;
        SetImage(icon, CloseXSprite(), false);
        return Tappable(hit);
    }

    /// <summary>White X with rounded ends and a soft navy shadow (so it reads over the video), drawn once to a PNG.</summary>
    private static Sprite CloseXSprite()
    {
        string path = WelcomeDir + "/close_x.png";
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(WelcomeDir);
            const int size = 128;
            const float a = 26f, b = 102f, halfWidth = 9f;
            var px = new Color32[size * size];
            Color32 navy = WelcomeNavy;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(SegmentDistance(p, new Vector2(a, a), new Vector2(b, b)), SegmentDistance(p, new Vector2(a, b), new Vector2(b, a)));
                    var ps = p + new Vector2(0f, 5f);   // shadow sits below the X
                    float ds = Mathf.Min(SegmentDistance(ps, new Vector2(a, a), new Vector2(b, b)), SegmentDistance(ps, new Vector2(a, b), new Vector2(b, a)));

                    float face = Mathf.Clamp01(halfWidth + 0.75f - d);
                    float shadow = 0.55f * (1f - Mathf.SmoothStep(0f, 1f, (ds - halfWidth + 1f) / 7f));
                    float alpha = face + shadow * (1f - face);
                    Color c = Color.Lerp((Color)navy, Color.white, alpha > 0f ? face / alpha : 0f);
                    c.a = alpha;
                    px[y * size + x] = c;
                }
            }

            SavePng(path, px, size, size);
            AssetDatabase.Refresh();
        }

        SetupWelcomeSprite(path);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    [MenuItem("Aquapark/UI/Rebuild Welcome Back Only")]
    public static void RebuildWelcomeBackMenu()
    {
        Debug.Log(RebuildWelcomeBack());
    }

    /// <summary>Replaces only the Welcome Back panel in the open scene; every other panel is left exactly as it is.</summary>
    public static string RebuildWelcomeBack()
    {
        LoadInfo();
        LoadFonts();
        GameObject canvas = GameObject.Find("Canvas");
        Transform root = canvas != null ? canvas.transform.Find("AquaUI") : null;
        UIManager ui = root != null ? root.GetComponent<UIManager>() : null;
        if (ui == null)
        {
            return "Open gameplay.unity first (needs Canvas/AquaUI with the UIManager).";
        }

        Transform old = root.Find("WelcomeBack");
        int index = old != null ? old.GetSiblingIndex() : ui.vip != null ? ui.vip.transform.GetSiblingIndex() + 1 : root.childCount;
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        WelcomeBackScreen screen = BuildWelcomeBack(root);
        screen.transform.SetSiblingIndex(index);
        ui.welcomeBack = screen;
        EditorUtility.SetDirty(ui);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.scene);
        return "Welcome Back panel rebuilt. Save the scene to keep it.";
    }

    /// <summary>A child placed by a box in water-picture pixels (top-left origin), so it scales with the picture.</summary>
    private static RectTransform OnWater(RectTransform parent, string name, float x0, float y0, float x1, float y1, RectTransform rowBox = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return OnWater(rt, x0, y0, x1, y1, rowBox);
    }

    /// <summary>
    /// Anchors an existing rect to a box in water-picture pixels. Inside a row ('rowBox', itself placed with OnWater)
    /// the box is turned into shares of that row.
    /// </summary>
    private static RectTransform OnWater(RectTransform rt, float x0, float y0, float x1, float y1, RectTransform rowBox = null)
    {
        Vector2 min = new Vector2(x0 / WaterW, 1f - y1 / WaterH);
        Vector2 max = new Vector2(x1 / WaterW, 1f - y0 / WaterH);
        if (rowBox != null)
        {
            Vector2 rMin = rowBox.anchorMin, rSize = rowBox.anchorMax - rowBox.anchorMin;
            min = new Vector2((min.x - rMin.x) / rSize.x, (min.y - rMin.y) / rSize.y);
            max = new Vector2((max.x - rMin.x) / rSize.x, (max.y - rMin.y) / rSize.y);
        }

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static Sprite SheetSprite(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(WelcomeSheet))
        {
            if (asset is Sprite sprite && sprite.name == name)
            {
                return sprite;
            }
        }

        Debug.LogWarning("AquaUI: sprite not found in the welcome sheet: " + name);
        return null;
    }

    #region Art cut from Assets/2D/Subs Screen

    /// <summary>
    /// Writes the pieces the sheet does not have on their own: the water picture with its top faded out, and the
    /// option row split into its tick box and its bar (so only the bar is see-through). Redone when the sources change.
    /// </summary>
    private static Sprite MakeWelcomeArt(out Sprite box, out Sprite bar, out Sprite shade)
    {
        Directory.CreateDirectory(WelcomeDir);
        string waterPath = WelcomeDir + "/welcome_water.png";
        string boxPath = WelcomeDir + "/welcome_option_box.png";
        string barPath = WelcomeDir + "/welcome_option_bar.png";
        string shadePath = WelcomeDir + "/welcome_legal_shade.png";

        if (!File.Exists(shadePath))
        {
            // Clear at the top to 80 % navy at the bottom.
            const int h = 128;
            Color32 navy = WelcomeNavy;
            var px = new Color32[4 * h];
            for (int y = 0; y < h; y++)
            {
                navy.a = (byte)Mathf.RoundToInt(204f * Mathf.SmoothStep(0f, 1f, 1f - (float)y / (h - 1)));
                for (int x = 0; x < 4; x++)
                {
                    px[y * 4 + x] = navy;
                }
            }

            SavePng(shadePath, px, 4, h);
        }

        if (IsStale(waterPath, WelcomeWater))
        {
            Texture2D tex = LoadPng(WelcomeWater);
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            for (int y = 0; y < h; y++)
            {
                float fromTop = 1f - (y + 0.5f) / h;
                byte a = (byte)Mathf.RoundToInt(255f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WaterFadeStart, WaterOpaqueFrom, fromTop)));
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x].a = a;
                }
            }

            SavePng(waterPath, px, w, h);
            Object.DestroyImmediate(tex);
        }

        if (IsStale(boxPath, WelcomeSheet) || IsStale(barPath, WelcomeSheet))
        {
            Sprite row = SheetSprite(WelcomeRowSprite);
            Texture2D sheet = LoadPng(WelcomeSheet);
            RectInt r = new RectInt((int)row.rect.x, (int)row.rect.y, (int)row.rect.width, (int)row.rect.height);

            // The box ends at the first fully clear column after it; the bar is everything to the right of that gap.
            int gap = -1, barStart = -1;
            for (int x = r.xMin; x < r.xMax; x++)
            {
                bool clear = ColumnClear(sheet, x, r);
                if (gap < 0 && clear && x > r.xMin + 5)
                {
                    gap = x;
                }
                else if (gap >= 0 && !clear)
                {
                    barStart = x;
                    break;
                }
            }

            SaveCrop(sheet, new RectInt(r.xMin, r.yMin, gap - r.xMin, r.height), boxPath);
            SaveCrop(sheet, new RectInt(barStart, r.yMin, r.xMax - barStart, r.height), barPath);
            Object.DestroyImmediate(sheet);
        }

        AssetDatabase.Refresh();
        foreach (string path in new[] { waterPath, boxPath, barPath, shadePath })
        {
            SetupWelcomeSprite(path);
        }

        shade = AssetDatabase.LoadAssetAtPath<Sprite>(shadePath);
        box = AssetDatabase.LoadAssetAtPath<Sprite>(boxPath);
        bar = AssetDatabase.LoadAssetAtPath<Sprite>(barPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(waterPath);
    }

    private static bool IsStale(string output, string source)
    {
        return !File.Exists(output) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(output);
    }

    private static Texture2D LoadPng(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        return tex;
    }

    private static void SavePng(string path, Color32[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static bool ColumnClear(Texture2D tex, int x, RectInt r)
    {
        for (int y = r.yMin; y < r.yMax; y++)
        {
            if (tex.GetPixel(x, y).a > 0.04f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Saves a part of the sheet, trimmed to its solid pixels (the sheet has faint white specks around the pieces).</summary>
    private static void SaveCrop(Texture2D tex, RectInt r, string path)
    {
        int x0 = r.xMax, x1 = r.xMin - 1, y0 = r.yMax, y1 = r.yMin - 1;
        for (int y = r.yMin; y < r.yMax; y++)
        {
            for (int x = r.xMin; x < r.xMax; x++)
            {
                if (tex.GetPixel(x, y).a > 0.6f)
                {
                    x0 = Mathf.Min(x0, x);
                    x1 = Mathf.Max(x1, x);
                    y0 = Mathf.Min(y0, y);
                    y1 = Mathf.Max(y1, y);
                }
            }
        }

        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        Color32[] px = new Color32[w * h];
        Color32[] all = tex.GetPixels32();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                px[y * w + x] = all[(y0 + y) * tex.width + x0 + x];
            }
        }

        SavePng(path, px, w, h);
    }

    private static void SetupWelcomeSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || importer.textureType == TextureImporterType.Sprite && importer.mipmapEnabled == false)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    #endregion
}
