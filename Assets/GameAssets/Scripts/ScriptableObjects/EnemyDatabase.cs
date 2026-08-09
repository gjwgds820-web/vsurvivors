using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "EnemyDatabase", menuName = "Data/EnemyDatabase")]
public class EnemyDatabase : ScriptableObject
{
    public List<EnemyData> enemies = new List<EnemyData>();
    public List<BossPatternData> bossPatterns = new List<BossPatternData>();
    public List<BossActiveSkillData> bossActiveSkills = new List<BossActiveSkillData>();
    public List<BossPassiveSkillEffectData> bossPassiveSkillEffects = new List<BossPassiveSkillEffectData>();
}