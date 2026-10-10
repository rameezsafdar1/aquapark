using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class AquaUIBuilder
{
    public const string PacketDir = "Assets/UI/MissionPacket";
    private const string PacketBodyPath = WelcomeSourceDir + "/Packet.png";
    private const string PacketTopSource = WelcomeSourceDir + "/packet top.png";
    private const string PacketTopPath = PacketDir + "/mission_packet_top.png";
    // card.png with its painted-in checkerboard removed (cut out once, outside Unity) and the glow behind the card.
    private const string PacketCardPath = PacketDir + "/mission_card.png";
    private const string PacketRaysPath = PacketDir + "/mission_rays.png";

    // Packet.png and 'packet top.png' share one 959 x 1423 canvas; the top's art is in its first PacketTopRows rows.
    private const float PacketW = 959f, PacketH = 1423f;
    private const int PacketTopRows = 264;

    // The cut-out card's size; the texts below are placed in its pixels (top-left origin).
    private const float MCardW = 1019f, MCardH = 1478f;

    private const float PacketHeight = 470f;   // design px
    private const float MissionCardWidth = 300f;

    /// <summary>
    /// The mission reward reveal: dark backdrop, glow rays, the packet (card inside, behind the packet picture, the top on
    /// its own so it can tear off), a white flash, "TAP TO CONTINUE" and a full-screen tap area. MissionPacketScreen animates it.
    /// </summary>
    private static MissionPacketScreen BuildMissionPacket(Transform root)
    {
        MakePacketArt();

        MakePanelRoot(root, "MissionPacket", out RectTransform page);
        var screen = page.gameObject.AddComponent<MissionPacketScreen>();

        Image backdrop = SetImage(Container(page, "Backdrop"), null, true);
        backdrop.color = new Color(0.02f, 0.07f, 0.2f, 0f);
        screen.backdrop = backdrop;

        RectTransform rays = Centered(page, "Rays", new Vector2(680f, 680f), new Vector2(0f, screen.cardFinalY));
        SetImage(rays, AssetDatabase.LoadAssetAtPath<Sprite>(PacketRaysPath), false).color = new Color(1f, 1f, 1f, 0f);
        screen.rays = rays;

        RectTransform packet = Centered(page, "Packet", new Vector2(PacketHeight * PacketW / PacketH, PacketHeight), new Vector2(0f, screen.packetY));
        screen.packet = packet;
        screen.packetGroup = packet.gameObject.AddComponent<CanvasGroup>();
        screen.packetGroup.blocksRaycasts = false;

        // Card first, so it sits behind the packet picture until it slides out.
        RectTransform card = Centered(packet, "Card", new Vector2(MissionCardWidth, MissionCardWidth * MCardH / MCardW), new Vector2(0f, screen.cardInPacketY));
        card.localScale = Vector3.one * screen.cardInPacketScale;
        SetImage(card, AssetDatabase.LoadAssetAtPath<Sprite>(PacketCardPath), false).preserveAspect = true;
        screen.card = card;

        TextMeshProUGUI title = Lilita(card, "Title", "MISSION COMPLETE!", 26f, White, new Frame(0, 0, 10, 10), null,
            TextAlignmentOptions.Center, TextStyle.Outline(2.5f, 2f), true);
        title.fontSizeMin = 12f;
        OnMissionCard(title.rectTransform, 200f, 268f, 820f, 352f);
        screen.titleText = title;

        Image icon = SetImage(Container(card, "Icon"), Spr("icon_coin"), false);
        icon.preserveAspect = true;
        OnMissionCard(icon.rectTransform, 300f, 470f, 720f, 900f);
        screen.rewardIcon = icon;

        TextMeshProUGUI amount = Lilita(card, "Amount", "+100", 52f, White, new Frame(0, 0, 10, 10), null,
            TextAlignmentOptions.Center, TextStyle.Outline(4f, 3f), true);
        amount.fontSizeMin = 18f;
        OnMissionCard(amount.rectTransform, 150f, 930f, 870f, 1085f);
        screen.amountText = amount;

        TextMeshProUGUI label = Lilita(card, "Label", "COINS", 26f, new Color32(0xFF, 0xE4, 0x5C, 255), new Frame(0, 0, 10, 10), null,
            TextAlignmentOptions.Center, TextStyle.Outline(2.5f, 2f), true);
        label.fontSizeMin = 12f;
        OnMissionCard(label.rectTransform, 150f, 1085f, 870f, 1170f);
        screen.labelText = label;

        Image body = SetImage(Container(packet, "Body"), AssetDatabase.LoadAssetAtPath<Sprite>(PacketBodyPath), false);
        body.preserveAspect = true;

        // The top band, placed exactly where it is on the shared canvas.
        RectTransform top = Container(packet, "Top");
        top.anchorMin = new Vector2(0f, 1f - PacketTopRows / PacketH);
        top.anchorMax = Vector2.one;
        top.offsetMin = top.offsetMax = Vector2.zero;
        SetImage(top, AssetDatabase.LoadAssetAtPath<Sprite>(PacketTopPath), false);
        screen.packetTop = top;
        screen.packetTopGroup = top.gameObject.AddComponent<CanvasGroup>();

        Image flash = SetImage(Container(page, "Flash"), null, false);
        flash.color = new Color(1f, 1f, 1f, 0f);
        screen.flash = flash;

        TextMeshProUGUI tap = Lilita(page, "TapText", "TAP TO CONTINUE", 22f, White, new Frame(0, 0, 10, 10), null,
            TextAlignmentOptions.Center, TextStyle.Outline(2.5f, 2f));
        RectTransform tr = tap.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
        tr.sizeDelta = new Vector2(320f, 40f);
        tr.anchoredPosition = new Vector2(0f, 110f);
        tap.alpha = 0f;
        screen.tapText = tap;

        Image hit = SetImage(Container(page, "TapArea"), null, true);
        hit.color = new Color(1f, 1f, 1f, 0f);
        screen.tapArea = MakeButton(hit.gameObject, hit, false);

        screen.coinIcon = Spr("icon_coin");
        screen.gemIcon = Spr("icon_gem");
        screen.spinIcon = Spr("spin_wheel");
        screen.boostIcon = Spr("spin_prize4");

        EditorUtility.SetDirty(screen);
        page.gameObject.SetActive(false);   // UIManager opens it; keep the scene showing the menu
        return screen;
    }

    private static RectTransform Centered(Transform parent, string name, Vector2 size, Vector2 position)
    {
        RectTransform rt = Container(parent, name);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    /// <summary>Anchors a rect to a box in cut-out card pixels (top-left origin), so it scales with the card.</summary>
    private static void OnMissionCard(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2(x0 / MCardW, 1f - y1 / MCardH);
        rt.anchorMax = new Vector2(x1 / MCardW, 1f - y0 / MCardH);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Cuts the packet's top band out of 'packet top.png' (full width, so it lines up with Packet.png) and sets the sprites up.</summary>
    private static void MakePacketArt()
    {
        Directory.CreateDirectory(PacketDir);
        if (IsStale(PacketTopPath, PacketTopSource))
        {
            Texture2D tex = LoadPng(PacketTopSource);
            int w = tex.width, h = tex.height, rows = Mathf.Min(PacketTopRows, h);
            Color32[] all = tex.GetPixels32();
            var px = new Color32[w * rows];
            // Texture rows go bottom-up: the top band is the last rows.
            System.Array.Copy(all, (h - rows) * w, px, 0, w * rows);
            SavePng(PacketTopPath, px, w, rows);
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();
        }

        foreach (string path in new[] { PacketTopPath, PacketCardPath, PacketRaysPath })
        {
            SetupWelcomeSprite(path);
        }

        if (!File.Exists(PacketCardPath) || !File.Exists(PacketRaysPath))
        {
            Debug.LogWarning("AquaUI: " + PacketCardPath + " or " + PacketRaysPath + " is missing.");
        }
    }

    [MenuItem("Aquapark/UI/Rebuild Mission Packet Only")]
    public static void RebuildMissionPacketMenu()
    {
        Debug.Log(RebuildMissionPacket());
    }

    /// <summary>Replaces only the Mission Packet panel in the open scene; every other panel is left exactly as it is.</summary>
    public static string RebuildMissionPacket()
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

        // Just above the reward popup, below the toast.
        Transform old = root.Find("MissionPacket");
        int index = old != null ? old.GetSiblingIndex() : ui.reward != null ? ui.reward.transform.GetSiblingIndex() + 1 : root.childCount;
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        MissionPacketScreen screen = BuildMissionPacket(root);
        screen.transform.SetSiblingIndex(index);
        ui.missionPacket = screen;
        EditorUtility.SetDirty(ui);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.scene);
        return "Mission Packet panel rebuilt. Save the scene to keep it.";
    }
}
