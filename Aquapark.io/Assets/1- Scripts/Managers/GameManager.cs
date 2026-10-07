using Dreamteck.Splines;
using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runs before AiManager so the chosen level is in place before the racers are put on it.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool gameOver, gameStarted;
    /// <summary>True when the race ended because the player died instead of finishing.</summary>
    [HideInInspector] public bool playerFailed;
    public GameObject endCam;
    public SplineComputer endSpline;
    public AiManager aiManager;

    [Header("Levels")]
    [Tooltip("All level prefabs. One is picked at random every time the scene starts. If empty, the level already in the scene is used.")]
    [SerializeField] private LevelConfig[] levels;
    [Tooltip("The chosen level is created under this object. Any levels already under it are switched off.")]
    [SerializeField] private Transform levelsRoot;
    [Tooltip("For testing: play this level (index into the array) instead of a random one. -1 = random.")]
    [SerializeField] private int forceLevelIndex = -1;
    [Tooltip("Scales the player and AI speeds of every level (1.15 = 15% faster). The start boost is added on top unchanged.")]
    [SerializeField, Range(0.5f, 2f)] private float raceSpeedMultiplier = 1.15f;

    /// <summary>The level that is being played, or null when the scene's own level is used.</summary>
    public LevelConfig CurrentLevel { get; private set; }
    [Header("Menu lobby")]
    [Tooltip("Short straight tube the player waits on in the menu. When Play is pressed the screen fades and the racers are moved onto the real level. Leave empty to start on the level directly.")]
    [SerializeField] private MenuLobby lobby;
    [SerializeField] private float fadeSeconds = 0.35f;

    /// <summary>Raised while the screen is black, right after the racers were moved onto the level.</summary>
    public event System.Action RaceSwapped;

    /// <summary>True while the player is waiting on the menu lobby (so Play starts with a fade to the level).</summary>
    public bool UsesLobby => lobby != null && lobby.gameObject.activeSelf;

    private int startWait;
    [SerializeField] private TextMeshProUGUI startText;
    [Header("Start UI")]
    [Tooltip("Pressing this starts the countdown. If empty, a tap anywhere starts it instead.")]
    [SerializeField] private Button startButton;
    [Tooltip("Shows the elapsed race time once the race has started.")]
    [SerializeField] private TextMeshProUGUI raceTimerText;
    private bool timerStarted;
    private float raceTime;

    /// <summary>Seconds since the race started (stops when the race ends).</summary>
    public float RaceTime => raceTime;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        startWait = 3;

        LoadRandomLevel();
        SetUpLobby();

        if (startButton != null)
        {
            startButton.onClick.AddListener(BeginCountdown);
        }

        if (raceTimerText != null)
        {
            raceTimerText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Old behaviour kept as a fallback when no start button is assigned.
        if (startButton == null && !gameStarted && !timerStarted && Input.GetKeyDown(KeyCode.Mouse0))
        {
            BeginCountdown();
        }

        if (gameStarted && !gameOver)
        {
            raceTime += Time.deltaTime;
            UpdateRaceTimerText();
        }
    }

    private void LoadRandomLevel()
    {
        if (levels == null || levels.Length == 0)
        {
            return;
        }

        int index = forceLevelIndex >= 0 && forceLevelIndex < levels.Length ? forceLevelIndex : Random.Range(0, levels.Length);
        LevelConfig prefab = levels[index];
        if (prefab == null)
        {
            Debug.LogWarning($"GameManager: level {index} in the levels array is empty, using the level in the scene.");
            return;
        }

        // Switch off whatever level the scene already contains.
        if (levelsRoot != null)
        {
            foreach (Transform child in levelsRoot)
            {
                child.gameObject.SetActive(false);
            }
        }

        CurrentLevel = Instantiate(prefab, levelsRoot);
        CurrentLevel.name = prefab.name;

        endSpline = CurrentLevel.endSpline;

        if (endCam != null && CurrentLevel.endCamAnchor != null)
        {
            endCam.transform.SetPositionAndRotation(CurrentLevel.endCamAnchor.position, CurrentLevel.endCamAnchor.rotation);
        }

        aiManager.ApplyLevel(CurrentLevel, raceSpeedMultiplier);
    }

    private void SetUpLobby()
    {
        if (lobby == null)
        {
            return;
        }

        if (aiManager == null)
        {
            lobby.gameObject.SetActive(false);
            return;
        }

        lobby.gameObject.SetActive(true);
        aiManager.EnterLobby(lobby);

        // The real level stays hidden until the race starts, so it is not visible beyond the lobby.
        if (CurrentLevel != null)
        {
            CurrentLevel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Called by the Start button: hides it, fades from the lobby to the level (when there is a lobby),
    /// then runs the 3-2-1 countdown and the race begins.
    /// </summary>
    public void BeginCountdown()
    {
        if (timerStarted || gameStarted)
        {
            return;
        }

        timerStarted = true;

        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        StartCoroutine(BeginRoutine());
    }

    private IEnumerator BeginRoutine()
    {
        if (lobby != null && lobby.gameObject.activeSelf)
        {
            yield return ScreenFade.Out(fadeSeconds);

            if (CurrentLevel != null)
            {
                CurrentLevel.gameObject.SetActive(true);
            }

            Transform player = aiManager.PlayerTransform;
            Vector3 before = player.position;
            aiManager.EnterRace();
            lobby.gameObject.SetActive(false);
            yield return null;   // let the followers settle on the level

            // Stop the follow camera from swooping across the map to catch up.
            Vector3 delta = player.position - before;
            foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            {
                cam.OnTargetObjectWarped(player, delta);
            }

            RaceSwapped?.Invoke();
            yield return null;
            yield return ScreenFade.In(fadeSeconds);
        }

        yield return startTimer();
    }

    private IEnumerator startTimer()
    {
        startText.gameObject.SetActive(true);
        startText.text = startWait.ToString();
        for (int i = 0; i < startWait; i++)
        {
            yield return new WaitForSeconds(1f);
            startText.text = (startWait - i - 1).ToString();

            if ((startWait - i - 1) <= 0)
            {
                startText.text = "GO!";
                yield return new WaitForSeconds(0.5f);
                startText.gameObject.SetActive(false);
            }

        }
        gameStarted = true;
        raceTime = 0f;

        if (raceTimerText != null)
        {
            raceTimerText.gameObject.SetActive(true);
            UpdateRaceTimerText();
        }

        aiManager.StartGame();
    }

    private void UpdateRaceTimerText()
    {
        if (raceTimerText == null)
        {
            return;
        }

        int minutes = (int)(raceTime / 60f);
        raceTimerText.text = $"{minutes}:{raceTime % 60f:00.0}";
    }

    /// <summary>Hides the race clock (the results screen replaces it).</summary>
    public void HideRaceTimer()
    {
        if (raceTimerText != null)
        {
            raceTimerText.gameObject.SetActive(false);
        }
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(0);
    }

}
