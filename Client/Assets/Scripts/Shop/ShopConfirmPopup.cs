using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopConfirmPopup : UI_CanToggle
{
    [SerializeField] private TMP_Text   titleText;
    [SerializeField] private TMP_Text   priceText;
    [SerializeField] private GameObject quantitySelector;
    [SerializeField] private TMP_Text   quantityText;
    [SerializeField] private Button     minusButton;
    [SerializeField] private Button     plusButton;
    [SerializeField] private Button     confirmButton;
    [SerializeField] private Button     cancelButton;

    private enum Mode { Buy, Sell }

    private Mode              _mode;
    private ShopItemResponse  _shopItem;
    private ItemEntry         _shopItemInfo;
    private InventorySlotData _invData;
    private int               _unitPrice;
    private int               _maxQuantity;
    private int               _selectedQuantity;

    private void Awake()
    {
        if (minusButton   != null) minusButton.onClick.AddListener(OnClickMinus);
        if (plusButton    != null) plusButton.onClick.AddListener(OnClickPlus);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnClickConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(OnClickCancel);
        HideUI();
    }

    // ─── 열기 ────────────────────────────────────────────────────────────

    public void ShowBuy(ShopItemResponse shopItem, ItemEntry itemInfo)
    {
        _mode         = Mode.Buy;
        _shopItem     = shopItem;
        _shopItemInfo = itemInfo;
        _unitPrice    = itemInfo.Buy;
        // 랜덤 아이템은 수량 1 고정
        _maxQuantity  = shopItem.is_random ? 1 : 99;
        _selectedQuantity = 1;

        if (titleText        != null) titleText.text = $"'{itemInfo.Name}' 구매";
        if (quantitySelector != null) quantitySelector.SetActive(_maxQuantity > 1);

        UpdateUI();
        ShowUI();
    }

    public void ShowSell(InventorySlotData data)
    {
        _mode      = Mode.Sell;
        _invData   = data;
        _unitPrice = data.ItemInfo.Sell;
        // 장비는 개별 슬롯이므로 수량 1 고정
        _maxQuantity = data.ItemInfo.ItemType == "Equipment" ? 1 : data.Quantity;
        _selectedQuantity = 1;

        if (titleText        != null) titleText.text = $"'{data.ItemInfo.Name}' 판매";
        if (quantitySelector != null) quantitySelector.SetActive(_maxQuantity > 1);

        UpdateUI();
        ShowUI();
    }

    // ─── 수량 버튼 ────────────────────────────────────────────────────────

    public void OnClickMinus()
    {
        if (_selectedQuantity > 1) { _selectedQuantity--; UpdateUI(); }
    }

    public void OnClickPlus()
    {
        if (_selectedQuantity < _maxQuantity) { _selectedQuantity++; UpdateUI(); }
    }

    // ─── 확인/취소 ───────────────────────────────────────────────────────

    public void OnClickConfirm()
    {
        if (_mode == Mode.Buy)
            ShopManager.Instance?.BuyItem(_shopItem, _shopItemInfo, _selectedQuantity);
        else
            ShopManager.Instance?.SellItem(_invData, _selectedQuantity);

        _shopItem = null; _invData = null;
        HideUI();
    }

    public void OnClickCancel()
    {
        _shopItem = null; _invData = null;
        HideUI();
    }

    // ─── 내부 ─────────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (quantityText != null) quantityText.text = _selectedQuantity.ToString();
        if (priceText    != null) priceText.text    = $"{(_unitPrice * _selectedQuantity):N0} G";
        if (minusButton  != null) minusButton.interactable = _selectedQuantity > 1;
        if (plusButton   != null) plusButton.interactable  = _selectedQuantity < _maxQuantity;
    }
}
