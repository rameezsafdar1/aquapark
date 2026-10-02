using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>A short message that slides up and fades out ("Not enough coins").</summary>
[RequireComponent(typeof(CanvasGroup))]
public class ToastMessage : MonoBehaviour
{
    public TMP_Text label;
    private CanvasGroup group;
    private Vector2 homePosition;
    private bool captured;

    public void Show(string message)
    {
        var rect = (RectTransform)transform;
        if (!captured)
        {
            homePosition = rect.anchoredPosition;
            captured = true;
        }

        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
        }

        label.text = message;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        DOTween.Kill(group);
        DOTween.Kill(rect);

        rect.anchoredPosition = homePosition - new Vector2(0f, 30f);
        group.alpha = 0f;
        group.DOFade(1f, 0.15f).SetUpdate(true);
        rect.DOAnchorPos(homePosition, 0.2f).SetEase(Ease.OutCubic).SetUpdate(true);
        group.DOFade(0f, 0.3f).SetDelay(1.6f).SetUpdate(true).OnComplete(() => gameObject.SetActive(false));
    }
}
