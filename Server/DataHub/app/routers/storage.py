from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import Storage, Inventory, User
from app.schemas import (
    StorageResponse, StorageItemResponse,
    StorageDepositRequest, StorageWithdrawRequest,
    InventoryItemResponse,
)
from app.jwt import get_current_user
from app import models, item_config

router = APIRouter(
    prefix="/storage",
    tags=["Storage"]
)

_MAX_SLOTS = 20


def _add_to_storage(db: Session, user_id: int, item_id: int, quantity: int,
                    enhance_level: int, max_stack: int) -> Storage:
    """창고에 아이템 추가. max_stack 초과 시 새 슬롯 생성. 첫 번째로 수정된 슬롯 반환."""
    remaining = quantity
    first_slot = None

    existing_slots = db.query(Storage).filter(
        Storage.user_id == user_id,
        Storage.item_id == item_id,
        Storage.enhance_level == enhance_level
    ).all()
    for slot in existing_slots:
        if remaining <= 0:
            break
        space = max_stack - slot.quantity
        if space <= 0:
            continue
        add_amt = min(remaining, space)
        slot.quantity += add_amt
        remaining -= add_amt
        if first_slot is None:
            first_slot = slot

    while remaining > 0:
        slot_count = db.query(Storage).filter(Storage.user_id == user_id).count()
        if slot_count >= _MAX_SLOTS:
            raise HTTPException(status_code=400, detail="창고가 꽉 찼습니다.")
        add_amt = min(remaining, max_stack)
        new_slot = Storage(
            user_id=user_id,
            item_id=item_id,
            quantity=add_amt,
            enhance_level=enhance_level,
        )
        db.add(new_slot)
        db.flush()
        remaining -= add_amt
        if first_slot is None:
            first_slot = new_slot

    return first_slot


@router.get("/{user_id}", response_model=StorageResponse)
def get_storage(user_id: int, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """창고 전체 조회"""
    items = db.query(Storage).filter(Storage.user_id == user_id).all()
    return StorageResponse(user_id=user_id, items=items)


@router.post("/deposit", response_model=StorageItemResponse)
def deposit(request: StorageDepositRequest, db: Session = Depends(get_db),
            current_user: models.User = Depends(get_current_user)):
    """인벤토리 → 창고 이동"""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인의 아이템만 이동할 수 있습니다.")

    inv_item = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id
    ).first()
    if not inv_item:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")
    if inv_item.quantity < request.quantity:
        raise HTTPException(status_code=400, detail="수량이 부족합니다.")

    max_stack = item_config.get_max_stack(inv_item.item_id)
    result = _add_to_storage(db, request.user_id, inv_item.item_id,
                             request.quantity, inv_item.enhance_level, max_stack)

    inv_item.quantity -= request.quantity
    if inv_item.quantity == 0:
        db.delete(inv_item)

    db.commit()
    db.refresh(result)
    return result


@router.post("/withdraw", response_model=InventoryItemResponse)
def withdraw(request: StorageWithdrawRequest, db: Session = Depends(get_db),
             current_user: models.User = Depends(get_current_user)):
    """창고 → 인벤토리 이동"""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인의 아이템만 이동할 수 있습니다.")

    storage_item = db.query(Storage).filter(
        Storage.id == request.storage_id,
        Storage.user_id == request.user_id
    ).first()
    if not storage_item:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")
    if storage_item.quantity < request.quantity:
        raise HTTPException(status_code=400, detail="수량이 부족합니다.")

    max_stack = item_config.get_max_stack(storage_item.item_id)
    remaining = request.quantity
    first_slot = None

    existing_slots = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.item_id == storage_item.item_id,
        Inventory.enhance_level == storage_item.enhance_level,
        Inventory.quickslot_index.is_(None)
    ).all()
    for slot in existing_slots:
        if remaining <= 0:
            break
        space = max_stack - slot.quantity
        if space <= 0:
            continue
        add_amt = min(remaining, space)
        slot.quantity += add_amt
        remaining -= add_amt
        if first_slot is None:
            first_slot = slot

    while remaining > 0:
        normal_count = db.query(Inventory).filter(
            Inventory.user_id == request.user_id,
            Inventory.quickslot_index.is_(None)
        ).count()
        if normal_count >= 20:
            raise HTTPException(status_code=400, detail="가방이 꽉 찼습니다.")
        add_amt = min(remaining, max_stack)
        new_slot = Inventory(
            user_id=request.user_id,
            item_id=storage_item.item_id,
            quantity=add_amt,
            enhance_level=storage_item.enhance_level,
        )
        db.add(new_slot)
        db.flush()
        remaining -= add_amt
        if first_slot is None:
            first_slot = new_slot

    storage_item.quantity -= request.quantity
    if storage_item.quantity == 0:
        db.delete(storage_item)

    db.commit()
    db.refresh(first_slot)
    return first_slot
