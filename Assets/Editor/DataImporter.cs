using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class DataImporter
{
    #region Import All
    [MenuItem("Tools/Import All Data")]
    public static void ImportAllData()
    {
        Debug.Log("Starting to import all data...");
        ImportSkillDataFromCSV();
        ImportCharacterDataFromJson();
        ImportRelicDataFromJson();
        ImportShadowDataFromCSV();
        ImportUpgradeDataFromJson();
        ImportEnemyDataFromCSV();
        ImportStageDataFromJson();
        Debug.Log("All data imported successfully.");
    }
    #endregion

    #region Skill
    [MenuItem("Tools/Import Skill Data(CSV)")]
    public static void ImportSkillDataFromCSV()
    {
        string path = Application.dataPath + "/GameAssets/Data/abillity.csv";

        if (!File.Exists(path))
        {
            Debug.LogError("abillity.csv file not found at: " + path);
            return;
        }

        string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
        string assetPath = "Assets/GameAssets/Data/SkillDatabase.asset";

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
            
            skill.Icon = LoadIconSprite("Assets/GameAssets/Icons/Skills", skill.ID.ToString());
            database.skills.Add(skill);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        SetAssetAddressable(assetPath);
        Debug.Log("Skill data imported successfully from CSV.");
        AssetDatabase.Refresh();
    }
    #endregion

    #region Character
    [MenuItem("Tools/Import Character Data(JSON)")]
    public static void ImportCharacterDataFromJson()
    {
        string path = Application.dataPath + "/GameAssets/Data/CharacterData.json";

        if (!File.Exists(path))
        {
            Debug.LogError("CharacterData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/GameAssets/Data/CharacterDatabase.asset";

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
            character.Icon = LoadIconSprite("Assets/GameAssets/Icons/Characters", character.Name);
            if (character.Icon == null)
            {
                Debug.LogWarning($"[캐릭터 아이콘 로드 실패] ID: {character.ID} / 찾으려는 경로: Assets/GameAssets/Icons/Characters/{character.Name}");
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        SetAssetAddressable(assetPath);
        Debug.Log("Character data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion
    
    #region Relic
    [MenuItem("Tools/Import Relic Data(JSON)")]
    public static void ImportRelicDataFromJson()
    {
        string path = Application.dataPath + "/GameAssets/Data/RelicData.json";

        if (!File.Exists(path))
        {
            Debug.LogError("RelicData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/GameAssets/Data/RelicDatabase.asset";

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
            relic.Icon = LoadIconSprite("Assets/GameAssets/Icons/Relics", relic.Name);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        SetAssetAddressable(assetPath);
        Debug.Log("Relic data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion

    #region Shadow
    [MenuItem("Tools/Import Shadow Data(CSV)")]
    public static void ImportShadowDataFromCSV()
    {
        string path = Application.dataPath + "/GameAssets/Data/shadow.csv";
        if (!File.Exists(path))
        {
            Debug.LogError("shadow.csv file not found at: " + path);
            return;
        }

        string[] lines = File.ReadAllLines(path, System.Text.Encoding.UTF8);
        string assetPath = "Assets/GameAssets/Data/ShadowDatabase.asset";

        ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ShadowDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new ShadowDatabase asset at: " + assetPath);
        }

        if (database.shadows == null) database.shadows = new System.Collections.Generic.List<ShadowData>();
        database.shadows.Clear();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] row = lines[i].Split(',');
            if (row.Length < 14) continue;

            ShadowData shadow = new ShadowData
            {
                ID = int.Parse(row[0]),
                Name = row[1],
                Description = row[2],
                Element = row[3].Trim() switch
                {
                    "무속성" => ElementType.None,
                    "불" => ElementType.Fire,
                    "물" => ElementType.Water,
                    "풀" => ElementType.Leaf,
                    "나무" => ElementType.Leaf,
                    "빛" => ElementType.Light,
                    "어둠" => ElementType.Dark,
                    _ => ElementType.None
                },
                AttackType = int.Parse(row[4]),
                Recognize = float.Parse(row[5]),
                MaxHealth = float.Parse(row[6]),
                AttackPower = float.Parse(row[7]),
                AttackCooldown = float.Parse(row[8]),
                AttackRange = float.Parse(row[9]),
                MaxPierce = int.Parse(row[10]),
                Defence = float.Parse(row[11]),
                MoveSpeed = float.Parse(row[12]),
                SkillID = int.TryParse(row[13], out int skillId) ? skillId : 0,
                CurrentLevel = int.Parse(row[0]) % 100,
                MaxLevel = 6,
                TargetPriority = 0
            };
            
            shadow.Icon = LoadIconSprite("Assets/GameAssets/Icons/Shadows", shadow.ID.ToString());
            database.shadows.Add(shadow);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        SetAssetAddressable(assetPath);
        Debug.Log("Shadow data imported successfully from CSV.");
        AssetDatabase.Refresh();
    }
    #endregion

    #region Upgrade
    [MenuItem("Tools/Import Upgrade Data(JSON)")]
    public static void ImportUpgradeDataFromJson()
    {
        string path = Application.dataPath + "/GameAssets/Data/UpgradeData.json";
        if (!File.Exists(path))
        {
            Debug.LogError("UpgradeData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/GameAssets/Data/UpgradeDatabase.asset";

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
            upgrade.Icon = LoadIconSprite("Assets/GameAssets/Icons/Upgrades", upgrade.Name);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        SetAssetAddressable(assetPath);
        Debug.Log("Upgrade data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion
    #region Enemy
    [MenuItem("Tools/Import Enemy Data(CSV)")]
    public static void ImportEnemyDataFromCSV()
    {
        string assetPath = "Assets/GameAssets/Data/EnemyDatabase.asset";
        EnemyDatabase database = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<EnemyDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new EnemyDatabase asset at: " + assetPath);
        }

        database.enemies.Clear();

        // 1. Load Monster CSV
        string monsterPath = Application.dataPath + "/GameAssets/Data/monster.csv";
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
                enemy.Icon = LoadIconSprite("Assets/GameAssets/Icons/Enemies", enemy.Name);
                database.enemies.Add(enemy);
            }
        }
        else
        {
            Debug.LogWarning("monster.csv file not found at: " + monsterPath);
        }

        // 2. Load Boss CSV
        string bossPath = Application.dataPath + "/GameAssets/Data/boss.csv";
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
                enemy.Icon = LoadIconSprite("Assets/GameAssets/Icons/Enemies", enemy.Name);
                database.enemies.Add(enemy);
            }
        }
        else
        {
            Debug.LogWarning("boss.csv file not found at: " + bossPath);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        SetAssetAddressable(assetPath);
        Debug.Log("Enemy data imported successfully from Monster and Boss CSVs.");
        AssetDatabase.Refresh();
    }
    #endregion

    #region Stage
    [MenuItem("Tools/Import Stage Data(JSON)")]
    public static void ImportStageDataFromJson()
    {
        string path = Application.dataPath + "/GameAssets/Data/StageData.json";

        if (!File.Exists(path))
        {
            Debug.LogError("StageData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/GameAssets/Data/StageDatabase.asset";

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
        SetAssetAddressable(assetPath);
        Debug.Log("Stage data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion

    private static void SetAssetAddressable(string assetPath)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("[DataImporter] AddressableAssetSettings를 찾을 수 없습니다. 어드레서블 자동 설정을 건너뜁니다.");
            return;
        }

        string groupName = "ScriptableObjects";
        AddressableAssetGroup group = settings.FindGroup(groupName);

        if (group == null)
        {
            Debug.Log($"[DataImporter] '{groupName}' 그룹이 없어 새로 생성합니다.");
            // 기본 번들 압축 스키마 등을 사용해 그룹 생성
            group = settings.CreateGroup(groupName, false, false, true, null);
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, true);

        if (entry != null)
        {
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            entry.SetAddress(fileName);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }
    }

    private static Sprite LoadIconSprite(string folderPath, string assetName)
    {
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:Sprite", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Equals(assetName, StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return null;
    }
}

