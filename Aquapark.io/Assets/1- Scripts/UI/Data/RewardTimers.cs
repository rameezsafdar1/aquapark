using System;

/// <summary>
/// Rules of the 7-day daily reward. One reward per UTC day; miss a day and the cycle starts again.
/// After day 7 the cycle restarts too.
/// </summary>
public static class DailyRewardState
{
    public const int DaysInCycle = 7;

    public static bool CanClaim => SaveData.LastDailyClaimDay != SaveData.Today;

    /// <summary>Index (0-6) of the reward the player gets on their next claim.</summary>
    public static int NextIndex
    {
        get
        {
            long last = SaveData.LastDailyClaimDay;
            if (last >= 0 && SaveData.Today - last > 1)
            {
                return 0;
            }

            return SaveData.DailyStreak % DaysInCycle;
        }
    }

    /// <summary>How many cards on the calendar should show as already claimed.</summary>
    public static int ClaimedCount => CanClaim ? NextIndex : Math.Min(SaveData.DailyStreak, DaysInCycle);

    public static void MarkClaimed(int claimedIndex)
    {
        SaveData.DailyStreak = claimedIndex + 1;
        SaveData.LastDailyClaimDay = SaveData.Today;
    }
}

/// <summary>One free spin of the wheel per UTC day. More spins come from rewarded ads.</summary>
public static class SpinState
{
    public static bool FreeSpinAvailable => SaveData.LastFreeSpinDay != SaveData.Today;

    public static void MarkFreeSpinUsed()
    {
        SaveData.LastFreeSpinDay = SaveData.Today;
    }
}
