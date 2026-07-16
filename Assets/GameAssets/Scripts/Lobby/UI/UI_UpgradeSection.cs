using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UI_UpgradeSection : UI_Base
{
    public static event Action OnUpgradeCompleted;

    private static readonly string[] GroupOrder = { "survival", "shadow", "utility", "special" };

    [SerializeField] private float _panelPreferredHeight = 400f;
    [SerializeField] private float _groupTitleHeight = 40f;
    [SerializeField] private float _groupSpacing = 8f;
    [SerializeField] private int _groupPaddingTop = 12;
    [SerializeField] private int _groupPaddingBottom = 20;

    enum GameObjects
    {
        UpgradeScrollContent
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindObject(typeof(GameObjects));

        RefreshAll();

        return true;
    }

    public void RefreshAll()
    {
        Transform scrollContent = GetObject((int)GameObjects.UpgradeScrollContent).transform;
        ClearContainer(scrollContent);

        UserData userData = DataManager.Instance.currentUserData;
        List<UpgradeGroupData> upgradeGroups = new List<UpgradeGroupData>(DataManager.Instance.UpgradeGroupDict.Values);
        upgradeGroups.Sort((left, right) =>
        {
            int groupCompare = GetGroupOrder(left.Group).CompareTo(GetGroupOrder(right.Group));
            if (groupCompare != 0)
            {
                return groupCompare;
            }

            int stringCompare = string.Compare(left.Group, right.Group, StringComparison.OrdinalIgnoreCase);
            if (stringCompare != 0)
            {
                return stringCompare;
            }

            return left.GroupID.CompareTo(right.GroupID);
        });

        string currentGroup = string.Empty;
        Transform currentGroupContainer = null;

        foreach (UpgradeGroupData upgradeGroup in upgradeGroups)
        {
            if (upgradeGroup.Levels == null || upgradeGroup.Levels.Count == 0)
            {
                continue;
            }

            if (!string.Equals(currentGroup, upgradeGroup.Group, StringComparison.OrdinalIgnoreCase))
            {
                currentGroup = upgradeGroup.Group;
                currentGroupContainer = CreateGroupContainer(scrollContent, currentGroup);
            }

            if (currentGroupContainer == null)
            {
                continue;
            }

            int currentLevel = userData.GetUpgradeLevel(upgradeGroup.GroupID);

            GameObject go = Instantiate(ResourceManager.Instance.LoadPrefab("UI/SubItem/UI_UpgradePanel"), currentGroupContainer);
            go.name = $"UI_UpgradePanel_{upgradeGroup.GroupID}";

            UI_UpgradePanel panel = go.GetComponent<UI_UpgradePanel>();
            panel.SetInfo(upgradeGroup, currentLevel, OnUpgradeRequested, _panelPreferredHeight);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent as RectTransform);
    }

    private void ClearContainer(Transform targetContainer)
    {
        foreach (Transform child in targetContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private static int GetGroupOrder(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return int.MaxValue;
        }

        for (int i = 0; i < GroupOrder.Length; i++)
        {
            if (string.Equals(GroupOrder[i], groupName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return GroupOrder.Length;
    }

    private Transform CreateGroupContainer(Transform parent, string groupName)
    {
        GameObject groupObject = new GameObject($"UpgradeGroup_{groupName}");
        groupObject.transform.SetParent(parent, false);

        RectTransform rectTransform = groupObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup layoutGroup = groupObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = _groupSpacing;
        layoutGroup.padding = new RectOffset(0, 0, _groupPaddingTop, _groupPaddingBottom);

        ContentSizeFitter fitter = groupObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject headerObject = new GameObject("GroupTitle");
        headerObject.transform.SetParent(groupObject.transform, false);

        TMP_Text titleText = headerObject.AddComponent<TextMeshProUGUI>();
        titleText.text = string.IsNullOrWhiteSpace(groupName) ? "Group" : groupName;
        titleText.font = ResourceManager.Instance.LoadTMPFont("BMJUA_ttf SDF");
        titleText.fontSize = 100f;
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = Color.white;

        LayoutElement titleLayout = headerObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = _groupTitleHeight;
        titleLayout.minHeight = _groupTitleHeight;

        return groupObject.transform;
    }

    private void OnUpgradeRequested(int upgradeId)
    {
        UserData userData = DataManager.Instance.currentUserData;
        if (!DataManager.Instance.UpgradeGroupDict.TryGetValue(upgradeId, out UpgradeGroupData upgradeGroupData))
        {
            Debug.LogWarning($"Upgrade group not found: {upgradeId}");
            return;
        }

        int currentLevel = userData.GetUpgradeLevel(upgradeId);
        if (currentLevel >= upgradeGroupData.MaxLevel)
        {
            Debug.Log("Upgrade is already at max level.");
            return;
        }

        if (currentLevel >= upgradeGroupData.Levels.Count)
        {
            Debug.LogWarning($"No upgrade level data found for group {upgradeId} at level {currentLevel + 1}.");
            return;
        }

        UpgradeData nextUpgrade = upgradeGroupData.Levels[currentLevel];
        if (!TryConsumeUpgradeCost(userData, nextUpgrade))
        {
            return;
        }

        userData.Upgrade(upgradeId);
        //DataManager.Instance.SaveGame();

        OnUpgradeCompleted?.Invoke();

        RefreshAll();
    }

    private bool TryConsumeUpgradeCost(UserData userData, UpgradeData upgradeData)
    {
        if (string.Equals(upgradeData.CostType, "Gold", StringComparison.OrdinalIgnoreCase))
        {
            if (userData.Gold < upgradeData.CostAmount)
            {
                Debug.Log("Not enough gold for upgrade.");
                return false;
            }

            userData.Gold -= upgradeData.CostAmount;
            return true;
        }

        if (string.Equals(upgradeData.CostType, "Diamond", StringComparison.OrdinalIgnoreCase))
        {
            if (userData.Diamond < upgradeData.CostAmount)
            {
                Debug.Log("Not enough diamond for upgrade.");
                return false;
            }

            userData.Diamond -= upgradeData.CostAmount;
            return true;
        }

        Debug.LogWarning($"Unknown upgrade cost type: {upgradeData.CostType}");
        return false;
    }
}