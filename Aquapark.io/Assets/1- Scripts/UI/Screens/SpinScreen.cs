using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lucky spin page. One free spin a day (plus bonus spins from the daily reward); more with a rewarded ad.
/// Slice 0 is at the top of the wheel and the slices go clockwise.
/// </summary>
public class SpinScreen : UIPanel
{
    private enum Kind
    {
        Coins,
        Gems,
        Boost,
        Skin
    }

    private struct Prize
    {
        public Kind kind;
        public int amount;
        public int weight;

        public Prize(Kind kind, int amount, int weight)
        {
            this.kind = kind;
            this.amount = amount;
            this.weight = weight;
        }
    }

    // Same order as the pictures on the wheel in the design.
    private static readonly Prize[] Prizes =
    {
        new Prize(Kind.Coins, 100, 24),
        new Prize(Kind.Gems, 5, 18),
        new Prize(Kind.Coins, 500, 6),
        new Prize(Kind.Boost, 2, 12),
        new Prize(Kind.Gems, 20, 8),
        new Prize(Kind.Coins, 250, 16),
        new Prize(Kind.Skin, 1, 4),
        new Prize(Kind.Gems, 100, 1)
    };

    private const int SkinFallbackCoins = 500;

    public RectTransform wheel;
    public Button backButton;
    public Button freeButton;
    public Button adButton;
    public Button hubButton;
    public Graphic freeGraphic;
    public TMP_Text freeTagText;
    public GameObject nextChip;
    public TMP_Text nextText;

    private bool spinning;

    private void Awake()
    {
        backButton.onClick.AddListener(() =>
        {
            if (!spinning)
            {
                UIManager.Instance.CloseCurrentPage();
            }
        });
        freeButton.onClick.AddListener(OnFreeSpin);
        hubButton.onClick.AddListener(OnFreeSpin);
        adButton.onClick.AddListener(() =>
        {
            if (!spinning)
            {
                AdService.ShowRewarded(() => StartSpin());
            }
        });
    }

    protected override void OnShown()
    {
        Refresh();
    }

    private void Update()
    {
        if (nextChip.activeSelf)
        {
            nextText.text = "Next free spin in " + UIFormat.Clock(UIFormat.UntilNextDay());
        }
    }

    private static int FreeSpins => (SpinState.FreeSpinAvailable ? 1 : 0) + SaveData.BonusSpins;

    private void Refresh()
    {
        int free = FreeSpins;
        freeTagText.text = free + " FREE SPIN" + (free == 1 ? "" : "S") + " LEFT";
        freeGraphic.color = free > 0 && !spinning ? Color.white : new Color(0.6f, 0.6f, 0.65f, 1f);
        nextChip.SetActive(free == 0);
    }

    private void OnFreeSpin()
    {
        if (spinning)
        {
            return;
        }

        if (FreeSpins <= 0)
        {
            UIManager.Instance.ShowToast("No free spins left. Watch an ad to spin again!");
            return;
        }

        if (SpinState.FreeSpinAvailable)
        {
            SpinState.MarkFreeSpinUsed();
        }
        else
        {
            SaveData.BonusSpins -= 1;
        }

        StartSpin();
    }

    private void StartSpin()
    {
        spinning = true;
        Refresh();

        int slice = PickPrize();
        // Slice i sits i * 45 degrees clockwise from the top; turn the wheel clockwise until it is under the pointer.
        float jitter = Random.Range(-16f, 16f);
        float turn = 360f * 5f + ((360f - slice * 45f) % 360f) + jitter;
        float startZ = wheel.localEulerAngles.z;

        wheel.DOKill();
        DOTween.To(() => 0f, v => wheel.localEulerAngles = new Vector3(0f, 0f, startZ - v), turn, 4.2f)
            .SetEase(Ease.OutQuart)
            .SetUpdate(true)
            .SetTarget(wheel)
            .OnComplete(() => Finish(slice));
    }

    private static int PickPrize()
    {
        int total = 0;
        foreach (Prize prize in Prizes)
        {
            total += prize.weight;
        }

        int roll = Random.Range(0, total);
        for (int i = 0; i < Prizes.Length; i++)
        {
            roll -= Prizes[i].weight;
            if (roll < 0)
            {
                return i;
            }
        }

        return 0;
    }

    private void Finish(int slice)
    {
        spinning = false;
        Prize prize = Prizes[slice];
        UIManager ui = UIManager.Instance;

        switch (prize.kind)
        {
            case Kind.Coins:
                CurrencyManager.Add(RewardType.Coins, prize.amount);
                ui.ShowReward(RewardType.Coins, prize.amount, "YOU WON");
                break;

            case Kind.Gems:
                CurrencyManager.Add(RewardType.Gems, prize.amount);
                ui.ShowReward(RewardType.Gems, prize.amount, "YOU WON");
                break;

            case Kind.Boost:
                SaveData.CoinBoostRaces += 1;
                ui.ShowToast("Next race pays double coins!");
                break;

            default:
                GrantSkin();
                break;
        }

        Refresh();
    }

    private void GrantSkin()
    {
        SkinDatabase database = UIManager.Instance.Skins;
        if (database != null)
        {
            foreach (SkinData skin in database.skins)
            {
                if (!SkinManager.IsOwned(skin))
                {
                    SkinManager.Grant(skin);
                    UIManager.Instance.ShowToast("New skin: " + skin.displayName + "!");
                    return;
                }
            }
        }

        // The player already owns everything.
        CurrencyManager.Add(RewardType.Coins, SkinFallbackCoins);
        UIManager.Instance.ShowReward(RewardType.Coins, SkinFallbackCoins, "YOU WON");
    }
}
