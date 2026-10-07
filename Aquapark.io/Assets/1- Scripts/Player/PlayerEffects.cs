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

    [SerializeField] private AvatarPass[] Skins;
    [SerializeField] private AudioSource screamAudio;

    /// <summary>Every character model on the player (one is active). Used by PlayerLoadout.</summary>
    public AvatarPass[] SkinModels => Skins;
    /// <summary>Every floatie model the player can ride, same order as each skin's held floaties.</summary>
    public GameObject[] FloatieModels => Floaties;
    /// <summary>Index into FloatieModels of the floatie shown when riding.</summary>
    public int CurrentFloatie { get => currentFloatie; set => currentFloatie = Mathf.Clamp(value, 0, Floaties.Length - 1); }
    /// <summary>True once the race effects are on (LoadParticles); before that the skins' foot splashes stay hidden.</summary>
    public bool RaceStarted { get; private set; }

    private void Start()
    {
        for (int i = 0; i < Skins.Length; i++)
        {
            if (Skins[i].gameObject.activeSelf)
            {
                foreach (var particle in Skins[i].waterTrail)
                {
                    particle.gameObject.SetActive(false);
                }
            }
        }
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
        screamAudio.Play();
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
        waterWaveParticle.SetActive(true);
    }

    public void LoadParticles()
    {
        RaceStarted = true;
        for (int i = 0; i < Skins.Length; i++)
        {
            if (Skins[i].gameObject.activeSelf)
            {
                foreach (var particle in Skins[i].waterTrail)
                {
                    particle.gameObject.SetActive(true);
                }                
            }
        }

        GetComponent<Animator>().SetBool("Start", true);
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
