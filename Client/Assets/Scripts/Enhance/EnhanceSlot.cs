using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EnhanceSlot : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image      gradeBorder;
    [SerializeField] private Image      itemIcon;
    [SerializeField] private TMP_Text   enhanceText;
    [SerializeField] private GameObject selectedOverlay;

    private InventorySlotData _data;
    public  InventorySlotData Data       => _data;
    public  bool              IsEquipped { get; private set; }

    public void Setup(InventorySlotData data, bool isEquipped = false)
    {
        _data      = data;
        IsEquipped = isEquipped;
        if (gradeBorder    != null) gradeBorder.color = GradeUtil.GetColor(data.ItemInfo.Grade);
        if (itemIcon       != null) { itemIcon.sprite = data.ItemInfo.Icon; itemIcon.gameObject.SetActive(true); }
        if (enhanceText    != null) enhanceText.text  = data.EnhanceLevel > 0 ? $"+{data.EnhanceLevel}" : "";
        if (selectedOverlay != null) selectedOverlay.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedOverlay != null) selectedOverlay.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_data == null) return;
        SoundManager.Instance.PlaySFX2D("ItemSelect");
        GetComponentInParent<EnhancePanel>()?.OnSlotClicked(this);
    }
}
