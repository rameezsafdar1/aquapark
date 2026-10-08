using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the letter faces of a TextMeshProUGUI on top of it. Created and driven by <see cref="TextFillOverlay"/>.</summary>
[ExecuteAlways]
public class TextFillGraphic : MaskableGraphic
{
    private TextMeshProUGUI source;
    private Material sourceMaterial;
    private Material fillMaterial;

    public override Texture mainTexture
    {
        get
        {
            if (fillMaterial != null && fillMaterial.HasProperty(ShaderUtilities.ID_MainTex))
            {
                return fillMaterial.GetTexture(ShaderUtilities.ID_MainTex);
            }

            return base.mainTexture;
        }
    }

    public override Material material
    {
        get { return fillMaterial != null ? fillMaterial : base.material; }
        set { base.material = value; }
    }

    public void Bind(TextMeshProUGUI text)
    {
        source = text;
        Follow();
        SetAllDirty();
    }

    /// <summary>Picks up material, pivot and fade changes of the source; called every frame from outside the canvas rebuild.</summary>
    public void Follow()
    {
        if (source == null)
        {
            return;
        }

        Material shared = source.fontSharedMaterial;
        if (shared != sourceMaterial)
        {
            sourceMaterial = shared;
            fillMaterial = TextFillOverlay.FillMaterialFor(shared);
            SetMaterialDirty();
        }

        if (rectTransform.pivot != source.rectTransform.pivot)
        {
            rectTransform.pivot = source.rectTransform.pivot;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            CopyMesh();
        }

        // CrossFadeColor / CrossFadeAlpha act on the source's renderer only, and a disabled source must not leave faces behind.
        Color tint = source.enabled ? source.canvasRenderer.GetColor() : Color.clear;
        if (canvasRenderer.GetColor() != tint)
        {
            canvasRenderer.SetColor(tint);
        }
    }

    /// <summary>Hands the source's current text mesh to this renderer.</summary>
    public void CopyMesh()
    {
        if (source != null && source.mesh != null)
        {
            canvasRenderer.SetMesh(source.mesh);
        }
    }

    protected override void UpdateGeometry()
    {
        CopyMesh();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
}
