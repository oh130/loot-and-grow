import random
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import Inventory, EquippedItem
from app.schemas import EnhanceRequest, EnhanceResponse
from app.jwt import get_current_user
from app import models

router = APIRouter(prefix="/enhance", tags=["Enhance"])

_MAX_ENHANCE = 20


@router.post("/weapon", response_model=EnhanceResponse)
def enhance_weapon(
    request: EnhanceRequest,
    db: Session = Depends(get_db),
    current_user: models.User = Depends(get_current_user),
):
    """무기 강화. 인벤토리 또는 장착 슬롯에서 무기를 찾아 강화석 소모 후 확률 판정. 성공 시 enhance_level +1."""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인 계정으로만 강화할 수 있습니다.")

    weapon = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id,
    ).first()

    equipped_weapon = None
    if not weapon:
        equipped_weapon = db.query(EquippedItem).filter(
            EquippedItem.id == request.inventory_id,
            EquippedItem.user_id == request.user_id,
        ).first()

    if not weapon and not equipped_weapon:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")

    target = weapon if weapon else equipped_weapon
    if target.enhance_level >= _MAX_ENHANCE:
        raise HTTPException(status_code=400, detail="최대 강화 단계입니다.")

    stone = db.query(Inventory).filter(
        Inventory.id == request.stone_inventory_id,
        Inventory.user_id == request.user_id,
    ).first()
    if not stone:
        raise HTTPException(status_code=404, detail="강화석 슬롯을 찾을 수 없습니다.")
    if stone.quantity < request.req_stone:
        raise HTTPException(status_code=400, detail="강화석이 부족합니다.")

    # 강화석 소모
    stone.quantity -= request.req_stone
    if stone.quantity == 0:
        db.delete(stone)

    # 확률 판정 (success_rate는 1~100 퍼센테이지)
    success = random.randint(1, 100) <= request.success_rate
    if success:
        target.enhance_level += 1

    db.commit()
    return EnhanceResponse(success=success, new_enhance_level=target.enhance_level)
