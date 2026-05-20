# Loot & Grow

**Unity 멀티플레이어 액션 RPG 프로젝트**  
몬스터를 사냥하고, 다른 플레이어를 약탈하고, 장비를 강화하며 성장하는 PVE/PVP 혼합형 게임.  
4인 팀 · 모바일(Android) 빌드.

> Unity 6 · Netcode for GameObjects · FastAPI · Docker · AWS EC2

---

## 스크린샷

<!-- 스크린샷 추가 예정 -->

---

## 기술 스택

| 영역 | 기술 |
|------|------|
| 클라이언트 | Unity 6 (URP), C#, Netcode for GameObjects |
| 서버 | Python, FastAPI, SQLAlchemy, MySQL |
| 인프라 | Docker, AWS EC2, GitHub Actions |
| 도구 | Google Sheets API |

---

## 담당 파트

### 서버 — DataHub 전체
동적 데이터(계정 정보, 보유 아이템, 장비 장착 상태, 상점 상태 등)를 처리하는 FastAPI 서버를 단독 설계·구현.

- REST API 엔드포인트 설계
- SQLAlchemy ORM으로 DB 스키마 정의
- env 파일, API key 등의 보안 유지

### 클라이언트 — 데이터 관련 시스템 전담
데이터와 직접적인 연관을 갖는 시스템들에 대한 로직과 UI를 클라이언트에서 구현.

- DataAPI.cs: Unity 클라이언트에서의 API 사용 Helper Script, 팀원들을 고려하여 주석을 상세히 작성
- 회원가입 및 로그인 인증
- 인벤토리 · 장비 · 상점 · 강화 · 창고 시스템

### 데이터 파이프라인 — 정적 데이터 자동화
적 · 아이템 · 장비 · 강화 수치 등 기획 데이터를 Google Sheets에서 관리하고,  
Docker 기반 Python 스크립트로 JSON 변환 후 서버와 ScriptableObject에 자동 반영.

```
Google Sheets → gsheet_to_json.py (Docker) → ItemTable.json
→ ScriptableObject (Unity Editor Tool)
```

### 인프라 — 배포 자동화
DataHub 서버를 AWS EC2 + Docker 환경에서 운영.  
`main` branch에 push 시 GitHub Actions가 SSH로 EC2에 접속해 자동 서버 재배포.

---

## 프로젝트 구조

```
Client/Assets/Scripts/
├── Inventory/      # 인벤토리 슬롯, 아이템 정보 패널, 버리기 팝업
├── Storage/        # 창고 패널 (싱글턴, 서버 연동)
├── Shop/           # 상점 패널, 구매/판매
├── Enhance/        # 강화 패널
├── System/         # DataAPI, GraphicsManager, PoolManager
├── Player/         # 이동, 전투, 장비 비주얼
├── UI/             # HUD, 메뉴, 채팅, 미니맵
└── ...

Server/DataHub/     # FastAPI 서버 (DataHub)
Tools/              # Google Sheets → JSON 변환 스크립트
.github/workflows/  # GitHub Actions 배포 자동화
```
