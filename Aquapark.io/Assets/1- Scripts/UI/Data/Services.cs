using System;
using UnityEngine;

/// <summary>
/// Rewarded-ad entry point for the UI. Until an ad SDK is hooked up it grants the reward straight away
/// so the buttons can be tested. To go live, assign <see cref="RewardedHandler"/> once at startup:
/// it must show a rewarded ad and call the callback with true if the player earned the reward.
/// </summary>
public static class AdService
{
    public static Action<Action<bool>> RewardedHandler;

    public static void ShowRewarded(Action onRewarded)
    {
        if (RewardedHandler == null)
        {
            Debug.Log("[AdService] No ad SDK hooked up, granting the reward directly.");
            onRewarded?.Invoke();
            return;
        }

        RewardedHandler(earned =>
        {
            if (earned)
            {
                onRewarded?.Invoke();
            }
        });
    }
}

/// <summary>
/// Real-money purchase entry point. Until Unity IAP (or similar) is hooked up the purchase succeeds
/// immediately. To go live, assign <see cref="PurchaseHandler"/> (productId, callback(success)) and
/// <see cref="RestoreHandler"/>, and optionally <see cref="PriceLookup"/> to show store prices.
/// </summary>
public static class PurchaseService
{
    public static Action<string, Action<bool>> PurchaseHandler;
    public static Action RestoreHandler;
    public static Func<string, string> PriceLookup;

    public static string GetPrice(string productId, string fallback)
    {
        string price = PriceLookup?.Invoke(productId);
        return string.IsNullOrEmpty(price) ? fallback : price;
    }

    public static void Purchase(string productId, Action onSuccess)
    {
        if (PurchaseHandler == null)
        {
            Debug.Log("[PurchaseService] No store hooked up, granting " + productId + " directly.");
            onSuccess?.Invoke();
            return;
        }

        PurchaseHandler(productId, ok =>
        {
            if (ok)
            {
                onSuccess?.Invoke();
            }
        });
    }

    public static void Restore()
    {
        if (RestoreHandler == null)
        {
            Debug.Log("[PurchaseService] No store hooked up, nothing to restore.");
            return;
        }

        RestoreHandler();
    }
}

/// <summary>Short vibration for rewards and results, respecting the Settings toggle.</summary>
public static class Haptics
{
    public static void Pulse()
    {
        if (!SaveData.Haptics)
        {
            return;
        }

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
