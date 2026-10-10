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
        // Slice i sits i * 45 degrees clockwise from the top, so it is under the (top) pointer when the wheel's angle is
        // i * 45 (mod 360). Aim for that absolute angle: the wheel keeps the angle of the previous spin, and turning a fixed
        // amount from there (as before) stopped it on a different slice from the one that was paid out.
        float jitter = Random.Range(-16f, 16f);   // stays well inside the 45-degree slice
        float startZ = wheel.localEulerAngles.z;
        float targetZ = slice * 45f + jitter;
        float turn = 360f * 5f + Mathf.Repeat(startZ - targetZ, 360f);

        wheel.DOKill();
        DOTween.To(() => 0f, v => wheel.localEulerAngles = new Vector3(0f, 0f, startZ - v), turn, 4.2f)
            .SetEase(Ease.OutQuart)
            .SetUpdate(true)
            .SetTarget(wheel)
            .OnComplete(() => Finish(SliceUnderPointer()));
    }

    /// <summary>The slice the pointer is actually on, read from the wheel's final angle (this is what gets paid out).</summary>
    private int SliceUnderPointer()
    {
        float angle = Mathf.Repeat(wheel.localEulerAngles.z, 360f);
        return Mathf.RoundToInt(angle / 45f) % Prizes.Length;
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
                ShowCurrencyPrize(RewardType.Coins, prize.amount);
                break;

            case Kind.Gems:
                CurrencyManager.Add(RewardType.Gems, prize.amount);
                ShowCurrencyPrize(RewardType.Gems, prize.amount);
                break;

            case Kind.Boost:
                SaveData.CoinBoostRaces += 1;
                if (ui.missionPacket != null)
                {
                    ui.OpenRewardPacket(ui.missionPacket.boostIcon, "DOUBLE COINS", "NEXT RACE", ui.missionPacket.spinTitle);
                }
                else
                {
                    ui.ShowToast("Next race pays double coins!");
                }
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
                    UIManager ui = UIManager.Instance;
                    if (ui.missionPacket != null)
                    {
                        ui.OpenRewardPacket(skin.icon, skin.displayName.ToUpperInvariant(),
                            skin.slot == SkinSlot.Floatie ? "NEW FLOATIE" : "NEW CHARACTER", ui.missionPacket.spinTitle);
                    }
                    else
                    {
                        ui.ShowToast("New skin: " + skin.displayName + "!");
                    }
                    return;
                }
            }
        }

        // The player already owns everything.
        CurrencyManager.Add(RewardType.Coins, SkinFallbackCoins);
        ShowCurrencyPrize(RewardType.Coins, SkinFallbackCoins);
    }

    /// <summary>Coins / gems prize: out of the reward packet, or the plain popup if the scene has no packet screen.</summary>
    private static void ShowCurrencyPrize(RewardType type, int amount)
    {
        UIManager ui = UIManager.Instance;
        MissionPacketScreen packet = ui.missionPacket;
        if (packet == null)
        {
            ui.ShowReward(type, amount, "YOU WON");
            return;
        }

        bool coins = type == RewardType.Coins;
        ui.OpenRewardPacket(coins ? packet.coinIcon : packet.gemIcon, "+" + UIFormat.Number(amount), coins ? "COINS" : "GEMS", packet.spinTitle);
    }
}
