# UnitTest.Aas

## 프로젝트 개요
- **이름**: UnitTest.Aas
- **타입**: F# 테스트 프로젝트
- **프레임워크**: .NET 9.0
- **역할**: Ev2.Aas.FS AAS(Asset Administration Shell) 통합 테스트
- **테스트 대상**: AASX 파일 처리, XML 직렬화, AAS 표준 호환성 검증

UnitTest.Aas는 Ev2.Aas.FS 프로젝트의 AAS(Asset Administration Shell) 관련 기능을 테스트하는 단위 테스트 프로젝트입니다. 이 프로젝트는 F#으로 작성되었으며, NUnit 테스트 프레임워크와 FsUnit를 사용하여 AAS 직렬화/역직렬화, AASX 파일 처리, JSON/XML 변환 등의 기능을 검증합니다.

### 주요 기능
- AAS 표준 형식(JSON, XML, AASX)과 DS 엔진 내부 객체 간의 변환 테스트
- AASX 패키지 파일 생성 및 읽기 테스트
- AasCore.Aas3_0 라이브러리를 통한 AAS 3.0 표준 호환성 테스트
- Project, DsSystem, Work, Call 등 핵심 도메인 객체의 AAS 매핑 테스트
- 라운드트립 변환 검증 (Runtime Object ↔ JSON ↔ AAS ↔ AASX)

## 의존성 정보

### 대상 프레임워크
- **TargetFramework**: net9.0
- **IsTestProject**: true
- **IsPackable**: false

### NuGet 패키지 참조
- **Microsoft.NET.Test.Sdk** v17.11.0 - .NET 테스트 SDK
- **FsUnit** v6.0.0 - F# 단위 테스트 도구
- **FsUnit.xUnit** v6.0.0 - xUnit과의 통합
- **NUnit3TestAdapter** v4.6.0 - NUnit 테스트 어댑터

### 프로젝트 참조
- **Dual.Common.UnitTest.FS** - 공통 단위 테스트 유틸리티
- **Ev2.Aas.FS** - AAS 직렬화/역직렬화 엔진
- **Ev2.Core.FS** - 핵심 도메인 모델 및 비즈니스 로직
- **UnitTest.Core** - 코어 기능 단위 테스트

## 파일 구조

```
UnitTest.Aas/
├── UnitTest.Aas.fsproj          # 프로젝트 파일
├── Program.fs                   # 프로그램 진입점
├── TestData.fs                  # 테스트용 JSON 데이터 정의
├── TestSetup.fs                 # 글로벌 테스트 설정
├── AasTest.fs                   # 기본 AAS 변환 테스트
├── FromAasTest.fs               # AAS에서 DS 객체로 변환 테스트
├── ToAasTest.fs                 # DS 객체에서 AAS로 변환 테스트
├── AasSpec.Test/
│   └── AasCore3.0Test.fs       # AasCore 3.0 라이브러리 테스트
├── bin/                         # 빌드 출력 디렉토리
└── obj/                         # 임시 빌드 파일
```

## 주요 컴포넌트

### 네임스페이스 및 모듈

#### T.Core 네임스페이스
프로젝트의 모든 테스트 모듈이 포함된 주요 네임스페이스

#### DsJson 모듈 (TestData.fs)
```fsharp
module DsJson
```
- **dsJson**: DsSystem 객체의 샘플 JSON 데이터
- **dsProject**: Project 객체의 샘플 JSON 데이터  
- **getDsProjectJson**: 동적 DB 연결 문자열을 사용하는 Project JSON 생성 함수

#### TestSetup 모듈 (TestSetup.fs)
```fsharp
[<AutoOpen>]
module TestSetup
```
- **GlobalTestSetup**: 전역 테스트 설정 클래스
- 모듈 초기화 및 테스트 데이터 디렉토리 생성
- 로깅 설정 및 앱 설정 구성

#### Aas 모듈 (AasTest.fs)
```fsharp
module Aas
```
- **aasJson0**: 기본 AAS JSON 샘플
- **aasXml0**: 기본 AAS XML 샘플
- 서브모델 없는 환경의 최소 AAS 구조 정의

#### FromAasTest 모듈 (FromAasTest.fs)
```fsharp
module FromAasTest
```
- AAS 형식에서 DS 객체로의 변환 테스트
- JSON/XML 양방향 변환 검증
- AASX 파일에서 XML 추출 테스트

#### ToAasTest 모듈 (ToAasTest.fs)
```fsharp
module ToAasTest
```
- DS 객체에서 AAS 형식으로의 변환 테스트
- Project → NjProject → AASX 파일 생성 테스트
- 복합 도메인 객체(실린더 시스템)의 AAS 매핑 테스트

#### AasCore3_0Test 모듈 (AasSpec.Test/AasCore3.0Test.fs)
```fsharp
module AasCore3_0Test
```
- AasCore.Aas3_0 라이브러리 직접 테스트
- 서브모델 최소 구조 테스트
- Property, SubmodelElementCollection, Range 등 요소 테스트

### 주요 테스트 시나리오

#### 1. JSON/XML 양방향 변환
- **AAS JSON ↔ AAS XML 변환 검증**: `FromAasTest.T` 클래스
- **DS JSON ↔ Runtime 객체 변환 검증**: `ToAasTest.T` 클래스
- **JObj 기반 AAS 구조 생성**: `ToAasTest.T.AasShell: JObj -> string conversion test`

#### 2. AASX 파일 처리
- **Project → AASX 파일 내보내기**: `ToAasTest.T2` 클래스의 다양한 시나리오
- **AASX 파일에서 서브모델 XML 추출**: `FromAasTest.T.Aasx xml submodel xml fetch test`
- **AASX 파일 정리**: TestHelpers의 `cleanupTestFile()` 자동화
- **Hello DS → AASX 파일**: 간단한 프로젝트 변환 예제

#### 3. 도메인 객체 매핑
- **계층 구조**: Project → DsSystem → Flow/Work → Call
- **UI 요소**: Flow 내의 Button, Lamp, Condition, Action
- **API 정의**: ApiDef, ApiCall로 외부 시스템 연동
- **워크플로우 연결**: ArrowBetweenWorks, ArrowBetweenCalls
- **복잡한 Parameter 구조**: JSON 문자열 내 중첩 객체

#### 4. 확장 시나리오
- **실린더 시스템**: `ToAasTest.T2.Project with cylinder: instance -> Aas Test`
- **다중 시스템 인스턴스**: PassiveSystem 처리
- **복제 및 중복 생성**: `edProject.Replicate()`, `edSysCyl.Duplicate()`
- **데이터베이스 통합**: PostgreSQL과 함께 AASX 내보내기

#### 5. AAS Core 3.0 사양 테스트
- **최소 서브모델**: `AasCore3_0Test.submodel min test`
- **Property 요소**: 단일 값 속성 및 타입 검증
- **SubmodelElementCollection**: 중첩 요소 컬렉션
- **Range 타입**: 범위 값 표현
- **복잡한 구조**: 다중 레벨 중첩 요소

## 빌드 명령어

### 프로젝트 빌드
```bash
# UnitTest.Aas 프로젝트만 빌드
cd "F:\Git\aas\submodules\dsev2\src\unit-test\UnitTest.Aas"
dotnet build

# 또는 전체 솔루션에서 빌드
cd "F:\Git\aas\submodules\dsev2\src"
dotnet build dsev2.sln
```

### 테스트 실행
```bash
# UnitTest.Aas 테스트만 실행
dotnet test UnitTest.Aas.fsproj

# 상세 출력과 함께 테스트 실행
dotnet test UnitTest.Aas.fsproj --verbosity normal

# 전체 솔루션의 모든 테스트 실행
cd "F:\Git\aas\submodules\dsev2\src"
dotnet test dsev2.sln
```

### 특정 테스트 클래스 실행
```bash
# FromAasTest의 특정 테스트만 실행
dotnet test --filter "FullyQualifiedName~FromAasTest"

# ToAasTest의 특정 테스트만 실행  
dotnet test --filter "FullyQualifiedName~ToAasTest"
```

## 테스트 데이터

### 내장 JSON 데이터 (TestData.fs)
프로젝트 내부에 하드코딩된 복합 테스트 데이터:

#### DsJson.dsJson - DsSystem 샘플
```json
{
  "RuntimeType": "System",
  "Id": 1,
  "Name": "MainSystem",
  "Flows": [
    {
      "RuntimeType": "Flow",
      "Buttons": [...],
      "Lamps": [...],
      "Conditions": [...],
      "Actions": [...]
    }
  ],
  "Works": [
    {
      "RuntimeType": "Work",
      "Name": "BoundedWork1",
      "Parameter": "{\"Company\":\"dualsoft\",\"Name\":\"kwak\",\"Room\":510}",
      "Calls": [...],
      "Arrows": [...]
    }
  ],
  "ApiDefs": [...],
  "ApiCalls": [...]
}
```

#### DsJson.dsProject - Project 샘플
```json
{
  "RuntimeType": "Project",
  "Id": 4,
  "Name": "MainProject",
  "Database": {
    "Case": "Sqlite",
    "Fields": ["Data Source=:memory:;Version=3;BusyTimeout=20000"]
  },
  "ActiveSystems": [...],
  "PassiveSystems": []
}
```

### AAS 표준 데이터 (AasTest.fs)
#### 최소 AAS 구조
```json
{
  "assetAdministrationShells": [
    {
      "id": "something_142922d6",
      "assetInformation": {
        "assetKind": "NotApplicable",
        "globalAssetId": "something_eea66fa1"
      },
      "modelType": "AssetAdministrationShell"
    }
  ]
}
```

### 테스트 파일 관리 (TestHelpers 모듈)
- **고유 경로 생성**: `getUniqueAasxPath()`, `getUniqueJsonPath()`, `getUniqueSqlitePath()`
- **타임스탬프 기반**: `getUniquePathByTime()`, GUID 기반: `getUniquePathByGuid()`
- **자동 정리**: `cleanupTestFile()`, `cleanupTestFiles()` 함수
- **테스트 디렉토리**: `testDataDir()` 함수로 `test-data` 폴더 관리

### 샘플 프로젝트 생성
- **createHelloDS()**: 간단한 테스트 프로젝트 생성 (UnitTest.Core 제공)
- **createEditableProject()**: 편집 가능한 프로젝트 생성
- **createEditableSystemCylinder()**: 실린더 시스템 생성

## 테스트 패턴 및 기법

### 1. 변환 테스트 패턴
```fsharp
// JSON → 객체 → XML 변환
let env = J.CreateIClassFromJson<Aas.Environment>(aasJson)
let xml = env.ToXml()
xml =~= expectedXml
```

### 2. AASX 파일 테스트 패턴
```fsharp
// 프로젝트 → AASX 파일 생성
let aasxPath = getUniqueAasxPath()
project.ExportToAasxFile(aasxPath)
// 검증 로직
cleanupTestFile aasxPath  // 정리
```

### 3. 라운드트립 테스트 패턴
```fsharp
// 원본 → 변환 → 역변환 → 비교
let original = Project.FromJson(jsonData)
let njProject = NjProject.FromJson(jsonData)
let submodel = njProject.ToSjSubmodel()
let restored = NjProject.FromISubmodel(submodel)
original.ToJson() =~= restored.ToJson()
```

## 기술적 특징

### AAS 표준 준수
- **Asset Administration Shell 3.0 표준** 구현
- **공식 AAS 네임스페이스**: `https://admin-shell.io/aas/3/0`
- **표준 요소 지원**: Property, SubmodelElementCollection, Range, Environment

### 다중 직렬화 형식
- **JSON**: Newtonsoft.Json 기반 (`J.CreateIClassFromJson<T>()`)
- **XML**: AAS XML 스키마 준수 (`ToXml()`, `FromXml()`)
- **AASX**: 압축된 AAS 패키지 형식 (`ExportToAasxFile()`)

### 타입 변환 아키텍처
- **Runtime Type**: Project, DsSystem, Flow, Work, Call (도메인 모델)
- **NJ Type**: NjProject, NjSystem (Newtonsoft Json 직렬화용)
- **AAS Type**: Environment, Submodel, SubmodelElement (AAS 표준)

### F# 함수형 패러다임
- **불변 데이터 구조** 사용
- **파이프라인 연산자** (`|>`) 활용
- **패턴 매칭** 기반 타입 변환
- **컴퓨테이션 표현식** 활용

## 검증 항목

### 1. 구조적 검증
- AAS XML 네임스페이스 확인 (`admin-shell.io`)
- 서브모델 요소 계층 구조 검증
- Property 값 타입 정확성 (`xs:string`, `xs:double`, `xs:boolean`)

### 2. 라운드트립 검증
- **JSON → AAS → JSON** 일관성
- **XML → AAS → XML** 일관성  
- **Runtime Object → AAS → Runtime Object** 일관성

### 3. 파일 시스템 검증
- AASX 파일 생성 및 압축 구조 확인
- 임시 파일 정리 확인 (`cleanupTestFile()`)
- 파일 경로 고유성 보장 (GUID/타임스탬프 기반)

## 관련 프로젝트
- **Ev2.Core.FS**: 핵심 도메인 모델 및 비즈니스 로직
- **Ev2.Aas.FS**: AAS 직렬화/역직렬화 엔진  
- **UnitTest.Core**: 코어 기능 단위 테스트 (TestHelpers 제공)
- **Dual.Common.UnitTest.FS**: 공통 테스트 유틸리티 (FsUnit 확장)

## 확장성 가이드

### 새로운 AAS 요소 추가
1. `TestData.fs`에 새로운 JSON 구조 정의
2. 해당 요소의 변환 테스트 추가 (`FromAasTest`, `ToAasTest`)
3. AAS 사양 준수 검증 테스트 작성 (`AasCore3_0Test`)

### 새로운 직렬화 형식 지원
1. Core 라이브러리에서 변환 로직 구현
2. 해당 형식의 라운드트립 테스트 추가
3. 파일 I/O 테스트 추가 (생성/읽기/정리)

이 프로젝트는 Industry 4.0 표준인 AAS와 DS 엔진 간의 호환성을 보장하는 중요한 역할을 담당하며, Asset Administration Shell 표준의 완전한 구현을 검증합니다.