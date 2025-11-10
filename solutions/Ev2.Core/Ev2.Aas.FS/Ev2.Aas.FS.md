# Ev2.Aas.FS

## 프로젝트 개요
- **프로젝트명**: Ev2.Aas.FS
- **타입**: F# 라이브러리
- **프레임워크**: .NET 9.0
- **언어 버전**: F# 9.0 (프로젝트 파일에서 LangVersion 9.0 설정)
- **네임스페이스**: Dual.Ev2.Aas
- **역할**: Asset Administration Shell (AAS) 3.0 표준 통합 및 AASX 파일 처리
- **문서 생성일**: 2024-09-16

## 설명
Ev2.Core.FS 프로젝트의 AAS (Asset Administration Shell) 패키지 탐색기 형식 파일에 대한 직렬화/역직렬화 기능을 담당합니다. AasCore.Aas3_0 패키지를 사용하여 Runtime 타입 ↔ NJ타입 ↔ AASX 간의 변환을 처리합니다.

### 주요 기능
- **AAS 3.0 표준 준수**: XML/JSON 직렬화 및 AASX 패키지 처리
- **AASX 패키지 처리**: ZIP 기반 AASX 파일 읽기/쓰기/주입
- **타입 변환 체계**: Runtime ↔ NJ ↔ AAS JSON/XML ↔ AASX
- **Semantic URL 매핑**: 확장 속성을 위한 동적 URL 생성
- **확장 메커니즘**: TypeFactory를 통한 동적 타입 지원
- **C#/F# 상호운용성**: Extension 메서드를 통한 언어 간 호환성
- **System.Text.Json 통합**: 고성능 JSON 처리

## 의존성 정보

### TargetFramework
- `net9.0`: .NET 9.0 (AasCore.Aas3_0 호환성 요구사항)

### 프로젝트 참조
- `Ev2.Core.FS`: 핵심 도메인 모델 및 비즈니스 로직

### 패키지 참조
- `AasCore.Aas3_0`: AAS 3.0 표준 구현
  - 조건부 참조: 로컬 프로젝트 우선, 없으면 NuGet 패키지 (v1.0.0) 사용
  - 로컬 경로: `..\..\..\submodules\aas-core3.0-csharp\src\AasCore.Aas3_0\`
  - 주석 처리된 패키지: `System.IO.Packaging` (v9.0.6)

## 파일 구조

```
Ev2.Aas.FS/
├── Ev2.Aas.FS.fsproj          # 프로젝트 파일
├── CLAUDE.md                  # 프로젝트 지침
├── Ev2.Aas.FS.md             # 이 문서
│
├── 핵심 소스 파일/
│   ├── Prelude.fs             # 공통 유틸리티 (문자열 처리, 상수)
│   ├── Core.Aas.fs            # AAS 핵심 타입, Semantic URL 매핑
│   ├── AAS.Extensions.fs      # IHasSemantics, UniqueInfo, SME 확장
│   ├── Core.To.Aas.fs         # Core → AAS 변환 (NJ → JSON)
│   ├── Core.From.Aas.fs       # AAS → Core 변환 (JSON → NJ)
│   ├── AasX.fs                # AASX 패키지 처리 (ZIP, XML)
│   └── Ev2AasExtensionForCSharp.fs  # C# Extension 메서드 래퍼
│
├── 참조 파일/
│   ├── AasSyntax.md           # AAS 구문 가이드
│   ├── maximal.json           # AAS 최대 구성 예제
│   └── minimal.json           # AAS 최소 구성 예제
│
├── 개발 문서/
│   └── devdoc/
│       ├── AAS type 정의.md   # AAS 타입 정의
│       ├── AAS 검증.md        # AAS 검증 가이드
│       ├── Project.AasXml.md  # 프로젝트 AAS XML 매핑
│       ├── how-to-add-type-member.md  # 타입 멤버 추가 가이드
│       └── SampleAASFolder/   # AAS 샘플 폴더 구조
│           ├── _rels/         # 관계 파일
│           ├── aasx/          # AASX 내부 구조
│           └── [Content_Types].xml
│
└── 빌드 출력/
    ├── bin/
    └── obj/
```

## 주요 컴포넌트

### 1. 핵심 모듈

#### Prelude.fs
- **문자열 유틸리티**: `surround`, `escapeQuote`, `singleQuote`, `doubleQuote`
- **AAS 상수**: `SubmodelIdShort = "SequenceControlSubmodel"`
- **내부 모듈**: `PreludeModule` (AutoOpen으로 자동 임포트)

#### Core.Aas.fs
- **JSON 타입 별칭**:
  - `JNode = System.Text.Json.Nodes.JsonNode`
  - `JObj = System.Text.Json.Nodes.JsonObject`
  - `JArr = System.Text.Json.Nodes.JsonArray`
- **AasSemantics.map**: Semantic URL 매핑 Dictionary
  - **기본 객체**: Submodel, Project, System, FakeSystemSubmodel
  - **고유 식별자**: Name, Guid, Id, Parameter
  - **시스템 속성**: IRI, Author, EngineVersion, LangVersion, Description, DateTime
  - **컬렉션**: ApiDefs, ApiCalls, Works, Arrows, Calls, Flows, Buttons 등
  - **URL 패턴**: `https://dualsoft.com/aas/{category}/{type}`

#### AAS.Extensions.fs
- **IHasSemantics 확장**:
  - `hasSemanticKey()`: SemanticId 키 매칭 메서드
  - AasSemantics.map 및 TypeFactory 기반 검색
- **UniqueInfo 레코드**: `{ Name: string; Guid: Guid; Parameter: string; Id: Id option }`
- **SMEsExtension 클래스**: SubmodelElement 컬렉션 확장 메서드
  - `TryGetPropValueBySemanticKey()`: Semantic 키로 속성 값 조회
  - `TryGetPropValueByCategory()`: 카테고리로 속성 값 조회
  - `CollectChildrenSMEWithSemanticKey()`: Semantic 키로 자식 요소 수집

### 2. 변환 모듈

#### Core.To.Aas.fs
- **NjUnique 확장 메서드**:
  - `tryCollectPropertiesNjUnique()`: Name, Guid, Parameter, Id 기본 속성 수집
  - `tryCollectExtensionProperties()`: TypeFactory를 통한 확장 속성 수집
  - `CollectProperties()`: 모든 속성을 JNode 배열로 수집
- **변환 대상**: NjProject, NjSystem, NjFlow, NjWork, NjCall 등
- **Newtonsoft.Json 사용**: JSON 직렬화 처리

#### Core.From.Aas.fs
- **공통 변환 함수**:
  - `createSimpleFromSMC<'T>()`: UniqueInfo 기반 단순 객체 생성
  - `readAasExtensionProperties()`: TypeFactory를 통한 확장 속성 읽기
- **NjProject 정적 메서드**:
  - `FromAasxFile()`: AASX 파일에서 NjProject 생성
  - `FromISubmodel()`: ISubmodel에서 NjProject 생성
- **변환 과정**: AAS → SubmodelElementCollection → NjObject

#### AasX.fs
- **NjProject 확장 메서드**:
  - `ToSjENV()`: SubmodelJSON 환경 객체 생성
  - `ExportToAasxFile()`: 새 AASX 파일 생성
  - `InjectToExistingAasxFile()`: 기존 AASX에 Submodel 주입
  - `ToAasJsonStringENV()`: AAS JSON 문자열 생성
  - `ToENV()`: AAS Environment 객체 생성
- **AASX 구조**: AssetAdministrationShell, AssetInformation, Submodel 생성
- **ZIP 처리**: System.IO.Compression 사용
- **XML/JSON 직렬화**: AasCore.Aas3_0 라이브러리 활용

### 3. 상호운용성

#### Ev2AasExtensionForCSharp.fs
- **Ev2AasExtensionForCSharp 클래스**: Project 확장 메서드
  - `CsExportToAasxFile()`: AASX 파일 내보내기 (옵션: DbApi 포함)
  - `CsInjectToExistingAasxFile()`: 기존 AASX에 Submodel 주입
  - `CsUpdateDbAasXml()`: 데이터베이스 AAS XML 업데이트
  - `CsToAasJsonString()`: NjProject → AAS JSON 문자열
  - `CsToENV()`: NjProject → AAS Environment
- **AasxExtensions 클래스**: 정적 유틸리티 메서드
  - `FromAasxFile()`: AASX → Project 변환
  - `CsTrySetProperty<'T>()`: JObj 속성 설정
  - `CsTryGetPropValue<'T>()`: SubmodelElementCollection 속성 조회
- **C# 호환성**: `[<Extension>]`, `[<Optional>]`, `[<DefaultParameterValue>]` 사용

## 아키텍처 특징

### 타입 변환 체계
```
Runtime Type (Project, DsSystem, etc.)
    ↕
NJ Type (NjProject, NjSystem, etc.)
    ↕
AAS JSON/XML
    ↕
AASX Package File
```

### Semantic URL 체계
- **기본 객체**: `https://dualsoft.com/aas/{type}` (예: project, system, submodel)
- **확장 속성**: `https://dualsoft.com/aas/extension/{type}/{property}` (TypeFactory를 통한 동적 생성)
- **고유 식별자**: `https://dualsoft.com/aas/unique/{identifier}` (name, guid, id, parameter)
- **복수 컬렉션**: `https://dualsoft.com/aas/plural/{collection}` (apiDefs, works, flows 등)
- **시스템 속성**: `https://dualsoft.com/aas/system/{property}` (iri, author, engineVersion 등)

### 확장 메커니즘
- **TypeFactory 통합**: `getTypeFactory()`를 통한 동적 타입 지원
- **확장 속성 수집**: `WriteAasExtensionProperties()`, `ReadAasExtensionProperties()`
- **Semantic URL 생성**: 확장 타입별 자동 URL 매핑
- **타입 식별**: JSON `$type` 필드 및 semantic 키 기반 매칭
- **양방향 변환**: Core ↔ AAS 확장 속성 자동 변환

## 빌드 명령어

### 개발 환경 빌드
```bash
# 프로젝트 빌드
dotnet build Ev2.Aas.FS.fsproj

# Release 모드 빌드
dotnet build Ev2.Aas.FS.fsproj --configuration Release

# 의존성 복원
dotnet restore Ev2.Aas.FS.fsproj
```

### 솔루션 레벨 빌드
```bash
# 전체 솔루션 빌드 (상위 디렉토리에서)
cd ../../
dotnet build dsev2.sln

# 테스트 실행 (AAS 기능 테스트)
dotnet test ../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj
```

### 패키지 참조 확인
```bash
# 패키지 종속성 확인
dotnet list package

# 프로젝트 참조 확인
dotnet list reference
```

## 테스트

### 단위 테스트 프로젝트
- **위치**: `../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj`
- **테스트 범위**: 
  - AASX 변환 테스트
  - AAS 표준 호환성 검증
  - 확장 속성 직렬화/역직렬화
  - JSON/XML 변환 정확성

### 테스트 실행
```bash
# AAS 테스트 실행
dotnet test ../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj

# 상세 출력으로 테스트
dotnet test ../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj --verbosity normal
```

## 사용 예시

### F# 사용법
```fsharp
open Dual.Ev2.Aas
open Ev2.Core.FS

// Project를 AASX로 내보내기
let project = (* 생성된 Project 객체 *)
project.ExportToAasxFile("output.aasx")

// NjProject를 AAS JSON 문자열로 변환
let njProject = project.ToNjProject()
let aasJson = njProject.ToAasJsonStringENV()

// AASX 파일에서 Project 읽기
let loadedProject = NjProject.FromAasxFile("input.aasx") |> fun nj -> nj.ToJson() |> Project.FromJson
```

### C# 사용법
```csharp
using Dual.Ev2.Aas;
using Ev2.Core.FS;

// C# 확장 메서드 사용
var project = /* 생성된 Project 객체 */;
project.CsExportToAasxFile("output.aasx");

// NjProject를 통한 AAS JSON 생성
var njProject = project.ToNjProject();
var aasJson = njProject.CsToAasJsonString();

// AASX 파일에서 Project 읽기
var loadedProject = AasxExtensions.FromAasxFile("input.aasx");
```

## 관련 표준

### AAS (Asset Administration Shell) 3.0
- **표준 기관**: Industrial Digital Twin Association (IDTA)
- **목적**: Industry 4.0 디지털 트윈 표준화
- **파일 형식**: AASX (ZIP 기반 패키지), JSON, XML

### 지원 형식
- **AASX**: 압축된 AAS 패키지 (권장)
- **JSON**: 웹 API 및 클라우드 통합용
- **XML**: 엔터프라이즈 시스템 통합용

## 문제 해결

### 일반적인 문제
1. **AasCore.Aas3_0 패키지 오류**: .NET 9.0 프레임워크 확인
2. **Semantic URL 충돌**: AasSemantics.map 테이블 확인
3. **확장 속성 누락**: 타입 등록 및 TypeFactory 확인

### 디버깅 팁
- AAS JSON 출력을 통한 변환 결과 확인
- 단위 테스트를 통한 개별 기능 검증
- 로그 출력을 통한 변환 과정 추적

## 향후 개발 계획
- 확장 속성 처리 기능 완성 (현재 주석 처리됨)
- AAS 4.0 표준 대응 준비
- 성능 최적화 (대용량 AASX 파일 처리)
- 추가 AAS 검증 규칙 구현