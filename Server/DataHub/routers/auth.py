from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from passlib.context import CryptContext
from datetime import datetime, timezone
from app import models, schemas
from app.database import get_db
from app.jwt import create_access_token, get_current_user

# bcrypt 알고리즘으로 비밀번호를 암호화/검증하는 도구
pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")

# prefix="/auth" → 이 파일의 모든 경로 앞에 /auth가 붙음
router = APIRouter(prefix="/auth", tags=["Auth"])


# POST /auth/register — 회원가입
@router.post("/register", response_model=schemas.UserResponse)
def register_user(user: schemas.UserCreate, db: Session = Depends(get_db)):
    # 이미 존재하는 login_id인지 확인
    if db.query(models.User).filter(models.User.login_id == user.login_id).first():
        raise HTTPException(status_code=400, detail="이미 존재하는 아이디입니다.")

    new_user = models.User(
        login_id=user.login_id,
        hashed_password=pwd_context.hash(user.password),
        username=user.username,
    )
    db.add(new_user)
    db.commit()
    db.refresh(new_user)

    # 회원가입 시 캐릭터 자동 생성 (기본 스탯으로)
    new_character = models.Character(user_id=new_user.id)
    db.add(new_character)
    db.commit()

    return new_user


# POST /auth/login — 로그인
@router.post("/login", response_model=schemas.UserResponse)
def login_user(user: schemas.UserLogin, db: Session = Depends(get_db)):
    db_user = db.query(models.User).filter(models.User.login_id == user.login_id).first()

    # 아이디 없거나 비밀번호 불일치 시 거부
    if not db_user or not pwd_context.verify(user.password, db_user.hashed_password):
        raise HTTPException(status_code=400, detail="아이디 또는 비밀번호가 잘못되었습니다.")

    # 마지막 로그인 시각 갱신 (UTC 기준 현재 시각)
    db_user.last_login_at = datetime.now(timezone.utc)
    db.commit()
    db.refresh(db_user)

    # JWT 토큰 발급 후 응답에 포함
    token = create_access_token(db_user.id)
    response = schemas.UserResponse.model_validate(db_user)
    response.token = token
    return response


# GET /auth/verify — 토큰 유효성 검증 (게임 서버용)
@router.get("/verify", response_model=schemas.VerifyResponse)
def verify_token(current_user: models.User = Depends(get_current_user)):
    return schemas.VerifyResponse(id=current_user.id, username=current_user.username, role=current_user.role)
