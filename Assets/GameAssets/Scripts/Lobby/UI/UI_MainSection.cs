using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class UI_MainSection : UI_Base
{
    enum Buttons
    {
        PassFrame,
        StageFrame,
        MailButton,
        StageEnterButton
    }

    enum Texts
    {
        PassNameText,
        PassLevelText,
        StageIndexText,
        StageNameText,
        CostText
    }

    private int _battleEnergyCost = 10;
    private CameraController _cameraController;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindButton(typeof(Buttons));
        BindText(typeof(Texts));
        _cameraController = FindAnyObjectByType<CameraController>();

        Get<Button>((int)Buttons.PassFrame).onClick.AddListener(OnPassButtonClicked);
        Get<Button>((int)Buttons.StageFrame).onClick.AddListener(OnStageSelectButtonClicked);
        Get<Button>((int)Buttons.MailButton).onClick.AddListener(OnMailButtonClicked);
        Get<Button>((int)Buttons.StageEnterButton).onClick.AddListener(OnStartButtonClicked);

        UI_StagePopup.OnStageChanged -= UpdateUI;
        UI_StagePopup.OnStageChanged += UpdateUI;

        UpdateUI();

        return true;
    }

    private void OnDestroy()
    {
        UI_StagePopup.OnStageChanged -= UpdateUI;
    }

    public void UpdateUI()
    {
        if (!_init) return;
        UserData userData = DataManager.Instance.currentUserData;
        GetText((int)Texts.PassNameText).text = userData.IsPassBought ? "Battle Pass" : "Buy Pass";
        GetText((int)Texts.PassLevelText).text = $"Level {userData.CurrentPassLevel}";
        if (DataManager.Instance.StageDict.TryGetValue(userData.CurrentStage, out var stageData))
        {
            GetText((int)Texts.StageIndexText).text = $"Stage {stageData.Name}";
            GetText((int)Texts.StageNameText).text = $"{stageData.Name}";
        }
        else
        {
            GetText((int)Texts.StageIndexText).text = $"Stage {userData.CurrentStage}";
            GetText((int)Texts.StageNameText).text = $"Stage Name {userData.CurrentStage}";
        }
        GetText((int)Texts.CostText).text = _battleEnergyCost.ToString();
    }

    private void OnPassButtonClicked()
    {
        // UIManager.Instance.ShowPopup("UI_PassPopup");
    }

    private void OnStageSelectButtonClicked()
    {
        _cameraController.BlockInput(true);
        UIManager.Instance.ShowPopup("UI_StagePopup");
    }

    private void OnStartButtonClicked()
    {
        UserData userData = DataManager.Instance.currentUserData;
        if (userData.CurrentEnergy >= _battleEnergyCost)
        {
            userData.CurrentEnergy -= _battleEnergyCost;
            // 전투 진입 전 데이터 백업 (리트라이 시 복구용)
            DataManager.Instance.BackupUserData();
            
            // DataManager.Instance.SaveGame();
            if (VSurvivors.Managers.LoadingManager.Instance != null)
            {
                VSurvivors.Managers.LoadingManager.Instance.LoadScene("BattleScene", true);
            }
            else
            {
                SceneManager.LoadScene("BattleScene");
            }
        }
        else
        {
            UIManager.Instance.ShowPopup("UI_EnergyPopup");
        }
    }

    private void OnMailButtonClicked()
    {
        // UIManager.Instance.ShowPopup("UI_MailPopup");
    }
}