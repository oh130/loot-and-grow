from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from typing import List
from app import models, schemas
from app.database import get_db
from app.jwt import get_current_user

router = APIRouter(prefix="/admin", tags=["Admin"])


def get_admin_user(requester_id: int, db: Session) -> models.User:
    """요청자가 실제 admin인지 확인하는 공통 함수. admin이 아니면 403 에러."""
    user = db.query(models.User).filter(models.User.id == requester_id).first()
    if not user or user.role != "admin":
        raise HTTPException(status_code=403, detail="어드민 권한이 없습니다.")
    return user


# GET /admin/users?requester_id=1 — 전체 유저 목록 조회
# requester_id: 이 API를 호출하는 어드민 유저의 id
@router.get("/users", response_model=List[schemas.UserResponse])
def get_all_users(requester_id: int, db: Session = Depends(get_db), _=Depends(get_current_user)):
    get_admin_user(requester_id, db)
    return db.query(models.User).all()


# PUT /admin/users/{user_id}/ban — 특정 유저의 밴 상태 변경
# Body 예시: {"is_banned": true}
@router.put("/users/{user_id}/ban", response_model=schemas.UserResponse)
def update_ban(user_id: int, data: schemas.BanUpdate, requester_id: int, db: Session = Depends(get_db), _=Depends(get_current_user)):
    get_admin_user(requester_id, db)

    target = db.query(models.User).filter(models.User.id == user_id).first()
    if not target:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다.")

    target.is_banned = data.is_banned
    db.commit()
    db.refresh(target)
    return target


# DELETE /admin/users/{user_id} — 특정 유저 계정 삭제 (관련 데이터 전부 삭제)
@router.delete("/users/{user_id}")
def delete_user(user_id: int, requester_id: int, db: Session = Depends(get_db), _=Depends(get_current_user)):
    get_admin_user(requester_id, db)

    target = db.query(models.User).filter(models.User.id == user_id).first()
    if not target:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다.")
    if target.role == "admin":
        raise HTTPException(status_code=403, detail="어드민 계정은 삭제할 수 없습니다.")

    db.query(models.EquippedItem).filter(models.EquippedItem.user_id == user_id).delete()
    db.query(models.Inventory).filter(models.Inventory.user_id == user_id).delete()
    db.query(models.Character).filter(models.Character.user_id == user_id).delete()
    db.delete(target)
    db.commit()
    return {"message": f"유저 {user_id}번 계정이 삭제되었습니다."}


# PUT /admin/users/{user_id}/role — 특정 유저의 권한 변경
# Body 예시: {"role": "admin"}
@router.put("/users/{user_id}/role", response_model=schemas.UserResponse)
def update_role(user_id: int, data: schemas.RoleUpdate, requester_id: int, db: Session = Depends(get_db), _=Depends(get_current_user)):
    get_admin_user(requester_id, db)

    if data.role not in ("user", "admin"):
        raise HTTPException(status_code=400, detail="role은 'user' 또는 'admin'이어야 합니다.")

    target = db.query(models.User).filter(models.User.id == user_id).first()
    if not target:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다.")

    target.role = data.role
    db.commit()
    db.refresh(target)
    return target
