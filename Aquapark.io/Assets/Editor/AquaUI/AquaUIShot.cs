using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders the canvas to a PNG without entering Play mode, to compare the UI with the Figma screens.</summary>
public static class AquaUIShot
{
    /// <summary>
    /// Saves a screenshot at 1170 x 2532 (the 3x Figma export size). 'show' and 'hide' are object paths below the canvas,
    /// for example "AquaUI/Menu/Shop". Visibility is restored afterwards.
    /// </summary>
    public static string Capture(string path, string[] show = null, string[] hide = null, int width = 1170, int height = 2532)
    {
        var canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            return "No canvas";
        }

        var canvas = canvasGo.GetComponent<Canvas>();
        var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;

        var camGo = new GameObject("ShotCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f, 1f);
        cam.orthographic = true;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        cam.cullingMask = 1 << LayerMask.NameToLayer("UI");

        var restore = new System.Collections.Generic.List<(GameObject, bool)>();
        void Set(string[] paths, bool active)
        {
            if (paths == null) return;
            foreach (string p in paths)
            {
                Transform t = canvasGo.transform.Find(p);
                if (t == null)
                {
                    // Inactive objects are only found by walking the path.
                    t = FindDeep(canvasGo.transform, p);
                }

                if (t == null) { Debug.LogWarning("AquaUIShot: not found " + p); continue; }
                restore.Add((t.gameObject, t.gameObject.activeSelf));
                t.gameObject.SetActive(active);
                // Panels fade in with a CanvasGroup: show them fully.
                var group = t.GetComponent<CanvasGroup>();
                if (group != null && active) group.alpha = 1f;
            }
        }

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        try
        {
            Set(show, true);
            Set(hide, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return "saved " + path;
        }
        finally
        {
            RenderTexture.active = null;
            cam.targetTexture = null;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);
            for (int i = restore.Count - 1; i >= 0; i--)
            {
                restore[i].Item1.SetActive(restore[i].Item2);
            }
        }
    }

    private static Transform FindDeep(Transform root, string path)
    {
        Transform current = root;
        foreach (string part in path.Split('/'))
        {
            Transform next = null;
            foreach (Transform child in current)
            {
                if (child.name == part)
                {
                    next = child;
                    break;
                }
            }

            if (next == null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }
}
