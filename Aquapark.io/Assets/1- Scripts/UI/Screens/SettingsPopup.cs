using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Sound, music and vibration switches, restore purchases, close.</summary>
public class SettingsPopup : UIPanel
{
    public Button soundButton;
    public Button musicButton;
    public Button hapticsButton;
    public Button restoreButton;
    public Button closeButton;
    public TMP_Text soundLabel;
    public TMP_Text musicLabel;
    public TMP_Text hapticsLabel;

    private void Awake()
    {
        soundButton.onClick.AddListener(() => { SaveData.Sound = !SaveData.Sound; Refresh(); });
        musicButton.onClick.AddListener(() => { SaveData.Music = !SaveData.Music; Refresh(); });
        hapticsButton.onClick.AddListener(() => { SaveData.Haptics = !SaveData.Haptics; Refresh(); });
        restoreButton.onClick.AddListener(() =>
        {
            PurchaseService.Restore();
            UIManager.Instance.ShowToast("Purchases restored");
        });
        closeButton.onClick.AddListener(() => Hide());
    }

    protected override void OnShown()
    {
        Refresh();
    }

    private void Refresh()
    {
        soundLabel.text = "SOUND  " + (SaveData.Sound ? "ON" : "OFF");
        musicLabel.text = "MUSIC  " + (SaveData.Music ? "ON" : "OFF");
        hapticsLabel.text = "VIBRATION  " + (SaveData.Haptics ? "ON" : "OFF");
    }
}
