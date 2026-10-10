using UnityEngine;

public class PushHandler : MonoBehaviour
{
    [SerializeField] private Locomotion mainPlayer;
    [SerializeField] private float frontThreshold, pushValue, boostValue;
    [Tooltip("Chance (0-1) that an AI racer bumping the player from the side knocks them off the slide, when the player is not steering. A steering player always wins side bumps.")]
    [Range(0f, 1f)]
    [SerializeField] private float knockOffChance = 1f;
    private bool boosted;
    private AiPush lastKnocker;        // the AI racer that last knocked the player off
    private float lastKnockTime = -999f;
    private float boostTime;

    private void Update()
    {
        if (boosted)
        {
            boostTime += Time.deltaTime;
            if (boostTime >= 1)
            {
                boostTime = 0;
                mainPlayer.splineFollower.followSpeed = mainPlayer.initialSpeed;
                boosted = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Agent"))
        {
            return;
        }

        Vector3 toOther = (other.transform.position - transform.position).normalized;
        float forwardDot = Vector3.Dot(transform.forward, toOther);
        float rightDot = Vector3.Dot(transform.right, toOther);
        AiPush push = other.GetComponent<AiPush>();

        // The racer that just won a bump touches the flying player again a moment later: that is not a new bump.
        if (push != null && push == lastKnocker && Time.time - lastKnockTime < 1f)
        {
            return;
        }

        FollowCamDirector.ShakeBump();

        if (forwardDot > frontThreshold)
        {
            // Rammed an AI racer from behind: it gets shoved ahead.
            AudioManager.Play(Sfx.Hit);
            if (push != null)
            {
                push.TakePushFromBehind(pushValue);
            }
        }
        else if (forwardDot < -frontThreshold)
        {
            // Rammed from behind: the player gets a speed boost.
            AudioManager.Play(Sfx.GotHit);
            Boost();
        }
        else
        {
            float side = rightDot > 0f ? 1f : -1f;   // which side the AI racer is on
            // Frozen racers cannot knock anyone off, and a giant player cannot be knocked off.
            bool aiWins = push != null && !push.IsInAir && !push.IsFrozen && !RaceBuffs.IsActive(BuffType.Giant) &&
                          mainPlayer.CanBeKnockedOff && Random.value < knockOffChance;
            if (aiWins)
            {
                // The player was not steering: the AI racer wins the side bump and throws the player off, away from it.
                AudioManager.Play(Sfx.GotHit);
                lastKnocker = push;
                lastKnockTime = Time.time;
                mainPlayer.KnockOff(-side);
            }
            else
            {
                // The player wins: the AI racer is thrown off (counts for the knock-off missions if it falls).
                AudioManager.Play(Sfx.Hit);
                if (push != null)
                {
                    push.PushedByPlayer();
                    push.Jump(side);
                }
            }
        }
    }

    private void Boost()
    {
        mainPlayer.splineFollower.followSpeed += boostValue;
        boosted = true;
    }
}
