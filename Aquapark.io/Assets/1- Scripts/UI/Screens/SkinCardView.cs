using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One card in the skins grid: picture, name or price, and the green tick when equipped.</summary>
public class SkinCardView : MonoBehaviour
{
    public Button button;
    public Image bg;
    public Image art;
    public TMP_Text nameText;
    public GameObject equippedBadge;

    [Header("Price pill (locked skins)")]
    public GameObject priceRoot;
    public Image priceBg;
    public Image priceIcon;
    public TMP_Text priceText;

    [Header("Sprites, filled in by the UI builder")]
    [Tooltip("Card backgrounds: purple, blue, green, yellow.")]
    public Sprite[] normalSprites = new Sprite[4];
    public Sprite[] selectedSprites = new Sprite[4];
    public Sprite pillNavy;
    public Sprite pillRed;
    [Tooltip("Transparent margin around the price pill picture, added to its width.")]
    public float pillPad = 2f;
    public Sprite coinIcon;
    public Sprite gemIcon;

    public SkinData Skin { get; private set; }

    public void Bind(SkinData skin, bool selected)
    {
        Skin = skin;
        int color = Mathf.Clamp(skin.cardColor, 0, normalSprites.Length - 1);
        bg.sprite = selected ? selectedSprites[color] : normalSprites[color];
        art.sprite = skin.icon;
        art.enabled = skin.icon != null;

        bool owned = SkinManager.IsOwned(skin);
        equippedBadge.SetActive(SkinManager.IsEquipped(skin));
        nameText.gameObject.SetActive(owned);
        nameText.text = skin.displayName.ToUpperInvariant();
        priceRoot.SetActive(!owned);

        if (owned)
        {
            return;
        }

        RectTransform pill = priceBg.rectTransform;
        priceText.rectTransform.offsetMin = new Vector2(skin.unlock == SkinUnlock.Currency ? 18f : 2f, 0f);
        switch (skin.unlock)
        {
            case SkinUnlock.RewardedAd:
                priceBg.sprite = pillRed;
                priceIcon.gameObject.SetActive(false);
                priceText.text = "AD";
                pill.sizeDelta = new Vector2(34f + pillPad * 2f, pill.sizeDelta.y);
                break;
            case SkinUnlock.PlayerLevel:
                priceBg.sprite = pillNavy;
                priceIcon.gameObject.SetActive(false);
                priceText.text = "Lv " + skin.unlockLevel;
                pill.sizeDelta = new Vector2(44f + pillPad * 2f, pill.sizeDelta.y);
                break;
            default:
                priceBg.sprite = pillNavy;
                priceIcon.gameObject.SetActive(true);
                priceIcon.sprite = skin.currency == RewardType.Coins ? coinIcon : gemIcon;
                priceText.text = UIFormat.Number(skin.price);
                pill.sizeDelta = new Vector2((skin.price >= 1000 ? 68f : 54f) + pillPad * 2f, pill.sizeDelta.y);
                break;
        }
    }
}
