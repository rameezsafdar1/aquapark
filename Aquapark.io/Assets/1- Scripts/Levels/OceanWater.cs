using UnityEngine;

/// <summary>
/// The ocean around the finish island (tag "Ocean"). A player who lands on it sinks through, the camera stops following,
/// a splash plays here and the race is lost. Lives on the EndZone's ocean object so every level gets it.
/// </summary>
public class OceanWater : MonoBehaviour
{
    [Tooltip("Splash effect spawned where the player hits the water.")]
    public GameObject splashPrefab;
    [Tooltip("Size of the splash (the effect is made for small objects).")]
    public float splashScale = 3f;
    [Tooltip("Seconds before the spawned splash is removed.")]
    public float splashLifetime = 3f;

    public void Splash(Vector3 point)
    {
        if (splashPrefab == null)
        {
            return;
        }

        GameObject splash = Instantiate(splashPrefab, point, Quaternion.identity);
        splash.transform.localScale = Vector3.one * splashScale;
        Destroy(splash, splashLifetime);
    }
}
