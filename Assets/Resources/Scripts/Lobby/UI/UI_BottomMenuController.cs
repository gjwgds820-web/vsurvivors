using System;
using UnityEngine;
using UnityEngine.UI;

public class BottomMenuController : UI_Base
{
    enum MenuSlots
    {
        Slot0,
        Slot1,
        Slot2,
        Slot3,
        Slot4,
    }

    enum MenuButtons
    {
        ShopButton,
        InventoryButton,
        MainButton,
        UpgradeButton,
        EventButton
    }

    enum MenuFrames
    {
        ShopButtonFrame,
        InventoryButtonFrame,
        MainButtonFrame,
        UpgradeButtonFrame,
        EventButtonFrame
    }
    [Header("References")]
    [SerializeField] private CameraController cameraController;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;
        
        BindObject(typeof(MenuSlots));
        BindImage(typeof(MenuFrames));
        BindButton(typeof(MenuButtons));

        int count = Enum.GetValues(typeof(MenuSlots)).Length;

        for (int i = 0; i < count; i++)
        {
            int index = i;
            Button button = GetButton(i);
            if (button != null)
            {
                button.onClick.AddListener(() => OnButtonClicked(index));
            }
        }

        if (cameraController != null)
        {
            cameraController.OnSectionChanged -= UpdateMenuButtonVisuals;
            cameraController.OnSectionChanged += UpdateMenuButtonVisuals;

            UpdateMenuButtonVisuals(cameraController.GetCurrentSection());
        }

        return true;
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (cameraController != null)
        {
            cameraController.OnSectionChanged -= UpdateMenuButtonVisuals;
        }
    }

    private void OnButtonClicked(int index)
    {
        // cameraController의 OnSectionButtonClick 메서드를 호출하여 카메라 이동
        if (cameraController != null)
        {
            cameraController.OnSectionButtonClick(index);
        }
    }

    private void UpdateMenuButtonVisuals(int selectedIndex)
    {
        int count = Enum.GetValues(typeof(MenuSlots)).Length;

        for (int i = 0; i < count; i++)
        {
            Image buttonImage = GetObject(i).GetComponentInChildren<Image>();
            GameObject buttonObject = GetObject(i);

            if (buttonImage != null)
            {
                buttonImage.color = (i == selectedIndex) ? selectedColor : normalColor;
            }

            if (buttonObject != null)
            {
                buttonObject.transform.localScale = (i == selectedIndex) ? Vector3.one * 1.2f : Vector3.one;
            }
        }
    }
}