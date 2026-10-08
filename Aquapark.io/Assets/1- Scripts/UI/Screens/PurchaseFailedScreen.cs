using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Fail-safe offer shown after a purchase did not go through ("Purchase Failed" card, art in Assets/2D/Subs Screen):
/// a gameplay video fills the screen behind the card, the card offers the weekly subscription, and the close button
/// (as on the Welcome Back screen) appears closeDelay seconds after opening. Open it with UIManager.Instance.OpenPurchaseFailed().
/// Prices are the store's localized price (PurchaseService.PriceLookup, i.e. the Play Console price), with fallbackPrice
/// until the store has answered. Nothing is bought from here yet; the IAP package is hooked up later.
/// </summary>
public class PurchaseFailedScreen : UIPanel
{
    [Header("Close button")]
    public Button closeButton;
    [Tooltip("Seconds after opening before the close button appears (to be set from the remote config later).")]
    public float closeDelay = 3f;

    [Header("Store")]
    [Tooltip("Play Console product id of the weekly subscription offered here.")]
    public string productId = "vip_weekly";
    [Tooltip("Shown until the store reports the real (localized) price.")]
    public string fallbackPrice = "INR 819";

    [Header("Texts ({0} = the store price)")]
    public TMP_Text priceText;
    public string priceTemplate = "WEEKLY <color=#FFE45C>{0}/</color> RECURRING";
    public TMP_Text legalText;
    [Tooltip("{0} = the store price. Keep the <link> tags so the two links stay tappable.")]
    [TextArea(4, 10)]
    public string legalTemplate =
        "Get an amazing 3 day trial subscription with the entire game unlocked during the trial period without any charges - " +
        "try out all waterpark rides, unlimited passes, bonus currency and no ads! If you choose not to cancel within the 3 day trial period, " +
        "your trial subscription will convert to a paid subscription for {0} week/recurring and will be charged via Google Play Store. " +
        "You may also cancel anytime during your billing cycle via the Google Play Store subscription tab. " +
        "Please note that a subscription is not necessary to use the app.\n" +
        "Refer to our <link=\"terms\"><u><color=#1E6FD9>Terms & Conditions</color></u></link> and " +
        "<link=\"subscription\"><u><color=#1E6FD9>Subscription Policy</color></u></link>.";
    public string termsUrl = "";
    public string subscriptionPolicyUrl = "";

    [Header("Background video (whole screen)")]
    public VideoClip clip;
    public RawImage video;
    [Tooltip("Zoom into the clip (1 = just cover the screen). The default pushes the recording's race HUD off the top.")]
    [Range(1f, 2f)] public float videoZoom = 1.25f;
    [Tooltip("Which part of the clip stays on screen when cropped: 0 = bottom, 1 = top.")]
    [Range(0f, 1f)] public float videoFocusY = 0f;

    private VideoPlayer player;
    private RenderTexture videoTexture;
    private float priceTimer;
    private float closeTimer;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => UIManager.Instance.ClosePurchaseFailed());
        }

        if (legalText != null && legalText.TryGetComponent(out TextLinks links))
        {
            links.Clicked += id => OpenLink(id == "terms" ? termsUrl : subscriptionPolicyUrl);
        }
    }

    protected override void OnShown()
    {
        closeTimer = closeDelay;
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(closeDelay <= 0f);
        }

        RefreshTexts();
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

        // The store may answer after the screen opened: keep the price current.
        priceTimer -= Time.unscaledDeltaTime;
        if (priceTimer <= 0f)
        {
            priceTimer = 1f;
            RefreshTexts();
        }
    }

    /// <summary>Writes the price line and the small print with the current store price.</summary>
    public void RefreshTexts()
    {
        string price = PurchaseService.GetPrice(productId, fallbackPrice);
        SetText(priceText, string.Format(priceTemplate, price));
        SetText(legalText, string.Format(legalTemplate, price));
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

    private static void SetText(TMP_Text label, string text)
    {
        if (label != null && label.text != text)
        {
            label.text = text;
        }
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
