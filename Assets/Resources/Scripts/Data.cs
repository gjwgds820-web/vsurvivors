using UnityEngine;

[System.Serializable]
public class SkillData
{
    public int ID;
    public SkillType Type;
    public string Name;
    public string Description;
    public int MaxLevel;
    public int CurrentLevel;
    [System.NonSerialized] public Sprite Icon;
}

public enum SkillType
{
    Shadow,
    Passive,
}

[System.Serializable]
public class CharacterData
{
    public int ID;
    public string Name;
    public string Description;
    public int MaxLevel;
    public int CurrentLevel;
    public Sprite Icon;
}

[System.Serializable]
public class RelicData
{
    public int ID;
    public string Name;
    public string Description;
    public int MaxLevel;
    public int CurrentLevel;
    public Sprite Icon;
}

[System.Serializable]
public class ShadowData
{
    public int ID;
    public string Name;
    public string Description;
    public int MaxLevel;
    public int CurrentLevel;
    public Sprite Icon;
}

[System.Serializable]
public class UpgradeData
{
    public int ID;
    public int Type;
    public string Name;
    public string Description;
    public int MaxLevel;
    public int CurrentLevel;
    public string CostType;
    public int CostAmount;
    public string EffectType;
    public float EffectAmount;
    public Sprite Icon;
}