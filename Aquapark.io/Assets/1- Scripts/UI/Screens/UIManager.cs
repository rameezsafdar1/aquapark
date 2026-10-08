using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the three states of the UI (menu, racing, results) and opens/closes every page and popup.
/// The race itself is still started by GameManager (its Start Button is the Play button in the footer).
/// </summary>
public class UIManager : MonoBehaviour
{
    public enum State
    {
        Menu,
        Racing,
        Results
    }

    /// <summary>Set before reloading the scene so the next race begins without the menu.</summary>
    public static bool StartNextRaceImmediately;

    [Header("Menu")]
    public GameObject menuRoot;
    public GameObject home;
    public HomeScreen homeScreen;
    public UIPanel shop;
    public UIPanel skins;
    public UIPanel spin;
    public UIPanel daily;
    public FooterNav footer;
    [Tooltip("VIP subscription offer (Figma '09').")]
    public VipOfferScreen vip;
    [Tooltip("New VIP offer (Welcome back, Assets/2D/Subs Screen). Shown on launch instead of the older one when set.")]
    public WelcomeBackScreen welcomeBack;
    [Tooltip("Fail-safe weekly offer shown after a purchase fails (Assets/2D/Subs Screen). Open with OpenPurchaseFailed().")]
    public PurchaseFailedScreen purchaseFailed;
    [Tooltip("Show the VIP offer once per launch to returning players who are not VIP.")]
    public bool showVipOnLaunch = true;

    [Header("Popups")]
    public SettingsPopup settings;
    public RewardPopup reward;
    public ToastMessage toast;

    [Header("Race")]
    public RaceHud hud;
    public ResultsScreen results;
    public PausePopup pause;
    [Tooltip("Beach Cup qualifier shown after every won race.")]
    public QualifierScreen qualifier;

    [Header("Data")]
    public SkinDatabase skinDatabase;
    public SkinDatabase floatieDatabase;
    public Sprite coinSprite;
    public Sprite gemSprite;

    private State state = State.Menu;
    private float badgeTimer;
    private bool qualifierStarted;

    public static UIManager Instance { get; private set; }
    public State CurrentState => state;
    public SkinDatabase Skins => skinDatabase;
    public SkinDatabase Floaties => floatieDatabase;

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        SkinManager.Initialise(skinDatabase);
        SkinManager.Initialise(floatieDatabase);
        SkinManager.Unlocked += OnItemUnlocked;

        foreach (UIPanel panel in new UIPanel[] { shop, skins, spin, daily, vip, welcomeBack, purchaseFailed, settings, reward, results, pause, qualifier })
        {
            if (panel != null)
            {
                panel.Hide(true);
            }
        }

        if (toast != null)
        {
            toast.gameObject.SetActive(false);
        }

        hud.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        SaveData.Changed += OnSaveChanged;
    }

    private void OnDisable()
    {
        SaveData.Changed -= OnSaveChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        SkinManager.Unlocked -= OnItemUnlocked;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RaceSwapped -= OnRaceSwapped;
        }
    }

    private void Start()
    {
        footer.playButton.onClick.AddListener(OnPlayClicked);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RaceSwapped += OnRaceSwapped;
        }

        RefreshBadges();
        EnterMenu();

        // After a restart the scene comes back exactly as on first launch, then Play is pressed for the player,
        // so the race starts through the same path (lobby, fade, countdown) as a normal click.
        if (StartNextRaceImmediately)
        {
            StartNextRaceImmediately = false;
            ScreenFade.SetBlack();   // hide the menu; the race fades in from black
            footer.playButton.onClick.Invoke();
        }
        else if (MissionManager.RewardPending && homeScreen != null)
        {
            // Back on the menu with a finished mission (e.g. right after the first race): give its reward now.
            StartCoroutine(ShowMissionRewardSoon());
        }
        else if (showVipOnLaunch && welcomeBack != null && WelcomeBackScreen.ShouldShowOnLaunch)
        {
            StartCoroutine(ShowVipSoon(OpenWelcomeBack));
        }
        else if (showVipOnLaunch && welcomeBack == null && vip != null && VipOfferScreen.ShouldShowOnLaunch)
        {
            StartCoroutine(ShowVipSoon(OpenVip));
        }
    }

    private IEnumerator ShowVipSoon(System.Action open)
    {
        yield return new WaitForSecondsRealtime(0.4f);   // let the menu settle first
        if (state == State.Menu && !daily.IsOpen)
        {
            open();
        }
    }

    private IEnumerator ShowMissionRewardSoon()
    {
        yield return new WaitForSecondsRealtime(0.6f);   // let the menu settle first
        if (state == State.Menu && MissionManager.RewardPending)
        {
            homeScreen.ShowMissionReward();
        }
    }

    private void Update()
    {
        if (state == State.Menu)
        {
            // The daily reward and free spin come back at midnight UTC, so keep the red dots honest.
            badgeTimer -= Time.unscaledDeltaTime;
            if (badgeTimer <= 0f)
            {
                badgeTimer = 1f;
                RefreshBadges();
            }
        }
        else if (state == State.Racing && GameManager.Instance != null && GameManager.Instance.gameOver)
        {
            BeginResults();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleBack();
        }
    }

    #region States

    private void EnterMenu()
    {
        state = State.Menu;
        menuRoot.SetActive(true);
        hud.gameObject.SetActive(false);
        RefreshMenu();
    }

    private void EnterRace()
    {
        state = State.Racing;
        CloseAllMenuPanels();
        menuRoot.SetActive(false);
        hud.gameObject.SetActive(true);
    }

    private void OnRaceSwapped()
    {
        if (state == State.Menu)
        {
            EnterRace();
        }
    }

    private void OnPlayClicked()
    {
        if (state != State.Menu)
        {
            return;
        }

        // GameManager starts the countdown from the same click (this button is its Start Button). With a menu lobby
        // it fades to black first and calls RaceSwapped, which switches the UI to the race HUD behind the fade.
        if (GameManager.Instance == null || !GameManager.Instance.UsesLobby)
        {
            EnterRace();
        }
    }

    private void BeginResults()
    {
        state = State.Results;
        bool failed = GameManager.Instance.playerFailed;
        int rank = GameManager.Instance.aiManager.PlayerRank;
        int racers = GameManager.Instance.aiManager.RacerCount;
        float raceTime = GameManager.Instance.RaceTime;
        RaceStats.EndRace(rank, failed);   // missions count this race
        if (failed)
        {
            QualifierScreen.ResetCup();   // losing a race starts the Beach Cup over
        }

        StartCoroutine(ShowResultsAfter(failed ? 1.6f : 2.8f, rank, racers, failed, raceTime));
    }

    private IEnumerator ShowResultsAfter(float seconds, int rank, int racers, bool failed, float raceTime)
    {
        yield return new WaitForSecondsRealtime(seconds);
        hud.gameObject.SetActive(false);
        GameManager.Instance.HideRaceTimer();
        results.Present(rank, racers, failed, raceTime);
    }

    /// <summary>
    /// Called when the results screen is closed. After a win the Beach Cup qualifier plays its next round first,
    /// then the game continues the same way it would have without it.
    /// </summary>
    public void FinishResults(bool failed)
    {
        if (failed || qualifier == null)
        {
            ReloadRace(failed);
            return;
        }

        if (!qualifierStarted)
        {
            qualifierStarted = true;   // both claim buttons end up here; play the round once
            StartCoroutine(PlayQualifier());
        }
    }

    private IEnumerator PlayQualifier()
    {
        yield return ScreenFade.Out(0.3f);
        results.Hide(true);
        qualifier.Play(() => ReloadRace(false));
    }

    #endregion

    #region Pause and scene flow

    public void OpenPause()
    {
        if (state != State.Racing || pause.IsOpen)
        {
            return;
        }

        Time.timeScale = 0f;
        pause.Show();
    }

    public void ResumeRace()
    {
        Time.timeScale = 1f;
        pause.Hide();
    }

    /// <summary>Reloads the scene. With autoStart the new race begins at once, otherwise the menu shows.</summary>
    public void ReloadRace(bool autoStart)
    {
        Time.timeScale = 1f;
        StartNextRaceImmediately = autoStart;
        GameManager.Instance.RestartLevel();
    }

    #endregion

    #region Menu pages and popups

    public void OpenShop()
    {
        OpenPage(shop);
    }

    public void OpenSkins()
    {
        OpenPage(skins);
    }

    public void OpenSpin()
    {
        OpenPage(spin);
    }

    public void ToggleShop()
    {
        if (shop.IsOpen) { CloseCurrentPage(); } else { OpenShop(); }
    }

    public void ToggleSkins()
    {
        if (skins.IsOpen) { CloseCurrentPage(); } else { OpenSkins(); }
    }

    public void OpenDaily()
    {
        if (state != State.Menu)
        {
            return;
        }

        daily.Show();
        RefreshMenu();
    }

    public void CloseDaily()
    {
        daily.Hide();
        RefreshMenu();
    }

    public void OpenVip()
    {
        if (state != State.Menu || vip == null)
        {
            return;
        }

        vip.Show();
        RefreshMenu();
    }

    public void CloseVip()
    {
        vip.Hide();
        RefreshMenu();
    }

    public void OpenWelcomeBack()
    {
        if (state != State.Menu || welcomeBack == null)
        {
            return;
        }

        welcomeBack.Show();
        RefreshMenu();
    }

    public void CloseWelcomeBack()
    {
        welcomeBack.Hide();
        RefreshMenu();
    }

    /// <summary>Shows the fail-safe offer on top of whatever is open (call it when a purchase fails).</summary>
    public void OpenPurchaseFailed()
    {
        if (state != State.Menu || purchaseFailed == null)
        {
            return;
        }

        purchaseFailed.Show();
        RefreshMenu();
    }

    public void ClosePurchaseFailed()
    {
        purchaseFailed.Hide();
        RefreshMenu();
    }

    public void OpenSettings()
    {
        settings.Show();
    }

    private void OpenPage(UIPanel page)
    {
        if (state != State.Menu)
        {
            return;
        }

        foreach (UIPanel other in new[] { shop, skins, spin })
        {
            if (other != page)
            {
                other.Hide(true);
            }
        }

        daily.Hide(true);
        page.Show();
        RefreshMenu();
    }

    public void CloseCurrentPage()
    {
        shop.Hide();
        skins.Hide();
        spin.Hide();
        RefreshMenu();
    }

    private void CloseAllMenuPanels()
    {
        shop.Hide(true);
        skins.Hide(true);
        spin.Hide(true);
        daily.Hide(true);
        if (vip != null) vip.Hide(true);
        if (welcomeBack != null) welcomeBack.Hide(true);
        if (purchaseFailed != null) purchaseFailed.Hide(true);
        settings.Hide(true);
        reward.Hide(true);
    }

    /// <summary>Shows exactly the parts of the menu that belong together: home, a page, or the daily popup.</summary>
    public void RefreshMenu()
    {
        bool page = shop.IsOpen || skins.IsOpen || spin.IsOpen;
        bool popup = daily.IsOpen || (vip != null && vip.IsOpen) || (welcomeBack != null && welcomeBack.IsOpen)
                     || (purchaseFailed != null && purchaseFailed.IsOpen);
        home.SetActive(state == State.Menu && !page && !popup);
        footer.gameObject.SetActive(state == State.Menu && !spin.IsOpen && !popup);
        footer.SetVariant(shop.IsOpen ? 1 : skins.IsOpen ? 2 : 0);
    }

    private void HandleBack()
    {
        if (state == State.Racing)
        {
            if (pause.IsOpen) { ResumeRace(); } else { OpenPause(); }
            return;
        }

        if (state != State.Menu)
        {
            return;
        }

        if (reward.IsOpen) { reward.Hide(); return; }
        if (settings.IsOpen) { settings.Hide(); return; }
        if (purchaseFailed != null && purchaseFailed.IsOpen) { ClosePurchaseFailed(); return; }
        if (daily.IsOpen) { CloseDaily(); return; }
        if (vip != null && vip.IsOpen) { CloseVip(); return; }
        if (welcomeBack != null && welcomeBack.IsOpen) { CloseWelcomeBack(); return; }
        if (shop.IsOpen || skins.IsOpen || spin.IsOpen) { CloseCurrentPage(); }
    }

    #endregion

    #region Helpers used by the screens

    /// <summary>Level items unlock on their own (the shop announces its own purchases), so tell the player.</summary>
    private void OnItemUnlocked(SkinData item)
    {
        if (item.unlock == SkinUnlock.PlayerLevel)
        {
            ShowToast((item.slot == SkinSlot.Floatie ? "New floatie: " : "New character: ") + item.displayName + "!");
        }
    }

    public void ShowToast(string message)
    {
        if (toast != null)
        {
            toast.Show(message);
        }
    }

    public Sprite GetRewardSprite(RewardType type)
    {
        return type == RewardType.Coins ? coinSprite : gemSprite;
    }

    /// <summary>Opens the "You got!" popup. The currency must already have been added.</summary>
    public void ShowReward(RewardType type, int amount, string title = "YOU GOT")
    {
        Haptics.Pulse();
        reward.Present(GetRewardSprite(type), "+" + UIFormat.Number(amount), title);
    }

    public void ShowReward(Sprite icon, string amountText, string title)
    {
        Haptics.Pulse();
        reward.Present(icon, amountText, title);
    }

    public void RefreshBadges()
    {
        if (homeScreen != null)
        {
            homeScreen.SetBadges(DailyRewardState.CanClaim, SpinState.FreeSpinAvailable ? 1 + SaveData.BonusSpins : SaveData.BonusSpins);
        }
    }

    // Sound and music switches are applied by AudioManager.
    private void OnSaveChanged()
    {
        RefreshBadges();
    }

    #endregion
}
