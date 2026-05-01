using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UI_ResultPopup : UI_Base
{
    enum Texts
    {
        TitleText,
        StageIndexText,
        StageNameText,
        ResultText,
        TimerText,
        CounterText,
        LevelText
    }

    enum GameObjects
    {
        ShadowFrame,
        PassiveFrame,
        RewardFrame
    }

    enum Buttons
    {
        LobbyButton,
        RetryButton
    }

    private GameManager _gameManager;
    private bool _isVictory;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindText(typeof(Texts));
        BindObject(typeof(GameObjects));
        BindButton(typeof(Buttons));

        _gameManager = FindAnyObjectByType<GameManager>();

        GetButton((int)Buttons.LobbyButton).onClick.AddListener(OnClickLobby);
        GetButton((int)Buttons.RetryButton).onClick.AddListener(OnClickRetry);

        return true;
    }

    public void Setup(bool isVictory)
    {
        Init();
        _isVictory = isVictory;
        RefreshUI();
    }

    private void RefreshUI()
    {
        GetText((int)Texts.TitleText).text = _isVictory ? "VICTORY" : "GAME OVER";
        GetText((int)Texts.ResultText).text = _isVictory ? "STAGE CLEARED" : "FAILED";
        
        // Example stage data mappings
        GetText((int)Texts.StageIndexText).text = "STAGE 1";
        GetText((int)Texts.StageNameText).text = "Dark Forest";

        // Query ECS Data
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        // Get GameDirectorData
        var directorQuery = entityManager.CreateEntityQuery(typeof(GameDirectorData));
        if (directorQuery.TryGetSingleton<GameDirectorData>(out var directorData))
        {
            int killed = directorData.KilledEnemyCount;
            float time = directorData.GlobalTimer;
            int mins = (int)(time / 60);
            int secs = (int)(time % 60);

            GetText((int)Texts.CounterText).text = killed.ToString();
            GetText((int)Texts.TimerText).text = $"{mins:00}:{secs:00}";
        }
        else
        {
            GetText((int)Texts.CounterText).text = "0";
            GetText((int)Texts.TimerText).text = "00:00";
        }

        // Get PlayerData Level
        var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerData));
        if (playerQuery.TryGetSingleton<PlayerData>(out var playerData))
        {
            GetText((int)Texts.LevelText).text = $"Lv. {playerData.Level}";
        }
        else
        {
            GetText((int)Texts.LevelText).text = "Lv. 1";
        }

        UpdateOwnedSkillsUI();
    }

    private void UpdateOwnedSkillsUI()
    {
        if (_gameManager == null) return;

        Transform shadowGroup = GetObject((int)GameObjects.ShadowFrame).transform;
        Transform passiveGroup = GetObject((int)GameObjects.PassiveFrame).transform;

        foreach (Transform child in shadowGroup) Destroy(child.gameObject);
        foreach (Transform child in passiveGroup) Destroy(child.gameObject);

        List<SkillData> currentShadows = _gameManager.CurrentShadows;
        List<SkillData> currentPassives = _gameManager.CurrentPassives;

        foreach (SkillData shadow in currentShadows)
        {
            UI_OwnedSkillSlot slot = ResourceManager.Instance.Instantiate("UI/SubItem/UI_OwnedSkillSlot", shadowGroup).GetComponent<UI_OwnedSkillSlot>();
            slot.SetSkill(shadow);
        }
        foreach (SkillData passive in currentPassives)
        {
            UI_OwnedSkillSlot slot = ResourceManager.Instance.Instantiate("UI/SubItem/UI_OwnedSkillSlot", passiveGroup).GetComponent<UI_OwnedSkillSlot>();
            slot.SetSkill(passive);
        }
    }

    private void OnClickLobby()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("LobbyScene");
    }

    private void OnClickRetry()
    {
        UIManager.Instance.CloseAllPopups();
        Time.timeScale = 1f;
        SceneManager.LoadScene("BattleScene");
    }
}