using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// The VIP subscription offer (Figma '09 · VIP Subscription Offer'): pick Skin Set 1 or 2, then Continue starts the
/// weekly subscription with a 3-day free trial through <see cref="PurchaseService"/>. Shown once per app launch to
/// returning players who are not VIP yet (see UIManager), and from code with UIManager.Instance.OpenVip().
/// Granting the perks (VIP skins, daily gems, x2 coins) is not hooked up yet; buying sets SaveData.VipPlan and removes ads.
/// </summary>
public class VipOfferScreen : UIPanel
{
    [System.Serializable]
    public class Plan
    {
        public Button button;
        public Image bg;
        public Image radio;
        public GameObject check;
        public TMP_Text priceText;
    }

    [Header("Store")]
    public string[] productIds = { "vip_skin_set_1", "vip_skin_set_2" };
    [Tooltip("Shown until the store reports the real price.")]
    public string fallbackPrice = "INR 819";

    [Header("Links (opened in the browser)")]
    public string termsUrl = "";
    public string privacyUrl = "";
    public string subscriptionPolicyUrl = "";

    [Header("Parts")]
    public Plan[] plans = new Plan[2];
    public Button closeButton;
    public Button restoreButton;
    public Button continueButton;
    public Button termsButton;
    public Button privacyButton;
    public Button subscriptionButton;
    public TMP_Text legalText;

    [Header("Plan card looks")]
    public Sprite cardOn;
    public Sprite cardOff;
    public Sprite radioOn;
    public Sprite radioOff;
    [Tooltip("Shadow / glow margin around the card picture, in design px, for each look.")]
    public float cardOnPad;
    public float cardOffPad;
    public Vector2 cardSize = new Vector2(354f, 74f);

    [Header("Headline: UNLOCK <count> VIP SKINS")]
    [Tooltip("Shows the number of characters in the game (UIManager's skin database).")]
    public TMP_Text skinCountText;
    public TMP_Text unlockText;
    public TMP_Text vipSkinsText;

    [Header("Background video (replaces the sunburst and the Figma placeholder characters)")]
    [Tooltip("Plays in a loop behind the whole screen. Leave empty for the Figma background and characters. " +
             "The builder picks the first clip in Assets/UI/Vip or Assets/Videos.")]
    public VideoClip showcaseClip;
    public RawImage showcaseVideo;
    [Tooltip("Full-screen video + its readability shade; shown once the first frame is ready.")]
    public GameObject videoCard;
    [Tooltip("The Figma placeholder characters, shown only when there is no clip.")]
    public GameObject showcaseArt;
    [Tooltip("Zoom into the clip (1 = just cover the screen). The default pushes the recording's race HUD off the top.")]
    [Range(1f, 2f)] public float videoZoom = 1.25f;
    [Tooltip("Which part of the clip stays on screen when zoomed or cropped: 0 = bottom, 1 = top.")]
    [Range(0f, 1f)] public float videoFocusY = 0f;
    [Tooltip("Same across: 0 = left, 1 = right.")]
    [Range(0f, 1f)] public float videoFocusX = 0.5f;
    [Tooltip("Key out a green-screen background (for clips without transparency).")]
    public bool chromaKey;
    public Material chromaKeyMaterial;
    public Color keyColor = new Color(0f, 1f, 0f, 1f);
    [Range(0f, 1f)] public float keyThreshold = 0.35f;
    [Range(0.001f, 0.5f)] public float keySmoothness = 0.08f;

    private int selected;
    private VideoPlayer player;
    private RenderTexture videoTexture;
    private Material keyMaterial;

    /// <summary>Shown this app launch already (statics survive Play mode without domain reload, so reset on load).</summary>
    private static bool shownThisLaunch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        shownThisLaunch = false;
    }

    /// <summary>Returning players (at least one level done) who are not VIP see the offer once per launch.</summary>
    public static bool ShouldShowOnLaunch => !shownThisLaunch && !SaveData.IsVip && SaveData.Level > 1;

    private void Awake()
    {
        for (int i = 0; i < plans.Length; i++)
        {
            int index = i;
            plans[i].button.onClick.AddListener(() => Select(index));
        }

        closeButton.onClick.AddListener(() => UIManager.Instance.CloseVip());
        restoreButton.onClick.AddListener(PurchaseService.Restore);
        continueButton.onClick.AddListener(Buy);
        termsButton.onClick.AddListener(() => OpenLink(termsUrl));
        privacyButton.onClick.AddListener(() => OpenLink(privacyUrl));
        subscriptionButton.onClick.AddListener(() => OpenLink(subscriptionPolicyUrl));
    }

    protected override void OnShown()
    {
        shownThisLaunch = true;
        Select(Mathf.Clamp(SaveData.VipPlan - 1, 0, plans.Length - 1));
        LayoutHeadline();
        PlayShowcase();
    }

    /// <summary>Writes the real character count and re-centres UNLOCK / number / VIP SKINS around it.</summary>
    public void LayoutHeadline(SkinDatabase db = null)
    {
        if (skinCountText == null)
        {
            return;
        }

        if (db == null && UIManager.Instance != null)
        {
            db = UIManager.Instance.Skins;
        }

        if (db != null && db.skins != null)
        {
            skinCountText.text = db.skins.Length.ToString();
        }

        // UNLOCK, the number and VIP SKINS on one line, 7 px apart (as in the design), centred on the screen.
        const float gap = 7f, centre = 195.5f - 195f;
        float unlockW = Width(unlockText), numberW = Width(skinCountText), vipW = Width(vipSkinsText);
        float left = centre - (unlockW + gap + numberW + gap + vipW) * 0.5f;
        Place(unlockText, left, unlockW);
        Place(skinCountText, left + unlockW + gap, numberW);
        Place(vipSkinsText, left + unlockW + gap + numberW + gap, vipW);
    }

    private static float Width(TMP_Text text)
    {
        return text != null ? text.GetPreferredValues(text.text).x : 0f;
    }

    /// <summary>Puts a text box of exactly its own width with its left edge at x (relative to the screen centre).</summary>
    private static void Place(TMP_Text text, float x, float width)
    {
        if (text == null)
        {
            return;
        }

        RectTransform rt = text.rectTransform;
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
        SetX(rt, x + width * 0.5f);
    }

    private static void SetX(RectTransform rt, float x)
    {
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        }
    }

    private void PlayShowcase()
    {
        bool hasClip = showcaseClip != null && showcaseVideo != null;
        if (showcaseArt != null)
        {
            showcaseArt.SetActive(!hasClip);   // the Figma characters only when there is no video
        }

        if (videoCard != null)
        {
            videoCard.SetActive(false);
        }

        if (!hasClip)
        {
            return;
        }

        if (player == null)
        {
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.prepareCompleted += OnVideoReady;
        }

        if (videoTexture == null || videoTexture.width != (int)showcaseClip.width || videoTexture.height != (int)showcaseClip.height)
        {
            if (videoTexture != null)
            {
                videoTexture.Release();
            }

            videoTexture = new RenderTexture((int)showcaseClip.width, (int)showcaseClip.height, 0, RenderTextureFormat.ARGB32);
        }

        player.clip = showcaseClip;
        player.targetTexture = videoTexture;
        showcaseVideo.texture = videoTexture;

        showcaseVideo.uvRect = CropFor(showcaseClip, showcaseVideo.rectTransform.rect.size);

        if (chromaKey && chromaKeyMaterial != null)
        {
            if (keyMaterial == null)
            {
                keyMaterial = new Material(chromaKeyMaterial);
            }

            keyMaterial.SetColor("_KeyColor", keyColor);
            keyMaterial.SetFloat("_Threshold", keyThreshold);
            keyMaterial.SetFloat("_Smoothness", keySmoothness);
            showcaseVideo.material = keyMaterial;
        }
        else
        {
            showcaseVideo.material = null;
        }

        player.Prepare();
    }

    /// <summary>The part of the clip that covers a box of this size without stretching, zoomed and placed by the focus.</summary>
    public Rect CropFor(VideoClip clip, Vector2 box)
    {
        float clipAspect = (float)clip.width / Mathf.Max(1f, clip.height);
        float boxAspect = box.x / Mathf.Max(1f, box.y);
        float w = 1f, h = 1f;
        if (clipAspect < boxAspect)
        {
            h = clipAspect / boxAspect;   // clip taller than the screen: lose some top/bottom
        }
        else
        {
            w = boxAspect / clipAspect;   // clip wider: lose some sides
        }

        float zoom = Mathf.Max(1f, videoZoom);
        w /= zoom;
        h /= zoom;
        return new Rect(Mathf.Clamp(videoFocusX - w * 0.5f, 0f, 1f - w), Mathf.Clamp(videoFocusY - h * 0.5f, 0f, 1f - h), w, h);
    }

    private void OnVideoReady(VideoPlayer source)
    {
        if (!IsOpen)
        {
            return;
        }

        source.Play();
        if (videoCard != null)
        {
            videoCard.SetActive(true);
        }

        if (showcaseArt != null)
        {
            showcaseArt.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.Stop();
        }
    }

    private void OnDestroy()
    {
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }

        if (keyMaterial != null)
        {
            Destroy(keyMaterial);
        }
    }

    public void Select(int index)
    {
        selected = index;

        // Draw the selected card first so the other card covers its glow (as in the design).
        int first = int.MaxValue;
        foreach (Plan plan in plans)
        {
            first = Mathf.Min(first, plan.button.transform.GetSiblingIndex());
        }

        plans[index].button.transform.SetSiblingIndex(first);

        for (int i = 0; i < plans.Length; i++)
        {
            bool on = i == index;
            Plan plan = plans[i];
            plan.bg.sprite = on ? cardOn : cardOff;
            float pad = on ? cardOnPad : cardOffPad;
            plan.bg.rectTransform.sizeDelta = cardSize + Vector2.one * (pad * 2f);
            plan.radio.sprite = on ? radioOn : radioOff;
            plan.check.SetActive(on);
        }

        RefreshPrices();   // the small print names the selected set's price
    }

    private string Price(int index)
    {
        return PurchaseService.GetPrice(productIds[index], fallbackPrice);
    }

    private void RefreshPrices()
    {
        for (int i = 0; i < plans.Length; i++)
        {
            plans[i].priceText.text = "then " + Price(i) + "/week";
        }

        legalText.text = "Choose Skin Set 1 or Skin Set 2 for a 3-day free trial: 20 VIP skins per set, no ads, daily gems and x2 coins. " +
                         "After the trial, the selected set renews at " + Price(selected) + "/week unless cancelled at least 24h before renewal. " +
                         "Manage anytime in Google Play Subscriptions. Not required to play.";
    }

    private void Buy()
    {
        int plan = selected + 1;
        PurchaseService.Purchase(productIds[selected], () =>
        {
            SaveData.VipPlan = plan;
            SaveData.NoAds = true;
            UIManager.Instance.CloseVip();
            UIManager.Instance.ShowToast("Welcome to VIP! Skin Set " + plan + " unlocked");
        });
    }

    private static void OpenLink(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            UIManager.Instance.ShowToast("Link not set yet");
            return;
        }

        Application.OpenURL(url);
    }
}
