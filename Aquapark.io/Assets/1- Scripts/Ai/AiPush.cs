using Dreamteck.Splines;
using UnityEngine;

public class AiPush : MonoBehaviour
{
    [SerializeField] private AiMover MainAi;
    [SerializeField] private float pushDuration, frontThreshold, pushValue;
    private float speed;
    private bool pushed;
    private float timePassed;


    private void Start()
    {
        speed = MainAi.follower.followSpeed;
    }

    private void Update()
    {
        if (pushed)
        {
            timePassed += Time.deltaTime;
            if (timePassed >= pushDuration)
            {
                MainAi.follower.followSpeed = speed;
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
    }

    public void Jump()
    {
        MainAi.Jump();
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
                    push.Jump();
                }
            }
            else
            {
                AiPush push = other.GetComponent<AiPush>();
                if (push != null)
                {
                    push.Jump();
                }
            }
        }
    }



}
