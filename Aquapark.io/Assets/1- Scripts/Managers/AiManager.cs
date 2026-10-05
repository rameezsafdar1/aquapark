using Dreamteck.Splines;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;

public class AiManager : MonoBehaviour
{
    [SerializeField] private Locomotion Player;
    [SerializeField] private PlayerEffects effects;
    [SerializeField] private SplineFollower[] allAgents;
    [SerializeField] private float minSpeed, maxSpeed, startSpacing, startBoostDuration = 5;
    [SerializeField] private TextMeshPro playerPosition;
    [Tooltip("The old floating rank text above the player. The HUD shows the rank now, so it is off by default.")]
    [SerializeField] private bool showWorldRankLabel;
    private List<double> distances = new List<double>();
    private float startPercent, boostDuration, initialSpeed;
    private bool playerBoostTime;
    private bool inLobby;
    private SplineComputer raceSpline;
    private float raceLength;

    [Header("Catch-up after the player lands")]
    [Tooltip("AI racers further behind than the hidden gap are moved back to that gap, behind the camera, so nobody pops in beside the player.")]
    [SerializeField] private float hiddenGapMargin = 25f;
    [Tooltip("Metres. The hidden gap is never smaller than this, even if the camera is very close.")]
    [SerializeField] private float minHiddenGap = 35f;
    [Tooltip("Extra metres between AI racers that are moved at the same time, so they do not stack up.")]
    [SerializeField] private float respawnSpacing = 6f;
    [Tooltip("Temporary speed boost for the AI racers that were moved (0.10 = +10%).")]
    [SerializeField, Range(0f, 0.5f)] private float catchUpBoostMin = 0.10f;
    [SerializeField, Range(0f, 0.5f)] private float catchUpBoostMax = 0.15f;
    [Tooltip("The boost ends when the AI racer is this close (metres) behind the player.")]
    [SerializeField] private float catchUpEndGap = 12f;
    [Tooltip("The boost also ends after this many seconds, whatever happens.")]
    [SerializeField] private float catchUpMaxSeconds = 15f;

    private float[] baseSpeeds;      // each AI racer's normal speed, restored when its boost ends
    private float[] boostLeft;       // seconds of boost left (0 = not boosted)

    /// <summary>The player's transform (the camera follows it).</summary>
    public Transform PlayerTransform => Player.transform;

    /// <summary>The player's current race position (1 = first), refreshed every frame.</summary>
    public int PlayerRank { get; private set; } = 1;
    /// <summary>Player plus all AI racers.</summary>
    public int RacerCount => allAgents.Length + 1;
    /// <summary>Spline percent (0-1) of every racer, AI first and the player always last. Refreshed every frame.</summary>
    public IReadOnlyList<double> RacerPercents => distances;

    private void Awake()
    {
        baseSpeeds = new float[allAgents.Length];
        boostLeft = new float[allAgents.Length];
        for (int i = 0; i < allAgents.Length; i++)
        {
            baseSpeeds[i] = Random.Range(minSpeed, maxSpeed);
            allAgents[i].followSpeed = baseSpeeds[i];
        }
    }

    private void Start()
    {
        if (playerPosition != null)
        {
            playerPosition.gameObject.SetActive(showWorldRankLabel);
        }

        // In the menu lobby the racers are placed on the real track only when the race starts (EnterRace).
        if (!inLobby)
        {
            InitAi();
        }
    }

    private void Update()
    {
        Positioning();
        UpdateCatchUp();
        if (playerBoostTime)
        {
            boostDuration += Time.deltaTime;
            if (boostDuration >= startBoostDuration)
            {
                Player.splineFollower.followSpeed = initialSpeed;
                playerBoostTime = false;
                effects.windLines.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Puts the player and every AI racer on a level's spline and applies that level's race settings.
    /// Must run before Start() places the racers, so GameManager (which calls this) runs first.
    /// </summary>
    public void ApplyLevel(LevelConfig level)
    {
        minSpeed = level.aiSpeedMin;
        maxSpeed = level.aiSpeedMax;
        startSpacing = level.aiStartSpacingPercent;
        startBoostDuration = level.startBoostDuration;

        for (int i = 0; i < allAgents.Length; i++)
        {
            allAgents[i].spline = level.mainSpline;
            baseSpeeds[i] = Random.Range(minSpeed, maxSpeed);
            boostLeft[i] = 0f;
            allAgents[i].followSpeed = baseSpeeds[i];
        }

        raceSpline = level.mainSpline;
        raceLength = level.trackLength > 1f ? level.trackLength : level.mainSpline.CalculateLength();
        Player.splineFollower.spline = level.mainSpline;
        Player.splineFollower.followSpeed = level.playerSpeed;
    }

    /// <summary>Parks the player on the menu lobby tube and hides the AI until the race starts.</summary>
    public void EnterLobby(MenuLobby lobby)
    {
        inLobby = true;
        for (int i = 0; i < allAgents.Length; i++)
        {
            allAgents[i].follow = false;
            allAgents[i].gameObject.SetActive(false);
        }

        Player.splineFollower.follow = false;
        Player.transform.position = lobby.standPoint.position;
        Player.transform.rotation = lobby.standPoint.rotation;
    }

    /// <summary>Moves the player and every AI racer from the lobby onto the level's track, in their starting grid.</summary>
    public void EnterRace()
    {
        if (!inLobby)
        {
            return;
        }

        inLobby = false;
        Player.splineFollower.spline = raceSpline;
        effects.LandFloatie();
        effects.LoadParticles();
        for (int i = 0; i < allAgents.Length; i++)
        {
            allAgents[i].spline = raceSpline;
            allAgents[i].gameObject.SetActive(true);
        }

        startPercent = 0f;
        InitAi();
    }

    public void InitAi()
    {
        int playerPos = Random.Range(0, allAgents.Length);

        for (int i = 0; i < allAgents.Length; i++)
        {
            startPercent += startSpacing;
            if (i == playerPos)
            {
                //allAgents[i].SetPercent(Player.GetPercent() + 0.003f);
                Player.splineFollower.SetPercent(startPercent);
            }
            else
            {
                allAgents[i].SetPercent(startPercent);
            }

            Debug.Log("Init called");
            Animator anim = allAgents[i].GetComponent<Animator>();
            anim.SetBool("Start", true);
            allAgents[i].GetComponent<AiEffects>().StartRaceEffects();
        }
    }

    public void StartGame()
    {
        Player.ShakeComplete();
        Player.splineFollower.follow = true;
        initialSpeed = Player.splineFollower.followSpeed;
        Player.splineFollower.followSpeed += 20;
        playerBoostTime = true;
        effects.windLines.SetActive(true);

        for (int i = 0; i < allAgents.Length; i++)
        {
            allAgents[i].follow = true;
        }
    }

    /// <summary>
    /// Called when the player lands back on the slide. AI racers far behind are moved up to a spot behind the camera (so nobody
    /// appears beside the player) and get a temporary 10-15% speed boost to catch up. AI racers already close are left alone.
    /// </summary>
    public void SetAiDynamically()
    {
        double playerPercent = Player.splineFollower.GetPercent();
        float length = RaceLength;
        float hiddenGap = HiddenGapMeters();
        int moved = 0;

        for (int i = 0; i < allAgents.Length; i++)
        {
            baseSpeeds[i] = Random.Range(minSpeed, maxSpeed);
            allAgents[i].followSpeed = baseSpeeds[i];
            boostLeft[i] = 0f;

            double agentPercent = allAgents[i].GetPercent();
            if (agentPercent >= playerPercent)
            {
                continue;
            }

            float gap = (float)(playerPercent - agentPercent) * length;
            if (gap <= hiddenGap)
            {
                continue;   // already close behind the player, in view: moving it would be visible
            }

            // Pull it up to just behind the camera, never further back than where it already is.
            float target = hiddenGap + moved * respawnSpacing;
            moved++;
            if (target < gap)
            {
                allAgents[i].SetPercent(playerPercent - target / length);
            }

            allAgents[i].followSpeed = baseSpeeds[i] * (1f + Random.Range(catchUpBoostMin, catchUpBoostMax));
            boostLeft[i] = catchUpMaxSeconds;
        }
    }

    /// <summary>Ends the catch-up boost of AI racers that are close enough, or whose time ran out.</summary>
    private void UpdateCatchUp()
    {
        if (boostLeft == null || inLobby)
        {
            return;
        }

        double playerPercent = Player.splineFollower.GetPercent();
        float length = RaceLength;
        for (int i = 0; i < boostLeft.Length; i++)
        {
            if (boostLeft[i] <= 0f)
            {
                continue;
            }

            boostLeft[i] -= Time.deltaTime;
            float gap = (float)(playerPercent - allAgents[i].GetPercent()) * length;
            if (boostLeft[i] <= 0f || gap <= catchUpEndGap)
            {
                boostLeft[i] = 0f;
                allAgents[i].followSpeed = baseSpeeds[i];
            }
        }
    }

    /// <summary>Length of the track being raced, in metres.</summary>
    private float RaceLength
    {
        get
        {
            if (raceLength <= 1f)
            {
                SplineComputer spline = Player.splineFollower.spline;
                raceLength = spline != null ? spline.CalculateLength() : 1000f;
            }

            return Mathf.Max(1f, raceLength);
        }
    }

    /// <summary>How far behind the player (metres along the track) an AI racer has to be to be out of the camera's view.</summary>
    private float HiddenGapMeters()
    {
        float cameraDistance = 0f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            cameraDistance = Vector3.Distance(cam.transform.position, Player.transform.position);
        }

        return Mathf.Max(minHiddenGap, cameraDistance + hiddenGapMargin);
    }

    private void Positioning()
    {
        distances.Clear();

        for (int i = 0; i < allAgents.Length; i++)
        {
            distances.Add(allAgents[i].GetPercent());
        }

        double playerPercent = Player.splineFollower.GetPercent();
        distances.Add(playerPercent);

        var sorted = distances.OrderByDescending(d => d).ToList();

        int rank = sorted.IndexOf(playerPercent) + 1;
        PlayerRank = rank;
        playerPosition.text = GetOrdinal(rank);
    }

    string GetOrdinal(int number)
    {
        if (number % 100 >= 11 && number % 100 <= 13) return number + "th";
        switch (number % 10)
        {
            case 1: return number + "st";
            case 2: return number + "nd";
            case 3: return number + "rd";
            default: return number + "th";
        }
    }
}
