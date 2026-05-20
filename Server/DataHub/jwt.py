from datetime import datetime, timedelta, timezone
from jose import JWTError, jwt
from fastapi import Depends, HTTPException
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from sqlalchemy.orm import Session
from app.database import get_db
from app import models

import os

# 토큰 서명에 사용하는 비밀 키 — 환경변수에서 읽음 (docker-compose.yml → .env)
SECRET_KEY = os.environ.get("SECRET_KEY", "capstone-crunch-secret-key")
ALGORITHM = "HS256"
ACCESS_TOKEN_EXPIRE_HOURS = 24  # 토큰 유효 시간

# Authorization: Bearer <token> 헤더를 자동으로 파싱해주는 도구
bearer_scheme = HTTPBearer()


def create_access_token(user_id: int) -> str:
    """로그인 성공 시 호출. user_id를 담은 JWT 토큰을 생성해 반환한다."""
    expire = datetime.now(timezone.utc) + timedelta(hours=ACCESS_TOKEN_EXPIRE_HOURS)
    payload = {"sub": str(user_id), "exp": expire}
    return jwt.encode(payload, SECRET_KEY, algorithm=ALGORITHM)


def get_current_user(
    credentials: HTTPAuthorizationCredentials = Depends(bearer_scheme),
    db: Session = Depends(get_db)
) -> models.User:
    """
    요청 헤더의 Bearer 토큰을 검증하고 해당 유저 객체를 반환한다.
    토큰이 없거나 만료/위조된 경우 401 에러를 반환한다.
    보호가 필요한 엔드포인트에 Depends(get_current_user)로 사용한다.
    """
    token = credentials.credentials
    try:
        payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
        user_id = int(payload.get("sub"))
    except (JWTError, TypeError, ValueError):
        raise HTTPException(status_code=401, detail="토큰이 유효하지 않습니다.")

    user = db.query(models.User).filter(models.User.id == user_id).first()
    if not user:
        raise HTTPException(status_code=401, detail="유저를 찾을 수 없습니다.")
    if user.is_banned:
        raise HTTPException(status_code=403, detail="정지된 계정입니다.")

    return user


def get_admin_user(current_user: models.User = Depends(get_current_user)) -> models.User:
    """get_current_user 위에 어드민 권한까지 검사한다."""
    if current_user.role != "admin":
        raise HTTPException(status_code=403, detail="어드민 권한이 필요합니다.")
    return current_user
