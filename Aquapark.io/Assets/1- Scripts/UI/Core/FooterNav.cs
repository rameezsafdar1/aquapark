using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bottom bar: Shop, Skins, Play, Ranks and Spin. The bar, tabs and highlights are separate objects in the scene,
/// so tabs can be moved, added or removed by hand. The Spin tab is handled by HomeScreen (it also owns the red dot).
/// </summary>
public class FooterNav : MonoBehaviour
{
    public Button shopButton;
    public Button skinsButton;
    public Button ranksButton;
    public Button playButton;
    [Tooltip("Lit-up background behind the Shop tab while the shop page is open.")]
    public GameObject shopHighlight;
    [Tooltip("Lit-up background behind the Skins tab while the skins page is open.")]
    public GameObject skinsHighlight;

    private void Start()
    {
        shopButton.onClick.AddListener(() => UIManager.Instance.ToggleShop());
        skinsButton.onClick.AddListener(() => UIManager.Instance.ToggleSkins());
        ranksButton.onClick.AddListener(() => UIManager.Instance.ShowToast("Ranks are coming soon"));
    }

    /// <summary>0 = home, 1 = shop open, 2 = skins open.</summary>
    public void SetVariant(int variant)
    {
        shopHighlight.SetActive(variant == 1);
        skinsHighlight.SetActive(variant == 2);
    }
}
