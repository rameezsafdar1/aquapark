using UnityEngine;

public class PushHandler : MonoBehaviour
{
    [SerializeField] private Locomotion mainPlayer;
    [SerializeField] private float frontThreshold, pushValue, boostValue;
    private bool boosted;
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
        if (other.CompareTag("Agent"))
        {
            Vector3 toOther = (other.transform.position - transform.position).normalized;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            float forwardDot = Vector3.Dot(forward, toOther);
            float rightDot = Vector3.Dot(right, toOther);

            if (forwardDot > frontThreshold)
            {
                Debug.Log("Enemy is in front");
                AiPush push = other.GetComponent<AiPush>();
                if (push != null)
                {
                    push.TakePushFromBehind(pushValue);
                }
            }
            else if (forwardDot < -frontThreshold)
            {
                //Debug.Log("Enemy is behind");
                Boost();
            }
            else if (rightDot > 0)
            {
                AiPush push = other.GetComponent<AiPush>();
                if (push != null)
                {
                    push.Jump(1);
                }
                Debug.Log("Enemy is to the right");
            }
            else
            {
                AiPush push = other.GetComponent<AiPush>();
                if (push != null)
                {
                    push.Jump(-1);
                }
                Debug.Log("Enemy is to the left");
            }
        }
    }

    private void Boost()
    {
        mainPlayer.splineFollower.followSpeed += boostValue;
        boosted = true;
    }
}
