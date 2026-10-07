using UnityEditor;
using UnityEngine;

/// <summary>
/// Aquapark > Skins &amp; Floaties Manager: one table per list (characters, floaties) where each item's name, unlock rule
/// (free, coins, gems, player level or rewarded ad), price / level, rarity and card colour are set. Changes are saved in
/// the SkinDatabase assets, which the shop and the player loadout read at runtime.
/// </summary>
public class SkinManagerWindow : EditorWindow
{
    private const string CharactersPath = "Assets/UI/Figma/Data/SkinDatabase.asset";
    private const string FloatiesPath = "Assets/UI/Figma/Data/FloatieDatabase.asset";

    private int tab;
    private Vector2 scroll;
    private SerializedObject list;

    [MenuItem("Aquapark/Skins & Floaties Manager")]
    public static void Open()
    {
        var window = GetWindow<SkinManagerWindow>("Skins & Floaties");
        window.minSize = new Vector2(760f, 400f);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4f);
        int newTab = GUILayout.Toolbar(tab, new[] { "Characters", "Floaties" }, GUILayout.Height(26f));
        if (newTab != tab || list == null || list.targetObject == null)
        {
            tab = newTab;
            var db = AssetDatabase.LoadAssetAtPath<SkinDatabase>(tab == 0 ? CharactersPath : FloatiesPath);
            list = db != null ? new SerializedObject(db) : null;
            scroll = Vector2.zero;
        }

        if (list == null)
        {
            EditorGUILayout.HelpBox("List not found: " + (tab == 0 ? CharactersPath : FloatiesPath) +
                                    ". Run Aquapark > UI > Rebuild Skin Lists.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(4f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        SkinListDrawer.Draw(list);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(2f);
        if (GUILayout.Button("Select list asset", GUILayout.Width(140f)))
        {
            Selection.activeObject = list.targetObject;
            EditorGUIUtility.PingObject(list.targetObject);
        }

        EditorGUILayout.Space(4f);
    }
}

/// <summary>Selecting a SkinDatabase asset shows the same table as the manager window.</summary>
[CustomEditor(typeof(SkinDatabase))]
public class SkinDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Open Skins & Floaties Manager"))
        {
            SkinManagerWindow.Open();
        }

        EditorGUILayout.Space(4f);
        SkinListDrawer.Draw(serializedObject);
    }
}

/// <summary>Draws a SkinDatabase as an editable table.</summary>
public static class SkinListDrawer
{
    // One dropdown for what the designer thinks in; maps onto SkinData.unlock + SkinData.currency.
    private static readonly string[] UnlockOptions = { "Free", "Coins", "Gems", "Player level", "Rewarded ad" };
    private static readonly string[] ColorOptions = { "Purple", "Blue", "Green", "Yellow" };

    private const float Icon = 44f;

    public static void Draw(SerializedObject so)
    {
        so.Update();
        SerializedProperty items = so.FindProperty("skins");

        int free = 0, coins = 0, gems = 0, level = 0, ad = 0;
        for (int i = 0; i < items.arraySize; i++)
        {
            switch (GetUnlock(items.GetArrayElementAtIndex(i)))
            {
                case 0: free++; break;
                case 1: coins++; break;
                case 2: gems++; break;
                case 3: level++; break;
                default: ad++; break;
            }
        }

        EditorGUILayout.HelpBox(items.arraySize + " items  -  " + free + " free, " + coins + " coins, " + gems + " gems, " +
                                level + " by level, " + ad + " by ad. Order here = order in the shop grid. Level items " +
                                "unlock by themselves when the player reaches the level.", MessageType.None);

        Header();
        int remove = -1, moveUp = -1, moveDown = -1;
        for (int i = 0; i < items.arraySize; i++)
        {
            SerializedProperty item = items.GetArrayElementAtIndex(i);
            Row(item, i, items.arraySize, ref remove, ref moveUp, ref moveDown);
        }

        if (remove >= 0 && EditorUtility.DisplayDialog("Remove item",
                "Remove '" + items.GetArrayElementAtIndex(remove).FindPropertyRelative("displayName").stringValue +
                "' from the shop? Players who own it keep it in their save, it just stops showing.", "Remove", "Cancel"))
        {
            items.DeleteArrayElementAtIndex(remove);
        }

        if (moveUp > 0)
        {
            items.MoveArrayElement(moveUp, moveUp - 1);
        }

        if (moveDown >= 0 && moveDown < items.arraySize - 1)
        {
            items.MoveArrayElement(moveDown, moveDown + 1);
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("+ Add item", GUILayout.Width(120f)))
        {
            items.arraySize++;
            SerializedProperty added = items.GetArrayElementAtIndex(items.arraySize - 1);
            added.FindPropertyRelative("id").stringValue = "new_item_" + items.arraySize;
            added.FindPropertyRelative("displayName").stringValue = "New item";
            added.FindPropertyRelative("ownedByDefault").boolValue = false;
            added.isExpanded = true;
        }

        so.ApplyModifiedProperties();
    }

    private static void Header()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Space(Icon + 8f);
        GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(120f));
        GUILayout.Label("Unlocked by", EditorStyles.miniBoldLabel, GUILayout.Width(100f));
        GUILayout.Label("Price / level", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
        GUILayout.Label("Rarity", EditorStyles.miniBoldLabel, GUILayout.Width(80f));
        GUILayout.Label("Card", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
        GUILayout.Label("Owned at start", EditorStyles.miniBoldLabel, GUILayout.Width(90f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private static void Row(SerializedProperty item, int index, int count, ref int remove, ref int moveUp, ref int moveDown)
    {
        SerializedProperty name = item.FindPropertyRelative("displayName");
        SerializedProperty icon = item.FindPropertyRelative("icon");
        SerializedProperty price = item.FindPropertyRelative("price");
        SerializedProperty unlockLevel = item.FindPropertyRelative("unlockLevel");
        SerializedProperty owned = item.FindPropertyRelative("ownedByDefault");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        Rect iconRect = GUILayoutUtility.GetRect(Icon, Icon, GUILayout.Width(Icon), GUILayout.Height(Icon));
        DrawSprite(iconRect, icon.objectReferenceValue as Sprite);

        EditorGUILayout.BeginVertical();
        GUILayout.Space(12f);
        EditorGUILayout.BeginHorizontal();

        name.stringValue = EditorGUILayout.TextField(name.stringValue, GUILayout.Width(120f));

        int unlock = GetUnlock(item);
        int newUnlock = EditorGUILayout.Popup(unlock, UnlockOptions, GUILayout.Width(100f));
        if (newUnlock != unlock)
        {
            SetUnlock(item, newUnlock);
            unlock = newUnlock;
        }

        switch (unlock)
        {
            case 1:
            case 2:
                price.intValue = Mathf.Max(0, EditorGUILayout.IntField(price.intValue, GUILayout.Width(80f)));
                break;
            case 3:
                unlockLevel.intValue = Mathf.Max(1, EditorGUILayout.IntField(unlockLevel.intValue, GUILayout.Width(80f)));
                break;
            default:
                GUILayout.Label("-", GUILayout.Width(80f));
                break;
        }

        EditorGUILayout.PropertyField(item.FindPropertyRelative("rarity"), GUIContent.none, GUILayout.Width(80f));
        SerializedProperty color = item.FindPropertyRelative("cardColor");
        color.intValue = EditorGUILayout.Popup(Mathf.Clamp(color.intValue, 0, 3), ColorOptions, GUILayout.Width(70f));

        using (new EditorGUI.DisabledScope(unlock == 0))
        {
            bool shown = unlock == 0 || owned.boolValue;
            bool set = EditorGUILayout.Toggle(shown, GUILayout.Width(90f));
            if (unlock != 0)
            {
                owned.boolValue = set;
            }
        }

        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(index == 0))
        {
            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24f))) moveUp = index;
        }

        using (new EditorGUI.DisabledScope(index == count - 1))
        {
            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24f))) moveDown = index;
        }

        if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24f))) remove = index;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, "Details", true);
        if (item.isExpanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(item.FindPropertyRelative("id"), new GUIContent("Save id", "Stored in save files: never change it after release."));
            EditorGUILayout.PropertyField(item.FindPropertyRelative("modelName"), new GUIContent("Model name", "Child of Player/Character (characters) or Character/Floaties (floaties)."));
            EditorGUILayout.PropertyField(icon);
            EditorGUILayout.PropertyField(item.FindPropertyRelative("slot"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>0 free, 1 coins, 2 gems, 3 level, 4 ad.</summary>
    private static int GetUnlock(SerializedProperty item)
    {
        var unlock = (SkinUnlock)item.FindPropertyRelative("unlock").enumValueIndex;
        switch (unlock)
        {
            case SkinUnlock.Free: return 0;
            case SkinUnlock.PlayerLevel: return 3;
            case SkinUnlock.RewardedAd: return 4;
            default:
                return (RewardType)item.FindPropertyRelative("currency").enumValueIndex == RewardType.Gems ? 2 : 1;
        }
    }

    private static void SetUnlock(SerializedProperty item, int option)
    {
        SerializedProperty unlock = item.FindPropertyRelative("unlock");
        SerializedProperty currency = item.FindPropertyRelative("currency");
        SerializedProperty price = item.FindPropertyRelative("price");
        switch (option)
        {
            case 0:
                unlock.enumValueIndex = (int)SkinUnlock.Free;
                break;
            case 1:
            case 2:
                unlock.enumValueIndex = (int)SkinUnlock.Currency;
                currency.enumValueIndex = (int)(option == 1 ? RewardType.Coins : RewardType.Gems);
                if (price.intValue <= 0)
                {
                    price.intValue = option == 1 ? 1000 : 50;
                }

                break;
            case 3:
                unlock.enumValueIndex = (int)SkinUnlock.PlayerLevel;
                break;
            default:
                unlock.enumValueIndex = (int)SkinUnlock.RewardedAd;
                break;
        }
    }

    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        EditorGUI.DrawRect(rect, new Color(0.23f, 0.45f, 0.78f, 1f));
        if (sprite == null || sprite.texture == null)
        {
            return;
        }

        Rect tr = sprite.textureRect;
        float aspect = tr.width / tr.height;
        Rect fit = rect;
        if (aspect > 1f)
        {
            fit.height = rect.width / aspect;
            fit.y += (rect.height - fit.height) * 0.5f;
        }
        else
        {
            fit.width = rect.height * aspect;
            fit.x += (rect.width - fit.width) * 0.5f;
        }

        Texture2D tex = sprite.texture;
        var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        GUI.DrawTextureWithTexCoords(fit, tex, uv, true);
    }
}
