using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using Cysharp.Threading.Tasks;

public class UI_Product : UI_Base
{
    private enum CurrencyType
    {
        Gold,
        Diamond
    }

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

    private struct ShopProductRow
    {
        public CurrencyType RewardCurrency;
        public int RewardAmount;
        public string PriceType;
        public int PriceAmount;
    }

    [Header("Temporary Table Binding")]
    [Tooltip("ShopDatabase row index (0-based). 0: Gold 상품, 1: Diamond 상품")]
    [SerializeField] private int productRowIndex = 0;
    [SerializeField] private string toastAddress = "UI_ToastMessage";

    private CurrencyType _rewardCurrency;
    private int _rewardAmount;
    private string _paymentHint;
    private string _priceType;
    private int _priceAmount;

    public override bool Init()
    {
        if (base.Init() == false)
            return false;

        BindImage(typeof(Images));
        BindText(typeof(Texts));
        BindButton(typeof(Buttons));

        ResolveProductFromCsv();
        RefreshButtonText();

        GetButton((int)Buttons.ProductButton).onClick.AddListener(OnProductButtonClicked);

        return true;
    }

    private void OnProductButtonClicked()
    {
        HandlePurchaseAsync().Forget();
    }

    private async UniTaskVoid HandlePurchaseAsync()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsToastShowing)
        {
            return;
        }

        if (DataManager.Instance == null || DataManager.Instance.currentUserData == null)
        {
            Debug.LogError("[UI_Product] DataManager or UserData is null.");
            return;
        }

        UserData userData = DataManager.Instance.currentUserData;
        if (_rewardCurrency == CurrencyType.Gold)
        {
            userData.AddGold(_rewardAmount);
        }
        else
        {
            userData.AddDiamond(_rewardAmount);
        }

        UI_TopUIController topUI = FindAnyObjectByType<UI_TopUIController>();
        if (topUI != null)
        {
            topUI.UpdateAllUI();
        }

        string rewardLabel = _rewardCurrency == CurrencyType.Gold ? "Gold" : "Diamond";
        string toastMessage = $"{rewardLabel} +{_rewardAmount:N0} (Test Purchase)";

        if (UIManager.Instance != null)
        {
            await UIManager.Instance.ShowToastAsync(toastAddress, toastMessage);
        }
    }

    private void ResolveProductFromCsv()
    {
        if (DataManager.Instance == null || DataManager.Instance.ShopProducts == null || DataManager.Instance.ShopProducts.Count == 0)
        {
            ApplyFallbackProduct();
            return;
        }

        int rowIndex = Mathf.Clamp(productRowIndex, 0, DataManager.Instance.ShopProducts.Count - 1);
        ShopData source = DataManager.Instance.ShopProducts[rowIndex];
        if (source == null)
        {
            ApplyFallbackProduct();
            return;
        }

        ShopProductRow row = new ShopProductRow
        {
            RewardCurrency = ParseCurrency(source.RewardType, CurrencyType.Gold),
            RewardAmount = source.RewardAmount,
            PriceType = source.PriceType,
            PriceAmount = source.PriceAmount
        };

        _rewardCurrency = row.RewardCurrency;
        _rewardAmount = Mathf.Max(0, row.RewardAmount);
        _priceType = string.IsNullOrWhiteSpace(row.PriceType) ? "None" : row.PriceType;
        _priceAmount = Mathf.Max(0, row.PriceAmount);
        _paymentHint = BuildPaymentHint(_priceType, _priceAmount);
    }

    private void ApplyFallbackProduct()
    {
        _rewardCurrency = CurrencyType.Gold;
        _rewardAmount = 10000;
        _priceType = "Diamond";
        _priceAmount = 100;
        _paymentHint = BuildPaymentHint(_priceType, _priceAmount);
    }

    private static CurrencyType ParseCurrency(string value, CurrencyType fallback)
    {
        if (!string.IsNullOrWhiteSpace(value) && System.Enum.TryParse(value.Trim(), true, out CurrencyType parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static string BuildPaymentHint(string priceType, int priceAmount)
    {
        if (string.Equals(priceType, "Diamond", System.StringComparison.OrdinalIgnoreCase))
        {
            return $"Cost {priceAmount:N0} Diamond (planned)";
        }

        if (string.Equals(priceType, "Cash", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(priceType, "IAP", System.StringComparison.OrdinalIgnoreCase))
        {
            return priceAmount <= 0
                ? "Cost 0 (planned)"
                : $"Cost {priceAmount:N0} Cash/IAP (planned)";
        }

        return priceAmount <= 0
            ? "Cost 0 (planned)"
            : $"Cost {priceAmount:N0} {priceType} (planned)";
    }

    private void RefreshButtonText()
    {
        TMP_Text buttonText = GetText((int)Texts.ProductButtonText);
        if (buttonText == null)
        {
            return;
        }

        string rewardLabel = _rewardCurrency == CurrencyType.Gold ? "Gold" : "Diamond";
        buttonText.text = $"Get {_rewardAmount:N0} {rewardLabel}\n{_paymentHint}";
    }
}