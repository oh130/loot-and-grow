from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import Character
from app.schemas import CharacterResponse, CharacterUpdate
from app.jwt import get_current_user, get_admin_user
from app import models

router = APIRouter(
    prefix="/character",
    tags=["Character"]
)

@router.get("/{user_id}", response_model=CharacterResponse)
def get_character(user_id: int, db: Session = Depends(get_db), _=Depends(get_current_user)):
    """유저의 캐릭터 정보 반환. 캐릭터가 없으면 기본값으로 자동 생성."""
    character = db.query(Character).filter(Character.user_id == user_id).first()
    if not character:
        character = Character(user_id=user_id)
        db.add(character)
        db.commit()
        db.refresh(character)
    return character

@router.put("/{user_id}", response_model=CharacterResponse)
def update_character(user_id: int, data: CharacterUpdate, db: Session = Depends(get_db), _: models.User = Depends(get_admin_user)):
    """캐릭터 스탯/위치 저장. 어드민(게임 서버) 전용. None인 필드는 건드리지 않음"""
    character = db.query(Character).filter(Character.user_id == user_id).first()
    if not character:
        raise HTTPException(status_code=404, detail="캐릭터를 찾을 수 없습니다.")

    # None 및 C# 센티넬(-1) 제외 후 업데이트
    for field, value in data.model_dump(exclude_none=True).items():
        if isinstance(value, (int, float)) and value < 0:
            continue
        setattr(character, field, value)

    db.commit()
    db.refresh(character)
    return character
