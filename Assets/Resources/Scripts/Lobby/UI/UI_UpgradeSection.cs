using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UI_UpgradeSection : UI_Base
{
    enum Buttons
    {
        CharacterButton,
        ShadowButton
    }

    enum GameObjects
    {
        CharacterUpgradeContainer,
        ShadowUpgradeContainer
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindButton(typeof(Buttons));
        BindObject(typeof(GameObjects));

        GetButton((int)Buttons.CharacterButton).onClick.AddListener(() => OnUpgradeTabClicked("Character"));
        GetButton((int)Buttons.ShadowButton).onClick.AddListener(() => OnUpgradeTabClicked("Shadow"));

        RefreshAll();

        OnUpgradeTabClicked("Character"); // 기본적으로 캐릭터 업그레이드 탭이 활성화되도록 설정

        return true;
    }

    public void RefreshAll()
    {
        Transform charContainer = GetObject((int)GameObjects.CharacterUpgradeContainer).transform;
        Transform shadowContainer = GetObject((int)GameObjects.ShadowUpgradeContainer).transform;

        ClearContainer(charContainer);
        ClearContainer(shadowContainer);

        UserData userData = DataManager.Instance.currentUserData;

        foreach (var kvp in DataManager.Instance.UpgradeDict)
        {
            UpgradeData data = kvp.Value;
            int currentLevel = userData.UpgradeLevels.ContainsKey(data.ID) ? userData.UpgradeLevels[data.ID] : 0;

            Transform targetContainer = data.Type == 0 ? charContainer : shadowContainer;
            GameObject go = Instantiate(ResourceManager.Instance.LoadPrefab("UI/SubItem/UI_UpgradePanel"), targetContainer);
            go.name = $"UI_UpgradePanel_{data.ID}";

            UI_UpgradePanel panel = go.GetComponent<UI_UpgradePanel>();
            panel.SetInfo(data, currentLevel, OnUpgradeRequested);
        }
    }

    private void ClearContainer(Transform targetContainer)
    {
        foreach (Transform child in targetContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnUpgradeRequested(int upgradeId)
    {
        UserData userData = DataManager.Instance.currentUserData;
        UpgradeData upgradeData = DataManager.Instance.UpgradeDict[upgradeId];

        int currentLevel = userData.UpgradeLevels.ContainsKey(upgradeId) ? userData.UpgradeLevels[upgradeId] : 0;
        if (currentLevel >= upgradeData.MaxLevel)
        {
            Debug.Log("Upgrade is already at max level.");
            return;
        }

        // 업그레이드 비용 체크 및 차감 로직 추가 필요

        userData.UpgradeLevels[upgradeId] = currentLevel + 1;

        RefreshAll();
    }

    private void OnUpgradeTabClicked(string tabName)
    {
        bool isCharacter = (tabName == "Character");
        GetObject((int)GameObjects.CharacterUpgradeContainer).SetActive(isCharacter);
        GetObject((int)GameObjects.ShadowUpgradeContainer).SetActive(!isCharacter);
        GetButton((int)Buttons.CharacterButton).image.color = isCharacter ? Color.yellow : Color.white;
        GetButton((int)Buttons.ShadowButton).image.color = isCharacter ? Color.white : Color.yellow;
    }
}