using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UI_ShopSection : UI_Base
{
    enum ShopTab
    {
        DailyShop,
        SpecialPackage,
        LimitedPackage,
        LimitedPackageDaily,
        LimitedPackageWeekly,
        LimitedPackageMonthly,
        DiamondShop,
        GoldShop,
        EnergyShop
    }

    enum Containers
    {
        DailyShopContainer,
        SpecialPackageContainer,
        LimitedPackageDailyContainer,
        LimitedPackageWeeklyContainer,
        LimitedPackageMonthlyContainer,
        DiamondShopContainer,
        GoldShopContainer,
        EnergyShopContainer
    }

    [SerializeField] private GameObject limitedContents;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindButton(typeof(ShopTab));
        BindObject(typeof(Containers));
        GetButton((int)ShopTab.DailyShop).onClick.AddListener(() => OpenShopTab("DailyShop"));
        GetButton((int)ShopTab.SpecialPackage).onClick.AddListener(() => OpenShopTab("SpecialPackage"));
        GetButton((int)ShopTab.LimitedPackage).onClick.AddListener(() => OpenShopTab("LimitedPackage"));
        GetButton((int)ShopTab.LimitedPackageDaily).onClick.AddListener(() => OpenShopTab("LimitedPackageDaily"));
        GetButton((int)ShopTab.LimitedPackageWeekly).onClick.AddListener(() => OpenShopTab("LimitedPackageWeekly"));
        GetButton((int)ShopTab.LimitedPackageMonthly).onClick.AddListener(() => OpenShopTab("LimitedPackageMonthly"));
        GetButton((int)ShopTab.DiamondShop).onClick.AddListener(() => OpenShopTab("DiamondShop"));
        GetButton((int)ShopTab.GoldShop).onClick.AddListener(() => OpenShopTab("GoldShop"));
        GetButton((int)ShopTab.EnergyShop).onClick.AddListener(() => OpenShopTab("EnergyShop"));

        GetButton((int)ShopTab.DailyShop).onClick.Invoke();

        return true;
    }

    private void OpenShopTab(string tabName)
    {
        if (!Enum.TryParse(tabName, out ShopTab selectedTab)) return;

        if (selectedTab == ShopTab.LimitedPackage)
        {
            selectedTab = ShopTab.LimitedPackageDaily;
        }

        bool isLimitedSubTab = selectedTab == ShopTab.LimitedPackageDaily ||
                                selectedTab == ShopTab.LimitedPackageWeekly ||
                                selectedTab == ShopTab.LimitedPackageMonthly;

        if (limitedContents != null)
        {
            limitedContents.SetActive(isLimitedSubTab);
        }
        
        string targetContainerName = selectedTab.ToString() + "Container";

        foreach (Containers container in Enum.GetValues(typeof(Containers)))
        {
            GameObject containerObj = GetObject((int)container);
            if (containerObj != null)
            {
                containerObj.SetActive(container.ToString() == targetContainerName);
            }
        }

        foreach (ShopTab tab in Enum.GetValues(typeof(ShopTab)))
        {
            Button btn = GetButton((int)tab);
            if (btn != null)
            {
                if (tab == ShopTab.LimitedPackage)
                {
                    btn.image.color = isLimitedSubTab ? Color.yellow : Color.white;
                }
                else
                {
                    btn.image.color = (tab == selectedTab) ? Color.yellow : Color.white;
                }
            }
        }
    }

    public void TopUIClick(string tabName)
    {
        OpenShopTab(tabName);
    }
}