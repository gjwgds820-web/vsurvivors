using UnityEngine;
using UnityEngine.UI;

public class UI_OwnedSkillSlot : UI_Base
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
    enum GameObjects
    {
        EmptyObject,
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;
        
        BindImage(typeof(Images));
        BindObject(typeof(GameObjects));

        return true;
    }

    public void SetSkill(SkillData skillData)
    {
        Init();

        if (skillData != null)
        {
            GetImage((int)Images.SkillIconImage).gameObject.SetActive(true);
            GetImage((int)Images.SkillIconImage).sprite = skillData.Icon;

            for (int i = 0; i < 5; i++)
            {
                if (skillData.CurrentLevel == 6)
                    GetImage((int)Images.Star1 + i).color = Color.red;
                else if (i < skillData.CurrentLevel)
                    GetImage((int)Images.Star1 + i).color = Color.yellow;
                else
                    GetImage((int)Images.Star1 + i).color = Color.gray;
            }
            GetObject((int)GameObjects.EmptyObject).SetActive(false);
        }
        else
        {
            GetObject((int)GameObjects.EmptyObject).SetActive(true);
            GetImage((int)Images.SkillIconImage).gameObject.SetActive(false);
        }
    }
}