using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Brings the title logo to life: it pops in whenever the menu opens, then floats and "breathes" gently,
/// and a glossy shine sweeps across it every few seconds (clipped to the logo by a Mask).
/// The shine is created at runtime, so the only setup is adding this to the logo Image.
/// </summary>
[RequireComponent(typeof(Image))]
public class LogoAnimator : MonoBehaviour
{
    [Header("Pop in")]
    public float popDuration = 0.6f;
    [Tooltip("Scale the logo starts from when it pops in.")]
    public float popFromScale = 0.4f;
    public float popTwistDegrees = 8f;

    [Header("Idle")]
    public float bobHeight = 6f;
    public float bobDuration = 1.6f;
    public float breatheScale = 1.03f;
    public float breatheDuration = 1.3f;

    [Header("Shine")]
    [Tooltip("Seconds between shine sweeps.")]
    public float shineInterval = 3.5f;
    public float shineDuration = 0.7f;
    [Range(0f, 1f)] public float shineAlpha = 0.45f;
    [Tooltip("Shine band width as a fraction of the logo width.")]
    [Range(0.05f, 0.5f)] public float shineWidth = 0.16f;

    private RectTransform rect;
    private RectTransform shine;
    private Vector2 basePos;
    private Vector3 baseScale;
    private Quaternion baseRot;
    private bool captured;
    private Sequence intro, shineLoop;

    private void Awake()
    {
        rect = (RectTransform)transform;
        CreateShine();
    }

    private void OnEnable()
    {
        if (!captured)
        {
            basePos = rect.anchoredPosition;
            baseScale = rect.localScale;
            baseRot = rect.localRotation;
            captured = true;
        }

        StopAll();
        rect.localScale = baseScale * popFromScale;
        rect.localRotation = baseRot * Quaternion.Euler(0f, 0f, -popTwistDegrees);

        intro = DOTween.Sequence()
            .Append(rect.DOScale(baseScale, popDuration).SetEase(Ease.OutBack, 2.2f))
            .Join(rect.DOLocalRotateQuaternion(baseRot, popDuration).SetEase(Ease.OutBack, 3f))
            .OnComplete(StartIdle);
        StartShine();
    }

    private void OnDisable()
    {
        StopAll();
        if (captured)
        {
            rect.anchoredPosition = basePos;
            rect.localScale = baseScale;
            rect.localRotation = baseRot;
        }
    }

    private void StartIdle()
    {
        rect.DOAnchorPosY(basePos.y + bobHeight, bobDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        rect.DOScale(baseScale * breatheScale, breatheDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    private void StartShine()
    {
        float travel = rect.rect.width * 0.65f;
        shineLoop = DOTween.Sequence()
            .AppendCallback(() => shine.anchoredPosition = new Vector2(-travel, 0f))
            .AppendInterval(popDuration * 0.8f)
            .Append(shine.DOAnchorPosX(travel, shineDuration).SetEase(Ease.InOutQuad))
            .AppendInterval(shineInterval)
            .SetLoops(-1, LoopType.Restart);
    }

    private void StopAll()
    {
        intro?.Kill();
        shineLoop?.Kill();
        rect.DOKill();
        if (shine != null)
        {
            shine.DOKill();
        }
    }

    // A soft white diagonal band, masked to the logo's shape.
    private void CreateShine()
    {
        var mask = gameObject.GetComponent<Mask>();
        if (mask == null)
        {
            mask = gameObject.AddComponent<Mask>();
        }

        mask.showMaskGraphic = true;

        var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 64; x++)
        {
            float t = x / 63f;
            float a = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(t * 2f - 1f));
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();

        var go = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        shine = (RectTransform)go.transform;
        shine.SetParent(rect, false);
        shine.anchorMin = shine.anchorMax = new Vector2(0.5f, 0.5f);
        shine.sizeDelta = new Vector2(rect.rect.width * shineWidth, rect.rect.height * 1.8f);
        shine.localRotation = Quaternion.Euler(0f, 0f, -20f);
        shine.anchoredPosition = new Vector2(-rect.rect.width, 0f);

        var image = go.GetComponent<Image>();
        image.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 1), new Vector2(0.5f, 0.5f));
        image.color = new Color(1f, 1f, 1f, shineAlpha);
        image.raycastTarget = false;
    }
}
