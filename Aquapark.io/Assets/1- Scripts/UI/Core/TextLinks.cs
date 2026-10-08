using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Makes &lt;link="id"&gt; parts of a TextMeshPro text tappable; Clicked gets the link id.</summary>
[RequireComponent(typeof(TMP_Text))]
public class TextLinks : MonoBehaviour, IPointerClickHandler
{
    public event Action<string> Clicked;

    private TMP_Text text;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }

        Camera cam = eventData.pressEventCamera;
        int index = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, cam);
        if (index >= 0)
        {
            Clicked?.Invoke(text.textInfo.linkInfo[index].GetLinkID());
        }
    }
}
