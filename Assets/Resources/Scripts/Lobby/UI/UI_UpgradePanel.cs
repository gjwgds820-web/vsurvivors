using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UI_UpgradePanel : UI_Base
{
    private int _upgradeId = 0;
    private Action<int> _onClickUpgradeAction;

    enum Images
    {
        IconImage,
    }

    enum Texts
    {
        UpgradeNameText,
        TextPanelText,
    }

    enum Buttons
    {
        UpgradeButton,
    }

    enum GameObjects
    {
        UpgradeProgress
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindText(typeof(Texts));
        BindImage(typeof(Images));
        BindButton(typeof(Buttons));
        BindObject(typeof(GameObjects));

        Button btn = GetButton((int)Buttons.UpgradeButton);
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClickUpgrade);

        return true;
    }

    public void SetInfo(UpgradeData data, int currentLevel, Action<int> onClickUpgradeAction)
    {
        Init();

        _upgradeId = data.ID;
        _onClickUpgradeAction = onClickUpgradeAction;

        if (data.Icon != null) GetImage((int)Images.IconImage).sprite = data.Icon;
        GetText((int)Texts.UpgradeNameText).text = $"{data.Name} Lv.{currentLevel}/{data.MaxLevel}";
        GetText((int)Texts.TextPanelText).text = $"{data.EffectType} + {data.EffectAmount}";

        Transform progressContainer = GetObject((int)GameObjects.UpgradeProgress).transform;
        for (int i = 0; i < data.MaxLevel; i++)
        {
            Image ProgressImage = progressContainer.GetChild(i).GetComponent<Image>();
            if (ProgressImage != null)
            {
                ProgressImage.color = (i < currentLevel) ? Color.green : Color.gray;
            }
        }
    }

    private void OnClickUpgrade()
    {
        _onClickUpgradeAction?.Invoke(_upgradeId);
    }
}