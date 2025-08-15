# UnitTest.Aas

## 프로젝트 개요

UnitTest.Aas는 Ev2.Aas.FS 프로젝트의 AAS(Asset Administration Shell) 관련 기능을 테스트하는 단위 테스트 프로젝트입니다. 이 프로젝트는 F#으로 작성되었으며, NUnit 테스트 프레임워크와 FsUnit를 사용하여 AAS 직렬화/역직렬화, AASX 파일 처리, JSON/XML 변환 등의 기능을 검증합니다.

### 주요 기능
- AAS 표준 형식(JSON, XML, AASX)과 DS 엔진 내부 객체 간의 변환 테스트
- AASX 패키지 파일 생성 및 읽기 테스트
- AasCore.Aas3_0 라이브러리를 통한 AAS 3.0 표준 호환성 테스트
- Project, DsSystem, Work, Call 등 핵심 도메인 객체의 AAS 매핑 테스트

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
- AAS JSON ↔ AAS XML 변환 검증
- DS JSON ↔ Runtime 객체 변환 검증

#### 2. AASX 파일 처리
- Project → AASX 파일 내보내기
- AASX 파일에서 서브모델 XML 추출
- AASX 파일 정리 (테스트 후 cleanup)

#### 3. 도메인 객체 매핑
- Project, DsSystem, Work, Call 계층 구조
- Flow, Button, Lamp, Condition, Action UI 요소
- ApiDef, ApiCall API 정의 및 호출
- Arrow 객체를 통한 워크플로우 연결

#### 4. 확장 시나리오
- 실린더 시스템과 같은 복합 객체 테스트
- 다중 시스템 인스턴스 처리
- 복제 및 중복 생성 테스트

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

### 샘플 구조
테스트에서 사용되는 주요 데이터 구조:

```json
{
  "RuntimeType": "Project",
  "ActiveSystems": [
    {
      "RuntimeType": "System",
      "Flows": [...],
      "Works": [
        {
          "RuntimeType": "Work", 
          "Calls": [...],
          "Arrows": [...]
        }
      ],
      "ApiDefs": [...],
      "ApiCalls": [...]
    }
  ]
}
```

### 테스트 파일 관리
- 임시 테스트 파일은 `getUniqueAasxPath()`, `getUniqueJsonPath()` 함수로 생성
- 테스트 완료 후 `cleanupTestFile()`, `cleanupTestFiles()` 함수로 정리
- 테스트 데이터 디렉토리는 `testDataDir()` 함수로 관리

## 관련 프로젝트
- **Ev2.Core.FS**: 핵심 도메인 모델 및 비즈니스 로직
- **Ev2.Aas.FS**: AAS 직렬화/역직렬화 엔진  
- **UnitTest.Core**: 코어 기능 단위 테스트
- **Dual.Common.UnitTest.FS**: 공통 테스트 유틸리티

이 프로젝트는 Industry 4.0 표준인 AAS와 DS 엔진 간의 호환성을 보장하는 중요한 역할을 담당합니다.