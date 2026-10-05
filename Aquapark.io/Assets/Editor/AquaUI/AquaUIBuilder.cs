using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Builds the whole Aqua Race UI inside the open scene's Canvas from the Figma export in Assets/UI/Figma.
/// Layout numbers are the Figma design pixels (390 x 844); the canvas scales them to the screen by width.
/// Run through Aquapark > UI > Rebuild UI In Open Scene (or call AquaUIBuilder.Build() from code).
/// </summary>
public static partial class AquaUIBuilder
{
    public const string Dir = "Assets/UI/Figma";
    public const string SpriteDir = Dir + "/Sprites";
    public const string FontDir = Dir + "/Fonts";
    public const string DataDir = Dir + "/Data";

    private const float DW = 390f;
    private const float DH = 844f;

    private static readonly Color Navy = new Color32(27, 42, 74, 255);
    private static readonly Color White = Color.white;

    [Serializable]
    private class TextInfo
    {
        public string text;
        public float size, x, y, w, h;
        public string align;
        public string font;
    }

    [Serializable]
    private class SpriteInfo
    {
        public List<TextInfo> texts;
        public string key;
        public float x, y, w, h;
        public int px_w, px_h;
        public bool gen;
        public float pad;
        public float bx, by, bw, bh;
    }

    [Serializable]
    private class SpriteList
    {
        public List<SpriteInfo> items;
    }

    private struct Frame
    {
        public float x, y, w, h;

        public Frame(float x, float y, float w, float h)
        {
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
        }

        public float CX => x + w * 0.5f;
        public float CY => y + h * 0.5f;
        public Frame Moved(float dx, float dy) => new Frame(x + dx, y + dy, w, h);
    }

    private struct TextStyle
    {
        public float stroke;       // outline thickness in design px (0 = none)
        public float shadowY;      // hard drop shadow offset in design px
        public float shadowAlpha;

        public static TextStyle None => new TextStyle();
        public static TextStyle Shadow(float y, float alpha = 0.6f) => new TextStyle { shadowY = y, shadowAlpha = alpha };
        public static TextStyle Outline(float stroke, float shadowY) => new TextStyle { stroke = stroke, shadowY = shadowY, shadowAlpha = 1f };
    }

    private static Dictionary<string, SpriteInfo> info;
    private static TMP_FontAsset lilita;
    private static TMP_FontAsset fredoka;

    #region Menu entries

    [MenuItem("Aquapark/UI/Import Figma Assets")]
    public static void ImportMenu()
    {
        Debug.Log(ImportAssets());
    }

    [MenuItem("Aquapark/UI/Rebuild UI In Open Scene")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    #endregion

    #region Import (sprites, fonts, skin data)

    private static readonly Dictionary<string, float> SlicedRadius = new Dictionary<string, float>
    {
        { "seg_sel", 18.5f }, { "seg_bg", 24.5f }, { "price_pill", 11f }, { "price_pill_red", 11f }, { "toast_pill", 20f },
        { "btn_green", 18f }, { "btn_yellow", 18f }, { "btn_blue", 18f }, { "btn_pink", 18f }, { "popup_panel", 30f },
        { "skin_grid_bg", 24f }, { "white_pill", 12f }
    };

    /// <summary>Sets every exported PNG up as a sprite (3 pixels per design pixel) and creates the TextMeshPro fonts and the skin list.</summary>
    public static string ImportAssets()
    {
        LoadInfo();
        var log = new System.Text.StringBuilder();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string file in Directory.GetFiles(SpriteDir, "*.png"))
            {
                string path = file.Replace('\\', '/');
                string key = Path.GetFileNameWithoutExtension(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 3f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 4096;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;

                // Full rectangle meshes: needed for 9-sliced pictures and cheaper for UI anyway.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteExtrude = 0;
                importer.SetTextureSettings(settings);

                if (SlicedRadius.TryGetValue(key, out float radius) && info.TryGetValue(key, out SpriteInfo si))
                {
                    int side = Mathf.RoundToInt((si.pad + radius) * 3f);
                    int vertical = Mathf.Max(1, Mathf.Min(side, si.px_h / 2 - 1));
                    side = Mathf.Min(side, si.px_w / 2 - 1);
                    importer.spriteBorder = new Vector4(side, vertical, side, vertical);
                }

                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        log.AppendLine("Sprites imported.");

        Directory.CreateDirectory(FontDir + "/Materials");
        lilita = MakeFont("LilitaOne-Regular.ttf", "LilitaOne SDF");
        fredoka = MakeFont("Fredoka-Bold.ttf", "Fredoka Bold SDF");
        log.AppendLine("Fonts: " + (lilita != null) + ", " + (fredoka != null));

        MakeSkinDatabase();
        log.AppendLine("Skin database ready.");
        return log.ToString();
    }

    private static TMP_FontAsset MakeFont(string ttf, string assetName)
    {
        string path = FontDir + "/" + assetName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null)
        {
            return existing;
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/" + ttf);
        if (font == null)
        {
            Debug.LogError("Font file missing: " + ttf);
            return null;
        }

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 15, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        asset.name = assetName;
        AssetDatabase.CreateAsset(asset, path);
        asset.material.name = assetName + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        for (int i = 0; i < asset.atlasTextures.Length; i++)
        {
            asset.atlasTextures[i].name = assetName + " Atlas " + i;
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
        }

        var chars = new System.Text.StringBuilder();
        for (int c = 32; c < 127; c++)
        {
            chars.Append((char)c);
        }

        asset.TryAddCharacters(chars.ToString(), out string missing);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void MakeSkinDatabase()
    {
        Directory.CreateDirectory(DataDir);
        string path = DataDir + "/SkinDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<SkinDatabase>(path);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<SkinDatabase>();
            AssetDatabase.CreateAsset(db, path);
        }

        SkinData Make(string id, string name, SkinUnlock unlock, int price, RewardType currency, int level, SkinRarity rarity, int color, bool owned)
        {
            return new SkinData
            {
                id = id,
                displayName = name,
                icon = Spr("char_" + id),
                unlock = unlock,
                price = price,
                currency = currency,
                unlockLevel = level,
                rarity = rarity,
                cardColor = color,
                ownedByDefault = owned
            };
        }

        db.skins = new[]
        {
            Make("penguin", "Cool Penguin", SkinUnlock.Free, 0, RewardType.Coins, 0, SkinRarity.Epic, 0, true),
            Make("pickle", "Pickle", SkinUnlock.Free, 0, RewardType.Coins, 0, SkinRarity.Common, 1, true),
            Make("bubble", "Bubble", SkinUnlock.Free, 0, RewardType.Coins, 0, SkinRarity.Common, 1, true),
            Make("duck", "Duck", SkinUnlock.Currency, 1500, RewardType.Coins, 0, SkinRarity.Rare, 2, false),
            Make("shark", "Shark", SkinUnlock.RewardedAd, 0, RewardType.Coins, 0, SkinRarity.Rare, 1, false),
            Make("alien", "Alien", SkinUnlock.Currency, 80, RewardType.Gems, 0, SkinRarity.Epic, 0, false),
            Make("fox", "Fox", SkinUnlock.PlayerLevel, 0, RewardType.Coins, 12, SkinRarity.Rare, 3, false),
            Make("ghost", "Ghost", SkinUnlock.PlayerLevel, 0, RewardType.Coins, 12, SkinRarity.Legendary, 3, false)
        };

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private static void LoadInfo()
    {
        info = new Dictionary<string, SpriteInfo>();
        string json = File.ReadAllText(Dir + "/sprites.json");
        foreach (SpriteInfo item in JsonUtility.FromJson<SpriteList>(json).items)
        {
            info[item.key] = item;
        }
    }

    private static void LoadFonts()
    {
        lilita = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "/LilitaOne SDF.asset");
        fredoka = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "/Fredoka Bold SDF.asset");
    }

    #endregion

    #region Sprites and rects

    private static Sprite Spr(string key)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + key + ".png");
        if (sprite == null)
        {
            Debug.LogWarning("AquaUI: sprite not found: " + key);
        }

        return sprite;
    }

    private static SpriteInfo Info(string key)
    {
        if (!info.TryGetValue(key, out SpriteInfo si))
        {
            throw new Exception("AquaUI: unknown sprite " + key);
        }

        return si;
    }

    /// <summary>Where an exported Figma piece sits on its screen.</summary>
    private static Frame UnitFrame(string key)
    {
        SpriteInfo si = Info(key);
        return new Frame(si.x, si.y, si.w, si.h);
    }

    /// <summary>The logical box of a Figma node (without its shadow margin).</summary>
    private static Frame NodeFrame(string key)
    {
        SpriteInfo si = Info(key);
        return new Frame(si.bx, si.by, si.bw, si.bh);
    }

    private static Frame Grow(Frame f, float pad)
    {
        return new Frame(f.x - pad, f.y - pad, f.w + pad * 2f, f.h + pad * 2f);
    }

    /// <summary>
    /// Makes a full-width strip (vignette, footer bar) stretch to the screen edges on screens wider than the design,
    /// keeping its designed overhang on each side. Use only on direct children of full-screen containers.
    /// </summary>
    private static RectTransform StretchX(RectTransform rt)
    {
        float w = rt.sizeDelta.x, cx = rt.anchoredPosition.x;
        rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
        rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
        rt.sizeDelta = new Vector2(w - DW, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2(cx, rt.anchoredPosition.y);
        return rt;
    }

    private static RectTransform NewRect(Transform parent, string name, Frame f, Frame? parentFrame)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(f.w, f.h);
        rt.pivot = new Vector2(0.5f, 0.5f);

        if (parentFrame == null)
        {
            // Directly on a full-screen container: pin to the top, middle or bottom depending on where it sits,
            // so the layout still works on taller or shorter phones.
            float cy = f.CY;
            float anchorY = cy < DH * 0.38f ? 1f : cy > DH * 0.62f ? 0f : 0.5f;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY);
            float y = anchorY == 1f ? -cy : anchorY == 0f ? DH - cy : DH * 0.5f - cy;
            rt.anchoredPosition = new Vector2(f.CX - DW * 0.5f, y);
        }
        else
        {
            Frame pf = parentFrame.Value;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(f.CX - pf.CX, -(f.CY - pf.CY));
        }

        return rt;
    }

    private static RectTransform Container(Transform parent, string name, Frame? frame = null, Frame? parentFrame = null)
    {
        RectTransform rt;
        if (frame == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
        }
        else
        {
            rt = NewRect(parent, name, frame.Value, parentFrame);
        }

        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>An exported Figma piece, placed exactly where the design has it.</summary>
    private static Image Unit(Transform parent, string key, string name = null, float dx = 0f, float dy = 0f, Frame? parentFrame = null, bool raycast = false)
    {
        Frame f = UnitFrame(key).Moved(dx, dy);
        RectTransform rt = NewRect(parent, name ?? key, f, parentFrame);
        return SetImage(rt, Spr(key), raycast);
    }

    private static Image SetImage(RectTransform rt, Sprite sprite, bool raycast)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = raycast;
        return image;
    }

    /// <summary>A generated sprite (bar, card, panel) drawn around a logical box. Its shadow margin is added automatically.</summary>
    private static Image Gen(Transform parent, string key, Frame logical, string name = null, Frame? parentFrame = null, bool sliced = false)
    {
        SpriteInfo si = Info(key);
        RectTransform rt = NewRect(parent, name ?? key, Grow(logical, si.pad), parentFrame);
        Image image = SetImage(rt, Spr(key), false);
        if (sliced)
        {
            Slice(image);
        }

        return image;
    }

    /// <summary>9-slices a picture. The sprites are 3 px per design px and the canvas reference is 100 px per unit, so the multiplier makes one design px equal 3 sprite px.</summary>
    private static void Slice(Image image)
    {
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 100f;
    }

    /// <summary>An invisible tap area.</summary>
    private static Button Hit(Transform parent, string name, Frame f, Frame? parentFrame = null, bool press = true)
    {
        RectTransform rt = NewRect(parent, name, f, parentFrame);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        return MakeButton(rt.gameObject, image, press);
    }

    private static Button MakeButton(GameObject go, Graphic target, bool press = true)
    {
        var button = go.AddComponent<Button>();
        button.targetGraphic = target;
        button.transition = Selectable.Transition.None;
        if (press)
        {
            go.AddComponent<PressFx>();
        }

        return button;
    }

    /// <summary>Makes an existing picture tappable.</summary>
    private static Button Tappable(Image image)
    {
        image.raycastTarget = true;
        return MakeButton(image.gameObject, image);
    }

    #endregion

    #region Text

    private static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, Frame f, Frame? parentFrame,
        TextAlignmentOptions align, TextStyle style, bool autoSize = false, float minSize = 8f)
    {
        RectTransform rt = NewRect(parent, name, f, parentFrame);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSharedMaterial = TextMaterial(font, style, size);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;
        if (autoSize)
        {
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = size;
            tmp.overflowMode = TextOverflowModes.Truncate;
        }

        return tmp;
    }

    private static TextMeshProUGUI Lilita(Transform parent, string name, string text, float size, Color color, Frame f, Frame? pf = null,
        TextAlignmentOptions align = TextAlignmentOptions.Center, TextStyle? style = null, bool autoSize = false)
    {
        return Text(parent, name, text, lilita, size, color, f, pf, align, style ?? TextStyle.Shadow(2f), autoSize);
    }

    private static TextMeshProUGUI Fredoka(Transform parent, string name, string text, float size, Color color, Frame f, Frame? pf = null,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        return Text(parent, name, text, fredoka, size, color, f, pf, align, TextStyle.None);
    }

    /// <summary>Outline + hard shadow like the Figma text (navy stroke, offset navy shadow). Cached as material assets.</summary>
    private static Material TextMaterial(TMP_FontAsset font, TextStyle style, float size)
    {
        if (style.stroke <= 0f && style.shadowY <= 0f)
        {
            return font.material;
        }

        // The glyph edge fades over 15 / 90 em in the atlas, so outline and shadow are fractions of that range.
        const float spread = 15f / 90f;
        float strokeEm = style.stroke / size;
        float shadowEm = style.shadowY / size;
        float outline = Mathf.Clamp01(strokeEm / spread * (size > 40f ? 0.65f : 0.85f));
        float shadow = Mathf.Clamp01(shadowEm / spread);

        string key = string.Format("{0}_o{1:0.00}_s{2:0.00}_a{3:0.0}", font.name.Replace(" ", ""), outline, shadow, style.shadowAlpha);
        string path = FontDir + "/Materials/" + key + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        var material = new Material(font.material) { name = key };
        if (outline > 0f)
        {
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor("_OutlineColor", Navy);
            material.SetFloat("_OutlineWidth", outline);
            // The outline is drawn centred on the glyph edge, so grow the letters by half of it to keep them readable.
            material.SetFloat("_FaceDilate", outline * 0.4f);
        }

        if (shadow > 0f)
        {
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(Navy.r, Navy.g, Navy.b, style.shadowAlpha));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -shadow);
            material.SetFloat("_UnderlayDilate", outline);
            material.SetFloat("_UnderlaySoftness", 0f);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    #endregion
}
