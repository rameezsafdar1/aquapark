using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps outlined text from biting into itself. TextMeshPro draws every letter with its own outline, so in tight pairs
/// like "TA" the outline of the next letter covers the letter before it. Figma draws all strokes under all fills; this
/// does the same by drawing the letter faces once more on top, from the text's own mesh, with the outline made clear.
/// Add it to any TextMeshProUGUI whose material has an outline. The overlay is a hidden child that is never saved.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextFillOverlay : MonoBehaviour
{
    private const string FillName = "TMP Fill Overlay";

    private static readonly Dictionary<Material, Material> FillMaterials = new Dictionary<Material, Material>();
    private static readonly HashSet<TextMeshProUGUI> Pending = new HashSet<TextMeshProUGUI>();
    private static readonly List<TextMeshProUGUI> Flushing = new List<TextMeshProUGUI>();

    private TextMeshProUGUI source;
    private TextFillGraphic fill;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        FillMaterials.Clear();
        Pending.Clear();
    }

    /// <summary>
    /// In Play mode every outlined or shadowed text gets the overlay by itself the first time it is drawn, so scenes,
    /// prefabs and texts made at runtime are all covered without editing them.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        // Domain reload is off, so drop the subscriptions from the last Play session first.
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnAnyTextChanged);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnAnyTextChanged);
        Canvas.willRenderCanvases -= AttachPending;
        Canvas.willRenderCanvases += AttachPending;
    }

    public static bool Needs(Material material)
    {
        return material != null &&
               (material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) || material.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay));
    }

    private static void OnAnyTextChanged(Object changed)
    {
        if (Application.isPlaying && changed is TextMeshProUGUI text && Needs(text.fontSharedMaterial) && text.GetComponent<TextFillOverlay>() == null)
        {
            // This runs inside the canvas rebuild, where adding graphics is not allowed; attach just before the next one.
            Pending.Add(text);
        }
    }

    private static void AttachPending()
    {
        if (Pending.Count == 0)
        {
            return;
        }

        Flushing.AddRange(Pending);
        Pending.Clear();
        foreach (TextMeshProUGUI text in Flushing)
        {
            if (text != null && text.GetComponent<TextFillOverlay>() == null)
            {
                text.gameObject.AddComponent<TextFillOverlay>();
            }
        }

        Flushing.Clear();
    }

    /// <summary>The source material with the outline and shadow made clear, so only the letter faces are left.</summary>
    public static Material FillMaterialFor(Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            return null;
        }

        if (FillMaterials.TryGetValue(sourceMaterial, out Material cached) && cached != null)
        {
            return cached;
        }

        var material = new Material(sourceMaterial) { name = sourceMaterial.name + " (Fill)", hideFlags = HideFlags.HideAndDontSave };
        // Same dilate and outline width as the source, so the face edge lands exactly where it does there.
        if (material.HasProperty(ShaderUtilities.ID_OutlineColor))
        {
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.clear);
        }

        material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        material.DisableKeyword(ShaderUtilities.Keyword_Glow);
        if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            material.SetColor(ShaderUtilities.ID_UnderlayColor, Color.clear);
        }

        FillMaterials[sourceMaterial] = material;
        return material;
    }

    private void OnEnable()
    {
        source = GetComponent<TextMeshProUGUI>();
        EnsureFill();
        fill.gameObject.SetActive(true);
        fill.Bind(source);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        if (fill != null)
        {
            fill.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (fill != null)
        {
            if (Application.isPlaying)
            {
                Destroy(fill.gameObject);
            }
            else
            {
                DestroyImmediate(fill.gameObject);
            }
        }
    }

    private void LateUpdate()
    {
        if (fill != null)
        {
            fill.Follow();
        }
    }

    private void OnTextChanged(Object changed)
    {
        // Runs inside the canvas rebuild, so the mesh is handed straight to the renderer instead of marking anything dirty.
        if (changed == source && fill != null)
        {
            fill.CopyMesh();
        }
    }

    private void EnsureFill()
    {
        if (fill != null)
        {
            return;
        }

        // A script reload loses the reference but keeps the hidden child, so look for it first.
        Transform existing = transform.Find(FillName);
        if (existing != null)
        {
            fill = existing.GetComponent<TextFillGraphic>();
            if (fill != null)
            {
                return;
            }

            DestroyImmediate(existing.gameObject);
        }

        var go = new GameObject(FillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement));
        go.hideFlags = HideFlags.HideAndDontSave;
        go.layer = gameObject.layer;
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        fill = go.AddComponent<TextFillGraphic>();
        fill.raycastTarget = false;
    }
}
