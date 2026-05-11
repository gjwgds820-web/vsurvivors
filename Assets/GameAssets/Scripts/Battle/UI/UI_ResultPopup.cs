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
        
        GetText((int)Texts.StageIndexText).text = "STAGE 1";
        GetText((int)Texts.StageNameText).text = "Dark Forest";

        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
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

        if (DataManager.Instance != null)
        {
            DataManager.Instance.ClearBackup();
            // 보통 로비로 나갈때는 획득 재화를 유지하고 세이브하는 등을 여기서 처리합니다.
            // DataManager.Instance.SaveGame(); 
        }
        
        // 로비로 나갈때도 ECS 월드 충돌을 방지하기위해 초기화
        World.DisposeAllWorlds();
        DefaultWorldInitialization.Initialize("Default World", false);

        VSurvivors.Managers.LoadingManager.Instance.LoadScene("LobbyScene");
    }

    private void OnClickRetry()
    {
        UIManager.Instance.CloseAllPopups();
        Time.timeScale = 1f;

        // 사망 시 리트라이를 위해, 전투 중에 획득한 경험치, 골드 등을 모두 리셋 필요
        // 게임 진입 시 찍어두었던 스냅샷(백업) 데이터로 복원하여 임시 데이터만 깔끔하게 날림
        if (DataManager.Instance != null)
        {
            DataManager.Instance.RestoreUserDataFromBackup();
        }

        // ECS 월드 제거 및 재생성 (무한 로딩 및 잔존 엔티티 제거)
        World.DisposeAllWorlds();
        DefaultWorldInitialization.Initialize("Default World", false);

        VSurvivors.Managers.LoadingManager.Instance.LoadScene("BattleScene", true);
    }
}
