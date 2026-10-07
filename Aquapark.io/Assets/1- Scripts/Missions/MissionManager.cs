using System;
using UnityEngine;

/// <summary>What <see cref="MissionManager.ClaimReward"/> actually gave, for the reward (packet) animation.</summary>
public struct MissionGrant
{
    public MissionRewardKind kind;
    public int amount;          // coins / gems / spins (or the fallback coins when a skin could not be given)
    public SkinData skin;       // the skin given, or null
    public bool skinFallback;   // true when a skin was due but every skin is owned, so coins were given instead
}

/// <summary>
/// The mission card above the Play button. One mission is active at a time (from Resources/Missions/MissionList); races add
/// progress (<see cref="RaceStats"/>), a finished mission waits with its reward until the menu claims it, then the next one
/// starts. Progress is saved in <see cref="SaveData"/>.
///
/// For the card UI: <see cref="Title"/>, <see cref="ProgressText"/>, <see cref="Progress01"/>, <see cref="Reward"/>,
/// <see cref="RewardPending"/>, <see cref="ClaimReward"/> and the <see cref="Changed"/> event.
/// </summary>
public static class MissionManager
{
    public const string ListResourcePath = "Missions/MissionList";

    private static MissionList list;

    /// <summary>Raised when progress changes, a mission completes, or a reward is claimed.</summary>
    public static event Action Changed;

    /// <summary>Raised once when a race completes the active mission (its reward is now pending).</summary>
    public static event Action<MissionDefinition> Completed;

    // No domain reload when entering Play mode: drop listeners left over from the last session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        Completed = null;
    }

    public static MissionList List
    {
        get
        {
            if (list == null)
            {
                list = Resources.Load<MissionList>(ListResourcePath);
            }

            return list;
        }
    }

    public static bool HasMissions => List != null && List.missions.Count > 0;

    /// <summary>The active mission (null if the list is missing or empty).</summary>
    public static MissionDefinition Current
    {
        get
        {
            if (!HasMissions) return null;
            int i = Mathf.Clamp(SaveData.MissionIndex, 0, List.missions.Count - 1);
            return List.missions[i];
        }
    }

    /// <summary>The active mission's target, grown for every time the list has repeated.</summary>
    public static int Target
    {
        get
        {
            MissionDefinition m = Current;
            if (m == null) return 1;
            float growth = Mathf.Pow(List.targetGrowthPerRepeat, SaveData.MissionCycle);
            return Mathf.Max(1, Mathf.CeilToInt(m.target * growth));
        }
    }

    public static string Title => Current != null ? Current.TitleFor(Target) : "";
    public static float Progress => Mathf.Min(SaveData.MissionProgress, Target);
    public static float Progress01 => Mathf.Clamp01(Progress / Target);
    public static string ProgressText => Mathf.FloorToInt(Progress) + "/" + Target;
    public static MissionReward Reward => Current != null ? Current.reward : null;

    /// <summary>True when the active mission is done and its reward has not been claimed yet.</summary>
    public static bool RewardPending => HasMissions && SaveData.MissionRewardPending;

    /// <summary>Called by <see cref="RaceStats.EndRace"/> once per race.</summary>
    public static void ReportRace(int rank, bool failed)
    {
        MissionDefinition m = Current;
        if (m == null || RewardPending)
        {
            return;   // finished missions wait for their claim; nothing counts towards the next one yet
        }

        float value = ValueFromRace(m.type, rank, failed);
        float progress = m.withinOneRace ? Mathf.Max(SaveData.MissionProgress, value) : SaveData.MissionProgress + value;
        bool done = progress >= Target;
        SaveData.MissionProgress = Mathf.Min(progress, Target);

        if (done)
        {
            SaveData.MissionRewardPending = true;
            Completed?.Invoke(m);
        }

        Changed?.Invoke();
    }

    private static float ValueFromRace(MissionType type, int rank, bool failed)
    {
        switch (type)
        {
            case MissionType.WinRaces: return !failed && rank == 1 ? 1f : 0f;
            case MissionType.FinishTop3: return !failed && rank <= 3 ? 1f : 0f;
            case MissionType.FinishRaces: return failed ? 0f : 1f;
            case MissionType.PlayRaces: return 1f;
            case MissionType.Jumps: return RaceStats.Jumps;
            case MissionType.FountainJumps: return RaceStats.FountainJumps;
            case MissionType.FlightSeconds: return RaceStats.FlightSeconds;
            case MissionType.KnockOffRacers: return RaceStats.Knockouts;
            case MissionType.Shortcuts: return RaceStats.Shortcuts;
            default: return 0f;
        }
    }

    /// <summary>
    /// Gives the pending reward and moves on to the next mission. Returns what was given so the card can show it.
    /// Does nothing (returns default) if no reward is pending.
    /// </summary>
    public static MissionGrant ClaimReward()
    {
        var grant = new MissionGrant();
        MissionDefinition m = Current;
        if (m == null || !RewardPending)
        {
            return grant;
        }

        MissionReward r = m.reward;
        grant.kind = r.kind;
        switch (r.kind)
        {
            case MissionRewardKind.Coins:
                grant.amount = r.amount;
                CurrencyManager.Add(RewardType.Coins, r.amount);
                break;

            case MissionRewardKind.Gems:
                grant.amount = r.amount;
                CurrencyManager.Add(RewardType.Gems, r.amount);
                break;

            case MissionRewardKind.FreeSpins:
                grant.amount = r.amount;
                SaveData.BonusSpins += r.amount;
                break;

            case MissionRewardKind.Skin:
                grant.skin = PickSkin(r.skinId);
                if (grant.skin != null)
                {
                    SkinManager.Grant(grant.skin);
                    grant.amount = 1;
                }
                else
                {
                    grant.skinFallback = true;
                    grant.amount = r.fallbackCoins;
                    CurrencyManager.Add(RewardType.Coins, r.fallbackCoins);
                }
                break;
        }

        Advance();
        return grant;
    }

    private static SkinData PickSkin(string id)
    {
        SkinDatabase database = UIManager.Instance != null ? UIManager.Instance.Skins : null;
        if (database == null) return null;

        if (!string.IsNullOrEmpty(id))
        {
            foreach (SkinData skin in database.skins)
                if (skin.id == id && !SkinManager.IsOwned(skin)) return skin;
        }

        foreach (SkinData skin in database.skins)
            if (!SkinManager.IsOwned(skin)) return skin;

        return null;
    }

    private static void Advance()
    {
        int next = SaveData.MissionIndex + 1;
        int cycle = SaveData.MissionCycle;
        if (next >= List.missions.Count)
        {
            next = Mathf.Clamp(List.repeatFromIndex, 0, List.missions.Count - 1);
            cycle++;
        }

        SaveData.SetMission(next, cycle);
        Changed?.Invoke();
    }

    /// <summary>Testing: finishes the active mission straight away (its reward becomes pending).</summary>
    public static void DebugCompleteCurrent()
    {
        if (Current == null) return;
        SaveData.MissionProgress = Target;
        SaveData.MissionRewardPending = true;
        Completed?.Invoke(Current);
        Changed?.Invoke();
    }

    /// <summary>Testing: back to the first mission with no progress.</summary>
    public static void DebugReset()
    {
        SaveData.SetMission(0, 0);
        Changed?.Invoke();
    }
}
