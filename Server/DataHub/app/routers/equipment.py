from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from typing import List
from app.database import get_db
from app.models import EquippedItem, Inventory, User
from app.schemas import EquippedItemResponse, EquipRequest, UnequipRequest
from app.jwt import get_current_user

router = APIRouter(
    prefix="/equipment",
    tags=["Equipment"]
)

@router.get("/{user_id}", response_model=List[EquippedItemResponse])
def get_equipped_items(user_id: int, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """유저의 장착 장비 목록 반환 (최대 5개: weapon/helmet/top/bottom/accessory)"""
    user = db.query(User).filter(User.id == user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다.")
    return db.query(EquippedItem).filter(EquippedItem.user_id == user_id).all()

@router.post("/equip", response_model=EquippedItemResponse)
def equip_item(request: EquipRequest, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """아이템 장착. 해당 슬롯에 이미 장착된 게 있으면 교체"""
    owned = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.item_id == request.item_id,
        Inventory.enhance_level == request.enhance_level
    ).first()
    if not owned:
        raise HTTPException(status_code=400, detail="보유하지 않은 아이템입니다.")

    existing = db.query(EquippedItem).filter(
        EquippedItem.user_id == request.user_id,
        EquippedItem.slot_type == request.slot_type
    ).first()

    if existing:
        existing.item_id = request.item_id
        existing.enhance_level = request.enhance_level
        db.commit()
        db.refresh(existing)
        return existing
    else:
        new_equip = EquippedItem(
            user_id=request.user_id,
            slot_type=request.slot_type,
            item_id=request.item_id,
            enhance_level=request.enhance_level
        )
        db.add(new_equip)
        db.commit()
        db.refresh(new_equip)
        return new_equip

@router.post("/unequip")
def unequip_item(request: UnequipRequest, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """해당 슬롯 장비 해제"""
    existing = db.query(EquippedItem).filter(
        EquippedItem.user_id == request.user_id,
        EquippedItem.slot_type == request.slot_type
    ).first()

    if not existing:
        raise HTTPException(status_code=404, detail="해당 슬롯에 장착된 아이템이 없습니다.")

    db.delete(existing)
    db.commit()
    return {"message": f"{request.slot_type} 슬롯 해제 완료"}
