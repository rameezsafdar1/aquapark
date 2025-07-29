using Dreamteck.Splines;
using UnityEngine;

public class JumpSpawner : MonoBehaviour
{
    [SerializeField] private GameObject Fountain;
    [SerializeField] private SplineComputer spline;    
    [SerializeField] private int totalFountains;


    private void Start()
    {
        for (int i = 0; i < totalFountains; i++)
        {
            float randomPos = Random.Range(0.2f, 0.7f);

            Instantiate(Fountain, spline.EvaluatePosition(randomPos), Quaternion.identity, transform);

        }
    }
}
