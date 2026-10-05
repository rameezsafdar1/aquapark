using UnityEngine;

public class AiEffects : MonoBehaviour
{
    private int currentFloatie;
    public GameObject[] Floaties, FloatiesInHand;

    [Header("Water effects (same as the player's)")]
    [Tooltip("Drops trailing behind the ring.")]
    public ParticleSystem[] waterTrail;
    [Tooltip("Splash played when landing back on the slide.")]
    public GameObject landingEffect;
    [Tooltip("Wake around the ring while it is on the water.")]
    public GameObject waterWaves;
    /// <summary>The active skin (set by its AvatarPass); holds the foot splashes.</summary>
    [HideInInspector] public AvatarPass skin;

    /// <summary>The ring this racer rides on (picked once by SelectFloatie).</summary>
    public GameObject CurrentFloatie => Floaties[currentFloatie];

    public void SelectFloatie()
    {
        currentFloatie = Random.Range(0, Floaties.Length);
        Floaties[currentFloatie].SetActive(true);
    }

    /// <summary>Switches on the water effects when the race starts (they stay hidden in the start grid).</summary>
    public void StartRaceEffects()
    {
        if (skin != null)
        {
            foreach (ParticleSystem splash in skin.waterTrail)
            {
                splash.gameObject.SetActive(true);
            }
        }

        SetWaterEffects(true);
    }

    public void AirFloatie()
    {
        for (int i = 0; i < Floaties.Length; i++)
        {
            Floaties[i].SetActive(false);
            FloatiesInHand[i].SetActive(false);
        }
        FloatiesInHand[currentFloatie].SetActive(true);
        SetWaterEffects(false);
    }

    public void LandFloatie()
    {
        for (int i = 0; i < Floaties.Length; i++)
        {
            Floaties[i].SetActive(false);
            FloatiesInHand[i].SetActive(false);
        }
        Floaties[currentFloatie].SetActive(true);
        SetWaterEffects(true);
        if (landingEffect != null)
        {
            landingEffect.SetActive(true);   // disables itself when the splash ends
        }
    }

    public void NoFloatie()
    {
        for (int i = 0; i < Floaties.Length; i++)
        {
            Floaties[i].SetActive(false);
            FloatiesInHand[i].SetActive(false);
        }
        SetWaterEffects(false);
    }

    // Wake and drop trail are on while the ring is on the water, off in the air and after the finish.
    private void SetWaterEffects(bool on)
    {
        if (waterWaves != null)
        {
            waterWaves.SetActive(on);
        }

        foreach (ParticleSystem trail in waterTrail)
        {
            if (trail == null)
            {
                continue;
            }

            if (on)
            {
                trail.gameObject.SetActive(true);
                trail.Play();
            }
            else
            {
                trail.Stop();
            }
        }
    }
}
