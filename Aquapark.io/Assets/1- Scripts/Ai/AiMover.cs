using DG.Tweening;
using Dreamteck.Splines;
using UnityEngine;

public class AiMover : MonoBehaviour
{
    public SplineFollower follower;
    [SerializeField] private AiEffects Effects;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private float airSpeed = 7f;
    [SerializeField] private Animator anim;
    [SerializeField] private float minimumDelay, maximumDelay, sideLength, moveSpeed, jumpForce, raycastDistance, sideSpeed = 2;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform characterModel;
    [Tooltip("Degrees the racer leans at its widest sideways position (sideLength), so there is tilt on the flat floor too. On the curved wall the wall's own slope is used when it is steeper.")]
    [SerializeField] private float edgeLeanAngle = 12f;
    [Tooltip("Height of the water surface above the track's centre line, in metres.")]
    [SerializeField] private float waterSurfaceHeight = 0.42f;
    [Tooltip("How deep the bottom of the ring sits in the water, in metres.")]
    [SerializeField] private float ringSink = 0.12f;
    [Tooltip("Small speed jitter of the character while riding the slide.")]
    [SerializeField] private RideBob rideBob = new RideBob();
    private float floatHeight = float.NaN;   // model height that puts this racer's ring at the waterline
    private float ringHalfWidth;             // metres; a tilted ring dips its edge by this × sin(tilt)
    private float delay, timePassed, moveDis, gravity, timeInAir;
    private bool inAir;
    private float horizontalValue, horizontalTime;
    private Vector3 moveDir;
    private bool collidedFinish;
    private bool hasStartLane;   // placed in a grid lane by AiManager, so Start keeps that sideways position
    [SerializeField] private GameObject[] skins;


    // The skin is picked in Awake, not Start: its AvatarPass swaps this racer's Animator avatar, which resets all
    // animator parameters. Doing it here means it happens before AiManager.InitAi sets "Start", not after.
    private void Awake()
    {
        int randomSkin = Random.Range(0, skins.Length);

        skins[randomSkin].SetActive(true);
    }

    private void Start()
    {
        if (!hasStartLane)
        {
            moveDis = Random.Range(-sideLength, sideLength);
            characterModel.DOLocalMoveX(moveDis, 0f);
        }
        delay = Random.Range(minimumDelay, maximumDelay);
    }

    /// <summary>Puts the racer in the left (-1) or right (+1) lane of the starting grid, clear of the centre.</summary>
    public void SetStartLane(float side)
    {
        hasStartLane = true;
        moveDis = Mathf.Sign(side) * sideLength * 0.85f;
        characterModel.DOKill();
        Vector3 p = characterModel.localPosition;
        characterModel.localPosition = new Vector3(moveDis, p.y, p.z);
        delay = Random.Range(minimumDelay, maximumDelay);
        timePassed = 0f;
    }

    // The racer floats with its ring at the waterline and tilts with the slide; the sideways tweens only move it along X.
    private void LateUpdate()
    {
        if (collidedFinish || inAir)
        {
            rideBob.Reset();
        }

        if (collidedFinish)
        {
            // The finish dive spreads racers along their forward axis. The end of the dive path tilts up, which would lift
            // that spread out of the pool, so keep the character level with the path.
            Vector3 p = characterModel.position;
            p.y = transform.position.y;
            characterModel.position = p;
            return;
        }

        if (inAir)
        {
            return;
        }

        if (float.IsNaN(floatHeight))
        {
            // Rings differ in size, so measure this racer's once and place its bottom just under the water surface.
            floatHeight = waterSurfaceHeight - ringSink - MeasureRing(out ringHalfWidth);
        }

        // Full lean at the widest sideways position (sideLength is in the racer's local units).
        rideBob.Remove(characterModel);
        SlideSurface.Float(transform, characterModel, groundLayer, floatHeight, ringHalfWidth, edgeLeanAngle, sideLength * transform.lossyScale.x);
        rideBob.Apply(characterModel, follower.follow ? follower.followSpeed : 0f);   // calm while waiting at the start
    }

    // Metres from the character model's pivot up to the bottom of its ring, and the ring's half-width, from the ring mesh
    // bounds (the ring meshes are not readable, and bounds are cheap). Measured in the model's own space, so tilt does not matter.
    private float MeasureRing(out float halfWidth)
    {
        float lowest = float.MaxValue;
        halfWidth = 0f;
        foreach (MeshFilter mf in Effects.CurrentFloatie.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null)
            {
                continue;
            }

            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = characterModel.InverseTransformPoint(mf.transform.TransformPoint(corner));
                lowest = Mathf.Min(lowest, p.y);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(p.x));
            }
        }

        halfWidth *= characterModel.lossyScale.x;
        return lowest == float.MaxValue ? 0f : lowest * characterModel.lossyScale.y;
    }

    private void Update()
    {
        if (!GameManager.Instance.gameStarted || GameManager.Instance.gameOver || frozen)
        {
            return;
        }

        if (inAir)
        {
            HandleAirMovement();
            timeInAir += Time.deltaTime;
            if (timeInAir >= 1f)
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
        if (horizontalTime <= 0.3f)
        {
            moveDir += transform.right * horizontalValue * sideSpeed;
            horizontalTime += Time.deltaTime;
        }

        else
        {
            moveDir = transform.forward * airSpeed;
        }


        moveDir.y = gravity;

        _controller.Move(moveDir * Time.deltaTime);

        //float mouseX = Input.GetAxis("Mouse X");
        //transform.Rotate(Vector3.up, mouseX * 100f * Time.deltaTime);
    }


    private void GetSidewaysPositionOnSlide()
    {
        moveDis = Random.Range(-sideLength, sideLength);
        characterModel.DOLocalMoveX(moveDis, moveSpeed);
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
        if (collidedFinish)
        {
            return;
        }

        collidedFinish = true;
        SplineFollower splineFollower = GetComponent<SplineFollower>();
        GameManager.Instance.aiManager.AgentFinished(splineFollower);
        splineFollower.followSpeed = 10;
        splineFollower.spline = GameManager.Instance.endSpline;
        // Spread the racers across the pool so they do not stack. The dive path ends 8 m from the far wall with
        // 20+ m of water behind and to the sides, so spread sideways (metres) and mostly backwards (local units, x5 scale).
        // Blended in over the dive so nobody visibly jumps sideways or back at the finish line.
        float side = Random.Range(-8f, 8f);
        DOTween.To(() => splineFollower.motion.offset.x, x => splineFollower.motion.offset = new Vector2(x, 0f), side, 2f);
        characterModel.DOKill();   // stop any sideways move still running from the slide
        characterModel.DOLocalMove(new Vector3(0, 0, Random.Range(-3.5f, 0.8f)), 2f);
        characterModel.DOLocalRotate(Vector3.zero, 0.1f);
        splineFollower.SetPercent(0);
        anim.SetBool("Dive", true);
        Effects.NoFloatie();
    }

    private float lastPlayerPushTime = -999f;

    public void MarkPushedByPlayer() => lastPlayerPushTime = Time.time;

    /// <summary>True if the player shoved this racer within the last few seconds.</summary>
    public bool PushedByPlayerWithin(float seconds) => Time.time - lastPlayerPushTime <= seconds;

    /// <summary>True while the racer is off the slide (pushed off or jumping).</summary>
    public bool IsInAir => inAir;

    /// <summary>True once the racer crossed the finish line.</summary>
    public bool Finished => collidedFinish;

    private bool frozen;

    /// <summary>True while the freeze buff holds this racer (it cannot move, steer, jump or knock anyone off).</summary>
    public bool IsFrozen => frozen;

    /// <summary>Freeze buff: stops the racer where it is (RaceBuffs holds its slide speed at 0) and pauses its animation.</summary>
    public void SetFrozen(bool value)
    {
        frozen = value;
        anim.speed = value ? 0f : 1f;
        if (value)
        {
            characterModel.DOPause();
        }
        else
        {
            characterModel.DOPlay();
        }
    }

    /// <summary>Puts a racer that fell off the slide back on it at the given percent, riding normally again.</summary>
    public void ReturnToTrack(double percent)
    {
        inAir = false;
        gravity = 0;
        timeInAir = 0;
        horizontalTime = 1f;
        characterModel.DOKill();
        characterModel.localRotation = Quaternion.identity;
        follower.SetPercent(percent);
        follower.follow = true;
        anim.SetBool("inAir", false);
        Effects.LandFloatie();
    }

    public void Jump(float value)
    {
        if (frozen)
        {
            return;   // frozen racers cannot be thrown off
        }

        horizontalValue = value;
        horizontalTime = 0;
        anim.SetBool("inAir", true);
        Quaternion rot = transform.rotation;
        rot.x = 0;
        rot.z = 0;

        transform.rotation = rot;

        characterModel.DOLocalMove(Vector3.zero, 0.1f);
        characterModel.DOLocalRotate(Vector3.zero, 0.1f);
        follower.follow = false;
        inAir = true;
        gravity = jumpForce;
        Effects.AirFloatie();
    }
}
