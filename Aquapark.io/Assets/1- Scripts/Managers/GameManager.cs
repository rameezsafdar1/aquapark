using Dreamteck.Splines;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool gameOver, gameStarted;
    public GameObject endCam;
    public SplineComputer endSpline;
    public AiManager aiManager;
    private int startWait;
    [SerializeField] private TextMeshProUGUI startText;
    private bool timerStarted;
    
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
    }

    private void Update()
    {
        if (!gameStarted && !timerStarted)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                StartCoroutine(startTimer());
                timerStarted = true;
            }
        }
    }

    private IEnumerator startTimer()
    {
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
        aiManager.StartGame();
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(0);
    }

}
