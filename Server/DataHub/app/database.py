from sqlalchemy import create_engine
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker
import os

# 1. 도커 컨테이너 환경변수에서 DB 주소를 가져옵니다.
SQLALCHEMY_DATABASE_URL = os.getenv("DATABASE_URL", "mysql+pymysql://root:password1234@db:3306/my_rpg_db")

# 2. DB 엔진 생성 (실제 도로 건설)
engine = create_engine(
    SQLALCHEMY_DATABASE_URL,
    pool_recycle=280,      # MySQL 기본 wait_timeout(300s) 전에 연결 재사용
    pool_pre_ping=True,    # 쿼리 전 연결 살아있는지 확인 후 끊겼으면 재연결
)
# 3. DB와 대화할 창구(Session)를 만드는 공장 설정
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
# 4. 나중에 models.py에서 상속받을 기본 클래스
Base = declarative_base()

# DB 세션을 생성하고 닫아주는 의존성 함수
def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()