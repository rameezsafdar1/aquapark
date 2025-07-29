using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    public GameObject landingEffect;
    public ParticleSystem[] waterTrail;

    [SerializeField] private GameObject[] Floaties;
    [HideInInspector] public GameObject[] FloatiesInHand;
    [SerializeField] private GameObject waterWaveParticle, glider, projector;
    public GameObject windLines;
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
        waterWaveParticle.SetActive(false);
        glider.SetActive(true);
        projector.SetActive(true);
    }

    public void LandFloatie()
    {
        for (int i = 0; i < Floaties.Length; i++)
        {
            Floaties[i].SetActive(false);
            FloatiesInHand[i].SetActive(false);
        }
        Floaties[currentFloatie].SetActive(true);
        waterWaveParticle.SetActive(true);
        glider.SetActive(false);
        projector.SetActive(false);
    }

    public void NoFloatie()
    {
        for (int i = 0; i < Floaties.Length; i++)
        {
            Floaties[i].SetActive(false);
            FloatiesInHand[i].SetActive(false);
        }
        glider.SetActive(false);
        projector.SetActive(false); 
        waterWaveParticle.SetActive(false);
    }

}
