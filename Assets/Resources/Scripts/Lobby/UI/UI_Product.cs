using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UI_Product : UI_Base
{
    enum Images
    {
        ProductImage,
    }

    enum Texts
    {
        ProductButtonText
    }

    enum Buttons
    {
        ProductButton,
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindImage(typeof(Images));
        BindText(typeof(Texts));
        BindButton(typeof(Buttons));

        GetButton((int)Buttons.ProductButton).onClick.AddListener(OnProductButtonClicked);

        return true;
    }

    private void OnProductButtonClicked()
    {
        // 상품 구매 로직 구현 (예: 결제 시스템 연동)
        Debug.Log("상품 구매 버튼 클릭됨");
    }
}