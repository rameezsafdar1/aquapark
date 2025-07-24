using DG.Tweening;
using Dreamteck.Splines;
using Unity.Android.Gradle.Manifest;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class AiMover : MonoBehaviour
{
    public SplineFollower follower;
    [SerializeField] private AiEffects Effects;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private float airSpeed = 7f;
    [SerializeField] private Animator anim;
    [SerializeField] private float minimumDelay, maximumDelay, sideLength, moveSpeed, yOffset, jumpForce, raycastDistance;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform characterModel;
    private float delay, timePassed, moveDis, gravity, timeInAir;
    private bool inAir;

    private void Start()
    {
        moveDis = Random.Range(-sideLength, sideLength);
        float yPos = Mathf.Abs(moveDis / moveSpeed) * yOffset;

        characterModel.DOLocalMove(new Vector3(moveDis, yPos, 0), 0f);
        float targetRotationZ = (moveDis / moveSpeed) * 50;

        characterModel.DOLocalRotate(new Vector3(0f, 0f, targetRotationZ), 0f);
        delay = Random.Range(minimumDelay, maximumDelay);
    }

    private void Update()
    {
        if (!GameManager.Instance.gameStarted)
        {
            return;
        }

        if (inAir)
        {
            HandleAirMovement();
            timeInAir += Time.deltaTime;
            if (timeInAir >= 0.5f)
            {
                CheckForLanding();
            }
        }
        else
        {
            timePassed += Time.deltaTime;
            if (timePassed >= delay)
            {
                GetSidewaysPositionOnSlide();
                timePassed = 0;
            }
        }
    }

    private void CheckForLanding()
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        if (Physics.Raycast(ray, raycastDistance, groundLayer))
        {
            LandBackOnSpline();
        }
    }

    private void LandBackOnSpline()
    {
        inAir = false;
        gravity = 0;

        follower.follow = true;
        SplineSample sample = new SplineSample();
        follower.Project(transform.position, ref sample);

        follower.SetPercent(sample.percent);
        timeInAir = 0;
        anim.SetBool("inAir", false);

        Effects.LandFloatie();
        //GameManager.Instance.aiManager.SetAiDynamically();
    }

    private void HandleAirMovement()
    {
        gravity -= airSpeed * Time.deltaTime;

        Vector3 moveDir = transform.forward * airSpeed;
        moveDir.y = gravity;

        _controller.Move(moveDir * Time.deltaTime);

        //float mouseX = Input.GetAxis("Mouse X");
        //transform.Rotate(Vector3.up, mouseX * 100f * Time.deltaTime);
    }


    private void GetSidewaysPositionOnSlide()
    {
        moveDis = Random.Range(-sideLength, sideLength);
        float yPos = Mathf.Abs(moveDis / moveSpeed) * yOffset;

        characterModel.DOLocalMove(new Vector3(moveDis, yPos, 0), moveSpeed);
        float targetRotationZ = (moveDis / moveSpeed) * 50;

        characterModel.DOLocalRotate(new Vector3(0f, 0f, targetRotationZ), moveSpeed);
        delay = Random.Range(minimumDelay, maximumDelay);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Finish"))
        {
            PlayEndSequence();
        }
    }

    private void PlayEndSequence()
    {
        SplineFollower splineFollower = GetComponent<SplineFollower>();
        splineFollower.followSpeed = 10;
        splineFollower.spline = GameManager.Instance.endSpline;
        splineFollower.SetPercent(0);
        anim.SetBool("Dive", true);
    }

    public void Jump()
    {
        anim.SetBool("inAir", true);
        characterModel.DOLocalMove(Vector3.zero, 0.1f);
        characterModel.DOLocalRotate(Vector3.zero, 0.1f);
        follower.follow = false;
        inAir = true;
        gravity = jumpForce;
        Effects.AirFloatie();
    }
}
