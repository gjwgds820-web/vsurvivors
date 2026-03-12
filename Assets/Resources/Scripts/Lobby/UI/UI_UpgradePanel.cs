using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UI_UpgradePanel : UI_Base
{
    [SerializeField] private int _upgradeId = 0;
    enum Texts
    {
        UpgradeText,
        UpgradeNameText,
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindText(typeof(Texts));

        GetText((int)Texts.UpgradeText).text = "Atk + 100";
        GetText((int)Texts.UpgradeNameText).text = "Upgrade Name";

        return true;
    }
}