using Unity.VisualScripting;
using UnityEngine;

public class MaterialApplier : MonoBehaviour
{

    [SerializeField] private Material slideMat;


    [ContextMenu("Paint All Slides")]
    private void PaintAllSlides()
    {
        ApplyMaterialRecursive(transform);
    }

    private void ApplyMaterialRecursive(Transform parent)
    {
        Renderer rend = parent.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = slideMat;  
        }

        foreach (Transform child in parent)
        {
            ApplyMaterialRecursive(child);
        }
    }

    [ContextMenu("Add colliders")]
    private void AddColliders()
    {
        ApplyCollidersRecursive(transform);
    }

    private void ApplyCollidersRecursive(Transform parent)
    {
        Renderer rend = parent.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.AddComponent<MeshCollider>();
        }

        foreach (Transform child in parent)
        {
            ApplyCollidersRecursive(child);
        }
    }
}
