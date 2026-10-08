using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public static partial class AquaUIBuilder
{
    private static readonly Color VipGrey = new Color32(0x4a, 0x5a, 0x80, 255);
    private static readonly Color VipLime = new Color32(0xb5, 0xff, 0x8a, 255);
    private static readonly Color VipYellow = new Color32(0xff, 0xe4, 0x5c, 255);

    /// <summary>
    /// The VIP subscription offer (Figma '09 · VIP Subscription Offer'). Static art comes from figma_data/vip_build.py
    /// (background redrawn, the rest matted out of the frame render); the plan cards are live so the selection can move.
    /// Benefit labels are drawn above the cards: in the design their second line runs under the first card.
    /// </summary>
    private static VipOfferScreen BuildVip(Transform root)
    {
        MakePanelRoot(root, "Vip", out RectTransform page);
        var vip = page.gameObject.AddComponent<VipOfferScreen>();

        // Background covers the screen without stretching the sunburst (crops the sides on narrow phones).
        RectTransform bg = Container(page, "Background");
        SetImage(bg, Spr("vip_bg"), true);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = DW / DH;

        Image restore = Unit(page, "vip_restore");
        vip.restoreButton = Tappable(restore);
        vip.closeButton = Tappable(Unit(page, "vip_close"));
        Unit(page, "vip_header");
        // UNLOCK <count> VIP SKINS: the number is live (character count); VipOfferScreen re-centres the three parts.
        vip.unlockText = Lilita(page, "Unlock", "UNLOCK", 24, White, new Frame(60, 188, 100, 35), null, TextAlignmentOptions.Center, TextStyle.Outline(3.8f, 3f));
        vip.vipSkinsText = Lilita(page, "VipSkins", "VIP SKINS", 24, White, new Frame(210, 188, 125, 35), null, TextAlignmentOptions.Center, TextStyle.Outline(3.8f, 3f));
        vip.skinCountText = Lilita(page, "SkinCount", "40", 38, White, new Frame(150, 182, 67, 47), null, TextAlignmentOptions.Center, TextStyle.Outline(5f, 3f));
        vip.skinCountText.enableVertexGradient = true;
        vip.skinCountText.colorGradient = new VertexGradient(Hex("#B5FF5A"), Hex("#B5FF5A"), Hex("#3CC832"), Hex("#3CC832"));
        vip.LayoutHeadline(AssetDatabase.LoadAssetAtPath<SkinDatabase>(DataDir + "/SkinDatabase.asset"));

        // Showcase: the Figma characters, used only when there is no background video.
        vip.showcaseArt = Unit(page, "vip_showcase").gameObject;
        // Background video: covers the whole screen right above the sunburst, with a shade so the text stays readable.
        RectTransform videoRoot = Container(page, "BackgroundVideo");
        videoRoot.SetSiblingIndex(bg.GetSiblingIndex() + 1);
        vip.videoCard = videoRoot.gameObject;
        RectTransform videoRect = Container(videoRoot, "Video");
        vip.showcaseVideo = videoRect.gameObject.AddComponent<RawImage>();
        vip.showcaseVideo.raycastTarget = false;
        SetImage(Container(videoRoot, "Shade"), Spr("vip_video_shade"), false);
        videoRoot.gameObject.SetActive(false);
        vip.showcaseClip = FindShowcaseClip();
        vip.chromaKeyMaterial = ChromaKeyMaterial();
        Unit(page, "vip_benefits");

        vip.plans = new VipOfferScreen.Plan[2];
        for (int i = 0; i < 2; i++)
        {
            vip.plans[i] = MakeVipPlan(page, i);
        }

        vip.cardOn = Spr("vip_plan_on");
        vip.cardOff = Spr("vip_plan_off");
        vip.radioOn = Spr("vip_radio_on");
        vip.radioOff = Spr("vip_radio_off");
        vip.cardOnPad = Info("vip_plan_on").pad;
        vip.cardOffPad = Info("vip_plan_off").pad;
        vip.cardSize = new Vector2(354f, 74f);

        string[] labels = { "20 SKINS\nPER SET", "NO\nADS", "DAILY\nGEMS", "x2\nCOINS" };
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = Lilita(page, "BenefitLabel" + (i + 1), labels[i], 13, White, new Frame(20 + 90 * i, 474, 80, 28), null,
                TextAlignmentOptions.Top, TextStyle.Outline(2f, 2f));
            label.lineSpacing = -14f;
        }

        Image cont = Unit(page, "vip_continue");
        vip.continueButton = Tappable(cont);

        Text(page, "NoCommitment", "No commitment · Cancel anytime", fredoka, 14, VipLime, new Frame(20, 746, 350, 18), null,
            TextAlignmentOptions.Center, TextStyle.Shadow(1.5f, 0.6f));
        vip.legalText = Fredoka(page, "Legal", "", 10, White, new Frame(20, 769, 350, 54), null, TextAlignmentOptions.Top);
        vip.legalText.textWrappingMode = TextWrappingModes.Normal;
        vip.legalText.lineSpacing = -6f;

        vip.termsButton = VipLink(page, "Terms", new Frame(82, 825, 31, 13));
        vip.privacyButton = VipLink(page, "Privacy Policy", new Frame(127, 825, 71, 13));
        vip.subscriptionButton = VipLink(page, "Subscription Policy", new Frame(212, 825, 96, 13));

        // Preview in the editor: first plan selected and the price filled in.
        for (int i = 0; i < 2; i++)
        {
            bool on = i == 0;
            VipOfferScreen.Plan plan = vip.plans[i];
            plan.bg.sprite = on ? vip.cardOn : vip.cardOff;
            plan.bg.rectTransform.sizeDelta = vip.cardSize + Vector2.one * ((on ? vip.cardOnPad : vip.cardOffPad) * 2f);
            plan.radio.sprite = on ? vip.radioOn : vip.radioOff;
            plan.check.SetActive(on);
        }

        vip.legalText.text = "Choose Skin Set 1 or Skin Set 2 for a 3-day free trial: 20 VIP skins per set, no ads, daily gems and x2 coins. " +
                             "After the trial, the selected set renews at INR 819/week unless cancelled at least 24h before renewal. " +
                             "Manage anytime in Google Play Subscriptions. Not required to play.";
        return vip;
    }

    public const string VipVideoDir = "Assets/UI/Vip";

    /// <summary>The first video clip in Assets/UI/Vip or Assets/Videos (the recorded showcase), or null.</summary>
    private static VideoClip FindShowcaseClip()
    {
        if (!AssetDatabase.IsValidFolder(VipVideoDir))
        {
            AssetDatabase.CreateFolder("Assets/UI", "Vip");
        }

        var folders = AssetDatabase.IsValidFolder("Assets/Videos") ? new[] { VipVideoDir, "Assets/Videos" } : new[] { VipVideoDir };
        foreach (string guid in AssetDatabase.FindAssets("t:VideoClip", folders))
        {
            return AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(guid));
        }

        return null;
    }

    private static Material ChromaKeyMaterial()
    {
        const string path = Dir + "/Shaders/UIChromaKey.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("UI/Aqua Chroma Key");
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    /// <summary>One plan card (design: Skin Set 1 at y 500, Skin Set 2 86 px below).</summary>
    private static VipOfferScreen.Plan MakeVipPlan(Transform parent, int index)
    {
        float dy = 86f * index;
        Frame cf = new Frame(18, 500 + dy, 354, 74);
        RectTransform card = NewRect(parent, "Plan" + (index + 1), cf, null);
        var plan = new VipOfferScreen.Plan();

        plan.bg = Gen(card, index == 0 ? "vip_plan_on" : "vip_plan_off", cf, "Bg", cf);
        plan.bg.raycastTarget = true;
        plan.button = MakeButton(card.gameObject, plan.bg, false);

        plan.radio = Gen(card, index == 0 ? "vip_radio_on" : "vip_radio_off", new Frame(32, 520 + dy, 34, 34), "Radio", cf);
        plan.check = Unit(card, "vip_check", "Check", 0f, dy, cf).gameObject;

        Lilita(card, "Title", "SKIN SET " + (index + 1), 20, Navy, new Frame(78, 511 + dy, 180, 24), cf, TextAlignmentOptions.MidlineLeft, TextStyle.None);
        Frame trial = new Frame(78, 542 + dy, 71, 16);
        Gen(card, "vip_trial", trial, "Trial", cf);
        Lilita(card, "TrialText", "3 DAYS FREE", 11, White, trial, cf, TextAlignmentOptions.Center, TextStyle.None);
        plan.priceText = Fredoka(card, "Price", "then INR 819/week", 13, VipGrey, new Frame(154, 541 + dy, 134, 18), cf, TextAlignmentOptions.MidlineLeft);
        Unit(card, "vip_preview" + (index + 1), "Preview", 0f, 0f, cf);
        return plan;
    }

    private static Button VipLink(Transform parent, string label, Frame f)
    {
        Text(parent, label.Replace(" ", "") + "Link", "<u>" + label + "</u>", fredoka, 11, VipYellow, Grow(f, 2f), null, TextAlignmentOptions.Center, TextStyle.None);
        return Hit(parent, label.Replace(" ", "") + "Button", new Frame(f.x - 4, f.y - 6, f.w + 8, f.h + 10), null, false);
    }
}
