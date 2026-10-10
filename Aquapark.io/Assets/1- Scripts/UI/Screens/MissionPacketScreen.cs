using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reward reveal for missions and the lucky spin (art in Assets/2D/Subs Screen): the packet flies up, wobbles, stretches out to the sides, squeezes,
/// its top tears off and the reward card slides out of it and comes to the middle with the reward's icon and amount.
/// Tapping during the packet part speeds it up; tapping once the card is shown closes the screen.
/// The reward is already given before this plays; it only shows what was given. The music is turned down while it is open,
/// the tear plays Sfx.TearPaper and the card coming out plays Sfx.Reward.
/// Open it with UIManager.Instance.OpenMissionPacket(grant) or UIManager.Instance.OpenRewardPacket(icon, amount, label, title).
/// </summary>
public class MissionPacketScreen : UIPanel
{
    [Header("Parts")]
    public Image backdrop;
    public RectTransform rays;
    [Tooltip("The packet: the card starts as its first child (behind the packet picture), the torn-off top is its last.")]
    public RectTransform packet;
    public CanvasGroup packetGroup;
    public RectTransform packetTop;
    public CanvasGroup packetTopGroup;
    public RectTransform card;
    public Image flash;
    public TMP_Text tapText;
    public Button tapArea;

    [Header("Card")]
    public TMP_Text titleText;
    public Image rewardIcon;
    public TMP_Text amountText;
    public TMP_Text labelText;
    public string title = "MISSION COMPLETE!";
    public string spinTitle = "YOU WON!";

    [Header("Reward icons")]
    public Sprite coinIcon;
    public Sprite gemIcon;
    public Sprite spinIcon;
    [Tooltip("Lucky spin's double-coins prize.")]
    public Sprite boostIcon;

    [Header("Layout (canvas units)")]
    public float packetY = -20f;
    [Tooltip("Card inside the packet, before the tear (packet space).")]
    public float cardInPacketY = -10f;
    public float cardInPacketScale = 0.7f;
    [Tooltip("How far the card slides up out of the opened packet (packet space).")]
    public float cardOutY = 270f;
    public float cardFinalY = 10f;

    [Header("Feel")]
    [Range(0f, 1f)] public float backdropAlpha = 0.85f;
    [Tooltip("How much wider (and flatter) the packet gets before it squeezes.")]
    public float stretch = 0.2f;
    public float squeeze = 0.16f;
    [Tooltip("Speed of the packet part while the player holds/taps to skip.")]
    public float skipSpeed = 3f;
    public float raysSpeed = 25f;

    private Sequence sequence;
    private bool claimable;
    private Action closed;
    private Vector2 topRest;
    private bool restSaved;

    private void Awake()
    {
        SaveRest();
        if (tapArea != null)
        {
            tapArea.onClick.AddListener(OnTap);
        }
    }

    private void SaveRest()
    {
        if (!restSaved && packetTop != null)
        {
            topRest = packetTop.anchoredPosition;
            restSaved = true;
        }
    }

    /// <summary>Plays the packet opening for a mission reward. onClosed runs after the card has been tapped away.</summary>
    public void Play(MissionGrant grant, Action onClosed = null)
    {
        titleText.text = title;
        Fill(grant);
        Open(onClosed);
    }

    /// <summary>Plays the packet opening for any reward: icon, big amount line, small label under it, and the banner title.</summary>
    public void Play(Sprite icon, string amount, string label, string cardTitle, Action onClosed = null)
    {
        titleText.text = cardTitle;
        SetCard(icon, amount, label);
        Open(onClosed);
    }

    private void Open(Action onClosed)
    {
        SaveRest();
        closed = onClosed;
        Show();
        ResetPose();
        BuildSequence();
        AudioManager.DuckMusic(true);
    }

    /// <summary>Back button: speeds the animation up, or closes once the card is shown.</summary>
    public void Skip()
    {
        OnTap();
    }

#if UNITY_EDITOR
    /// <summary>Editor checks: freezes the animation at this time (seconds) so a frame can be captured. Returns its length.</summary>
    public float DebugSeek(float time)
    {
        if (sequence == null) return 0f;
        sequence.Goto(time);
        sequence.Pause();
        return sequence.Duration();
    }
#endif

    private void Update()
    {
        if (rays != null && rays.gameObject.activeInHierarchy)
        {
            rays.Rotate(0f, 0f, -raysSpeed * Time.unscaledDeltaTime);
        }
    }

    private void Fill(MissionGrant grant)
    {
        int amount = grant.amount;
        switch (grant.kind)
        {
            case MissionRewardKind.Gems:
                SetCard(gemIcon, "+" + UIFormat.Number(amount), "GEMS");
                break;
            case MissionRewardKind.FreeSpins:
                SetCard(spinIcon, "+" + amount, amount == 1 ? "FREE SPIN" : "FREE SPINS");
                break;
            case MissionRewardKind.Skin when grant.skin != null:
                SetCard(grant.skin.icon, grant.skin.displayName.ToUpperInvariant(),
                    grant.skin.slot == SkinSlot.Floatie ? "NEW FLOATIE" : "NEW CHARACTER");
                break;
            default:   // coins, or the coins given instead of a skin
                SetCard(coinIcon, "+" + UIFormat.Number(amount), "COINS");
                break;
        }
    }

    private void SetCard(Sprite icon, string amount, string label)
    {
        rewardIcon.sprite = icon;
        rewardIcon.preserveAspect = true;
        rewardIcon.enabled = icon != null;
        amountText.text = amount;
        labelText.text = label;
    }

    /// <summary>Everything back where the animation starts: packet below the screen, card inside it, top still on.</summary>
    private void ResetPose()
    {
        sequence?.Kill();
        claimable = false;

        card.DOKill();
        card.SetParent(packet, false);
        card.SetAsFirstSibling();
        card.anchoredPosition = new Vector2(0f, cardInPacketY);
        card.localScale = Vector3.one * cardInPacketScale;
        card.localRotation = Quaternion.identity;
        rewardIcon.rectTransform.localScale = Vector3.one;

        packet.DOKill();
        packet.anchoredPosition = new Vector2(0f, packetY - 1100f);
        packet.localScale = Vector3.one;
        packet.localRotation = Quaternion.Euler(0f, 0f, -14f);
        packetGroup.alpha = 1f;

        packetTop.DOKill();
        packetTop.anchoredPosition = topRest;
        packetTop.localRotation = Quaternion.identity;
        packetTopGroup.alpha = 1f;

        rays.localScale = Vector3.one * 0.5f;
        SetAlpha(rays.GetComponent<Graphic>(), 0f);
        SetAlpha(flash, 0f);
        SetAlpha(backdrop, 0f);
        tapText.DOKill();
        tapText.alpha = 0f;
        tapText.transform.localScale = Vector3.one;
    }

    private void BuildSequence()
    {
        Graphic raysGraphic = rays.GetComponent<Graphic>();
        Sequence s = DOTween.Sequence().SetUpdate(true).SetTarget(this);

        // 1. The packet flies up into the middle.
        s.Append(packet.DOAnchorPosY(packetY, 0.55f).SetEase(Ease.OutBack));
        s.Join(packet.DOLocalRotate(Vector3.zero, 0.55f).SetEase(Ease.OutBack));
        s.Join(DOTween.To(v => SetAlpha(backdrop, v), 0f, backdropAlpha, 0.3f));

        // 2. Wobble.
        s.Append(packet.DOPunchRotation(new Vector3(0f, 0f, 9f), 0.75f, 7, 0.5f));
        s.Join(packet.DOPunchScale(new Vector3(0.05f, -0.04f, 0f), 0.75f, 7, 0.5f));

        // 3. Stretch out to the sides from the centre, 4. squeeze in.
        s.Append(packet.DOScale(new Vector3(1f + stretch, 1f - stretch * 0.6f, 1f), 0.32f).SetEase(Ease.OutQuad));
        s.AppendInterval(0.06f);
        s.Append(packet.DOScale(new Vector3(1f - squeeze, 1f + squeeze * 0.75f, 1f), 0.16f).SetEase(Ease.InQuad));

        // 5. Tear: the top flies off, the packet springs back, white flash.
        s.AppendCallback(() =>
        {
            Haptics.Pulse();
            AudioManager.Play(Sfx.TearPaper);
        });
        s.Append(packet.DOScale(1f, 0.45f).SetEase(Ease.OutBack));
        s.Join(packetTop.DOAnchorPos(topRest + new Vector2(170f, 430f), 0.65f).SetEase(Ease.OutQuad));
        s.Join(packetTop.DOLocalRotate(new Vector3(0f, 0f, -38f), 0.65f).SetEase(Ease.OutQuad));
        s.Join(DOTween.To(v => packetTopGroup.alpha = v, 1f, 0f, 0.35f).SetDelay(0.3f));
        s.Join(DOTween.To(v => SetAlpha(flash, v), 0.8f, 0f, 0.4f));

        // 6. The card slides up out of the opening.
        s.Insert(s.Duration() - 0.3f, card.DOAnchorPosY(cardOutY, 0.45f).SetEase(Ease.OutCubic));

        // 7. The card comes to the front and the middle, the packet drops away, rays behind the card.
        s.AppendCallback(TakeCardOut);
        s.Append(card.DOAnchorPos(new Vector2(0f, cardFinalY), 0.5f).SetEase(Ease.OutBack));
        s.Join(card.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        s.Join(packet.DOAnchorPosY(packetY - 1100f, 0.55f).SetEase(Ease.InBack));
        s.Join(DOTween.To(v => packetGroup.alpha = v, 1f, 0f, 0.3f).SetDelay(0.25f));
        s.Join(DOTween.To(v => SetAlpha(raysGraphic, v), 0f, 1f, 0.4f).SetDelay(0.15f));
        s.Join(rays.DOScale(1f, 0.6f).SetEase(Ease.OutBack).SetDelay(0.15f));
        s.Append(rewardIcon.rectTransform.DOPunchScale(Vector3.one * 0.25f, 0.45f, 6, 0.6f));
        s.AppendCallback(Haptics.Pulse);

        // 8. Tap to continue.
        s.AppendInterval(0.2f);
        s.AppendCallback(ShowTapHint);
        sequence = s;
    }

    /// <summary>Moves the card out of the packet (same spot on screen) and in front of it.</summary>
    private void TakeCardOut()
    {
        Transform page = packet.parent;
        card.SetParent(page, true);
        card.SetSiblingIndex(packet.GetSiblingIndex() + 1);
        Haptics.Pulse();
        AudioManager.Play(Sfx.Reward);
    }

    private void ShowTapHint()
    {
        claimable = true;
        if (sequence != null)
        {
            sequence.timeScale = 1f;
        }

        tapText.DOFade(1f, 0.3f).SetUpdate(true);
        tapText.transform.DOScale(1.08f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void OnTap()
    {
        if (!IsOpen)
        {
            return;
        }

        if (!claimable)
        {
            if (sequence != null && sequence.IsActive())
            {
                sequence.timeScale = skipSpeed;
            }

            return;
        }

        claimable = false;
        tapText.DOKill();
        tapText.transform.DOKill();
        tapText.DOFade(0f, 0.15f).SetUpdate(true);
        card.DOScale(0f, 0.28f).SetEase(Ease.InBack).SetUpdate(true);
        rays.DOScale(0f, 0.28f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() =>
        {
            Action done = closed;
            closed = null;
            AudioManager.DuckMusic(false);
            UIManager.Instance.CloseMissionPacket();
            done?.Invoke();
        });
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic != null)
        {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }

    private void OnDisable()
    {
        AudioManager.DuckMusic(false);   // also when it is closed some other way (e.g. a race starts)
        sequence?.Kill();
        sequence = null;
        DOTween.Kill(this);
        if (tapText != null)
        {
            tapText.DOKill();
            tapText.transform.DOKill();
        }
    }
}
