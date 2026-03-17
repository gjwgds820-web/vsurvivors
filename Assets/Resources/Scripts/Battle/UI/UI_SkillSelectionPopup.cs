using System.Collections.Generic;
using UnityEngine;

public class UI_SkillSelectionPopup : UI_Base
{
    enum GameObjects
    {
        SelectionZone,
        ShadowSlotFrame,
        PassiveSlotFrame,
    }

    private GameManager _gameManager;
    private List<UI_SkillSlot> _skillSlots = new List<UI_SkillSlot>();

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindObject(typeof(GameObjects));

        _gameManager = FindAnyObjectByType<GameManager>();

        return true;
    }

    private void OnEnable()
    {
        Init();
        RefreshPopup();
    }

    private void RefreshPopup()
    {
        List<SkillData> currentOptions = DataManager.Instance.SelectedOptions;
        Transform SelectionZone = GetObject((int)GameObjects.SelectionZone).transform;

        if (_skillSlots.Count == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject go = ResourceManager.Instance.Instantiate("UI/SubItem/UI_SkillSlot", SelectionZone);
                _skillSlots.Add(go.GetComponent<UI_SkillSlot>());
            }
        }

        for (int i = 0; i < _skillSlots.Count; i++)
        {
            if (i < currentOptions.Count)
            {
                _skillSlots[i].gameObject.SetActive(true);
                _skillSlots[i].Setup(currentOptions[i], OnSkillSelected, OnSkillRerolled);
            }
            else
            {
                _skillSlots[i].gameObject.SetActive(false);
            }
        }

        UpdateOwnedSkillsUI();
    }

    private void UpdateOwnedSkillsUI()
    {
        Transform shadowGroup = GetObject((int)GameObjects.ShadowSlotFrame).transform;
        Transform passiveGroup = GetObject((int)GameObjects.PassiveSlotFrame).transform;
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

    private void OnSkillSelected(SkillData selectedSkill)
    {
        _gameManager.LevelUp(selectedSkill);

        Time.timeScale = 1f;
        UIManager.Instance.CloseTopPopup();
    }

    private void OnSkillRerolled(UI_SkillSlot targetSlot, SkillData oldSkill)
    {
        SkillData newSkill = _gameManager.RerollSingleOption(oldSkill, DataManager.Instance.SelectedOptions);
        if (newSkill.ID != oldSkill.ID)
        {
            int index = DataManager.Instance.SelectedOptions.IndexOf(oldSkill);
            if (index != -1) DataManager.Instance.SelectedOptions[index] = newSkill;

            targetSlot.Setup(newSkill, OnSkillSelected, OnSkillRerolled);
        }
    }
}