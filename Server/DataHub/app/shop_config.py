from typing import Dict, List
from app.shop_item_pool import (  # DataImporter 자동 생성
    EQUIPMENT_FIXED_POOL,
    EQUIPMENT_RANDOM_POOL,
    CONSUMABLE_FIXED_POOL,
    CONSUMABLE_RANDOM_POOL,
)

# 랜덤 슬롯 갱신 주기 (초)
ROTATION_SECONDS: int = 300

# 랜덤 슬롯 수 범위
RANDOM_SLOT_MIN: int = 1
RANDOM_SLOT_MAX: int = 3

# 상점 타입별 풀
FIXED_POOL: Dict[str, List[Dict]] = {
    "equipment":  EQUIPMENT_FIXED_POOL,
    "consumable": CONSUMABLE_FIXED_POOL,
}

RANDOM_POOL: Dict[str, List[Dict]] = {
    "equipment":  EQUIPMENT_RANDOM_POOL,
    "consumable": CONSUMABLE_RANDOM_POOL,
}
