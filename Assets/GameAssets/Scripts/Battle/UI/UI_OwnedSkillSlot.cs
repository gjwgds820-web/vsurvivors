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
                var starImg = GetImage((int)Images.Star1 + i);
                if (starImg != null)
                {
                    if (skillData.CurrentLevel >= 6)
                        starImg.color = Color.red;
                    else if (i < skillData.CurrentLevel)
                        starImg.color = Color.yellow;
                    else
                        starImg.color = Color.gray;
                }
            }

            GameObject emptyObj = GetObject((int)GameObjects.EmptyObject);
            if (emptyObj != null) emptyObj.SetActive(false);
        }
        else
        {
            GameObject emptyObj = GetObject((int)GameObjects.EmptyObject);
            if (emptyObj != null) emptyObj.SetActive(true);

            Image iconImg = GetImage((int)Images.SkillIconImage);
            if (iconImg != null) iconImg.gameObject.SetActive(false);
        }
    }
}