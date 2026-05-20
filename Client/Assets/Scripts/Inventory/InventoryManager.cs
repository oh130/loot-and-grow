using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 인벤토리 20칸 고정 슬롯을 관리한다.
/// OnEnable 시 서버에서 아이템 목록을 받아 슬롯을 채운다.
/// quickslot_index >= 0인 아이템은 QuickSlotManager로 분리해서 전달한다.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("SO 참조")]
    [SerializeField] private ItemTableSO         itemTable;
    [SerializeField] private EquipmentDetailSO   equipDetail;
    [SerializeField] private ConsumableDetailSO  consumableDetail;
    [SerializeField] private EnhanceTableSO      enhanceTable;

    public ItemTableSO         ItemTable     => itemTable;
    public EquipmentDetailSO   EquipDetail   => equipDetail;
    public ConsumableDetailSO  ConsumDetail  => consumableDetail;
    public EnhanceTableSO      EnhanceTable  => enhanceTable;

    [Header("UI 참조")]
    [SerializeField] private InventorySlot[]  slots;
    [SerializeField] private DiscardPopup     discardPopup;
    [SerializeField] private ShopConfirmPopup shopConfirmPopup;
    [SerializeField] private ItemInfoPanel    itemInfoPanel;
    [SerializeField] private TMP_Text         messageText;
    [SerializeField] private TMP_Text         goldText;

    public DiscardPopup     DiscardPopup     => discardPopup;
    public ShopConfirmPopup ShopConfirmPopup => shopConfirmPopup;

    public EquipmentPanel EquipmentPanel { get; private set; }
    public ShopPanel      ShopPanel      { get; set; }
    public ItemInfoPanel  ItemInfoPanel  => itemInfoPanel;

    private InventorySlot _selectedSlot;
    private Coroutine     _fullMessageCoroutine;

    private int _reselectInventoryId = -1;
    private int _reselectItemId      = -1;
    private int _reselectEnhance     = 0;

    private bool _fetchInProgress = false;

    private readonly Dictionary<int, float> _itemCooldowns = new Dictionary<int, float>();

    private static readonly WaitForSeconds WaitTwoSeconds = new WaitForSeconds(2f);

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        EquipmentPanel   = FindAnyObjectByType<EquipmentPanel>(FindObjectsInactive.Include);
        ShopPanel        = FindAnyObjectByType<ShopPanel>(FindObjectsInactive.Include);
        if (discardPopup     == null) discardPopup     = FindAnyObjectByType<DiscardPopup>(FindObjectsInactive.Include);
        if (shopConfirmPopup == null) shopConfirmPopup = FindAnyObjectByType<ShopConfirmPopup>(FindObjectsInactive.Include);
        if (messageText != null) messageText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        StartCoroutine(FetchInventory());
        StartCoroutine(FetchGold());
    }

    public void Refresh()
    {
        StartCoroutine(FetchInventory());
        StartCoroutine(FetchGold());
    }

    public void RefreshKeepFocusByInventoryId(int inventoryId)
    {
        _reselectInventoryId = inventoryId;
        StartCoroutine(FetchInventory());
    }

    public void RefreshKeepFocusByItemId(int itemId, int enhanceLevel)
    {
        _reselectItemId  = itemId;
        _reselectEnhance = enhanceLevel;
        StartCoroutine(FetchInventory());
    }

    private IEnumerator FetchInventory()
    {
        if (!UserSession.IsLoggedIn) yield break;
        if (_fetchInProgress) yield break;

        _fetchInProgress = true;
        yield return StartCoroutine(DataAPI.GetInventory(UserSession.DbUserId,
            onSuccess: res => PopulateSlots(res.items),
            onError:   err => Debug.LogError($"[Inventory] 요청 실패: {err}")
        ));
        _fetchInProgress = false;
    }

    private void PopulateSlots(List<InventoryItemResponse> serverItems)
    {
        foreach (var slot in slots) slot.Clear();

        InventorySlot     reselectTarget   = null;
        InventorySlotData reselectQuickData = null;
        InventorySlotData[] quickslotData  = new InventorySlotData[5];
        int invIndex = 0;

        foreach (var item in serverItems)
        {
            ItemEntry itemInfo = itemTable.GetById(item.item_id);
            if (itemInfo == null)
            {
                Debug.LogWarning($"[Inventory] item_id {item.item_id} 을 ItemTableSO에서 찾을 수 없음");
                continue;
            }

            var data = new InventorySlotData(item.id, itemInfo, item.quantity, item.enhance_level, item.quickslot_index);

            if (item.quickslot_index >= 0 && item.quickslot_index < 5)
            {
                quickslotData[item.quickslot_index] = data;
                if (_reselectInventoryId >= 0 && item.id == _reselectInventoryId)
                    reselectQuickData = data;
            }
            else
            {
                if (invIndex < slots.Length)
                {
                    slots[invIndex].Setup(data);
                    if (_reselectInventoryId >= 0 && item.id == _reselectInventoryId)
                        reselectTarget = slots[invIndex];
                    else if (_reselectItemId >= 0 && item.item_id == _reselectItemId && item.enhance_level == _reselectEnhance)
                        reselectTarget = slots[invIndex];
                    invIndex++;
                }
            }
        }

        if (QuickSlotManager.Instance != null)
            QuickSlotManager.Instance.Populate(quickslotData);

        _reselectInventoryId = -1;
        _reselectItemId      = -1;
        _reselectEnhance     = 0;
        _selectedSlot        = null;

        if (reselectTarget != null)
        {
            _selectedSlot = reselectTarget;
            reselectTarget.SetSelected(true);
            if (itemInfoPanel != null)
                itemInfoPanel.ShowInventoryItem(reselectTarget.Data, GetDetail(reselectTarget.Data), GetConsumDetail(reselectTarget.Data));
        }
        else if (reselectQuickData != null)
        {
            if (itemInfoPanel != null)
                itemInfoPanel.ShowInventoryItem(reselectQuickData, GetDetail(reselectQuickData), GetConsumDetail(reselectQuickData));
        }
        else if (EquipmentPanel?.SelectedSlot == null && QuickSlotManager.Instance?.SelectedSlot == null)
        {
            if (itemInfoPanel != null) itemInfoPanel.ShowEmpty();
        }
    }

    // ─── 슬롯 클릭 ───────────────────────────────────────────────────────

    public void OnSlotClicked(InventorySlot slot)
    {
        if (slot.Data == null) return;

        if (EquipmentPanel != null)            EquipmentPanel.ClearSelection();
        if (QuickSlotManager.Instance != null) QuickSlotManager.Instance.ClearSelection();
        if (ShopPanel != null)                 ShopPanel.ClearSelection();
        StoragePanel.Instance?.ClearSelection();

        if (_selectedSlot == slot)
        {
            slot.SetSelected(false);
            _selectedSlot = null;
            if (itemInfoPanel != null) itemInfoPanel.ShowEmpty();
            return;
        }

        if (_selectedSlot != null) _selectedSlot.SetSelected(false);
        _selectedSlot = slot;
        slot.SetSelected(true);
        if (itemInfoPanel != null) itemInfoPanel.ShowInventoryItem(slot.Data, GetDetail(slot.Data), GetConsumDetail(slot.Data));
    }

    public void ClearInventorySelection()
    {
        if (_selectedSlot != null) _selectedSlot.SetSelected(false);
        _selectedSlot = null;
    }

    // ─── 장착 / 사용 ─────────────────────────────────────────────────────

    public void RequestUseFromInfo(InventorySlotData data)
    {
        if (!CanUseItem(data.ItemInfo.ItemID)) return;

        int inventoryId = data.InventoryId;
        SetItemCooldown(data.ItemInfo.ItemID);
        StartCoroutine(DataAPI.ConsumeItem(UserSession.DbUserId, data.InventoryId, 1,
            onSuccess: _ => {
                RefreshKeepFocusByInventoryId(inventoryId);
                ServerManager.Instance?.RequestApplyItemEffectRpc(data.ItemInfo.ItemID);
            },
            onError:   err => Debug.LogError($"[Inventory] 아이템 사용 실패: {err}")
        ));
    }

    // ─── 픽업 (외부 코드에서 아이템 획득 시 호출) ───────────────────────

    /// <summary>
    /// 장비 아이템 획득. 인벤토리가 꽉 찼으면 메시지 표시.
    /// </summary>
    /// <param name="itemId">ItemTableSO의 ItemID 값</param>
    /// <param name="enhanceLevel">강화 레벨 (기본 0)</param>
    /// <example>
    /// yield return StartCoroutine(InventoryManager.Instance.PickupEquipment(3000, 0));
    /// </example>
    public IEnumerator PickupEquipment(ulong itemObjectId, int itemId, int enhanceLevel = 0)
    {
        bool isSuccess = false;
        yield return StartCoroutine(DataAPI.AddEquipment(UserSession.DbUserId, itemId, enhanceLevel,
            onSuccess: _ => {
                    Refresh();
                    isSuccess = true;
                },
            onError: err => {
                if (err.Contains("가방이 꽉 찼습니다")) ShowMessage("가방이 꽉 찼습니다.");
                else Debug.LogError($"[Inventory] 장비 획득 실패: {err}");
            }
        ));

        if(isSuccess)
        {
            ItemEntry itemEntry = itemTable.GetById(itemId);
            ServerManager.Instance?.EndAddItemRpc(itemObjectId, true, $"{itemEntry.Name}(+{enhanceLevel})을(를) 획득했습니다.");
        }
        else
        {
            ServerManager.Instance?.EndAddItemRpc(itemObjectId, false, "");
        }
    }

    /// <summary>
    /// 소모품/재료 아이템 획득. 인벤토리가 꽉 찼으면 퀵슬롯에 시도.
    /// 둘 다 꽉 찼으면 "가방이 꽉 찼습니다" 메시지 표시.
    /// </summary>
    /// <param name="itemId">ItemTableSO의 ItemID 값</param>
    /// <param name="quantity">획득 수량</param>
    /// <param name="itemType">아이템 타입 문자열 ("Consumable" 이면 퀵슬롯 fallback 시도)</param>
    /// <example>
    /// yield return StartCoroutine(InventoryManager.Instance.PickupItem(4000, 1, "Consumable"));
    /// </example>
    public IEnumerator PickupItem(ulong itemObjectId, int itemId, int quantity, string itemType)
    {
        bool isSuccess = false;
        bool inventoryFull = false;

        ItemEntry itemEntry = itemTable.GetById(itemId);

        yield return StartCoroutine(DataAPI.AddItem(UserSession.DbUserId, itemId, quantity,
            onSuccess: _ => {
                Refresh();
                isSuccess = true;
            },
            onError: err => {
                if (err.Contains("가방이 꽉 찼습니다")) inventoryFull = true;
                else Debug.LogError($"[Inventory] 아이템 획득 실패: {err}");
            }
        ));

        if (!inventoryFull)
        {
            if (isSuccess)
            {
                if (ServerManager.Instance != null)
                    ServerManager.Instance.EndAddItemRpc(itemObjectId, true, $"{itemEntry?.Name}을(를) {quantity}개 획득했습니다.");
            }
            else
            {
                if (ServerManager.Instance != null)
                    ServerManager.Instance.EndAddItemRpc(itemObjectId, false, "");
            }
            yield break;
        }

        // 소모품이고 퀵슬롯에 공간 있으면 퀵슬롯 시도
        if (itemType == "Consumable" && QuickSlotManager.Instance != null && QuickSlotManager.Instance.HasFreeSlot())
        {
            bool quickslotFull = false;
            yield return StartCoroutine(DataAPI.AddToQuickSlot(UserSession.DbUserId, itemId, quantity,
                onSuccess: _ =>
                {
                    Refresh();
                    isSuccess = true;
                },
                onError: err => {
                    if (err.Contains("퀵슬롯이 꽉 찼습니다")) quickslotFull = true;
                    else Debug.LogError($"[Inventory] 퀵슬롯 획득 실패: {err}");
                }
            ));
            if (quickslotFull) ShowMessage("가방이 꽉 찼습니다.");
        }
        else
        {
            ShowMessage("가방이 꽉 찼습니다.");
        }

        if(isSuccess)
        {
            ServerManager.Instance?.EndAddItemRpc(itemObjectId, true, $"{itemEntry?.Name}을(를) {quantity}개 획득했습니다.");
        }
        else
        {
            ServerManager.Instance?.EndAddItemRpc(itemObjectId, false, "");
        }
    }

    // ─── 버리기 ──────────────────────────────────────────────────────────

    public void ShowDiscardPopup(InventorySlotData data)
    {
        if (discardPopup != null) discardPopup.Show(data);
        else Debug.LogWarning("[Inventory] discardPopup 미연결 — Inspector 또는 씬에 DiscardPopup 오브젝트 필요");
    }

    public void DiscardItem(InventorySlotData data, int quantity)
    {
        StartCoroutine(DoDiscard(data, quantity));
    }

    private IEnumerator DoDiscard(InventorySlotData data, int quantity)
    {
        bool success = false;
        yield return StartCoroutine(DataAPI.ConsumeItem(UserSession.DbUserId, data.InventoryId, quantity,
            onSuccess: _ => { Refresh(); success = true; },
            onError:   err => Debug.LogError($"[Inventory] 버리기 실패: {err}")
        ));
        if (!success) yield break;

        DroppedItemData droppedItemData = new DroppedItemData(data);
        droppedItemData.Quantity = quantity;
        if (ServerManager.Instance != null)
            ServerManager.Instance.RequestThrowItemRpc(droppedItemData);
    }

    // ─── 아이템 쿨타임 ──────────────────────────────────────────────────

    public bool CanUseItem(int itemId)
    {
        if (!_itemCooldowns.TryGetValue(itemId, out float endTime)) return true;
        return Time.time >= endTime;
    }

    public void SetItemCooldown(int itemId)
    {
        if (consumableDetail == null) return;
        ConsumableDetailEntry detail = consumableDetail.GetById(itemId);
        if (detail == null || detail.Cooltime <= 0) return;
        _itemCooldowns[itemId] = Time.time + detail.Cooltime;
    }

    public float GetRemainingCooldown(int itemId)
    {
        if (!_itemCooldowns.TryGetValue(itemId, out float endTime)) return 0f;
        return Mathf.Max(0f, endTime - Time.time);
    }

    public float GetCooldownDuration(int itemId)
    {
        if (consumableDetail == null) return 0f;
        ConsumableDetailEntry detail = consumableDetail.GetById(itemId);
        return detail != null ? detail.Cooltime : 0f;
    }

    // ─── 메시지 표시 ─────────────────────────────────────────────────────

    public void ShowMessage(string msg)
    {
        if (_fullMessageCoroutine != null) StopCoroutine(_fullMessageCoroutine);
        _fullMessageCoroutine = StartCoroutine(MessageRoutine(msg));
    }

    private IEnumerator MessageRoutine(string msg)
    {
        if (messageText != null) { messageText.text = msg; messageText.gameObject.SetActive(true); }
        yield return WaitTwoSeconds;
        if (messageText != null) messageText.gameObject.SetActive(false);
        _fullMessageCoroutine = null;
    }

    // ─── 골드 표시 ──────────────────────────────────────────────────────

    public int Gold { get; private set; }

    public void SetGold(int gold)
    {
        Gold = gold;
        if (goldText != null) goldText.text = $"{gold:N0} G";
    }

    private IEnumerator FetchGold()
    {
        if (!UserSession.IsLoggedIn) yield break;
        yield return StartCoroutine(DataAPI.GetCharacter(UserSession.DbUserId,
            onSuccess: res => SetGold(res.gold),
            onError:   err => { SetGold(0); Debug.LogWarning($"[Inventory] 골드 조회 실패: {err}"); }
        ));
    }

    // ─── 내부 헬퍼 ───────────────────────────────────────────────────────

    private EquipmentDetailEntry GetDetail(InventorySlotData data)
    {
        if (equipDetail == null || data.ItemInfo.ItemType != "Equipment") return null;
        return equipDetail.GetById(data.ItemInfo.ItemID);
    }

    private ConsumableDetailEntry GetConsumDetail(InventorySlotData data)
    {
        if (consumableDetail == null || data.ItemInfo.ItemType != "Consumable") return null;
        return consumableDetail.GetById(data.ItemInfo.ItemID);
    }
}
