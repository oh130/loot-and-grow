import random
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import Inventory, EquippedItem
from app.schemas import EnhanceRequest, EnhanceResponse
from app.jwt import get_current_user
from app import models, schemas

router = APIRouter(prefix="/enhance", tags=["Enhance"])

_MAX_ENHANCE = 20


def _do_enhance(request: EnhanceRequest, db: Session, current_user: models.User) -> schemas.EnhanceResponse:
    """무기·방어구 공통 강화 로직. 강화석 소모 → 성공 판정 → 실패 시 파괴 판정."""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인 계정으로만 강화할 수 있습니다.")

    target = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id,
    ).first()

    if not target:
        target = db.query(EquippedItem).filter(
            EquippedItem.id == request.inventory_id,
            EquippedItem.user_id == request.user_id,
        ).first()

    if not target:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")
    if target.enhance_level >= _MAX_ENHANCE:
        raise HTTPException(status_code=400, detail="최대 강화 단계입니다.")

    stones = db.query(Inventory).filter(
        Inventory.item_id == request.stone_item_id,
        Inventory.user_id == request.user_id,
    ).order_by(Inventory.id).all()

    total_stones = sum(s.quantity for s in stones)
    if total_stones < request.req_stone:
        raise HTTPException(status_code=400, detail="강화석이 부족합니다.")

    # 강화석 소모 — 슬롯을 순서대로 채워 씀 (30/30/30/10에서 50 소모 → 30/20)
    remaining = request.req_stone
    for stone in stones:
        if remaining <= 0:
            break
        consume = min(stone.quantity, remaining)
        stone.quantity -= consume
        remaining -= consume
        if stone.quantity == 0:
            db.delete(stone)

    current_level = target.enhance_level
    success = random.randint(1, 100) <= request.success_rate
    destroyed = False

    if success:
        target.enhance_level += 1
        new_level = target.enhance_level
    else:
        new_level = current_level
        if request.destroy_rate > 0 and random.randint(1, 100) <= request.destroy_rate:
            destroyed = True
            db.delete(target)

    db.commit()
    return schemas.EnhanceResponse(success=success, new_enhance_level=new_level, destroyed=destroyed)


@router.post("/weapon", response_model=schemas.EnhanceResponse)
def enhance_weapon(
    request: EnhanceRequest,
    db: Session = Depends(get_db),
    current_user: models.User = Depends(get_current_user),
):
    """무기 강화. 강화석 소모 후 확률 판정. 실패 시 destroy_rate에 따라 파괴될 수 있음."""
    return _do_enhance(request, db, current_user)


@router.post("/armor", response_model=schemas.EnhanceResponse)
def enhance_armor(
    request: EnhanceRequest,
    db: Session = Depends(get_db),
    current_user: models.User = Depends(get_current_user),
):
    """방어구 강화. 강화석 소모 후 확률 판정. 실패 시 destroy_rate에 따라 파괴될 수 있음."""
    return _do_enhance(request, db, current_user)
