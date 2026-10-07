using UnityEngine;
using Dreamteck.Splines;
using DG.Tweening;
using UnityEngine.Serialization;

public class Locomotion : MonoBehaviour
{
    #region Variables
    [Header("References")]
    public SplineFollower splineFollower;
    [SerializeField] private Animator anim;
    public Transform modelTransform;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private PlayerEffects _effects;

    [Header("Side Movement")]
    [SerializeField] private float horizontalSpeed = 5f;
    [SerializeField] private float jumpThreshold = 2.6f;

    [Header("Air Movement")]
    [Tooltip("Forward speed while in the air (gliding).")]
    [SerializeField] private float glideSpeed = 45f;
    [Tooltip("How fast the fall speeds up while in the air. Was shared with the forward speed (old 'airSpeed'), so the scene value carries over.")]
    [FormerlySerializedAs("airSpeed")]
    [SerializeField] private float airGravity = 7f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpUpForce = 5f;
    [SerializeField] private float gravity = -9.8f;

    [Header("Landing Detection")]
    [SerializeField] private LayerMask groundLayer, runLayer;
    [SerializeField] private float raycastDistance = 1.2f;

    private float xValue;
    private bool inAir = false;
    /// <summary>True while the player is off the slide (jumped or fell).</summary>
    public bool IsInAir => inAir;
    private float timeInAir;
    private Vector3 positionBeforeAirMove;   // where the last air move started (for the landing sweep)
    private bool launchedByFountain;         // the jump being started comes from a fountain (missions)
    private double takeoffPercent;           // spline percent where the current jump started (shortcut missions)
    private Vector3 takeoffPosition;

    [Header("Rotation Settings")]
    [Tooltip("Seconds the model takes to slide sideways. Its height and tilt follow the slide surface (SlideSurface).")]
    [SerializeField] private float rotationSpeed = 0.2f;
    [Tooltip("Degrees the player leans when steered to the jump-off edge, so there is tilt on the flat floor too. On the curved wall the wall's own slope is used when it is steeper.")]
    [SerializeField] private float edgeLeanAngle = 12f;
    [SerializeField] private Animator shakeAnim;
    [Tooltip("Small speed jitter of the character while riding the slide.")]
    [SerializeField] private RideBob rideBob = new RideBob();
    [HideInInspector] public float initialSpeed;
    private bool collidedFinish;

    [Header("Getting Knocked Off")]
    [Tooltip("Seconds after the last steering input (left/right, or holding the screen / mouse) during which the player still counts as steering. Steering players win side bumps; idle ones can be knocked off by AI racers.")]
    [SerializeField] private float steerGrace = 0.25f;
    [Tooltip("How far sideways an AI racer throws the player, in the player's local units (the jump-off edge is Jump Threshold).")]
    [SerializeField] private float knockDistance = 1.2f;
    [Tooltip("Seconds the sideways throw lasts.")]
    [SerializeField] private float knockDuration = 0.3f;
    [Tooltip("Seconds after the race starts during which AI racers cannot knock the player off (the starting grid is tight).")]
    [SerializeField] private float knockImmunityAtStart = 3f;
    [Tooltip("Seconds after landing back on the slide during which AI racers cannot knock the player off.")]
    [SerializeField] private float knockImmunityAfterLanding = 1f;
    private float raceStartTime = -1f;
    private float lastLandTime = -999f;
    private float lastSteerTime = -999f;
    private float knockSide;
    private float knockTime = float.MaxValue;
    private bool knockedOff;   // the jump being started is a knock-off, not a jump (missions)

    /// <summary>True while the player is steering or held the controls a moment ago.</summary>
    public bool IsSteering => Time.time - lastSteerTime <= steerGrace;
    /// <summary>True when an AI racer may knock the player off right now (riding, not steering, not just started or landed).</summary>
    public bool CanBeKnockedOff =>
        !inAir && !IsDone && !IsSteering && raceStartTime >= 0f &&
        Time.time - raceStartTime >= knockImmunityAtStart && Time.time - lastLandTime >= knockImmunityAfterLanding;

    /// <summary>True from the end of the race (finish line, pool, ocean...).</summary>
    public bool IsDone => collidedFinish || GameManager.Instance.gameOver;

    #endregion

    private void Start()
    {
        initialSpeed = splineFollower.followSpeed;
    }

    private void Update()
    {
        if (!GameManager.Instance.gameStarted && !GameManager.Instance.gameOver)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                StartShake();
            }
            if (Input.GetKeyUp(KeyCode.Mouse0))
            {
                ShakeComplete();
            }
        }

        if (GameManager.Instance.gameOver || !GameManager.Instance.gameStarted)
        {
            return;
        }


        if (raceStartTime < 0f)
        {
            raceStartTime = Time.time;
        }

        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Input.GetMouseButton(0))
        {
            lastSteerTime = Time.time;
        }

        if (!inAir)
        {
            HandleSplineMovement();
            CheckForJumpOff();
        }
        else
        {
            timeInAir += Time.deltaTime;
            if (timeInAir >= 0.5f)
            {
                CheckForLanding();
            }

            // Landing can end the race (pool / ocean) and switch the controller off; then there is nothing left to move.
            if (!GameManager.Instance.gameOver && _controller.enabled)
            {
                positionBeforeAirMove = transform.position;
                HandleAirMovement();
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            gravity = jumpUpForce;
        }

    }

    void StartShake()
    {
        shakeAnim.enabled = true;
    }

    public void ShakeComplete()
    {
        shakeAnim.enabled = false;
        modelTransform.DOLocalMove(Vector3.zero, 0.1f).SetEase(Ease.OutSine);
    }

    private void HandleSplineMovement()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal") * horizontalSpeed * Time.deltaTime;
        xValue += horizontalInput;
        //xValue = Mathf.Clamp(xValue, -maxSideDistance, maxSideDistance);

        // Only the sideways position is tweened; height and tilt follow the slide surface in LateUpdate.
        modelTransform.DOLocalMoveX(xValue, rotationSpeed);
    }

    private void LateUpdate()
    {
        if (!GameManager.Instance.gameStarted || GameManager.Instance.gameOver || inAir || collidedFinish)
        {
            rideBob.Reset();
            return;
        }

        // Full lean at the jump-off point (jumpThreshold is in the player's local units).
        rideBob.Remove(modelTransform);
        SlideSurface.Follow(transform, modelTransform, groundLayer, edgeLeanAngle, jumpThreshold * transform.lossyScale.x);
        rideBob.Apply(modelTransform, splineFollower.followSpeed);
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
        // Missions: count the jump (only when leaving the slide, not a fountain hit mid-air) and remember the take-off.
        if (!inAir)
        {
            if (!knockedOff)
            {
                RaceStats.Jumped(launchedByFountain);
            }

            knockedOff = false;
            takeoffPercent = splineFollower.GetPercent();
            takeoffPosition = transform.position;
        }
        launchedByFountain = false;

        splineFollower.follow = false;
        modelTransform.DOLocalRotate(new Vector3(0f, 0f, 0f), rotationSpeed);
        modelTransform.DOLocalMove(new Vector3(0f, 0f, 0f), rotationSpeed);

        Quaternion rot = transform.rotation;
        rot.x = 0;
        rot.z = 0;
        transform.rotation = rot;

        gravity = jumpUpForce;
        positionBeforeAirMove = transform.position;
        anim.SetBool("inAir", true);
        AudioManager.Play(Sfx.Jump);
        for (int i = 0; i < _effects.waterTrail.Length; i++)
        {
            //_effects.waterTrail[i].enableEmission = false;
            _effects.waterTrail[i].Stop();
        }
        inAir = true;
        _effects.AirFloatie();
    }

    /// <summary>An AI racer rammed the idle player from the side: thrown off the slide towards side (-1 left, +1 right).</summary>
    public void KnockOff(float side)
    {
        if (!CanBeKnockedOff)
        {
            return;
        }

        knockSide = Mathf.Sign(side);
        knockTime = 0f;
        knockedOff = true;
        JumpOffSpline();
    }

    private void HandleAirMovement()
    {
        gravity -= airGravity * Time.deltaTime;

        Vector3 moveDir = transform.forward * glideSpeed;
        if (knockTime < knockDuration)
        {
            // Knocked off by an AI racer: thrown sideways for a moment, then glides on as usual.
            moveDir += transform.right * knockSide * (knockDistance * transform.lossyScale.x / knockDuration);
            knockTime += Time.deltaTime;
        }

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

        RaycastHit hit;

        // Cover the whole drop since the last move as well: a fast fall (or a low frame rate) can carry the player past the
        // top of the pool's trigger box in one frame, and a ray that starts inside the box does not see it - it would hit
        // the sand under the pool instead.
        float fell = Mathf.Max(0f, positionBeforeAirMove.y - transform.position.y);
        Ray sweep = new Ray(transform.position + Vector3.up * fell, Vector3.down);

        if (Physics.Raycast(sweep, out hit, raycastDistance + fell, runLayer))
        {
            if (hit.transform.tag == "Dead")
            {
                GameManager.Instance.gameOver = true;
                Die();
            }

            else if (hit.transform.tag == "Ocean")
            {
                FallIntoOcean(hit);
            }

            else if (hit.transform.tag == "poolWater")
            {
                if (!GameManager.Instance.gameOver)
                {
                    GameManager.Instance.aiManager.PlayerFinished();   // reached the pool: lock the place now
                    GameManager.Instance.gameOver = true;
                    GameManager.Instance.endCam.SetActive(true);
                    PlayDiveIn();
                }
            }

            else
            {
                anim.SetBool("Running", true);
            }
        }
        else
        {
            anim.SetBool("Running", false);
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
        knockTime = float.MaxValue;
        lastLandTime = Time.time;
        splineFollower.motion.offset = Vector3.zero;

        splineFollower.follow = true;
        SplineSample sample = new SplineSample();
        splineFollower.Project(transform.position, ref sample);

        splineFollower.SetPercent(sample.percent);

        // Missions: flight time, and a shortcut when the landing is much further along the track than the jump travelled.
        RaceStats.AddFlight(timeInAir);
        float trackLength = GameManager.Instance.CurrentLevel != null ? GameManager.Instance.CurrentLevel.trackLength : 0f;
        Vector3 flown = transform.position - takeoffPosition;
        flown.y = 0f;
        RaceStats.Landed((float)(sample.percent - takeoffPercent) * trackLength, flown.magnitude);

        timeInAir = 0;
        _effects.landingEffect.SetActive(true);
        anim.SetBool("inAir", false);
        AudioManager.Play(Sfx.Land);
        FollowCamDirector.ShakeLanding();

        for (int i = 0; i < _effects.waterTrail.Length; i++)
        {
            //_effects.waterTrail[i].enableEmission = true;
            _effects.waterTrail[i].gameObject.SetActive(true);
        }
        _effects.LandFloatie();
        anim.SetBool("Running", false);
        GameManager.Instance.aiManager.SetAiDynamically();
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Finish"))
        {
            if (!GameManager.Instance.gameOver)
            {
                GameManager.Instance.aiManager.PlayerFinished();   // crossed the finish line: lock the place now
                GameManager.Instance.gameOver = true;
                GameManager.Instance.endCam.SetActive(true);
                PlayEndSequence();
            }
        }

        //if (other.CompareTag("poolWater"))
        //{
        //    if (!GameManager.Instance.gameOver)
        //    {
        //        GameManager.Instance.gameOver = true;
        //        GameManager.Instance.endCam.SetActive(true);
        //        PlayDiveIn();
        //    }
        //}

        if (other.CompareTag("Jumper"))
        {
            launchedByFountain = true;
            JumpOffSpline();
        }
    }

    private void PlayEndSequence()
    {
        collidedFinish = true;
        splineFollower.follow = false;
        anim.SetBool("Dive", true);
        AudioManager.Play(Sfx.Finish);

        modelTransform.DOLocalRotate(new Vector3(0f, 0f, 0f), rotationSpeed);
        modelTransform.DOLocalMove(new Vector3(0f, 0f, 0f), rotationSpeed);

        Quaternion rot = transform.rotation;
        rot.x = 0;
        rot.z = 0;
        transform.rotation = rot;

        gravity = 10;

        for (int i = 0; i < _effects.waterTrail.Length; i++)
        {
            _effects.waterTrail[i].Stop();
        }

        _effects.NoFloatie();

        Vector3 pos = transform.position;

        transform.DOLocalJump(new Vector3(pos.x + 20, pos.y - 15f, pos.z), 15, 1, 2f).OnComplete(() =>
        {
            AudioManager.Play(Sfx.Splash);   // the dive reaches the pool
            DiveOut(6f);
        });
    }
    private void DiveOut(float outValue)
    {
        Vector3 pos = transform.position;
        transform.DOMove(new Vector3(pos.x, pos.y + outValue, pos.z), 3f);
    }

    private void PlayDiveIn()
    {
        RaceStats.AddFlight(timeInAir);   // the jump straight into the pool counts as flight time
        if (!collidedFinish)
        {
            modelTransform.DOLocalRotate(new Vector3(0f, 0f, 0f), rotationSpeed);
            modelTransform.DOLocalMove(new Vector3(0f, 0f, 0f), rotationSpeed);
            splineFollower.follow = false;
            anim.SetBool("Dive", true);
            AudioManager.Play(Sfx.Splash);   // fell off the track into the pool
            Vector3 pos = transform.position;
            transform.DOMove(new Vector3(pos.x, pos.y -15f, pos.z), 1.5f).OnComplete(() => DiveOut(9.3f));
            _effects.NoFloatie();
        }
    }

    // Missed the island and hit the ocean: splash, sink through the water while the camera stays put, then the fail screen.
    private void FallIntoOcean(RaycastHit hit)
    {
        if (GameManager.Instance.gameOver)
        {
            return;
        }

        GameManager.Instance.gameOver = true;
        GameManager.Instance.playerFailed = true;
        RaceStats.AddFlight(timeInAir);
        splineFollower.follow = false;
        FollowCamDirector.StopFollowing(hit.point);

        OceanWater ocean = hit.collider.GetComponentInParent<OceanWater>();
        if (ocean != null)
        {
            ocean.Splash(hit.point);
        }
        AudioManager.Play(Sfx.Splash);
        _effects.NoFloatie();

        // Pass through the water instead of standing on its collider.
        _controller.enabled = false;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        transform.DOMove(hit.point + forward.normalized * 6f + Vector3.down * 12f, 1.2f).SetEase(Ease.InQuad);
    }

    private void Die()
    {
        RaceStats.AddFlight(timeInAir);
        GameManager.Instance.playerFailed = true;
        splineFollower.follow = false;
        anim.SetBool("Die", true);
        _effects.NoFloatie();
    }
}
