using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shown when the race ends: place, top-3 board, coin and gem rewards, claim x3 with an ad, or just claim.</summary>
public class ResultsScreen : UIPanel
{
    public TMP_Text ribbonText;

    [Header("Leaderboard rows")]
    public TMP_Text[] positionTexts = new TMP_Text[3];
    public TMP_Text[] nameTexts = new TMP_Text[3];
    public TMP_Text[] timeTexts = new TMP_Text[3];
    public GameObject board;

    [Header("Rewards")]
    public GameObject coinReward;
    public TMP_Text coinText;
    public GameObject gemReward;
    public TMP_Text gemText;

    [Header("Buttons")]
    public Button claimTripleButton;
    public GameObject claimTripleGroup;
    public Button claimButton;
    public TMP_Text claimLabel;
    public RectTransform sunburst;

    private static readonly string[] Names =
    {
        "SplashKing", "Noodle_99", "BubbleBoy", "WaveRider", "AquaAce", "DripDrop", "FinnTheFish", "TubeMaster", "Splashy", "ReefRunner"
    };

    private int baseCoins;
    private int baseGems;
    private bool failed;
    private bool collected;
    private bool wired;

    /// <summary>Coins for a finishing position: 1st gets the most.</summary>
    public static int CoinsForRank(int rank)
    {
        switch (rank)
        {
            case 1: return 150;
            case 2: return 100;
            case 3: return 70;
            default: return 40;
        }
    }

    public void Present(int rank, int racers, bool didFail, float raceTime)
    {
        Wire();
        failed = didFail;
        collected = false;

        baseCoins = failed ? 0 : CoinsForRank(rank);
        if (!failed && SaveData.CoinBoostRaces > 0)
        {
            baseCoins *= 2;
        }

        baseGems = failed ? 0 : rank == 1 ? 2 : rank == 2 ? 1 : 0;

        // 2x rewards buff: coins and gems of this race are doubled.
        if (!failed && RaceBuffs.DoubleRewards)
        {
            baseCoins *= 2;
            baseGems *= 2;
        }

        ribbonText.text = failed ? "YOU FELL!" : UIFormat.Ordinal(rank).ToUpperInvariant() + " PLACE!";
        board.SetActive(!failed);
        coinReward.SetActive(!failed);
        gemReward.SetActive(!failed && baseGems > 0);
        claimTripleGroup.SetActive(!failed);
        claimLabel.text = failed ? "Tap to retry" : "No thanks, claim " + UIFormat.Number(baseCoins);
        coinText.text = "+" + UIFormat.Number(baseCoins);
        gemText.text = "+" + baseGems;

        if (!failed)
        {
            FillBoard(rank, raceTime);
        }

        Show();

        if (sunburst != null)
        {
            sunburst.DOKill();
            sunburst.DOLocalRotate(new Vector3(0f, 0f, -360f), 40f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
        }

        Haptics.Pulse();
    }

    private void FillBoard(int rank, float raceTime)
    {
        float playerTime = raceTime > 1f ? raceTime : 45f;
        float winner = rank == 1 ? playerTime : playerTime - (rank - 1) * Random.Range(0.5f, 1.1f);
        int[] places = rank <= 3 ? new[] { 1, 2, 3 } : new[] { 1, 2, rank };

        int nameOffset = Random.Range(0, Names.Length);
        float time = winner;
        int lastPlace = 1;
        for (int row = 0; row < 3; row++)
        {
            int place = places[row];
            time += (place - lastPlace) * Random.Range(0.4f, 1.1f);
            lastPlace = place;

            bool you = place == rank;
            positionTexts[row].text = place.ToString();
            nameTexts[row].text = you ? "You" : Names[(nameOffset + row) % Names.Length];
            timeTexts[row].text = UIFormat.RaceTime(you ? playerTime : time);
        }
    }

    private void Wire()
    {
        if (wired)
        {
            return;
        }

        wired = true;
        claimButton.onClick.AddListener(() =>
        {
            Collect(1);
            UIManager.Instance.FinishResults(failed);
        });

        claimTripleButton.onClick.AddListener(() =>
        {
            AdService.ShowRewarded(() =>
            {
                Collect(3);
                UIManager.Instance.FinishResults(false);
            });
        });
    }

    /// <summary>Pays out once, however many buttons get pressed.</summary>
    private void Collect(int multiplier)
    {
        if (collected)
        {
            return;
        }

        collected = true;
        CurrencyManager.Add(RewardType.Coins, baseCoins * multiplier);
        CurrencyManager.Add(RewardType.Gems, baseGems);

        if (!failed)
        {
            SaveData.Level += 1;
            if (SaveData.CoinBoostRaces > 0)
            {
                SaveData.CoinBoostRaces -= 1;
            }
        }
    }
}
