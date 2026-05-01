using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Data/SkillDatabase")]
public class SkillDatabase : ScriptableObject
{
    public List<SkillData> skills = new List<SkillData>();
}