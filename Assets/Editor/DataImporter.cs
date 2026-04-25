using UnityEngine;
using UnityEditor;
using System.IO;
using System;

public class DataImporter
{
    #region Skill
    [MenuItem("Tools/Import Skill Data(CSV)")]
    public static void ImportSkillDataFromCSV()
    {
        string path = Application.dataPath + "/Resources/Data/abillity.csv";

        if (!File.Exists(path))
        {
            Debug.LogError("abillity.csv file not found at: " + path);
            return;
        }

        string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
        string assetPath = "Assets/Resources/Data/SkillDatabase.asset";

        SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<SkillDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new SkillDatabase asset at: " + assetPath);
        }

        database.skills.Clear();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] row = lines[i].Split(',');
            if (row.Length < 8) continue;

            SkillData skill = new SkillData
            {
                ID = int.Parse(row[0]),
                GroupID = int.Parse(row[0]) / 100,
                Level = int.Parse(row[0]) % 100,
                Name = row[1],
                Description = row[2],
                Type = row[3].Trim() == "Active" ? SkillType.Shadow : SkillType.Passive,
                Stats = row[4],
                Value = row[5],
                DisplayDescription = row[6],
                IconPath = row[7],
                CurrentLevel = int.Parse(row[0]) % 100, // 호환성을 위해 Level 값을 CurrentLevel에도 설정
                MaxLevel = row[3].Trim() == "Active" ? 6 : 5 // 임시로 Active는 6, 아니면 5
            };
            
            skill.Icon = Resources.Load<Sprite>("Icons/Skills/" + skill.ID);
            database.skills.Add(skill);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Skill data imported successfully from CSV.");
        AssetDatabase.Refresh();
    }
    #endregion

    #region Character
    [MenuItem("Tools/Import Character Data(JSON)")]
    public static void ImportCharacterDataFromJson()
    {
        string path = Application.dataPath + "/Resources/Data/CharacterData.json";

        if (!File.Exists(path))
        {
            Debug.LogError("CharacterData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/Resources/Data/CharacterDatabase.asset";

        CharacterDatabase database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<CharacterDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new CharacterDatabase asset at: " + assetPath);
        }

        JsonUtility.FromJsonOverwrite(jsonContent, database);
        foreach (CharacterData character in database.characters)
        {
            character.Icon = Resources.Load<Sprite>("Icons/Characters/" + character.Name);
            if (character.Icon == null)
            {
                Debug.LogWarning($"[캐릭터 아이콘 로드 실패] ID: {character.ID} / 찾으려는 경로: Resources/Icons/Characters/{character.Name}");
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Character data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion
    
    #region Relic
    [MenuItem("Tools/Import Relic Data(JSON)")]
    public static void ImportRelicDataFromJson()
    {
        string path = Application.dataPath + "/Resources/Data/RelicData.json";

        if (!File.Exists(path))
        {
            Debug.LogError("RelicData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/Resources/Data/RelicDatabase.asset";

        RelicDatabase database = AssetDatabase.LoadAssetAtPath<RelicDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<RelicDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new RelicDatabase asset at: " + assetPath);
        }

        JsonUtility.FromJsonOverwrite(jsonContent, database);
        foreach (RelicData relic in database.relics)
        {
            relic.Icon = Resources.Load<Sprite>("Icons/Relics/" + relic.Name);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Relic data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion

    #region Shadow
    [MenuItem("Tools/Import Shadow Data(JSON)")]
    public static void ImportShadowDataFromJson()
    {
        string path = Application.dataPath + "/Resources/Data/ShadowData.json";
        if (!File.Exists(path))
        {
            Debug.LogError("ShadowData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/Resources/Data/ShadowDatabase.asset";

        ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ShadowDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new ShadowDatabase asset at: " + assetPath);
        }

        JsonUtility.FromJsonOverwrite(jsonContent, database);
        foreach (ShadowData shadow in database.shadows)
        {
            shadow.Icon = Resources.Load<Sprite>("Icons/Shadows/" + shadow.ID);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Shadow data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion

    #region Upgrade
    [MenuItem("Tools/Import Upgrade Data(JSON)")]
    public static void ImportUpgradeDataFromJson()
    {
        string path = Application.dataPath + "/Resources/Data/UpgradeData.json";
        if (!File.Exists(path))
        {
            Debug.LogError("UpgradeData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/Resources/Data/UpgradeDatabase.asset";

        UpgradeDatabase database = AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<UpgradeDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new UpgradeDatabase asset at: " + assetPath);
        }

        JsonUtility.FromJsonOverwrite(jsonContent, database);
        foreach (UpgradeData upgrade in database.upgrades)
        {
            upgrade.Icon = Resources.Load<Sprite>("Icons/Upgrades/" + upgrade.Name);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Upgrade data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion
    #region Enemy
    [MenuItem("Tools/Import Enemy Data(CSV)")]
    public static void ImportEnemyDataFromCSV()
    {
        string assetPath = "Assets/Resources/Data/EnemyDatabase.asset";
        EnemyDatabase database = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<EnemyDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new EnemyDatabase asset at: " + assetPath);
        }

        database.enemies.Clear();

        // 1. Load Monster CSV
        string monsterPath = Application.dataPath + "/Resources/Data/monster.csv";
        if (File.Exists(monsterPath))
        {
            string[] lines = File.ReadAllLines(monsterPath, System.Text.Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] row = lines[i].Split(',');
                if (row.Length < 11) continue;

                EnemyData enemy = new EnemyData
                {
                    ID = int.Parse(row[0]),
                    Name = row[1],
                    Description = row[2],
                    AttackType = int.Parse(row[3]),
                    MaxHealth = float.Parse(row[4]),
                    AttackPower = float.Parse(row[5]),
                    AttackCooldown = float.Parse(row[6]),
                    AttackRange = float.Parse(row[7]),
                    MaxPierce = int.Parse(row[8]),
                    Def = float.Parse(row[9]),
                    MoveSpeed = float.Parse(row[10]),
                    IsBoss = false,
                    IsPiercing = int.Parse(row[8]) > 0 // 임시 맵핑 (max_pierce에 따라)
                };
                enemy.Icon = Resources.Load<Sprite>("Icons/Enemies/" + enemy.Name);
                database.enemies.Add(enemy);
            }
        }
        else
        {
            Debug.LogWarning("monster.csv file not found at: " + monsterPath);
        }

        // 2. Load Boss CSV
        string bossPath = Application.dataPath + "/Resources/Data/boss.csv";
        if (File.Exists(bossPath))
        {
            string[] lines = File.ReadAllLines(bossPath, System.Text.Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] row = lines[i].Split(',');
                if (row.Length < 13) continue;

                EnemyData enemy = new EnemyData
                {
                    ID = int.Parse(row[0]),
                    Name = row[1],
                    Description = row[2],
                    EliteType = row[3],
                    MaxHealth = float.Parse(row[4]),
                    AttackPower = float.Parse(row[5]),
                    AttackCooldown = float.Parse(row[6]),
                    AttackRange = float.Parse(row[7]),
                    MaxPierce = int.Parse(row[8]),
                    Def = float.Parse(row[9]),
                    MoveSpeed = float.Parse(row[10]),
                    Skill1 = int.TryParse(row[11], out int s1) ? s1 : 0,
                    Skill2 = int.TryParse(row[12], out int s2) ? s2 : 0,
                    IsBoss = true,
                    IsPiercing = int.Parse(row[8]) > 0
                };
                enemy.Icon = Resources.Load<Sprite>("Icons/Enemies/" + enemy.Name);
                database.enemies.Add(enemy);
            }
        }
        else
        {
            Debug.LogWarning("boss.csv file not found at: " + bossPath);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Enemy data imported successfully from Monster and Boss CSVs.");
        AssetDatabase.Refresh();
    }
    #endregion

    #region Stage
    [MenuItem("Tools/Import Stage Data(JSON)")]
    public static void ImportStageDataFromJson()
    {
        string path = Application.dataPath + "/Resources/Data/StageData.json";

        if (!File.Exists(path))
        {
            Debug.LogError("StageData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/Resources/Data/StageDatabase.asset";

        StageDatabase database = AssetDatabase.LoadAssetAtPath<StageDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<StageDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new StageDatabase asset at: " + assetPath);
        }

        JsonUtility.FromJsonOverwrite(jsonContent, database);

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Stage data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion
}

