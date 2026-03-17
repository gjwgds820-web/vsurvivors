using UnityEngine;
using UnityEditor;
using System.IO;
using System;

public class DataImporter
{
    #region Skill
    [MenuItem("Tools/Import Skill Data(JSON)")]
    public static void ImportSkillDataFromJson()
    {
        string path = Application.dataPath + "/Resources/Data/SkillData.json";

        if (!File.Exists(path))
        {
            Debug.LogError("SkillData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/Resources/Data/SkillDatabase.asset";

        SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<SkillDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new SkillDatabase asset at: " + assetPath);
        }

        JsonUtility.FromJsonOverwrite(jsonContent, database);
        foreach (SkillData skill in database.skills)
        {
            skill.Icon = Resources.Load<Sprite>("Icons/Skills/" + skill.Name);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Skill data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Import Skill Data(CSV)")]
    public static void ImportSkillDataFromCSV()
    {
        string path = Application.dataPath + "/Resources/Data/SkillData.csv";
        
        if (!File.Exists(path))
        {
            Debug.LogError("SkillData.csv file not found at: " + path);
            return;
        }

        string assetPath = "Assets/Resources/Data/SkillDatabase.asset";
        SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<SkillDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new SkillDatabase asset at: " + assetPath);
        }

        database.skills.Clear();
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');

            try
            {
                SkillData skill = new SkillData
                {
                    ID = int.Parse(values[0]),
                    Type = (SkillType)Enum.Parse(typeof(SkillType), values[1]),
                    Name = values[2],
                    Description = values[3],
                    MaxLevel = int.Parse(values[4]),
                    CurrentLevel = int.Parse(values[5]),
                    Icon = Resources.Load<Sprite>("Icons/Skills/" + values[2])
                };
                database.skills.Add(skill);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line {i + 1} in CSV: {e.Message}");
            }
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

    [MenuItem("Tools/Import Character Data(CSV)")]
    public static void ImportCharacterDataFromCSV()
    {
        string path = Application.dataPath + "/Resources/Data/CharacterData.csv";
        
        if (!File.Exists(path))
        {
            Debug.LogError("CharacterData.csv file not found at: " + path);
            return;
        }

        string assetPath = "Assets/Resources/Data/CharacterDatabase.asset";
        CharacterDatabase database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<CharacterDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new CharacterDatabase asset at: " + assetPath);
        }

        database.characters.Clear();
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');

            try
            {
                CharacterData character = new CharacterData
                {
                    ID = int.Parse(values[0]),
                    Name = values[1],
                    Description = values[2],
                    MaxLevel = int.Parse(values[3]),
                    CurrentLevel = int.Parse(values[4]),
                    Icon = Resources.Load<Sprite>("Icons/Characters/" + values[1])
                };
                database.characters.Add(character);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line {i + 1} in CSV: {e.Message}");
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Character data imported successfully from CSV.");
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

    [MenuItem("Tools/Import Relic Data(CSV)")]
    public static void ImportRelicDataFromCSV()
    {
        string path = Application.dataPath + "/Resources/Data/RelicData.csv";
        
        if (!File.Exists(path))
        {
            Debug.LogError("RelicData.csv file not found at: " + path);
            return;
        }

        string assetPath = "Assets/Resources/Data/RelicDatabase.asset";
        RelicDatabase database = AssetDatabase.LoadAssetAtPath<RelicDatabase>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<RelicDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new RelicDatabase asset at: " + assetPath);
        }

        database.relics.Clear();
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');

            try
            {
                RelicData relic = new RelicData
                {
                    ID = int.Parse(values[0]),
                    Name = values[1],
                    Description = values[2],
                    MaxLevel = int.Parse(values[3]),
                    CurrentLevel = int.Parse(values[4]),
                    Icon = Resources.Load<Sprite>("Icons/Relics/" + values[1])
                };
                database.relics.Add(relic);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line {i + 1} in CSV: {e.Message}");
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Relic data imported successfully from CSV.");
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
            shadow.Icon = Resources.Load<Sprite>("Icons/Shadows/" + shadow.Name);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Shadow data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Import Shadow Data(CSV)")]
    public static void ImportShadowDataFromCSV()
    {
        string path = Application.dataPath + "/Resources/Data/ShadowData.csv";
        
        if (!File.Exists(path))
        {
            Debug.LogError("ShadowData.csv file not found at: " + path);
            return;
        }

        string assetPath = "Assets/Resources/Data/ShadowDatabase.asset";
        ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ShadowDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new ShadowDatabase asset at: " + assetPath);
        }

        database.shadows.Clear();
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');

            try
            {
                ShadowData shadow = new ShadowData
                {
                    ID = int.Parse(values[0]),
                    Name = values[1],
                    Description = values[2],
                    MaxLevel = int.Parse(values[3]),
                    CurrentLevel = int.Parse(values[4]),
                    Icon = Resources.Load<Sprite>("Icons/Shadows/" + values[1])
                };
                database.shadows.Add(shadow);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line {i + 1} in CSV: {e.Message}");
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Shadow data imported successfully from CSV.");
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

    [MenuItem("Tools/Import Upgrade Data(CSV)")]
    public static void ImportUpgradeDataFromCSV()
    {
        string path = Application.dataPath + "/Resources/Data/UpgradeData.csv";
        
        if (!File.Exists(path))
        {
            Debug.LogError("UpgradeData.csv file not found at: " + path);
            return;
        }

        string assetPath = "Assets/Resources/Data/UpgradeDatabase.asset";
        UpgradeDatabase database = AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(assetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<UpgradeDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new UpgradeDatabase asset at: " + assetPath);
        }

        database.upgrades.Clear();
        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');

            try
            {
                UpgradeData upgrade = new UpgradeData
                {
                    ID = int.Parse(values[0]),
                    Name = values[1],
                    Description = values[2],
                    MaxLevel = int.Parse(values[3]),
                    CurrentLevel = int.Parse(values[4]),
                    CostType = values[5],
                    CostAmount = int.Parse(values[6]),
                    EffectType = values[7],
                    EffectAmount = float.Parse(values[8]),
                    Icon = Resources.Load<Sprite>("Icons/Upgrades/" + values[1])
                };
                database.upgrades.Add(upgrade);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing line {i + 1} in CSV: {e.Message}");
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Upgrade data imported successfully from CSV.");
        AssetDatabase.Refresh();
    }
    #endregion
}