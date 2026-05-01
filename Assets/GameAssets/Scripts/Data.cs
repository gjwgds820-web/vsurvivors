using UnityEngine;

[System.Serializable]
public class SkillData
{
    public int ID;
    public int GroupID;
    public int Level;
    public SkillType Type;
    public string Name;
    public string Description;
    public int MaxLevel;
    public int CurrentLevel;
    
    // New fields from CSV
    public string Stats;
    public string Value;
    public string DisplayDescription;
    public string IconPath;

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
    None = 0,
    Fire = 1,
    Water = 2,
    Leaf = 3,
    Light = 4,
    Dark = 5,
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
    public int TargetPriority;
    public int AttackType;
    public float MaxHealth;
    public float AttackPower;
    public float AttackCooldown;
    public float AttackRange;
    public int MaxPierce;
    public float Defence;
    public float MoveSpeed;
    public float Recognize;
    public int SkillID;
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

    // New fields from CSV
    public int AttackType;
    public int MaxPierce;
    public float Def;
    public string EliteType;
    public int Skill1;
    public int Skill2;

    public Sprite Icon;
}
[System.Serializable]
public class StageData
{
    public int ID;
    public string Name;
    public string Help;
    public float Timer;
    public float Elite1;
    public float Elite2;
    public float Boss;
    public int SumChance;
    public int Portal1;
    public int Chance1;
    public int Portal2;
    public int Chance2;
    public int Portal3;
    public int Chance3;
    public int Portal4;
    public int Chance4;
    public int Portal5;
    public int Chance5;
}

[System.Serializable]
public class PortalData
{
    public int ID;
    public string Help;
    public int SummonAmount;
    public string Type;
    public int DelPortal;
    public int Monster1;
    public int Monster2;
    public int Monster3;
    public int Monster4;
    public int Monster5;
}
