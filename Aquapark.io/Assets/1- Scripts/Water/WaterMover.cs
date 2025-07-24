using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class WaterMover : MonoBehaviour
{
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.1f, 0f);

    private Renderer rend;
    private MaterialPropertyBlock propertyBlock;
    private Vector2 currentOffset;
    [SerializeField] private Vector2 tiling;
    private static readonly int MainTexProp = Shader.PropertyToID("_MainTex_ST");

    void Awake()
    {
        rend = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        currentOffset += scrollSpeed * Time.deltaTime;
        rend.GetPropertyBlock(propertyBlock);
        Vector4 st = new Vector4(tiling.x, tiling.y, currentOffset.x, currentOffset.y);
        propertyBlock.SetVector(MainTexProp, st);

        rend.SetPropertyBlock(propertyBlock);
    }
}
