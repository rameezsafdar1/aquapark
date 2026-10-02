using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Pill-shaped tab switch (FEATURED / GEMS / COINS, CHARACTERS / HATS / TRAILS). A yellow selector slides to the chosen tab.</summary>
public class SegmentedControl : MonoBehaviour
{
    public RectTransform selector;
    public RectTransform[] tabRects;
    public TMP_Text[] labels;
    public Button[] buttons;
    [Tooltip("Transparent margin around the selector picture (its shadow), added to the tab size.")]
    public float selectorPad;
    public Color selectedColor = new Color32(27, 42, 74, 255);
    public Color normalColor = Color.white;

    public int Selected { get; private set; }

    /// <summary>Raised when the player taps a tab (not when Select is called from code).</summary>
    public event Action<int> Changed;

    private void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].onClick.AddListener(() =>
            {
                Select(index);
                Changed?.Invoke(index);
            });
        }
    }

    public void Select(int index)
    {
        Selected = index;
        RectTransform tab = tabRects[index];
        selector.anchorMin = tab.anchorMin;
        selector.anchorMax = tab.anchorMax;
        selector.pivot = tab.pivot;
        selector.anchoredPosition = tab.anchoredPosition;
        selector.sizeDelta = tab.sizeDelta + new Vector2(selectorPad * 2f, selectorPad * 2f);

        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].color = i == index ? selectedColor : normalColor;
        }
    }
}
