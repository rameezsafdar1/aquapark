using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class AquaUIBuilder
{
    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    #region Race HUD

    private static RaceHud BuildHud(Transform root)
    {
        RectTransform go = Container(root, "Hud");
        var hud = go.gameObject.AddComponent<RaceHud>();

        hud.pauseButton = Tappable(Unit(go, "pause_btn"));
        hud.rankText = Lilita(go, "Rank", "3rd /12", 64, Hex("#FFD23F"), new Frame(75, 34, 240, 84), null, TextAlignmentOptions.Center, TextStyle.Outline(6f, 5f));
        Unit(go, "hud_coin_pill");
        hud.coinText = Lilita(go, "CoinText", "86", 22, White, new Frame(338, 72, 36, 25), null, TextAlignmentOptions.MidlineLeft, TextStyle.Shadow(2f, 0.6f), true);

        // Progress bar. TrackArea is the logical bar the racer markers move along.
        Frame trackFrame = new Frame(38, 139, 300, 14);
        Gen(go, "hud_track", trackFrame, "Track");
        hud.fillMask = MakeFill(go, "hud_fill", trackFrame, "ProgressFill");
        hud.fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 186f);
        Unit(go, "finish_flag");
        RectTransform area = NewRect(go, "TrackArea", trackFrame, null);
        hud.track = area;

        hud.aiMarkerTemplate = MakeMarker(area, "AiMarker", "racer_pink", 22f);
        hud.aiMarkerTemplate.gameObject.SetActive(false);
        hud.aiMarkerSprites = new[] { Spr("racer_pink"), Spr("racer_purple"), Spr("racer_green"), Spr("racer_orange"), Spr("racer_cyan"), Spr("racer_red") };
        hud.playerMarker = MakeMarker(area, "PlayerMarker", "racer_you", 30f);

        // Pop-ups
        hud.knockoutText = Lilita(go, "Knockout", "KNOCKOUT! +10", 30, Hex("#FF5FAE"), new Frame(60, 214, 270, 66), null, TextAlignmentOptions.Center, TextStyle.Outline(5f, 4f));
        hud.knockoutText.gameObject.SetActive(false);

        Frame chipFrame = new Frame(106, 278, 177, 41);
        RectTransform chip = NewRect(go, "Combo", chipFrame, null);
        Gen(chip, "combo_chip", chipFrame, "Bg", chipFrame);
        hud.comboText = Lilita(chip, "Text", "PERFECT JUMP  x2", 18, White, chipFrame, chipFrame, TextAlignmentOptions.Center, TextStyle.Shadow(1.5f, 0.8f));
        hud.comboChip = chip.gameObject;
        chip.gameObject.SetActive(false);

        hud.steerHint = Unit(go, "steer_hint").gameObject;
        return hud;
    }

    private static RectTransform MakeMarker(RectTransform track, string name, string spriteKey, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(track, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        SetImage(rt, Spr(spriteKey), false);
        return rt;
    }

    #endregion

    #region Results

    private static ResultsScreen BuildResults(Transform root)
    {
        MakePanelRoot(root, "Results", out RectTransform go);
        var res = go.gameObject.AddComponent<ResultsScreen>();

        Stretch(Unit(go, "res_dim").rectTransform);
        res.sunburst = Unit(go, "res_sunburst").rectTransform;
        Unit(go, "res_sunglow");
        Unit(go, "res_trophy");
        Unit(go, "res_ribbon");
        res.ribbonText = Lilita(go, "RibbonText", "1ST PLACE!", 40, Hex("#FFF36B"), new Frame(65, 290, 260, 50), null, TextAlignmentOptions.Center, TextStyle.Outline(5f, 4f), true);

        // Leaderboard
        Image board = Unit(go, "res_board");
        res.board = board.gameObject;
        Frame bf = UnitFrame("res_board");
        float[] rowY = { 413, 463, 511 };
        float[] circleX = { 63, 61, 61 };
        float[] nameX = { 103, 101, 101 };
        float[] nameY = { 418, 468, 516 };
        res.positionTexts = new TMP_Text[3];
        res.nameTexts = new TMP_Text[3];
        res.timeTexts = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            res.positionTexts[i] = Lilita(board.transform, "Pos" + i, (i + 1).ToString(), 16, Navy, new Frame(circleX[i], rowY[i], 30, 30), bf, TextAlignmentOptions.Center, TextStyle.None);
            res.nameTexts[i] = Fredoka(board.transform, "Name" + i, "You", 17, Navy, new Frame(nameX[i], nameY[i], 150, 21), bf, TextAlignmentOptions.MidlineLeft);
            res.timeTexts[i] = Fredoka(board.transform, "Time" + i, "00:42.8", 15, Hex("#6B7A99"), new Frame(243, nameY[i] + 1, 80, 18), bf, TextAlignmentOptions.MidlineRight);
        }

        // Rewards
        Image coin = Unit(go, "res_reward_coin");
        res.coinReward = coin.gameObject;
        res.coinText = Lilita(coin.transform, "Amount", "+120", 26, White, new Frame(106, 628, 77, 30), UnitFrame("res_reward_coin"), TextAlignmentOptions.Center, TextStyle.Shadow(2f, 0.8f), true);
        Image gem = Unit(go, "res_reward_gem");
        res.gemReward = gem.gameObject;
        res.gemText = Lilita(gem.transform, "Amount", "+2", 26, White, new Frame(216, 628, 66, 30), UnitFrame("res_reward_gem"), TextAlignmentOptions.Center, TextStyle.Shadow(2f, 0.8f), true);

        // Claim x3 (ad) and plain claim
        RectTransform group = Container(go, "ClaimTriple");
        res.claimTripleGroup = group.gameObject;
        res.claimTripleButton = Tappable(Unit(group, "res_claim_btn"));
        Unit(group, "res_adtag");

        res.claimButton = Hit(go, "ClaimButton", new Frame(60, 776, 270, 40));
        res.claimLabel = Fredoka(go, "ClaimLabel", "No thanks, claim 120", 17, White, new Frame(60, 786, 270, 21), null, TextAlignmentOptions.Center);
        return res;
    }

    #endregion

    #region Popups made without a Figma design (reward, pause, settings, toast)

    private static Button PopupButton(Transform parent, string name, string label, string sprite, Frame logical, Frame? pf = null)
    {
        Image bg = Gen(parent, sprite, logical, name, pf, true);
        bg.raycastTarget = true;
        Button button = MakeButton(bg.gameObject, bg);
        Lilita(bg.transform, "Label", label, 22, White, logical, Grow(logical, Info(sprite).pad), TextAlignmentOptions.Center, TextStyle.Outline(1.8f, 1.5f), true);
        return button;
    }

    private static RewardPopup BuildRewardPopup(Transform root)
    {
        Frame panel = new Frame(45, 250, 300, 330);
        MakePanelRoot(root, "Reward", out RectTransform go);
        var popup = go.gameObject.AddComponent<RewardPopup>();
        Image dim = SetImage(Container(go, "Dim"), Spr("dim_black"), true);
        dim.color = new Color(0.07f, 0.04f, 0.27f, 0.72f);
        RectTransform pop = Container(go, "Popup");
        popup.popTarget = pop;
        Gen(pop, "popup_panel", panel, "Panel", null, true);
        popup.titleText = Lilita(pop, "Title", "YOU GOT", 32, White, new Frame(panel.x, panel.y + 18, panel.w, 44), null, TextAlignmentOptions.Center, TextStyle.Outline(3f, 2.5f));
        RectTransform icon = NewRect(pop, "Icon", new Frame(147, 320, 96, 96), null);
        popup.icon = SetImage(icon, null, false);
        popup.amountText = Lilita(pop, "Amount", "+100", 44, Hex("#FFD23F"), new Frame(panel.x, panel.y + 190, panel.w, 56), null, TextAlignmentOptions.Center, TextStyle.Outline(5f, 4f));
        popup.okButton = PopupButton(pop, "OkButton", "AWESOME", "btn_green", new Frame(115, 510, 160, 52));
        return popup;
    }

    private static PausePopup BuildPausePopup(Transform root)
    {
        Frame panel = new Frame(45, 230, 300, 380);
        MakePanelRoot(root, "Pause", out RectTransform go);
        var popup = go.gameObject.AddComponent<PausePopup>();
        Image dim = SetImage(Container(go, "Dim"), Spr("dim_black"), true);
        dim.color = new Color(0.07f, 0.04f, 0.27f, 0.72f);
        RectTransform pop = Container(go, "Popup");
        popup.popTarget = pop;
        Gen(pop, "popup_panel", panel, "Panel", null, true);
        Lilita(pop, "Title", "PAUSED", 34, White, new Frame(panel.x, panel.y + 18, panel.w, 46), null, TextAlignmentOptions.Center, TextStyle.Outline(3f, 2.5f));
        popup.resumeButton = PopupButton(pop, "Resume", "RESUME", "btn_green", new Frame(115, 310, 160, 52));
        popup.restartButton = PopupButton(pop, "Restart", "RESTART", "btn_yellow", new Frame(115, 385, 160, 52));
        popup.homeButton = PopupButton(pop, "Home", "HOME", "btn_blue", new Frame(115, 460, 160, 52));
        return popup;
    }

    private static SettingsPopup BuildSettingsPopup(Transform root)
    {
        Frame panel = new Frame(35, 190, 320, 470);
        MakePanelRoot(root, "Settings", out RectTransform go);
        var popup = go.gameObject.AddComponent<SettingsPopup>();
        Image dim = SetImage(Container(go, "Dim"), Spr("dim_black"), true);
        dim.color = new Color(0.07f, 0.04f, 0.27f, 0.72f);
        RectTransform pop = Container(go, "Popup");
        popup.popTarget = pop;
        Gen(pop, "popup_panel", panel, "Panel", null, true);
        Lilita(pop, "Title", "SETTINGS", 34, White, new Frame(panel.x, panel.y + 18, panel.w, 46), null, TextAlignmentOptions.Center, TextStyle.Outline(3f, 2.5f));
        popup.soundButton = PopupButton(pop, "Sound", "SOUND  ON", "btn_blue", new Frame(95, 270, 200, 52));
        popup.musicButton = PopupButton(pop, "Music", "MUSIC  ON", "btn_blue", new Frame(95, 340, 200, 52));
        popup.hapticsButton = PopupButton(pop, "Haptics", "VIBRATION  ON", "btn_blue", new Frame(95, 410, 200, 52));
        popup.restoreButton = PopupButton(pop, "Restore", "RESTORE PURCHASES", "btn_yellow", new Frame(95, 480, 200, 52));
        popup.closeButton = PopupButton(pop, "Close", "CLOSE", "btn_pink", new Frame(115, 552, 160, 52));
        popup.soundLabel = popup.soundButton.GetComponentInChildren<TMP_Text>();
        popup.musicLabel = popup.musicButton.GetComponentInChildren<TMP_Text>();
        popup.hapticsLabel = popup.hapticsButton.GetComponentInChildren<TMP_Text>();
        return popup;
    }

    private static ToastMessage BuildToast(Transform root)
    {
        Frame f = new Frame(85, 690, 220, 40);
        RectTransform toast = NewRect(root, "Toast", f, null);
        toast.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        var message = toast.gameObject.AddComponent<ToastMessage>();
        Gen(toast, "toast_pill", f, "Bg", f, true);
        message.label = Lilita(toast, "Text", "Message", 16, White, f, f, TextAlignmentOptions.Center, TextStyle.None, true);
        toast.gameObject.SetActive(false);
        return message;
    }

    #endregion

    #region Orchestration

    /// <summary>Rebuilds the entire UI in the open scene and hooks it up to GameManager. Does not save the scene.</summary>
    public static string Build()
    {
        LoadInfo();
        LoadFonts();
        if (lilita == null || fredoka == null)
        {
            return "Fonts missing. Run Aquapark > UI > Import Figma Assets first.";
        }

        var gm = Object.FindFirstObjectByType<GameManager>();
        var canvas = GameObject.Find("Canvas") != null ? GameObject.Find("Canvas").GetComponent<Canvas>() : null;
        if (gm == null || canvas == null)
        {
            return "Open gameplay.unity first (needs a Canvas and a GameManager).";
        }

        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(DW, DH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        // Match height: every screen is DH units tall, so vertical layouts never get squeezed; wider screens
        // (16:9 phones, tablets) just get more room at the sides. Full-width strips stretch to the edges (StretchX).
        scaler.matchWidthOrHeight = 1f;

        // Keep the countdown and race timer texts GameManager uses; everything else on the canvas is the old UI.
        var keep = new HashSet<Transform>();
        var gmSo = new SerializedObject(gm);
        TMP_Text raceTimer = null;
        foreach (string field in new[] { "startText", "raceTimerText" })
        {
            var component = gmSo.FindProperty(field).objectReferenceValue as TMP_Text;
            if (component == null)
            {
                continue;
            }

            // The race timer used to live inside the old HUD: lift it out so the old UI can be deleted.
            if (component.transform.parent != canvas.transform)
            {
                component.transform.SetParent(canvas.transform, false);
            }

            keep.Add(component.transform);
            StyleTimerText(component);
            component.gameObject.SetActive(false);   // GameManager switches them on when the race starts
            if (field == "raceTimerText")
            {
                raceTimer = component;
            }
        }

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.transform.GetChild(i);
            if (!keep.Contains(child))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        RectTransform root = Container(canvas.transform, "AquaUI");
        root.SetSiblingIndex(0);
        RectTransform menu = Container(root, "Menu");

        var ui = root.gameObject.AddComponent<UIManager>();
        ui.menuRoot = menu.gameObject;

        HomeScreen home = BuildHome(menu);
        ui.home = home.gameObject;
        ui.homeScreen = home;
        ui.shop = BuildShop(menu);
        ui.skins = BuildSkins(menu);
        ui.spin = BuildSpin(menu);
        ui.footer = BuildFooter(menu, home);
        ui.daily = BuildDaily(menu);

        ui.hud = BuildHud(root);
        ui.results = BuildResults(root);
        ui.settings = BuildSettingsPopup(root);
        ui.reward = BuildRewardPopup(root);
        ui.pause = BuildPausePopup(root);
        ui.qualifier = BuildQualifier(root);
        ui.toast = BuildToast(root);

        if (raceTimer != null)
        {
            // Small race clock under the pause button.
            var rt = (RectTransform)raceTimer.transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 26f);
            rt.anchoredPosition = new Vector2(18f - DW * 0.5f, -124f);
            rt.localScale = Vector3.one;
            raceTimer.alignment = TextAlignmentOptions.MidlineLeft;
            raceTimer.fontSize = 20f;
            raceTimer.enableAutoSizing = false;
            raceTimer.fontSharedMaterial = TextMaterial(lilita, TextStyle.Outline(2f, 1.5f), 20f);
            raceTimer.color = White;
            raceTimer.textWrappingMode = TextWrappingModes.NoWrap;
        }

        ui.skinDatabase = AssetDatabase.LoadAssetAtPath<SkinDatabase>(DataDir + "/SkinDatabase.asset");
        ui.floatieDatabase = AssetDatabase.LoadAssetAtPath<SkinDatabase>(DataDir + "/FloatieDatabase.asset");
        ui.coinSprite = Spr("icon_coin");
        ui.gemSprite = Spr("icon_gem");

        // The footer's Play button is GameManager's Start Button.
        gmSo.FindProperty("startButton").objectReferenceValue = ui.footer.playButton;
        gmSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        return "Aqua Race UI built. Save the scene to keep it.";
    }

    private static void StyleTimerText(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        text.font = lilita;
        text.fontSharedMaterial = TextMaterial(lilita, TextStyle.Outline(5f, 4f), Mathf.Max(24f, text.fontSize));
        text.color = Hex("#FFF36B");
        EditorUtility.SetDirty(text);
    }

    #endregion
}
