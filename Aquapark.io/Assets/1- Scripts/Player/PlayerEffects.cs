using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    public GameObject landingEffect;
    public ParticleSystem[] waterTrail;

    [SerializeField] private GameObject[] Floaties;
    [HideInInspector] public GameObject[] FloatiesInHand;
    [SerializeField] private int currentFloatie;

    private void Start()
    {
        LandFloatie();
    }


    public void AirFloatie()
    {
        for (int i = 0; i < Floaties.Length; i++)
        {
            Floaties[i].SetActive(false);
            FloatiesInHand[i].SetActive(false);
        }
        FloatiesInHand[currentFloatie].SetActive(true);
    }

    public void LandFloatie()
    {
        for (int i = 0; i < Floaties.Length; i++)
        {
            Floaties[i].SetActive(false);
            FloatiesInHand[i].SetActive(false);
        }
        Floaties[currentFloatie].SetActive(true);
    }

}
