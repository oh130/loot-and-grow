import gspread
from google.oauth2.service_account import Credentials
import json
import os

# --- 설정 ---
# 구글 클라우드에서 받은 서비스 계정 키 파일 이름
SERVICE_ACCOUNT_FILE = "credentials.json"
# 사용자님의 스프레드시트 URL (또는 ID)
SPREADSHEET_URL = "https://docs.google.com/spreadsheets/d/187PEgWkGM9d0wKsibumV-zNdyT-bCEm79ZLA2-3Gqi8/edit"
# 결과물을 저장할 경로 (서버와 클라이언트가 참조할 수 있는 공통 경로)
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "Datas")
# ItemTable.json은 서버가 max_stack 조회에 사용 — 서버 data/ 에도 복사
SERVER_DATA_DIR = os.path.join(os.path.dirname(__file__), "..", "Server", "DataHub", "app", "data")

def export_sheets():
    # 1. 인증 및 구글 시트 열기
    scopes = ["https://www.googleapis.com/auth/spreadsheets", "https://www.googleapis.com/auth/drive"]
    creds = Credentials.from_service_account_file(SERVICE_ACCOUNT_FILE, scopes=scopes)
    client = gspread.authorize(creds)
    
    try:
        doc = client.open_by_url(SPREADSHEET_URL)
    except Exception as e:
        print(f"❌ 시트를 열 수 없습니다. URL이나 권한을 확인하세요: {e}")
        return

    # 2. 출력 폴더 생성
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)
    if not os.path.exists(SERVER_DATA_DIR):
        os.makedirs(SERVER_DATA_DIR)

    # 3. 모든 워크시트 순회
    for sheet in doc.worksheets():
        # 'rule', 'enums' 시트 무시
        if sheet.title.lower() in ('rule', 'enums'):
            print(f"⏩ Skipping '{sheet.title}' sheet.")
            continue

        print(f"📦 Exporting '{sheet.title}'...")
        
        # 첫 번째 줄을 Key로 하여 데이터 가져오기 (숫자 자동 변환 포함)
        data = sheet.get_all_records()

        # 숫자 컬럼의 빈 셀("")을 0으로 치환
        if data:
            numeric_cols = {key for row in data for key, val in row.items() if isinstance(val, (int, float))}
            for row in data:
                for key in numeric_cols:
                    if row[key] == "":
                        row[key] = 0

        # 파일 저장
        file_path = os.path.join(OUTPUT_DIR, f"{sheet.title}.json")
        with open(file_path, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=4)

        # ItemTable은 서버 data/ 에도 저장 (서버가 max_stack 조회에 사용)
        if sheet.title == "ItemTable":
            server_path = os.path.join(SERVER_DATA_DIR, "ItemTable.json")
            with open(server_path, "w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False, indent=4)
            print(f"   → {server_path} 에도 저장됨")

    print("\n✅ 모든 데이터가 JSON으로 성공적으로 변환되었습니다!")


if __name__ == "__main__":
    export_sheets()