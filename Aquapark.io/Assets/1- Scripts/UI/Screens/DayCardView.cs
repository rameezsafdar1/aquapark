using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One day of the daily reward calendar.</summary>
public class DayCardView : MonoBehaviour
{
    public enum DayState
    {
        Claimed,
        Today,
        Upcoming
    }

    public Image bg;
    public GameObject overlay;
    public GameObject check;
    public GameObject tag;
    public TMP_Text dayText;
    public TMP_Text amountText;
    public Sprite claimedSprite;
    public Sprite todaySprite;
    public Sprite upcomingSprite;

    public void Set(DayState state)
    {
        bg.sprite = state == DayState.Claimed ? claimedSprite : state == DayState.Today ? todaySprite : upcomingSprite;
        overlay.SetActive(state == DayState.Claimed);
        check.SetActive(state == DayState.Claimed);
        tag.SetActive(state == DayState.Today);
    }
}
