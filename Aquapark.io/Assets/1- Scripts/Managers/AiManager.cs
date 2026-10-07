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
    private readonly List<SplineFollower> finishedAgents = new List<SplineFollower>();   // AI racers in finish order
    private bool playerRankLocked;
    private float rescueTimer;
    private const int MinAiAhead = 3;               // AI racers that always start ahead of the player (player starts 4th or worse)
    private const float OffTrackDistance = 30f;   // metres from the track centre line: further than this = knocked off the slide
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
        ReturnFallenAgents();
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
    /// speedMultiplier scales the player and AI speeds together, so races stay just as close.
    /// </summary>
    public void ApplyLevel(LevelConfig level, float speedMultiplier = 1f)
    {
        minSpeed = level.aiSpeedMin * speedMultiplier;
        maxSpeed = level.aiSpeedMax * speedMultiplier;
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
        Player.splineFollower.followSpeed = level.playerSpeed * speedMultiplier;
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

    /// <summary>
    /// Starting grid: AI racers two per row (left and right lane), rows startSpacing apart; the player gets a random row of
    /// their own and rides its centre. The AI racers ahead are visible beside the player instead of hidden in a single file,
    /// and nobody starts close enough to the player to set off a bump.
    /// Every AI racer gets a slot (the old loop skipped the one whose slot the player took, so it was never put on the track
    /// and popped onto it, already moving, when the race started).
    /// </summary>
    public void InitAi()
    {
        // The player never starts in the top 3: AI racers on rows >= playerRow start ahead of them (allAgents.Length -
        // 2 * playerRow racers), so keep at least MinAiAhead of them in front. With too few AI the player starts last.
        int maxPlayerRow = Mathf.Max(0, (allAgents.Length - MinAiAhead) / 2);
        int playerRow = Random.Range(0, maxPlayerRow + 1);
        // A follower that was inactive when its spline was assigned (the AI racers are hidden in the lobby) still holds the
        // samples of its old spline, so SetPercent would put it at that percent of the OLD track - far off-screen until the
        // race starts and it snaps over. Rebuild first so every racer stands on the real track during the countdown.
        Player.splineFollower.RebuildImmediate();
        Player.splineFollower.SetPercent(startPercent + startSpacing * (playerRow + 1));

        for (int i = 0; i < allAgents.Length; i++)
        {
            int row = i / 2;
            if (row >= playerRow)
            {
                row++;   // skip the player's row
            }

            SplineFollower racer = allAgents[i];
            racer.RebuildImmediate();
            racer.SetPercent(startPercent + startSpacing * (row + 1));
            AiMover mover = racer.GetComponent<AiMover>();
            if (mover != null)
            {
                mover.SetStartLane(i % 2 == 0 ? -1f : 1f);
            }

            racer.GetComponent<Animator>().SetBool("Start", true);
            racer.GetComponent<AiEffects>().StartRaceEffects();
        }
    }

    public void StartGame()
    {
        Player.ShakeComplete();
        Player.splineFollower.follow = true;
        initialSpeed = Player.splineFollower.followSpeed;
        Player.splineFollower.followSpeed += 20;
        playerBoostTime = true;
        RaceStats.BeginRace();
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
            if (finishedAgents.Contains(allAgents[i]))
            {
                continue;   // already on the end spline: its percent is not a race position any more
            }

            if (!allAgents[i].follow)
            {
                continue;   // in the air: teleporting it would leave it flying from the new spot (ReturnFallenAgents handles falls)
            }

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

            // Pull it up to just behind the camera, never further back than where it already is. On spirals the track
            // behind the player can be in view (a lap above or beside), so use the first spot the camera cannot see.
            float target = hiddenGap + moved * respawnSpacing;
            moved++;
            target = HiddenSpotBehind(playerPercent, target, gap, length);
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
            if (boostLeft[i] <= 0f || finishedAgents.Contains(allAgents[i]))
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

    /// <summary>
    /// First distance (metres behind the player along the track, from 'from' in 10 m steps) whose spot is outside the camera's
    /// view. Returns 'limit' when every spot up to there can be seen (the racer is then left where it is).
    /// </summary>
    private float HiddenSpotBehind(double playerPercent, float from, float limit, float length)
    {
        Camera cam = Camera.main;
        SplineComputer spline = Player.splineFollower.spline;
        if (cam == null || spline == null)
        {
            return from;
        }

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        for (float g = from; g < limit; g += 10f)
        {
            double percent = System.Math.Max(0.0, playerPercent - g / length);
            Vector3 spot = spline.EvaluatePosition(percent);
            if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(spot, Vector3.one * 8f)))
            {
                return g;
            }
        }

        return limit;
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

    /// <summary>An AI racer crossed the finish line (it then switches to the end spline, so its percent no longer counts).</summary>
    public void AgentFinished(SplineFollower agent)
    {
        if (!finishedAgents.Contains(agent))
        {
            finishedAgents.Add(agent);
        }
    }

    /// <summary>
    /// The player reached the finish (finish line or straight into the pool). Their place is fixed now: one behind every AI
    /// racer that finished earlier, whatever happens afterwards.
    /// </summary>
    public void PlayerFinished()
    {
        if (playerRankLocked)
        {
            return;
        }

        playerRankLocked = true;
        PlayerRank = finishedAgents.Count + 1;
        playerPosition.text = GetOrdinal(PlayerRank);
    }

    private void Positioning()
    {
        if (inLobby || playerRankLocked)
        {
            return;
        }

        // Once the race is over (e.g. the player fell) the place shown stays as it was.
        if (GameManager.Instance != null && GameManager.Instance.gameOver)
        {
            playerRankLocked = true;
            return;
        }

        distances.Clear();
        double playerProgress = TrackProgress(Player.splineFollower, Player.transform);
        int rank = 1;
        for (int i = 0; i < allAgents.Length; i++)
        {
            double progress = AgentProgress(allAgents[i]);
            distances.Add(System.Math.Min(progress, 1.0));
            if (progress > playerProgress)
            {
                rank++;
            }
        }
        distances.Add(playerProgress);

        PlayerRank = rank;
        playerPosition.text = GetOrdinal(rank);
    }

    // Finished AI racers rank ahead of everyone still racing, in the order they finished (values above 1).
    private double AgentProgress(SplineFollower agent)
    {
        int order = finishedAgents.IndexOf(agent);
        if (order >= 0)
        {
            return 2.0 - order * 0.001;
        }

        return TrackProgress(agent, agent.transform);
    }

    // Percent along the race spline. A racer in the air is not moved by its follower, so its percent would freeze at the
    // take-off point while everyone else keeps going; use the closest point of the track under it instead (never less).
    // Only while it is near the track: a racer knocked off onto the ground can be closest to any far part of the track.
    private static double TrackProgress(SplineFollower follower, Transform body)
    {
        double percent = follower.GetPercent();
        if (!follower.follow && follower.spline != null)
        {
            SplineSample sample = new SplineSample();
            follower.Project(body.position, ref sample);
            if (Vector3.Distance(sample.position, body.position) <= OffTrackDistance)
            {
                percent = System.Math.Max(percent, sample.percent);
            }
        }

        return percent;
    }

    /// <summary>
    /// AI racers knocked off the slide never land on it again (they would slide along the sand or ocean for the rest of the
    /// race). Once one is clearly off the track, put it back on the slide at a spot behind the player the camera cannot see,
    /// with the usual catch-up boost.
    /// </summary>
    private void ReturnFallenAgents()
    {
        rescueTimer -= Time.deltaTime;
        if (rescueTimer > 0f || inLobby || GameManager.Instance == null || !GameManager.Instance.gameStarted || GameManager.Instance.gameOver)
        {
            return;
        }

        rescueTimer = 0.5f;
        double playerPercent = Player.splineFollower.GetPercent();
        float length = RaceLength;
        float hiddenGap = HiddenGapMeters();
        for (int i = 0; i < allAgents.Length; i++)
        {
            SplineFollower agent = allAgents[i];
            if (agent.follow || finishedAgents.Contains(agent) || agent.spline == null)
            {
                continue;
            }

            SplineSample sample = new SplineSample();
            agent.Project(agent.transform.position, ref sample);
            if (Vector3.Distance(sample.position, agent.transform.position) <= OffTrackDistance)
            {
                continue;   // a normal jump or bump: it lands by itself
            }

            float gap = HiddenSpotBehind(playerPercent, hiddenGap, hiddenGap + 300f, length);
            AiMover mover = agent.GetComponent<AiMover>();
            if (mover == null)
            {
                continue;
            }

            if (mover.PushedByPlayerWithin(8f))
            {
                RaceStats.KnockedOff();   // the player threw it off the slide
            }

            mover.ReturnToTrack(System.Math.Max(0.0, playerPercent - gap / length));
            baseSpeeds[i] = Random.Range(minSpeed, maxSpeed);
            agent.followSpeed = baseSpeeds[i] * (1f + Random.Range(catchUpBoostMin, catchUpBoostMax));
            boostLeft[i] = catchUpMaxSeconds;
        }
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
