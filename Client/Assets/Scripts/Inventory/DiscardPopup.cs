using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 버리기 확인 팝업.
/// - 수량 1개: 바로 확인/취소
/// - 수량 2개 이상: 수량 선택 UI 표시
/// </summary>
public class DiscardPopup : UI_CanToggle
{
    [SerializeField] private TMP_Text   itemNameText;
    [SerializeField] private GameObject quantitySelector;
    [SerializeField] private TMP_Text   quantityText;
    [SerializeField] private Button     minusButton;
    [SerializeField] private Button     plusButton;
    [SerializeField] private Button     confirmButton;
    [SerializeField] private Button     cancelButton;

    private InventorySlotData _pendingItem;
    private int _selectedQuantity;

    private void Awake()
    {
        if (minusButton   != null) minusButton.onClick.AddListener(OnClickMinus);
        if (plusButton    != null) plusButton.onClick.AddListener(OnClickPlus);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnClickConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(OnClickCancel);
        HideUI();
    }

    public void Show(InventorySlotData data)
    {
        _pendingItem      = data;
        _selectedQuantity = 1;

        if (itemNameText != null)
            itemNameText.text = $"'{data.ItemInfo.Name}' 을(를) 버리겠습니까?";

        bool hasMultiple = data.Quantity > 1;
        if (quantitySelector != null)
            quantitySelector.SetActive(hasMultiple);

        UpdateQuantityUI();
        ShowUI();
    }

    // - 버튼
    public void OnClickMinus()
    {
        if (_selectedQuantity > 1)
        {
            _selectedQuantity--;
            UpdateQuantityUI();
        }
    }

    // + 버튼
    public void OnClickPlus()
    {
        if (_pendingItem != null && _selectedQuantity < _pendingItem.Quantity)
        {
            _selectedQuantity++;
            UpdateQuantityUI();
        }
    }

    // 버리기 버튼 OnClick
    public void OnClickConfirm()
    {
        if (_pendingItem == null) return;
        InventoryManager.Instance.DiscardItem(_pendingItem, _selectedQuantity);
        _pendingItem = null;
        HideUI();
        SoundManager.Instance.PlaySFX2D("Discard", 0.65f);           
    }

    // 취소 버튼 OnClick
    public void OnClickCancel()
    {
        _pendingItem = null;
        HideUI();
    }

    public bool IsShowing => _isOpened;

    private void UpdateQuantityUI()
    {
        if (quantityText != null)
            quantityText.text = _selectedQuantity.ToString();

        if (minusButton != null)
            minusButton.interactable = _selectedQuantity > 1;
        if (plusButton != null)
            plusButton.interactable = _pendingItem != null && _selectedQuantity < _pendingItem.Quantity;
    }
}
