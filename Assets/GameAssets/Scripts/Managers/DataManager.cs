using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.IO;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.VisualScripting;
using Cysharp.Threading.Tasks;

public class DataManager : MonoBehaviour, IAsyncInitializable
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

    private bool _isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async UniTask InitAsync()
    {
        if (_isInitialized) return;

        saveFilePath = Path.Combine(Application.persistentDataPath, "UserData.json");
        
        // I/O 병목이 발생할 수 있는 유저 세이브 로드는 비동기 태스크로 처리합니다 (필요 시)
        // Unity Resources.Load를 사용하는 LoadData()는 메인 스레드에서 실행되어야 하므로 await 분리 등 확인
        
        LoadGame();
        
        // Resources 비동기 로딩을 기다릴 수 있도록 구조를 향상시킬 수도 있으나,
        // 여기선 먼저 앱 시작 시점에 메인 스레드에서 한 번 파싱하도록 유지합니다.
        await LoadDataAsync();

        _isInitialized = true;
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
        List<int> initialShadows = new List<int> { 21010101, 21020201, 22030301, 22040401, 21050501 };
        currentUserData.AddItem(initialShadows, 1); // 기본 그림자 지급
        
        // 1번 편성창(index 0)에 지급된 5개의 그림자 자동 장착
        if (currentUserData.FormationData == null) currentUserData.FormationData = new Dictionary<int, List<int>>();
        currentUserData.FormationData[0] = new List<int>(initialShadows);
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

    private async UniTask LoadDataAsync()
    {
        await LoadSkillDataAsync();
        await LoadCharacterDataAsync();
        await LoadRelicDataAsync();
        await LoadShadowDataAsync();
        await LoadUpgradeDataAsync();
        await LoadStageDataAsync();
        await LoadPortalDataAsync();
        Debug.Log($"Data Loaded: {SkillDict.Count} Skills, {CharacterDict.Count} Characters, {RelicDict.Count} Relics, {ShadowDict.Count} Shadows, {UpgradeDict.Count} Upgrades, {StageDict.Count} Stages, {PortalDict.Count} Portals");
    }

    private async UniTask LoadSkillDataAsync()
    {
        var handle = Addressables.LoadAssetAsync<SkillDatabase>("SkillDatabase");
        SkillDatabase skillDB = await handle.Task;

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

    private async UniTask LoadCharacterDataAsync()
    {
        var handle = Addressables.LoadAssetAsync<CharacterDatabase>("CharacterDatabase");
        CharacterDatabase characterDB = await handle.Task;

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

    private async UniTask LoadRelicDataAsync()
    {
        var handle = Addressables.LoadAssetAsync<RelicDatabase>("RelicDatabase");
        RelicDatabase relicDB = await handle.Task;

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

    private async UniTask LoadShadowDataAsync()
    {
        var handle = Addressables.LoadAssetAsync<ShadowDatabase>("ShadowDatabase");
        ShadowDatabase shadowDB = await handle.Task;

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

    private async UniTask LoadUpgradeDataAsync()
    {
        var handle = Addressables.LoadAssetAsync<UpgradeDatabase>("UpgradeDatabase");
        UpgradeDatabase upgradeDB = await handle.Task;

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

    private async UniTask LoadStageDataAsync()
    {
        var handle = Addressables.LoadAssetAsync<StageDatabase>("StageDatabase");
        StageDatabase stageDB = await handle.Task;
        if (stageDB != null)
        {
            foreach (StageData stage in stageDB.stages)
            {
                if (!StageDict.ContainsKey(stage.ID)) StageDict.Add(stage.ID, stage);
            }
        }
    }

    private async UniTask LoadPortalDataAsync()
    {
        var handle = Addressables.LoadAssetAsync<PortalDatabase>("PortalDatabase");
        PortalDatabase portalDB = await handle.Task;
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


