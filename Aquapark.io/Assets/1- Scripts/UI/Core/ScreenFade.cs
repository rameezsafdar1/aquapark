using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen black fade drawn above everything. Created on first use, so no scene setup is needed.
/// Usage: yield return ScreenFade.Out(0.35f); ...swap things...; yield return ScreenFade.In(0.35f);
/// </summary>
public class ScreenFade : MonoBehaviour
{
    private static ScreenFade instance;
    private CanvasGroup group;

    private static ScreenFade Get()
    {
        if (instance != null)
        {
            return instance;
        }

        var go = new GameObject("Screen Fade");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        go.AddComponent<GraphicRaycaster>();

        var imageGo = new GameObject("Black");
        imageGo.transform.SetParent(go.transform, false);
        var image = imageGo.AddComponent<Image>();
        image.color = Color.black;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        instance = go.AddComponent<ScreenFade>();
        instance.group = go.AddComponent<CanvasGroup>();
        instance.group.alpha = 0f;
        instance.group.blocksRaycasts = false;
        return instance;
    }

    /// <summary>Fades the screen to black.</summary>
    public static IEnumerator Out(float seconds) => Get().Run(1f, seconds);

    /// <summary>Fades the black away.</summary>
    public static IEnumerator In(float seconds) => Get().Run(0f, seconds);

    private IEnumerator Run(float target, float seconds)
    {
        float from = group.alpha;
        group.blocksRaycasts = true;   // no stray taps while the screen is changing
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, target, t / seconds);
            yield return null;
        }

        group.alpha = target;
        group.blocksRaycasts = target > 0.5f;
    }
}
