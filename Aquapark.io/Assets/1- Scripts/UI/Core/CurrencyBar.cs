using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The coin and gem pills at the top of a screen. Updates itself whenever the save changes.</summary>
public class CurrencyBar : MonoBehaviour
{
    public TMP_Text coinText;
    public TMP_Text gemText;
    public Button coinPlus;
    public Button gemPlus;

    private void Awake()
    {
        if (coinPlus != null)
        {
            coinPlus.onClick.AddListener(OnPlus);
        }

        if (gemPlus != null)
        {
            gemPlus.onClick.AddListener(OnPlus);
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

    private void OnPlus()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OpenShop();
        }
    }

    public void Refresh()
    {
        if (coinText != null)
        {
            coinText.text = UIFormat.Number(SaveData.Coins);
        }

        if (gemText != null)
        {
            gemText.text = UIFormat.Number(SaveData.Gems);
        }
    }
}
