using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_TopUIController : UI_Base
{

    enum Buttons
    {
        ProfileButton,
        EnergyButton,
        GoldButton,
        DiamondButton
    }

    enum Texts
    {
        Nickname,
        LevelText,
        ProgressText,
        EnergyAmountText,
        EnergyRegenTimer,
        GoldAmountText,
        DiamondAmountText
    }

    enum Sliders
    {
        LevelProgress
    }

    enum Images
    {
        CharacterImage
    }

    private CameraController _cameraController;
    private float _energyRechargeTimer = 300f;
    private const float ENERGY_RECHARGE_TIME = 300f;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        _cameraController = FindAnyObjectByType<CameraController>();

        BindButton(typeof(Buttons));
        BindText(typeof(Texts));
        BindImage(typeof(Images));
        Bind<Slider>(typeof(Sliders));

        Get<Button>((int)Buttons.ProfileButton).onClick.AddListener(OpenProfilePopup);
        Get<Button>((int)Buttons.EnergyButton).onClick.AddListener(() => OpenShopTab("Energy"));
        Get<Button>((int)Buttons.GoldButton).onClick.AddListener(() => OpenShopTab("Gold"));
        Get<Button>((int)Buttons.DiamondButton).onClick.AddListener(() => OpenShopTab("Diamond"));

        ProcessOfflineEnergy();
        UpdateAllUI();

        return true;
    }

    private void Update()
    {
        if (!_init) return;
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
            GetText((int)Texts.EnergyRegenTimer).text = "MAX";
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
        GetText((int)Texts.EnergyRegenTimer).text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void OnApplicationPause(bool pause)
    {
        if (!_init) return;
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
        if (!_init) return;
        UserData data = DataManager.Instance.currentUserData;
        System.DateTime adjustedTime = System.DateTime.Now.AddSeconds(-(ENERGY_RECHARGE_TIME - _energyRechargeTimer));
        data.LastEnergyUpdateTime = adjustedTime.Ticks;
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

        GetText((int)Texts.Nickname).text = data.UserName;
        GetImage((int)Images.CharacterImage).sprite = ResourceManager.Instance.LoadSprite($"UI/Portraits/Portrait_{data.PortraitIndex}");
        GetText((int)Texts.LevelText).text = data.CurrentLevel.ToString();
        float maxExp = data.CurrentLevel * 100f;
        Get<Slider>((int)Sliders.LevelProgress).value = data.CurrentExp / maxExp;
        GetText((int)Texts.ProgressText).text = $"{data.CurrentExp / maxExp * 100f:0}%";
    }

    private void UpdateEnergyUI()
    {
        UserData data = DataManager.Instance.currentUserData;
        GetText((int)Texts.EnergyAmountText).text = $"{data.CurrentEnergy}/{data.MaxEnergy}";
    }

    private void UpdateGoldUI()
    {
        UserData data = DataManager.Instance.currentUserData;
        GetText((int)Texts.GoldAmountText).text = FormatCurrency(data.Gold);
    }

    private void UpdateDiamondUI()
    {
        UserData data = DataManager.Instance.currentUserData;
        GetText((int)Texts.DiamondAmountText).text = data.Diamond.ToString();
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
        // UIManager.Instance.ShowPopup("UI_ProfilePopup");
    }

    private void OpenShopTab(string tabName)
    {
        _cameraController.OnSectionButtonClick(0);
        UI_ShopSection shopSection = FindAnyObjectByType<UI_ShopSection>();
        if (shopSection != null)
        {
            shopSection.TopUIClick(tabName);
        }
    }
}