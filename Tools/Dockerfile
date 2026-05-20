# 1. 파이썬 3.9 슬림 버전(가벼움)을 기반으로 시작
FROM python:3.9-slim

# 2. 컨테이너 내부의 작업 디렉토리를 /app으로 설정
WORKDIR /app

# 3. 라이브러리 설치를 위해 requirements.txt를 먼저 복사
COPY requirements.txt .

# 4. 필요한 라이브러리 설치 (캐시를 남기지 않아 용량 최적화)
RUN pip install --no-cache-dir -r requirements.txt

# 5. 현재 Tools 폴더의 모든 파일(스크립트, 키 등)을 컨테이너로 복사
COPY . .

# 6. 컨테이너가 실행되면 자동으로 파이썬 스크립트 실행
CMD ["python", "gsheet_to_json.py"]