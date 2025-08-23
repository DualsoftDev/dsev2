# UnitTest.Core

## 프로젝트 개요
- **프로젝트명**: UnitTest.Core
- **타입**: F# 테스트 프로젝트
- **프레임워크**: .NET 9.0
- **생성일**: 2024
- **역할**: Ev2.Core.FS 핵심 엔진의 기능 검증 및 단위 테스트
- **테스트 프레임워크**: NUnit 3.x + FsUnit

## 프로젝트 목적
DS Engine Version 2의 핵심 기능들을 포괄적으로 테스트합니다. 도메인 모델 생성, 데이터베이스 CRUD 작업, JSON 직렬화/역직렬화, 타입 변환 등 모든 핵심 기능의 정확성을 검증합니다.

## 의존성 정보

### Target Framework
- **.NET 9.0**

### NuGet 패키지
- **Microsoft.NET.Test.Sdk** 17.11.0 - .NET 테스트 SDK
- **FsUnit** 6.0.0 - F# 단위 테스트 유틸리티
- **FsUnit.xUnit** 6.0.0 - F# xUnit 통합
- **NUnit3TestAdapter** 4.6.0 - NUnit 테스트 어댑터

### 프로젝트 참조
- **Dual.Common.UnitTest.FS** - 공통 단위 테스트 유틸리티
- **Ev2.Core.FS** - DS Engine Version 2 핵심 라이브러리 (테스트 대상)

### 외부 파일 링크
- **appSettings.json** - Ev2.Core.FS에서 링크된 설정 파일
- **appSettings.json.template** - 설정 파일 템플릿

## 파일 구조

```
UnitTest.Core/
├── UnitTest.Core.fsproj      # 프로젝트 파일
├── UnitTest.Core.md          # 이 문서
├── Program.fs                # 테스트 엔트리 포인트
├── TestHelpers.fs            # 공통 테스트 유틸리티
├── Duplicate.Test.fs         # 객체 복제 테스트
├── ValueSpec.Test.fs         # 값 스펙 테스트
├── model/                    # 도메인 모델 테스트
│   ├── CreateSample.fs       # 기본 샘플 데이터 생성
│   ├── CreateSample.WithCylinders.fs  # 실린더 포함 샘플
│   ├── CreateSample.HelloDS.fs        # HelloDS 샘플
│   └── TestHelloDS.fs        # HelloDS 테스트
├── database/                 # 데이터베이스 기능 테스트
│   ├── Schema.Test.fs        # 스키마 및 전역 설정
│   ├── ReadFromDB.Test.fs    # 데이터베이스 읽기 테스트
│   └── SystemImportExport.Test.fs  # 시스템 가져오기/내보내기 테스트
├── newtonsoft.json/         # JSON 직렬화 테스트
│   └── FSharpOption.Test.fs  # F# Option 타입 직렬화 테스트
├── test-data/               # 테스트 데이터 파일들
│   ├── *.json               # JSON 테스트 파일들
│   ├── *.sqlite3            # SQLite 테스트 데이터베이스들
│   └── (130개 이상의 테스트 파일)
├── bin/                     # 빌드 출력
└── obj/                     # 빌드 임시 파일
```

## 주요 컴포넌트

### 핵심 테스트 모듈

#### TestHelpers.fs
**네임스페이스**: `T.TestHelpers`
**목적**: 모든 테스트에서 공통으로 사용하는 유틸리티 함수 제공

**주요 함수**:
```fsharp
val testDataDir : unit -> string                          // 테스트 데이터 디렉토리 경로
val getUniqueTestPath : string -> string -> string        // 고유 테스트 파일 경로 생성
val getUniqueTestPathWithGuid : string -> string -> string // GUID 기반 파일 경로
val getUniqueSqlitePath : unit -> string                  // SQLite DB 경로 생성
val getUniqueJsonPath : unit -> string                    // JSON 파일 경로 생성
val getUniqueAasxPath : unit -> string                    // AASX 파일 경로 생성
val cleanupTestFile : string -> unit                      // 테스트 파일 정리
val cleanupTestFiles : string list -> unit                // 여러 파일 정리
```

#### Schema.Test.fs (GlobalTestSetup)
**네임스페이스**: `T`
**목적**: 전역 테스트 환경 설정 및 초기화

**주요 기능**:
- `[<SetUpFixture>]` GlobalTestSetup 클래스
- Ev2.Core.FS 모듈 초기화
- 테스트용 프로젝트/시스템 생성
- SQLite/PostgreSQL 데이터베이스 API 설정

### 도메인 모델 테스트

#### CreateSample.fs
**네임스페이스**: `T.CreateSampleModule`
**목적**: 테스트용 복합 도메인 객체 생성

**생성하는 객체들**:
- **Project**: MainProject
- **DsSystem**: MainSystem (IRI 포함)
- **ApiDef/ApiCall**: API 정의 및 호출 (ValueSpec 포함)
- **Flow**: MainFlow (Button, Lamp, Condition, Action 포함)
- **Work**: BoundedWork1/2, FreeWork1 (다양한 Status4 상태)
- **Call**: Call1a/1b, Call2a/2b (AutoConditions, CommonConditions 포함)
- **ArrowBetweenWorks/ArrowBetweenCalls**: 작업 간/호출 간 화살표 연결

**특징**:
- JSON 매개변수를 활용한 복잡한 객체 설정
- DbStatus4 열거형 테스트 (Ready, Going, Finished, Homing)
- DbCallType 테스트 (Normal, Parallel, Repeat)
- ValueSpec 범위 테스트 (Ranges with Open/Closed bounds)

#### CreateSample.HelloDS.fs
**목적**: 간단한 HelloDS 샘플 생성
**사용 케이스**: 기본적인 프로젝트 구조 테스트

#### CreateSample.WithCylinders.fs
**목적**: 실린더 장비가 포함된 복잡한 시스템 테스트
**사용 케이스**: 산업 자동화 장비 시뮬레이션

### 데이터베이스 테스트

#### Schema.Test.fs
- **SQLite 지원**: 인메모리 및 파일 기반 데이터베이스
- **PostgreSQL 지원**: 로컬 서버 연결 테스트
- **스키마 생성**: 자동 테이블 생성 및 제약 조건 검증

#### SystemImportExport.Test.fs
**목적**: 시스템 단위의 데이터 가져오기/내보내기 테스트
- 데이터베이스 → JSON 변환
- JSON → 데이터베이스 복원
- 데이터 무결성 검증

### JSON 직렬화 테스트

#### FSharpOption.Test.fs
**네임스페이스**: `FSharpOptionTest`
**목적**: F# Option 타입과 Nullable 타입의 JSON 직렬화 검증

**테스트 케이스**:
- `Option<'T>` 타입 직렬화/역직렬화
- `Nullable<'T>` 타입 처리
- null 값 처리 및 빈 JSON 객체 생성

## 테스트 아키텍처

### 전역 설정
1. **모듈 초기화**: `Ev2.Core.FS.ModuleInitializer.Initialize()`
2. **로깅 활성화**: `DcLogger.EnableTrace <- true`
3. **앱 설정**: `UseUtcTime = false` 설정
4. **테스트 데이터 준비**: 복잡한 도메인 객체 미리 생성

### 테스트 데이터 관리
- **test-data/** 폴더: 130개 이상의 테스트 파일
- **고유 파일명**: GUID/타임스탬프 기반 충돌 방지
- **자동 정리**: 테스트 완료 후 임시 파일 삭제
- **다양한 형식**: JSON, SQLite, AASX 파일 지원

### 데이터베이스 테스트 전략
- **인메모리 DB**: 빠른 단위 테스트용
- **파일 DB**: 지속성 및 대용량 테스트용
- **PostgreSQL**: 프로덕션 환경 시뮬레이션
- **트랜잭션 테스트**: ACID 특성 검증

## 빌드 명령어

### 프로젝트 빌드
```bash
# 단일 프로젝트 빌드
dotnet build UnitTest.Core.fsproj

# Release 모드 빌드
dotnet build UnitTest.Core.fsproj --configuration Release

# 전체 솔루션에서 빌드 (상위 디렉토리에서)
cd ../../../
dotnet build dsev2.sln
```

### 테스트 실행
```bash
# 모든 테스트 실행
dotnet test UnitTest.Core.fsproj

# 상세 출력으로 테스트 실행
dotnet test UnitTest.Core.fsproj --verbosity normal

# 특정 테스트 클래스 실행
dotnet test UnitTest.Core.fsproj --filter "TestClass~CreateSample"

# 특정 테스트 메서드 실행
dotnet test UnitTest.Core.fsproj --filter "create hello ds"

# 병렬 실행 비활성화
dotnet test UnitTest.Core.fsproj -- NUnit.NumberOfTestWorkers=1
```

### 개발 워크플로우
```bash
# 의존성 복원
dotnet restore UnitTest.Core.fsproj

# 테스트 데이터 정리 (필요시)
# PowerShell에서: Remove-Item .\test-data\test_*.* -Force

# 지속적 테스트 (파일 변경 감지)
dotnet watch test UnitTest.Core.fsproj

# 커버리지 포함 테스트 (coverlet 패키지 필요)
dotnet test UnitTest.Core.fsproj --collect:"XPlat Code Coverage"
```

### 패키지 관리
```bash
# NuGet 패키지 복원
dotnet restore UnitTest.Core.fsproj

# 패키지 업데이트
dotnet add package FsUnit --version 6.0.0

# 프로젝트 정리
dotnet clean UnitTest.Core.fsproj
```

## 테스트 범위 및 시나리오

### 1. 도메인 모델 검증
- **객체 생성**: 모든 도메인 타입의 인스턴스 생성
- **속성 설정**: 필수/선택 속성 검증
- **관계 매핑**: 객체 간 연결 관계 테스트
- **불변성**: 생성 후 상태 변경 검증

### 2. 타입 변환 시스템
- **Runtime ↔ JSON**: NJ타입과의 변환
- **Runtime ↔ ORM**: 데이터베이스 매핑
- **타입 안전성**: 잘못된 타입 변환 에러 처리

### 3. 데이터베이스 CRUD
- **생성(Create)**: 복잡한 객체 그래프 삽입
- **조회(Read)**: 관계 포함 조회 및 지연 로딩
- **수정(Update)**: 부분 업데이트 및 낙관적 잠금
- **삭제(Delete)**: 계층적 삭제 및 참조 무결성

### 4. JSON 직렬화
- **다형성**: $type 필드를 통한 타입 보존
- **순환 참조**: 객체 그래프의 순환 참조 처리
- **F# 타입**: Option, List, Record 타입 직렬화
- **날짜/시간**: DateTime 형식 및 시간대 처리

### 5. ValueSpec 시스템
- **범위 검증**: Lower/Upper 경계 처리
- **개방/폐쇄 구간**: Open/Closed boundary 테스트
- **여러 범위**: 복수 범위 조합 검증

## 설정 및 구성

### appSettings.json
Ev2.Core.FS에서 링크된 설정 파일:
- 데이터베이스 연결 문자열
- UTC 시간 사용 여부
- 로깅 레벨 설정

### 테스트 환경 변수
- **데이터베이스**: SQLite (기본), PostgreSQL (선택적)
- **로깅**: 추적 모드 활성화
- **시간대**: 로컬 시간 사용

## 테스트 데이터 예시

### 생성되는 객체 구조
```
MainProject
└── MainSystem
    ├── ApiDef1a, UnusedApi (ApiDef)
    ├── ApiCall1a, ApiCall1b (ApiCall)
    ├── MainFlow
    │   ├── MyButton1 (DsButton)
    │   ├── MyLamp1 (Lamp)
    │   ├── MyCondition1 (DsCondition)
    │   ├── MyAction1 (DsAction)
    │   └── BoundedWork1 (Work)
    ├── BoundedWork1, BoundedWork2, FreeWork1 (Work)
    │   ├── Call1a, Call1b (Call) → BoundedWork1
    │   └── Call2a, Call2b (Call) → BoundedWork2
    └── ArrowBetweenWorks (Work 간 연결)
```

### JSON 매개변수 예시
```json
// Work 매개변수
{"Name":"kwak", "Company":"dualsoft", "Room":510}

// Call 매개변수  
{"Type":"call", "Count":3, "Pi":3.14}

// Arrow 매개변수
{"ArrowWidth":2.1, "ArrowHead":"Diamond", "ArrowTail":"Rectangle"}
```

## 개발 가이드라인

### 새 테스트 추가 시
1. 적절한 네임스페이스(`T`) 사용
2. NUnit 애트리뷰트 활용 (`[<Test>]`, `[<SetUp>]`)
3. FsUnit assertion 사용 (`===`, `should equal`)
4. 고유 파일명으로 테스트 데이터 충돌 방지
5. 테스트 완료 후 임시 파일 정리

### 테스트 네이밍 규칙
- **함수**: camelCase (예: `doTestNullable`)
- **설명적 이름**: 백틱 사용 (예: `create hello ds`)
- **모듈**: PascalCase + Module 접미사

### 주의사항
- 전역 상태 공유로 인한 테스트 간 의존성 주의
- 데이터베이스 연결 해제 및 리소스 정리
- 테스트 데이터 파일 크기 관리
- F# Option 타입과 C# Nullable 타입 구분

## 연관 프로젝트
- **Ev2.Core.FS** - 테스트 대상 핵심 엔진
- **Dual.Common.UnitTest.FS** - 공통 테스트 유틸리티
- **UnitTest.Aas** - AAS 기능 테스트 프로젝트

## 참고사항
이 프로젝트는 DS Engine Version 2의 품질과 안정성을 보장하는 핵심적인 역할을 수행합니다. 모든 핵심 기능에 대한 포괄적인 테스트 커버리지를 제공하며, 실제 산업 환경에서 발생할 수 있는 다양한 시나리오를 시뮬레이션합니다.

## 관련 문서
- [Ev2.Core.FS 문서](../../engine/Ev2.Core.FS/Ev2.Core.FS.md)
- [전체 솔루션 구조](../../dsev2.md)