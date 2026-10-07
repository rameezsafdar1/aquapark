using UnityEngine;

/// <summary>
/// What the player did in the current race, for the missions. Gameplay code reports events here; at the end of the race the
/// totals go to <see cref="MissionManager"/>.
/// </summary>
public static class RaceStats
{
    /// <summary>Extra metres a landing must be ahead of the jump's own forward travel to count as a shortcut.</summary>
    public const float ShortcutMinMeters = 40f;

    public static int Jumps { get; private set; }
    public static int FountainJumps { get; private set; }
    public static float FlightSeconds { get; private set; }
    public static int Knockouts { get; private set; }
    public static int Shortcuts { get; private set; }
    public static bool Racing { get; private set; }

    /// <summary>Called when the countdown ends and the race starts.</summary>
    public static void BeginRace()
    {
        Jumps = 0;
        FountainJumps = 0;
        FlightSeconds = 0f;
        Knockouts = 0;
        Shortcuts = 0;
        Racing = true;
    }

    public static void Jumped(bool fromFountain)
    {
        if (!Racing) return;
        Jumps++;
        if (fromFountain) FountainJumps++;
    }

    public static void AddFlight(float seconds)
    {
        if (!Racing || seconds <= 0f) return;
        FlightSeconds += seconds;
    }

    /// <summary>The player landed back on the slide. gainedMeters: how far along the track the landing is from the take-off;
    /// flownMeters: how far the player actually travelled. A big difference means they landed on a later part (shortcut).</summary>
    public static void Landed(float gainedMeters, float flownMeters)
    {
        if (!Racing) return;
        if (gainedMeters - flownMeters >= ShortcutMinMeters) Shortcuts++;
    }

    /// <summary>An AI racer the player pushed fell off the slide.</summary>
    public static void KnockedOff()
    {
        if (!Racing) return;
        Knockouts++;
    }

    /// <summary>The race is over (results are about to show). rank: 1 = first. failed: fell instead of reaching the pool.</summary>
    public static void EndRace(int rank, bool failed)
    {
        if (!Racing) return;
        Racing = false;
        MissionManager.ReportRace(rank, failed);
    }
}
