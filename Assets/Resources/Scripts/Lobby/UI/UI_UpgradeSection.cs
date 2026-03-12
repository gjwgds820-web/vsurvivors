using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UI_UpgradeSection : UI_Base
{
    enum Buttons
    {
        CharacterButton,
        ShadowButton
    }

    enum GameObjects
    {
        CharacterUpgradeContainer,
        ShadowUpgradeContainer
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindButton(typeof(Buttons));
        BindObject(typeof(GameObjects));

        GetButton((int)Buttons.CharacterButton).onClick.AddListener(() => OnUpgradeTabClicked("Character"));
        GetButton((int)Buttons.ShadowButton).onClick.AddListener(() => OnUpgradeTabClicked("Shadow"));

        GetObject((int)GameObjects.CharacterUpgradeContainer).SetActive(true);
        GetObject((int)GameObjects.ShadowUpgradeContainer).SetActive(false);
        GetButton((int)Buttons.CharacterButton).image.color = Color.yellow;
        GetButton((int)Buttons.ShadowButton).image.color = Color.white;

        return true;
    }

    private void OnUpgradeTabClicked(string tabName)
    {
        if (tabName == "Character")
        {
            GetObject((int)GameObjects.CharacterUpgradeContainer).SetActive(true);
            GetObject((int)GameObjects.ShadowUpgradeContainer).SetActive(false);
            GetButton((int)Buttons.CharacterButton).image.color = Color.yellow;
            GetButton((int)Buttons.ShadowButton).image.color = Color.white;
        }
        else if (tabName == "Shadow")
        {
            GetObject((int)GameObjects.CharacterUpgradeContainer).SetActive(false);
            GetObject((int)GameObjects.ShadowUpgradeContainer).SetActive(true);
            GetButton((int)Buttons.CharacterButton).image.color = Color.white;
            GetButton((int)Buttons.ShadowButton).image.color = Color.yellow;
        }
    }
}