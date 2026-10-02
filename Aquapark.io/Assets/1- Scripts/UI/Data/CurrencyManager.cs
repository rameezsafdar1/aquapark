using UnityEngine;

public enum RewardType
{
    Coins,
    Gems
}

/// <summary>Add or spend the two currencies. The UI listens to <see cref="SaveData.Changed"/>.</summary>
public static class CurrencyManager
{
    public static int Coins => SaveData.Coins;
    public static int Gems => SaveData.Gems;

    public static void Add(RewardType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (type == RewardType.Coins)
        {
            SaveData.Coins += amount;
        }
        else
        {
            SaveData.Gems += amount;
        }
    }

    public static bool CanAfford(RewardType type, int amount)
    {
        return (type == RewardType.Coins ? SaveData.Coins : SaveData.Gems) >= amount;
    }

    /// <summary>Takes the amount if the player has it. Returns false (and takes nothing) otherwise.</summary>
    public static bool TrySpend(RewardType type, int amount)
    {
        if (!CanAfford(type, amount))
        {
            return false;
        }

        if (type == RewardType.Coins)
        {
            SaveData.Coins -= amount;
        }
        else
        {
            SaveData.Gems -= amount;
        }

        return true;
    }
}
