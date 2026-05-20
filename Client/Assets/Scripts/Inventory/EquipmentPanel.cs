using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// weapon / helmet / top / bottom / accessory 5개 슬롯을 관리.
/// - OnEnable 시 서버에서 장착 장비 목록 조회 (단, DoEquip/DoUnequip 진행 중이면 스킵)
/// - 슬롯 클릭 → ItemInfoPanel에 정보 표시
/// - 장착/해제 요청 처리 후 스탯 재계산
/// </summary>
public class EquipmentPanel : MonoBehaviour
{
    [Header("SO 참조 (Inspector에서 드래그)")]
    public BaseStatSO    BaseStat;
    public EnhanceTableSO EnhanceTable;

    [Header("스탯 텍스트")]
    [SerializeField] private TMP_Text statText;

    [Header("슬롯 참조 (Inspector에서 드래그)")]
    [SerializeField] private EquipmentSlot weaponSlot;
    [SerializeField] private EquipmentSlot helmetSlot;
    [SerializeField] private EquipmentSlot topSlot;
    [SerializeField] private EquipmentSlot bottomSlot;
    [SerializeField] private EquipmentSlot accessorySlot;

	[Header("Visaul Equipment 참조")]
	[SerializeField] private PlayerEquipmentVisual equipmentVisual;

	private Dictionary<string, EquipmentSlot> _slots;
    private EquipmentSlot _selectedSlot;
    private bool _operationInProgress;
    private InventoryManager _manager;

    public EquipmentSlot SelectedSlot => _selectedSlot;

    private ItemTableSO       ItemTable   => _manager.ItemTable;
    private EquipmentDetailSO EquipDetail => _manager.EquipDetail;

    private void Awake()
    {
        _manager = FindAnyObjectByType<InventoryManager>();

        _slots = new Dictionary<string, EquipmentSlot>
        {
            { "weapon",    weaponSlot    },
            { "helmet",    helmetSlot    },
            { "top",       topSlot       },
            { "bottom",    bottomSlot    },
            { "accessory", accessorySlot }
        };

        foreach (var slot in _slots.Values)
        {
            slot.Init(this);
            slot.SetItem(null, null);
        }
    }

    private void OnEnable()
    {
        if (!_operationInProgress)
            StartCoroutine(FetchEquipped());
    }

    // ─── 초기 조회 ────────────────────────────────────────────────────────

    private IEnumerator FetchEquipped()
    {
        yield return null; // 한 프레임 대기 — 같은 프레임에 활성화된 오브젝트 Awake 완료 보장

        if (_manager == null) _manager = FindAnyObjectByType<InventoryManager>();
        if (!UserSession.IsLoggedIn) yield break;

        yield return StartCoroutine(DataAPI.GetEquipped(UserSession.DbUserId,
            onSuccess: res => {
                foreach (var slot in _slots.Values)
                    slot.SetItem(null, null);

                if (res.items != null)
                {
                    foreach (var equipped in res.items)
                    {
                        if (_slots.TryGetValue(equipped.slot_type, out EquipmentSlot slot))
                            slot.SetItem(equipped, ItemTable.GetById(equipped.item_id));
                    }
                }

                RecalculateStat();
            },
            onError: err => Debug.LogError($"[EquipmentPanel] 장착 장비 조회 실패: {err}")
        ));
    }

    // ─── 슬롯 선택 ────────────────────────────────────────────────────────

    public void OnSlotClicked(EquipmentSlot slot)
    {
        if (slot.CurrentEquipped == null) return;

        if (InventoryManager.Instance != null) InventoryManager.Instance.ClearInventorySelection();
        if (QuickSlotManager.Instance != null) QuickSlotManager.Instance.ClearSelection();
        if (InventoryManager.Instance != null && InventoryManager.Instance.ShopPanel != null)
            InventoryManager.Instance.ShopPanel.ClearSelection();

        if (_selectedSlot == slot)
        {
            slot.SetSelected(false);
            _selectedSlot = null;
            ItemInfoPanel panel = InventoryManager.Instance != null ? InventoryManager.Instance.ItemInfoPanel : null;
            if (panel != null) panel.ShowEmpty();
            return;
        }

        SelectSlot(slot);
    }

    public void ClearSelection()
    {
        if (_selectedSlot != null) _selectedSlot.SetSelected(false);
        _selectedSlot = null;
    }

    public void RefreshEquipped()
    {
        if (!_operationInProgress)
            StartCoroutine(FetchEquipped());
    }

    private void SelectSlot(EquipmentSlot slot)
    {
        if (_selectedSlot != null) _selectedSlot.SetSelected(false);
        _selectedSlot = slot;
        slot.SetSelected(true);

        ItemInfoPanel infoPanel = InventoryManager.Instance != null ? InventoryManager.Instance.ItemInfoPanel : null;
        if (infoPanel != null)
        {
            int itemId = slot.CurrentEquipped.item_id;
            infoPanel.ShowEquippedItem(slot, ItemTable.GetById(itemId), EquipDetail.GetById(itemId));
        }
    }

    // ─── 인벤토리 → 장착 ─────────────────────────────────────────────────

    public void RequestEquipFromInventory(InventorySlotData data)
    {
        EquipmentDetailEntry detail = EquipDetail.GetById(data.ItemInfo.ItemID);
        if (detail == null)
        {
            Debug.LogWarning($"[Equipment] EquipDetail에서 item_id {data.ItemInfo.ItemID} 없음");
            return;
        }
        RequestEquip(detail.EquipType.ToLower(), data.ItemInfo.ItemID, data.InventoryId, data.EnhanceLevel);
    }

    // ─── 장착 요청 ────────────────────────────────────────────────────────

    public void RequestEquip(string slotType, int itemId, int inventoryId, int enhanceLevel = 0)
    {
        StartCoroutine(DoEquip(slotType, itemId, inventoryId, enhanceLevel));
    }

    private IEnumerator DoEquip(string slotType, int itemId, int inventoryId, int enhanceLevel)
    {
        _operationInProgress = true;

        // 기존 착용 장비 정보를 미리 저장 (아직 DB는 건드리지 않는다)
        int oldItemId  = -1;
        int oldEnhance = 0;
        if (_slots.TryGetValue(slotType, out EquipmentSlot targetSlot) && targetSlot.CurrentEquipped != null)
        {
            oldItemId  = targetSlot.CurrentEquipped.item_id;
            oldEnhance = targetSlot.CurrentEquipped.enhance_level;
        }

        // 1. 새 장비 장착
        bool success = false;
        yield return StartCoroutine(DataAPI.EquipItem(UserSession.DbUserId, slotType, itemId, enhanceLevel,
            onSuccess: res => {
                success = true;
                if (_slots.TryGetValue(slotType, out EquipmentSlot slot))
                {
                    slot.SetItem(res, ItemTable.GetById(res.item_id));
                    SelectSlot(slot);
                }
                RecalculateStat();
                SoundManager.Instance.PlaySFX2D("Equip");           
            },
            onError: err => Debug.LogError($"[EquipmentPanel] 장착 실패: {err}")
        ));

        if (!success) { _operationInProgress = false; yield break; }

        // 2. 새 장비를 인벤토리에서 제거 (슬롯 1칸 해방)
        yield return StartCoroutine(DataAPI.DeleteItem(UserSession.DbUserId, inventoryId,
            onSuccess: _ => {
                if (InventoryManager.Instance != null) InventoryManager.Instance.Refresh();
            },
            onError: err => Debug.LogError($"[EquipmentPanel] 인벤토리 제거 실패: {err}")
        ));

        // 3. 기존 장비를 인벤토리로 반환 (2번으로 슬롯이 해방됐으므로 20칸 제한에 걸리지 않는다)
        if (oldItemId >= 0)
        {
            yield return StartCoroutine(DataAPI.AddEquipment(UserSession.DbUserId, oldItemId, oldEnhance,
                onSuccess: _ => {
                    if (InventoryManager.Instance != null) InventoryManager.Instance.Refresh();
                },
                onError: err => Debug.LogError($"[EquipmentPanel] 기존 장비 인벤토리 반환 실패: {err}")
            ));
        }

        _operationInProgress = false;
        StartCoroutine(FetchEquipped());
    }

    // ─── 해제 요청 ────────────────────────────────────────────────────────

    public void RequestUnequip(string slotType)
    {
        StartCoroutine(DoUnequip(slotType));
    }

    private IEnumerator DoUnequip(string slotType)
    {
        if (!_slots.TryGetValue(slotType, out EquipmentSlot slot) || slot.CurrentEquipped == null)
            yield break;

        _operationInProgress = true;

        int itemId       = slot.CurrentEquipped.item_id;
        int enhanceLevel = slot.CurrentEquipped.enhance_level;
        bool success     = false;

        yield return StartCoroutine(DataAPI.UnequipItem(UserSession.DbUserId, slotType,
            onSuccess: _ => {
                success = true;
                ClearSelection();
                slot.SetItem(null, null);
                RecalculateStat();
                SoundManager.Instance.PlaySFX2D("UnEquip");           
            },
            onError: err => Debug.LogError($"[EquipmentPanel] 해제 실패: {err}")
        ));

        if (!success) { _operationInProgress = false; yield break; }

        yield return StartCoroutine(DataAPI.AddEquipment(UserSession.DbUserId, itemId, enhanceLevel,
            onSuccess: _ => {
                if (InventoryManager.Instance != null)
                    InventoryManager.Instance.RefreshKeepFocusByItemId(itemId, enhanceLevel);
            },
            onError: err => Debug.LogError($"[EquipmentPanel] 해제 후 인벤토리 반환 실패: {err}")
        ));

        _operationInProgress = false;
    }

    // 사망시 장착 장비를 제거(해제 X)
    public void RequestDeleteEquip(string slotType)
    {
        StartCoroutine(DoDeleteEquip(slotType));
    }

    private IEnumerator DoDeleteEquip(string slotType)
    {
        if (!_slots.TryGetValue(slotType, out EquipmentSlot slot) || slot.CurrentEquipped == null)
            yield break;

        _operationInProgress = true;

        int itemId       = slot.CurrentEquipped.item_id;
        int enhanceLevel = slot.CurrentEquipped.enhance_level;

        yield return StartCoroutine(DataAPI.UnequipItem(UserSession.DbUserId, slotType,
            onSuccess: _ => {
                ClearSelection();
                slot.SetItem(null, null);
                RecalculateStat();
            },
            onError: err => Debug.LogError($"[EquipmentPanel] 해제 실패: {err}")
        ));

        _operationInProgress = false;
    }

	// ─── 장비 장착 ──────────────────────────────────────────────────────

	private void FindLocalEquipmentVisual()
	{
		if (equipmentVisual != null) return;

		if (NetworkManager.Singleton == null) return;
		if (NetworkManager.Singleton.LocalClient == null) return;
		if (NetworkManager.Singleton.LocalClient.PlayerObject == null) return;

		equipmentVisual =
			NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerEquipmentVisual>();
	}

	private void ApplyEquipmentVisual()
	{
		FindLocalEquipmentVisual();
		if (equipmentVisual == null) return;

		if (_slots.TryGetValue("helmet", out EquipmentSlot helmetSlot) &&
			helmetSlot.CurrentEquipped != null)
		{
			int itemId = helmetSlot.CurrentEquipped.item_id;
			equipmentVisual.EquipHat(itemId);
		}
		else
		{
			equipmentVisual.UnequipHat();
		}

		if (_slots.TryGetValue("weapon", out EquipmentSlot weaponSlot) &&
			weaponSlot.CurrentEquipped != null)
		{
			int itemId = weaponSlot.CurrentEquipped.item_id;
			equipmentVisual.EquipWeapon(itemId);
		}
		else
		{
			equipmentVisual.UnequipWeapon();
		}

		if (_slots.TryGetValue("top", out EquipmentSlot topSlot) &&
			topSlot.CurrentEquipped != null)
		{
			int itemId = topSlot.CurrentEquipped.item_id;
			equipmentVisual.EquipTop(itemId);
		}
		else
		{
			equipmentVisual.UnequipTop();
		}

		if (_slots.TryGetValue("bottom", out EquipmentSlot bottomSlot) &&
			bottomSlot.CurrentEquipped != null)
		{
			int itemId = bottomSlot.CurrentEquipped.item_id;
			equipmentVisual.EquipBottom(itemId);
		}
		else
		{
			equipmentVisual.UnequipBottom();
		}

		if (_slots.TryGetValue("accessory", out EquipmentSlot accessorySlot) &&
			accessorySlot.CurrentEquipped != null)
		{
			int itemId = accessorySlot.CurrentEquipped.item_id;
			equipmentVisual.EquipAccessory(itemId);
		}
		else
		{
			equipmentVisual.UnequipAccessory();
		}
	}

	// ─── 스탯 재계산 ──────────────────────────────────────────────────────

	private void RecalculateStat()
    {
        List<EquippedItemResponse> equippedList = new List<EquippedItemResponse>();
        foreach (var slot in _slots.Values)
        {
            if (slot.CurrentEquipped != null)
                equippedList.Add(slot.CurrentEquipped);
        }

        PlayerStat stat = PlayerStat.Calculate(BaseStat, EquipDetail, equippedList, EnhanceTable);

        if (statText != null)
            statText.text =
                $"최대 체력 <color=#ffffff>{stat.MaxHP:F2}</color>\n" +
                $"공격력 <color=#ffffff>{stat.ATK:F2}</color>\n" +
                $"방어력 <color=#ffffff>{stat.DEF:F2}</color>\n" +
                $"공격 속도 <color=#ffffff>{stat.APS:F2}</color>\n" +
                $"이동 속도 <color=#ffffff>{stat.SPD:F2}</color>\n" +
                $"체력 재생 <color=#ffffff>{stat.HPR:F2}</color>\n" +
                $"치명타 확률 <color=#ffffff>{stat.CHC:F2}</color>";

        ServerManager.Instance?.RequestApplyStatRpc(stat);
		ApplyEquipmentVisual();
	}
}
