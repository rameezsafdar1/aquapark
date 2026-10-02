using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The 7-day daily reward calendar. One claim per UTC day; claiming with a rewarded ad (x2) doubles the reward.
/// Days 1-5 give coins or gems, day 6 gives 2 extra wheel spins, day 7 is a chest with coins, gems and a skin.
/// </summary>
public class DailyPopup : UIPanel
{
    private enum Kind
    {
        Coins,
        Gems,
        Spins,
        Chest
    }

    private struct Reward
    {
        public Kind kind;
        public int amount;

        public Reward(Kind kind, int amount)
        {
            this.kind = kind;
            this.amount = amount;
        }
    }

    private static readonly Reward[] Table =
    {
        new Reward(Kind.Coins, 100),
        new Reward(Kind.Gems, 5),
        new Reward(Kind.Coins, 250),
        new Reward(Kind.Gems, 10),
        new Reward(Kind.Coins, 500),
        new Reward(Kind.Spins, 2),
        new Reward(Kind.Chest, 0)
    };

    private const int ChestCoins = 1000;
    private const int ChestGems = 50;

    public DayCardView[] days = new DayCardView[7];
    public Button claimButton;
    public Button doubleButton;
    public Button closeButton;
    public Graphic claimGraphic;
    public Graphic doubleGraphic;
    public GameObject adTag;
    public GameObject nextChip;
    public TMP_Text nextText;

    private void Awake()
    {
        claimButton.onClick.AddListener(() => Claim(1));
        doubleButton.onClick.AddListener(() => AdService.ShowRewarded(() => Claim(2)));
        closeButton.onClick.AddListener(() => UIManager.Instance.CloseDaily());
    }

    protected override void OnShown()
    {
        Refresh();
    }

    private void Update()
    {
        if (nextChip.activeSelf)
        {
            nextText.text = "Next reward in " + UIFormat.Clock(UIFormat.UntilNextDay());
        }
    }

    private void Refresh()
    {
        bool canClaim = DailyRewardState.CanClaim;
        int claimed = DailyRewardState.ClaimedCount;
        int next = DailyRewardState.NextIndex;

        for (int i = 0; i < days.Length; i++)
        {
            DayCardView.DayState state = i < claimed ? DayCardView.DayState.Claimed
                : (canClaim && i == next) ? DayCardView.DayState.Today
                : DayCardView.DayState.Upcoming;
            days[i].Set(state);
            days[i].dayText.text = "DAY " + (i + 1);
            if (days[i].amountText != null)
            {
                days[i].amountText.text = Label(Table[i]);
            }
        }

        claimButton.interactable = canClaim;
        doubleButton.interactable = canClaim;
        claimGraphic.color = canClaim ? Color.white : new Color(0.6f, 0.6f, 0.65f, 1f);
        doubleGraphic.color = canClaim ? Color.white : new Color(0.6f, 0.6f, 0.65f, 1f);
        adTag.SetActive(canClaim);
        nextChip.SetActive(!canClaim);
    }

    private static string Label(Reward reward)
    {
        switch (reward.kind)
        {
            case Kind.Spins: return "x" + reward.amount + " SPINS";
            case Kind.Chest: return "";
            default: return UIFormat.Number(reward.amount);
        }
    }

    private void Claim(int multiplier)
    {
        if (!DailyRewardState.CanClaim)
        {
            return;
        }

        int index = DailyRewardState.NextIndex;
        Reward reward = Table[index];
        DailyRewardState.MarkClaimed(index);
        Refresh();

        UIManager ui = UIManager.Instance;
        switch (reward.kind)
        {
            case Kind.Coins:
                CurrencyManager.Add(RewardType.Coins, reward.amount * multiplier);
                ui.ShowReward(RewardType.Coins, reward.amount * multiplier, "DAILY REWARD");
                break;

            case Kind.Gems:
                CurrencyManager.Add(RewardType.Gems, reward.amount * multiplier);
                ui.ShowReward(RewardType.Gems, reward.amount * multiplier, "DAILY REWARD");
                break;

            case Kind.Spins:
                SaveData.BonusSpins += reward.amount * multiplier;
                ui.ShowToast("+" + reward.amount * multiplier + " free spins!");
                break;

            default:
                CurrencyManager.Add(RewardType.Coins, ChestCoins * multiplier);
                CurrencyManager.Add(RewardType.Gems, ChestGems * multiplier);
                GrantChestSkin();
                ui.ShowReward(RewardType.Coins, ChestCoins * multiplier, "MEGA CHEST");
                break;
        }
    }

    /// <summary>The chest holds a skin: the best one the player does not own yet.</summary>
    private static void GrantChestSkin()
    {
        SkinDatabase database = UIManager.Instance.Skins;
        if (database == null)
        {
            return;
        }

        SkinData best = null;
        foreach (SkinData skin in database.skins)
        {
            if (!SkinManager.IsOwned(skin) && (best == null || skin.rarity > best.rarity))
            {
                best = skin;
            }
        }

        if (best != null)
        {
            SkinManager.Grant(best);
        }
    }
}
