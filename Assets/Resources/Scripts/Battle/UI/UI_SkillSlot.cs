using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UI_SkillSlot : UI_Base
{
    enum Images
    {
        SkillIconImage,
        Star1,
        Star2,
        Star3,
        Star4,
        Star5,
    }

    enum Texts
    {
        SkillNameText,
        SkillDescriptionText,
    }

    enum Buttons
    {
        UI_SkillSlot,
        RerollButton,
    }

    private SkillData _currentSkill;
    private Action<SkillData> _onSelect;
    private Action<UI_SkillSlot, SkillData> _onReroll;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;
        
        BindImage(typeof(Images));
        BindText(typeof(Texts));
        BindButton(typeof(Buttons));

        return true;
    }


    public void Setup(SkillData skillData, Action<SkillData> onSelect, Action<UI_SkillSlot, SkillData> onReroll)
    {
        Init();

        _currentSkill = skillData;
        _onSelect = onSelect;
        _onReroll = onReroll;

        GetText((int)Texts.SkillNameText).text = skillData.Name;
        // 기획상 추가된 DisplayDescription을 우선 노출, 없으면 기존 Description 노출
        GetText((int)Texts.SkillDescriptionText).text = string.IsNullOrEmpty(skillData.DisplayDescription) ? skillData.Description : skillData.DisplayDescription;
        
        // 아이콘이 비어있다면 리소스 폴더에서 동적으로 로드 시도
        Sprite iconSprite = skillData.Icon;
        if (iconSprite == null)
        {
            iconSprite = ResourceManager.Instance.LoadSprite($"Icons/Skills/{skillData.ID}");
        }
        
        GetImage((int)Images.SkillIconImage).sprite = iconSprite;
        GetImage((int)Images.SkillIconImage).gameObject.SetActive(iconSprite != null);

        for (int i = 0; i < 5; i++)
        {
            if (skillData.CurrentLevel >= 6)
                GetImage((int)Images.Star1 + i).color = Color.red;
            else if (i < skillData.CurrentLevel)
                GetImage((int)Images.Star1 + i).color = Color.yellow;
            else
                GetImage((int)Images.Star1 + i).color = Color.gray;
        }

        GetButton((int)Buttons.UI_SkillSlot).onClick.RemoveAllListeners();
        GetButton((int)Buttons.UI_SkillSlot).onClick.AddListener(() => _onSelect?.Invoke(_currentSkill));
        GetButton((int)Buttons.RerollButton).onClick.RemoveAllListeners();
        GetButton((int)Buttons.RerollButton).onClick.AddListener(() => _onReroll?.Invoke(this, _currentSkill));
    }
}