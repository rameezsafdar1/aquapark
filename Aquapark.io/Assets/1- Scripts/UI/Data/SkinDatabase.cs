using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How an item is unlocked. Edit it per item in Aquapark > Skins &amp; Floaties Manager.</summary>
public enum SkinUnlock
{
    [Tooltip("Owned from the first launch.")]
    Free,
    [Tooltip("Bought with coins or gems (see Currency and Price).")]
    Currency,
    [Tooltip("Unlocked by watching a rewarded ad.")]
    RewardedAd,
    [Tooltip("Unlocked automatically when the player reaches Unlock Level.")]
    PlayerLevel
}

/// <summary>Which equip slot an item fills: the character model, or the floatie the character rides.</summary>
public enum SkinSlot
{
    Character,
    Floatie
}

public enum SkinRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

[Serializable]
public class SkinData
{
    [Tooltip("Unique and never changed after release: it is what the save file stores.")]
    public string id;
    public string displayName;
    public SkinSlot slot = SkinSlot.Character;
    [Tooltip("Picture shown in the grid and the large preview.")]
    public Sprite icon;
    [Tooltip("Name of the model object on the player: a child of Player/Character for characters, of Character/Floaties for floaties.")]
    public string modelName;
    public RewardType currency = RewardType.Coins;
    [Tooltip("Cost in Currency when Unlock is Currency.")]
    public int price = 500;
    [Tooltip("Owned from the first launch (always true for Free items).")]
    public bool ownedByDefault;
    public SkinUnlock unlock = SkinUnlock.Currency;
    [Tooltip("Player level that unlocks it when Unlock is PlayerLevel.")]
    public int unlockLevel = 10;
    public SkinRarity rarity = SkinRarity.Common;
    [Tooltip("Card colour in the grid: 0 purple, 1 blue, 2 green, 3 yellow.")]
    public int cardColor = 1;

    public bool StartsOwned => ownedByDefault || unlock == SkinUnlock.Free;
}

/// <summary>
/// A list of shop items (the characters, or the floaties). Add an entry and it appears in the grid, no UI work needed.
/// Edit prices and unlock rules in Aquapark > Skins &amp; Floaties Manager, or by selecting the asset.
/// </summary>
[CreateAssetMenu(fileName = "SkinDatabase", menuName = "Aquapark/Skin Database")]
public class SkinDatabase : ScriptableObject
{
    public SkinData[] skins = new SkinData[0];
}

/// <summary>Ownership, unlocking and equipping rules on top of the save file.</summary>
public static class SkinManager
{
    /// <summary>Raised whenever an equipped item changes (check skin.slot). PlayerLoadout listens to swap the model.</summary>
    public static event Action<SkinData> Equipped;

    /// <summary>Raised when an item becomes owned (bought, ad, level, reward), with the item.</summary>
    public static event Action<SkinData> Unlocked;

    private static readonly List<SkinDatabase> databases = new List<SkinDatabase>();
    private static bool listening;
    private static bool checking;

    // No domain reload when entering Play mode: start every session clean (lists, listeners, the SaveData hook).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        databases.Clear();
        listening = false;
        checking = false;
        Equipped = null;
        Unlocked = null;
    }

    public static void Initialise(SkinDatabase database)
    {
        if (database == null || database.skins == null)
        {
            return;
        }

        if (!databases.Contains(database))
        {
            databases.Add(database);
        }

        foreach (SkinData skin in database.skins)
        {
            if (skin.StartsOwned && !string.IsNullOrEmpty(skin.id))
            {
                SaveData.AddSkin(skin.id);
            }
        }

        // A save from an older list may point at an item that no longer exists: fall back to the first owned one,
        // per slot (a database normally holds one slot, but this keeps mixed lists safe).
        foreach (SkinSlot slot in new[] { SkinSlot.Character, SkinSlot.Floatie })
        {
            bool hasSlot = false;
            bool equippedExists = false;
            foreach (SkinData skin in database.skins)
            {
                if (skin.slot != slot)
                {
                    continue;
                }

                hasSlot = true;
                if (skin.id == GetEquipped(slot) && SaveData.OwnsSkin(skin.id))
                {
                    equippedExists = true;
                }
            }

            if (!hasSlot || equippedExists)
            {
                continue;
            }

            foreach (SkinData skin in database.skins)
            {
                if (skin.slot == slot && SaveData.OwnsSkin(skin.id))
                {
                    SetEquipped(slot, skin.id);
                    break;
                }
            }
        }

        CheckLevelUnlocks();

        // Level unlocks happen by themselves whenever the save changes (the level goes up on the results screen).
        if (Application.isPlaying && !listening)
        {
            listening = true;
            SaveData.Changed += CheckLevelUnlocks;
        }
    }

    public static bool IsOwned(SkinData skin) => SaveData.OwnsSkin(skin.id);
    public static bool IsEquipped(SkinData skin) => GetEquipped(skin.slot) == skin.id;

    /// <summary>Id of the equipped item in a slot.</summary>
    public static string GetEquipped(SkinSlot slot) => slot == SkinSlot.Floatie ? SaveData.EquippedFloatie : SaveData.EquippedSkin;

    /// <summary>The equipped item of a slot, looked up in the registered databases (null if none).</summary>
    public static SkinData GetEquippedItem(SkinSlot slot)
    {
        string id = GetEquipped(slot);
        foreach (SkinDatabase database in databases)
        {
            if (database == null || database.skins == null)
            {
                continue;
            }

            foreach (SkinData skin in database.skins)
            {
                if (skin.slot == slot && skin.id == id)
                {
                    return skin;
                }
            }
        }

        return null;
    }

    private static void SetEquipped(SkinSlot slot, string id)
    {
        if (slot == SkinSlot.Floatie)
        {
            SaveData.EquippedFloatie = id;
        }
        else
        {
            SaveData.EquippedSkin = id;
        }
    }

    public static bool Equip(SkinData skin)
    {
        if (!IsOwned(skin))
        {
            return false;
        }

        SetEquipped(skin.slot, skin.id);
        Equipped?.Invoke(skin);
        return true;
    }

    /// <summary>Pays the item's price and unlocks it. False when it is not for sale or the player cannot afford it.</summary>
    public static bool TryBuy(SkinData skin)
    {
        if (IsOwned(skin) || skin.unlock != SkinUnlock.Currency || !CurrencyManager.TrySpend(skin.currency, skin.price))
        {
            return false;
        }

        Grant(skin);
        return true;
    }

    /// <summary>True when the item's level requirement is met (always true for items not unlocked by level).</summary>
    public static bool MeetsLevel(SkinData skin) => skin.unlock != SkinUnlock.PlayerLevel || SaveData.Level >= skin.unlockLevel;

    /// <summary>Unlocks without paying (rewarded ad, level reward, daily chest...).</summary>
    public static void Grant(SkinData skin)
    {
        if (skin == null || IsOwned(skin))
        {
            return;
        }

        SaveData.AddSkin(skin.id);
        Unlocked?.Invoke(skin);
    }

    /// <summary>Unlocks every level item whose level the player has reached.</summary>
    public static void CheckLevelUnlocks()
    {
        // Granting saves, which raises SaveData.Changed, which calls this again.
        if (checking)
        {
            return;
        }

        checking = true;
        foreach (SkinDatabase database in databases)
        {
            if (database == null || database.skins == null)
            {
                continue;
            }

            foreach (SkinData skin in database.skins)
            {
                if (skin.unlock == SkinUnlock.PlayerLevel && !IsOwned(skin) && SaveData.Level >= skin.unlockLevel)
                {
                    Grant(skin);
                }
            }
        }

        checking = false;
    }
}
