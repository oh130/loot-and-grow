# 1. 파이썬 환경 설치
FROM python:3.11-slim

# 2. 컨테이너 안의 작업 공간 만들기
WORKDIR /code

# 3. 라이브러리 설치 (requirements.txt가 같은 폴더에 있어야 함)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# 4. 내 소스 코드 전부 복사
COPY . .

# 5. 서버 실행 (app 폴더 안의 main.py 실행)
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]