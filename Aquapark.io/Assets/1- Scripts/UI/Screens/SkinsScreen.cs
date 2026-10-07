using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The skins page: a big preview of the selected item, a Characters / Floaties tab bar with one card grid per tab,
/// and the Equip / Buy button.
/// </summary>
public class SkinsScreen : UIPanel
{
    public Button backButton;
    public SegmentedControl tabs;
    [Tooltip("Cards of the Characters tab.")]
    public SkinCardView[] cards;
    [Tooltip("Cards of the Floaties tab.")]
    public SkinCardView[] floatieCards = new SkinCardView[0];
    public GameObject characterPage;
    public GameObject floatiePage;
    public ScrollRect gridScroll;
    [Tooltip("Preview size = icon size in design px times this.")]
    public float previewScale = 3.6f;
    [Tooltip("Largest preview width in design px, so wide items (floaties) stay clear of the arrows.")]
    public float previewMaxWidth = 140f;
    public Image previewArt;
    [Tooltip("Live 3D model of the selected item. When it has a model, the picture (previewArt) is hidden.")]
    public SkinPreview3D preview3D;
    public TMP_Text nameText;
    public GameObject epicTag;
    public Button arrowLeft;
    public Button arrowRight;
    public Button actionButton;
    public TMP_Text actionLabel;
    public Graphic actionGraphic;

    private SkinData[] skins = new SkinData[0];
    private SkinCardView[] activeCards = new SkinCardView[0];
    private int selected;

    private void Awake()
    {
        backButton.onClick.AddListener(() => UIManager.Instance.CloseCurrentPage());
        arrowLeft.onClick.AddListener(() => Select(selected - 1));
        arrowRight.onClick.AddListener(() => Select(selected + 1));
        actionButton.onClick.AddListener(OnAction);
        tabs.Changed += OnTabChanged;

        foreach (SkinCardView[] grid in new[] { cards, floatieCards })
        {
            for (int i = 0; i < grid.Length; i++)
            {
                int index = i;
                grid[i].button.onClick.AddListener(() => Select(index));
            }
        }
    }

    protected override void OnShown()
    {
        tabs.Select(0);
        ShowTab(0);
    }

    private void OnTabChanged(int index)
    {
        ShowTab(index);
    }

    /// <summary>0 = Characters, 1 = Floaties. Swaps the grid page and selects the equipped item of that tab.</summary>
    private void ShowTab(int index)
    {
        bool floaties = index == 1;
        SkinDatabase database = floaties ? UIManager.Instance.Floaties : UIManager.Instance.Skins;
        skins = database != null && database.skins != null ? database.skins : new SkinData[0];
        activeCards = floaties ? floatieCards : cards;

        if (characterPage != null) characterPage.SetActive(!floaties);
        if (floatiePage != null) floatiePage.SetActive(floaties);
        if (gridScroll != null)
        {
            gridScroll.content = (RectTransform)(floaties ? floatiePage : characterPage).transform;
            gridScroll.StopMovement();
            gridScroll.verticalNormalizedPosition = 1f;
        }

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

    /// <summary>Selects an item; an owned item is equipped straight away (no EQUIP press needed).</summary>
    private void Select(int index)
    {
        if (skins.Length == 0)
        {
            return;
        }

        selected = (index % skins.Length + skins.Length) % skins.Length;
        SkinData skin = skins[selected];
        if (SkinManager.IsOwned(skin) && !SkinManager.IsEquipped(skin))
        {
            SkinManager.Equip(skin);
            UIManager.Instance.ShowToast(skin.displayName + " equipped");
        }

        RefreshAll();
    }

    private void RefreshAll()
    {
        for (int i = 0; i < activeCards.Length; i++)
        {
            bool has = i < skins.Length;
            activeCards[i].gameObject.SetActive(has);
            if (has)
            {
                activeCards[i].Bind(skins[i], i == selected);
            }
        }

        if (skins.Length == 0)
        {
            return;
        }

        SkinData skin = skins[selected];
        bool live = preview3D != null && preview3D.Show(skin);
        previewArt.sprite = skin.icon;
        previewArt.enabled = !live && skin.icon != null;
        if (skin.icon != null)
        {
            previewArt.rectTransform.sizeDelta = PreviewSize(skin.icon);
        }

        nameText.text = skin.displayName.ToUpperInvariant();
        epicTag.SetActive(skin.rarity == SkinRarity.Epic || skin.rarity == SkinRarity.Legendary);

        // Owned items are equipped on select, so the button is only for locked ones.
        bool owned = SkinManager.IsOwned(skin);
        actionButton.gameObject.SetActive(!owned);
        // The label is a sibling of the button (not a child), so it must be hidden separately.
        actionLabel.gameObject.SetActive(!owned);
        if (!owned)
        {
            switch (skin.unlock)
            {
                case SkinUnlock.RewardedAd: actionLabel.text = "WATCH AD"; break;
                case SkinUnlock.PlayerLevel: actionLabel.text = "LV " + skin.unlockLevel; break;
                default: actionLabel.text = "BUY"; break;
            }
        }
    }

    /// <summary>Icon size in design px times previewScale, shrunk to fit previewMaxWidth.</summary>
    public Vector2 PreviewSize(Sprite icon)
    {
        Vector2 size = new Vector2(icon.rect.width, icon.rect.height) / icon.pixelsPerUnit * previewScale;
        return size.x > previewMaxWidth ? size * (previewMaxWidth / size.x) : size;
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
            return;
        }

        switch (skin.unlock)
        {
            case SkinUnlock.RewardedAd:
                AdService.ShowRewarded(() =>
                {
                    SkinManager.Grant(skin);
                    OnUnlocked(skin);
                });
                break;

            case SkinUnlock.PlayerLevel:
                // Level items unlock by themselves when the level is reached (SkinManager.CheckLevelUnlocks).
                UIManager.Instance.ShowToast("Reach level " + skin.unlockLevel + " to unlock");
                break;

            case SkinUnlock.Free:
                SkinManager.Grant(skin);
                OnUnlocked(skin);
                break;

            default:
                if (SkinManager.TryBuy(skin))
                {
                    OnUnlocked(skin);
                }
                else
                {
                    UIManager.Instance.ShowToast(skin.currency == RewardType.Coins ? "Not enough coins" : "Not enough gems");
                }

                break;
        }
    }

    /// <summary>After a purchase or ad: the item stays selected and is equipped straight away.</summary>
    private void OnUnlocked(SkinData skin)
    {
        int index = System.Array.IndexOf(skins, skin);
        if (index >= 0)
        {
            selected = index;
        }

        SkinManager.Equip(skin);
        UIManager.Instance.ShowToast(skin.displayName + " unlocked and equipped!");
        RefreshAll();
    }
}
