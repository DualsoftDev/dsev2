# UnitTest.Core

## 프로젝트 개요

Ev2.Core.FS 엔진의 핵심 기능에 대한 단위 테스트를 수행하는 F# 테스트 프로젝트입니다. 산업 자동화 시스템의 도메인 모델, 데이터베이스 연동, JSON 직렬화/역직렬화 등 핵심 기능들을 검증합니다.

### 주요 특징
- **Target Framework**: .NET 9.0
- **언어**: F#
- **테스트 프레임워크**: NUnit, FsUnit, xUnit
- **주요 테스트 영역**: 도메인 모델, 데이터베이스 CRUD, JSON 직렬화

## 의존성 정보

### Target Framework
- **Target Framework**: `net9.0`
- **프로젝트 유형**: 테스트 프로젝트 (`IsTestProject: true`)

### NuGet Package References
```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
<PackageReference Include="FsUnit" Version="6.0.0" />
<PackageReference Include="FsUnit.xUnit" Version="6.0.0" />
<PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
```

### Project References
- `Dual.Common.UnitTest.FS` - 공통 단위 테스트 유틸리티
- `Ev2.Core.FS` - 메인 핵심 엔진 프로젝트

### Linked Files
- `appSettings.json` - Ev2.Core.FS에서 링크된 설정 파일
- `appSettings.json.template` - 설정 파일 템플릿

## 파일 구조

```
UnitTest.Core/
├── UnitTest.Core.fsproj       # 프로젝트 파일
├── Program.fs                 # 진입점
├── TestHelpers.fs             # 테스트 유틸리티 헬퍼
├── Duplicate.Test.fs          # 중복 처리 테스트
├── ValueSpec.Test.fs          # 값 사양 테스트
│
├── model/                     # 도메인 모델 테스트
│   ├── CreateSample.fs                    # 기본 샘플 데이터 생성
│   ├── CreateSample.WithCylinders.fs      # 실린더 포함 샘플 생성
│   ├── CreateSample.HelloDS.fs            # HelloDS 샘플 생성
│   └── TestHelloDS.fs                     # HelloDS 테스트
│
├── database/                  # 데이터베이스 테스트
│   ├── Schema.Test.fs                     # 스키마 테스트
│   ├── ReadFromDB.Test.fs                 # DB 읽기 테스트
│   └── SystemImportExport.Test.fs         # 시스템 가져오기/내보내기 테스트
│
├── newtonsoft.json/          # JSON 직렬화 테스트
│   └── FSharpOption.Test.fs               # F# Option 타입 JSON 테스트
│
├── test-data/                # 테스트 데이터 디렉토리
│   ├── *.sqlite3                          # SQLite 테스트 DB 파일들
│   ├── *.json                             # JSON 테스트 데이터 파일들
│   └── ...                                # 기타 테스트 데이터
│
├── bin/                      # 빌드 출력
└── obj/                      # 빌드 임시 파일
```

## 주요 컴포넌트

### 네임스페이스 및 모듈
- **T.TestHelpers** - 테스트 유틸리티 함수들
- **T.CreateSampleModule** - 샘플 데이터 생성 함수들
- **T** (네임스페이스) - 모든 테스트 관련 코드의 루트 네임스페이스

### 핵심 테스트 영역

#### 1. 도메인 모델 테스트 (model/)
- **Project, DsSystem, Work, Call** 등 핵심 도메인 객체 생성 및 조작
- 계층적 구조 테스트: `Project > DsSystem > Work > Call`
- API 정의 및 호출 테스트
- Flow, ArrowBetweenWorks, ArrowBetweenCalls 테스트

#### 2. 데이터베이스 테스트 (database/)
- SQLite 스키마 생성 및 검증
- CRUD 작업 테스트
- 시스템 가져오기/내보내기 기능
- ORM 매핑 검증

#### 3. JSON 직렬화 테스트 (newtonsoft.json/)
- F# Option 타입의 JSON 직렬화/역직렬화
- Nullable 타입 처리
- 커스텀 JSON 컨버터 테스트

### 테스트 헬퍼 함수들

#### TestHelpers.fs
```fsharp
// 테스트 데이터 경로 관련
- testDataDir() : string
- getUniqueTestPath(testName, extension) : string
- getUniqueTestPathWithGuid(testName, extension) : string

// 고유 파일 경로 생성
- getUniquePathByTime(extension) : string
- getUniquePathByGuid(extension) : string
- getUniqueSqlitePath() : string
- getUniqueAasxPath() : string
- getUniqueJsonPath() : string

// 테스트 정리
- cleanupTestFile(filePath) : unit
- cleanupTestFiles(filePaths) : unit
```

### 샘플 데이터 생성

#### CreateSample.fs의 주요 함수
- `createEditableProject()` - 편집 가능한 프로젝트 생성
- 계층적 구조 생성: Project → DsSystem → Work → Call
- API 정의 및 호출 설정
- Flow 및 Arrow 연결 설정

## 빌드 명령어

### 기본 빌드
```bash
# 프로젝트 빌드
cd F:\Git\aas\submodules\dsev2\src\unit-test\UnitTest.Core
dotnet build UnitTest.Core.fsproj

# 솔루션 레벨에서 빌드
cd F:\Git\aas\submodules\dsev2\src
dotnet build dsev2.sln
```

### 테스트 실행
```bash
# 단위 테스트 실행
dotnet test UnitTest.Core.fsproj

# 상세 출력으로 테스트 실행
dotnet test UnitTest.Core.fsproj --verbosity normal

# 솔루션의 모든 테스트 실행
cd F:\Git\aas\submodules\dsev2\src
dotnet test dsev2.sln
```

### 특정 구성으로 빌드
```bash
# Release 구성으로 빌드
dotnet build UnitTest.Core.fsproj --configuration Release

# Debug 구성으로 빌드 (기본값)
dotnet build UnitTest.Core.fsproj --configuration Debug
```

## 테스트 데이터

### test-data 디렉토리
- **SQLite 파일들**: 다양한 테스트 시나리오의 데이터베이스 파일
- **JSON 파일들**: 직렬화/역직렬화 테스트용 JSON 데이터
- **샘플 데이터**: 개발 및 테스트용 샘플 프로젝트 데이터

### 파일 명명 규칙
- `test_[8자리GUID].[확장자]` - GUID 기반 고유 파일명
- 테스트별 고유 파일 생성으로 병렬 테스트 지원

## 주요 테스트 시나리오

1. **도메인 모델 생성 및 조작**
   - 계층적 객체 구조 생성
   - 관계 설정 (Parent-Child, Arrow 연결)
   - 속성 설정 및 검증

2. **데이터베이스 연동**
   - 스키마 생성 및 마이그레이션
   - CRUD 작업 (Create, Read, Update, Delete)
   - 트랜잭션 처리

3. **JSON 직렬화**
   - F# 특화 타입 처리 (Option, Record)
   - 복잡한 객체 그래프 직렬화
   - 순환 참조 처리

## 관련 프로젝트
- **Ev2.Core.FS** - 테스트 대상인 핵심 엔진
- **UnitTest.Aas** - AAS 관련 기능 테스트
- **Dual.Common.UnitTest.FS** - 공통 테스트 유틸리티