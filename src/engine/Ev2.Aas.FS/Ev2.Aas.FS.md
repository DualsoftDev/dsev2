# Ev2.Aas.FS

## 프로젝트 개요

Ev2.Aas.FS는 DSEv2 엔진의 AAS (Asset Administration Shell) 통합 레이어를 담당하는 F# 프로젝트입니다. 이 프로젝트는 Ev2.Core.FS의 런타임 타입들을 AAS 표준 형식(JSON/XML/AASX)으로 직렬화/역직렬화하는 기능을 제공합니다.

### 주요 특징
- **AAS 표준 지원**: Asset Administration Shell 3.0 표준 구현
- **다중 형식 지원**: JSON, XML, AASX 파일 형식 지원
- **타입 변환**: Runtime Type ↔ NJ Type ↔ AAS 형식 간 변환
- **확장 시스템**: 써드파티 확장 타입에 대한 직렬화 지원
- **AASX 패키지**: 압축된 AAS 패키지 파일 읽기/쓰기

## 기술 정보

### 타겟 프레임워크
- **.NET 9.0** (net9.0)
- **F# 언어 버전**: 9.0
- **루트 네임스페이스**: Dual.Ev2.Aas

### 의존성

#### 프로젝트 참조
- **Ev2.Core.FS**: 핵심 도메인 모델 및 비즈니스 로직
- **AasCore.Aas3_0**: AAS 3.0 표준 구현 (조건부 참조)
  - 로컬 프로젝트 참조: `../../../submodules/aas-core3.0-csharp/src/AasCore.Aas3_0/AasCore.Aas3_0.csproj`
  - NuGet 패키지: `AasCore.Aas3_0` v1.0.0 (로컬 프로젝트 없을 시)

#### 주요 .NET 라이브러리
- **System.Text.Json**: JSON 직렬화/역직렬화
- **System.Xml**: XML 처리
- **System.IO.Compression**: AASX 압축 파일 처리
- **Newtonsoft.Json**: JSON 처리 (일부 기능)

## 파일 구조

```
Ev2.Aas.FS/
├── Ev2.Aas.FS.fsproj          # 프로젝트 파일
├── CLAUDE.md                  # 프로젝트 지침 문서
├── Ev2.Aas.FS.md             # 이 문서
│
├── 핵심 소스 파일/
│   ├── Prelude.fs             # 공통 유틸리티 및 상수
│   ├── Core.Aas.fs            # AAS 핵심 타입 및 JSON 확장
│   ├── AAS.Extensions.fs      # AAS 타입 확장 메서드
│   ├── Core.To.Aas.fs         # Runtime Type → AAS 변환
│   ├── Core.From.Aas.fs       # AAS → Runtime Type 변환
│   ├── AasX.fs                # AASX 파일 처리
│   └── Ev2AasExtensionForCSharp.fs  # C# 상호운용성 지원
│
├── 개발 문서/
│   └── devdoc/
│       ├── AAS type 정의.md
│       ├── AAS 검증.md
│       ├── Project.AasXml.md
│       └── SampleAASFolder/   # 샘플 AAS 구조
│
└── 테스트 데이터/
    ├── maximal.json           # 최대 구성 샘플
    ├── minimal.json           # 최소 구성 샘플
    └── AasSyntax.md          # AAS 문법 가이드
```

## 주요 컴포넌트

### 1. 핵심 네임스페이스 및 타입

#### `Dual.Ev2.Aas`
- **JNode**: System.Text.Json.Nodes.JsonNode의 축약형
- **JObj**: System.Text.Json.Nodes.JsonObject의 축약형
- **JArr**: System.Text.Json.Nodes.JsonArray의 축약형

#### AAS 시맨틱 매핑 (`AasSemantics`)
```fsharp
// 주요 시맨틱 URL 매핑
"Project" -> "https://dualsoft.com/aas/project"
"System"  -> "https://dualsoft.com/aas/system"
"Name"    -> "https://dualsoft.com/aas/unique/name"
"Guid"    -> "https://dualsoft.com/aas/unique/guid"
```

### 2. JSON 확장 기능 (`JsonExtensionModule`)

#### 열거형 타입들
- **Category**: PARAMETER, CONSTANT, VARIABLE
- **SemanticIdType**: ExternalReference, GlobalReference, ModelReference
- **ModelType**: Property, Submodel, SubmodelElementCollection 등
- **KindType**: Template, Instance, TemplateQualifier

#### JSON 객체 확장 메서드
```fsharp
type System.Text.Json.Nodes.JsonObject with
    member x.Set(key:N, value:string): JObj
    member x.SetTypedValue<'T>(value:'T): JObj option
    member x.SetSemantic(semanticKey:string): JObj
    member x.TrySetProperty<'T>(value:'T, name:string): JObj option
```

### 3. AAS 확장 기능 (`AasExtensions`)

#### ISubmodelElement 확장
```fsharp
type SMEsExtension =
    static member TryGetPropValueBySemanticKey(smc:ISubmodelElement seq, semanticKey:string): string option
    static member CollectChildrenSMEWithSemanticKey(smc:ISubmodelElement seq, semanticKey: string): ISubmodelElement []
    static member TryGetPropValue<'T>(smc:ISubmodelElement seq, propName: string): 'T option
```

#### SubmodelElementCollection 확장
```fsharp
type SubmodelElementCollection with
    member smc.ReadUniqueInfo(): UniqueInfo
    member smc.TryGetPropValue<'T>(propName: string): 'T option
    member smc.GetSMC(semanticKey: string): SubmodelElementCollection []
```

### 4. AASX 파일 처리 (`AasXModule`)

#### 주요 기능
- **버전 감지**: XML에서 AAS 버전 자동 감지 (1.0, 2.0, 3.0)
- **파일 구조 파싱**: AASX 압축 파일 내부 구조 분석
- **Environment 추출**: AASX에서 AAS Environment 객체 생성
- **업데이트 지원**: 기존 AASX 파일의 Submodel 업데이트

```fsharp
// 주요 함수들
val readEnvironmentFromAasx: string -> {| FilePath: string; Version: string; Environment: Aas.Environment; OriginalXml: string |}
val updateSubmodels: Aas.Environment -> Aas.Submodel -> ResizeArray<ISubmodel>
val createUpdatedAasxFile: string -> string -> Aas.Environment -> string
```

### 5. 타입 변환 시스템

#### Runtime Type → AAS 변환 (`Core.To.Aas.fs`)
- NjUnique 타입의 AAS 속성 변환
- 확장 타입별 특별 속성 처리
- JSON 기반 Submodel 생성

#### AAS → Runtime Type 변환 (`Core.From.Aas.fs`)
- AAS Submodel에서 Runtime 객체 생성
- 확장 속성 파싱 및 적용
- 타입별 역직렬화 지원

### 6. C# 상호운용성 (`Ev2AasExtensionForCSharp.fs`)
F#의 타입 확장이 C#에서 인식되지 않는 문제를 해결하기 위한 래퍼 메서드들을 제공합니다.

## 빌드 및 테스트

### 빌드 명령어
```bash
# 프로젝트 빌드
cd F:\Git\aas\submodules\dsev2\src\engine\Ev2.Aas.FS
dotnet build Ev2.Aas.FS.fsproj

# 솔루션 전체 빌드
cd F:\Git\aas\submodules\dsev2\src
dotnet build dsev2.sln
```

### 테스트 실행
```bash
# AAS 관련 테스트 실행
cd F:\Git\aas\submodules\dsev2\src
dotnet test unit-test/UnitTest.Aas/UnitTest.Aas.fsproj

# 전체 테스트 실행
dotnet test dsev2.sln
```

### 문서 생성
프로젝트는 `GenerateDocumentationFile` 옵션이 활성화되어 있어 빌드 시 XML 문서가 자동 생성됩니다.

## 확장 메커니즘

이 프로젝트는 써드파티 확장을 지원합니다:

1. **확장 타입 등록**: TypeFactory를 통한 동적 타입 등록
2. **시맨틱 URL 생성**: 확장 속성용 고유 시맨틱 URL 자동 생성
3. **직렬화 지원**: 확장 속성의 AAS 표준 형식 변환
4. **역직렬화 지원**: AAS에서 확장 타입으로 복원

### 확장 속성 시맨틱 URL 패턴
```
https://dualsoft.com/aas/extension/{typeName}/{propertyName}
```

## 주요 상수

### Submodel 식별자
```fsharp
let [<Literal>] SubmodelIdShort = "SequenceControlSubmodel"
```

## 관련 프로젝트

- **Ev2.Core.FS**: 핵심 도메인 모델
- **UnitTest.Aas**: AAS 기능 테스트
- **Hmc.Aas**: 써드파티 확장 예시 프로젝트

## 참고 문서

- [AAS 표준 문서](https://www.plattform-i40.de/PI40/Navigation/EN/Standardisation/AAS/aas.html)
- [AasCore.Aas3_0 라이브러리](https://github.com/aas-core-works/aas-core3.0-csharp)
- 프로젝트 내 devdoc 폴더의 상세 문서들