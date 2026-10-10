#if UNITY_STANDALONE
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PC builds only: a small drop down in the top-left corner of the menu that resizes the game window to one of
/// several 9:16 portrait sizes. Spawns itself (no scene object), survives scene reloads and remembers the choice.
/// Screen.SetResolution does nothing in the Editor's Game view, so the list only shows there.
/// </summary>
public class WindowSizeMenu : MonoBehaviour
{
    private const string PrefKey = "WindowSizeHeight";
    // Heights of the 9:16 options; widths are height * 9 / 16.
    private static readonly int[] Heights = { 640, 720, 800, 960, 1080, 1280, 1440, 1600, 1920 };
    // Room left for the title bar and task bar when deciding which sizes fit the monitor.
    private const int ScreenMargin = 90;

    private static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.32f, 0.85f);
    private static readonly Color ItemColor = new Color(0.16f, 0.20f, 0.48f, 1f);
    private static readonly Color SelectedColor = new Color(0.20f, 0.55f, 0.95f, 1f);

    private GameObject canvasRoot;
    private GameObject list;
    private TextMeshProUGUI label;
    private RectTransform arrow;
    private readonly List<(int height, Image image)> items = new List<(int, Image)>();
    private TMP_FontAsset font;
    private Sprite rounded;
    private UIManager panelsOwner;
    private UIPanel[] panels = new UIPanel[0];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        var go = new GameObject("Window Size Menu");
        DontDestroyOnLoad(go);
        go.AddComponent<WindowSizeMenu>();
    }

    private void Awake()
    {
        ApplySaved();
        font = FindFont();
        rounded = MakeRoundedSprite();
        Build();
    }

    private void Update()
    {
        // Home screen only, so it never sits on top of the race HUD, results, offers or popups.
        bool show = UIManager.Instance == null || (UIManager.Instance.CurrentState == UIManager.State.Menu && !AnyPanelOpen());
        if (canvasRoot.activeSelf != show)
        {
            canvasRoot.SetActive(show);
            if (!show)
            {
                SetOpen(false);
            }
        }

        if (show && list.activeSelf && Input.GetMouseButtonDown(0) && !IsPointerOver(list) && !IsPointerOver(label.transform.parent.gameObject))
        {
            SetOpen(false);
        }
    }

    private bool AnyPanelOpen()
    {
        // Re-collect the panels whenever the scene (and so the UIManager) is reloaded.
        if (panelsOwner != UIManager.Instance)
        {
            panelsOwner = UIManager.Instance;
            panels = FindObjectsByType<UIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        foreach (UIPanel panel in panels)
        {
            if (panel != null && panel.IsOpen)
            {
                return true;
            }
        }
        return false;
    }

    // ---------- Sizes ----------

    private static int Width(int height) => Mathf.RoundToInt(height * 9f / 16f);

    private static List<int> FittingHeights()
    {
        int maxHeight = Screen.currentResolution.height - ScreenMargin;
        var fitting = new List<int>();
        foreach (int h in Heights)
        {
            if (h <= maxHeight)
            {
                fitting.Add(h);
            }
        }
        if (fitting.Count == 0)
        {
            fitting.Add(Heights[0]);
        }
        return fitting;
    }

    private static void ApplySaved()
    {
        int saved = PlayerPrefs.GetInt(PrefKey, 0);
        if (saved > 0 && FittingHeights().Contains(saved) && Screen.height != saved)
        {
            Screen.SetResolution(Width(saved), saved, FullScreenMode.Windowed);
        }
    }

    private void Choose(int height)
    {
        PlayerPrefs.SetInt(PrefKey, height);
        PlayerPrefs.Save();
        Screen.SetResolution(Width(height), height, FullScreenMode.Windowed);
        label.text = SizeText(height);
        RefreshSelection(height);
        SetOpen(false);
    }

    private static string SizeText(int height) => Width(height) + " x " + height;

    private static int CurrentHeight()
    {
        int saved = PlayerPrefs.GetInt(PrefKey, 0);
        return saved > 0 ? saved : Screen.height;
    }

    // ---------- UI ----------

    private void Build()
    {
        canvasRoot = new GameObject("Canvas", typeof(RectTransform));
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390, 844);
        scaler.matchWidthOrHeight = 1f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        // Header button (top-left, in the free sky left of the logo).
        var header = MakeBox("Header", canvasRoot.transform, PanelColor);
        Place(header, new Vector2(0, 1), new Vector2(10, -78), new Vector2(104, 28));
        header.gameObject.AddComponent<Button>().onClick.AddListener(() => SetOpen(!list.activeSelf));

        label = MakeText("Label", header, SizeText(CurrentHeight()), 13);
        label.rectTransform.offsetMin = new Vector2(8, 0);
        label.rectTransform.offsetMax = new Vector2(-22, 0);

        arrow = MakeChevron(header);

        // Drop-down list under the header.
        List<int> heights = FittingHeights();
        const float itemHeight = 26f, pad = 4f;
        var listRect = MakeBox("List", canvasRoot.transform, PanelColor);
        Place(listRect, new Vector2(0, 1), new Vector2(10, -110), new Vector2(104, heights.Count * (itemHeight + 2) + pad * 2 - 2));
        list = listRect.gameObject;

        for (int i = 0; i < heights.Count; i++)
        {
            int h = heights[i];
            var item = MakeBox(h.ToString(), listRect, ItemColor);
            Place(item, new Vector2(0, 1), new Vector2(pad, -pad - i * (itemHeight + 2)), new Vector2(104 - pad * 2, itemHeight));
            item.gameObject.AddComponent<Button>().onClick.AddListener(() => Choose(h));
            var text = MakeText("Label", item, SizeText(h), 12);
            text.alignment = TextAlignmentOptions.Center;
            items.Add((h, item.GetComponent<Image>()));
        }

        RefreshSelection(CurrentHeight());
        SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        list.SetActive(open);
        arrow.localRotation = Quaternion.Euler(0, 0, open ? 180 : 0);
    }

    private void RefreshSelection(int height)
    {
        foreach (var (h, image) in items)
        {
            image.color = h == height ? SelectedColor : ItemColor;
        }
    }

    private RectTransform MakeBox(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        return (RectTransform)go.transform;
    }

    private TextMeshProUGUI MakeText(string name, RectTransform parent, string text, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            tmp.font = font;
        }
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    /// <summary>Down-pointing chevron from two thin bars (the fonts have no arrow glyph).</summary>
    private RectTransform MakeChevron(RectTransform parent)
    {
        var holder = new GameObject("Arrow", typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        var rect = (RectTransform)holder.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-13, 0);
        rect.sizeDelta = new Vector2(12, 12);

        for (int side = -1; side <= 1; side += 2)
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(rect, false);
            var barRect = (RectTransform)bar.transform;
            barRect.sizeDelta = new Vector2(7, 2);
            barRect.anchoredPosition = new Vector2(side * 2.2f, 0);
            barRect.localRotation = Quaternion.Euler(0, 0, side * 45);
            var image = bar.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }
        return rect;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static bool IsPointerOver(GameObject go)
    {
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)go.transform, Input.mousePosition, null);
    }

    private static TMP_FontAsset FindFont()
    {
        foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (asset.name.Contains("Fredoka"))
            {
                return asset;
            }
        }
        return TMP_Settings.defaultFontAsset;
    }

    /// <summary>White rounded square, 9-sliced, so the boxes are not plain rectangles.</summary>
    private static Sprite MakeRoundedSprite()
    {
        const int size = 32, radius = 10;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
#endif
