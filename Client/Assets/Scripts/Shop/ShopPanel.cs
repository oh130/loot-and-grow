using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 상점 UI 패널. NPC별로 shopType("equipment" / "consumable")을 Inspector에서 설정한다.
/// 고정 아이템은 항상 표시, 랜덤 아이템은 5분마다 갱신된다.
/// 슬롯은 shopSlotPrefab을 slotsParent 아래에 동적으로 생성한다.
/// </summary>
public class ShopPanel : MonoBehaviour
{
    [Header("상점 타입 (Inspector에서 설정)")]
    [SerializeField] public string shopType = "equipment";  // "equipment" | "consumable"

    [Header("슬롯 동적 생성")]
    [SerializeField] private ShopSlot shopSlotPrefab;
    [SerializeField] private Transform slotsParent;

    [Header("UI")]
    [SerializeField] private TMP_Text timerText;

    [Header("SO 참조")]
    [SerializeField] private ItemTableSO itemTable;

    private readonly List<ShopSlot> _activeSlots = new List<ShopSlot>();
    private List<ShopItemResponse>  _items        = new List<ShopItemResponse>();
    private DateTime                _nextRotationAt;
    private bool                    _fetchInProgress;
    private ShopSlot                _selectedSlot;

    // ─── 열기/닫기 ───────────────────────────────────────────────────────

    public void Open()
    {
        if (InventoryManager.Instance != null) InventoryManager.Instance.ShopPanel = this;
        ShopManager.Instance?.Open();
        UI_Menu.Instance?.OpenShop(this);

        if (!_fetchInProgress && DateTime.UtcNow >= _nextRotationAt)
            StartCoroutine(FetchItems());
        else if (!_fetchInProgress)
            PopulateSlots();
    }

    public void Close()
    {
        ClearSelection();
    }

    public void ForceRefresh()
    {
        _nextRotationAt = DateTime.MinValue;
    }

    public void RemoveSlot(int shopRotationId)
    {
        ShopSlot slot = _activeSlots.Find(s => s.ShopItem != null && s.ShopItem.id == shopRotationId);
        if (slot == null) return;
        if (_selectedSlot == slot) ClearSelection();
        _activeSlots.Remove(slot);
        Destroy(slot.gameObject);
        _items.RemoveAll(i => i.id == shopRotationId);
    }

    // ─── 슬롯 선택 ───────────────────────────────────────────────────────

    public void OnSlotClicked(ShopSlot slot)
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.ClearInventorySelection();
            InventoryManager.Instance.EquipmentPanel?.ClearSelection();
        }
        if (QuickSlotManager.Instance != null) QuickSlotManager.Instance.ClearSelection();

        if (_selectedSlot != null) _selectedSlot.SetSelected(false);
        _selectedSlot = slot;
        if (_selectedSlot != null) _selectedSlot.SetSelected(true);

        if (InventoryManager.Instance != null && InventoryManager.Instance.ItemInfoPanel != null)
            InventoryManager.Instance.ItemInfoPanel.ShowShopItem(slot.ShopItem, slot.ItemInfo);
    }

    public void ClearSelection()
    {
        if (_selectedSlot != null) { _selectedSlot.SetSelected(false); _selectedSlot = null; }
    }

    // ─── 메시지 ──────────────────────────────────────────────────────────

    public void ShowMessage(string msg)
    {
        if (InventoryManager.Instance != null) InventoryManager.Instance.ShowMessage(msg);
    }

    // ─── 타이머 / 자동 갱신 ─────────────────────────────────────────────

    private void Update()
    {
        if (_fetchInProgress) return;

        double remaining = (_nextRotationAt - DateTime.UtcNow).TotalSeconds;
        if (remaining <= 0)
        {
            if (timerText != null) timerText.text = "갱신 중...";
            StartCoroutine(FetchItems());
        }
        else
        {
            if (timerText != null) timerText.text = $"갱신까지 {(int)remaining}초";
        }
    }

    // ─── 서버 조회 ───────────────────────────────────────────────────────

    private IEnumerator FetchItems()
    {
        if (_fetchInProgress) yield break;
        _fetchInProgress = true;

        yield return StartCoroutine(DataAPI.GetShopItems(shopType,
            onSuccess: res =>
            {
                _items = res.items ?? new List<ShopItemResponse>();
                _nextRotationAt = DateTime.UtcNow.AddSeconds(res.seconds_remaining);
                PopulateSlots();
            },
            onError: err =>
            {
                _nextRotationAt = DateTime.UtcNow.AddSeconds(10);
                Debug.LogError("[Shop] 아이템 조회 실패: " + err);
            }
        ));

        _fetchInProgress = false;
    }

    // ─── 슬롯 동적 생성 ──────────────────────────────────────────────────

    private void PopulateSlots()
    {
        ClearSelection();

        // 기존 슬롯 제거
        foreach (var s in _activeSlots)
            if (s != null) Destroy(s.gameObject);
        _activeSlots.Clear();

        if (shopSlotPrefab == null || slotsParent == null) return;

        _items.Sort((a, b) =>
        {
            int GradeOrder(string g) => g switch { "Low" => 0, "Mid" => 1, "High" => 2, "Top" => 3, _ => 99 };
            ItemEntry ia = itemTable?.GetById(a.item_id);
            ItemEntry ib = itemTable?.GetById(b.item_id);
            return GradeOrder(ia?.Grade).CompareTo(GradeOrder(ib?.Grade));
        });

        foreach (var item in _items)
        {
            ItemEntry info = itemTable?.GetById(item.item_id);
            if (info == null) continue;

            ShopSlot slot = Instantiate(shopSlotPrefab, slotsParent, false);
            slot.Setup(item, info);
            _activeSlots.Add(slot);
        }
    }
}
