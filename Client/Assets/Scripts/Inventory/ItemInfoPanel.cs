using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 우측 패널. 선택된 슬롯의 아이템 정보와 상황별 버튼을 표시한다.
///
/// 버튼 규칙:
///   actionButton    - 장착(Equipment) / 사용(Consumable) / 해제(장착 슬롯). 그 외 숨김.
///   quickslotButton - 소모품일 때만 표시. 퀵슬롯 등록 / 퀵슬롯 해제.
///   discardButton   - 인벤토리 아이템(일반+퀵슬롯 모두)일 때 표시.
/// </summary>
public class ItemInfoPanel : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button   actionButton;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Button   quickslotButton;
    [SerializeField] private TMP_Text quickslotLabel;
    [SerializeField] private Button   discardButton;
    [SerializeField] private TMP_Text discardLabel;
    [SerializeField] private Button   sellButton;
    [SerializeField] private TMP_Text sellLabel;
    [SerializeField] private Button   buyButton;
    [SerializeField] private TMP_Text buyLabel;
    [SerializeField] private Button   storageButton;
    [SerializeField] private TMP_Text storageLabel;

    private InventorySlotData _invData;
    private EquipmentSlot     _equipSlot;
    private ShopItemResponse  _shopItem;
    private ItemEntry         _shopItemInfo;
    private InventorySlotData _storageItemData; // InventoryId = storage DB id

    private void Awake()
    {
        if (actionButton    != null) actionButton.onClick.AddListener(OnAction);
        if (quickslotButton != null) quickslotButton.onClick.AddListener(OnQuickSlot);
        if (discardButton   != null) discardButton.onClick.AddListener(OnDiscard);
        if (sellButton      != null) sellButton.onClick.AddListener(OnSell);
        if (buyButton       != null) buyButton.onClick.AddListener(OnBuy);
        if (storageButton   != null) storageButton.onClick.AddListener(OnStorage);
        if (discardLabel    != null) discardLabel.text = "버리기";
        if (sellLabel       != null) sellLabel.text    = "판매";
        if (buyLabel        != null) buyLabel.text     = "구매";
        ShowEmpty();
    }

    public void ShowEmpty()
    {
        _invData         = null;
        _equipSlot       = null;
        _shopItem        = null;
        _shopItemInfo    = null;
        _storageItemData = null;
        SetVisible(false, false, false, false, false, false, false);
    }

    public void ShowInventoryItem(InventorySlotData data, EquipmentDetailEntry equipDetail = null, ConsumableDetailEntry consumDetail = null)
    {
        _invData         = data;
        _equipSlot       = null;
        _storageItemData = null;

        if (infoText != null) infoText.text = BuildText(data.ItemInfo, data.EnhanceLevel, equipDetail, consumDetail);

        bool storageOpen = StoragePanel.Instance != null && StoragePanel.Instance.IsOpen;
        if (storageOpen)
        {
            if (storageLabel != null) storageLabel.text = "보관";
            SetVisible(true, false, false, false, false, false, true);
            return;
        }

        bool isEquip      = data.ItemInfo.ItemType == "Equipment";
        bool isConsumable = data.ItemInfo.ItemType == "Consumable";

        if (actionLabel != null) actionLabel.text = isEquip ? "장착" : "사용";

        bool showQuickslot = false;
        if (isConsumable)
        {
            bool isInQuickSlot = data.QuickslotIndex >= 0;
            if (isInQuickSlot)
            {
                if (quickslotLabel != null) quickslotLabel.text = "퀵슬롯 해제";
                showQuickslot = true;
            }
            else if (QuickSlotManager.Instance != null && QuickSlotManager.Instance.HasFreeSlot())
            {
                if (quickslotLabel != null) quickslotLabel.text = "퀵슬롯 등록";
                showQuickslot = true;
            }
        }

        bool shopOpen = ShopManager.Instance != null && ShopManager.Instance.IsOpen;
        SetVisible(true, !shopOpen && (isEquip || isConsumable), !shopOpen && showQuickslot, !shopOpen, shopOpen, false, false);
    }

    /// <summary>
    /// 창고 슬롯 선택 시 호출. data.InventoryId = storage DB id.
    /// </summary>
    public void ShowStorageItem(InventorySlotData data, EquipmentDetailEntry equipDetail = null, ConsumableDetailEntry consumDetail = null)
    {
        _invData         = null;
        _equipSlot       = null;
        _shopItem        = null;
        _shopItemInfo    = null;
        _storageItemData = data;

        if (infoText     != null) infoText.text    = BuildText(data.ItemInfo, data.EnhanceLevel, equipDetail, consumDetail);
        if (storageLabel != null) storageLabel.text = "회수";
        SetVisible(true, false, false, false, false, false, true);
    }

    public void ShowEquippedItem(EquipmentSlot slot, ItemEntry itemInfo, EquipmentDetailEntry equipDetail = null)
    {
        _invData         = null;
        _equipSlot       = slot;
        _storageItemData = null;

        if (infoText    != null) infoText.text   = BuildText(itemInfo, slot.CurrentEquipped.enhance_level, equipDetail, null);
        if (actionLabel != null) actionLabel.text = "해제";
        SetVisible(true, true, false, false, false, false, false);
    }

    public void ShowShopItem(ShopItemResponse shopItem, ItemEntry itemInfo)
    {
        _invData         = null;
        _equipSlot       = null;
        _shopItem        = shopItem;
        _shopItemInfo    = itemInfo;
        _storageItemData = null;

        EquipmentDetailEntry  equipDetail  = InventoryManager.Instance?.EquipDetail?.GetById(itemInfo.ItemID);
        ConsumableDetailEntry consumDetail = InventoryManager.Instance?.ConsumDetail?.GetById(itemInfo.ItemID);

        if (infoText != null) infoText.text = BuildText(itemInfo, 0, equipDetail, consumDetail, showSellPrice: false);
        SetVisible(true, false, false, false, false, true, false);
    }

    public void RefreshSellButton()
    {
        if (_invData == null && _equipSlot == null) return;
        bool showSell = _invData != null && ShopManager.Instance != null && ShopManager.Instance.IsOpen;
        if (sellButton != null) sellButton.gameObject.SetActive(showSell);
    }

    // ─── 내부 ────────────────────────────────────────────────────────────

    private void SetVisible(bool info, bool action, bool quickslot, bool discard, bool sell, bool buy, bool storage)
    {
        if (infoText        != null) infoText.gameObject.SetActive(info);
        if (actionButton    != null) actionButton.gameObject.SetActive(action);
        if (quickslotButton != null) quickslotButton.gameObject.SetActive(quickslot);
        if (discardButton   != null) discardButton.gameObject.SetActive(discard);
        if (sellButton      != null) sellButton.gameObject.SetActive(sell);
        if (buyButton       != null) buyButton.gameObject.SetActive(buy);
        if (storageButton   != null) storageButton.gameObject.SetActive(storage);
    }

    private void OnStorage()
    {
        SoundManager.Instance.PlaySFX2D("ItemSelect");
        if (_storageItemData != null)
            StoragePanel.Instance?.Withdraw(_storageItemData);
        else if (_invData != null)
            StoragePanel.Instance?.Deposit(_invData);
    }

    private static string S(int v)   => v >= 0 ? $"+{v}"    : $"{v}";
    private static string S(float v) => v >= 0 ? $"+{v:F2}" : $"{v:F2}";

    private string BuildText(ItemEntry item, int enhanceLevel, EquipmentDetailEntry equipDetail, ConsumableDetailEntry consumDetail, bool showSellPrice = true)
    {
        if (item == null) return "";
        var sb = new StringBuilder();

        sb.Append($"<color={GradeUtil.GetHex(item.Grade)}>{item.Name}</color>");
        if (item.ItemType == "Equipment" && enhanceLevel > 0)
            sb.Append($" <color=#88ff88>+{enhanceLevel}</color>");
        sb.AppendLine();
        sb.AppendLine($"<color=#aaaaaa>{item.Grade}  {item.ItemType}</color>");

        if (item.ItemType == "Equipment" && equipDetail != null)
        {
            string typeStr = string.IsNullOrEmpty(equipDetail.WeaponType)
                ? equipDetail.EquipType
                : $"{equipDetail.EquipType} ({equipDetail.WeaponType})";
            sb.AppendLine($"<color=#888888>{typeStr}</color>");
            sb.AppendLine();

            EnhanceTableSO enhanceTable = InventoryManager.Instance != null ? InventoryManager.Instance.EnhanceTable : null;
            bool isWeapon = equipDetail.WeaponType == "Sword" || equipDetail.WeaponType == "Staff" || equipDetail.WeaponType == "Bow";

            if (equipDetail.HP  != 0)  sb.AppendLine($"최대 체력 <color=#ffffff>{S(equipDetail.HP):F2}</color>");

            if (equipDetail.ATK != 0 || (isWeapon && enhanceTable != null && enhanceLevel > 0))
            {
                float totalAtk = equipDetail.ATK + (isWeapon && enhanceTable != null ? enhanceTable.GetCumulativeATKBonus(equipDetail.WeaponType, enhanceLevel) : 0f);
                sb.AppendLine($"공격력 <color=#ffffff>{S(totalAtk):F2}</color>");
            }

            if (equipDetail.DEF != 0)  sb.AppendLine($"방어력 <color=#ffffff>{S(equipDetail.DEF):F2}</color>");
            if (equipDetail.APS != 0f) sb.AppendLine($"공격 속도 <color=#ffffff>{S(equipDetail.APS):F2}</color>");
            if (equipDetail.SPD != 0f) sb.AppendLine($"이동 속도 <color=#ffffff>{S(equipDetail.SPD):F2}</color>");
            if (equipDetail.HPR != 0f) sb.AppendLine($"체력 재생 <color=#ffffff>{S(equipDetail.HPR):F2}</color>");

            if (isWeapon && enhanceTable != null && enhanceLevel > 0)
            {
                int crit = enhanceTable.GetCritBonus(enhanceLevel);
                if (crit > 0) sb.AppendLine($"치명타 확률 <color=#ffffff>+{crit}%</color>");
            }
        }

        if (item.ItemType == "Consumable" && consumDetail != null)
        {
            sb.AppendLine();
            if (!string.IsNullOrEmpty(consumDetail.EffectType))
                sb.AppendLine($"효과: <color=#aaffaa>{PlayerStat.GetKoreanName(consumDetail.EffectType)}</color>");
            if (consumDetail.EffectValue != 0f)
                sb.AppendLine($"효과량: <color=#ffffff>{consumDetail.EffectValue:F2}</color>");
            if (consumDetail.EffectDuration > 0f)
                sb.AppendLine($"지속시간: <color=#ffffff>{consumDetail.EffectDuration:F1}초</color>");
            if (consumDetail.Cooltime > 0f)
                sb.AppendLine($"쿨타임: <color=#ffffff>{consumDetail.Cooltime:F1}초</color>");
        }

        sb.AppendLine();
        if (showSellPrice)
            sb.AppendLine($"판매가: <color=#aaaaaa>{item.Sell:N0} G</color>");
        else
            sb.AppendLine($"구매가: <color=#ffd700>{item.Buy:N0} G</color>");
        return sb.ToString().TrimEnd();
    }

    private void OnAction()
    {
        // 버리기 팝업이 열려 있으면 먼저 취소 — 장착/사용과 동시에 진행되면 아이템 복제 버그 발생
        var popup = InventoryManager.Instance != null ? InventoryManager.Instance.DiscardPopup : null;
        if (popup != null && popup.IsShowing) popup.OnClickCancel();

        if (_invData != null)
        {
            if (_invData.ItemInfo.ItemType == "Equipment")
                InventoryManager.Instance.EquipmentPanel.RequestEquipFromInventory(_invData);
            else
                InventoryManager.Instance.RequestUseFromInfo(_invData);
        }
        else if (_equipSlot != null)
        {
            InventoryManager.Instance.EquipmentPanel.RequestUnequip(_equipSlot.SlotType);
        }
    }

    private void OnQuickSlot()
    {
        if (_invData == null || QuickSlotManager.Instance == null) return;

        SoundManager.Instance.PlaySFX2D("ItemSelect");
        if (_invData.QuickslotIndex >= 0)
            QuickSlotManager.Instance.RequestUnassign(_invData);
        else
            QuickSlotManager.Instance.RequestAssign(_invData);
    }

    private void OnDiscard()
    {
        SoundManager.Instance.PlaySFX2D("ItemSelect");
        if (_invData != null)
            InventoryManager.Instance.ShowDiscardPopup(_invData);
    }

    private void OnSell()
    {
        if (_invData == null || ShopManager.Instance == null) return;

        SoundManager.Instance.PlaySFX2D("ItemSelect");

        var popup = InventoryManager.Instance?.ShopConfirmPopup;
        if (popup != null) popup.ShowSell(_invData);
        else ShopManager.Instance.SellItem(_invData, 1);
    }

    private void OnBuy()
    {
        if (_shopItem == null || _shopItemInfo == null || ShopManager.Instance == null) return;

        SoundManager.Instance.PlaySFX2D("ItemSelect");

        var popup = InventoryManager.Instance?.ShopConfirmPopup;
        if (popup != null) popup.ShowBuy(_shopItem, _shopItemInfo);
        else ShopManager.Instance.BuyItem(_shopItem, _shopItemInfo, 1);
    }

}
