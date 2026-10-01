using UnityEngine;

/// <summary>Ordered list of generated level prefabs. Level N is levels[N - 1].</summary>
[CreateAssetMenu(menuName = "Aquapark/Level Database")]
public class LevelDatabase : ScriptableObject
{
    public LevelConfig[] levels = new LevelConfig[0];

    public int Count => levels.Length;

    public LevelConfig Get(int levelNumber)
    {
        if (levels.Length == 0) return null;
        int index = Mathf.Clamp(levelNumber - 1, 0, levels.Length - 1);
        return levels[index];
    }
}
