using Unity.Netcode;

/// <summary>
/// 런타임에서 사용하는 인벤토리 슬롯 데이터.
/// 정적(ItemEntry from SO) + 동적(서버 quantity)를 합친 것.
/// 서버 응답 클래스(InventoryItemResponse 등)는 ApiModels.cs에 있음.
/// </summary>
public class InventorySlotData
{
    public int InventoryId   { get; }
    public ItemEntry ItemInfo { get; }
    public int Quantity      { get; set; }
    public int EnhanceLevel  { get; }
    public int QuickslotIndex { get; } // -1 = 일반 인벤토리, 0~4 = 퀵슬롯

    public InventorySlotData(int inventoryId, ItemEntry itemInfo, int quantity, int enhanceLevel = 0, int quickslotIndex = -1)
    {
        InventoryId   = inventoryId;
        ItemInfo      = itemInfo;
        Quantity      = quantity;
        EnhanceLevel  = enhanceLevel;
        QuickslotIndex = quickslotIndex;
    }
}

// 전송용 데이터 (아이템 드롭용)
public struct DroppedItemData : INetworkSerializable
{
    public ItemEntryNS ItemInfo;
    public int Quantity;
    public int EnhanceLevel;

    public DroppedItemData(ItemEntryNS itemInfo, int quantity, int enhanceLevel = 0)
    {
        ItemInfo = itemInfo;
        Quantity = quantity;
        EnhanceLevel = enhanceLevel;
    }

    public DroppedItemData(InventorySlotData inventorySlotData)
    {
        ItemInfo = new ItemEntryNS(inventorySlotData.ItemInfo);
        Quantity = inventorySlotData.Quantity;
        EnhanceLevel = inventorySlotData.EnhanceLevel;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemInfo);
        serializer.SerializeValue(ref Quantity);
        serializer.SerializeValue(ref EnhanceLevel);
    }
}
