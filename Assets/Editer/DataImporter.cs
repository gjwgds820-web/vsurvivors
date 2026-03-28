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
            shadow.Icon = Resources.Load<Sprite>("Icons/Shadows/" + shadow.Name);
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
    [MenuItem("Tools/Import Enemy Data(JSON)")]
    public static void ImportEnemyDataFromJson()
    {
        string path = Application.dataPath + "/Resources/Data/EnemyData.json";
        if (!File.Exists(path))
        {
            Debug.LogError("EnemyData.json file not found at: " + path);
            return;
        }

        string jsonContent = File.ReadAllText(path);
        string assetPath = "Assets/Resources/Data/EnemyDatabase.asset";

        EnemyDatabase database = AssetDatabase.LoadAssetAtPath<EnemyDatabase>(assetPath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<EnemyDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            Debug.Log("Created new EnemyDatabase asset at: " + assetPath);
        }

        JsonUtility.FromJsonOverwrite(jsonContent, database);
        foreach (EnemyData enemy in database.enemies)
        {
            enemy.Icon = Resources.Load<Sprite>("Icons/Enemies/" + enemy.Name);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Enemy data imported successfully from JSON.");
        AssetDatabase.Refresh();
    }
    #endregion
}