using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 강화 UI 패널.
/// 장비 목록에서 항목을 선택하면 infoText에 이름·스탯 변화·보유 강화석 정보를 표시하고,
/// 강화석이 충분할 때 강화 버튼을 활성화한다.
/// 강화석 item_id: 기본 5010(강화석). Inspector에서 변경 가능.
/// </summary>
public class EnhancePanel : MonoBehaviour
{
    [Header("SO 참조")]
    [SerializeField] private EnhanceTableSO    enhanceTable;
    [SerializeField] private EquipmentDetailSO equipDetail;
    [SerializeField] private ItemTableSO       itemTable;

    [Header("강화 슬롯 (선택 장비 표시)")]
    [SerializeField] private Image    itemIcon;
    [SerializeField] private Image    itemGradeBorder;
    [SerializeField] private TMP_Text itemEnhanceText;

    [Header("장비 목록")]
    [SerializeField] private EnhanceSlot slotPrefab;
    [SerializeField] private Transform   slotsParent;

    [Header("정보 패널")]
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button   enhanceButton;
    [SerializeField] private Button   closeButton;

    [Header("루트 오브젝트")]
    [SerializeField] private GameObject enhanceUI;

    private CanvasGroup _enhanceUICG;

    [Header("강화석 아이템 ID")]
    [SerializeField] private int stoneItemId = 5010;

    private static readonly int MaxEnhanceLevel = 20;

    private readonly List<EnhanceSlot> _slots       = new();
    private EnhanceSlot                _selectedSlot;
    private InventorySlotData          _selectedItem;
    private InventorySlotData          _stoneSlot;
    private bool                       _selectedItemIsEquipped;

    private void Awake()
    {
        if (enhanceButton != null) enhanceButton.onClick.AddListener(OnEnhanceClicked);
        if (closeButton   != null) closeButton.onClick.AddListener(Close);
        if (enhanceUI     != null) _enhanceUICG = enhanceUI.GetComponent<CanvasGroup>();
    }

    // ─── 열기 ───────────────────────────────────────────────────────────────

    public void Open()
    {
        SoundManager.Instance.PlaySFX2D("MenuOpen");
        if (enhanceUI != null)
        {
            enhanceUI.SetActive(true);
            if (_enhanceUICG != null) { _enhanceUICG.alpha = 0f; _enhanceUICG.interactable = false; _enhanceUICG.blocksRaycasts = false; }
        }
        StartCoroutine(FetchAndPopulate());
    }

    public void Close()
    {
        SoundManager.Instance.PlaySFX2D("MenuClose");
        if (enhanceUI != null) enhanceUI.SetActive(false);
    }

    // ─── 슬롯 클릭 ──────────────────────────────────────────────────────────

    public void OnSlotClicked(EnhanceSlot slot)
    {
        SoundManager.Instance.PlaySFX2D("ItemSelect");
        if (_selectedSlot != null) _selectedSlot.SetSelected(false);
        _selectedSlot           = slot;
        _selectedItem           = slot.Data;
        _selectedItemIsEquipped = slot.IsEquipped;
        slot.SetSelected(true);

        if (resultText != null) resultText.text = "";
        UpdateItemDisplay();
        UpdateInfoText();
        RefreshEnhanceButton();
    }

    // ─── 강화 버튼 ──────────────────────────────────────────────────────────

    public void OnEnhanceClicked()
    {
        SoundManager.Instance.PlaySFX2D("ItemSelect");
        if (_selectedItem == null || _stoneSlot == null) return;
        StartCoroutine(DoEnhance());
    }

    // ─── 내부 ───────────────────────────────────────────────────────────────

    private IEnumerator FetchAndPopulate()
    {
        List<InventoryItemResponse>  invItems      = null;
        List<EquippedItemResponse>   equippedItems = null;

        yield return StartCoroutine(DataAPI.GetInventory(UserSession.DbUserId,
            onSuccess: res => invItems = res.items,
            onError:   err => Debug.LogError("[Enhance] 인벤토리 조회 실패: " + err)
        ));
        yield return StartCoroutine(DataAPI.GetEquipped(UserSession.DbUserId,
            onSuccess: res => equippedItems = res.items,
            onError:   err => Debug.LogError("[Enhance] 장착 장비 조회 실패: " + err)
        ));

        if (invItems != null)
            Populate(invItems, equippedItems ?? new List<EquippedItemResponse>());
    }

    private void Populate(List<InventoryItemResponse> items, List<EquippedItemResponse> equippedItems)
    {
        foreach (var s in _slots) if (s != null) Destroy(s.gameObject);
        _slots.Clear();

        _stoneSlot = null;
        InventorySlotData prevSelected = _selectedItem;
        _selectedSlot = null;
        _selectedItem = null;

        foreach (var item in items)
        {
            ItemEntry info = itemTable != null ? itemTable.GetById(item.item_id) : null;
            if (info == null) continue;

            if (info.ItemID == stoneItemId)
            {
                _stoneSlot = new InventorySlotData(item.id, info, item.quantity, item.enhance_level, item.quickslot_index);
                continue;
            }

            if (info.ItemType != "Equipment") continue;
            EquipmentDetailEntry detail = equipDetail != null ? equipDetail.GetById(info.ItemID) : null;
            if (detail == null) continue;
            bool isWeapon = detail.WeaponType == "Sword" || detail.WeaponType == "Staff" || detail.WeaponType == "Bow";
            if (!isWeapon) continue;

            var data = new InventorySlotData(item.id, info, item.quantity, item.enhance_level, item.quickslot_index);
            EnhanceSlot slot = Instantiate(slotPrefab, slotsParent, false);
            slot.Setup(data);
            _slots.Add(slot);

            if (prevSelected != null && item.id == prevSelected.InventoryId)
            {
                _selectedSlot = slot;
                _selectedItem = data;
                _selectedItemIsEquipped = false;
                slot.SetSelected(true);
            }
        }

        foreach (var item in equippedItems)
        {
            if (item.slot_type != "weapon") continue;

            ItemEntry info = itemTable != null ? itemTable.GetById(item.item_id) : null;
            if (info == null) continue;

            var data = new InventorySlotData(item.id, info, 1, item.enhance_level, -1);
            EnhanceSlot slot = Instantiate(slotPrefab, slotsParent, false);
            slot.Setup(data, isEquipped: true);
            _slots.Add(slot);

            if (prevSelected != null && item.id == prevSelected.InventoryId)
            {
                _selectedSlot = slot;
                _selectedItem = data;
                _selectedItemIsEquipped = true;
                slot.SetSelected(true);
            }
        }

        UpdateItemDisplay();
        UpdateInfoText();
        RefreshEnhanceButton();
        if (_enhanceUICG != null) { _enhanceUICG.alpha = 1f; _enhanceUICG.interactable = true; _enhanceUICG.blocksRaycasts = true; }
    }

    private void UpdateItemDisplay()
    {
        if (_selectedItem == null)
        {
            if (itemIcon        != null) itemIcon.gameObject.SetActive(false);
            if (itemGradeBorder != null) itemGradeBorder.color = Color.clear;
            if (itemEnhanceText != null) itemEnhanceText.text  = "";
            return;
        }

        if (itemIcon        != null) { itemIcon.sprite = _selectedItem.ItemInfo.Icon; itemIcon.gameObject.SetActive(true); }
        if (itemGradeBorder != null) itemGradeBorder.color = GradeUtil.GetColor(_selectedItem.ItemInfo.Grade);
        if (itemEnhanceText != null) itemEnhanceText.text  = _selectedItem.EnhanceLevel > 0 ? $"+{_selectedItem.EnhanceLevel}" : "";
    }

    private void UpdateInfoText()
    {
        if (infoText == null) return;

        if (_selectedItem == null)
        {
            infoText.text = "장비를 선택하세요.";
            return;
        }

        int currentLevel = _selectedItem.EnhanceLevel;

        EquipmentDetailEntry detail  = equipDetail != null ? equipDetail.GetById(_selectedItem.ItemInfo.ItemID) : null;
        string               wepType = detail != null && detail.WeaponType != null ? detail.WeaponType : "";


        var sb = new StringBuilder();
        sb.AppendLine($"<color={GradeUtil.GetHex(_selectedItem.ItemInfo.Grade)}>{_selectedItem.ItemInfo.Name}</color>");
        sb.AppendLine();

        if (currentLevel >= MaxEnhanceLevel)
        {
            sb.Append("최대 강화 단계입니다.");
            infoText.text = sb.ToString();
            return;
        }

        int nextLevel = currentLevel + 1;
        EnhanceEntry entry = enhanceTable != null ? enhanceTable.GetByLevel(nextLevel) : null;
        if (entry == null) { infoText.text = sb.ToString(); return; }

        float addAtk  = wepType == "Sword" ? entry.SwordAdd : wepType == "Staff" ? entry.StaffAdd : entry.BowAdd;
        float curAtk  = (detail != null ? detail.ATK : 0f) + (enhanceTable != null ? enhanceTable.GetCumulativeATKBonus(wepType, currentLevel) : 0f);

        EnhanceEntry curEntry = currentLevel > 0 && enhanceTable != null ? enhanceTable.GetByLevel(currentLevel) : null;
        int curCrit  = curEntry != null ? curEntry.CritChance : 0;
        int nextCrit = entry.CritChance;

        sb.AppendLine($"다음 강화 레벨 : +{nextLevel}");

        if (addAtk != 0f)
            sb.AppendLine($"공격력: {curAtk:F2} → {curAtk + addAtk:F2}");
        if (nextCrit != curCrit)
            sb.AppendLine($"치명타 확률: {curCrit}% → {nextCrit}%");

        sb.AppendLine();

        int stoneQty = _stoneSlot != null ? _stoneSlot.Quantity : 0;
        sb.Append($"강화석: {stoneQty}개 보유 / {entry.ReqStone}개 필요  (성공률 {(int)entry.SuccessRate}%)");

        infoText.text = sb.ToString();
    }

    private void RefreshEnhanceButton()
    {
        if (enhanceButton == null) return;

        if (_selectedItem == null || _selectedItem.EnhanceLevel >= MaxEnhanceLevel)
        {
            enhanceButton.interactable = false;
            return;
        }

        EnhanceEntry entry   = enhanceTable != null ? enhanceTable.GetByLevel(_selectedItem.EnhanceLevel + 1) : null;
        int          stoneQty = _stoneSlot  != null ? _stoneSlot.Quantity : 0;
        enhanceButton.interactable = entry != null && stoneQty >= entry.ReqStone;
    }

    private IEnumerator DoEnhance()
    {
        if (_selectedItem == null || _stoneSlot == null) yield break;

        EnhanceEntry entry = enhanceTable != null ? enhanceTable.GetByLevel(_selectedItem.EnhanceLevel + 1) : null;
        if (entry == null) yield break;

        enhanceButton.interactable = false;
        if (resultText != null) resultText.text = "";

        yield return StartCoroutine(DataAPI.EnhanceWeapon(
            UserSession.DbUserId,
            _selectedItem.InventoryId,
            _stoneSlot.InventoryId,
            (int)entry.SuccessRate,
            entry.ReqStone,
            onSuccess: res =>
            {
                if (resultText != null)
                {
                    resultText.gameObject.SetActive(true);
                    resultText.text = res.success ? $"강화 성공! +{res.new_enhance_level}" : "강화 실패...";
                    DOVirtual.DelayedCall(3f, () => {resultText.text = "";}).SetLink(gameObject);
                }

                // 강화석 소모
                _stoneSlot.Quantity -= entry.ReqStone;

                // 성공 시 강화 레벨 갱신
                if (res.success)
                {
                    _selectedItem = new InventorySlotData(
                        _selectedItem.InventoryId, _selectedItem.ItemInfo,
                        _selectedItem.Quantity, res.new_enhance_level, _selectedItem.QuickslotIndex);

                    if (_selectedSlot != null)
                    {
                        _selectedSlot.Setup(_selectedItem);
                        _selectedSlot.SetSelected(true);
                    }

                    if (_selectedItemIsEquipped && InventoryManager.Instance != null && InventoryManager.Instance.EquipmentPanel != null)
                        InventoryManager.Instance.EquipmentPanel.RefreshEquipped();
                }

                UpdateItemDisplay();
                UpdateInfoText();
                RefreshEnhanceButton();
            },
            onError: err =>
            {
                string msg = err.Contains("강화석") ? "강화석이 부족합니다." :
                             err.Contains("최대")   ? "최대 강화 단계입니다." : err;
                if (resultText != null) { resultText.gameObject.SetActive(true); resultText.text = msg; }
                RefreshEnhanceButton();
            }
        ));
    }

}
