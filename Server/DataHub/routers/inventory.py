from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import Inventory, User
from app.schemas import (
    InventoryResponse, InventoryItemResponse,
    InventoryAddEquipmentRequest, InventoryAddItemRequest,
    InventoryDeleteRequest, InventoryConsumeRequest,
    QuickslotAssignRequest, QuickslotUnassignRequest,
)
from app.jwt import get_current_user, get_admin_user
from app import models, item_config

router = APIRouter(
    prefix="/inventory",
    tags=["Inventory"]
)

@router.get("/{user_id}", response_model=InventoryResponse)
def get_user_inventory(user_id: int, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """유저의 전체 인벤토리 반환 (일반 + 퀵슬롯 모두 포함)"""
    user = db.query(User).filter(User.id == user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다.")

    items = db.query(Inventory).filter(Inventory.user_id == user_id).all()
    return InventoryResponse(user_id=user_id, items=items)

@router.delete("/delete")
def delete_item(request: InventoryDeleteRequest, db: Session = Depends(get_db), current_user: models.User = Depends(get_current_user)):
    """인벤토리 슬롯 삭제"""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인의 아이템만 삭제할 수 있습니다.")

    item = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id
    ).first()
    if not item:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")

    db.delete(item)
    db.commit()
    return {"message": "아이템이 삭제되었습니다."}

@router.post("/consume", response_model=InventoryItemResponse)
def consume_item(request: InventoryConsumeRequest, db: Session = Depends(get_db), current_user: models.User = Depends(get_current_user)):
    """아이템 수량 소모. 수량이 0이 되면 슬롯 삭제 (퀵슬롯 아이템도 동일 처리)."""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인의 아이템만 소모할 수 있습니다.")

    item = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id
    ).first()
    if not item:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")
    if item.quantity < request.quantity:
        raise HTTPException(status_code=400, detail="수량이 부족합니다.")

    item.quantity -= request.quantity
    if item.quantity == 0:
        item_id_saved = item.item_id
        deleted_qs = item.quickslot_index
        db.delete(item)
        db.commit()

        if deleted_qs is not None:
            remaining = db.query(Inventory).filter(
                Inventory.user_id == request.user_id,
                Inventory.quickslot_index.isnot(None)
            ).order_by(Inventory.quickslot_index).all()
            for i, inv in enumerate(remaining):
                inv.quickslot_index = i
            db.commit()

        return InventoryItemResponse(id=request.inventory_id, item_id=item_id_saved, quantity=0)

    db.commit()
    db.refresh(item)
    return item

@router.delete("/admin/delete")
def admin_delete_item(request: InventoryDeleteRequest, db: Session = Depends(get_db), _: models.User = Depends(get_admin_user)):
    """어드민 전용: 타 유저의 인벤토리 슬롯 삭제"""
    item = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id
    ).first()
    if not item:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")

    db.delete(item)
    db.commit()
    return {"message": "아이템이 삭제되었습니다."}

@router.post("/add/equipment", response_model=InventoryItemResponse)
def add_equipment(request: InventoryAddEquipmentRequest, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """장비를 인벤토리에 추가. 항상 새 슬롯 생성 (스택 없음), enhance_level 보존.
    20칸 제한 적용 (픽업/장착 해제 공통).
    장비 교체 시에는 클라이언트가 먼저 새 장비 장착+인벤토리 삭제를 완료한 뒤 이 API를 호출해야
    슬롯 수가 유지되어 제한에 걸리지 않는다.
    """
    user = db.query(User).filter(User.id == request.user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다.")

    normal_count = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.quickslot_index.is_(None)
    ).count()
    if normal_count >= 20:
        raise HTTPException(status_code=400, detail="가방이 꽉 찼습니다.")

    new_item = Inventory(
        user_id=request.user_id,
        item_id=request.item_id,
        quantity=1,
        enhance_level=request.enhance_level
    )
    db.add(new_item)
    db.commit()
    db.refresh(new_item)
    return new_item

@router.post("/add/item", response_model=InventoryItemResponse)
def add_item(request: InventoryAddItemRequest, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """소모품/재료를 일반 인벤토리에 추가. max_stack 초과 시 새 슬롯 생성. 20칸 제한."""
    user = db.query(User).filter(User.id == request.user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다.")

    max_stack = item_config.get_max_stack(request.item_id)

    # 퀵슬롯에 같은 아이템이 있으면 거기에 먼저 스택 (max_stack 체크)
    quickslot_existing = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.item_id == request.item_id,
        Inventory.quickslot_index.isnot(None)
    ).first()
    if quickslot_existing and quickslot_existing.quantity < max_stack:
        space = max_stack - quickslot_existing.quantity
        quickslot_existing.quantity += min(request.quantity, space)
        db.commit()
        db.refresh(quickslot_existing)
        return quickslot_existing

    remaining = request.quantity
    first_slot = None

    # 기존 일반 슬롯들 채우기 (max_stack 미만인 슬롯부터)
    existing_slots = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.item_id == request.item_id,
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

    # 남은 수량은 새 슬롯 생성
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
            item_id=request.item_id,
            quantity=add_amt,
            enhance_level=0
        )
        db.add(new_slot)
        db.flush()
        remaining -= add_amt
        if first_slot is None:
            first_slot = new_slot

    db.commit()
    if first_slot:
        db.refresh(first_slot)
    return first_slot

@router.post("/add/quickslot", response_model=InventoryItemResponse)
def add_to_quickslot(request: InventoryAddItemRequest, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """소모품을 퀵슬롯에 직접 추가. 인벤토리가 꽉 찼을 때 소모품 픽업 시 사용.
    - 같은 item_id가 이미 퀵슬롯에 있으면 수량 합산
    - 없으면 빈 슬롯(0~4) 자동 배정
    """
    user = db.query(User).filter(User.id == request.user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다.")

    max_stack = item_config.get_max_stack(request.item_id)

    existing = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.item_id == request.item_id,
        Inventory.quickslot_index.isnot(None)
    ).first()
    if existing:
        existing.quantity = min(existing.quantity + request.quantity, max_stack)
        db.commit()
        db.refresh(existing)
        return existing

    used = {row.quickslot_index for row in db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.quickslot_index.isnot(None)
    ).all()}
    free_slot = next((i for i in range(5) if i not in used), None)
    if free_slot is None:
        raise HTTPException(status_code=400, detail="퀵슬롯이 꽉 찼습니다.")

    new_item = Inventory(
        user_id=request.user_id,
        item_id=request.item_id,
        quantity=min(request.quantity, max_stack),
        enhance_level=0,
        quickslot_index=free_slot
    )
    db.add(new_item)
    db.commit()
    db.refresh(new_item)
    return new_item

@router.put("/quickslot/assign", response_model=InventoryItemResponse)
def assign_quickslot(request: QuickslotAssignRequest, db: Session = Depends(get_db), current_user: models.User = Depends(get_current_user)):
    """인벤토리 소모품을 퀵슬롯에 등록. 클라이언트가 소모품 여부를 확인 후 호출.
    등록된 아이템은 20칸 카운트에서 제외됨.
    """
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인의 아이템만 수정할 수 있습니다.")
    if not (0 <= request.slot_index <= 4):
        raise HTTPException(status_code=400, detail="슬롯 인덱스는 0~4 사이여야 합니다.")

    item = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id
    ).first()
    if not item:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")
    if item.quickslot_index is not None:
        raise HTTPException(status_code=400, detail="이미 퀵슬롯에 등록된 아이템입니다.")

    occupied = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.quickslot_index == request.slot_index
    ).first()
    if occupied:
        raise HTTPException(status_code=400, detail="해당 퀵슬롯이 이미 사용 중입니다.")

    item.quickslot_index = request.slot_index
    db.commit()
    db.refresh(item)
    return item

@router.put("/quickslot/unassign", response_model=InventoryItemResponse)
def unassign_quickslot(request: QuickslotUnassignRequest, db: Session = Depends(get_db), current_user: models.User = Depends(get_current_user)):
    """퀵슬롯 해제. 아이템이 일반 인벤토리로 복귀 (20칸 카운트에 포함)."""
    if current_user.id != request.user_id:
        raise HTTPException(status_code=403, detail="본인의 아이템만 수정할 수 있습니다.")

    item = db.query(Inventory).filter(
        Inventory.id == request.inventory_id,
        Inventory.user_id == request.user_id
    ).first()
    if not item:
        raise HTTPException(status_code=404, detail="해당 아이템을 찾을 수 없습니다.")
    if item.quickslot_index is None:
        raise HTTPException(status_code=400, detail="퀵슬롯에 등록되지 않은 아이템입니다.")

    normal_count = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.quickslot_index.is_(None)
    ).count()
    if normal_count >= 20:
        raise HTTPException(status_code=400, detail="인벤토리가 꽉 찼습니다. 먼저 아이템을 비워주세요.")

    item.quickslot_index = None
    db.commit()

    remaining = db.query(Inventory).filter(
        Inventory.user_id == request.user_id,
        Inventory.quickslot_index.isnot(None)
    ).order_by(Inventory.quickslot_index).all()
    for i, inv in enumerate(remaining):
        inv.quickslot_index = i
    db.commit()

    db.refresh(item)
    return item
