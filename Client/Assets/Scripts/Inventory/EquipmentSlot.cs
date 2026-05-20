using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Inspector에서 SlotType을 "weapon" / "helmet" / "top" / "bottom" / "accessory" 중 하나로 설정.
/// </summary>
public class EquipmentSlot : MonoBehaviour, IPointerClickHandler
{
    [Header("슬롯 설정")]
    public string SlotType;

    [Header("UI")]
    [SerializeField] private Image      gradeBorder;
    [SerializeField] private Image      itemIcon;
    [SerializeField] private GameObject emptyLabel;
    [SerializeField] private TMP_Text   enhanceText;
    [SerializeField] private GameObject selectedOverlay;

    private EquipmentPanel _panel;

    public EquippedItemResponse CurrentEquipped { get; private set; }

    public void Init(EquipmentPanel panel) => _panel = panel;

    public void SetItem(EquippedItemResponse equipped, ItemEntry itemInfo)
    {
        CurrentEquipped = equipped;
        bool hasItem = equipped != null;

        if (emptyLabel      != null) emptyLabel.SetActive(!hasItem);
        if (itemIcon        != null) itemIcon.gameObject.SetActive(hasItem);
        if (enhanceText     != null) enhanceText.gameObject.SetActive(false);
        if (selectedOverlay != null) selectedOverlay.SetActive(false);

        if (!hasItem)
        {
            if (gradeBorder != null) gradeBorder.color = Color.white;
            return;
        }

        if (itemInfo == null) return;

        if (gradeBorder != null) gradeBorder.color = GradeUtil.GetColor(itemInfo.Grade);
        if (itemIcon    != null) { itemIcon.color = Color.white; itemIcon.sprite = itemInfo.Icon; }
        if (enhanceText != null)
        {
            enhanceText.gameObject.SetActive(equipped.enhance_level > 0);
            enhanceText.text = equipped.enhance_level > 0 ? $"+{equipped.enhance_level}" : "";
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedOverlay != null) selectedOverlay.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        SoundManager.Instance.PlaySFX2D("ItemSelect");
        _panel.OnSlotClicked(this);
    }
}
