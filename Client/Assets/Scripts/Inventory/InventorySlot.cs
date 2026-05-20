using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image      gradeBorder;
    [SerializeField] private Image      itemIcon;
    [SerializeField] private TMP_Text   quantityText;
    [SerializeField] private GameObject selectedOverlay;
    [SerializeField] private Image      cooldownOverlay;
    [SerializeField] private TMP_Text   cooldownText;

    private InventorySlotData _data;

    public InventorySlotData Data => _data;

    public void Setup(InventorySlotData data)
    {
        _data = data;
        if (gradeBorder  != null) gradeBorder.color = GradeUtil.GetColor(data.ItemInfo.Grade);
        if (itemIcon     != null) { itemIcon.gameObject.SetActive(true); itemIcon.color = Color.white; itemIcon.sprite = data.ItemInfo.Icon; }
        if (quantityText != null)
        {
            if (data.ItemInfo.ItemType == "Equipment")
                quantityText.text = data.EnhanceLevel > 0 ? $"+{data.EnhanceLevel}" : "";
            else
                quantityText.text = data.Quantity > 1 ? $"x{data.Quantity}" : "";
        }
        if (selectedOverlay != null) selectedOverlay.SetActive(false);
    }

    public void Clear()
    {
        _data = null;
        if (gradeBorder     != null) gradeBorder.color = Color.clear;
        if (itemIcon        != null) itemIcon.gameObject.SetActive(false);
        if (quantityText    != null) quantityText.text = "";
        if (selectedOverlay != null) selectedOverlay.SetActive(false);
    }

    private void Update()
    {
        if (_data == null || _data.ItemInfo.ItemType != "Consumable" || InventoryManager.Instance == null)
        {
            SetCooldownUI(0f, 0f);
            return;
        }
        float remaining = InventoryManager.Instance.GetRemainingCooldown(_data.ItemInfo.ItemID);
        float total     = InventoryManager.Instance.GetCooldownDuration(_data.ItemInfo.ItemID);
        SetCooldownUI(remaining, total);
    }

    private void SetCooldownUI(float remaining, float total)
    {
        bool onCooldown = remaining > 0f;
        if (cooldownOverlay != null)
        {
            cooldownOverlay.gameObject.SetActive(onCooldown);
            cooldownOverlay.fillAmount = total > 0f ? remaining / total : 0f;
        }
        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(onCooldown);
            if (onCooldown) cooldownText.text = remaining.ToString("F1");
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedOverlay != null) selectedOverlay.SetActive(selected);
    }

    // StoragePanel이 설정하면 InventoryManager 대신 해당 핸들러로 라우팅
    public System.Action<InventorySlot> ClickOverride;

    public void OnPointerClick(PointerEventData eventData)
    {
        SoundManager.Instance.PlaySFX2D("ItemSelect");
        if (ClickOverride != null)
            ClickOverride.Invoke(this);
        else
            InventoryManager.Instance.OnSlotClicked(this);
    }
}
