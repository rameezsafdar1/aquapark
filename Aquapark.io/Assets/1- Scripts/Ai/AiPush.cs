using Dreamteck.Splines;
using UnityEngine;

public class AiPush : MonoBehaviour
{
    [SerializeField] private AiMover MainAi;
    [SerializeField] private float pushDuration, frontThreshold, pushValue;
    private float speed;
    private float pushTotal;   // speed added by the pushes still running (removed again when they end)
    private bool pushed;
    private float timePassed;


    private void Start()
    {
        speed = MainAi.follower.followSpeed;
    }

    private void Update()
    {
        if (pushed && !(UseRelativePush && MainAi.IsFrozen))   // frozen: the buff holds the speed, finish the push after it
        {
            timePassed += Time.deltaTime;
            if (timePassed >= pushDuration)
            {
                if (UseRelativePush)
                {
                    // Take back only what the pushes added, so a catch-up boost or pacesetter speed set meanwhile survives.
                    MainAi.follower.followSpeed -= pushTotal;
                }
                else
                {
                    MainAi.follower.followSpeed = speed;
                }

                pushTotal = 0f;
                timePassed = 0f;
                pushed = false;
            }
        }
    }


    public void TakePushFromBehind(float pushValue)
    {
        pushed = true;
        timePassed = 0;
        Debug.Log("got pushed");
        MainAi.follower.followSpeed += pushValue;
        pushTotal += pushValue;
    }

    private static bool UseRelativePush => GameManager.Instance != null && GameManager.Instance.aiManager != null && GameManager.Instance.aiManager.PacesettersEnabled;

    /// <summary>True while this racer is off the slide (it cannot knock the player off then).</summary>
    public bool IsInAir => MainAi.IsInAir;

    /// <summary>True while the freeze buff holds this racer (it cannot knock the player off then).</summary>
    public bool IsFrozen => MainAi.IsFrozen;

    /// <summary>The player shoved this racer sideways (if it falls off the slide, it counts for the knock-off missions).</summary>
    public void PushedByPlayer()
    {
        MainAi.MarkPushedByPlayer();
    }

    public void Jump(float horizontalValue)
    {
        MainAi.Jump(horizontalValue);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Agent"))
        {
            Vector3 toOther = (other.transform.position - transform.position).normalized;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            float forwardDot = Vector3.Dot(forward, toOther);
            float rightDot = Vector3.Dot(right, toOther);

            if (forwardDot > frontThreshold)
            {
                AiPush push = other.GetComponent<AiPush>();
                if (push != null)
                {
                    push.TakePushFromBehind(pushValue);
                }
            }
            else if (forwardDot < -frontThreshold)
            {
                //Debug.Log("Enemy is behind");
            }
            else if (rightDot > 0)
            {
                AiPush push = other.GetComponent<AiPush>();
                if (push != null)
                {
                    push.Jump(1);
                }
            }
            else
            {
                AiPush push = other.GetComponent<AiPush>();
                if (push != null)
                {
                    push.Jump(-1);
                }
            }
        }
    }



}
