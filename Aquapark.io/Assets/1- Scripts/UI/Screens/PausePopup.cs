using UnityEngine;
using UnityEngine.UI;

/// <summary>Pause menu: resume, restart the race, or go back to the main menu.</summary>
public class PausePopup : UIPanel
{
    public Button resumeButton;
    public Button restartButton;
    public Button homeButton;

    private void Awake()
    {
        resumeButton.onClick.AddListener(() => UIManager.Instance.ResumeRace());
        restartButton.onClick.AddListener(() => UIManager.Instance.ReloadRace(true));
        homeButton.onClick.AddListener(() => UIManager.Instance.ReloadRace(false));
    }
}
