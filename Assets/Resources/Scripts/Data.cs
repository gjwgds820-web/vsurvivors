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
    [Newtonsoft.Json.JsonIgnore] 
    public Sprite Icon;
}

public enum SkillType
{
    Shadow,
    Passive,
}

public enum ElementType
{
    Fire = 0,
    Water = 1,
    Leaf = 2,
    Light = 3,
    Dark = 4,
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
    public ElementType Element;
    public Sprite Icon;
    public LevelStat[] LevelStats;
    public int TargetPriority;
    public int AttackType;
}

[System.Serializable]
public class LevelStat
{
    public int MaxHealth;
    public int AttackPower;
    public int AttackRange;
    public float AttackCooldown;
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

[System.Serializable]
public class EnemyData
{
    public int ID;
    public string Name;
    public string Description;
    public EnemyType Type;
    public float MaxHealth;
    public float AttackPower;
    public float AttackRange;
    public float AttackCooldown;
    public float MoveSpeed;
    public HitBoxShape HitBoxShape;
    public float HitboxRadius;
    public float HitboxDuration;
    public bool IsPiercing;
    public bool IsBoss;
    public Sprite Icon;
}