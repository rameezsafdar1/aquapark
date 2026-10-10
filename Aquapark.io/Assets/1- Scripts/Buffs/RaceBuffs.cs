using System.Collections.Generic;
using DG.Tweening;
using Dreamteck.Splines;
using UnityEngine;

public enum BuffType
{
    None,
    Speed,           // the player rides faster for a few seconds
    Giant,           // the player grows for a few seconds and cannot be knocked off
    Freeze,          // every AI racer stops for a few seconds
    DoubleRewards    // coins and gems from this race are doubled
}

/// <summary>
/// The race buffs. One buff per race: once one is used, none of the others can be. Lives only for one race (the scene is
/// reloaded for every race), and is created the first time something asks for it.
/// </summary>
[DefaultExecutionOrder(100)]   // after the scripts that set follow speeds in Update, so the speed holds see their changes
public class RaceBuffs : MonoBehaviour
{
    [Tooltip("Seconds the giant and freeze buffs last.")]
    public float duration = 5f;
    [Tooltip("Seconds the speed buff lasts.")]
    public float speedDuration = 10f;
    [Tooltip("Speed buff: the player's slide speed is multiplied by this.")]
    public float speedMultiplier = 1.5f;
    [Tooltip("Giant buff: the player's character (and floatie) is scaled by this.")]
    public float giantScale = 2f;
    [Tooltip("Seconds the character takes to grow and to shrink back.")]
    public float giantTweenSeconds = 0.35f;

    private static RaceBuffs instance;

    /// <summary>The buffs of the race being played: the scene's Race Buffs object (created with default settings if missing).</summary>
    public static RaceBuffs Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RaceBuffs>();
            }

            if (instance == null)
            {
                instance = new GameObject("Race Buffs").AddComponent<RaceBuffs>();
            }

            return instance;
        }
    }

    /// <summary>True when the 2x rewards buff was used this race.</summary>
    public static bool DoubleRewards => instance != null && instance.Used == BuffType.DoubleRewards;

    /// <summary>True while the given buff is running.</summary>
    public static bool IsActive(BuffType type) => instance != null && instance.Active == type;

    /// <summary>The buff picked this race, None until one is pressed.</summary>
    public BuffType Used { get; private set; }
    /// <summary>The buff running right now. 2x rewards stays active for the whole race.</summary>
    public BuffType Active { get; private set; }
    /// <summary>Seconds left on the running timed buff.</summary>
    public float TimeLeft { get; private set; }
    /// <summary>Full length in seconds of the running timed buff.</summary>
    public float TotalTime { get; private set; }

    /// <summary>True while a buff may be picked: none used yet and the race is on.</summary>
    public bool CanUse
    {
        get
        {
            GameManager gm = GameManager.Instance;
            return Used == BuffType.None && gm != null && gm.gameStarted && !gm.gameOver;
        }
    }

    private readonly List<SpeedHold> holds = new List<SpeedHold>();
    private readonly List<AiMover> frozen = new List<AiMover>();
    private Locomotion player;
    private PlayerEffects playerEffects;
    private Vector3 playerModelScale;

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>Starts a buff. False (and nothing happens) when a buff was already used this race or the race is not on.</summary>
    public bool TryUse(BuffType type)
    {
        if (type == BuffType.None || !CanUse)
        {
            return false;
        }

        player = GameManager.Instance.aiManager.PlayerTransform.GetComponent<Locomotion>();
        playerEffects = player.GetComponent<PlayerEffects>();
        Used = type;
        Active = type;
        TotalTime = type == BuffType.Speed ? speedDuration : duration;
        TimeLeft = TotalTime;

        switch (type)
        {
            case BuffType.Speed:
                holds.Add(new SpeedHold(player.splineFollower, s => s * speedMultiplier));
                break;

            case BuffType.Giant:
                playerModelScale = player.modelTransform.localScale;
                player.modelTransform.DOScale(playerModelScale * giantScale, giantTweenSeconds).SetEase(Ease.OutBack);
                break;

            case BuffType.Freeze:
                foreach (AiMover ai in FindObjectsByType<AiMover>(FindObjectsSortMode.None))
                {
                    if (ai.Finished)
                    {
                        continue;   // already on the end spline
                    }

                    ai.SetFrozen(true);
                    frozen.Add(ai);
                    holds.Add(new SpeedHold(ai.follower, s => 0f));
                }
                break;
        }

        AudioManager.Play(Sfx.Reward);
        Haptics.Pulse();
        return true;
    }

    private void LateUpdate()
    {
        if (Active == BuffType.None || Active == BuffType.DoubleRewards)
        {
            return;
        }

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            End();
            return;
        }

        foreach (SpeedHold hold in holds)
        {
            hold.Apply();
        }

        if (Active == BuffType.Speed && playerEffects != null && !player.IsInAir && !player.IsDone)
        {
            playerEffects.windLines.SetActive(true);
        }
    }

    private void End()
    {
        foreach (SpeedHold hold in holds)
        {
            hold.Release();
        }
        holds.Clear();

        switch (Active)
        {
            case BuffType.Speed:
                if (playerEffects != null)
                {
                    playerEffects.windLines.SetActive(false);
                }
                break;

            case BuffType.Giant:
                if (player != null)
                {
                    player.modelTransform.DOScale(playerModelScale, giantTweenSeconds).SetEase(Ease.InOutSine);
                }
                break;

            case BuffType.Freeze:
                foreach (AiMover ai in frozen)
                {
                    if (ai != null)
                    {
                        ai.SetFrozen(false);
                    }
                }
                frozen.Clear();
                break;
        }

        TimeLeft = 0f;
        Active = BuffType.None;
    }

    /// <summary>
    /// Overrides a spline follower's speed while a buff runs. Other scripts keep setting followSpeed (start boost, bumps,
    /// AI catch-up); when they do, their value becomes the new base speed, so it is the one restored when the buff ends.
    /// </summary>
    private class SpeedHold
    {
        private readonly SplineFollower follower;
        private readonly System.Func<float, float> change;
        private float baseSpeed;
        private float written = float.NaN;

        public SpeedHold(SplineFollower follower, System.Func<float, float> change)
        {
            this.follower = follower;
            this.change = change;
            baseSpeed = follower.followSpeed;
            Apply();
        }

        public void Apply()
        {
            if (follower == null)
            {
                return;
            }

            if (follower.followSpeed != written)
            {
                baseSpeed = follower.followSpeed;   // someone else set it since the last frame
            }

            written = change(baseSpeed);
            follower.followSpeed = written;
        }

        public void Release()
        {
            if (follower != null && follower.followSpeed == written)
            {
                follower.followSpeed = baseSpeed;
            }
        }
    }
}
