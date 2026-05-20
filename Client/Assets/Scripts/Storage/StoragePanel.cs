using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 창고 UI 패널 겸 매니저 싱글턴.
/// Shop처럼 NPC마다 다른 패널이 아니라 패널이 씬에 하나뿐이므로
/// InventoryManager처럼 Panel+Manager 역할을 하나의 스크립트로 통합.
/// slot.Data.InventoryId = storage DB id (InventoryId 필드명 재사용).
/// </summary>
public class StoragePanel : MonoBehaviour
{
    public static StoragePanel Instance { get; private set; }

    [Header("슬롯 (20칸 고정)")]
    [SerializeField] private InventorySlot[] slots;

    [Header("SO 참조")]
    [SerializeField] private ItemTableSO itemTable;

    public bool IsOpen { get; private set; }

    private InventorySlot _selectedStorageSlot;
    private bool _fetchInProgress;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── 열기/닫기 ───────────────────────────────────────────────────────

    public void Open()
    {
        IsOpen = true;
        UI_Menu.Instance?.OpenStorage(this);

        foreach (var slot in slots)
            slot.ClickOverride = s => OnStorageSlotClicked(s);

        if (!_fetchInProgress)
            StartCoroutine(FetchStorage());
    }

    /// <summary>UI_Menu에서 패널 숨기기 전 선택/오버라이드 정리용.</summary>
    public void Close()
    {
        ClearSelection();
        foreach (var slot in slots)
            slot.ClickOverride = null;
    }

    /// <summary>IsOpen 상태 해제. CloseMenu/CloseStorage에서 호출.</summary>
    public void ForceClose()
    {
        IsOpen = false;
        _selectedStorageSlot = null;
    }

    public void Refresh()
    {
        if (!_fetchInProgress)
            StartCoroutine(FetchStorage());
    }

    // ─── 창고 슬롯 선택 ──────────────────────────────────────────────────

    public void OnStorageSlotClicked(InventorySlot slot)
    {
        if (slot.Data == null) return;

        InventoryManager.Instance?.ClearInventorySelection();
        InventoryManager.Instance?.EquipmentPanel?.ClearSelection();
        QuickSlotManager.Instance?.ClearSelection();

        if (_selectedStorageSlot == slot)
        {
            _selectedStorageSlot.SetSelected(false);
            _selectedStorageSlot = null;
            InventoryManager.Instance?.ItemInfoPanel?.ShowEmpty();
            return;
        }

        if (_selectedStorageSlot != null) _selectedStorageSlot.SetSelected(false);
        _selectedStorageSlot = slot;
        slot.SetSelected(true);

        var equipDetail  = InventoryManager.Instance?.EquipDetail?.GetById(slot.Data.ItemInfo.ItemID);
        var consumDetail = InventoryManager.Instance?.ConsumDetail?.GetById(slot.Data.ItemInfo.ItemID);
        InventoryManager.Instance?.ItemInfoPanel?.ShowStorageItem(slot.Data, equipDetail, consumDetail);
    }

    public void ClearSelection()
    {
        if (_selectedStorageSlot == null) return;
        _selectedStorageSlot.SetSelected(false);
        _selectedStorageSlot = null;
    }

    // ─── 보관 (인벤토리 → 창고) ──────────────────────────────────────────

    public void Deposit(InventorySlotData invData)
    {
        StartCoroutine(DoDeposit(invData));
    }

    private IEnumerator DoDeposit(InventorySlotData invData)
    {
        yield return StartCoroutine(DataAPI.DepositItem(
            UserSession.DbUserId,
            inventoryId: invData.InventoryId,
            quantity:    invData.Quantity,
            onSuccess: _ =>
            {
                Refresh();
                InventoryManager.Instance?.Refresh();
                InventoryManager.Instance?.ItemInfoPanel?.ShowEmpty();
                SoundManager.Instance.PlaySFX2D("ItemSelect");
            },
            onError: err =>
            {
                string msg = err.Contains("꽉") ? "창고가 꽉 찼습니다." : err;
                InventoryManager.Instance?.ShowMessage(msg);
            }
        ));
    }

    // ─── 회수 (창고 → 인벤토리) ──────────────────────────────────────────

    public void Withdraw(InventorySlotData storageData)
    {
        StartCoroutine(DoWithdraw(storageData));
    }

    private IEnumerator DoWithdraw(InventorySlotData storageData)
    {
        yield return StartCoroutine(DataAPI.WithdrawItem(
            UserSession.DbUserId,
            storageId: storageData.InventoryId,
            quantity:  storageData.Quantity,
            onSuccess: _ =>
            {
                ClearSelection();
                Refresh();
                InventoryManager.Instance?.Refresh();
                InventoryManager.Instance?.ItemInfoPanel?.ShowEmpty();
                SoundManager.Instance.PlaySFX2D("ItemSelect");
            },
            onError: err =>
            {
                string msg = err.Contains("꽉") ? "가방이 꽉 찼습니다." : err;
                InventoryManager.Instance?.ShowMessage(msg);
            }
        ));
    }

    // ─── 서버 조회 ───────────────────────────────────────────────────────

    private IEnumerator FetchStorage()
    {
        if (_fetchInProgress) yield break;
        _fetchInProgress = true;

        yield return StartCoroutine(DataAPI.GetStorage(UserSession.DbUserId,
            onSuccess: res => PopulateSlots(res.items),
            onError:   err => Debug.LogError("[Storage] 조회 실패: " + err)
        ));

        _fetchInProgress = false;
    }

    private void PopulateSlots(List<StorageItemResponse> serverItems)
    {
        foreach (var slot in slots) slot.Clear();

        int i = 0;
        foreach (var item in serverItems)
        {
            if (i >= slots.Length) break;
            ItemEntry itemInfo = itemTable?.GetById(item.item_id);
            if (itemInfo == null) { i++; continue; }

            var data = new InventorySlotData(item.id, itemInfo, item.quantity, item.enhance_level);
            slots[i].Setup(data);
            i++;
        }
    }
}
