using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using Unity.Entities.UniversalDelegates;
using Unity.VisualScripting;

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

    [SerializeField] private GameObject _equippedCharacterSlot;
    [SerializeField] private Transform _characterContent;
    [SerializeField] private Transform _relicContent;
    [SerializeField] private Transform _shadowContent;

    private int _currentFormationIndex = 0;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindButton(typeof(Buttons));
        BindObject(typeof(GameObjects));
        GetObject((int)GameObjects.EquipmentFrame).GetComponent<UI_EquipmentFrame>().Init();
        GetObject((int)GameObjects.FormationFrame).GetComponent<UI_FormationFrame>().Init();

        GetButton((int)Buttons.CharacterButton).onClick.AddListener(() => OnInventoryTabClicked("Character"));
        GetButton((int)Buttons.RelicButton).onClick.AddListener(() => OnInventoryTabClicked("Relic"));
        GetButton((int)Buttons.ShadowButton).onClick.AddListener(() => OnInventoryTabClicked("Shadow"));
        GetButton((int)Buttons.UpgradeButton).onClick.AddListener(() => OnUpgradeButtonClicked());
        GetButton((int)Buttons.ExtractButton).onClick.AddListener(() => OnExtractButtonClicked());
        GetButton((int)Buttons.FormationButton1).onClick.AddListener(() => OnFormationButtonClicked(1));
        GetButton((int)Buttons.FormationButton2).onClick.AddListener(() => OnFormationButtonClicked(2));
        GetButton((int)Buttons.FormationButton3).onClick.AddListener(() => OnFormationButtonClicked(3));
        GetButton((int)Buttons.FormationButton4).onClick.AddListener(() => OnFormationButtonClicked(4));

        RefreshAll();

        _equippedCharacterSlot.SetActive(true);
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
        GetButton((int)Buttons.FormationButton1).image.color = Color.yellow;
        GetButton((int)Buttons.FormationButton2).image.color = Color.white;
        GetButton((int)Buttons.FormationButton3).image.color = Color.white;
        GetButton((int)Buttons.FormationButton4).image.color = Color.white;

        return true;
    }

    public void RefreshAll()
    {
        RefreshInventory();
        GetObject((int)GameObjects.EquipmentFrame).GetComponent<UI_EquipmentFrame>().RefreshView();
        GetObject((int)GameObjects.FormationFrame).GetComponent<UI_FormationFrame>().RefreshView(_currentFormationIndex); // 湲곕낯?곸쑝濡?1?몄꽦 蹂댁뿬以?
    }

    private void RefreshInventory()
    {
        UserData userData = DataManager.Instance.currentUserData;

        ClearSlots(_characterContent);
        foreach (int charID in DataManager.Instance.CharacterDict.Keys)
        {
            Sprite icon = DataManager.Instance.CharacterDict[charID].Icon;
            int quantity = userData.Inventory.ContainsKey(charID) ? userData.Inventory[charID] : 0;
            bool isEquipped = userData.SelectedCharacterID == charID;

            CreateSlot(_characterContent, charID, icon, quantity, isEquipped, OnItemSlotClicked);
        }
        ClearSlots(_relicContent);
        foreach (int relicID in DataManager.Instance.RelicDict.Keys)
        {
            Sprite icon = DataManager.Instance.RelicDict[relicID].Icon;
            int quantity = userData.Inventory.ContainsKey(relicID) ? userData.Inventory[relicID] : 0;
            bool isEquipped = userData.EquippedRelicsID.Contains(relicID);

            CreateSlot(_relicContent, relicID, icon, quantity, isEquipped, OnItemSlotClicked);
        }
        ClearSlots(_shadowContent);
        List<int> currentFormation = userData.FormationData.ContainsKey(_currentFormationIndex)
                                 ? userData.FormationData[_currentFormationIndex]
                                 : new List<int>(10); // 최대 10개의 그림자 슬롯
        foreach (int shadowID in DataManager.Instance.ShadowDict.Keys)
        {
            if (shadowID % 10 != 1) continue; // 레벨 1(끝자리가 1)인 그림자만 편성창에 표시

            Sprite icon = DataManager.Instance.ShadowDict[shadowID].Icon;
            int quantity = userData.Inventory.ContainsKey(shadowID) ? userData.Inventory[shadowID] : 0;
            bool isEquipped = currentFormation.Contains(shadowID);

            CreateSlot(_shadowContent, shadowID, icon, quantity, isEquipped, OnItemSlotClicked);
        }
    }

    private void CreateSlot(Transform parent, int itemID, Sprite icon, int quantity, bool isEquipped, Action<int> onClick)
    {
        GameObject go = Instantiate(ResourceManager.Instance.LoadPrefab("UI/Inventory/UI_InventorySlot"), parent);
        UI_InventorySlot slot = go.GetComponent<UI_InventorySlot>();
        slot.SetData(itemID, icon, quantity, isEquipped, onClick);
    }

    private void ClearSlots(Transform parent)
    {
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnItemSlotClicked(int itemID)
    {
        UserData userData = DataManager.Instance.currentUserData;

        if (!userData.Inventory.TryGetValue(itemID, out int quantity) || quantity <= 0) return;

        if (DataManager.Instance.CharacterDict.ContainsKey(itemID))
        {
            userData.SelectedCharacterID = itemID;
        }
        else if (DataManager.Instance.RelicDict.ContainsKey(itemID))
        {
            while (userData.EquippedRelicsID.Count < 6) userData.EquippedRelicsID.Add(0);

            int existingIndex = userData.EquippedRelicsID.IndexOf(itemID);
            if (existingIndex >= 0)
            {
                userData.EquippedRelicsID[existingIndex] = 0;
            }
            else
            {
                int emptyIndex = userData.EquippedRelicsID.IndexOf(0);
                if (emptyIndex >= 0)
                {
                    userData.EquippedRelicsID[emptyIndex] = itemID;
                }
            }
        }
        else if (DataManager.Instance.ShadowDict.ContainsKey(itemID))
        {
            if (!userData.FormationData.ContainsKey(_currentFormationIndex) || userData.FormationData[_currentFormationIndex] == null)
            {
                userData.FormationData[_currentFormationIndex] = new List<int>(10); // 理쒕? 10媛쒖쓽 洹몃┝???щ’
            }
            List<int> formation = userData.FormationData[_currentFormationIndex];

            if (formation.Contains(itemID))
            {
                formation.Remove(itemID);
            }
            else
            {
                if (formation.Count < 10)
                {
                    formation.Add(itemID);
                }
            }
        }

        RefreshAll();
    }

    private void OnInventoryTabClicked(string tabName)
    {
        switch (tabName)
        {
            case "Character":
                RefreshInventory();
                _equippedCharacterSlot.SetActive(true);
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
                RefreshInventory();
                _equippedCharacterSlot.SetActive(true);
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
                RefreshInventory();
                _equippedCharacterSlot.SetActive(false);
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
        _currentFormationIndex = index - 1; // 0-based index濡?蹂??
        GetObject((int)GameObjects.FormationFrame).GetComponent<UI_FormationFrame>().RefreshView(_currentFormationIndex);
        GetButton((int)Buttons.FormationButton1).image.color = index == 1 ? Color.yellow : Color.white;
        GetButton((int)Buttons.FormationButton2).image.color = index == 2 ? Color.yellow : Color.white;
        GetButton((int)Buttons.FormationButton3).image.color = index == 3 ? Color.yellow : Color.white;
        GetButton((int)Buttons.FormationButton4).image.color = index == 4 ? Color.yellow : Color.white;
    }

    private void OnEquipmentChanged()
    {
        RefreshAll();
    }
}

