using System;
using System.Globalization;

/// <summary>Small text helpers shared by the screens.</summary>
public static class UIFormat
{
    /// <summary>1250 becomes "1,250".</summary>
    public static string Number(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    public static string Ordinal(int n)
    {
        if (n % 100 >= 11 && n % 100 <= 13)
        {
            return n + "th";
        }

        switch (n % 10)
        {
            case 1: return n + "st";
            case 2: return n + "nd";
            case 3: return n + "rd";
            default: return n + "th";
        }
    }

    /// <summary>"23:59:41"</summary>
    public static string Clock(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
        {
            t = TimeSpan.Zero;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
    }

    /// <summary>Race time as "00:42.8".</summary>
    public static string RaceTime(float seconds)
    {
        seconds = Math.Max(0f, seconds);
        int minutes = (int)(seconds / 60f);
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00.0}", minutes, seconds - minutes * 60f);
    }

    /// <summary>Time left until the daily rewards reset (midnight UTC).</summary>
    public static TimeSpan UntilNextDay()
    {
        DateTime now = DateTime.UtcNow;
        return now.Date.AddDays(1) - now;
    }
}
