using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The "YOU GOT +100" popup shown after a reward.</summary>
public class RewardPopup : UIPanel
{
    public Image icon;
    public TMP_Text titleText;
    public TMP_Text amountText;
    public Button okButton;

    private void Awake()
    {
        okButton.onClick.AddListener(() => Hide());
    }

    public void Present(Sprite sprite, string amount, string title)
    {
        icon.sprite = sprite;
        icon.preserveAspect = true;
        titleText.text = title;
        amountText.text = amount;
        Show();
        icon.transform.DOKill();
        icon.transform.localScale = Vector3.one * 0.4f;
        icon.transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);
    }
}
