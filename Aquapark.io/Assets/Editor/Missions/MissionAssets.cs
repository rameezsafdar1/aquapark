using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tools for the mission card: builds the default mission list (Resources/Missions) and test helpers.
/// Menu: Aquapark > Missions.
/// </summary>
public static class MissionAssets
{
    const string Dir = "Assets/Resources/Missions";
    const string ListPath = Dir + "/MissionList.asset";

    struct Spec
    {
        public string file, title;
        public MissionType type;
        public int target;
        public bool oneRace;
        public MissionRewardKind reward;
        public int amount;
        public Spec(string file, string title, MissionType type, int target, bool oneRace, MissionRewardKind reward, int amount)
        {
            this.file = file; this.title = title; this.type = type; this.target = target; this.oneRace = oneRace; this.reward = reward; this.amount = amount;
        }
    }

    // Easy start, then a mix of racing, jumping, flying and knocking racers off, with the rewards spread across coins,
    // gems, free spins and the odd skin.
    static readonly Spec[] Defaults =
    {
        new Spec("M01_FirstRace",      "Finish your first race",               MissionType.FinishRaces,    1,  false, MissionRewardKind.Coins,     50),
        new Spec("M02_Jumps5OneRace",  "Make {0} jumps in one race",           MissionType.Jumps,          5,  true,  MissionRewardKind.Coins,     75),
        new Spec("M03_Win3",           "Win {0} races",                        MissionType.WinRaces,       3,  false, MissionRewardKind.Coins,     100),
        new Spec("M04_Fountains3",     "Jump off {0} fountains",               MissionType.FountainJumps,  3,  false, MissionRewardKind.Gems,      5),
        new Spec("M05_KnockOff3",      "Throw {0} players off",      MissionType.KnockOffRacers, 3,  false, MissionRewardKind.FreeSpins, 1),
        new Spec("M06_Fly30",          "Fly for {0} seconds",                  MissionType.FlightSeconds,  30, false, MissionRewardKind.Coins,     150),
        new Spec("M07_Top3x5",         "Finish top 3 {0} times",        MissionType.FinishTop3,     5,  false, MissionRewardKind.Gems,      10),
        new Spec("M08_Shortcuts2",     "Take {0} shortcuts",                   MissionType.Shortcuts,      2,  false, MissionRewardKind.Coins,     200),
        new Spec("M09_Jumps8OneRace",  "Make {0} jumps in one race",           MissionType.Jumps,          8,  true,  MissionRewardKind.FreeSpins, 1),
        new Spec("M10_KnockOff5",      "Throw {0} players off",      MissionType.KnockOffRacers, 5,  false, MissionRewardKind.Skin,      1),
        new Spec("M11_Win5",           "Win {0} races",                        MissionType.WinRaces,       5,  false, MissionRewardKind.Coins,     250),
        new Spec("M12_Fly15OneRace",   "Fly {0}s in one race",      MissionType.FlightSeconds,  15, true,  MissionRewardKind.Gems,      15),
        new Spec("M13_Play10",         "Play {0} races",                       MissionType.PlayRaces,      10, false, MissionRewardKind.FreeSpins, 2),
        new Spec("M14_Shortcuts3OneRace", "{0} shortcuts in one race",    MissionType.Shortcuts,      3,  true,  MissionRewardKind.Coins,     300),
        new Spec("M15_KnockOff3OneRace", "Throw {0} off in one race",  MissionType.KnockOffRacers, 3,  true,  MissionRewardKind.Gems,      20),
        new Spec("M16_Win10",          "Win {0} races",                        MissionType.WinRaces,       10, false, MissionRewardKind.Skin,      1),
    };

    [MenuItem("Aquapark/Missions/Create Default Mission List")]
    public static void CreateDefaults()
    {
        EnsureFolder(Dir);
        var list = AssetDatabase.LoadAssetAtPath<MissionList>(ListPath);
        if (list == null)
        {
            list = ScriptableObject.CreateInstance<MissionList>();
            AssetDatabase.CreateAsset(list, ListPath);
        }

        list.missions = new List<MissionDefinition>();
        foreach (var s in Defaults)
        {
            string path = $"{Dir}/{s.file}.asset";
            var m = AssetDatabase.LoadAssetAtPath<MissionDefinition>(path);
            if (m == null)
            {
                m = ScriptableObject.CreateInstance<MissionDefinition>();
                AssetDatabase.CreateAsset(m, path);
            }

            m.title = s.title;
            m.type = s.type;
            m.target = s.target;
            m.withinOneRace = s.oneRace;
            m.reward = new MissionReward { kind = s.reward, amount = s.amount, fallbackCoins = 500 };
            EditorUtility.SetDirty(m);
            list.missions.Add(m);
        }

        list.repeatFromIndex = 2;
        list.targetGrowthPerRepeat = 1.5f;
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssets();
        Debug.Log($"Mission list: {list.missions.Count} missions at {ListPath}");
    }

    [MenuItem("Aquapark/Missions/Log Current Mission")]
    public static void LogCurrent()
    {
        Debug.Log(MissionManager.HasMissions
            ? $"Mission {SaveData.MissionIndex + 1} (round {SaveData.MissionCycle + 1}): {MissionManager.Title}  {MissionManager.ProgressText}  reward {MissionManager.Reward.kind} {MissionManager.Reward.amount}  pending {MissionManager.RewardPending}"
            : "No mission list at Resources/" + MissionManager.ListResourcePath);
    }

    [MenuItem("Aquapark/Missions/Complete Current Mission (test)")]
    public static void CompleteCurrent()
    {
        MissionManager.DebugCompleteCurrent();
        LogCurrent();
    }

    [MenuItem("Aquapark/Missions/Reset Missions (test)")]
    public static void ResetMissions()
    {
        MissionManager.DebugReset();
        LogCurrent();
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }
}
