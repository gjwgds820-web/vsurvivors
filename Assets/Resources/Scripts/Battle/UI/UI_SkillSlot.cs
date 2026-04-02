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
        GetText((int)Texts.SkillDescriptionText).text = skillData.Description;
        
        // 아이콘이 비어있다면 리소스 폴더에서 동적으로 로드 시도
        Sprite iconSprite = skillData.Icon;
        if (iconSprite == null)
        {
            if (skillData.Type == SkillType.Shadow)
            {
                iconSprite = ResourceManager.Instance.LoadSprite($"Icons/Shadows/Shadow_{skillData.ID - 40000000}");
            }
            else // Passive 등 다른 타입일 경우
            {
                // 패시브 아이콘 경로에 맞게 확장하세요
                iconSprite = ResourceManager.Instance.LoadSprite($"Icons/Passives/Passive_{skillData.ID}");
            }
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