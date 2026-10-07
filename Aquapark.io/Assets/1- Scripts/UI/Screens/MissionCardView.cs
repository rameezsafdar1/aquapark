using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The mission card above the Play button: shows the active mission from <see cref="MissionManager"/> (title, reward,
/// progress bar) and refreshes itself when progress changes. When the mission is done it shows "CLAIM!" and pulses.
/// Tapping is handled by HomeScreen (missionButton).
/// </summary>
public class MissionCardView : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text rewardText;
    public TMP_Text progressText;
    public Image rewardIcon;
    [Tooltip("Clips the fill picture; its width shows the progress.")]
    public RectTransform fillMask;
    public float fillFullWidth = 150f;

    [Header("Reward icons")]
    public Sprite coinIcon;
    public Sprite gemIcon;
    public Sprite spinIcon;
    public Sprite skinIcon;
    [Tooltip("The spin and skin icons are white; they are tinted so they show on the light card.")]
    public Color spinTint = new Color32(0x3a, 0x8d, 0xf0, 255);
    public Color skinTint = new Color32(0x9b, 0x5c, 0xf0, 255);

    private Tween pulse;

    private void Awake()
    {
        // Long mission titles shrink first, then end in "..." rather than being cut mid-word.
        titleText.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void OnEnable()
    {
        MissionManager.Changed += Refresh;
        SaveData.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        MissionManager.Changed -= Refresh;
        SaveData.Changed -= Refresh;
        StopPulse();
    }

    public void Refresh()
    {
        if (!MissionManager.HasMissions)
        {
            titleText.text = "";
            rewardText.text = "";
            progressText.text = "";
            rewardIcon.enabled = false;
            SetFill(0f);
            return;
        }

        titleText.text = MissionManager.Title;
        ShowReward(MissionManager.Reward);

        bool ready = MissionManager.RewardPending;
        progressText.text = ready ? "CLAIM!" : MissionManager.ProgressText;
        SetFill(ready ? 1f : MissionManager.Progress01);

        if (ready) StartPulse();
        else StopPulse();
    }

    private void ShowReward(MissionReward reward)
    {
        rewardIcon.enabled = true;
        rewardIcon.color = Color.white;
        switch (reward.kind)
        {
            case MissionRewardKind.Coins:
                rewardIcon.sprite = coinIcon;
                rewardText.text = "+" + UIFormat.Number(reward.amount);
                break;
            case MissionRewardKind.Gems:
                rewardIcon.sprite = gemIcon;
                rewardText.text = "+" + UIFormat.Number(reward.amount);
                break;
            case MissionRewardKind.FreeSpins:
                rewardIcon.sprite = spinIcon;
                rewardIcon.color = spinTint;
                rewardText.text = "+" + reward.amount;
                break;
            default:
                rewardIcon.sprite = skinIcon;
                rewardIcon.color = skinTint;
                rewardText.text = "SKIN";
                break;
        }
    }

    private void SetFill(float t)
    {
        if (fillMask != null)
        {
            fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0.01f, fillFullWidth * Mathf.Clamp01(t)));
        }
    }

    private void StartPulse()
    {
        if (pulse != null && pulse.IsActive()) return;
        transform.localScale = Vector3.one;
        pulse = transform.DOScale(1.06f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void StopPulse()
    {
        if (pulse != null) pulse.Kill();
        pulse = null;
        transform.localScale = Vector3.one;
    }
}
