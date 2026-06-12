import random
from datetime import datetime, timezone, timedelta
from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import ShopRotation, Character, Inventory
from app.schemas import (
    ShopItemsResponse, ShopItemResponse,
    ShopBuyRequest, ShopBuyResponse,
    ShopSellRequest, ShopSellResponse,
)
from app.shop_config import (
    ROTATION_SECONDS,
    RANDOM_SLOT_MIN, RANDOM_SLOT_MAX,
    FIXED_POOL, RANDOM_POOL,
)
from app.jwt import get_current_user
from app import models

router = APIRouter(prefix="/shop", tags=["Shop"])

_MAX_INV   = 20
_MAX_QUICK = 5

_VALID_SHOP_TYPES = {"equipment", "consumable"}


def _now_naive() -> datetime:
    return datetime.now(timezone.utc).replace(tzinfo=None)


def _regenerate(db: Session, shop_type: str) -> list:
    """해당 shop_type의 랜덤 로테이션을 재생성한다."""
    db.query(ShopRotation).filter(
        ShopRotation.shop_type == shop_type,
        ShopRotation.is_random == True,
    ).delete()

    pool = RANDOM_POOL.get(shop_type, [])
    if not pool:
        db.commit()
        return []

    expires_at = _now_naive() + timedelta(seconds=ROTATION_SECONDS)
    count = random.randint(RANDOM_SLOT_MIN, min(RANDOM_SLOT_MAX, len(pool)))
    chosen = random.sample(pool, k=count)

    for entry in chosen:
        db.add(ShopRotation(
            item_id=entry["item_id"],
            shop_type=shop_type,
            is_random=True,
            expires_at=expires_at,
        ))
    db.commit()
    return db.query(ShopRotation).filter(
        ShopRotation.shop_type == shop_type,
        ShopRotation.is_random == True,
    ).all()


@router.get("/items", response_model=ShopItemsResponse)
def get_shop_items(
    shop_type: str = Query(..., description="'equipment' 또는 'consumable'"),
    db: Session = Depends(get_db),
    _=Depends(get_current_user),
):
    """상점 아이템 목록 반환. 고정 아이템 + 랜덤 로테이션 아이템을 합쳐서 반환."""
    if shop_type not in _VALID_SHOP_TYPES:
        raise HTTPException(status_code=400, detail="shop_type은 'equipment' 또는 'consumable'이어야 합니다.")

    # 고정 아이템 (항상 판매)
    fixed = [
        ShopItemResponse(id=0, item_id=entry["item_id"], is_random=False)
        for entry in FIXED_POOL.get(shop_type, [])
    ]

    # 랜덤 아이템 (만료 시 재생성)
    now = _now_naive()
    random_rows = db.query(ShopRotation).filter(
        ShopRotation.shop_type == shop_type,
        ShopRotation.is_random == True,
    ).all()

    if not random_rows or random_rows[0].expires_at <= now:
        random_rows = _regenerate(db, shop_type)

    seconds_remaining = (
        max(0.0, (random_rows[0].expires_at - now).total_seconds())
        if random_rows else 0.0
    )

    random_items = [
        ShopItemResponse(id=row.id, item_id=row.item_id, is_random=True)
        for row in random_rows
    ]

    return ShopItemsResponse(
        items=fixed + random_items,
        seconds_remaining=seconds_remaining,
    )


@router.post("/buy", response_model=ShopBuyResponse)
def buy_item(
    request: ShopBuyRequest,
    db: Session = Depends(get_db),
    current_user: models.User = Depends(get_current_user),
):
    """아이템 구매. 고정 아이템(shop_rotation_id=0)과 랜덤 아이템 모두 처리."""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인 계정으로만 구매할 수 있습니다.")

    quantity = max(1, request.quantity)
    item_id  = request.item_id
    is_random = request.shop_rotation_id > 0

    if is_random:
        shop_item = db.query(ShopRotation).filter(
            ShopRotation.id == request.shop_rotation_id,
            ShopRotation.is_random == True,
        ).first()
        if not shop_item:
            raise HTTPException(status_code=404, detail="해당 상점 아이템을 찾을 수 없습니다.")
        if shop_item.expires_at <= _now_naive():
            raise HTTPException(status_code=400, detail="상점이 갱신되었습니다. 다시 확인해주세요.")
        if shop_item.item_id != item_id:
            raise HTTPException(status_code=400, detail="아이템 정보가 일치하지 않습니다.")
        quantity = 1  # 랜덤 아이템은 항상 1개

    character = db.query(Character).filter(Character.user_id == request.user_id).first()
    if not character:
        raise HTTPException(status_code=404, detail="캐릭터를 찾을 수 없습니다.")

    total_price = request.buy_price * quantity
    if character.gold < total_price:
        raise HTTPException(status_code=400, detail="골드가 부족합니다.")

    if request.item_type == "Equipment":
        # 장비는 quantity만큼 개별 슬롯 생성
        for _ in range(quantity):
            inv_count = db.query(Inventory).filter(
                Inventory.user_id == request.user_id,
                Inventory.quickslot_index.is_(None),
            ).count()
            if inv_count >= _MAX_INV:
                raise HTTPException(status_code=400, detail="가방이 꽉 찼습니다.")
            db.add(Inventory(user_id=request.user_id, item_id=item_id, quantity=1, enhance_level=0))
    else:
        # 소모품은 기존 스택에 합산
        existing = db.query(Inventory).filter(
            Inventory.user_id == request.user_id,
            Inventory.item_id == item_id,
        ).first()
        if existing:
            existing.quantity += quantity
        else:
            inv_count = db.query(Inventory).filter(
                Inventory.user_id == request.user_id,
                Inventory.quickslot_index.is_(None),
            ).count()
            if inv_count < _MAX_INV:
                db.add(Inventory(user_id=request.user_id, item_id=item_id, quantity=quantity, enhance_level=0))
            else:
                used = {r.quickslot_index for r in db.query(Inventory).filter(
                    Inventory.user_id == request.user_id,
                    Inventory.quickslot_index.isnot(None),
                ).all()}
                free = next((i for i in range(_MAX_QUICK) if i not in used), None)
                if free is None:
                    raise HTTPException(status_code=400, detail="가방이 꽉 찼습니다.")
                db.add(Inventory(
                    user_id=request.user_id, item_id=item_id,
                    quantity=quantity, enhance_level=0, quickslot_index=free,
                ))

    character.gold -= total_price

    if is_random:
        db.delete(shop_item)  # 랜덤 아이템은 구매 즉시 로테이션에서 제거

    db.commit()
    return ShopBuyResponse(gold=character.gold)


@router.post("/sell", response_model=ShopSellResponse)
def sell_item(
    request: ShopSellRequest,
    db: Session = Depends(get_db),
    current_user: models.User = Depends(get_current_user),
):
    """아이템 판매. quantity만큼 수량 감소(0이면 슬롯 삭제). 판매 총액 반환."""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인 계정으로만 판매할 수 있습니다.")

    inv_item = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id,
    ).first()
    if not inv_item:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")

    character = db.query(Character).filter(Character.user_id == request.user_id).first()
    if not character:
        raise HTTPException(status_code=404, detail="캐릭터를 찾을 수 없습니다.")

    unit_price   = request.sell_price
    actual_qty   = min(request.quantity, inv_item.quantity)
    sell_total   = unit_price * actual_qty

    if actual_qty >= inv_item.quantity:
        db.delete(inv_item)
    else:
        inv_item.quantity -= actual_qty

    character.gold += sell_total
    db.commit()
    return ShopSellResponse(gold=character.gold, sell_price=sell_total)
