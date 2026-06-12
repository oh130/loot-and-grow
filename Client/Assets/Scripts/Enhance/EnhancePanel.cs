using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 강화 UI 패널.
/// 인벤토리/장착 슬롯에서 무기·방어구를 선택해 강화한다.
/// 강화 실패 시 DestroyRate에 따라 아이템이 파괴될 수 있다.
/// 강화석 item_id: 기본 5010. Inspector에서 변경 가능.
/// </summary>
public class EnhancePanel : MonoBehaviour
{
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

    private static readonly int MaxWeaponEnhanceLevel = 20;
    private static readonly int MaxArmorEnhanceLevel  = 10;

    private readonly List<EnhanceSlot>       _slots      = new();
    private readonly List<InventorySlotData> _stoneSlots = new();
    private EnhanceSlot                      _selectedSlot;
    private InventorySlotData                _selectedItem;
    private bool                             _selectedItemIsEquipped;
    private Tween                            _resultClearTween;

    private int TotalStoneQty
    {
        get { int t = 0; foreach (var s in _stoneSlots) t += s.Quantity; return t; }
    }

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
        if (_selectedItem == null || _stoneSlots.Count == 0) return;
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

        _stoneSlots.Clear();
        InventorySlotData prevSelected = _selectedItem;
        _selectedSlot = null;
        _selectedItem = null;

        foreach (var item in items)
        {
            ItemEntry info = GameDataSO.Instance.ItemTable != null ? GameDataSO.Instance.ItemTable.GetById(item.item_id) : null;
            if (info == null) continue;

            if (info.ItemID == stoneItemId)
            {
                _stoneSlots.Add(new InventorySlotData(item.id, info, item.quantity, item.enhance_level, item.quickslot_index));
                continue;
            }

            if (info.ItemType != "Equipment") continue;
            EquipmentDetailEntry detail = GameDataSO.Instance.EquipDetail != null ? GameDataSO.Instance.EquipDetail.GetById(info.ItemID) : null;
            if (detail == null) continue;

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
            ItemEntry info = GameDataSO.Instance.ItemTable != null ? GameDataSO.Instance.ItemTable.GetById(item.item_id) : null;
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

    // ─── 헬퍼 ───────────────────────────────────────────────────────────────

    private bool IsWeaponItem(InventorySlotData item)
    {
        EquipmentDetailEntry detail = GameDataSO.Instance.EquipDetail != null ? GameDataSO.Instance.EquipDetail.GetById(item.ItemInfo.ItemID) : null;
        return detail != null && detail.WeaponType != "None" && !string.IsNullOrEmpty(detail.WeaponType);
    }

    private string GetArmorSlotType(InventorySlotData item)
    {
        EquipmentDetailEntry detail = GameDataSO.Instance.EquipDetail != null ? GameDataSO.Instance.EquipDetail.GetById(item.ItemInfo.ItemID) : null;
        return detail?.EquipType ?? "";
    }

    private int GetMaxEnhanceLevel()
    {
        if (_selectedItem == null) return MaxWeaponEnhanceLevel;
        return IsWeaponItem(_selectedItem) ? MaxWeaponEnhanceLevel : MaxArmorEnhanceLevel;
    }

    // ─── UI 갱신 ────────────────────────────────────────────────────────────

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

        int currentLevel  = _selectedItem.EnhanceLevel;
        int maxLevel      = GetMaxEnhanceLevel();

        var sb = new StringBuilder();
        sb.AppendLine($"<color={GradeUtil.GetHex(_selectedItem.ItemInfo.Grade)}>{_selectedItem.ItemInfo.Name}</color>");
        sb.AppendLine();

        if (currentLevel >= maxLevel)
        {
            sb.Append("최대 강화 단계입니다.");
            infoText.text = sb.ToString();
            return;
        }

        int nextLevel = currentLevel + 1;
        EnhanceEntry entry = GameDataSO.Instance.EnhanceTable != null ? GameDataSO.Instance.EnhanceTable.GetByLevel(nextLevel) : null;
        if (entry == null) { infoText.text = sb.ToString(); return; }

        sb.AppendLine($"다음 강화 레벨 : +{nextLevel}");

        EquipmentDetailEntry detail = GameDataSO.Instance.EquipDetail != null ? GameDataSO.Instance.EquipDetail.GetById(_selectedItem.ItemInfo.ItemID) : null;

        if (IsWeaponItem(_selectedItem))
        {
            // 무기: 공격력·치명타 표시
            string wepType = detail != null ? detail.WeaponType : "";
            float addAtk  = wepType == "Sword" ? entry.SwordAdd : wepType == "Staff" ? entry.StaffAdd : entry.BowAdd;
            float curAtk  = (detail != null ? detail.ATK : 0f)
                          + (GameDataSO.Instance.EnhanceTable != null ? GameDataSO.Instance.EnhanceTable.GetCumulativeATKBonus(wepType, currentLevel) : 0f);

            EnhanceEntry curEntry = currentLevel > 0 && GameDataSO.Instance.EnhanceTable != null ? GameDataSO.Instance.EnhanceTable.GetByLevel(currentLevel) : null;
            int curCrit  = curEntry  != null ? curEntry.CritChance : 0;
            int nextCrit = entry.CritChance;

            if (addAtk  != 0f)          sb.AppendLine($"공격력: {curAtk:F2} → {curAtk + addAtk:F2}");
            if (nextCrit != curCrit)     sb.AppendLine($"치명타 확률: {curCrit}% → {nextCrit}%");
        }
        else
        {
            // 방어구: 슬롯별 스탯 표시
            string slotType = GetArmorSlotType(_selectedItem);
            ArmorEnhanceEntry curArmor  = currentLevel > 0 ? GameDataSO.Instance.ArmorEnhanceTable?.GetBySlotAndLevel(slotType, currentLevel) : null;
            ArmorEnhanceEntry nextArmor = GameDataSO.Instance.ArmorEnhanceTable?.GetBySlotAndLevel(slotType, nextLevel);

            if (nextArmor != null)
            {
                float baseDef   = detail != null ? detail.DEF : 0f;
                float baseHp    = detail != null ? detail.HP  : 0f;
                float baseAtk   = detail != null ? detail.ATK : 0f;
                float baseSpd   = detail != null ? detail.SPD : 0f;
                float baseAps   = detail != null ? detail.APS : 0f;
                float baseHpr   = detail != null ? detail.HPR : 0f;

                float curDef  = baseDef + (curArmor != null ? curArmor.DefAdd       : 0f);
                float curHp   = baseHp  + (curArmor != null ? curArmor.HealthAdd    : 0f);
                float curAtk  = baseAtk + (curArmor != null ? curArmor.AtkAdd       : 0f);
                float curSpd  = baseSpd + (curArmor != null ? curArmor.MoveSpeedAdd : 0f);
                float curAps  = baseAps + (curArmor != null ? curArmor.APSAdd       : 0f);
                float curHpr  = baseHpr + (curArmor != null ? curArmor.HPRAdd       : 0f);

                if (nextArmor.DefAdd       > 0) sb.AppendLine($"방어력: {curDef:F2} → {baseDef + nextArmor.DefAdd:F2}");
                if (nextArmor.HealthAdd    > 0) sb.AppendLine($"체력: {curHp:F2} → {baseHp + nextArmor.HealthAdd:F2}");
                if (nextArmor.AtkAdd       > 0) sb.AppendLine($"공격력: {curAtk:F2} → {baseAtk + nextArmor.AtkAdd:F2}");
                if (nextArmor.MoveSpeedAdd > 0) sb.AppendLine($"이동속도: {curSpd:F2} → {baseSpd + nextArmor.MoveSpeedAdd:F2}");
                if (nextArmor.APSAdd       > 0) sb.AppendLine($"공격속도: {curAps:F3} → {baseAps + nextArmor.APSAdd:F3}");
                if (nextArmor.HPRAdd       > 0) sb.AppendLine($"HP 회복: {curHpr:F3} → {baseHpr + nextArmor.HPRAdd:F3}");
            }
        }

        sb.AppendLine();
        sb.Append($"강화석: {TotalStoneQty}개 보유 / {entry.ReqStone}개 필요  (성공률 {(int)entry.SuccessRate}%)");
        if (entry.DestroyRate > 0)
            sb.Append($"\n파괴 확률 {entry.DestroyRate}%");

        infoText.text = sb.ToString();
    }

    private void RefreshEnhanceButton()
    {
        if (enhanceButton == null) return;

        if (_selectedItem == null || _selectedItem.EnhanceLevel >= GetMaxEnhanceLevel())
        {
            enhanceButton.interactable = false;
            return;
        }

        EnhanceEntry entry = GameDataSO.Instance.EnhanceTable != null ? GameDataSO.Instance.EnhanceTable.GetByLevel(_selectedItem.EnhanceLevel + 1) : null;
        enhanceButton.interactable = entry != null && TotalStoneQty >= entry.ReqStone;
    }

    private void ConsumeStoneLocally(int amount)
    {
        int remaining = amount;
        while (remaining > 0 && _stoneSlots.Count > 0)
        {
            var slot    = _stoneSlots[0];
            int consume = Mathf.Min(slot.Quantity, remaining);
            slot.Quantity -= consume;
            remaining     -= consume;
            if (slot.Quantity <= 0) _stoneSlots.RemoveAt(0);
        }
    }

    // ─── 강화 실행 ──────────────────────────────────────────────────────────

    private IEnumerator DoEnhance()
    {
        if (_selectedItem == null || _stoneSlots.Count == 0) yield break;

        EnhanceEntry entry = GameDataSO.Instance.EnhanceTable != null ? GameDataSO.Instance.EnhanceTable.GetByLevel(_selectedItem.EnhanceLevel + 1) : null;
        if (entry == null) yield break;

        enhanceButton.interactable = false;

        bool isWeapon = IsWeaponItem(_selectedItem);

        // 콜백을 로컬 변수로 분리해 무기/방어구 공통 사용
        Action<EnhanceResponse> onSuccess = res => HandleEnhanceResult(res, entry.ReqStone);
        Action<string>          onError   = HandleEnhanceError;

        if (isWeapon)
            yield return StartCoroutine(DataAPI.EnhanceWeapon(
                UserSession.DbUserId, _selectedItem.InventoryId, stoneItemId,
                (int)entry.SuccessRate, entry.ReqStone, entry.DestroyRate,
                onSuccess, onError));
        else
            yield return StartCoroutine(DataAPI.EnhanceArmor(
                UserSession.DbUserId, _selectedItem.InventoryId, stoneItemId,
                (int)entry.SuccessRate, entry.ReqStone, entry.DestroyRate,
                onSuccess, onError));
    }

    private void HandleEnhanceResult(EnhanceResponse res, int reqStone)
    {
        if (res.destroyed)
        {
            SoundManager.Instance.PlaySFX2D("EnhanceFailed");
            _resultClearTween?.Kill();
            if (resultText != null) { resultText.gameObject.SetActive(true); resultText.text = "장비가 파괴됐습니다!"; }

            if (_selectedSlot != null) { Destroy(_selectedSlot.gameObject); _slots.Remove(_selectedSlot); }
            if (_selectedItemIsEquipped && InventoryManager.Instance?.EquipmentPanel != null)
                InventoryManager.Instance.EquipmentPanel.RefreshEquipped();

            _selectedSlot           = null;
            _selectedItem           = null;
            _selectedItemIsEquipped = false;

            UpdateItemDisplay();
            UpdateInfoText();
            RefreshEnhanceButton();
            _resultClearTween = DOVirtual.DelayedCall(3f, () => { if (resultText != null) resultText.text = ""; }).SetLink(gameObject);
            return;
        }

        ConsumeStoneLocally(reqStone);

        if (resultText != null)
        {
            _resultClearTween?.Kill();
            resultText.gameObject.SetActive(true);
            if (res.success) SoundManager.Instance.PlaySFX2D("EnhanceSuccess");
            else             SoundManager.Instance.PlaySFX2D("EnhanceFailed");
            resultText.text = res.success ? $"강화 성공! +{res.new_enhance_level}" : "강화 실패...";
            _resultClearTween = DOVirtual.DelayedCall(3f, () => { if (resultText != null) resultText.text = ""; }).SetLink(gameObject);
        }

        if (res.success)
        {
            _selectedItem = new InventorySlotData(
                _selectedItem.InventoryId, _selectedItem.ItemInfo,
                _selectedItem.Quantity, res.new_enhance_level, _selectedItem.QuickslotIndex);

            if (_selectedSlot != null) { _selectedSlot.Setup(_selectedItem); _selectedSlot.SetSelected(true); }

            if (_selectedItemIsEquipped && InventoryManager.Instance?.EquipmentPanel != null)
                InventoryManager.Instance.EquipmentPanel.RefreshEquipped();
        }

        UpdateItemDisplay();
        UpdateInfoText();
        RefreshEnhanceButton();
    }

    private void HandleEnhanceError(string err)
    {
        string msg = err.Contains("강화석") ? "강화석이 부족합니다." :
                     err.Contains("최대")   ? "최대 강화 단계입니다." : err;
        if (resultText != null) { resultText.gameObject.SetActive(true); resultText.text = msg; }
        RefreshEnhanceButton();
    }
}
