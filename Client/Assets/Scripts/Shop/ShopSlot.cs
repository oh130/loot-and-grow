using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 상점 로테이션 슬롯 하나. InventorySlot과 같은 grade border + icon 구조에 가격 텍스트 추가.
/// 클릭하면 ShopPanel을 통해 ItemInfoPanel에 구매 정보를 표시한다.
/// </summary>
public class ShopSlot : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image      gradeBorder;
    [SerializeField] private Image      itemIcon;
    [SerializeField] private TMP_Text   priceText;
    [SerializeField] private GameObject selectedOverlay;

    private ShopItemResponse _shopItem;
    private ItemEntry        _itemInfo;

    public ShopItemResponse ShopItem => _shopItem;
    public ItemEntry        ItemInfo => _itemInfo;

    public void Setup(ShopItemResponse shopItem, ItemEntry itemInfo)
    {
        _shopItem = shopItem;
        _itemInfo = itemInfo;

        if (gradeBorder != null) gradeBorder.color = itemInfo != null ? GradeUtil.GetColor(itemInfo.Grade) : Color.gray;
        if (itemIcon    != null) { itemIcon.gameObject.SetActive(itemInfo != null); if (itemInfo != null) itemIcon.sprite = itemInfo.Icon; }

        int price = itemInfo != null ? itemInfo.Buy : 0;
        if (priceText != null) priceText.text = price > 0 ? $"{price} G" : "-";

        SetSelected(false);
    }

    public void Clear()
    {
        _shopItem = null;
        _itemInfo = null;
        if (gradeBorder     != null) gradeBorder.color = Color.clear;
        if (itemIcon        != null) itemIcon.gameObject.SetActive(false);
        if (priceText       != null) priceText.text = "";
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedOverlay != null) selectedOverlay.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_shopItem == null || _itemInfo == null) return;
        var panel = GetComponentInParent<ShopPanel>();
        if (panel != null) panel.OnSlotClicked(this);
        SoundManager.Instance.PlaySFX2D("ItemSelect");
    }
}
