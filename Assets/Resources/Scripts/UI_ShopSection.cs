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

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindButton(typeof(ShopTab));
        BindObject(typeof(Containers));

        Get<Button>((int)ShopTab.DailyShop).onClick.AddListener(() => OpenShopTab("DailyShop"));
        Get<Button>((int)ShopTab.SpecialPackage).onClick.AddListener(() => OpenShopTab("SpecialPackage"));
        Get<Button>((int)ShopTab.LimitedPackage).onClick.AddListener(() => OpenShopTab("LimitedPackage"));
        Get<Button>((int)ShopTab.LimitedPackageDaily).onClick.AddListener(() => OpenShopTab("LimitedPackageDaily"));
        Get<Button>((int)ShopTab.LimitedPackageWeekly).onClick.AddListener(() => OpenShopTab("LimitedPackageWeekly"));
        Get<Button>((int)ShopTab.LimitedPackageMonthly).onClick.AddListener(() => OpenShopTab("LimitedPackageMonthly"));
        Get<Button>((int)ShopTab.DiamondShop).onClick.AddListener(() => OpenShopTab("DiamondShop"));
        Get<Button>((int)ShopTab.GoldShop).onClick.AddListener(() => OpenShopTab("GoldShop"));
        Get<Button>((int)ShopTab.EnergyShop).onClick.AddListener(() => OpenShopTab("EnergyShop"));

        return true;
    }

    private void OpenShopTab(string tabName)
    {
        int count = Enum.GetValues(typeof(Containers)).Length;

        for (int i = 0; i < count; i++)
        {
            GameObject container = GetObject(i);
            if (container != null)
            {
                container.SetActive(container.name == $"{tabName}Container");
            }
        }
    }

    public void TopUIClick(string tabName)
    {
        OpenShopTab(tabName);
    }
}