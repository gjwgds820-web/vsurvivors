using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_PausePopup : UI_Base
{
    enum Buttons
    {
        GiveupButton,
        CancelButton,
    }

    enum Texts
    {
        TimerText,
    }

    private GameManager _gameManager;
    private EntityManager _entityManager;
    private EntityQuery _gameDirectorQuery;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _gameDirectorQuery = _entityManager.CreateEntityQuery(typeof(GameDirectorData));

        BindButton(typeof(Buttons));
        BindText(typeof(Texts));

        _gameManager = FindAnyObjectByType<GameManager>();

        GetButton((int)Buttons.GiveupButton).onClick.AddListener(OnGiveupButtonClicked);
        GetButton((int)Buttons.CancelButton).onClick.AddListener(OnCancelButtonClicked);

        return true;
    }

    private void OnEnable()
    {
        Init();
        Time.timeScale = 0f; // 일시정지
        UpdateOwnedSkillsUI();
        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        if (_gameDirectorQuery.HasSingleton<GameDirectorData>())
        {
            var directorData = _gameDirectorQuery.GetSingleton<GameDirectorData>();
            float globalTimer = directorData.GlobalTimer;
            
            int minutes = Mathf.FloorToInt(globalTimer / 60f);
            int seconds = Mathf.FloorToInt(globalTimer % 60f);
            
            GetText((int)Texts.TimerText).text = $"{minutes:00}:{seconds:00}";
        }
        else
        {
            GetText((int)Texts.TimerText).text = "00:00";
        }
    }

    private void UpdateOwnedSkillsUI()
    {
        UI_OwnedSkillsPanel panel = GetComponentInChildren<UI_OwnedSkillsPanel>();
        if (panel != null)
        {
            panel.RefreshUI();
        }
        else
        {
            Debug.LogWarning("[UI_PausePopup] UI_OwnedSkillsPanel을 찾을 수 없습니다.");
        }
    }

    private void OnGiveupButtonClicked()
    {
        // 포기 확인 팝업 띄우기
        UIManager.Instance.ShowPopup("UI_GiveupConfirmPopup");
    }

    private void OnCancelButtonClicked()
    {
        Time.timeScale = 1f; // 게임 재개
        UIManager.Instance.CloseTopPopup();
    }
}
