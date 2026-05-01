using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_InventorySlot : UI_Base
{
    enum Images
    {
        IconImage,
    }

    enum GameObjects
    {
        EquippedIndicator,
    }

    enum Buttons
    {
        Button,
    }

    private int _itemID;
    private Action<int> _onClickAction;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        Bind<Image>(typeof(Images));
        Bind<GameObject>(typeof(GameObjects));
        Bind<Button>(typeof(Buttons));

        Button btn = GetButton((int)Buttons.Button);
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClickSlot);

        return true;
    }

    public void SetData(int itemID, Sprite icon, int quantity, bool isEquipped, Action<int> onClickAction)
    {
        Init();

        _itemID = itemID;
        _onClickAction = onClickAction;

        if (icon == null)
        {
            GetImage((int)Images.IconImage).color = Color.clear; // 아이콘이 없으면 투명하게 처리
        }
        else
        {
            if (quantity <= 0)
            {
                GetImage((int)Images.IconImage).color = Color.gray;
            }
            else
            {
                GetImage((int)Images.IconImage).color = Color.white;
            }
        }
        GetImage((int)Images.IconImage).sprite = icon;
        GetObject((int)GameObjects.EquippedIndicator).SetActive(isEquipped);
    }

    private void OnClickSlot()
    {
        _onClickAction?.Invoke(_itemID);
    }
}