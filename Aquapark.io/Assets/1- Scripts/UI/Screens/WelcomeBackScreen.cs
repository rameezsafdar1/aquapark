using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// The new VIP offer ("Welcome back! Drive 40 waterpark rides offer", art in Assets/2D/Subs Screen). A gameplay video
/// plays at the top and fades into the water picture with the characters; two options sit below, one ticked at a time,
/// then Continue. Shown once per app launch to returning players who are not VIP (see UIManager), in place of the older
/// VipOfferScreen, which is still built and opens with UIManager.Instance.OpenVip().
/// The option texts are placeholders and Continue only closes the screen until the IAP button is attached to it.
/// The small print at the bottom names each set's weekly price as the store reports it (PurchaseService.PriceLookup,
/// i.e. the localized Play Console price), with fallbackPrice until the store has answered.
/// </summary>
public class WelcomeBackScreen : UIPanel
{
    [System.Serializable]
    public class Option
    {
        public Button button;
        public GameObject check;
        public TMP_Text label;
    }

    [Header("Parts")]
    public Option[] options = new Option[2];
    public Button continueButton;
    [Tooltip("Turn off once the IAP button on Continue closes the screen after a purchase.")]
    public bool closeOnContinue = true;

    [Header("Close button")]
    public Button closeButton;
    [Tooltip("Seconds after opening before the close button appears (to be set from the remote config later).")]
    public float closeDelay = 3f;

    [Header("Store")]
    [Tooltip("Play Console product id of each option's subscription, top first.")]
    public string[] productIds = { "vip_skin_set_1", "vip_skin_set_2" };
    [Tooltip("Shown until the store reports the real (localized) price.")]
    public string fallbackPrice = "INR 819";

    [Header("Small print")]
    public TMP_Text legalText;
    [Tooltip("{0} = Set 1's price, {1} = Set 2's price. Keep the <link> tags so the two links stay tappable.")]
    [TextArea(4, 10)]
    public string legalTemplate =
        "Choose Set 1 or Set 2 and enjoy a 3-day trial with 20 unique rides per each set, unlimited passes, bonus currency and no ads! " +
        "After the trial, Set 1 renews at {0}/week and Set 2 at {1}/week unless cancelled. Cancel anytime via Google Play Subscriptions. " +
        "A subscription is not required to use the app.\n" +
        "Refer to our <link=\"terms\"><u><color=#FFE45C>Terms & Conditions</color></u></link> and " +
        "<link=\"subscription\"><u><color=#FFE45C>Subscription Policy</color></u></link>.";
    public string termsUrl = "";
    public string subscriptionPolicyUrl = "";

    [Header("Layout")]
    [Tooltip("The water picture (bottom of the screen). The video fills the screen above it, down to opaqueFrom.")]
    public RectTransform water;
    [Tooltip("Where the water picture is fully opaque, as a share of its height from the top (its top part fades out).")]
    [Range(0f, 1f)] public float opaqueFrom = 0.355f;
    [Tooltip("Share of the screen (from the top) the video should fill; the water and buttons move up to meet it.")]
    [Range(0.2f, 0.8f)] public float videoShare = 0.5f;
    [Tooltip("Room kept under the water for the small print, in canvas units.")]
    public float legalSpace = 92f;

    [Header("Background video (top of the screen)")]
    public VideoClip clip;
    public RawImage video;
    [Tooltip("Zoom into the clip (1 = just cover the video area).")]
    [Range(1f, 2f)] public float videoZoom = 1f;
    [Tooltip("Which part of the clip stays on screen when cropped: 0 = bottom, 1 = top.")]
    [Range(0f, 1f)] public float videoFocusY = 0.35f;

    private int selected;
    private VideoPlayer player;
    private RenderTexture videoTexture;
    private float priceTimer;
    private float closeTimer;

    /// <summary>Shown this app launch already (statics survive Play mode without domain reload, so reset on load).</summary>
    private static bool shownThisLaunch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        shownThisLaunch = false;
    }

    /// <summary>Returning players (at least one level done) who are not VIP see the offer once per launch.</summary>
    public static bool ShouldShowOnLaunch => !shownThisLaunch && !SaveData.IsVip && SaveData.Level > 1;

    /// <summary>The ticked option (0 = top).</summary>
    public int Selected => selected;

    private void Awake()
    {
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            options[i].button.onClick.AddListener(() => Select(index));
        }

        continueButton.onClick.AddListener(OnContinue);
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => UIManager.Instance.CloseWelcomeBack());
        }

        if (legalText != null && legalText.TryGetComponent(out TextLinks links))
        {
            links.Clicked += id => OpenLink(id == "terms" ? termsUrl : subscriptionPolicyUrl);
        }
    }

    protected override void OnShown()
    {
        shownThisLaunch = true;
        closeTimer = closeDelay;
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(closeDelay <= 0f);
        }

        Select(selected);
        RefreshLegal();
        Layout();
        PlayVideo();
    }

    private void Update()
    {
        if (closeButton != null && !closeButton.gameObject.activeSelf)
        {
            closeTimer -= Time.unscaledDeltaTime;
            if (closeTimer <= 0f)
            {
                closeButton.gameObject.SetActive(true);
            }
        }

        // The store may answer after the screen opened: keep the prices current.
        priceTimer -= Time.unscaledDeltaTime;
        if (priceTimer <= 0f)
        {
            priceTimer = 1f;
            RefreshLegal();
        }
    }

    /// <summary>Writes the small print with each set's current store price.</summary>
    public void RefreshLegal()
    {
        if (legalText == null)
        {
            return;
        }

        string text = string.Format(legalTemplate, Price(0), Price(1));
        if (legalText.text != text)
        {
            legalText.text = text;
        }
    }

    private string Price(int index)
    {
        return index < productIds.Length ? PurchaseService.GetPrice(productIds[index], fallbackPrice) : fallbackPrice;
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

    public void Select(int index)
    {
        selected = Mathf.Clamp(index, 0, options.Length - 1);
        for (int i = 0; i < options.Length; i++)
        {
            options[i].check.SetActive(i == selected);
        }
    }

    private void OnContinue()
    {
        if (closeOnContinue)
        {
            UIManager.Instance.CloseWelcomeBack();
        }
    }

    /// <summary>
    /// Lifts the water (with the title, options and Continue on it) so its opaque part starts at videoShare of the
    /// screen, but never lower than legalSpace above the bottom, then stretches the video down to meet it.
    /// </summary>
    public void Layout()
    {
        if (video == null || water == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float pageHeight = ((RectTransform)transform).rect.height;
        float below = water.rect.height * (1f - opaqueFrom);
        float lift = Mathf.Max(legalSpace, pageHeight * (1f - videoShare) - below);
        water.anchoredPosition = new Vector2(water.anchoredPosition.x, lift);

        float height = pageHeight - lift - below;
        RectTransform rt = video.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, Mathf.Max(0f, height));
    }

    private void PlayVideo()
    {
        if (video == null)
        {
            return;
        }

        video.enabled = false;   // until the first frame is ready (an empty RawImage draws white)
        if (clip == null)
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

        if (videoTexture == null || videoTexture.width != (int)clip.width || videoTexture.height != (int)clip.height)
        {
            if (videoTexture != null)
            {
                videoTexture.Release();
            }

            videoTexture = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32);
        }

        player.clip = clip;
        player.targetTexture = videoTexture;
        video.texture = videoTexture;
        video.uvRect = CropFor(clip, video.rectTransform.rect.size);
        player.Prepare();
    }

    /// <summary>The part of the clip that covers a box of this size without stretching, zoomed and placed by the focus.</summary>
    private Rect CropFor(VideoClip source, Vector2 box)
    {
        float clipAspect = (float)source.width / Mathf.Max(1f, source.height);
        float boxAspect = box.x / Mathf.Max(1f, box.y);
        float w = 1f, h = 1f;
        if (clipAspect < boxAspect)
        {
            h = clipAspect / boxAspect;   // clip taller than the box: lose some top/bottom
        }
        else
        {
            w = boxAspect / clipAspect;   // clip wider: lose some sides
        }

        float zoom = Mathf.Max(1f, videoZoom);
        w /= zoom;
        h /= zoom;
        return new Rect(0.5f - w * 0.5f, Mathf.Clamp(videoFocusY - h * 0.5f, 0f, 1f - h), w, h);
    }

    private void OnVideoReady(VideoPlayer source)
    {
        if (!IsOpen)
        {
            return;
        }

        source.Play();
        video.enabled = true;
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
    }
}
