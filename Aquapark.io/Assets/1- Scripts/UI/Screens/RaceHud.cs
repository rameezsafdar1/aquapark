using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the player sees while racing: live position (3rd / 12), a progress bar with a marker per racer,
/// the coin pill and the pause button. Knockout and combo popups are triggered through ShowKnockout / ShowCombo.
/// </summary>
public class RaceHud : MonoBehaviour
{
    public Button pauseButton;
    public TMP_Text rankText;
    public TMP_Text coinText;
    public GameObject steerHint;
    public float steerHintSeconds = 4f;

    [Header("Progress bar")]
    public RectTransform track;
    [Tooltip("Clips the yellow fill. Its width follows the player.")]
    public RectTransform fillMask;
    public RectTransform playerMarker;
    [Tooltip("Copied once per AI racer. Keep it switched off in the scene.")]
    public RectTransform aiMarkerTemplate;
    public Sprite[] aiMarkerSprites;

    [Header("Popups")]
    public TMP_Text knockoutText;
    public GameObject comboChip;
    public TMP_Text comboText;

    private readonly List<RectTransform> aiMarkers = new List<RectTransform>();
    private int shownRank = -1;
    private int shownTotal = -1;
    private float shownAt;
    private bool wired;

    private void OnEnable()
    {
        if (!wired)
        {
            wired = true;
            pauseButton.onClick.AddListener(() => UIManager.Instance.OpenPause());
            
        }

        SaveData.Changed += RefreshCoins;
        RefreshCoins();
        steerHint.SetActive(true);
        knockoutText.gameObject.SetActive(false);
        comboChip.SetActive(false);
        shownAt = Time.unscaledTime;
        shownRank = -1;
        BuildMarkers();
        Refresh();
    }

    private void OnDisable()
    {
        SaveData.Changed -= RefreshCoins;
    }

    private void RefreshCoins()
    {
        coinText.text = UIFormat.Number(SaveData.Coins);
    }

    private void BuildMarkers()
    {
        AiManager ai = GameManager.Instance != null ? GameManager.Instance.aiManager : null;
        if (ai == null)
        {
            return;
        }

        while (aiMarkers.Count < ai.RacerCount - 1)
        {
            RectTransform marker = Instantiate(aiMarkerTemplate, track);
            marker.gameObject.SetActive(true);
            if (aiMarkerSprites != null && aiMarkerSprites.Length > 0)
            {
                marker.GetComponent<Image>().sprite = aiMarkerSprites[aiMarkers.Count % aiMarkerSprites.Length];
            }

            aiMarkers.Add(marker);
        }

        // The player's marker is drawn last so it is never hidden behind an AI marker.
        playerMarker.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        Refresh();

        if (steerHint.activeSelf && Time.unscaledTime - shownAt > steerHintSeconds)
        {
            steerHint.SetActive(false);
        }
    }

    private void Refresh()
    {
        AiManager ai = GameManager.Instance != null ? GameManager.Instance.aiManager : null;
        if (ai == null)
        {
            return;
        }

        IReadOnlyList<double> percents = ai.RacerPercents;
        if (percents.Count == aiMarkers.Count + 1)
        {
            for (int i = 0; i < aiMarkers.Count; i++)
            {
                PlaceMarker(aiMarkers[i], percents[i]);
            }

            double player = percents[percents.Count - 1];
            PlaceMarker(playerMarker, player);
            fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0.01f, track.rect.width * Mathf.Clamp01((float)player)));
        }

        int rank = ai.PlayerRank;
        if (rank != shownRank || ai.RacerCount != shownTotal)
        {
            bool first = shownRank < 0;
            shownRank = rank;
            shownTotal = ai.RacerCount;

            string ordinal = UIFormat.Ordinal(rank);
            int digits = rank.ToString().Length;
            rankText.text = ordinal.Substring(0, digits)
                + "<size=47%>" + ordinal.Substring(digits) + "</size>"
                + "<size=41%><color=#FFFFFF> /" + shownTotal + "</color></size>";

            if (!first)
            {
                rankText.transform.DOKill(true);
                rankText.transform.DOPunchScale(Vector3.one * 0.3f, 0.35f, 6, 0.8f);
            }
        }
    }

    private void PlaceMarker(RectTransform marker, double percent)
    {
        marker.anchoredPosition = new Vector2(Mathf.Clamp01((float)percent) * track.rect.width, 0f);
    }

    /// <summary>"KNOCKOUT! +10" pop-up above the track.</summary>
    public void ShowKnockout(int points)
    {
        PopText(knockoutText.gameObject, knockoutText, "KNOCKOUT! +" + points);
    }

    /// <summary>Green "PERFECT JUMP x2" chip.</summary>
    public void ShowCombo(string label, int multiplier)
    {
        PopText(comboChip, comboText, label + "  x" + multiplier);
    }

    private static void PopText(GameObject root, TMP_Text text, string message)
    {
        text.text = message;
        root.SetActive(true);
        root.transform.DOKill();
        root.transform.localScale = Vector3.one * 0.5f;
        root.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        root.transform.DOScale(0f, 0.2f).SetDelay(1.4f).OnComplete(() => root.SetActive(false));
    }
}
