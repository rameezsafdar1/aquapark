using UnityEngine;

public class AvatarPass : MonoBehaviour
{
    [SerializeField] private Animator anim;
    private Animator localAnim;
    [SerializeField] private PlayerEffects Effects;
    [SerializeField] private GameObject[] FloatiesInHand;

    private void Awake()
    {   
        localAnim = GetComponent<Animator>();
        anim.avatar = localAnim.avatar;
        Effects.FloatiesInHand = FloatiesInHand;
    }
}
