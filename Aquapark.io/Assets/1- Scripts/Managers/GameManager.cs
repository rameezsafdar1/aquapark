using Dreamteck.Splines;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool gameOver, gameStarted;
    public GameObject endCam;
    public SplineComputer endSpline;
    public AiManager aiManager;
    
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

    private void Update()
    {
        if (!gameStarted)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                gameStarted = true;
                aiManager.StartGame();
            }
        }
    }
}
