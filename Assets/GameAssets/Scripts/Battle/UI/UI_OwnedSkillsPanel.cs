using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_OwnedSkillsPanel : UI_Base
{
    enum GameObjects
    {
        ShadowSlotFrame,
        PassiveSlotFrame,
    }

    enum Images
    {
        OwnedElement1,
        OwnedElement2,
    }

    private GameManager _gameManager;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindObject(typeof(GameObjects));
        BindImage(typeof(Images));

        _gameManager = FindAnyObjectByType<GameManager>();

        return true;
    }

    public void RefreshUI()
    {
        Init(); // 혹시 아직 초기화되지 않았다면 진행

        Transform shadowGroup = GetObject((int)GameObjects.ShadowSlotFrame).transform;
        Transform passiveGroup = GetObject((int)GameObjects.PassiveSlotFrame).transform;

        // 1. 기존 슬롯들 파괴
        foreach (Transform child in shadowGroup) Destroy(child.gameObject);
        foreach (Transform child in passiveGroup) Destroy(child.gameObject);

        if (_gameManager == null) return;

        // 2. 그림자와 패시브 슬롯 재생성
        foreach (SkillData shadow in _gameManager.CurrentShadows)
        {
            UI_OwnedSkillSlot slot = ResourceManager.Instance.Instantiate("UI/SubItem/UI_OwnedSkillSlot", shadowGroup).GetComponent<UI_OwnedSkillSlot>();
            slot.SetSkill(shadow);
        }
        foreach (SkillData passive in _gameManager.CurrentPassives)
        {
            UI_OwnedSkillSlot slot = ResourceManager.Instance.Instantiate("UI/SubItem/UI_OwnedSkillSlot", passiveGroup).GetComponent<UI_OwnedSkillSlot>();
            slot.SetSkill(passive);
        }

        // 3. 선택된 원소(Element) 이미지 갱신
        Image elem1 = GetImage((int)Images.OwnedElement1);
        Image elem2 = GetImage((int)Images.OwnedElement2);

        elem1.gameObject.SetActive(false);
        elem2.gameObject.SetActive(false);

        for (int i = 0; i < _gameManager.SelectedElements.Count; i++)
        {
            int elementID = _gameManager.SelectedElements[i];
            Sprite sprite = ResourceManager.Instance.LoadSprite($"Icons/Elements/Element_{elementID}");
            
            if (i == 0)
            {
                elem1.sprite = sprite;
                elem1.color = Color.white;
                elem1.gameObject.SetActive(true);
            }
            else if (i == 1)
            {
                elem2.sprite = sprite;
                elem2.color = Color.white;
                elem2.gameObject.SetActive(true);
            }
        }
    }
}
