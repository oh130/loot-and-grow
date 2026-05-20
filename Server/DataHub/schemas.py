from pydantic import BaseModel, validator
from datetime import datetime
from typing import Optional, List

# ─── 인증 관련 ───────────────────────────────────────────

# 회원가입 시 유니티 → 서버로 보내는 데이터
class UserCreate(BaseModel):
    login_id: str
    password: str
    username: str

# 로그인 시 유니티 → 서버로 보내는 데이터
class UserLogin(BaseModel):
    login_id: str
    password: str

# 로그인/회원가입 성공 시 서버 → 유니티로 돌려주는 데이터
# (비밀번호는 절대 포함하지 않음)
class UserResponse(BaseModel):
    id: int
    login_id: str
    username: str
    role: str
    is_banned: bool
    last_login_at: Optional[datetime] = None
    token: Optional[str] = None  # 로그인 시 발급되는 JWT 토큰 (회원가입 응답에는 None)

    class Config:
        from_attributes = True

# 게임 서버가 토큰 유효성 검증 시 받는 응답
class VerifyResponse(BaseModel):
    id: int
    username: str
    role: str

# ─── 인벤토리 관련 ───────────────────────────────────────

# 서버 → 유니티: 인벤토리 슬롯 하나
class InventoryItemResponse(BaseModel):
    id: int
    item_id: int
    quantity: int
    enhance_level: int = 0
    quickslot_index: int = -1  # -1 = 일반 인벤토리, 0~4 = 퀵슬롯 위치

    @validator("quickslot_index", pre=True, always=True)
    def coerce_null_to_minus_one(cls, v):
        return v if v is not None else -1

    class Config:
        from_attributes = True

# 서버 → 유니티: 인벤토리 전체
class InventoryResponse(BaseModel):
    user_id: int
    items: List[InventoryItemResponse]

# 유니티 → 서버: 장비 아이템 인벤토리 추가 (장착 해제/교체 시)
# - 항상 새 슬롯 생성, 20칸 제한 없음, enhance_level 보존
class InventoryAddEquipmentRequest(BaseModel):
    user_id: int
    item_id: int
    enhance_level: int = 0

# 유니티 → 서버: 소모품/재료 인벤토리 추가 (루팅, 구매 등)
# - 같은 item_id 슬롯에 수량 합산, 20칸 제한 있음
class InventoryAddItemRequest(BaseModel):
    user_id: int
    item_id: int
    quantity: int = 1

# 유니티 → 서버: 퀵슬롯 등록 (인벤토리 아이템에 quickslot_index 설정)
class QuickslotAssignRequest(BaseModel):
    user_id: int
    inventory_id: int
    slot_index: int  # 0~4

# 유니티 → 서버: 퀵슬롯 해제 (quickslot_index → null, 일반 인벤토리로 복귀)
class QuickslotUnassignRequest(BaseModel):
    user_id: int
    inventory_id: int

# 유니티 → 서버: 아이템 삭제 요청
class InventoryDeleteRequest(BaseModel):
    user_id: int       # 본인 아이템인지 확인용
    inventory_id: int  # inventories 테이블의 row id (InventoryItemResponse.id)

# 유니티 → 서버: 아이템 소모 요청 (수량 감소, 0이 되면 슬롯 삭제)
class InventoryConsumeRequest(BaseModel):
    user_id: int
    inventory_id: int
    quantity: int = 1

# ─── 캐릭터 관련 ─────────────────────────────────────────

class CharacterResponse(BaseModel):
    id: int
    user_id: int
    current_hp: int
    gold: int
    pos_x: float
    pos_y: float
    pos_z: float

    class Config:
        from_attributes = True

class CharacterUpdate(BaseModel):
    current_hp: Optional[int] = None
    gold: Optional[int] = None
    pos_x: Optional[float] = None
    pos_y: Optional[float] = None
    pos_z: Optional[float] = None

# ─── 장착 장비 관련 ───────────────────────────────────────

class EquippedItemResponse(BaseModel):
    id: int
    slot_type: str
    item_id: int
    enhance_level: int = 0

    class Config:
        from_attributes = True

class EquipRequest(BaseModel):
    user_id: int
    slot_type: str  # "weapon" / "helmet" / "top" / "bottom" / "accessory"
    item_id: int
    enhance_level: int = 0

class UnequipRequest(BaseModel):
    user_id: int
    slot_type: str

# ─── 상점 관련 ───────────────────────────────────────────

# 서버 → 유니티: 상점 슬롯 하나
class ShopItemResponse(BaseModel):
    id: int            # ShopRotation row id (고정 아이템은 0)
    item_id: int
    is_random: bool    # True=랜덤(구매 후 사라짐), False=고정(항상 판매)

    class Config:
        from_attributes = True

# 서버 → 유니티: 상점 전체 목록
class ShopItemsResponse(BaseModel):
    items: List[ShopItemResponse]
    seconds_remaining: float  # 랜덤 아이템 갱신까지 남은 초

# 유니티 → 서버: 아이템 구매
class ShopBuyRequest(BaseModel):
    user_id: int
    item_id: int           # 구매할 아이템 ID
    shop_rotation_id: int  # 고정 아이템=0, 랜덤 아이템=ShopRotation.id
    buy_price: int         # 클라이언트 SO 기반 단가 (서버 신뢰)
    item_type: str         # "Equipment" / "Consumable"
    quantity: int = 1      # 구매 수량 (랜덤 아이템은 항상 1)

# 서버 → 유니티: 구매 결과
class ShopBuyResponse(BaseModel):
    gold: int              # 구매 후 남은 골드

# 유니티 → 서버: 아이템 판매
class ShopSellRequest(BaseModel):
    user_id: int
    inventory_id: int
    sell_price: int        # 클라이언트 ItemEntry.Sell 기반 단가 (서버 신뢰)
    quantity: int = 1      # 판매 수량

# 서버 → 유니티: 판매 결과
class ShopSellResponse(BaseModel):
    gold: int              # 판매 후 남은 골드
    sell_price: int        # 이번 거래 총 판매액

# ─── 창고 관련 ───────────────────────────────────────────

class StorageItemResponse(BaseModel):
    id: int
    item_id: int
    quantity: int
    enhance_level: int = 0

    class Config:
        from_attributes = True

class StorageResponse(BaseModel):
    user_id: int
    items: List[StorageItemResponse]

class StorageDepositRequest(BaseModel):
    user_id: int
    inventory_id: int
    quantity: int = 1

class StorageWithdrawRequest(BaseModel):
    user_id: int
    storage_id: int
    quantity: int = 1

# ─── 강화 관련 ────────────────────────────────────────────

# 유니티 → 서버: 무기 강화 요청
class EnhanceRequest(BaseModel):
    user_id: int
    inventory_id: int       # 강화할 무기 슬롯 (inventories.id)
    stone_inventory_id: int # 강화석 슬롯 (inventories.id)
    success_rate: int       # 클라이언트 EnhanceTable 기반 성공률 % (1~100, 서버 신뢰)
    req_stone: int          # 클라이언트 EnhanceTable 기반 소모 강화석 수 (서버 신뢰)

# 서버 → 유니티: 강화 결과
class EnhanceResponse(BaseModel):
    success: bool
    new_enhance_level: int

# ─── 어드민 관련 ──────────────────────────────────────────

# 어드민이 다른 유저의 role을 바꿀 때 보내는 데이터
class RoleUpdate(BaseModel):
    role: str  # "user" 또는 "admin"

# 어드민이 다른 유저의 밴 상태를 바꿀 때 보내는 데이터
class BanUpdate(BaseModel):
    is_banned: bool
