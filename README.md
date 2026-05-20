# Loot & Grow

**멀티플레이어 액션 RPG 캡스톤 프로젝트**
몬스터를 사냥하고 장비를 강화하며 성장하는 게임.
4인 팀 · 16주 개발 · Android / Windows 빌드

> Unity 6 · Netcode for GameObjects · FastAPI · Docker · AWS EC2

---

## 스크린샷

<!-- 스크린샷 추가 예정 -->

---

## 기술 스택

| 영역 | 기술 |
|------|------|
| 클라이언트 | Unity 6 (URP), C#, Netcode for GameObjects |
| 서버 | Python, FastAPI, SQLAlchemy, Docker |
| 인프라 | AWS EC2, GitHub Actions |
| 데이터 | Google Sheets → JSON → ScriptableObject 자동화 |

---

## 담당 파트

### 클라이언트 — 아이템 관련 시스템 전담
인벤토리 · 장비 · 상점 · 강화 · 창고 시스템의 로직 및 UI 구현.
서버 API와의 연동, 슬롯 상태 관리, 팝업 흐름 설계를 포함한다.

- **버그 발견 및 수정** — 버리기 팝업이 열린 상태에서 장착을 실행하면 아이템이 장착되는 동시에 필드에 드롭되어 복제되는 Critical 버그를 발견하고 원인을 추적해 수정.
  → `OnAction()` 실행 전 팝업 상태를 확인하여 강제 취소 처리

### 서버 — DataHub 전체
동적 게임 데이터(인벤토리, 장비, 골드, 상점, 강화)를 처리하는 FastAPI 서버를 단독 설계·구현.

- REST API 엔드포인트 설계 및 구현
- SQLAlchemy ORM으로 DB 스키마 정의
- 클라이언트와의 API 연동은 `DataAPI.cs` 단일 레이어로 집중 관리

### 데이터 파이프라인 — 정적 데이터 자동화
아이템·장비·강화 수치 등 기획 데이터를 Google Sheets에서 관리하고,
Docker 기반 Python 스크립트로 JSON 변환 후 서버와 ScriptableObject에 자동 반영.

```
Google Sheets → gsheet_to_json.py (Docker) → ItemTable.json
                                            → ScriptableObject (Unity Editor Tool)
```

### 인프라 — 배포 자동화
DataHub 서버를 AWS EC2 + Docker 환경에서 운영.
`main` 브랜치 푸시 시 GitHub Actions가 SSH로 EC2에 접속해 자동 재배포.

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
