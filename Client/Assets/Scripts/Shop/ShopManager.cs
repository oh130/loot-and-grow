using System.Collections;
using UnityEngine;

/// <summary>
/// 상점 시스템 싱글턴. 열림/닫힘 상태, 구매/판매 요청을 처리한다.
/// 골드는 DB 캐릭터 데이터 기준이며 InventoryManager가 표시를 담당한다.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── 열기/닫기 ────────────────────────────────────────────────────────

    public void Open()
    {
        IsOpen = true;
        // UI 표시는 ShopPanel.Open()이 UI_Menu.OpenShop(this)를 직접 호출
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        UI_Menu.Instance?.CloseShop();
    }

    public void ForceClose() => IsOpen = false;

    // ─── 구매 ───────────────────────────────────────────────────────────

    public void BuyItem(ShopItemResponse item, ItemEntry itemInfo, int quantity)
    {
        StartCoroutine(DoBuy(item, itemInfo, quantity));
    }

    private IEnumerator DoBuy(ShopItemResponse item, ItemEntry itemInfo, int quantity)
    {
        bool   success = false;
        string errMsg  = null;

        yield return StartCoroutine(DataAPI.BuyShopItem(
            UserSession.DbUserId,
            itemId:          itemInfo.ItemID,
            shopRotationId:  item.id,
            buyPrice:        itemInfo.Buy,
            itemType:        itemInfo.ItemType,
            quantity:        quantity,
            onSuccess: res  => { InventoryManager.Instance?.SetGold(res.gold); success = true; },
            onError:   err  => errMsg = err
        ));

        if (success)
        {
            if (item.is_random)
                InventoryManager.Instance?.ShopPanel?.RemoveSlot(item.id);
            InventoryManager.Instance?.Refresh();
            SoundManager.Instance.PlaySFX2D("BuySell");           
        }
        else if (errMsg != null)
        {
            string msg = errMsg.Contains("골드") ? "골드가 부족합니다." :
                         errMsg.Contains("꽉")   ? "가방이 꽉 찼습니다." : errMsg;
            InventoryManager.Instance?.ShowMessage(msg);
        }
    }

    // ─── 판매 ────────────────────────────────────────────────────────────

    public void SellItem(InventorySlotData data, int quantity)
    {
        StartCoroutine(DoSell(data, quantity));
    }

    private IEnumerator DoSell(InventorySlotData data, int quantity)
    {
        bool success = false;

        yield return StartCoroutine(DataAPI.SellItem(
            UserSession.DbUserId,
            inventoryId: data.InventoryId,
            sellPrice:   data.ItemInfo.Sell,
            quantity:    quantity,
            onSuccess: res => { InventoryManager.Instance?.SetGold(res.gold); success = true; },
            onError:   err => InventoryManager.Instance?.ShowMessage("판매 실패: " + err)
        ));

        if (success)
        {
            InventoryManager.Instance?.Refresh();
            SoundManager.Instance.PlaySFX2D("BuySell");           
        }
    }
}
