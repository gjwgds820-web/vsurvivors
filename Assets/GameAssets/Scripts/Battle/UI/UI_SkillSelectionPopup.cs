using System.Collections.Generic;
using UnityEngine;

public class UI_SkillSelectionPopup : UI_Base
{
    enum GameObjects
    {
        SelectionZone,
    }

    private GameManager _gameManager;
    private List<UI_SkillSlot> _skillSlots = new List<UI_SkillSlot>();

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        // Debug.Log("[UI_SkillSelectionPopup] Init Called.");

        BindObject(typeof(GameObjects));

        _gameManager = FindAnyObjectByType<GameManager>();

        return true;
    }

    private void OnEnable()
    {
        // Debug.Log("[UI_SkillSelectionPopup] OnEnable Called.");
        Init();
        RefreshPopup();
        // Debug.Log("[UI_SkillSelectionPopup] Refresh Completed.");
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
        UI_OwnedSkillsPanel panel = GetComponentInChildren<UI_OwnedSkillsPanel>();
        if (panel != null)
        {
            panel.RefreshUI();
        }
        else
        {
            Debug.LogWarning("[UI_SkillSelectionPopup] UI_OwnedSkillsPanel을 찾을 수 없습니다.");
        }
    }

    private void OnSkillSelected(SkillData selectedSkill)
    {
        _gameManager.OnSkillSelectionComplete(selectedSkill);
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