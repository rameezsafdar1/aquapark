using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything the player owns and has set, kept as one JSON blob in PlayerPrefs.
/// Every setter saves straight away and raises <see cref="Changed"/> so the UI can refresh itself.
/// </summary>
public static class SaveData
{
    private const string PrefsKey = "aquapark.save.v1";

    [Serializable]
    private class Data
    {
        public int coins = 300;
        public int gems;
        public int level = 1;
        public bool noAds;
        public bool starterPackBought;
        public bool sound = true;
        public bool music = true;
        public bool haptics = true;
        public int dailyStreak;
        public long lastDailyClaimDay = -1;
        public long lastFreeSpinDay = -1;
        public int bonusSpins;
        public int coinBoostRaces;
        public List<string> ownedSkins = new List<string>();
        public string equippedSkin = "";
    }

    private static Data data;

    /// <summary>Raised after any value changes (currencies, settings, skins, timers).</summary>
    public static event Action Changed;

    /// <summary>Days to shift "today" by. Only for testing the daily reward and free spin.</summary>
    public static int DebugDayOffset;

    /// <summary>The current UTC day as a whole number (days since 1970).</summary>
    public static long Today => (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalDays + DebugDayOffset;

    private static Data D
    {
        get
        {
            if (data == null)
            {
                Load();
            }

            return data;
        }
    }

    public static int Coins { get => D.coins; set { D.coins = Mathf.Max(0, value); Commit(); } }
    public static int Gems { get => D.gems; set { D.gems = Mathf.Max(0, value); Commit(); } }
    public static int Level { get => D.level; set { D.level = Mathf.Max(1, value); Commit(); } }
    public static bool NoAds { get => D.noAds; set { D.noAds = value; Commit(); } }
    public static bool StarterPackBought { get => D.starterPackBought; set { D.starterPackBought = value; Commit(); } }
    public static bool Sound { get => D.sound; set { D.sound = value; Commit(); } }
    public static bool Music { get => D.music; set { D.music = value; Commit(); } }
    public static bool Haptics { get => D.haptics; set { D.haptics = value; Commit(); } }
    public static int DailyStreak { get => D.dailyStreak; set { D.dailyStreak = value; Commit(); } }
    public static long LastDailyClaimDay { get => D.lastDailyClaimDay; set { D.lastDailyClaimDay = value; Commit(); } }
    public static long LastFreeSpinDay { get => D.lastFreeSpinDay; set { D.lastFreeSpinDay = value; Commit(); } }
    /// <summary>Extra wheel spins (daily reward day 6).</summary>
    public static int BonusSpins { get => D.bonusSpins; set { D.bonusSpins = Mathf.Max(0, value); Commit(); } }
    /// <summary>Races left that pay double coins (wheel prize).</summary>
    public static int CoinBoostRaces { get => D.coinBoostRaces; set { D.coinBoostRaces = Mathf.Max(0, value); Commit(); } }
    public static string EquippedSkin { get => D.equippedSkin; set { D.equippedSkin = value ?? ""; Commit(); } }

    public static bool OwnsSkin(string id) => D.ownedSkins.Contains(id);

    public static void AddSkin(string id)
    {
        if (!D.ownedSkins.Contains(id))
        {
            D.ownedSkins.Add(id);
            Commit();
        }
    }

    /// <summary>Wipes the save. Handy from the editor menu while testing.</summary>
    public static void ResetAll()
    {
        data = new Data();
        Commit();
    }

    private static void Load()
    {
        try
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            data = string.IsNullOrEmpty(json) ? new Data() : JsonUtility.FromJson<Data>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveData: could not read the save, starting fresh. " + e.Message);
        }

        if (data == null)
        {
            data = new Data();
        }
    }

    private static void Commit()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
