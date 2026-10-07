using UnityEngine;

/// <summary>
/// Puts the equipped character and floatie (from the Skins shop) on the player. Runs on start and again whenever
/// something is equipped, so the lobby model changes straight away. Items are matched by SkinData.modelName against the
/// model objects PlayerEffects already knows (Player/Character children and Character/Floaties children).
/// </summary>
[RequireComponent(typeof(PlayerEffects))]
public class PlayerLoadout : MonoBehaviour
{
    private PlayerEffects effects;

    private void Awake()
    {
        effects = GetComponent<PlayerEffects>();
    }

    private void OnEnable()
    {
        SkinManager.Equipped += Apply;
    }

    private void OnDisable()
    {
        SkinManager.Equipped -= Apply;
    }

    // Start, not Awake: UIManager registers the skin lists with SkinManager in its Awake.
    private void Start()
    {
        Apply(SkinManager.GetEquippedItem(SkinSlot.Character));
        Apply(SkinManager.GetEquippedItem(SkinSlot.Floatie));
    }

    public void Apply(SkinData item)
    {
        if (item == null || string.IsNullOrEmpty(item.modelName))
        {
            return;
        }

        if (item.slot == SkinSlot.Floatie)
        {
            ApplyFloatie(item.modelName);
        }
        else
        {
            ApplyCharacter(item.modelName);
        }
    }

    private void ApplyCharacter(string modelName)
    {
        AvatarPass target = null;
        foreach (AvatarPass skin in effects.SkinModels)
        {
            if (skin != null && SameName(skin.name, modelName))
            {
                target = skin;
            }
        }

        if (target == null)
        {
            Debug.LogWarning("PlayerLoadout: no character model named " + modelName);
            return;
        }

        foreach (AvatarPass skin in effects.SkinModels)
        {
            if (skin != null && skin != target)
            {
                skin.gameObject.SetActive(false);
            }
        }

        target.gameObject.SetActive(true);
        target.Bind();

        // Foot splashes only show once the race effects are on (PlayerEffects.LoadParticles).
        foreach (ParticleSystem splash in target.waterTrail)
        {
            if (splash != null)
            {
                splash.gameObject.SetActive(effects.RaceStarted);
            }
        }
    }

    private void ApplyFloatie(string modelName)
    {
        GameObject[] floaties = effects.FloatieModels;
        for (int i = 0; i < floaties.Length; i++)
        {
            if (floaties[i] == null || !SameName(floaties[i].name, modelName))
            {
                continue;
            }

            // If a ring is on screen (riding), swap it in place; otherwise it shows the next time the player lands.
            bool riding = floaties[effects.CurrentFloatie] != null && floaties[effects.CurrentFloatie].activeSelf;
            effects.CurrentFloatie = i;
            if (riding)
            {
                effects.LandFloatie();
            }

            return;
        }

        Debug.LogWarning("PlayerLoadout: no floatie model named " + modelName);
    }

    private static bool SameName(string a, string b)
    {
        return string.Equals(a.Trim(), b.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
