using UnityEngine;

public class AiEffects : MonoBehaviour
{
    private int currentFloatie;
    public GameObject[] Floaties, FloatiesInHand;

    public void SelectFloatie()
    {
        currentFloatie = Random.Range(0, Floaties.Length);
        Floaties[currentFloatie].SetActive(true);
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

    public void NoFloatie()
    {
        for (int i = 0; i < Floaties.Length; i++)
        {
            Floaties[i].SetActive(false);
            FloatiesInHand[i].SetActive(false);
        }
    }
}
