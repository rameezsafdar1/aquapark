using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class AquaUIBuilder
{
    private const string FailedCardPath = WelcomeSourceDir + "/Purchase Failed Waterpark Splash UI.png";

    // The card picture's size; the texts below are placed in its pixels (top-left origin).
    private const float FailedW = 1149f, FailedH = 1368f;

    // Card width on the 390-wide design (a little margin on both sides).
    private const float FailedCardWidth = 374f;

    private static readonly Color FailedNavy = new Color32(0x0a, 0x2a, 0x6a, 255);

    /// <summary>
    /// The fail-safe offer after a failed purchase: gameplay video over the whole screen, the "Purchase Failed" card
    /// in the middle with its message, the weekly price on the plank and the small print in the box, plus the close
    /// button from the Welcome Back screen. Art: Assets/2D/Subs Screen/Purchase Failed Waterpark Splash UI.png.
    /// </summary>
    private static PurchaseFailedScreen BuildPurchaseFailed(Transform root)
    {
        MakePanelRoot(root, "PurchaseFailed", out RectTransform page);
        var screen = page.gameObject.AddComponent<PurchaseFailedScreen>();

        // Solid colour until the video's first frame; also stops taps reaching what is underneath.
        Image backdrop = SetImage(Container(page, "Backdrop"), null, true);
        backdrop.color = WelcomeNavy;

        RectTransform videoRect = Container(page, "Video");
        screen.video = videoRect.gameObject.AddComponent<RawImage>();
        screen.video.raycastTarget = false;
        screen.video.enabled = false;
        screen.clip = FindShowcaseClip();

        // Light shade over the video so the card stands out.
        Image shade = SetImage(Container(page, "Shade"), null, false);
        shade.color = new Color(WelcomeNavy.r, WelcomeNavy.g, WelcomeNavy.b, 0.35f);

        RectTransform card = Container(page, "Card");
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(FailedCardWidth, FailedCardWidth * FailedH / FailedW);
        card.anchoredPosition = Vector2.zero;
        Image cardImage = SetImage(card, AssetDatabase.LoadAssetAtPath<Sprite>(FailedCardPath), false);
        cardImage.preserveAspect = true;
        screen.popTarget = card;

        TextMeshProUGUI message = Lilita(card, "Message",
            "ALTHOUGH YOUR PREVIOUS ACQUISITION ATTEMPT WAS NOT A SUCCESS, WE ARE EXCITED TO BRING YOU A SIGNIFICANTLY MORE AFFORDABLE OPTION. " +
            "YOU CAN STILL ACCESS THE ENTIRE COLLECTION OF WATERPARK RIDES AND FUN ELEMENTS, NOW ENHANCED WITH INTEGRATED PROMOTIONAL FEATURES. " +
            "WE STRONGLY URGE YOU TO SEIZE THIS LIMITED-TIME OFFER AND SUBSCRIBE ON A WEEKLY BASIS AT AN INCREDIBLE RATE. " +
            "DON'T LET THIS UNMATCHED OPPORTUNITY SLIP THROUGH YOUR FINGERS!",
            11f, White, new Frame(0, 0, 10, 10), null, TextAlignmentOptions.Center, TextStyle.Outline(1.3f, 1f), true);
        message.textWrappingMode = TextWrappingModes.Normal;
        message.fontSizeMin = 6f;
        message.lineSpacing = -6f;
        message.characterSpacing = 1f;
        OnCard(message.rectTransform, 105f, 322f, 1044f, 505f);

        TextMeshProUGUI price = Lilita(card, "Price", "", 20f, White, new Frame(0, 0, 10, 10), null,
            TextAlignmentOptions.Center, TextStyle.Outline(2.5f, 1.5f), true);
        price.fontSizeMin = 10f;
        OnCard(price.rectTransform, 250f, 912f, 900f, 982f);
        screen.priceText = price;

        TextMeshProUGUI legal = Fredoka(card, "Legal", "", 8.4f, FailedNavy, new Frame(0, 0, 10, 10), null, TextAlignmentOptions.Center);
        legal.textWrappingMode = TextWrappingModes.Normal;
        legal.enableAutoSizing = true;
        legal.fontSizeMin = 6f;
        legal.fontSizeMax = 8.4f;
        legal.lineSpacing = -6f;
        OnCard(legal.rectTransform, 135f, 1005f, 1015f, 1310f);
        legal.raycastTarget = true;   // for the two links
        legal.gameObject.AddComponent<TextLinks>();
        screen.legalText = legal;
        screen.RefreshTexts();

        AddPurchaseFailedClose(screen);

        page.gameObject.SetActive(false);   // UIManager opens it; keep the scene showing the menu
        return screen;
    }

    /// <summary>The Welcome Back screen's close button (plain white X, top right), appearing closeDelay seconds after opening.</summary>
    public static Button AddPurchaseFailedClose(PurchaseFailedScreen screen)
    {
        screen.closeButton = MakeCloseX(screen.transform);
        EditorUtility.SetDirty(screen);
        return screen.closeButton;
    }

    [MenuItem("Aquapark/UI/Rebuild Purchase Failed Only")]
    public static void RebuildPurchaseFailedMenu()
    {
        Debug.Log(RebuildPurchaseFailed());
    }

    /// <summary>Replaces only the Purchase Failed panel in the open scene; every other panel is left exactly as it is.</summary>
    public static string RebuildPurchaseFailed()
    {
        LoadInfo();
        LoadFonts();
        GameObject canvas = GameObject.Find("Canvas");
        Transform root = canvas != null ? canvas.transform.Find("AquaUI") : null;
        UIManager ui = root != null ? root.GetComponent<UIManager>() : null;
        if (ui == null)
        {
            return "Open gameplay.unity first (needs Canvas/AquaUI with the UIManager).";
        }

        // Above the offers (a purchase can fail on them), below the toast and the rest that follows them.
        Transform old = root.Find("PurchaseFailed");
        Component after = ui.welcomeBack != null ? ui.welcomeBack : (Component)ui.vip;
        int index = old != null ? old.GetSiblingIndex() : after != null ? after.transform.GetSiblingIndex() + 1 : root.childCount;
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        PurchaseFailedScreen screen = BuildPurchaseFailed(root);
        screen.transform.SetSiblingIndex(index);
        ui.purchaseFailed = screen;
        EditorUtility.SetDirty(ui);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.scene);
        return "Purchase Failed panel rebuilt. Save the scene to keep it.";
    }

    /// <summary>Anchors a rect to a box in card-picture pixels (top-left origin), so it scales with the card.</summary>
    private static void OnCard(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0 / FailedW, 1f - y1 / FailedH);
        rt.anchorMax = new Vector2(x1 / FailedW, 1f - y0 / FailedH);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
