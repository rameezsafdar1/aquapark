using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The skins page: a big preview of the selected skin, the grid of all skins, and the Equip / Buy button.</summary>
public class SkinsScreen : UIPanel
{
    public Button backButton;
    public SegmentedControl tabs;
    public SkinCardView[] cards;
    public Image previewArt;
    public TMP_Text nameText;
    public GameObject epicTag;
    public Button arrowLeft;
    public Button arrowRight;
    public Button actionButton;
    public TMP_Text actionLabel;
    public Graphic actionGraphic;

    private SkinData[] skins = new SkinData[0];
    private int selected;

    private void Awake()
    {
        backButton.onClick.AddListener(() => UIManager.Instance.CloseCurrentPage());
        arrowLeft.onClick.AddListener(() => Select(selected - 1));
        arrowRight.onClick.AddListener(() => Select(selected + 1));
        actionButton.onClick.AddListener(OnAction);
        tabs.Changed += OnTabChanged;

        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            cards[i].button.onClick.AddListener(() => Select(index));
        }
    }

    protected override void OnShown()
    {
        SkinDatabase database = UIManager.Instance.Skins;
        skins = database != null && database.skins != null ? database.skins : new SkinData[0];
        tabs.Select(0);

        selected = 0;
        for (int i = 0; i < skins.Length; i++)
        {
            if (SkinManager.IsEquipped(skins[i]))
            {
                selected = i;
            }
        }

        RefreshAll();
    }

    private void OnTabChanged(int index)
    {
        if (index != 0)
        {
            UIManager.Instance.ShowToast(index == 1 ? "Hats are coming soon" : "Trails are coming soon");
            tabs.Select(0);
        }
    }

    private void Select(int index)
    {
        if (skins.Length == 0)
        {
            return;
        }

        selected = (index % skins.Length + skins.Length) % skins.Length;
        RefreshAll();
    }

    private void RefreshAll()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            bool has = i < skins.Length;
            cards[i].gameObject.SetActive(has);
            if (has)
            {
                cards[i].Bind(skins[i], i == selected);
            }
        }

        if (skins.Length == 0)
        {
            return;
        }

        SkinData skin = skins[selected];
        previewArt.sprite = skin.icon;
        previewArt.enabled = skin.icon != null;
        if (skin.icon != null)
        {
            previewArt.rectTransform.sizeDelta = new Vector2(skin.icon.rect.width, skin.icon.rect.height) / 3f * 2.6f;
        }

        nameText.text = skin.displayName.ToUpperInvariant();
        epicTag.SetActive(skin.rarity == SkinRarity.Epic || skin.rarity == SkinRarity.Legendary);

        bool owned = SkinManager.IsOwned(skin);
        bool equipped = SkinManager.IsEquipped(skin);
        if (owned)
        {
            actionLabel.text = equipped ? "EQUIPPED" : "EQUIP";
        }
        else
        {
            switch (skin.unlock)
            {
                case SkinUnlock.RewardedAd: actionLabel.text = "WATCH AD"; break;
                case SkinUnlock.PlayerLevel: actionLabel.text = "LV " + skin.unlockLevel; break;
                default: actionLabel.text = "BUY"; break;
            }
        }

        actionGraphic.color = equipped ? new Color(0.7f, 0.7f, 0.75f, 1f) : Color.white;
    }

    private void OnAction()
    {
        if (skins.Length == 0)
        {
            return;
        }

        SkinData skin = skins[selected];

        if (SkinManager.IsOwned(skin))
        {
            if (!SkinManager.IsEquipped(skin))
            {
                SkinManager.Equip(skin);
                UIManager.Instance.ShowToast(skin.displayName + " equipped");
                RefreshAll();
            }

            return;
        }

        switch (skin.unlock)
        {
            case SkinUnlock.RewardedAd:
                AdService.ShowRewarded(() =>
                {
                    SkinManager.Grant(skin);
                    SkinManager.Equip(skin);
                    RefreshAll();
                });
                break;

            case SkinUnlock.PlayerLevel:
                if (SkinManager.MeetsLevel(skin))
                {
                    SkinManager.Grant(skin);
                    SkinManager.Equip(skin);
                    RefreshAll();
                }
                else
                {
                    UIManager.Instance.ShowToast("Reach level " + skin.unlockLevel + " to unlock");
                }

                break;

            default:
                if (SkinManager.TryBuy(skin))
                {
                    SkinManager.Equip(skin);
                    UIManager.Instance.ShowToast(skin.displayName + " unlocked!");
                    RefreshAll();
                }
                else
                {
                    UIManager.Instance.ShowToast(skin.currency == RewardType.Coins ? "Not enough coins" : "Not enough gems");
                }

                break;
        }
    }
}
