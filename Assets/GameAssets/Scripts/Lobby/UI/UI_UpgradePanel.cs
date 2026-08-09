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
        LevelText,
        UpgradeNameText,
        TextPanelText,
    }

    enum Buttons
    {
        UpgradeButton,
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindText(typeof(Texts));
        BindImage(typeof(Images));
        BindButton(typeof(Buttons));

        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = 400f;
        layoutElement.preferredHeight = 400f;
        layoutElement.flexibleHeight = 0f;

        Button btn = GetButton((int)Buttons.UpgradeButton);
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClickUpgrade);

        return true;
    }

    public void SetInfo(UpgradeGroupData data, int currentLevel, Action<int> onClickUpgradeAction, float preferredHeight = 400f)
    {
        Init();

        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleHeight = 0f;
        }

        _upgradeId = data.GroupID;
        _onClickUpgradeAction = onClickUpgradeAction;

        bool isMaxLevel = currentLevel >= data.MaxLevel;
        UpgradeData nextUpgrade = !isMaxLevel && currentLevel < data.Levels.Count ? data.Levels[currentLevel] : null;
        UpgradeData currentUpgrade = currentLevel > 0 && data.Levels.Count > 0
            ? data.Levels[Mathf.Clamp(currentLevel - 1, 0, data.Levels.Count - 1)]
            : null;

        UpgradeData iconSource = nextUpgrade ?? currentUpgrade ?? (data.Levels.Count > 0 ? data.Levels[0] : null);
        if (iconSource != null && iconSource.Icon != null)
        {
            GetImage((int)Images.IconImage).sprite = iconSource.Icon;
        }

        Button upgradeButton = GetButton((int)Buttons.UpgradeButton);
        upgradeButton.gameObject.SetActive(!isMaxLevel);

        TMP_Text buttonText = upgradeButton.GetComponentInChildren<TMP_Text>(true);
        if (buttonText != null)
        {
            if (nextUpgrade != null)
            {
                buttonText.text = $"업그레이드\n<color=#00FF00>lv.{currentLevel} -> lv.{currentLevel + 1}</color>\n<color={GetCostColorTag(nextUpgrade.CostType)}>{nextUpgrade.CostType} {nextUpgrade.CostAmount}</color>";
                buttonText.color = Color.white;
            }
            else
            {
                buttonText.text = "MAX";
                buttonText.color = Color.gray;
            }
        }

        GetText((int)Texts.LevelText).text = isMaxLevel ? "Lv.MAX" : $"Lv.{currentLevel + 1}";
        GetText((int)Texts.UpgradeNameText).text = data.Name;

        float currentEffectAmount = GetCumulativeEffectAmount(data, currentLevel);
        float nextEffectAmount = nextUpgrade != null ? GetCumulativeEffectAmount(data, currentLevel + 1) : currentEffectAmount;

        if (isMaxLevel)
        {
            GetText((int)Texts.TextPanelText).text = $"적응 능력치 : {data.Name} {FormatSignedAmount(currentEffectAmount)}(MAX)";
        }
        else
        {
            GetText((int)Texts.TextPanelText).text = $"증가량 : {FormatSignedAmount(currentEffectAmount)} > {FormatSignedAmount(nextEffectAmount)}";
        }
    }

    private static float GetCumulativeEffectAmount(UpgradeGroupData data, int level)
    {
        if (data == null || data.Levels == null || data.Levels.Count == 0 || level <= 0)
        {
            return 0f;
        }

        int cappedLevel = Mathf.Min(level, data.Levels.Count);
        float sum = 0f;
        for (int i = 0; i < cappedLevel; i++)
        {
            sum += data.Levels[i].EffectAmount;
        }

        return sum;
    }

    private static string FormatSignedAmount(float value)
    {
        if (Mathf.Approximately(value, Mathf.Round(value)))
        {
            return $"+{(int)Mathf.Round(value)}";
        }

        return value > 0f ? $"+{value:0.##}" : value.ToString("0.##");
    }

    private static string GetCostColorTag(string costType)
    {
        if (string.Equals(costType, "Gold", StringComparison.OrdinalIgnoreCase))
        {
            return "#FFD700";
        }

        if (string.Equals(costType, "Diamond", StringComparison.OrdinalIgnoreCase))
        {
            return "#87CEEB";
        }

        return "#FFFFFF";
    }

    private void OnClickUpgrade()
    {
        _onClickUpgradeAction?.Invoke(_upgradeId);
    }
}