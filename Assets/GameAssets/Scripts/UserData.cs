using System.Collections.Generic;
using UnityEngine.Rendering;

[System.Serializable]
public class UserData
{
    private string _userName = "Player";
    private int _portraitIndex = 0;
    private int _currentLevel = 1;
    private int _gold = 0;
    private int _diamond = 0;
    private int _currentExp = 0;
    private int _currentEnergy = 0;
    private int _maxEnergy = 100;
    private long _lastEnergyUpdateTime = 0;
    private bool _isPassBought = false;
    private int _currentPassLevel = 1;
    private int _currentStage = 41010001;
    private int _selectedCharacterID = 11010101; // 기본 캐릭터 ID

    private List<int> _unlockedStages = new List<int>();
    private List<int> _unlockedCharactersID = new List<int>(); // 기본 캐릭터는 처음부터 잠금 해제
    private Dictionary<int, int> _inventory = new Dictionary<int, int>();
    private Dictionary<int, int> _upgradeLevels = new Dictionary<int, int>();
    private List<int> _upgradeGroupIds = new List<int>();
    private Dictionary<int, List<int>> _upgradeLevelIdsByGroup = new Dictionary<int, List<int>>();
    private Dictionary<int, bool> _purchasedItems = new Dictionary<int, bool>();
    private Dictionary<int, List<int>> _formationData = new Dictionary<int, List<int>>();
    private List<int> _selectedShadowsID = new List<int>(10);
    private List<int> _equippedRelicsID = new List<int>(6);

    public string UserName { get => _userName; set => _userName = value; }
    public int PortraitIndex { get => _portraitIndex; set => _portraitIndex = value; }
    public int CurrentLevel { get => _currentLevel; set => _currentLevel = value; }
    public int Gold { get => _gold; set => _gold = value; }
    public int Diamond { get => _diamond; set => _diamond = value; }
    public int CurrentExp { get => _currentExp; set => _currentExp = value; }
    public int CurrentEnergy { get => _currentEnergy; set => _currentEnergy = value; }
    public int MaxEnergy { get => _maxEnergy; set => _maxEnergy = value; }
    public long LastEnergyUpdateTime { get => _lastEnergyUpdateTime; set => _lastEnergyUpdateTime = value; }
    public bool IsPassBought { get => _isPassBought; set => _isPassBought = value; }
    public int CurrentPassLevel { get => _currentPassLevel; set => _currentPassLevel = value; }
    public int CurrentStage { get => _currentStage; set => _currentStage = value; }
    public int SelectedCharacterID { get => _selectedCharacterID; set => _selectedCharacterID = value; }
    public List<int> UnlockedStages { get => _unlockedStages; set => _unlockedStages = value; }
    public List<int> UnlockedCharactersID { get => _unlockedCharactersID; set => _unlockedCharactersID = value; }
    public Dictionary<int, int> Inventory { get => _inventory; set => _inventory = value; }
    public Dictionary<int, int> UpgradeLevels { get => _upgradeLevels; set => _upgradeLevels = value; }
    public List<int> UpgradeGroupIds { get => _upgradeGroupIds; set => _upgradeGroupIds = value; }
    public Dictionary<int, List<int>> UpgradeLevelIdsByGroup { get => _upgradeLevelIdsByGroup; set => _upgradeLevelIdsByGroup = value; }
    public Dictionary<int, bool> PurchasedItems { get => _purchasedItems; set => _purchasedItems = value; }
    public Dictionary<int, List<int>> FormationData { get => _formationData; set => _formationData = value; }
    public List<int> SelectedShadowsID { get => _selectedShadowsID; set => _selectedShadowsID = value; }
    public List<int> EquippedRelicsID { get => _equippedRelicsID; set => _equippedRelicsID = value; }

    public int GetUpgradeLevel(int upgradeGroupId)
    {
        return _upgradeLevels.TryGetValue(upgradeGroupId, out int currentLevel) ? currentLevel : 0;
    }

    public void SetUpgradeLevel(int upgradeGroupId, int level)
    {
        _upgradeLevels[upgradeGroupId] = level;
    }

    public void AddGold(int amount)
    {
        long next = (long)_gold + amount;
        if (next > int.MaxValue)
        {
            _gold = int.MaxValue;
        }
        else if (next < 0)
        {
            _gold = 0;
        }
        else
        {
            _gold = (int)next;
        }
    }

    public void AddDiamond(int amount)
    {
        long next = (long)_diamond + amount;
        if (next > int.MaxValue)
        {
            _diamond = int.MaxValue;
        }
        else if (next < 0)
        {
            _diamond = 0;
        }
        else
        {
            _diamond = (int)next;
        }
    }

    public void AddEnergy(int amount)
    {
        if (_currentEnergy + amount < _maxEnergy)
        {
            _currentEnergy += amount;
        }
        else
        {
            _currentEnergy = _maxEnergy;
        }
    }

    public void AddItem(int itemID, int quantity)
    {
        if (_inventory.ContainsKey(itemID))
        {
            _inventory[itemID] += quantity;
        }
        else
        {
            _inventory[itemID] = quantity;
        }
    }

    public void AddItem(List<int> itemIDs, int quantity)
    {
        foreach (int itemID in itemIDs)
        {
            AddItem(itemID, quantity);
        }
    }

    public void AddItem(List<int> itemIDs, List<int> quantities)
    {
        for (int i = 0; i < itemIDs.Count; i++)
        {
            AddItem(itemIDs[i], quantities[i]);
        }
    }

    public void Upgrade(int ID)
    {
        _upgradeLevels[ID] = GetUpgradeLevel(ID) + 1;
    }

    public float GetUpgradeGroupTotalEffect(int upgradeGroupId, Dictionary<int, UpgradeGroupData> upgradeGroupDict)
    {
        if (upgradeGroupDict == null || !upgradeGroupDict.TryGetValue(upgradeGroupId, out UpgradeGroupData groupData))
        {
            return 0f;
        }

        if (groupData.Levels == null || groupData.Levels.Count == 0)
        {
            return 0f;
        }

        int currentLevel = GetUpgradeLevel(upgradeGroupId);
        int cappedLevel = currentLevel < groupData.Levels.Count ? currentLevel : groupData.Levels.Count;

        float totalEffect = 0f;
        for (int i = 0; i < cappedLevel; i++)
        {
            totalEffect += groupData.Levels[i].EffectAmount;
        }

        return totalEffect;
    }

    public Dictionary<string, float> GetTotalUpgradeEffectsByType(Dictionary<int, UpgradeGroupData> upgradeGroupDict)
    {
        Dictionary<string, float> totals = new Dictionary<string, float>();
        if (upgradeGroupDict == null)
        {
            return totals;
        }

        foreach (var groupEntry in upgradeGroupDict)
        {
            UpgradeGroupData groupData = groupEntry.Value;
            if (groupData == null || groupData.Levels == null || groupData.Levels.Count == 0)
            {
                continue;
            }

            string effectType = groupData.Levels[0].EffectType;
            if (string.IsNullOrWhiteSpace(effectType))
            {
                effectType = groupData.Name;
            }

            float groupTotal = GetUpgradeGroupTotalEffect(groupEntry.Key, upgradeGroupDict);
            if (totals.TryGetValue(effectType, out float currentTotal))
            {
                totals[effectType] = currentTotal + groupTotal;
            }
            else
            {
                totals[effectType] = groupTotal;
            }
        }

        return totals;
    }
}