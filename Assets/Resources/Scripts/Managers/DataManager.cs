using UnityEngine;
using System.IO;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.VisualScripting;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    public UserData currentUserData;
    private String saveFilePath;
    public Dictionary<int, SkillData> SkillDict { get; private set; } = new Dictionary<int, SkillData>();
    public Dictionary<int, CharacterData> CharacterDict { get; private set; } = new Dictionary<int, CharacterData>();
    public Dictionary<int, RelicData> RelicDict { get; private set; } = new Dictionary<int, RelicData>();
    public Dictionary<int, ShadowData> ShadowDict { get; private set; } = new Dictionary<int, ShadowData>();
    public Dictionary<int, UpgradeData> UpgradeDict { get; private set; } = new Dictionary<int, UpgradeData>();
    public Dictionary<int, StageData> StageDict { get; private set; } = new Dictionary<int, StageData>();
    public Dictionary<int, PortalData> PortalDict { get; private set; } = new Dictionary<int, PortalData>();

    public List<SkillData> SelectedOptions = new List<SkillData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "UserData.json");
            LoadGame();
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region Save and Load
    public void SaveGame()
    {
        try
        {
            string jsonData = JsonConvert.SerializeObject(currentUserData, Formatting.Indented);
            File.WriteAllText(saveFilePath, jsonData);
            // Debug.Log("Game saved successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save game: " + e.Message);
        }
    }

    public void LoadGame()
    {
        if (File.Exists(saveFilePath))
        {
            try
            {
                string jsonData = File.ReadAllText(saveFilePath);
                currentUserData = JsonConvert.DeserializeObject<UserData>(jsonData);
                //Debug.Log("Game loaded successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load game: " + e.Message);
            }
        }
        else
        {
            // Debug.Log("No save file found. Starting a new game.");
            currentUserData = new UserData();
            InitializeUserData();
            // SaveGame();
        }
    }

    private void InitializeUserData()
    {
        currentUserData.UnlockedStages = new List<int> { 41010001, 41010002, 41010003 };
        currentUserData.UnlockedCharactersID = new List<int> { 11010101, 11010102 };
        currentUserData.CurrentEnergy = currentUserData.MaxEnergy;
        currentUserData.LastEnergyUpdateTime = DateTime.Now.Ticks;
        currentUserData.CurrentStage = 41010001;
        currentUserData.SelectedCharacterID = 11010101;
        currentUserData.Gold = 10000;
        currentUserData.Diamond = 500;
        foreach (var shadow in ShadowDict.Values)
        {
            currentUserData.Inventory[shadow.ID] = 0;
        }
        foreach (var relic in RelicDict.Values)
        {
            currentUserData.Inventory[relic.ID] = 0;
        }
        foreach (var character in CharacterDict.Values)
        {
            currentUserData.Inventory[character.ID] = 0;
        }
        currentUserData.AddItem(new List<int> {11010101, 11010102}, 1); // 기본 캐릭터 지급
        currentUserData.AddItem(new List<int> { 30000001, 30000002, 30000003 }, new List<int> { 1, 1, 1 }); // 기본 유물 지급
        currentUserData.AddItem(new List<int> { 21050101, 21020201, 21020301, 21020401, 21020501, 21020601, 21020701, 21030801 }, 1); // 기본 그림자 지급
    }

    public List<int> GetFormation(int formationIndex)
    {
        List<int> formation = currentUserData.FormationData[formationIndex];
        return formation;
    }

    private void OnApplicationQuit()
    {
        // SaveGame();
    }
    #endregion

    #region Database

    private void LoadData()
    {
        LoadSkillData();
        LoadCharacterData();
        LoadRelicData();
        LoadShadowData();
        LoadUpgradeData();
        LoadStageData();
        LoadPortalData();
        Debug.Log($"Data Loaded: {SkillDict.Count} Skills, {CharacterDict.Count} Characters, {RelicDict.Count} Relics, {ShadowDict.Count} Shadows, {UpgradeDict.Count} Upgrades, {StageDict.Count} Stages, {PortalDict.Count} Portals");
    }

    private void LoadSkillData()
    {
        SkillDatabase skillDB = Resources.Load<SkillDatabase>("Data/SkillDatabase");

        if (skillDB != null)
        {
            foreach (SkillData skill in skillDB.skills)
            {
                if (!SkillDict.ContainsKey(skill.ID))
                {
                    SkillDict.Add(skill.ID, skill);
                }
                else
                {
                    Debug.LogWarning($"Duplicate Skill ID found: {skill.ID}");
                }
            }
        }
    }

    private void LoadCharacterData()
    {
        CharacterDatabase characterDB = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");

        if (characterDB != null)
        {
            foreach (CharacterData character in characterDB.characters)
            {
                if (!CharacterDict.ContainsKey(character.ID))
                {
                    CharacterDict.Add(character.ID, character);
                }
                else
                {
                    Debug.LogWarning($"Duplicate Character ID found: {character.ID}");
                }
            }
        }
    }

    private void LoadRelicData()
    {
        RelicDatabase relicDB = Resources.Load<RelicDatabase>("Data/RelicDatabase");

        if (relicDB != null)
        {
            foreach (RelicData relic in relicDB.relics)
            {
                if (!RelicDict.ContainsKey(relic.ID))
                {
                    RelicDict.Add(relic.ID, relic);
                }
                else
                {
                    Debug.LogWarning($"Duplicate Relic ID found: {relic.ID}");
                }
            }
        }
    }

    private void LoadShadowData()
    {
        ShadowDatabase shadowDB = Resources.Load<ShadowDatabase>("Data/ShadowDatabase");

        if (shadowDB != null)
        {
            foreach (ShadowData shadow in shadowDB.shadows)
            {
                if (!ShadowDict.ContainsKey(shadow.ID))
                {
                    ShadowDict.Add(shadow.ID, shadow);
                }
                else
                {
                    Debug.LogWarning($"Duplicate Shadow ID found: {shadow.ID}");
                }
            }
        }
    }

    private void LoadUpgradeData()
    {
        UpgradeDatabase upgradeDB = Resources.Load<UpgradeDatabase>("Data/UpgradeDatabase");

        if (upgradeDB != null)
        {
            foreach (UpgradeData upgrade in upgradeDB.upgrades)
            {
                if (!UpgradeDict.ContainsKey(upgrade.ID))
                {
                    UpgradeDict.Add(upgrade.ID, upgrade);
                }
                else
                {
                    Debug.LogWarning($"Duplicate Upgrade ID found: {upgrade.ID}");
                }
            }
        }
    }

    public List<SkillData> GetAllPassiveSkills()
    {
        List<SkillData> passiveSkills = new List<SkillData>();
        foreach (var skill in SkillDict.Values)
        {
            if (skill.Type == SkillType.Passive)
            {
                passiveSkills.Add(skill);
            }
        }
        return passiveSkills;
    }

    private void LoadStageData()
    {
        StageDatabase stageDB = Resources.Load<StageDatabase>("Data/StageDatabase");
        if (stageDB != null)
        {
            foreach (StageData stage in stageDB.stages)
            {
                if (!StageDict.ContainsKey(stage.ID)) StageDict.Add(stage.ID, stage);
            }
        }
    }

    private void LoadPortalData()
    {
        PortalDatabase portalDB = Resources.Load<PortalDatabase>("Data/PortalDatabase");
        if (portalDB != null)
        {
            foreach (PortalData portal in portalDB.portals)
            {
                if (!PortalDict.ContainsKey(portal.ID)) PortalDict.Add(portal.ID, portal);
            }
        }
    }
    #endregion
}


