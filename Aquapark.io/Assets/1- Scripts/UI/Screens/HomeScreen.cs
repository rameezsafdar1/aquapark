using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The main menu: level progress, the Daily / No Ads / Spin buttons with their red dots, and settings.</summary>
public class HomeScreen : MonoBehaviour
{
    [Header("Level progress")]
    public TMP_Text levelText;
    public TMP_Text percentText;
    [Tooltip("Clips the green fill. Its width is changed to show progress.")]
    public RectTransform fillMask;
    public float fillFullWidth = 150f;

    [Header("Buttons")]
    public Button settingsButton;
    public Button dailyButton;
    public Button noAdsButton;
    public Button spinButton;
    public Button missionButton;

    [Header("Red dots")]
    public GameObject dailyBadge;
    public GameObject noAdsBadge;
    public GameObject spinBadge;
    public TMP_Text spinBadgeText;

    private void Awake()
    {
        settingsButton.onClick.AddListener(() => UIManager.Instance.OpenSettings());
        dailyButton.onClick.AddListener(() => UIManager.Instance.OpenDaily());
        spinButton.onClick.AddListener(() => UIManager.Instance.OpenSpin());
        noAdsButton.onClick.AddListener(OnNoAds);
        missionButton.onClick.AddListener(OnMission);
    }

    // Placeholder until the real mission card (with the packet animation) is connected: tap shows the active mission and its
    // progress, or claims the reward when the mission is done.
    private void OnMission()
    {
        UIManager ui = UIManager.Instance;
        if (!MissionManager.HasMissions)
        {
            ui.ShowToast("No missions set up");
            return;
        }

        if (!MissionManager.RewardPending)
        {
            ui.ShowToast(MissionManager.Title + "  " + MissionManager.ProgressText);
            return;
        }

        ShowMissionReward();
    }

    /// <summary>
    /// Gives the finished mission's reward. Called by UIManager when the menu opens with a reward waiting (e.g. right after
    /// the first race) and by tapping the card. Placeholder: the imported mission card's packet animation goes here.
    /// </summary>
    public void ShowMissionReward()
    {
        if (!MissionManager.RewardPending)
        {
            return;
        }

        UIManager ui = UIManager.Instance;
        MissionGrant grant = MissionManager.ClaimReward();
        switch (grant.kind)
        {
            case MissionRewardKind.Coins: ui.ShowReward(RewardType.Coins, grant.amount, "MISSION COMPLETE"); break;
            case MissionRewardKind.Gems: ui.ShowReward(RewardType.Gems, grant.amount, "MISSION COMPLETE"); break;
            case MissionRewardKind.FreeSpins: ui.ShowToast("Mission complete! +" + grant.amount + " free spin" + (grant.amount == 1 ? "" : "s")); break;
            default:
                if (grant.skin != null) ui.ShowToast("Mission complete! New skin: " + grant.skin.displayName);
                else ui.ShowReward(RewardType.Coins, grant.amount, "MISSION COMPLETE");
                break;
        }
    }

    private void OnEnable()
    {
        SaveData.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        SaveData.Changed -= Refresh;
    }

    private void OnNoAds()
    {
        if (SaveData.NoAds)
        {
            UIManager.Instance.ShowToast("Ads are already removed");
            return;
        }

        PurchaseService.Purchase("remove_ads", () =>
        {
            SaveData.NoAds = true;
            UIManager.Instance.ShowToast("Ads removed. Thank you!");
        });
    }

    /// <summary>Shows how far through the current block of 10 levels the player is.</summary>
    public void Refresh()
    {
        int level = SaveData.Level;
        float progress = ((level - 1) % 10) / 10f;
        levelText.text = level.ToString();
        percentText.text = Mathf.RoundToInt(progress * 100f) + "%";
        fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0.01f, fillFullWidth * progress));
        noAdsBadge.SetActive(!SaveData.NoAds);
    }

    public void SetBadges(bool dailyReady, int freeSpins)
    {
        dailyBadge.SetActive(dailyReady);
        spinBadge.SetActive(freeSpins > 0);
        spinBadgeText.text = freeSpins.ToString();
    }
}
