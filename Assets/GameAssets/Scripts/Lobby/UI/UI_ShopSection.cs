using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using Cysharp.Threading.Tasks;

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

    private CameraController _cameraController;
    private string _pendingTabName = "DailyShop";

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

        _cameraController = FindAnyObjectByType<CameraController>();
        if (_cameraController != null)
        {
            _cameraController.OnSectionChanged += HandleSectionChanged;
        }

        OpenShopTab("DailyShop");

        return true;
    }

    private void OnDestroy()
    {
        if (_cameraController != null)
        {
            _cameraController.OnSectionChanged -= HandleSectionChanged;
        }
    }

    private void HandleSectionChanged(int sectionIndex)
    {
        int shopSectionIndex = 0;

        if (sectionIndex == shopSectionIndex)
        {
            OpenShopTab(_pendingTabName);
        }
        else
        {
            _pendingTabName = "DailyShop";
        }
    }

    private void OpenShopTab(string tabName)
    {
        if (!Enum.TryParse(tabName, out ShopTab selectedTab)) return;

        // 선택된 탭을 기록해두어 화면 이동이나 갱신 시 상태 유지
        _pendingTabName = tabName;

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

        // 컨테이너 교체 후 중첩된 레이아웃 그룹들이 크기를 1차적으로 계산할 수 있도록 프레임을 넘겨 대기합니다
        if (gameObject.activeInHierarchy)
        {
            RefreshScrollDelayAsync().Forget();
        }
    }

    private async UniTaskVoid RefreshScrollDelayAsync()
    {
        ScrollRect scrollRect = GetComponentInChildren<ScrollRect>();
        /*
        if (scrollRect != null && scrollRect.content != null)
        {
            Debug.Log($"[ShopScroll Debug] 탭 클릭 직후 - Content Height: {scrollRect.content.rect.height}");
        }
        */

        // 유니티 UI 렌더 및 레이아웃 패스가 한 번 돌도록 대기 (가장 확실한 중첩 레이아웃 해결법)
        await UniTask.DelayFrame(2, PlayerLoopTiming.Update);

        // 대기하는 동안 오브젝트가 파괴되었거나 꺼졌다면 취소
        if (this == null || !gameObject.activeInHierarchy) return;

        if (scrollRect != null && scrollRect.content != null)
        {
            // 하위 오브젝트들(컨테이너들)부터 상위 오브젝트(Content) 순으로 역순 강제 갱신
            foreach (RectTransform child in scrollRect.content)
            {
                if (child.gameObject.activeSelf)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(child);
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

            /*
            float contentHeight = scrollRect.content.rect.height;
            float viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
            
            Debug.Log($"[ShopScroll Debug] Rebuild 완료 - Content Height: {contentHeight}, Viewport Height: {viewportHeight}");
            
            if (contentHeight <= viewportHeight && contentHeight > 0)
            {
                Debug.LogWarning("[ShopScroll Debug] 원인 발견: Content의 세로 길이가 Viewport(보이는 영역)보다 작거나 같습니다. 이 경우 상하 스크롤이 트리거되지 않습니다!");
            }
            if (contentHeight == 0)
            {
                Debug.LogWarning("[ShopScroll Debug] 원인 발견: Content의 세로 길이가 0입니다. 자식 컨테이너들에 ContentSizeFitter가 없거나, 내용물이 없는 상태로 인식되고 있습니다.");
            }
            */

            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void TopUIClick(string tabName)
    {
        _pendingTabName = tabName;
        OpenShopTab(tabName);
    }
}