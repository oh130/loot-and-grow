import json
import os
from typing import Dict

_DATA_PATH = os.path.join(os.path.dirname(__file__), "data", "ItemTable.json")

# { item_id: max_stack } — 서버 시작 시 1회 로드
_max_stack_map: Dict[int, int] = {}


def _load() -> None:
    if not os.path.exists(_DATA_PATH):
        return  # gsheet_to_json.py 미실행 시 빈 맵으로 폴백
    with open(_DATA_PATH, encoding="utf-8") as f:
        rows = json.load(f)
    for row in rows:
        item_id   = row.get("ItemID")
        max_stack = row.get("MaxStack")
        # 0 또는 미설정은 "장비 기본값 1"로 처리 — 장비 아이템은 시트에 비워둬도 됨
        if item_id is not None and max_stack:
            _max_stack_map[int(item_id)] = int(max_stack)


_load()


def get_max_stack(item_id: int) -> int:
    """item_id에 해당하는 슬롯당 최대 보유 수량을 반환한다.
    시트에 값이 없으면 1 (장비 기본값)."""
    return _max_stack_map.get(item_id, 1)
