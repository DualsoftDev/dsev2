# CLAUDE.md

이 파일은 이 저장소에서 코드 작업을 할 때 Claude Code (claude.ai/code)에게 가이드를 제공합니다.



### 프로젝트 구조
```bash
# 메인 솔루션 파일
src/dsev2.sln

# 핵심 엔진 프로젝트  
src/engine/Ev2.Core.FS/     # 핵심 도메인 모델과 비즈니스 로직
src/engine/Ev2.Aas.FS/      # AAS (Asset Administration Shell) 통합
```

## 고수준 아키텍처

### 도메인 모델 구조
시스템은 산업 자동화 시스템을 나타내는 계층적 객체 모델을 중심으로 구축됩니다:

- **Project** → 여러 시스템을 포함하며 루트 컨테이너 역할
- **DsSystem** → 플로우, 작업, API 정의를 가진 자동화 시스템
- **Flow** → 시스템 상호작용을 위한 UI 요소들(버튼, 램프, 조건, 액션) 포함
- **Work** → 시스템 내의 실행 가능한 단위, 호출들을 포함하고 화살표로 연결됨
- **Call** → 작업 내의 개별 실행 단계, API 호출과 연결됨
- **ApiDef/ApiCall** → 외부 시스템 인터페이스를 정의하고 구현

### 핵심 기술
- **언어**: 함수형 프로그래밍 패러다임을 위한 F# (.NET 9.0)
- **데이터베이스**: Dapper ORM을 사용한 SQLite/PostgreSQL 데이터 지속성
- **직렬화**: JSON 직렬화를 위한 Newtonsoft.Json, 산업 표준을 위한 AAS XML
- **아키텍처**: 관심사의 명확한 분리를 가진 도메인 주도 설계

### 주요 설계 패턴

#### 이중성(Duality) 원리
시스템은 객체가 맥락에 따라 다른 역할을 가질 수 있는 "이중성" 개념을 구현합니다:
- **구조적 이중성**: 시스템 ⊕ 디바이스, 인스턴스 ⊕ 참조
- **실행적 이중성**: 원인 ⊕ 결과, ReadTag ⊕ WriteTag

#### 타입 시스템 아키텍처
- **런타임 타입** (Project, DsSystem, Flow, Work, Call): 메인 비즈니스 객체
- **JSON 타입** (NjProject, NjSystem 등): Newtonsoft.Json 직렬화 객체
- **ORM 타입** (ORMProject, ORMSystem 등): 데이터베이스 매핑 객체

각 객체 타입은 서로 다른 맥락에서 이러한 표현 간의 변환 메서드를 가집니다.

#### 그래프 기반 플로우 제어
- 시스템 내에서 ArrowBetweenWorks로 연결된 Work 객체들
- 작업 내에서 ArrowBetweenCalls로 연결된 Call 객체들
- 복잡한 자동화 시나리오를 위한 순환 실행 플로우 지원

### 데이터베이스 아키텍처
- **스키마 정의**: `Ev2.Core.FS/database/Database.Schema.fs`
- **ORM 매핑**: `Ev2.Core.FS/database/Database.ORM.fs`
- **API 레이어**: `Ev2.Core.FS/database/AppDbApi.fs`
- **CRUD 작업**: `Ev2.Core.FS/TypeConversion,Serialization/DB.*.fs`

### AAS 통합
- **Asset Administration Shell**: Industry 4.0 표준 지원
- **AASX 파일 처리**: 압축된 AAS 패키지 읽기/쓰기
- **XML 직렬화**: 내부 객체와 AAS XML 형식 간 변환
- **Project-AAS 매핑**: AASX 파일에서 Project 객체 생성 가능

### 파일 구성 패턴
- **Interfaces.fs**: 핵심 도메인 모델 정의
- **Core.*.fs**: 비즈니스 로직과 작업
- **TypeConversion,Serialization/**: 형식 간 객체 변환
- **database/**: 데이터베이스 접근과 ORM 레이어
- **devdoc/**: 개발자 문서와 사양

### 테스트 전략
- 기능 영역별로 구성된 유닛 테스트 (Core, AAS)
- `docs/Spec/Data/`에 SQLite 데이터베이스와 JSON 파일이 있는 테스트 데이터 샘플
- `unit-test/UnitTest.Core/model/CreateSample*.fs`에 샘플 객체 생성

### 개발 워크플로우
1. `Interfaces.fs`에서 정의된 핵심 도메인 모델
2. `Core.*.fs` 파일에서 구현된 비즈니스 로직
3. ORM 변환 패턴을 통한 데이터베이스 지속성
4. 외부 통합을 위한 JSON/XML 직렬화
5. 모든 레이어에서 동작을 검증하는 유닛 테스트

코드베이스는 불변 데이터 구조, 타입 안전성, 순수 비즈니스 로직과 부작용 간의 명확한 분리를 가진 함수형 프로그래밍 원칙을 강조합니다.