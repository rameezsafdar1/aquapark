using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shop page: starter pack, three gem packs, remove ads and free coins for watching an ad.
/// Money-for-gems purchases go through PurchaseService (instant until a store is hooked up).
/// </summary>
public class ShopScreen : UIPanel
{
    public const int StarterCoins = 5000;
    public const int StarterGems = 100;
    public const int FreeCoinsAmount = 100;

    public Button backButton;
    public SegmentedControl tabs;
    public TMP_Text starterTimerText;
    public Button starterBuy;
    public Button pack80Buy;
    public Button pack500Buy;
    public Button pack1200Buy;
    public Button removeAdsBuy;
    public Button freeCoinsWatch;
    [Tooltip("Cards that get dimmed once bought.")]
    public Graphic starterCard;
    public Graphic removeAdsCard;

    private void Awake()
    {
        backButton.onClick.AddListener(() => UIManager.Instance.CloseCurrentPage());
        tabs.Changed += OnTabChanged;
        starterBuy.onClick.AddListener(BuyStarter);
        pack80Buy.onClick.AddListener(() => BuyGems("gems_80", 80));
        pack500Buy.onClick.AddListener(() => BuyGems("gems_500", 500));
        pack1200Buy.onClick.AddListener(() => BuyGems("gems_1200", 1200));
        removeAdsBuy.onClick.AddListener(BuyRemoveAds);
        freeCoinsWatch.onClick.AddListener(WatchForCoins);
    }

    protected override void OnShown()
    {
        tabs.Select(0);
        RefreshState();
    }

    private void Update()
    {
        starterTimerText.text = UIFormat.Clock(UIFormat.UntilNextDay());
    }

    private void OnTabChanged(int index)
    {
        if (index == 2)
        {
            UIManager.Instance.ShowToast("Coin packs are coming soon");
            tabs.Select(0);
        }
    }

    private void RefreshState()
    {
        SetBought(starterCard, starterBuy, SaveData.StarterPackBought);
        SetBought(removeAdsCard, removeAdsBuy, SaveData.NoAds);
    }

    private static void SetBought(Graphic card, Button button, bool bought)
    {
        card.color = bought ? new Color(0.55f, 0.55f, 0.6f, 1f) : Color.white;
        button.interactable = !bought;
    }

    private void BuyStarter()
    {
        PurchaseService.Purchase("starter_pack", () =>
        {
            CurrencyManager.Add(RewardType.Coins, StarterCoins);
            CurrencyManager.Add(RewardType.Gems, StarterGems);
            SaveData.StarterPackBought = true;

            // The pack also contains a skin: the first one the player does not own yet.
            SkinDatabase database = UIManager.Instance.Skins;
            if (database != null)
            {
                foreach (SkinData skin in database.skins)
                {
                    if (!SkinManager.IsOwned(skin))
                    {
                        SkinManager.Grant(skin);
                        break;
                    }
                }
            }

            RefreshState();
            UIManager.Instance.ShowReward(RewardType.Coins, StarterCoins, "STARTER PACK");
        });
    }

    private void BuyGems(string productId, int amount)
    {
        PurchaseService.Purchase(productId, () =>
        {
            CurrencyManager.Add(RewardType.Gems, amount);
            UIManager.Instance.ShowReward(RewardType.Gems, amount);
        });
    }

    private void BuyRemoveAds()
    {
        PurchaseService.Purchase("remove_ads", () =>
        {
            SaveData.NoAds = true;
            RefreshState();
            UIManager.Instance.ShowToast("Ads removed. Thank you!");
        });
    }

    private void WatchForCoins()
    {
        AdService.ShowRewarded(() =>
        {
            CurrencyManager.Add(RewardType.Coins, FreeCoinsAmount);
            UIManager.Instance.ShowReward(RewardType.Coins, FreeCoinsAmount);
        });
    }
}
