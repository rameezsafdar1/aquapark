using UnityEngine;

/// <summary>What a mission counts. Values come from <see cref="RaceStats"/> at the end of every race.</summary>
public enum MissionType
{
    WinRaces,        // finish 1st
    FinishTop3,      // finish 1st, 2nd or 3rd
    FinishRaces,     // reach the pool without falling
    PlayRaces,       // any race, even a fall
    Jumps,           // jumps off the slide (edge jumps and fountains)
    FountainJumps,   // jumps launched by a fountain
    FlightSeconds,   // seconds spent in the air
    KnockOffRacers,  // AI racers the player pushed off the slide
    Shortcuts        // landings on a part of the track well ahead of where the jump started (e.g. the next spiral lap)
}

/// <summary>What the mission pays out when it is claimed.</summary>
public enum MissionRewardKind
{
    Coins,
    Gems,
    FreeSpins,
    Skin
}

[System.Serializable]
public class MissionReward
{
    public MissionRewardKind kind = MissionRewardKind.Coins;
    [Tooltip("Coins / gems / spins. Ignored for a skin.")]
    [Min(1)] public int amount = 50;
    [Tooltip("Skin reward only: id of the skin to give. Empty = the first skin the player does not own yet.")]
    public string skinId = "";
    [Tooltip("Skin reward only: coins given instead when the player already owns every skin.")]
    [Min(0)] public int fallbackCoins = 500;
}

/// <summary>
/// One task for the mission card above the Play button ("Win 3 races", "Make 5 jumps in one race"...).
/// Create with Assets > Create > Aquapark > Mission, then add it to the <see cref="MissionList"/>.
/// </summary>
[CreateAssetMenu(menuName = "Aquapark/Mission", fileName = "Mission")]
public class MissionDefinition : ScriptableObject
{
    [Tooltip("Shown on the card. {0} is replaced with the target, e.g. \"Win {0} races\".")]
    public string title = "Win {0} races";
    public MissionType type = MissionType.WinRaces;
    [Tooltip("How many (or how many seconds for flight time).")]
    [Min(1)] public int target = 3;
    [Tooltip("On: it has to be done within a single race (progress shows the best race so far). Off: it adds up over races.")]
    public bool withinOneRace;
    public MissionReward reward = new MissionReward();

    public string TitleFor(int scaledTarget) => string.Format(title, scaledTarget);
}
