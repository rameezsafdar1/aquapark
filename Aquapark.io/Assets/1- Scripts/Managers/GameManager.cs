using Dreamteck.Splines;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Runs before AiManager so the chosen level is in place before the racers are put on it.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool gameOver, gameStarted;
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

    /// <summary>The level that is being played, or null when the scene's own level is used.</summary>
    public LevelConfig CurrentLevel { get; private set; }
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

        aiManager.ApplyLevel(CurrentLevel);
    }

    /// <summary>Called by the Start button: hides it and runs the 3-2-1 countdown, then the race begins.</summary>
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

        StartCoroutine(startTimer());
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

    public void RestartLevel()
    {
        SceneManager.LoadScene(0);
    }

}
