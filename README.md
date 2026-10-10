# Loot & Grow

**Unity 멀티플레이어 액션 RPG 프로젝트**  
몬스터를 사냥하고, 다른 플레이어를 약탈하고, 장비를 강화하며 성장하는 PVE/PVP 혼합형 게임.  
4인 팀 · 모바일(Android) 빌드.

> Unity 6 · Netcode for GameObjects · FastAPI · Docker · AWS EC2

---

## 플레이 화면

<img width="640" height="361" alt="Image" src="https://github.com/user-attachments/assets/c192249a-fa22-4688-9895-09859bbe8aee" />

---

## 기술 스택

| 영역 | 기술 |
|------|------|
| 클라이언트 | Unity 6 (URP), C#, Netcode for GameObjects |
| 서버 | Python, FastAPI, SQLAlchemy, MySQL |
| 인프라 | Docker, AWS EC2, GitHub Actions |
| 도구 | Google Sheets API, Unity Editor Window |

---

## 담당 파트

### 서버 — DataHub 전체
동적 데이터(계정 인증, 인벤토리, 장비 장착, 창고, 상점, 강화 이력 등)를 처리하는 FastAPI 서버를 단독 설계·구현.

- REST API 엔드포인트 설계 및 라우터 분리 (`auth`, `inventory`, `equipment`, `shop`, `enhance`, `storage`, `character`, `admin`)
- SQLAlchemy ORM으로 DB 스키마 정의 및 관계 설정
- JWT Access/Refresh Token 이중 인증 구조
- `.env` + Docker 환경변수 기반 보안 관리

### 클라이언트 — 데이터 연동 및 게임 시스템

**DataAPI.cs — Unity ↔ 서버 브릿지**  
팀원이 서버 구조를 몰라도 사용할 수 있도록 모든 API 호출을 Coroutine 기반 헬퍼로 추상화.  
`onSuccess` / `onError` 콜백 패턴으로 결과 처리. 공개 메서드에 summary·param·example 주석 명세.

**GameDataSO.cs — ScriptableObject 싱글턴**  
흩어져 있던 SO 참조(`ItemTable`, `EquipDetail`, `EnhanceTable` 등)를 단일 진입점으로 통합.  
Inspector 할당 없이 `GameDataSO.Instance.ItemTable` 형태로 어디서나 접근 가능.

**인벤토리 · 장비 · 상점 · 강화 · 창고 시스템**  
서버 데이터를 기반으로 동작하는 5개 패널 UI 및 로직 구현.  
장비 장착 시 `PlayerEquipmentVisual`(NGO NetworkBehaviour)과 연동해 모든 클라이언트에 외관을 실시간 동기화.

**GraphicsManager — 그래픽 품질 설정**  
URP renderScale, 그림자 거리, MSAA를 묶은 4단계 품질 프리셋(낮음/보통/높음/울트라) 구현.  
Cinemachine farClip 연동, PlayerPrefs 기반 설정 영속화.

### 에디터 툴

**ItemGiverWindow** (`Tools > Item Giver`)  
어드민 계정으로 서버에 로그인한 뒤 ItemTableSO 목록에서 아이템을 골라 특정 유저에게 즉시 지급.  
기획·QA 단계에서 API를 직접 호출하던 불편함을 에디터 내 GUI로 해결.

- 소모품·재료 / 장비 모드 탭 전환
- 장비 모드: 강화 수치 슬라이더 제공 (무기 최대 20강, 방어구 10강 자동 제한)
- JWT 토큰 EditorPrefs 캐싱으로 재시작 후에도 로그인 유지

**DataImporter**  
`gsheet_to_json.py`가 생성한 JSON 파일을 ScriptableObject에 자동으로 임포트하는 Unity 에디터 툴.  
데이터 파이프라인의 마지막 단계를 자동화.

**TMPFontReplacer** (`Tools > TMP 폰트 일괄 교체`)  
씬과 프리팹 전체의 TMP 폰트를 한 번에 교체하는 유틸리티.  
폰트 교체 작업을 오브젝트마다 일일이 수정하던 반복 작업을 제거.

### 데이터 파이프라인 — 정적 데이터 자동화
적·아이템·장비·강화 수치 등 기획 데이터를 Google Sheets에서 관리하고,  
Docker 기반 Python 스크립트로 JSON 변환 후 서버와 ScriptableObject에 자동 반영.

```
Google Sheets → gsheet_to_json.py (Docker) → *.json
→ Server/DataHub/app/data/   (FastAPI 직접 참조)
→ ScriptableObject (Unity DataImporter 에디터 툴)
```

### 인프라 — 배포 자동화
DataHub 서버를 AWS EC2 + Docker 환경에서 운영.  
`main` branch push 시 GitHub Actions가 SSH로 EC2에 접속해 서버를 자동 재배포.

---

## 프로젝트 구조

```
Client/Assets/
├── Scripts/
│   ├── System/         # DataAPI, GameDataSO, GraphicsManager, PoolManager
│   ├── Inventory/      # 인벤토리 슬롯, 아이템 정보 패널, 버리기 팝업
│   ├── Storage/        # 창고 패널 (싱글턴, 서버 연동)
│   ├── Shop/           # 상점 패널, 구매/판매
│   ├── Enhance/        # 강화 패널
│   ├── Player/         # 이동, 전투, 장비 비주얼 동기화
│   ├── Network/        # ClientManager, ServerManager
│   ├── Stage/          # NPC, 채집, 드롭 아이템, 구역 트리거
│   ├── UI/             # HUD, 미니맵, 채팅
│   └── Enemy/          # 적 AI, 체력, 스폰
└── Editor/
    ├── ItemGiverWindow.cs   # 어드민 아이템 즉시 지급 툴
    ├── DataImporter.cs      # JSON → ScriptableObject 임포터
    └── TMPFontReplacer.cs   # TMP 폰트 일괄 교체 툴

Server/DataHub/     # FastAPI 서버 (DataHub)
Tools/              # Google Sheets → JSON 변환 스크립트
.github/workflows/  # GitHub Actions 배포 자동화
```

## 성과

<img width="975" height="587" alt="image" src="https://github.com/user-attachments/assets/dd958e6c-dfbd-467e-87a5-13886d4603f7" />

2026 성남 인디크래프트 챌린저 부문 122팀 중 TOP 20 선정
산학프로젝트 교과 성취도 A+
