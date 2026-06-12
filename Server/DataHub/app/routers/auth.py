from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from passlib.context import CryptContext
from datetime import datetime, timezone
import httpx
from app import models, schemas
from app.database import get_db
from app.jwt import create_access_token, create_refresh_token, get_refresh_token_expiry, get_current_user

pwd_context = CryptContext(schemes=["bcrypt"], deprecated="auto")

router = APIRouter(prefix="/auth", tags=["Auth"])


def _issue_tokens(db_user: models.User, db: Session) -> schemas.UserResponse:
    """Access Token + Refresh Token 발급 후 DB에 저장하고 UserResponse로 반환.
    Refresh Token은 아직 유효하면 재사용 — 로그인마다 rotate하면 다른 세션(전용 서버 등)의
    토큰이 무효화되는 문제가 생기기 때문.
    """
    token = create_access_token(db_user.id)

    now = datetime.now(timezone.utc)
    expires = db_user.refresh_token_expires_at
    if expires is not None and expires.tzinfo is None:
        expires = expires.replace(tzinfo=timezone.utc)
    if not db_user.refresh_token or expires is None or expires <= now:
        refresh = create_refresh_token()
        db_user.refresh_token = refresh
        db_user.refresh_token_expires_at = get_refresh_token_expiry()
    else:
        refresh = db_user.refresh_token

    db_user.last_login_at = now
    db.commit()
    db.refresh(db_user)
    response = schemas.UserResponse.model_validate(db_user)
    response.token = token
    response.refresh_token = refresh
    return response


# POST /auth/register — 회원가입
@router.post("/register", response_model=schemas.UserResponse)
def register_user(user: schemas.UserCreate, db: Session = Depends(get_db)):
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

    db.add(models.Character(user_id=new_user.id))
    db.commit()

    return new_user


# POST /auth/login — 로그인
@router.post("/login", response_model=schemas.UserResponse)
def login_user(user: schemas.UserLogin, db: Session = Depends(get_db)):
    db_user = db.query(models.User).filter(models.User.login_id == user.login_id).first()
    if not db_user or not pwd_context.verify(user.password, db_user.hashed_password):
        raise HTTPException(status_code=400, detail="아이디 또는 비밀번호가 잘못되었습니다.")
    return _issue_tokens(db_user, db)


# GET /auth/verify — 토큰 유효성 검증 (게임 서버용)
@router.get("/verify", response_model=schemas.VerifyResponse)
def verify_token(current_user: models.User = Depends(get_current_user)):
    return schemas.VerifyResponse(id=current_user.id, username=current_user.username, role=current_user.role)


# POST /auth/refresh — Refresh Token으로 새 Access Token 발급
@router.post("/refresh", response_model=schemas.UserResponse)
def refresh_token(req: schemas.RefreshRequest, db: Session = Depends(get_db)):
    user = db.query(models.User).filter(models.User.refresh_token == req.refresh_token).first()
    if not user:
        raise HTTPException(status_code=401, detail="유효하지 않은 Refresh Token입니다.")
    expires = user.refresh_token_expires_at
    if expires is not None and expires.tzinfo is None:
        expires = expires.replace(tzinfo=timezone.utc)
    if expires is None or expires <= datetime.now(timezone.utc):
        raise HTTPException(status_code=401, detail="Refresh Token이 만료됐습니다. 다시 로그인해주세요.")
    if user.is_banned:
        raise HTTPException(status_code=403, detail="정지된 계정입니다.")
    return _issue_tokens(user, db)


# POST /auth/google — Google 로그인 (신규 유저면 자동 가입)
@router.post("/google", response_model=schemas.UserResponse)
def google_login(req: schemas.GoogleLoginRequest, db: Session = Depends(get_db)):
    r = httpx.get(f"https://oauth2.googleapis.com/tokeninfo?id_token={req.id_token}")
    if r.status_code != 200:
        raise HTTPException(status_code=400, detail="유효하지 않은 Google 토큰입니다.")

    google_data = r.json()
    google_id = google_data.get("sub")
    name      = google_data.get("name") or google_data.get("email", "Google유저")

    if not google_id:
        raise HTTPException(status_code=400, detail="Google 토큰에서 유저 정보를 가져올 수 없습니다.")

    user = db.query(models.User).filter(models.User.google_id == google_id).first()
    if not user:
        user = models.User(
            login_id        = f"google_{google_id}",
            hashed_password = "GOOGLE_AUTH_ONLY",
            username        = name,
            google_id       = google_id,
        )
        db.add(user)
        db.commit()
        db.refresh(user)
        db.add(models.Character(user_id=user.id))
        db.commit()

    return _issue_tokens(user, db)
