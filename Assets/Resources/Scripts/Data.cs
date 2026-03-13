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
    public Sprite Icon;
}

public enum SkillType
{
    Shadow,
    Passive,
}