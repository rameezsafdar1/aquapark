using UnityEngine;
using Dreamteck.Splines;
using DG.Tweening;

public class Locomotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineFollower splineFollower;
    [SerializeField] private Animator anim;
    public Transform modelTransform;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private PlayerEffects _effects;

    [Header("Side Movement")]
    [SerializeField] private float horizontalSpeed = 5f;
    [SerializeField] private float jumpThreshold = 2.6f, yOffset;

    [Header("Forward Speed")]
    [SerializeField] private float airSpeed = 7f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpUpForce = 5f;
    [SerializeField] private float gravity = -9.8f;

    [Header("Landing Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float raycastDistance = 1.2f;

    private float xValue;
    private bool inAir = false;
    private float timeInAir;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationAngle;
    [SerializeField] private float rotationSpeed = 0.2f;
    [Tooltip("When localMotion offset of spline follower reaches this point, the rotationAngle will be reached completely")]
    [SerializeField] private float rotationReachPoint;

    private void Update()
    {

        if (GameManager.Instance.gameOver)
        {
            return;
        }

        if (!inAir)
        {
            HandleSplineMovement();
            CheckForJumpOff();
        }
        else
        {
            HandleAirMovement();

            timeInAir += Time.deltaTime;
            if (timeInAir >= 0.5f)
            {
                CheckForLanding();
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            gravity = jumpUpForce;
        }

    }

    private void HandleSplineMovement()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal") * horizontalSpeed * Time.deltaTime;
        xValue += horizontalInput;
        //xValue = Mathf.Clamp(xValue, -maxSideDistance, maxSideDistance);

        float yPos = Mathf.Abs(xValue / rotationReachPoint) * yOffset;
        modelTransform.DOLocalMove(new Vector3(xValue, yPos, 0), rotationSpeed);
        
        float targetRotationZ = (xValue / rotationReachPoint) * rotationAngle;

        modelTransform.DOLocalRotate(new Vector3(0f, 0f, targetRotationZ), rotationSpeed);
    }

    private void CheckForJumpOff()
    {
        if (Mathf.Abs(xValue) >= jumpThreshold)
        {
            JumpOffSpline();
        }
    }

    private void JumpOffSpline()
    {    
        splineFollower.follow = false;
        modelTransform.DOLocalRotate(new Vector3(0f, 0f, 0f), rotationSpeed);
        modelTransform.DOLocalMove(new Vector3(0f, 0f, 0f), rotationSpeed);

        Quaternion rot = transform.rotation;
        rot.x = 0;
        rot.z = 0;
        transform.rotation = rot;

        gravity = jumpUpForce;
        anim.SetBool("inAir", true);
        for (int i = 0; i < _effects.waterTrail.Length; i++)
        {
            //_effects.waterTrail[i].enableEmission = false;
            _effects.waterTrail[i].Stop();
        }
        inAir = true;
        _effects.AirFloatie();
    }

    private void HandleAirMovement()
    {
        gravity -= airSpeed * Time.deltaTime;

        Vector3 moveDir = transform.forward * airSpeed;
        moveDir.y = gravity;

        _controller.Move(moveDir * Time.deltaTime);

        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up, mouseX * 100f * Time.deltaTime);

        
        xValue = 0;
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

        for (int i = 0; i < _effects.waterTrail.Length; i++)
        {
            //_effects.waterTrail[i].enableEmission = true;
            _effects.waterTrail[i].gameObject.SetActive(false);
        }

        inAir = false;
        gravity = 0;
        splineFollower.motion.offset = Vector3.zero;

        splineFollower.follow = true;
        SplineSample sample = new SplineSample();
        splineFollower.Project(transform.position, ref sample);

        splineFollower.SetPercent(sample.percent);

        timeInAir = 0;
        _effects.landingEffect.SetActive(true);
        anim.SetBool("inAir", false);

        for (int i = 0; i < _effects.waterTrail.Length; i++)
        {
            //_effects.waterTrail[i].enableEmission = true;
            _effects.waterTrail[i].gameObject.SetActive(true);
        }
        _effects.LandFloatie();
        GameManager.Instance.aiManager.SetAiDynamically();
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Finish"))
        {
            if (!GameManager.Instance.gameOver)
            {
                GameManager.Instance.gameOver = true;
                GameManager.Instance.endCam.SetActive(true);
                PlayEndSequence();
            }
        }

        if (other.CompareTag("Jumper"))
        {
            JumpOffSpline();
        }
    }

    private void PlayEndSequence()
    {
        splineFollower.followSpeed = 10;
        splineFollower.spline = GameManager.Instance.endSpline;
        splineFollower.SetPercent(0);
        anim.SetBool("Dive", true);
    }

}
