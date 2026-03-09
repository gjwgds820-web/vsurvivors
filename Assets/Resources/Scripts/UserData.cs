using System.Collections.Generic;

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
    private int _currentStage = 1;

    private List<int> _unlockedStages = new List<int>();
    private List<string> _unlockedCharacters = new List<string>();
    private Dictionary<int, int> _Inventory = new Dictionary<int, int>();
    private Dictionary<int, int> _UpgradeLevels = new Dictionary<int, int>();
    private Dictionary<int, bool> _PurchasedItems = new Dictionary<int, bool>();
    private Dictionary<int, int[]> _formationData = new Dictionary<int, int[]>();

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
    public List<int> UnlockedStages { get => _unlockedStages; set => _unlockedStages = value; }
    public List<string> UnlockedCharacters { get => _unlockedCharacters; set => _unlockedCharacters = value; }
    public Dictionary<int, int> Inventory { get => _Inventory; set => _Inventory = value; }
    public Dictionary<int, int> UpgradeLevels { get => _UpgradeLevels; set => _UpgradeLevels = value; }
    public Dictionary<int, bool> PurchasedItems { get => _PurchasedItems; set => _PurchasedItems = value; }
    public Dictionary<int, int[]> FormationData { get => _formationData; set => _formationData = value; }
}