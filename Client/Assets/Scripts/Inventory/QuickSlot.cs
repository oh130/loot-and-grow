using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class QuickSlot : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image      gradeBorder;
    [SerializeField] private Image      itemIcon;
    [SerializeField] private TMP_Text   quantityText;
    [SerializeField] private GameObject selectedOverlay;
    [SerializeField] private Image      cooldownOverlay;
    [SerializeField] private TMP_Text   cooldownText;

    public int SlotIndex { get; private set; }
    public InventorySlotData Data { get; private set; }

    public void Init(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void Setup(InventorySlotData data)
    {
        Data = data;
        if (gradeBorder     != null) gradeBorder.color = GradeUtil.GetColor(data.ItemInfo.Grade);
        if (itemIcon        != null) { itemIcon.gameObject.SetActive(true); itemIcon.color = Color.white; itemIcon.sprite = data.ItemInfo.Icon; }
        if (quantityText    != null) quantityText.text = data.Quantity > 1 ? $"x{data.Quantity}" : "";
        if (selectedOverlay != null) selectedOverlay.SetActive(false);
    }

    public void Clear()
    {
        Data = null;
        if (gradeBorder     != null) gradeBorder.color = Color.clear;
        if (itemIcon        != null) itemIcon.gameObject.SetActive(false);
        if (quantityText    != null) quantityText.text = "";
        if (selectedOverlay != null) selectedOverlay.SetActive(false);
    }

    private void Update()
    {
        if (Data == null || InventoryManager.Instance == null)
        {
            SetCooldownUI(0f, 0f);
            return;
        }
        float remaining = InventoryManager.Instance.GetRemainingCooldown(Data.ItemInfo.ItemID);
        float total     = InventoryManager.Instance.GetCooldownDuration(Data.ItemInfo.ItemID);
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Data == null) return;
        SoundManager.Instance.PlaySFX2D("ItemSelect");

        bool inventoryOpen = UI_Menu.Instance != null && UI_Menu.Instance.IsInventoryOpen;

        if (inventoryOpen)
            QuickSlotManager.Instance.OnSlotClicked(this);
        else
            QuickSlotManager.Instance.UseSlot(SlotIndex, Data.ItemInfo.ItemID);
    }
}
