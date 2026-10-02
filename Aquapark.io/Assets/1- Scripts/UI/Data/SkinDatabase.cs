using System;
using UnityEngine;

public enum SkinUnlock
{
    Free,
    Currency,
    RewardedAd,
    PlayerLevel
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
    [Tooltip("Picture shown in the grid and the large preview.")]
    public Sprite icon;
    [Tooltip("The character model to use in the race. Not used by the UI itself, listen to SkinManager.Equipped to swap it.")]
    public GameObject modelPrefab;
    public RewardType currency = RewardType.Coins;
    [Tooltip("0 and 'Owned By Default' for free skins.")]
    public int price = 500;
    public bool ownedByDefault;
    public SkinUnlock unlock = SkinUnlock.Currency;
    [Tooltip("Player level needed when Unlock is PlayerLevel.")]
    public int unlockLevel = 10;
    public SkinRarity rarity = SkinRarity.Common;
    [Tooltip("Card colour in the grid: 0 purple, 1 blue, 2 green, 3 yellow.")]
    public int cardColor = 1;
}

/// <summary>
/// The list of skins the Skins panel shows. Add an entry here and it appears in the grid, no UI work needed.
/// </summary>
[CreateAssetMenu(fileName = "SkinDatabase", menuName = "Aquapark/Skin Database")]
public class SkinDatabase : ScriptableObject
{
    public SkinData[] skins = new SkinData[0];
}

/// <summary>Ownership and equipping rules on top of the save file.</summary>
public static class SkinManager
{
    /// <summary>Raised whenever the equipped skin changes. A player model swapper can listen to this.</summary>
    public static event Action<SkinData> Equipped;

    public static void Initialise(SkinDatabase database)
    {
        if (database == null || database.skins == null)
        {
            return;
        }

        foreach (SkinData skin in database.skins)
        {
            if (skin.ownedByDefault && !string.IsNullOrEmpty(skin.id))
            {
                SaveData.AddSkin(skin.id);
            }
        }

        // A save from an older skin list may point at a skin that no longer exists.
        bool equippedExists = false;
        foreach (SkinData skin in database.skins)
        {
            if (skin.id == SaveData.EquippedSkin && SaveData.OwnsSkin(skin.id))
            {
                equippedExists = true;
            }
        }

        if (!equippedExists)
        {
            foreach (SkinData skin in database.skins)
            {
                if (SaveData.OwnsSkin(skin.id))
                {
                    SaveData.EquippedSkin = skin.id;
                    break;
                }
            }
        }
    }

    public static bool IsOwned(SkinData skin) => SaveData.OwnsSkin(skin.id);
    public static bool IsEquipped(SkinData skin) => SaveData.EquippedSkin == skin.id;

    public static bool Equip(SkinData skin)
    {
        if (!IsOwned(skin))
        {
            return false;
        }

        SaveData.EquippedSkin = skin.id;
        Equipped?.Invoke(skin);
        return true;
    }

    public static bool TryBuy(SkinData skin)
    {
        if (IsOwned(skin) || skin.unlock != SkinUnlock.Currency || !CurrencyManager.TrySpend(skin.currency, skin.price))
        {
            return false;
        }

        SaveData.AddSkin(skin.id);
        return true;
    }

    /// <summary>True when the skin can be unlocked right now with its own rule (level reached or free).</summary>
    public static bool MeetsLevel(SkinData skin) => skin.unlock != SkinUnlock.PlayerLevel || SaveData.Level >= skin.unlockLevel;

    /// <summary>Unlocks without paying (rewarded ad, level reward, daily chest...).</summary>
    public static void Grant(SkinData skin)
    {
        SaveData.AddSkin(skin.id);
    }
}
