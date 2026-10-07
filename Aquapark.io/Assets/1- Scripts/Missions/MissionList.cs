using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The missions in the order the player gets them. When one is claimed the next one shows up. After the last one the list
/// repeats from <see cref="repeatFromIndex"/> with bigger targets, so there is always a mission.
/// Lives at Resources/Missions/MissionList (loaded by <see cref="MissionManager"/>).
/// </summary>
[CreateAssetMenu(menuName = "Aquapark/Mission List", fileName = "MissionList")]
public class MissionList : ScriptableObject
{
    public List<MissionDefinition> missions = new List<MissionDefinition>();
    [Tooltip("Where the list starts again after the last mission (0 = from the start). Lets the easy first missions be one-offs.")]
    [Min(0)] public int repeatFromIndex = 2;
    [Tooltip("Targets are multiplied by this for every time the list has repeated (1.5 = +50% per round).")]
    [Min(1f)] public float targetGrowthPerRepeat = 1.5f;
}
