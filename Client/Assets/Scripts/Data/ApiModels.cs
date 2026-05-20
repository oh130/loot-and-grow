using System;
using System.Collections.Generic;

// ─── Auth ─────────────────────────────────────────────────────────────────
// schemas.py UserResponse와 일치

[Serializable]
public class LoginResponse
{
    public int id;
    public string login_id;
    public string username;
    public string role;
    public bool is_banned;
    public string last_login_at;
    public string token;
}

// GET /auth/verify 응답 — 게임 서버가 클라이언트 토큰을 검증할 때 사용
[Serializable]
public class VerifyResponse
{
    public int id;
    public string username;
    public string role;
}

// ─── Inventory ─────────────────────────────────────────────────────────────

[Serializable]
public class InventoryItemResponse
{
    public int id;
    public int item_id;
    public int quantity;
    public int enhance_level;
    public int quickslot_index; // -1 = 일반 인벤토리, 0~4 = 퀵슬롯 위치
}

[Serializable]
public class InventoryResponse
{
    public int user_id;
    public List<InventoryItemResponse> items;
}

// ─── Character ─────────────────────────────────────────────────────────────

[Serializable]
public class CharacterResponse
{
    public int id;
    public int user_id;
    public float current_hp;
    public int gold;
    public float pos_x;
    public float pos_y;
    public float pos_z;
}

[Serializable]
public class CharacterUpdateData
{
    public float current_hp = -1;
    public int gold = -1;
    public float pos_x;
    public float pos_y;
    public float pos_z;
}

// ─── Equipment ─────────────────────────────────────────────────────────────

[Serializable]
public class EquippedItemResponse
{
    public int id;
    public string slot_type;
    public int item_id;
    public int enhance_level;
}

// JsonUtility는 최상위 배열 파싱 불가 → wrapper로 감쌈
[Serializable]
public class EquippedItemListResponse
{
    public List<EquippedItemResponse> items;
}

// ─── Enhance ───────────────────────────────────────────────────────────────

[Serializable]
public class EnhanceResponse
{
    public bool success;
    public int  new_enhance_level;
}

// ─── Storage ───────────────────────────────────────────────────────────────

[Serializable]
public class StorageItemResponse
{
    public int id;
    public int item_id;
    public int quantity;
    public int enhance_level;
}

[Serializable]
public class StorageResponse
{
    public int user_id;
    public List<StorageItemResponse> items;
}

// ─── Shop ──────────────────────────────────────────────────────────────────

[Serializable]
public class ShopItemResponse
{
    public int  id;        // ShopRotation row id (고정 아이템은 0)
    public int  item_id;
    public bool is_random; // true=구매 후 사라짐, false=항상 판매
}

[Serializable]
public class ShopItemsResponse
{
    public List<ShopItemResponse> items;
    public float seconds_remaining;
}

[Serializable]
public class ShopBuyResponse
{
    public int gold;
}

[Serializable]
public class ShopSellResponse
{
    public int gold;
    public int sell_price;
}
