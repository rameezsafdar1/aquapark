using UnityEditor;
using UnityEngine;

/// <summary>Editor shortcut to wipe the player's save (coins, gems, level, owned and equipped skins, missions...).</summary>
public static class SaveDataMenu
{
    [MenuItem("Aquapark/Reset Save Data")]
    public static void ResetSave()
    {
        if (!EditorUtility.DisplayDialog("Reset save data",
                "Wipe coins, gems, level, owned / equipped skins and floaties, missions and settings?", "Reset", "Cancel"))
        {
            return;
        }

        // Clears the in-memory copy too (PlayerPrefs alone is not enough: the save stays loaded between play sessions),
        // then writes a fresh save. Default items are owned again the next time the game starts.
        SaveData.ResetAll();
        Debug.Log(Application.isPlaying
            ? "Save data reset. Exit and re-enter Play mode to start from a fresh game."
            : "Save data reset.");
    }
}
