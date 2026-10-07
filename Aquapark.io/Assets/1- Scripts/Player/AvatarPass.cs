using System.Collections.Generic;
using UnityEngine;

public class AvatarPass : MonoBehaviour
{
    [SerializeField] private Animator anim;
    private Animator localAnim;
    [SerializeField] private PlayerEffects Effects;
    [SerializeField] private AiEffects aiEffects;
    [SerializeField] private GameObject[] FloatiesInHand;
    public List<ParticleSystem> waterTrail = new List<ParticleSystem>();
    public GameObject particlePrefab;
    public string[] targetNames;

    private void Awake()
    {
        if (Effects != null)
        {
            Bind();
        }
        else
        {
            localAnim = GetComponent<Animator>();
            anim.avatar = localAnim.avatar;
            aiEffects.FloatiesInHand = FloatiesInHand;
            aiEffects.skin = this;
            aiEffects.SelectFloatie();
        }
    }

    /// <summary>
    /// Makes this the player's body: the shared Animator uses this model's avatar and the effects use its held floaties.
    /// Awake only runs on the first activation, so PlayerLoadout calls this again whenever it switches skins.
    /// </summary>
    public void Bind()
    {
        if (localAnim == null)
        {
            localAnim = GetComponent<Animator>();
        }

        anim.avatar = localAnim.avatar;
        if (Effects != null)
        {
            Effects.FloatiesInHand = FloatiesInHand;
        }
    }

    [ContextMenu("Place Particles on Matching Children")]
    public void PlaceParticlesOnChildren()
    {
        int placedCount = 0;
        Transform[] allChildren = transform.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            foreach (string name in targetNames)
            {
                if (child.name.Equals(name))
                {
                    // Check if a particle system already exists under this child
                    ParticleSystem existing = child.GetComponentInChildren<ParticleSystem>(true);

                    if (existing != null)
                    {
                        if (!waterTrail.Contains(existing))
                        {
                            waterTrail.Add(existing);
                            Debug.Log($"Existing particle found and added to list on: {child.name}");
                        }
                    }
                    else
                    {
                        GameObject go = Instantiate(particlePrefab, child.position, child.rotation, child);
                        ParticleSystem particleSystem = go.GetComponent<ParticleSystem>();
                        waterTrail.Add(particleSystem);

                        go.transform.localScale = Vector3.one;
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localPosition = Vector3.zero;

                        placedCount++;
                        Debug.Log($"Particle placed on: {child.name}");
                    }

                    break; // avoid double match
                }
            }

            Debug.Log($"Total particles placed: {placedCount}");
        }
    }
}
