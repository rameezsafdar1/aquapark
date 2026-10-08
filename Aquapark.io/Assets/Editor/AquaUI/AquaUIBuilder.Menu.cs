using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class AquaUIBuilder
{
    private static readonly Color DailyGrey = new Color32(0x4a, 0x5a, 0x80, 255);

    #region Shared pieces

    private static CurrencyBar MakeCurrencies(Transform parent)
    {
        RectTransform go = Container(parent, "Currencies");
        var bar = go.gameObject.AddComponent<CurrencyBar>();
        Unit(go, "coin_pill");
        Unit(go, "gem_pill");
        bar.coinText = Lilita(go, "CoinText", "1,250", 22, White, new Frame(152, 72, 57, 25), null, TextAlignmentOptions.MidlineLeft, TextStyle.Shadow(2f, 0.6f), true);
        bar.gemText = Lilita(go, "GemText", "45", 22, White, new Frame(306, 72, 32, 25), null, TextAlignmentOptions.MidlineLeft, TextStyle.Shadow(2f, 0.6f), true);
        bar.coinPlus = Hit(go, "CoinPlus", new Frame(211, 68, 32, 32));
        bar.gemPlus = Hit(go, "GemPlus", new Frame(337, 68, 32, 32));
        return bar;
    }

    private static SegmentedControl MakeSegmented(Transform parent, string name, Frame bg, string[] labels, Frame[] tabs)
    {
        RectTransform go = Container(parent, name);
        var seg = go.gameObject.AddComponent<SegmentedControl>();
        Gen(go, "seg_bg", bg, "Bg", null, true);
        Image selector = Gen(go, "seg_sel", tabs[0], "Selector", null, true);
        seg.selector = selector.rectTransform;
        seg.selectorPad = Info("seg_sel").pad;
        seg.tabRects = new RectTransform[tabs.Length];
        seg.buttons = new Button[tabs.Length];
        seg.labels = new TMP_Text[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            seg.labels[i] = Lilita(go, "Label" + i, labels[i], 16, i == 0 ? Navy : White, tabs[i], null, TextAlignmentOptions.Center, TextStyle.None);
            Button tab = Hit(go, "Tab" + i, tabs[i], null, false);
            seg.buttons[i] = tab;
            seg.tabRects[i] = (RectTransform)tab.transform;
        }

        return seg;
    }

    private static Frame Union(Frame a, Frame b)
    {
        float x0 = Mathf.Min(a.x, b.x), y0 = Mathf.Min(a.y, b.y);
        float x1 = Mathf.Max(a.x + a.w, b.x + b.w), y1 = Mathf.Max(a.y + a.h, b.y + b.h);
        return new Frame(x0, y0, x1 - x0, y1 - y0);
    }

    private static CanvasGroup MakePanelRoot(Transform parent, string name, out RectTransform rect)
    {
        rect = Container(parent, name);
        return rect.gameObject.AddComponent<CanvasGroup>();
    }

    #endregion

    #region Home and footer

    private const string MenuLogoSprite = "Aqua Park Waterpark UI Sprite Sheet (1)_0";
    private const float LogoShift = 80f;

    private static HomeScreen BuildHome(Transform menu)
    {
        RectTransform home = Container(menu, "Home");
        var screen = home.gameObject.AddComponent<HomeScreen>();

        StretchX(Unit(home, "vignette_top").rectTransform);
        StretchX(Unit(home, "vignette_bottom").rectTransform);
        screen.settingsButton = Tappable(Unit(home, "settings_btn"));
        MakeCurrencies(home);
        // Aqua Rush logo from the Subs Screen sheet (replaces the Figma "AQUA RUSH!" wordmark). It is much taller than the
        // wordmark, so the level pill and the Daily / No Ads tiles sit LogoShift lower than in the Figma design.
        Image logo = SetImage(NewRect(home, "logo", new Frame(77, 104, 236, 180), null), SheetSprite(MenuLogoSprite), false);
        logo.preserveAspect = true;
        logo.gameObject.AddComponent<LogoAnimator>();

        // Level progress pill
        const float s = LogoShift;
        Gen(home, "lvl_pill", new Frame(70, 212 + s, 250, 48), "LevelPill");
        Gen(home, "lvl_badge", new Frame(76, 218 + s, 36, 36), "LevelBadge");
        screen.levelText = Lilita(home, "LevelText", "12", 18, White, new Frame(76, 218 + s, 36, 36), null, TextAlignmentOptions.Center, TextStyle.Shadow(2f, 0.6f));
        Gen(home, "lvl_track", new Frame(120, 228 + s, 150, 16), "LevelTrack");
        screen.fillMask = MakeFill(home, "lvl_fill", new Frame(120, 228 + s, 150, 16), "LevelFill");
        screen.fillFullWidth = 150f;
        screen.fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 96f);
        screen.percentText = Fredoka(home, "PercentText", "64%", 15, White, new Frame(278, 227 + s, 34, 18), null, TextAlignmentOptions.MidlineLeft);

        BuildMissionCard(home, screen);

        // Daily (left) and No Ads (right)
        MenuButton(home, "Daily", "menu_daily_tile", "menu_daily_banner", "menu_daily_badge", 0f, LogoShift, out screen.dailyButton, out screen.dailyBadge, out TMP_Text _);
        MenuButton(home, "NoAds", "menu_noads_tile", "menu_noads_banner", "menu_daily_badge", 0f, LogoShift, out screen.noAdsButton, out screen.noAdsBadge, out TMP_Text _, float.NaN, 286f);
        // Spin lives in the footer now (see BuildFooter).
        return screen;
    }

    private static readonly Color MissionOrange = new Color32(227, 155, 0, 255);

    /// <summary>
    /// The mission card: the card picture with its text painted out (mission_card_bg), live title, reward icon + amount and a
    /// progress bar filled in by MissionCardView. Positions match the original Figma card (title, coin, "+50", bar, "1/3").
    /// </summary>
    private static void BuildMissionCard(Transform home, HomeScreen screen)
    {
        Frame card = UnitFrame("mission_card");
        RectTransform cardRt = NewRect(home, "MissionCard", card, null);
        Image cardImage = SetImage(cardRt, Spr("mission_card_bg"), true);
        screen.missionButton = Tappable(cardImage);

        var view = cardRt.gameObject.AddComponent<MissionCardView>();
        view.titleText = Lilita(cardRt, "Title", "Finish your first race", 19, Navy, new Frame(120, 596, 94, 24), card, TextAlignmentOptions.MidlineLeft, TextStyle.None, true);

        Image icon = SetImage(NewRect(cardRt, "RewardIcon", new Frame(216, 598, 21, 21), card), Spr("icon_coin"), false);
        icon.preserveAspect = true;
        view.rewardIcon = icon;
        view.rewardText = Lilita(cardRt, "RewardText", "+50", 19, MissionOrange, new Frame(240, 598, 36, 21), card, TextAlignmentOptions.MidlineLeft, TextStyle.None, true);

        Frame bar = new Frame(120, 625, 150, 14);
        SetImage(NewRect(cardRt, "Track", bar, card), Spr("mission_track"), false);
        view.fillMask = PlainFill(cardRt, "mission_fill", bar, card, "Fill");
        view.fillFullWidth = bar.w;
        view.progressText = Lilita(cardRt, "ProgressText", "0/1", 13, Navy, bar, card, TextAlignmentOptions.Center, TextStyle.None);

        view.coinIcon = Spr("icon_coin");
        view.gemIcon = Spr("icon_gem");
        view.spinIcon = Spr("navicon_spin");
        view.skinIcon = Spr("navicon_skins");
    }

    /// <summary>Like MakeFill, for a fill picture with no padding inside a parent frame.</summary>
    private static RectTransform PlainFill(Transform parent, string key, Frame logical, Frame parentFrame, string name)
    {
        RectTransform mask = NewRect(parent, name + "Mask", logical, parentFrame);
        mask.pivot = new Vector2(0f, 0.5f);
        mask.anchoredPosition = new Vector2(mask.anchoredPosition.x - logical.w * 0.5f, mask.anchoredPosition.y);
        mask.gameObject.AddComponent<RectMask2D>();

        var fill = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        fill.gameObject.layer = LayerMask.NameToLayer("UI");
        fill.SetParent(mask, false);
        fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.sizeDelta = new Vector2(logical.w, logical.h);
        fill.anchoredPosition = Vector2.zero;
        SetImage(fill, Spr(key), false);
        return mask;
    }

    private static void MenuButton(Transform parent, string name, string tile, string banner, string badge, float dx, float dy,
        out Button button, out GameObject badgeObject, out TMP_Text badgeText, float tileDx = float.NaN, float badgeDx = float.NaN)
    {
        float tdx = float.IsNaN(tileDx) ? dx : tileDx;
        float bdx = float.IsNaN(badgeDx) ? dx : badgeDx;
        Unit(parent, tile, name + "Tile", tdx, dy);
        Unit(parent, banner, name + "Banner", tdx, dy);
        Frame hit = Union(NodeFrame(tile).Moved(tdx, dy), NodeFrame(banner).Moved(tdx, dy));
        button = Hit(parent, name + "Button", Grow(hit, 2f));

        // The red dot for the daily button sits at the same place on all three, so reuse its sprite where the export failed.
        Image badgeImage = Unit(parent, badge, name + "Badge", bdx, dy);
        badgeObject = badgeImage.gameObject;
        Frame badgeUnit = UnitFrame(badge).Moved(bdx, dy);
        Frame badgeFrame = NodeFrame(badge).Moved(bdx, dy);
        badgeText = Lilita(badgeImage.transform, "Count", name == "NoAds" ? "!" : "1", 14, White, badgeFrame, badgeUnit, TextAlignmentOptions.Center, TextStyle.Shadow(1.5f, 0.6f));
    }

    /// <summary>A clipped fill: a mask whose width is changed at runtime, with the full-width fill picture inside it.</summary>
    private static RectTransform MakeFill(Transform parent, string key, Frame logical, string name)
    {
        RectTransform mask = NewRect(parent, name + "Mask", logical, null);
        mask.pivot = new Vector2(0f, 0.5f);
        mask.anchoredPosition = new Vector2(mask.anchoredPosition.x - logical.w * 0.5f, mask.anchoredPosition.y);
        mask.gameObject.AddComponent<RectMask2D>();

        SpriteInfo si = Info(key);
        var fill = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        fill.gameObject.layer = LayerMask.NameToLayer("UI");
        fill.SetParent(mask, false);
        fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.sizeDelta = new Vector2(logical.w + si.pad * 2f, logical.h + si.pad * 2f);
        fill.anchoredPosition = new Vector2(-si.pad, 0f);
        SetImage(fill, Spr(key), false);
        return mask;
    }

    /// <summary>
    /// The bottom bar made of separate pieces: one bar picture (both top corners rounded), tabs (highlight, round icon, icon, label,
    /// tap area), dividers, the Spin tab with its red dot, and the big Play button on top.
    /// </summary>
    private static FooterNav BuildFooter(Transform menu, HomeScreen home)
    {
        RectTransform footer = Container(menu, "Footer");
        var nav = footer.gameObject.AddComponent<FooterNav>();

        SpriteInfo bar = Info("footer_bar");
        RectTransform barRect = NewRect(footer, "Bar", new Frame(-bar.pad, 744f - bar.pad, bar.bw + bar.pad * 2f, bar.bh + bar.pad), null);
        SetImage(barRect, Spr("footer_bar"), false);
        StretchX(barRect);
        MakeDivider(footer, "Divider1", 75f);
        MakeDivider(footer, "Divider2", 315f);

        nav.shopButton = MakeTab(footer, "Shop", 9f, "navcircle_pink", "navicon_shop", "SHOP", out nav.shopHighlight, out _);
        nav.skinsButton = MakeTab(footer, "Skins", 75f, "navcircle_blue", "navicon_skins", "SKINS", out nav.skinsHighlight, out _);
        nav.ranksButton = MakeTab(footer, "Ranks", 249f, "navcircle_yellow", "navicon_trophy", "RANKS", out _, out _);
        home.spinButton = MakeTab(footer, "Spin", 315f, "navcircle_yellow", "navicon_spin", "SPIN", out _, out _);

        // Red dot with the number of spins available, on the top right of the Spin icon.
        Image badge = Unit(footer, "menu_spin_badge", "SpinBadge", 359f - 70f, 754f - 572f);
        home.spinBadge = badge.gameObject;
        Frame badgeUnit = UnitFrame("menu_spin_badge").Moved(289f, 182f);
        Frame badgeNode = NodeFrame("menu_spin_badge").Moved(289f, 182f);
        home.spinBadgeText = Lilita(badge.transform, "Count", "1", 14, White, badgeNode, badgeUnit, TextAlignmentOptions.Center, TextStyle.Shadow(1.5f, 0.6f));

        Image play = Unit(footer, "play_btn", "PlayButton", 0f, 0f, null, true);
        nav.playButton = MakeButton(play.gameObject, play);
        return nav;
    }

    private static void MakeDivider(Transform parent, string name, float x)
    {
        SetImage(NewRect(parent, name, new Frame(x, 762f, 2f, 44f), null), Spr("navtab_divider"), false);
    }

    private static Button MakeTab(Transform parent, string name, float x, string circleKey, string iconKey, string label, out GameObject highlight, out GameObject root)
    {
        Frame tab = new Frame(x, 752f, 66f, 82f);
        highlight = Gen(parent, "navtab_highlight", tab, name + "Highlight", null, true).gameObject;
        highlight.SetActive(false);
        Gen(parent, circleKey, new Frame(x + 9f, 758f, 48f, 48f), name + "Circle");
        SetImage(NewRect(parent, name + "Icon", new Frame(x + 20f, 769f, 26f, 26f), null), Spr(iconKey), false);
        Lilita(parent, name + "Label", label, 14, White, new Frame(x, 809f, 66f, 16f), null, TextAlignmentOptions.Center, TextStyle.Shadow(2f, 0.8f));
        root = null;
        return Hit(parent, name + "Tab", new Frame(x, 752f, 66f, 79f), null, false);
    }

    #endregion

    #region Shop

    private static ShopScreen BuildShop(Transform menu)
    {
        MakePanelRoot(menu, "Shop", out RectTransform page);
        var shop = page.gameObject.AddComponent<ShopScreen>();

        Stretch(Unit(page, "shop_bg").rectTransform);
        shop.backButton = Tappable(Unit(page, "back_btn"));
        MakeCurrencies(page);
        Unit(page, "shop_title");

        shop.tabs = MakeSegmented(page, "Tabs", new Frame(56, 172, 279, 49), new[] { "FEATURED", "GEMS", "COINS" },
            new[] { new Frame(62, 178, 108, 37), new Frame(172, 178, 77, 37), new Frame(251, 178, 80, 37) });

        shop.starterCard = Unit(page, "shop_starter");
        shop.starterTimerText = Fredoka(page, "StarterTimer", "23:59:41", 14, White, new Frame(60, 275, 62, 17), null, TextAlignmentOptions.MidlineLeft);
        shop.starterBuy = Hit(page, "StarterBuy", new Frame(267, 320, 95, 41));

        Unit(page, "shop_sec_gems");
        Unit(page, "shop_pack80");
        shop.pack80Buy = Hit(page, "Pack80Buy", new Frame(24, 522, 94, 32));
        Unit(page, "shop_pack500");
        shop.pack500Buy = Hit(page, "Pack500Buy", new Frame(148, 522, 94, 32));
        Unit(page, "shop_pack1200");
        shop.pack1200Buy = Hit(page, "Pack1200Buy", new Frame(272, 522, 94, 32));

        shop.removeAdsCard = Unit(page, "shop_removeads");
        shop.removeAdsBuy = Hit(page, "RemoveAdsBuy", new Frame(74, 614, 63, 27));
        Unit(page, "shop_freecoins");
        shop.freeCoinsWatch = Hit(page, "FreeCoinsWatch", new Frame(261, 614, 91, 27));
        return shop;
    }

    #endregion

    #region Skins

    private static SkinsScreen BuildSkins(Transform menu)
    {
        MakePanelRoot(menu, "Skins", out RectTransform page);
        var skins = page.gameObject.AddComponent<SkinsScreen>();

        Stretch(Unit(page, "skins_bg").rectTransform);
        skins.backButton = Tappable(Unit(page, "back_btn"));
        MakeCurrencies(page);
        Unit(page, "skins_title");
        Unit(page, "skins_glow");

        RectTransform preview = NewRect(page, "PreviewArt", new Frame(125, 184, 140, 180), null);
        skins.previewArt = SetImage(preview, null, false);
        skins.previewArt.preserveAspect = true;

        // Live 3D model of the selected item, same spot as the picture (which stays as the fallback and the editor view).
        RectTransform live = NewRect(page, "Preview3D", new Frame(120, 163, 150, 222), null);
        var liveImage = live.gameObject.AddComponent<RawImage>();
        liveImage.raycastTarget = false;
        skins.preview3D = live.gameObject.AddComponent<SkinPreview3D>();
        skins.preview3D.player = Object.FindFirstObjectByType<PlayerEffects>(FindObjectsInactive.Include);
        skins.preview3D.idleController = PreviewIdleController();

        skins.arrowLeft = Tappable(Unit(page, "skins_arrow_l"));
        skins.arrowRight = Tappable(Unit(page, "skins_arrow_r"));
        skins.nameText = Lilita(page, "SkinName", "GHOST", 26, White, new Frame(80, 402, 172, 30), null, TextAlignmentOptions.MidlineLeft, TextStyle.Outline(3.5f, 3f), true);
        skins.epicTag = Unit(page, "skins_tag_epic").gameObject;

        // Two tabs (trails were dropped): the selector fills half of the bar each.
        skins.tabs = MakeSegmented(page, "Tabs", new Frame(44, 446, 302, 49), new[] { "CHARACTERS", "FLOATIES" },
            new[] { new Frame(50, 452, 143, 37), new Frame(197, 452, 143, 37) });

        // Grid: one scrolling page per tab; SkinsScreen swaps them (and the scroll content) when the tab changes.
        Frame grid = new Frame(12, 494, 366, 196);
        Gen(page, "skin_grid_bg", grid, "GridBg", null, true);
        RectTransform viewport = NewRect(page, "GridViewport", grid, null);
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        var characters = AssetDatabase.LoadAssetAtPath<SkinDatabase>(DataDir + "/SkinDatabase.asset");
        var floaties = AssetDatabase.LoadAssetAtPath<SkinDatabase>(DataDir + "/FloatieDatabase.asset");
        int characterCount = characters != null && characters.skins != null ? characters.skins.Length : 8;
        int floatieCount = floaties != null && floaties.skins != null ? floaties.skins.Length : 8;

        skins.cards = MakeSkinGrid(viewport, "CharactersContent", characterCount, out RectTransform characterContent);
        skins.floatieCards = MakeSkinGrid(viewport, "FloatiesContent", floatieCount, out RectTransform floatieContent);
        floatieContent.gameObject.SetActive(false);
        scroll.content = characterContent;
        skins.characterPage = characterContent.gameObject;
        skins.floatiePage = floatieContent.gameObject;
        skins.gridScroll = scroll;
        PreviewFloaties(skins.floatieCards, floaties);

        Gen(page, "skin_fade", grid, "GridFade");
        PreviewSkins(skins);

        skins.actionButton = Tappable(Unit(page, "skins_equip_btn"));
        skins.actionGraphic = skins.actionButton.targetGraphic;
        skins.actionLabel = Lilita(page, "ActionLabel", "EQUIP", 19, White, NodeFrame("skins_equip_btn"), null, TextAlignmentOptions.Center, TextStyle.Outline(2f, 1.5f), true);
        return skins;
    }

    /// <summary>A controller with one looping state (Happy Idle) for the shop's 3D character preview.</summary>
    private static RuntimeAnimatorController PreviewIdleController()
    {
        const string path = "Assets/UI/Skins/SkinPreviewIdle.controller";
        var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
        if (existing != null)
        {
            return existing;
        }

        AnimationClip idle = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/4- Animations/Happy Idle.fbx"))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
            {
                idle = clip;
            }
        }

        return UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(path, idle);
    }

    /// <summary>A scrolling page of skin cards, four per row (design: columns 23/111/199/287, rows 110 apart from 506).</summary>
    private static SkinCardView[] MakeSkinGrid(RectTransform viewport, string name, int count, out RectTransform content)
    {
        int rows = Mathf.Max(2, (count + 3) / 4);
        var contentFrame = new Frame(12, 494, 366, 12 + rows * 110 + 14);
        content = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        content.gameObject.layer = LayerMask.NameToLayer("UI");
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, contentFrame.h);
        content.anchoredPosition = Vector2.zero;

        float[] columns = { 23, 111, 199, 287 };
        string[] colorNames = { "purple", "blue", "green", "yellow" };
        var cards = new SkinCardView[count];
        for (int i = 0; i < count; i++)
        {
            Frame cf = new Frame(columns[i % 4], 506 + (i / 4) * 110, 80, 100);
            cards[i] = MakeSkinCard(content, "Card" + i, cf, contentFrame, colorNames);
        }

        return cards;
    }

    /// <summary>Fills the hidden floatie page so it looks right when switched on in the editor.</summary>
    private static void PreviewFloaties(SkinCardView[] cards, SkinDatabase db)
    {
        if (db == null || db.skins == null)
        {
            return;
        }

        SkinManager.Initialise(db);
        for (int i = 0; i < cards.Length && i < db.skins.Length; i++)
        {
            cards[i].Bind(db.skins[i], i == 0);
        }
    }

    /// <summary>Fills the cards and the big picture with the first skins so the scene looks right before it runs.</summary>
    private static void PreviewSkins(SkinsScreen skins)
    {
        var db = AssetDatabase.LoadAssetAtPath<SkinDatabase>(DataDir + "/SkinDatabase.asset");
        if (db == null || db.skins == null)
        {
            return;
        }

        SkinManager.Initialise(db);
        for (int i = 0; i < skins.cards.Length && i < db.skins.Length; i++)
        {
            skins.cards[i].Bind(db.skins[i], i == 0);
        }

        if (db.skins.Length > 0 && db.skins[0].icon != null)
        {
            skins.previewArt.sprite = db.skins[0].icon;
            skins.previewArt.rectTransform.sizeDelta = skins.PreviewSize(db.skins[0].icon);
            skins.nameText.text = db.skins[0].displayName.ToUpperInvariant();
            skins.epicTag.SetActive(db.skins[0].rarity >= SkinRarity.Epic);
        }
    }

    private static SkinCardView MakeSkinCard(RectTransform content, string name, Frame cf, Frame contentFrame, string[] colorNames)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(content, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cf.w, cf.h);
        rt.anchoredPosition = new Vector2(cf.CX - contentFrame.CX, -(cf.CY - contentFrame.y));

        var view = go.AddComponent<SkinCardView>();
        Image bg = Gen(rt, "card_blue", cf, "Bg", cf);
        bg.raycastTarget = true;
        view.bg = bg;
        view.button = MakeButton(go, bg);
        view.normalSprites = new Sprite[4];
        view.selectedSprites = new Sprite[4];
        for (int c = 0; c < 4; c++)
        {
            view.normalSprites[c] = Spr("card_" + colorNames[c]);
            view.selectedSprites[c] = Spr("card_" + colorNames[c] + "_sel");
        }

        // Taller than the Figma placeholder (56 x 62) so the characters read big; feet tuck behind the name / price label,
        // which is drawn on top.
        RectTransform art = NewRect(rt, "Art", new Frame(cf.x + 4, cf.y - 1, 72, 90), cf);
        view.art = SetImage(art, null, false);
        view.art.preserveAspect = true;

        view.nameText = Lilita(rt, "Name", "PENGUIN", 12, White, new Frame(cf.x, cf.y + 76, cf.w, 16), cf, TextAlignmentOptions.Center, TextStyle.Outline(1.5f, 1f), true);

        // The green tick sits over the top right corner (design: card at 23,506, tick at 83,498).
        Image badge = Unit(rt, "equipped_badge", "Equipped", cf.x - 23f, cf.y - 506f, cf);
        view.equippedBadge = badge.gameObject;

        // Price pill, shown instead of the name when the skin is locked.
        RectTransform priceRoot = NewRect(rt, "Price", new Frame(cf.x + 6, cf.y + 74, 68, 22), cf);
        view.priceRoot = priceRoot.gameObject;
        SpriteInfo pillInfo = Info("price_pill");
        RectTransform pill = NewRect(priceRoot, "Pill", Grow(new Frame(cf.x + 6, cf.y + 74, 60, 22), pillInfo.pad), new Frame(cf.x + 6, cf.y + 74, 68, 22));
        view.priceBg = SetImage(pill, Spr("price_pill"), false);
        Slice(view.priceBg);
        view.pillNavy = Spr("price_pill");
        view.pillRed = Spr("price_pill_red");
        view.pillPad = pillInfo.pad;
        view.coinIcon = Spr("icon_coin");
        view.gemIcon = Spr("icon_gem");

        var icon = new GameObject("Icon", typeof(RectTransform)).GetComponent<RectTransform>();
        icon.gameObject.layer = LayerMask.NameToLayer("UI");
        icon.SetParent(pill, false);
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = new Vector2(16f, 16f);
        icon.anchoredPosition = new Vector2(pillInfo.pad + 11f, 0f);
        view.priceIcon = SetImage(icon, Spr("icon_coin"), false);

        var label = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        label.gameObject.layer = LayerMask.NameToLayer("UI");
        label.SetParent(pill, false);
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.offsetMin = new Vector2(18f, 0f);
        label.offsetMax = new Vector2(-4f, 0f);
        var text = label.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = lilita;
        text.fontSharedMaterial = TextMaterial(lilita, TextStyle.Shadow(1f, 0.6f), 12f);
        text.fontSize = 12f;
        text.color = White;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        view.priceText = text;
        return view;
    }

    #endregion

    #region Daily reward

    private static DailyPopup BuildDaily(Transform root)
    {
        CanvasGroup group = MakePanelRoot(root, "Daily", out RectTransform go);
        var popup = go.gameObject.AddComponent<DailyPopup>();

        RectTransform dim = Container(go, "Dim");
        Image dimImage = SetImage(dim, Spr("dim_black"), true);
        dimImage.color = new Color32(0x12, 0x0b, 0x45, (byte)(0.72f * 255f));

        MakeCurrencies(go);
        RectTransform pop = Container(go, "Popup");
        popup.popTarget = pop;
        Gen(pop, "daily_panel", new Frame(20, 176, 350, 520), "Panel");
        Unit(pop, "daily_ribbon");
        Unit(pop, "daily_title");
        Fredoka(pop, "Subtitle", "Log in every day for bigger rewards!", 15, DailyGrey, new Frame(73, 234, 244, 18), null, TextAlignmentOptions.Center);
        popup.closeButton = Tappable(Unit(pop, "daily_close"));

        float[] xs = { 41, 147, 253, 41, 147, 253 };
        float[] ys = { 268, 268, 268, 390, 390, 390 };
        popup.days = new DayCardView[7];
        for (int i = 0; i < 6; i++)
        {
            popup.days[i] = MakeDayCard(pop, i, new Frame(xs[i], ys[i], 96, 112));
        }

        popup.days[6] = MakeMegaCard(pop, new Frame(41, 512, 308, 96));

        Image claim = Unit(pop, "daily_claim_btn");
        popup.claimButton = Tappable(claim);
        popup.claimGraphic = claim;
        Image dbl = Unit(pop, "daily_x2_btn");
        popup.doubleButton = Tappable(dbl);
        popup.doubleGraphic = dbl;
        popup.adTag = Unit(pop, "daily_adtag").gameObject;

        Image chip = Unit(go, "daily_next");
        popup.nextChip = chip.gameObject;
        popup.nextText = Fredoka(chip.transform, "Text", "Next reward in 14:22:05", 15, White, new Frame(128, 730, 156, 18), UnitFrame("daily_next"), TextAlignmentOptions.MidlineLeft);
        return popup;
    }

    private static DayCardView MakeDayCard(Transform parent, int index, Frame cf)
    {
        RectTransform card = NewRect(parent, "Day" + (index + 1), cf, null);
        var view = card.gameObject.AddComponent<DayCardView>();
        view.bg = Gen(card, "day_blue", cf, "Bg", cf);
        view.claimedSprite = Spr("day_green");
        view.todaySprite = Spr("day_today");
        view.upcomingSprite = Spr("day_blue");
        Unit(card, "daily_reward" + (index + 1), "Reward", cf.x - UnitCardX(index), cf.y - UnitCardY(index), cf);
        view.dayText = Lilita(card, "DayText", "DAY " + (index + 1), 15, White, new Frame(cf.x, cf.y + 5, cf.w, 18), cf, TextAlignmentOptions.Center, TextStyle.Shadow(1.5f, 0.8f));

        SpriteInfo reward = Info("daily_reward" + (index + 1));
        if (reward.texts != null && reward.texts.Count > 0)
        {
            TextInfo t = reward.texts[0];
            Frame tf = new Frame(t.x - 6f - UnitCardX(index) + cf.x, t.y - UnitCardY(index) + cf.y, t.w + 12f, t.h);
            view.amountText = Lilita(card, "Amount", t.text, t.size, White, tf, cf, TextAlignmentOptions.Center, TextStyle.Shadow(2f, 0.8f));
        }

        view.overlay = Gen(card, "day_overlay", cf, "Overlay", cf).gameObject;
        view.check = Unit(card, "daily_check", "Check", cf.x - 41f, cf.y - 268f, cf).gameObject;
        view.tag = Unit(card, "daily_tag", "Tag", cf.x - 253f, cf.y - 268f, cf).gameObject;

        // Start from the "upcoming" look; the first card is today. DailyPopup sets the real state when it opens.
        view.overlay.SetActive(false);
        view.check.SetActive(false);
        view.tag.SetActive(index == 0);
        return view;
    }

    // Where each day's card sits in the design, so the pictures inside the card can be moved with it.
    private static float UnitCardX(int index) { return new[] { 41f, 147f, 253f, 41f, 147f, 253f }[index]; }
    private static float UnitCardY(int index) { return new[] { 268f, 268f, 268f, 390f, 390f, 390f }[index]; }

    private static DayCardView MakeMegaCard(Transform parent, Frame cf)
    {
        RectTransform card = NewRect(parent, "Day7", cf, null);
        var view = card.gameObject.AddComponent<DayCardView>();
        view.bg = Gen(card, "day_mega", cf, "Bg", cf);
        view.claimedSprite = view.todaySprite = view.upcomingSprite = Spr("day_mega");
        view.dayText = Lilita(card, "DayText", "DAY 7", 20, White, new Frame(57, 522, 80, 23), cf, TextAlignmentOptions.MidlineLeft, TextStyle.Shadow(2f, 0.8f));
        Lilita(card, "Title", "MEGA CHEST", 24, White, new Frame(57, 548, 160, 27), cf, TextAlignmentOptions.MidlineLeft, TextStyle.Shadow(2.5f, 0.8f));
        Fredoka(card, "Sub", "Legendary skin inside!", 13, White, new Frame(57, 580, 150, 16), cf, TextAlignmentOptions.MidlineLeft);
        Unit(card, "daily_chest", "Chest", 0f, 0f, cf);
        Unit(card, "daily_sparkle", "Sparkle", 0f, 0f, cf);
        view.overlay = Gen(card, "day_overlay_mega", cf, "Overlay", cf).gameObject;
        view.check = Unit(card, "daily_check", "Check", cf.x + cf.w * 0.5f - 22f - 67f, cf.y + cf.h * 0.5f - 22f - 308f, cf).gameObject;
        var tagStub = new GameObject("Tag", typeof(RectTransform));
        tagStub.transform.SetParent(card, false);
        view.tag = tagStub;
        view.overlay.SetActive(false);
        view.check.SetActive(false);
        return view;
    }

    #endregion

    #region Spin

    private static SpinScreen BuildSpin(Transform menu)
    {
        MakePanelRoot(menu, "Spin", out RectTransform page);
        var spin = page.gameObject.AddComponent<SpinScreen>();

        Stretch(Unit(page, "spin_bg").rectTransform);
        spin.backButton = Tappable(Unit(page, "back_btn"));
        MakeCurrencies(page);
        Unit(page, "spin_ribbon");
        Unit(page, "spin_title");

        Frame wheelFrame = NodeFrame("spin_wheel");
        RectTransform wheel = NewRect(page, "Wheel", wheelFrame, null);
        spin.wheel = wheel;
        Unit(wheel, "spin_wheel", "WheelArt", 0f, 0f, wheelFrame);
        for (int i = 1; i <= 8; i++)
        {
            Unit(wheel, "spin_prize" + i, "Prize" + i, 0f, 0f, wheelFrame);
        }

        Unit(wheel, "spin_jackpot", "Jackpot", 0f, 0f, wheelFrame);

        spin.hubButton = Tappable(Unit(page, "spin_hub"));
        Unit(page, "spin_pointer");

        Image freeTag = Unit(page, "spin_freetag");
        spin.freeTagText = Lilita(freeTag.transform, "Text", "1 FREE SPIN LEFT", 14, White, new Frame(134, 606, 123, 26), UnitFrame("spin_freetag"), TextAlignmentOptions.Center, TextStyle.Shadow(1.5f, 0.6f), true);

        Image free = Unit(page, "spin_free_btn");
        spin.freeButton = Tappable(free);
        spin.freeGraphic = free;
        spin.adButton = Tappable(Unit(page, "spin_again_btn"));
        Unit(page, "spin_adtag");

        Image chip = Unit(page, "spin_next");
        spin.nextChip = chip.gameObject;
        spin.nextText = Fredoka(chip.transform, "Text", "Next free spin in 05:12:33", 14, White, new Frame(128, 796, 160, 17), UnitFrame("spin_next"), TextAlignmentOptions.MidlineLeft);
        return spin;
    }

    #endregion
}
