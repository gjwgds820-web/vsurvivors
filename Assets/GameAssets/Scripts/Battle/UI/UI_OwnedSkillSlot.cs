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

        // 슬롯 크기(스케일) 변화에 맞춰 별(Star) UI도 비율에 따라 동적으로 위치와 크기가 맞춰지도록 Anchor 조정
        for (int i = 0; i < 5; i++)
        {
            var starImg = GetImage((int)Images.Star1 + i);
            if (starImg != null)
            {
                RectTransform rt = starImg.rectTransform;
                float step = 1f / 5f;
                
                // 가로를 5등분하고, 세로는 부모 기준 하단 25% 영역만큼 차지하도록 비율(Anchor)로 고정합니다.
                rt.anchorMin = new Vector2(i * step, 0f);
                rt.anchorMax = new Vector2((i + 1) * step, 0.25f);
                
                // 고정 픽셀 여백을 0으로 만들어버려서 부모 크기에 완벽히 비례하게 만듭니다.
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        return true;
    }

    public void SetSkill(SkillData skillData)
    {
        Init();

        if (skillData != null)
        {
            Sprite iconSprite = skillData.Icon;
            if (iconSprite == null)
            {
                int baseID = (skillData.ID / 10) * 10 + 1;
                iconSprite = ResourceManager.Instance.LoadSprite($"Icons/Skills/{baseID}");
            }

            GetImage((int)Images.SkillIconImage).gameObject.SetActive(true);
            GetImage((int)Images.SkillIconImage).sprite = iconSprite;

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