using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UI_InventorySection : UI_Base
{
    enum Buttons
    {
        CharacterButton,
        RelicButton,
        ShadowButton,
        UpgradeButton,
        ExtractButton,
        FormationButton1,
        FormationButton2,
        FormationButton3,
        FormationButton4,
    }

    enum GameObjects
    {
        CharacterInventory,
        RelicInventory,
        ShadowInventory,
        FormationFrame,
        EquipmentFrame
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindButton(typeof(Buttons));
        BindObject(typeof(GameObjects));

        GetButton((int)Buttons.CharacterButton).onClick.AddListener(() => OnInventoryTabClicked("Character"));
        GetButton((int)Buttons.RelicButton).onClick.AddListener(() => OnInventoryTabClicked("Relic"));
        GetButton((int)Buttons.ShadowButton).onClick.AddListener(() => OnInventoryTabClicked("Shadow"));
        GetButton((int)Buttons.UpgradeButton).onClick.AddListener(() => OnUpgradeButtonClicked());
        GetButton((int)Buttons.ExtractButton).onClick.AddListener(() => OnExtractButtonClicked());
        GetButton((int)Buttons.FormationButton1).onClick.AddListener(() => OnFormationButtonClicked(1));
        GetButton((int)Buttons.FormationButton2).onClick.AddListener(() => OnFormationButtonClicked(2));
        GetButton((int)Buttons.FormationButton3).onClick.AddListener(() => OnFormationButtonClicked(3));
        GetButton((int)Buttons.FormationButton4).onClick.AddListener(() => OnFormationButtonClicked(4));

        GetObject((int)GameObjects.EquipmentFrame).SetActive(true);
        GetObject((int)GameObjects.FormationFrame).SetActive(false);
        GetObject((int)GameObjects.CharacterInventory).SetActive(true);
        GetObject((int)GameObjects.RelicInventory).SetActive(false);
        GetObject((int)GameObjects.ShadowInventory).SetActive(false);
        GetButton((int)Buttons.UpgradeButton).gameObject.SetActive(true);
        GetButton((int)Buttons.ExtractButton).gameObject.SetActive(true);
        GetButton((int)Buttons.CharacterButton).image.color = Color.yellow;
        GetButton((int)Buttons.RelicButton).image.color = Color.white;
        GetButton((int)Buttons.ShadowButton).image.color = Color.white;

        return true;
    }

    private void UpdateFormationUI(int[] formationData)
    {
        
    }

    private void OnInventoryTabClicked(string tabName)
    {
        switch (tabName)
        {
            case "Character":
                GetObject((int)GameObjects.EquipmentFrame).SetActive(true);
                GetObject((int)GameObjects.FormationFrame).SetActive(false);
                GetObject((int)GameObjects.CharacterInventory).SetActive(true);
                GetObject((int)GameObjects.RelicInventory).SetActive(false);
                GetObject((int)GameObjects.ShadowInventory).SetActive(false);
                GetButton((int)Buttons.UpgradeButton).gameObject.SetActive(true);
                GetButton((int)Buttons.ExtractButton).gameObject.SetActive(true);
                GetButton((int)Buttons.CharacterButton).image.color = Color.yellow;
                GetButton((int)Buttons.RelicButton).image.color = Color.white;
                GetButton((int)Buttons.ShadowButton).image.color = Color.white;
                break;
            case "Relic":
                GetObject((int)GameObjects.EquipmentFrame).SetActive(true);
                GetObject((int)GameObjects.FormationFrame).SetActive(false);
                GetObject((int)GameObjects.CharacterInventory).SetActive(false);
                GetObject((int)GameObjects.RelicInventory).SetActive(true);
                GetObject((int)GameObjects.ShadowInventory).SetActive(false);
                GetButton((int)Buttons.UpgradeButton).gameObject.SetActive(true);
                GetButton((int)Buttons.ExtractButton).gameObject.SetActive(true);
                GetButton((int)Buttons.RelicButton).image.color = Color.yellow;
                GetButton((int)Buttons.CharacterButton).image.color = Color.white;
                GetButton((int)Buttons.ShadowButton).image.color = Color.white;
                break;
            case "Shadow":
                GetObject((int)GameObjects.EquipmentFrame).SetActive(false);
                GetObject((int)GameObjects.FormationFrame).SetActive(true);
                GetObject((int)GameObjects.CharacterInventory).SetActive(false);
                GetObject((int)GameObjects.RelicInventory).SetActive(false);
                GetObject((int)GameObjects.ShadowInventory).SetActive(true);
                GetButton((int)Buttons.UpgradeButton).gameObject.SetActive(false);
                GetButton((int)Buttons.ExtractButton).gameObject.SetActive(false);
                GetButton((int)Buttons.ShadowButton).image.color = Color.yellow;
                GetButton((int)Buttons.CharacterButton).image.color = Color.white;
                GetButton((int)Buttons.RelicButton).image.color = Color.white;
                break;
        }
    }

    private void OnUpgradeButtonClicked()
    {
        
    }

    private void OnExtractButtonClicked()
    {

    }

    private void OnFormationButtonClicked(int index)
    {
        UserData userData = DataManager.Instance.currentUserData;
        int formationIndex = index - 1; // 0-based index
        int[] formationData = userData.FormationData.ContainsKey(formationIndex) ? userData.FormationData[formationIndex] : new int[0];
        UpdateFormationUI(formationData);
    }
}