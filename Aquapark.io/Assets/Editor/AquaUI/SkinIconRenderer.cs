using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders shop icons from the real in-game models under the scene's "Characters Temp" object:
/// every child of Models becomes a character icon, every child of Floaties a floatie icon.
/// Each model is copied far below the scene on its own layer, posed with the "happy Idle" clip and photographed with a
/// transparent background. The scene objects themselves are never touched.
/// </summary>
public static class SkinIconRenderer
{
    public const string CharacterDir = "Assets/UI/Skins/Characters";
    public const string FloatieDir = "Assets/UI/Skins/Floaties";

    // Floatie icon size (characters keep this height but are narrower). 6 px per unit = 2x the Figma art density.
    public const int IconWidth = 336;
    public const int IconHeight = 372;
    public const float PixelsPerUnit = 6f;

    private const int Layer = 31;
    private const int RenderSize = 1024;
    private const int Margin = 4;

    /// <summary>
    /// Extra turn for models whose costume is rigged facing sideways (the lobster's crayfish suit points 90 degrees
    /// off from its body), so the icon shows its face like the others.
    /// </summary>
    private static readonly Dictionary<string, float> YawFix = new Dictionary<string, float> { { "Lobster", 90f } };

    [MenuItem("Aquapark/UI/Render Skin Icons")]
    public static void RenderAll()
    {
        Debug.Log(Render());
    }

    public static string Render()
    {
        var root = GameObject.Find("Characters Temp");
        if (root == null)
        {
            return "Characters Temp not found in the open scene";
        }

        Transform models = root.transform.Find("Models");
        Transform floaties = root.transform.Find("Floaties");
        Directory.CreateDirectory(CharacterDir);
        Directory.CreateDirectory(FloatieDir);

        // Measured: the faces point along Cross(up, Models.forward), not along Models.forward.
        Vector3 forward = models.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 face = Vector3.Cross(Vector3.up, forward);

        var written = new List<string>();
        written.AddRange(RenderCharacters(models, face));

        foreach (Transform child in floaties)
        {
            GameObject clone = MakeClone(child.gameObject, false);
            Bounds b = VisibleBounds(clone);
            Texture2D shot = Photograph(b.center, (face + Vector3.up * 0.85f).normalized, b.extents.magnitude, false);
            Object.DestroyImmediate(clone);

            Texture2D icon = AlphaBox(shot, out RectInt box) ? Fit(shot, box, IconWidth, IconHeight) : new Texture2D(IconWidth, IconHeight);
            string path = FloatieDir + "/" + IdFor(child.name) + ".png";
            File.WriteAllBytes(path, icon.EncodeToPNG());
            written.Add(path);
            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(icon);
        }

        AssetDatabase.Refresh();
        foreach (string path in written)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return "Rendered " + written.Count + " icons";
    }

    /// <summary>"Battery hat charc" -> "battery_hat_charc", "watermelon_001" -> "watermelon".</summary>
    public static string IdFor(string objectName)
    {
        string id = objectName.Trim().ToLowerInvariant().Replace(' ', '_');
        int cut = id.LastIndexOf('_');
        if (cut > 0 && int.TryParse(id.Substring(cut + 1), out _))
        {
            id = id.Substring(0, cut);
        }

        return id;
    }

    /// <summary>
    /// Characters are shot straight from the front with one fixed orthographic camera relative to each model's root, so
    /// every character has the same scale and stands on the same line. All shots then get the same crop (the union of
    /// everything visible, kept symmetric around the centre), so they line up exactly in the cards and the preview.
    /// </summary>
    private static List<string> RenderCharacters(Transform models, Vector3 face)
    {
        var sources = new List<Transform>();
        foreach (Transform child in models)
        {
            sources.Add(child);
        }

        // Pass 1: how far the posed models reach around their roots.
        Bounds reach = new Bounds();
        bool any = false;
        foreach (Transform source in sources)
        {
            GameObject clone = MakeClone(source.gameObject, true);
            Bounds b = VisibleBounds(clone);
            b.center -= clone.transform.position;
            if (!any) { reach = b; any = true; } else { reach.Encapsulate(b); }
            Object.DestroyImmediate(clone);
        }

        Vector3 dir = (face + Vector3.up * 0.12f).normalized;
        float orthoSize = Mathf.Max(reach.extents.x, reach.extents.y, reach.extents.z) * 1.15f;

        // Pass 2: the same camera for everyone.
        var shots = new List<Texture2D>();
        foreach (Transform source in sources)
        {
            GameObject clone = MakeClone(source.gameObject, true);
            shots.Add(Photograph(clone.transform.position + reach.center, dir, orthoSize, true));
            Object.DestroyImmediate(clone);
        }

        // One crop for all: union of visible pixels, widened to be symmetric about the centre column.
        int x0 = RenderSize, y0 = RenderSize, x1 = -1, y1 = -1;
        foreach (Texture2D shot in shots)
        {
            if (AlphaBox(shot, out RectInt box))
            {
                x0 = Mathf.Min(x0, box.xMin); y0 = Mathf.Min(y0, box.yMin);
                x1 = Mathf.Max(x1, box.xMax); y1 = Mathf.Max(y1, box.yMax);
            }
        }

        int half = Mathf.Max(RenderSize / 2 - x0, x1 - RenderSize / 2);
        var crop = new RectInt(RenderSize / 2 - half, y0, half * 2, y1 - y0);

        // Character icons take the crop's own (tall, narrow) shape, so the cards can show them as big as possible.
        int width = Mathf.RoundToInt((IconHeight - Margin * 2f) * crop.width / crop.height) + Margin * 2;

        var written = new List<string>();
        for (int i = 0; i < sources.Count; i++)
        {
            Texture2D icon = Fit(shots[i], crop, width, IconHeight);
            string path = CharacterDir + "/" + IdFor(sources[i].name) + ".png";
            File.WriteAllBytes(path, icon.EncodeToPNG());
            written.Add(path);
            Object.DestroyImmediate(icon);
            Object.DestroyImmediate(shots[i]);
        }

        return written;
    }

    private static GameObject MakeClone(GameObject source, bool pose)
    {
        Quaternion rotation = source.transform.rotation;
        if (YawFix.TryGetValue(source.name, out float yaw))
        {
            rotation = Quaternion.Euler(0f, yaw, 0f) * rotation;
        }

        var clone = Object.Instantiate(source, new Vector3(0f, -5000f, 0f), rotation);
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.transform.localScale = source.transform.lossyScale;
        clone.SetActive(true);
        foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = Layer;
        }

        var animator = clone.GetComponent<Animator>();
        if (pose && animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == "happy Idle")
                {
                    clip.SampleAnimation(clone, 0.3f);
                    break;
                }
            }
        }

        foreach (ParticleSystemRenderer r in clone.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            r.enabled = false;
        }

        return clone;
    }

    private static Bounds VisibleBounds(GameObject go)
    {
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled)
            {
                continue;
            }

            if (!any) { bounds = r.bounds; any = true; } else { bounds.Encapsulate(r.bounds); }
        }

        return bounds;
    }

    /// <summary>
    /// Renders layer 31 with a transparent background. Orthographic: 'size' is the half height of the view.
    /// Perspective: 'size' is the radius that must fit.
    /// </summary>
    private static Texture2D Photograph(Vector3 target, Vector3 dir, float size, bool orthographic)
    {
        var camGo = new GameObject("IconCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << Layer;
        float distance;
        if (orthographic)
        {
            cam.orthographic = true;
            cam.orthographicSize = size;
            distance = size * 4f;
        }
        else
        {
            cam.fieldOfView = 20f;
            distance = size / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        cam.transform.position = target + dir * distance;
        cam.transform.LookAt(target);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = distance * 3f;

        // Key light from the camera side; the scene's sun comes from behind the models.
        var lightGo = new GameObject("IconLight") { hideFlags = HideFlags.HideAndDontSave };
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.75f;
        light.cullingMask = 1 << Layer;
        lightGo.transform.rotation = Quaternion.LookRotation(-(dir + Vector3.up * 0.8f + cam.transform.right * 0.4f).normalized);

        var rt = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var shot = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
        shot.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
        shot.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        return shot;
    }

    /// <summary>The rectangle of visible pixels (xMax / yMax exclusive).</summary>
    private static bool AlphaBox(Texture2D shot, out RectInt box)
    {
        Color32[] px = shot.GetPixels32();
        int w = shot.width, h = shot.height;
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a > 8)
                {
                    x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                    y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                }
            }
        }

        box = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        return x1 >= 0;
    }

    /// <summary>Scales the given part of the shot to fit the icon size, centred, with a small margin.</summary>
    private static Texture2D Fit(Texture2D shot, RectInt crop, int iconWidth, int iconHeight)
    {
        Color32[] px = shot.GetPixels32();
        int w = shot.width, h = shot.height;
        var result = new Texture2D(iconWidth, iconHeight, TextureFormat.RGBA32, false);
        result.SetPixels32(new Color32[iconWidth * iconHeight]);

        float scale = Mathf.Min((iconWidth - Margin * 2f) / crop.width, (iconHeight - Margin * 2f) / crop.height);
        int dw = Mathf.Max(1, Mathf.RoundToInt(crop.width * scale)), dh = Mathf.Max(1, Mathf.RoundToInt(crop.height * scale));
        int ox = (iconWidth - dw) / 2, oy = (iconHeight - dh) / 2;

        // Box-filter downscale (the render is several times larger than the icon), premultiplied so edges stay clean.
        for (int y = 0; y < dh; y++)
        {
            for (int x = 0; x < dw; x++)
            {
                float sx0 = crop.x + x / scale, sx1 = crop.x + (x + 1) / scale;
                float sy0 = crop.y + y / scale, sy1 = crop.y + (y + 1) / scale;
                float r = 0, g = 0, b = 0, a = 0;
                int n = 0;
                for (int sy = (int)sy0; sy < Mathf.Max((int)sy0 + 1, (int)sy1); sy++)
                {
                    for (int sx = (int)sx0; sx < Mathf.Max((int)sx0 + 1, (int)sx1); sx++)
                    {
                        Color32 c = px[Mathf.Clamp(sy, 0, h - 1) * w + Mathf.Clamp(sx, 0, w - 1)];
                        float ca = c.a / 255f;
                        r += c.r / 255f * ca; g += c.g / 255f * ca; b += c.b / 255f * ca; a += ca;
                        n++;
                    }
                }

                result.SetPixel(ox + x, oy + y, a > 0f ? new Color(r / a, g / a, b / a, a / n) : new Color(0f, 0f, 0f, 0f));
            }
        }

        result.Apply();
        return result;
    }
}
