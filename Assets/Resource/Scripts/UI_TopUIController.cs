using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Android.Gradle.Manifest;

public class UI_TopUIController : MonoBehaviour
{
    [Header("Profile UI")]
    [SerializeField] private Button profileButton;
    [SerializeField] private TMP_Text userNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Slider expProgressBar;
    [SerializeField] private TMP_Text expProgressText;

    [Header("Energy UI")]
    [SerializeField] private Button energyButton;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text energyTimerText;

    [Header("Currency UI")]
    [SerializeField] private Button goldButton;
    [SerializeField] private TMP_Text goldText;

    [SerializeField] private Button diamondButton;
    [SerializeField] private TMP_Text diamondText;

    private float _energyRechargeTimer = 300f;
    private const float ENERGY_RECHARGE_TIME = 300f;

    private void Start()
    {
        profileButton.onClick.AddListener(OpenProfilePopup);
        energyButton.onClick.AddListener(() => OpenShopTab("Energy"));
        goldButton.onClick.AddListener(() => OpenShopTab("Gold"));
        diamondButton.onClick.AddListener(() => OpenShopTab("Diamond"));

        ProcessOfflineEnergy();
        UpdateAllUI();
    }

    private void Update()
    {
        HandleEnergyRecharge();
    }

    private void ProcessOfflineEnergy()
    {
        UserData data = DataManager.Instance.currentUserData;

        if (data.CurrentEnergy >= data.MaxEnergy)
        {
            _energyRechargeTimer = ENERGY_RECHARGE_TIME; // 타이머 초기화
            return;
        }

        if (data.LastEnergyUpdateTime == 0)
        {
            data.LastEnergyUpdateTime = System.DateTime.Now.Ticks;
            // DataManager.Instance.SaveGame();
            return;
        }

        System.DateTime lastTime = new System.DateTime(data.LastEnergyUpdateTime);
        System.TimeSpan elapsed = System.DateTime.Now - lastTime;

        int energyToRecharge = Mathf.FloorToInt((float)elapsed.TotalSeconds / ENERGY_RECHARGE_TIME);
        float leftoverSeconds = (float)elapsed.TotalSeconds % ENERGY_RECHARGE_TIME;

        if (energyToRecharge > 0)
        {
            data.CurrentEnergy += energyToRecharge;

            if (data.CurrentEnergy > data.MaxEnergy)
            {
                data.CurrentEnergy = data.MaxEnergy;
                _energyRechargeTimer = ENERGY_RECHARGE_TIME; // 타이머 초기화
            }
            else
            {
                _energyRechargeTimer = ENERGY_RECHARGE_TIME - leftoverSeconds;
            }

            data.LastEnergyUpdateTime = System.DateTime.Now.Ticks;
            // DataManager.Instance.SaveGame();
        }
        else
        {
            _energyRechargeTimer = ENERGY_RECHARGE_TIME - leftoverSeconds;
        }
    }

    private void HandleEnergyRecharge()
    {
        UserData data = DataManager.Instance.currentUserData;

        if (data.CurrentEnergy >= data.MaxEnergy)
        {
            energyTimerText.text = "MAX";
            _energyRechargeTimer = ENERGY_RECHARGE_TIME; // 타이머 초기화
            return;
        }

        _energyRechargeTimer -= Time.deltaTime;

        if (_energyRechargeTimer <= 0)
        {
            data.CurrentEnergy++;
            _energyRechargeTimer = ENERGY_RECHARGE_TIME;

            data.LastEnergyUpdateTime = System.DateTime.Now.Ticks;
            // DataManager.Instance.SaveGame();
            UpdateEnergyUI();
        }

        int minutes = Mathf.FloorToInt(_energyRechargeTimer / 60f);
        int seconds = Mathf.FloorToInt(_energyRechargeTimer % 60f);
        energyTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void OnAcationPause(bool pause)
    {
        if (pause)
        {
            UserData data = DataManager.Instance.currentUserData;
            System.DateTime adjustedTime = System.DateTime.Now.AddSeconds(-(ENERGY_RECHARGE_TIME - _energyRechargeTimer));
            data.LastEnergyUpdateTime = adjustedTime.Ticks;
            // DataManager.Instance.SaveGame();
        }        
        else
        {
            ProcessOfflineEnergy();
            UpdateAllUI();
        }
    }

    private void OnApplicationQuit()
    {
        UserData data = DataManager.Instance.currentUserData;
        System.DateTime adjustedTime = System.DateTime.Now.AddSeconds(-(ENERGY_RECHARGE_TIME - _energyRechargeTimer));
        data.LastEnergyUpdateTime = adjustedTime.Ticks;
        // DataManager.Instance.SaveGame();
    }

    public void UpdateAllUI()
    {
        UpdateProfileUI();
        UpdateEnergyUI();
        UpdateGoldUI();
        UpdateDiamondUI();
    }

    private void UpdateProfileUI()
    {
        UserData data = DataManager.Instance.currentUserData;

        userNameText.text = data.UserName;
        levelText.text = data.CurrentLevel.ToString();
        float maxExp = data.CurrentLevel * 100f;
        expProgressBar.value = data.CurrentExp / maxExp;
        expProgressText.text = $"{data.CurrentExp / maxExp * 100f:0}%";
    }

    private void UpdateEnergyUI()
    {
        UserData data = DataManager.Instance.currentUserData;
        energyText.text = $"{data.CurrentEnergy}/{data.MaxEnergy}";
    }

    private void UpdateGoldUI()
    {
        UserData data = DataManager.Instance.currentUserData;
        goldText.text = FormatCurrency(data.Gold);
    }

    private void UpdateDiamondUI()
    {
        UserData data = DataManager.Instance.currentUserData;
        diamondText.text = data.Diamond.ToString();
    }

    private string FormatCurrency(int amount)
    {
        if (amount >= 1000000000)
            return (amount / 1000000000f).ToString("0.#") + "B";
        else if (amount >= 1000000)
            return (amount / 1000000f).ToString("0.#") + "M";
        else if (amount >= 1000)
            return (amount / 1000f).ToString("0.#") + "K";
        else
            return amount.ToString();
    }

    private void OpenProfilePopup()
    {
        Debug.Log("프로필 팝업 열기");
    }

    private void OpenShopTab(string tabName)
    {
        Debug.Log($"{tabName} 상점 탭 열기");
    }
}