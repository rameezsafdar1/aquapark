using DG.Tweening;
using UnityEngine;

/// <summary>A screen, page or popup that fades in and out. Hidden panels are switched off completely.</summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    [Tooltip("Optional child that pops in (scales up) when the panel opens.")]
    public RectTransform popTarget;

    private CanvasGroup group;

    public bool IsOpen { get; private set; }

    private CanvasGroup Group
    {
        get
        {
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            return group;
        }
    }

    public void Show(bool instant = false)
    {
        bool wasOpen = IsOpen;
        IsOpen = true;
        gameObject.SetActive(true);
        Group.DOKill();
        Group.blocksRaycasts = true;
        Group.interactable = true;

        if (instant)
        {
            Group.alpha = 1f;
        }
        else
        {
            if (!wasOpen)
            {
                Group.alpha = 0f;
            }

            Group.DOFade(1f, 0.18f).SetUpdate(true);
            if (popTarget != null && !wasOpen)
            {
                popTarget.DOKill();
                popTarget.localScale = Vector3.one * 0.85f;
                popTarget.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        OnShown();
    }

    public void Hide(bool instant = false)
    {
        if (!IsOpen && !gameObject.activeSelf)
        {
            return;
        }

        IsOpen = false;
        Group.DOKill();
        Group.blocksRaycasts = false;

        if (instant || !gameObject.activeInHierarchy)
        {
            Group.alpha = 0f;
            gameObject.SetActive(false);
            return;
        }

        Group.DOFade(0f, 0.12f).SetUpdate(true).OnComplete(() =>
        {
            if (!IsOpen)
            {
                gameObject.SetActive(false);
            }
        });
    }

    /// <summary>Called every time the panel is opened, so it can refresh what it shows.</summary>
    protected virtual void OnShown()
    {
    }
}
