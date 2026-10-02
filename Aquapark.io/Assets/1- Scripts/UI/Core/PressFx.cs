using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Squashes a button a little while it is held down.</summary>
public class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float pressedScale = 0.94f;
    private Vector3 baseScale = Vector3.one;
    private bool captured;

    private void Capture()
    {
        if (!captured)
        {
            baseScale = transform.localScale;
            captured = true;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Capture();
        transform.DOKill();
        transform.DOScale(baseScale * pressedScale, 0.08f).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    private void Release()
    {
        Capture();
        transform.DOKill();
        transform.DOScale(baseScale, 0.12f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void OnDisable()
    {
        if (captured)
        {
            transform.DOKill();
            transform.localScale = baseScale;
        }
    }
}
