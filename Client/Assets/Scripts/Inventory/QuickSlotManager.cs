using System.Collections;
using UnityEngine;

/// <summary>
/// 퀵슬롯 5개를 관리한다.
/// - HUD에 상시 노출. 인벤토리 창이 닫혀 있으면 클릭 시 즉시 소모.
/// - 인벤토리 창이 열려 있으면 클릭 시 선택 모드 (ItemInfoPanel에 설명 + 퀵슬롯 해제 버튼).
/// </summary>
public class QuickSlotManager : MonoBehaviour
{
    public static QuickSlotManager Instance { get; private set; }

    [Header("HUD 슬롯")]
    [SerializeField] private QuickSlot[] hudSlots;

    private QuickSlot _selectedSlot;

    public QuickSlot SelectedSlot => _selectedSlot;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < hudSlots.Length; i++) hudSlots[i].Init(i);
    }

    // ─── 슬롯 갱신 ───────────────────────────────────────────────────────

    public void Populate(InventorySlotData[] slotDataByIndex)
    {
        for (int i = 0; i < 5; i++)
        {
            InventorySlotData data = slotDataByIndex[i];
            if (i < hudSlots.Length)
            {
                if (data != null) hudSlots[i].Setup(data);
                else              hudSlots[i].Clear();
            }
        }
    }

    public bool HasFreeSlot()
    {
        foreach (var slot in hudSlots)
            if (slot.Data == null) return true;
        return false;
    }

    // ─── 슬롯 클릭 ───────────────────────────────────────────────────────

    public void OnSlotClicked(QuickSlot clicked)
    {
        if (clicked.Data == null) return;

        if (InventoryManager.Instance != null) InventoryManager.Instance.ClearInventorySelection();
        if (InventoryManager.Instance != null && InventoryManager.Instance.EquipmentPanel != null)
            InventoryManager.Instance.EquipmentPanel.ClearSelection();
        if (InventoryManager.Instance != null && InventoryManager.Instance.ShopPanel != null)
            InventoryManager.Instance.ShopPanel.ClearSelection();

        if (_selectedSlot == clicked)
        {
            clicked.SetSelected(false);
            SyncSelection(clicked.SlotIndex, false);
            _selectedSlot = null;
            ShowEmptyInfo();
            return;
        }

        SelectSlot(clicked);
    }

    private void SelectSlot(QuickSlot slot)
    {
        ClearSelection();
        _selectedSlot = slot;
        slot.SetSelected(true);
        SyncSelection(slot.SlotIndex, true);

        if (InventoryManager.Instance == null) return;
        ItemInfoPanel panel = InventoryManager.Instance.ItemInfoPanel;
        if (panel != null)
        {
            EquipmentDetailEntry  equipDetail  = null;
            ConsumableDetailEntry consumDetail = null;
            int itemId = slot.Data.ItemInfo.ItemID;
            if (InventoryManager.Instance.EquipDetail  != null) equipDetail  = InventoryManager.Instance.EquipDetail.GetById(itemId);
            if (InventoryManager.Instance.ConsumDetail != null) consumDetail = InventoryManager.Instance.ConsumDetail.GetById(itemId);
            panel.ShowInventoryItem(slot.Data, equipDetail, consumDetail);
        }
    }

    public void ClearSelection()
    {
        if (_selectedSlot != null)
        {
            _selectedSlot.SetSelected(false);
            SyncSelection(_selectedSlot.SlotIndex, false);
            _selectedSlot = null;
        }
    }

    private void SyncSelection(int index, bool selected)
    {
        if (index < hudSlots.Length) hudSlots[index].SetSelected(selected);
    }

    private void ShowEmptyInfo()
    {
        if (InventoryManager.Instance == null) return;
        if (InventoryManager.Instance.EquipmentPanel != null && InventoryManager.Instance.EquipmentPanel.SelectedSlot != null) return;
        if (InventoryManager.Instance.ItemInfoPanel != null)
            InventoryManager.Instance.ItemInfoPanel.ShowEmpty();
    }

    // ─── 소모 (HUD 클릭 시 즉시 소모) ───────────────────────────────────

    public void UseSlot(int slotIndex, int itemId)
    {
        if (InventoryManager.Instance == null) return;
        if (!InventoryManager.Instance.CanUseItem(itemId)) return;

        QuickSlot slot = slotIndex < hudSlots.Length ? hudSlots[slotIndex] : null;
        if (slot?.Data == null) return;

        InventoryManager.Instance.SetItemCooldown(itemId);
        StartCoroutine(DoUse(slot, itemId));
    }

    private IEnumerator DoUse(QuickSlot slot, int itemId)
    {
        int inventoryId = slot.Data.InventoryId;
        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.ConsumeItem(UserSession.DbUserId, inventoryId, 1,
            onSuccess: _ => {
                if (InventoryManager.Instance != null)
                    InventoryManager.Instance.RefreshKeepFocusByInventoryId(inventoryId);
                isSuccess = true;
            },
            onError: err => Debug.LogError($"[QuickSlot] 소모 실패: {err}")
        ));

        if(isSuccess)
        {
            ServerManager.Instance?.RequestApplyItemEffectRpc(itemId);
        }
    }

    // ─── 퀵슬롯 등록 / 해제 ─────────────────────────────────────────────

    public void RequestAssign(InventorySlotData data)
    {
        // 빈 슬롯 중 가장 앞 인덱스 탐색
        int freeIndex = -1;
        for (int i = 0; i < hudSlots.Length; i++)
        {
            if (hudSlots[i].Data == null) { freeIndex = i; break; }
        }
        if (freeIndex < 0)
        {
            Debug.LogWarning("[QuickSlot] 빈 퀵슬롯 없음");
            return;
        }
        StartCoroutine(DoAssign(data.InventoryId, freeIndex));
    }

    private IEnumerator DoAssign(int inventoryId, int slotIndex)
    {
        yield return StartCoroutine(DataAPI.AssignQuickSlot(UserSession.DbUserId, inventoryId, slotIndex,
            onSuccess: _ => InventoryManager.Instance?.RefreshKeepFocusByInventoryId(inventoryId),
            onError:   err => Debug.LogError($"[QuickSlot] 등록 실패: {err}")
        ));
    }

    public void RequestUnassign(InventorySlotData data)
    {
        StartCoroutine(DoUnassign(data.InventoryId));
    }

    private IEnumerator DoUnassign(int inventoryId)
    {
        yield return StartCoroutine(DataAPI.UnassignQuickSlot(UserSession.DbUserId, inventoryId,
            onSuccess: _ => InventoryManager.Instance?.RefreshKeepFocusByInventoryId(inventoryId),
            onError:   err => Debug.LogError($"[QuickSlot] 해제 실패: {err}")
        ));
    }
}
