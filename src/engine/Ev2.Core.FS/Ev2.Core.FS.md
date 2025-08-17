# Ev2.Core.FS

## 프로젝트 개요
- **프로젝트명**: Ev2.Core.FS
- **타입**: F# 라이브러리
- **프레임워크**: .NET Standard 2.0
- **언어 버전**: F# 9.0 (LangVersion)
- **출력 타입**: Library
- **문서 생성**: 활성화됨 (GenerateDocumentationFile)
- **다국어 지원**: 영어(en), 한국어(ko)

DS Engine Version 2의 핵심 구현 프로젝트로, 산업 자동화 시스템을 위한 도메인 모델과 비즈니스 로직을 F# 함수형 프로그래밍 패러다임으로 구현합니다.

## 주요 기능
- 계층적 산업 자동화 시스템 객체 모델 구현
- Runtime, JSON, ORM 간 타입 변환 시스템 (Triple Type System)
- SQLite/PostgreSQL 데이터베이스 지원 (Dapper ORM)
- Newtonsoft.Json 기반 직렬화/역직렬화
- Third Party 확장 시스템 지원 (ITypeFactory)
- 반응형 프로그래밍 (System.Reactive)
- C#/F# 상호운용성 지원

## 의존성 정보

### Target Framework
- **TargetFramework**: netstandard2.0
- **LangVersion**: F# 9.0
- **OutputType**: Library
- **SatelliteResourceLanguages**: en;ko

### NuGet 패키지 참조
| 패키지 | 버전 | 용도 |
|--------|------|------|
| System.Reactive | 6.0.1 | 반응형 프로그래밍 및 이벤트 스트림 처리 |
| log4net | 2.0.17 | 로깅 프레임워크 |
| Newtonsoft.Json | 13.0.3 | JSON 직렬화/역직렬화 |
| Dapper | 2.1.35 | 경량 마이크로 ORM |

### 프로젝트 참조
| 프로젝트 | 경로 | 역할 |
|----------|------|------|
| Dual.Common.Base.FS | ../../../submodules/nuget/Common/ | 기본 유틸리티 및 C#/F# 상호운용성 |
| Dual.Common.Core.FS | ../../../submodules/nuget/Common/ | 함수형 프로그래밍 핵심 유틸리티 |
| Dual.Common.Db.FS | ../../../submodules/nuget/Common/ | 멀티 데이터베이스 지원 라이브러리 |

## 파일 구조

```
Ev2.Core.FS/
├── Ev2.Core.FS.fsproj          # 프로젝트 파일
├── AppSettings.fs              # 애플리케이션 설정
├── ConstEnums.fs              # 상수 및 열거형
├── Interfaces.fs              # 핵심 인터페이스 정의
├── Unique.fs                  # 고유 식별자 관리
├── TypeFactory.fs             # 타입 팩토리 (확장 시스템)
├── AbstractClasses.fs         # 추상 클래스
├── DsObjectUtils.fs          # 도메인 객체 유틸리티
├── Ev2CoreExtensionForCSharp.fs # C# 호환성 확장
├── MiniSample.fs             # 샘플 데이터 생성
├── EntryPoint.fs             # 모듈 초기화 진입점
├── AssemblyInfo.fs           # 어셈블리 정보
├── appSettings.json          # 설정 파일
├── appSettings.json.template # 설정 템플릿
├── database/                 # 데이터베이스 레이어
│   ├── Database.Schema.fs    # 스키마 정의
│   ├── Database.ORM.fs       # ORM 매핑
│   └── AppDbApi.fs          # 데이터베이스 API
├── TypeConversion,Serialization/ # 타입 변환 및 직렬화
│   ├── NewtonsoftJsonDsObjects.fs # JSON 직렬화
│   ├── DsCopy.Object.fs      # 객체 복사
│   ├── DsCopy.Properties.fs  # 속성 복사
│   ├── DsCompare.Objects.fs  # 객체 비교
│   ├── DB.Select.fs          # 데이터베이스 조회
│   ├── DB.Insert.fs          # 데이터베이스 삽입
│   ├── DB.Update.fs          # 데이터베이스 업데이트
│   └── DB.API.fs             # 데이터베이스 API
├── Extension/                # 확장 시스템 폴더
└── DevDoc/                   # 개발 문서
    ├── DbConstraint.md
    ├── ErrorMessages.md
    ├── 속성 추가 절차.md
    ├── 저장 포맷.md
    └── 확장.md
```

## 주요 컴포넌트

### 1. 도메인 모델 (`Interfaces.fs`)
**네임스페이스**: `Ev2.Core.FS`

#### 핵심 인터페이스 계층구조
```fsharp
IDsObject (기본 객체)
├── IParameter (매개변수)
├── IParameterContainer (매개변수 컨테이너)
├── IArrow (화살표 연결)
└── IUnique (고유 식별자)
    ├── IWithDateTime (시간 정보)
    └── IDs1stClass (1급 객체: IUnique + IWithDateTime)
        ├── IDsProject (프로젝트)
        └── IDsSystem (시스템)
```

#### 산업 자동화 도메인 모델
- **IDsProject** - 프로젝트 (최상위 컨테이너, 여러 시스템 포함)
- **IDsSystem** - 시스템 (자동화 시스템, 플로우/작업/API 정의 포함)
- **IDsFlow** - 플로우 (UI 요소: 버튼, 램프, 조건, 액션)
- **IDsWork** - 작업 (실행 가능한 단위, 호출들을 포함)
- **IDsCall** - 호출 (개별 실행 단계, API 호출과 연결)
- **IDsApiCall/IDsApiDef** - API 정의 및 호출 (외부 시스템 인터페이스)

#### Triple Type System (3중 타입 시스템)
각 도메인 객체는 세 가지 표현을 가집니다:

1. **Runtime Types (IRt\*)**
   - `IRtProject`, `IRtSystem`, `IRtFlow`, `IRtWork`, `IRtCall`
   - 메모리에서 실행되는 실제 비즈니스 객체
   - 불변성과 타입 안전성 보장

2. **JSON Types (INj\*)**
   - `INjProject`, `INjSystem`, `INjFlow`, `INjWork`, `INjCall`  
   - Newtonsoft.Json 직렬화를 위한 객체
   - $type 필드를 통한 다형성 지원

3. **ORM Types (IORM\*)**
   - `IORMProject`, `IORMSystem`, `IORMFlow`, `IORMWork`, `IORMCall`
   - 데이터베이스 테이블 매핑 객체
   - Dapper와 완벽 호환

### 2. 확장 시스템 (`TypeFactory.fs`)
#### ITypeFactory 인터페이스
Third Party 확장을 위한 핵심 인터페이스:

```fsharp
type ITypeFactory =
    // 스키마 확장
    abstract ModifySchema : baseSchema:string -> string
    abstract PostCreateDatabase : conn:IDbConnection * tr:IDbTransaction -> unit
    
    // 타입 생성
    abstract CreateRuntime : runtimeType:Type -> IRtUnique  
    abstract CreateNj : njType:Type -> INjUnique
    
    // 속성 처리
    abstract CopyProperties: source:IUnique * target:IUnique -> unit
    abstract DeserializeJson: typeName:string * jsonString:string * settings:JsonSerializerSettings -> INjUnique
    
    // 데이터베이스 후처리
    abstract HandleAfterInsert : IRtUnique * IDbConnection * IDbTransaction -> unit
    abstract HandleAfterUpdate : IRtUnique * IDbConnection * IDbTransaction -> unit
    abstract HandleAfterDelete : IRtUnique * IDbConnection * IDbTransaction -> unit
    abstract HandleAfterSelect : IRtUnique * IDbConnection * IDbTransaction -> unit
    
    // 비교 및 AAS 확장
    abstract ComputeExtensionDiff : obj1:IRtUnique * obj2:IRtUnique -> seq<ICompareResult>
    abstract GetSemanticId : semanticKey:string -> string
    abstract WriteAasExtensionProperties : njObj:INjUnique -> seq<System.Text.Json.Nodes.JsonObject option>
```

#### 확장 메커니즘
- **TypeFactory 전역 변수**: C# 친화적인 null 허용 설계
- **createWithFallback**: 확장 타입 생성 시 fallback 지원
- **확장 타입 agnostic**: Core 엔진은 확장 타입을 몰라도 동작

### 3. 데이터베이스 시스템
#### 스키마 정의 (`Database.Schema.fs`)
```fsharp
module Tn =  // Table Names
    let Project = "project"
    let System = "system"  
    let Flow = "flow"
    let Work = "work"
    let Call = "call"
    let ArrowWork = "arrowWork"
    let ArrowCall = "arrowCall"
    let ApiCall = "apiCall"
    let ApiDef = "apiDef"
    
    // 매핑 테이블 (N:M 관계)
    let MapProject2System = "mapProject2System"
    let MapCall2ApiCall = "mapCall2ApiCall"
```

#### ORM 레이어 (`Database.ORM.fs`)
- Dapper 기반 경량 ORM
- 타입 안전 데이터베이스 접근
- 트랜잭션 지원 및 연결 풀 관리
- 자동 스키마 생성 및 마이그레이션

#### API 레이어 (`AppDbApi.fs`)
- 고수준 데이터베이스 API 제공
- 연결 관리 및 에러 처리
- 로깅 및 성능 모니터링

### 4. 직렬화 시스템 (`TypeConversion,Serialization/`)
#### JSON 직렬화 (`NewtonsoftJsonDsObjects.fs`)
```fsharp
// 기본 JSON 객체
[<AbstractClass>]
type NjUnique() =
    inherit Unique()
    interface INjUnique
    
    // 디버깅용 런타임 타입 정보
    [<JsonProperty(Order = -101)>] 
    member val private RuntimeType = ...
    
    // Runtime 객체와의 연결
    [<JsonIgnore>]
    member x.RuntimeObject : Unique
```

#### 계층별 엔티티 클래스
```fsharp
[<AbstractClass>] type NjProjectEntity() = inherit NjUnique()
[<AbstractClass>] type NjSystemEntity() = inherit NjUnique() 
[<AbstractClass>] type NjFlowEntity() = inherit NjUnique()
[<AbstractClass>] type NjWorkEntity() = inherit NjUnique()
[<AbstractClass>] type NjCallEntity() = inherit NjUnique()
```

#### 타입 변환 시스템
| 변환 방향 | 파일 | 기능 |
|-----------|------|------|
| Runtime → JSON | DsCopy.Object.fs | 객체 구조 복사 |
| JSON → Runtime | DsCopy.Properties.fs | 속성 복사 |
| Runtime ↔ ORM | DB.Select/Insert/Update.fs | 데이터베이스 CRUD |
| 객체 비교 | DsCompare.Objects.fs | 버전 비교 및 diff |

### 5. 상수 및 열거형 (`ConstEnums.fs`)
#### 핵심 열거형 정의
```fsharp
type DbCallType = Normal | Parallel | Repeat
type DbArrowType = None | Start | Reset | StartReset
type DbStatus4 = Ready | Going | Finished | Homing
```

#### 확장 함수 및 유틸리티
- ResizeArray 확장 (`AddAsSet`, `VerifyAddAsSet`)
- DateTime 확장 (`TruncateToSecond`)
- 날짜 형식 상수 (`DateFormatString`)

### 6. 모듈 초기화 (`EntryPoint.fs`)
#### 시스템 초기화 프로세스
- `ModuleInitializer.Initialize()` - 전역 초기화 함수
- 함수 포인터 설정 (forward declarations)
- 설정 파일 로딩 및 검증
- 로깅 시스템 초기화

### 7. C# 상호운용성 (`Ev2CoreExtensionForCSharp.fs`)
F#에서 정의된 확장 메서드를 C#에서 접근 가능하도록 wrapping:
```fsharp
namespace Ev2.Core.FS
open System.Runtime.CompilerServices

type Ev2CoreExtensionForCSharp =
    [<Extension>] static member CsMethodName(obj: SomeType): ReturnType = 
        obj.FSharpMethodName()
```

## 아키텍처 패턴

### 1. 계층적 객체 모델
```
Project (프로젝트) - 최상위 컨테이너
├── DsSystem (시스템) - 자동화 시스템 
    ├── Flow (플로우) - UI 플로우 정의
    │   ├── Button, Lamp, Condition, Action - UI 요소들
    │   └── Work (작업) - 실행 가능한 단위
    │       └── Call (호출) - 개별 실행 단계
    │           └── ApiCall → ApiDef - 외부 API 연동
    └── ArrowBetweenWorks (작업 간 화살표) - 플로우 제어
        └── ArrowBetweenCalls (호출 간 화살표) - 세부 실행 순서
```

### 2. 이중성(Duality) 원리
객체가 맥락에 따라 다른 역할을 수행할 수 있는 핵심 개념:

#### 구조적 이중성
- **System ⊕ Device**: 시스템이 디바이스로도 동작
- **Instance ⊕ Reference**: 인스턴스가 참조로도 사용

#### 실행적 이중성  
- **원인 ⊕ 결과**: 동일 객체가 원인이자 결과
- **ReadTag ⊕ WriteTag**: 읽기/쓰기 태그의 이중 역할

### 3. Triple Type System
각 도메인 객체의 세 가지 표현과 변환:

```
Runtime Types (IRt*) ←→ JSON Types (INj*) ←→ ORM Types (IORM*)
       ↑                        ↑                      ↑
   비즈니스 로직           직렬화/역직렬화        데이터베이스 매핑
```

### 4. 확장 메커니즘
- **Core Agnostic**: 핵심 엔진은 확장 타입을 몰라도 동작
- **Dynamic Type Creation**: ITypeFactory를 통한 런타임 타입 생성
- **Multi-format Support**: 확장 타입의 JSON, Database, AAS 직렬화

## 설계 원칙

### 1. 함수형 프로그래밍 (Functional Programming)
- **불변성 (Immutability)**: 데이터 구조의 불변성 보장
- **타입 안전성 (Type Safety)**: F# 타입 시스템 활용
- **순수 함수 (Pure Functions)**: 부작용과 비즈니스 로직 분리
- **컴포지션 (Composition)**: 작은 함수의 조합으로 복잡한 로직 구성

### 2. 도메인 주도 설계 (Domain-Driven Design)
- **계층 분리**: Domain, Infrastructure, Application 계층 구분
- **도메인 모델 중심**: 비즈니스 로직이 도메인 모델에 집중
- **유비쿼터스 언어**: 도메인 전문가와 개발자 간 공통 언어

### 3. 확장성 (Extensibility)
- **플러그인 아키텍처**: ITypeFactory 기반 확장 시스템
- **C# 상호 운용성**: F#/C# 간 원활한 통합
- **Third Party 지원**: Core 엔진 수정 없이 타입 확장

### 4. 관심사 분리 (Separation of Concerns)
- **타입별 분리**: Runtime, JSON, ORM 타입의 명확한 역할 구분
- **레이어별 분리**: 데이터베이스, 직렬화, 비즈니스 로직 분리
- **기능별 분리**: 각 파일이 단일 책임 원칙 준수

## 빌드 명령어

### 기본 빌드
```bash
# 프로젝트 빌드
cd F:\Git\aas\submodules\dsev2\src
dotnet build engine/Ev2.Core.FS/Ev2.Core.FS.fsproj

# 전체 솔루션 빌드  
dotnet build dsev2.sln

# Release 모드 빌드
dotnet build engine/Ev2.Core.FS/Ev2.Core.FS.fsproj --configuration Release
```

### 개발 워크플로우
```bash
# 의존성 복원
dotnet restore dsev2.sln

# 빌드 후 테스트
dotnet test dsev2.sln

# 특정 구성으로 빌드
dotnet build dsev2.sln --configuration Debug --verbosity normal

# 문서 생성 포함 빌드 (GenerateDocumentationFile=true)
dotnet build engine/Ev2.Core.FS/Ev2.Core.FS.fsproj --verbosity detailed
```

### 패키징 및 배포
```bash
# NuGet 패키지 생성
dotnet pack engine/Ev2.Core.FS/Ev2.Core.FS.fsproj --configuration Release

# 출력 디렉토리 정리
dotnet clean engine/Ev2.Core.FS/Ev2.Core.FS.fsproj

# 특정 런타임으로 게시
dotnet publish engine/Ev2.Core.FS/Ev2.Core.FS.fsproj --configuration Release --framework netstandard2.0
```

## 테스트 및 검증

### 단위 테스트 프로젝트
- **UnitTest.Core** (`../unit-test/UnitTest.Core/UnitTest.Core.fsproj`)
  - 핵심 도메인 모델 테스트
  - 데이터베이스 CRUD 테스트
  - 타입 변환 테스트

### 테스트 실행
```bash
# 핵심 기능 테스트 실행
cd F:\Git\aas\submodules\dsev2\src
dotnet test unit-test/UnitTest.Core/UnitTest.Core.fsproj

# 모든 테스트 실행 (상세 출력)
dotnet test dsev2.sln --verbosity normal

# 특정 테스트 필터링
dotnet test unit-test/UnitTest.Core/ --filter "TestCategory=Database"
```

### 테스트 데이터
- **샘플 데이터**: `docs/Spec/Data/` (SQLite 데이터베이스, JSON 파일)
- **샘플 생성**: `MiniSample.fs` - 테스트용 객체 생성 함수
- **테스트 픽스처**: `../../../submodules/nuget/Common/Dual.Common.UnitTest.FS/`

### 통합 테스트
- **AAS 통합**: `../unit-test/UnitTest.Aas/` - AAS 직렬화/역직렬화 테스트  
- **확장 시스템**: `../../../src/UnitTest.Hmc/` - Third Party 확장 테스트
- **실제 사용 예시**: `../../../src/HmcConsoleApp/` - 콘솔 애플리케이션

## 확장 시스템 사용법

### 1. 확장 타입 정의 (C# 프로젝트)
```csharp
// 사용자 정의 프로젝트 타입
public class CustomProject : Project 
{
    public string Location { get; set; } = "";
    public int Priority { get; set; } = 0;
}

public class CustomSystem : DsSystem
{
    public string Area { get; set; } = "";
    public double Temperature { get; set; } = 0.0;
}
```

### 2. TypeFactory 구현
```csharp
public class MyTypeFactory : ITypeFactory 
{
    public IRtUnique CreateRuntime(Type runtimeType) 
    {
        return runtimeType.Name switch 
        {
            nameof(CustomProject) => new CustomProject(),
            nameof(CustomSystem) => new CustomSystem(),
            _ => null
        };
    }
    
    public INjUnique CreateNj(Type njType) 
    {
        return njType.Name switch 
        {
            nameof(NjCustomProject) => new NjCustomProject(),
            nameof(NjCustomSystem) => new NjCustomSystem(),
            _ => null
        };
    }
    
    // 추가 메서드 구현...
}
```

### 3. 확장 시스템 등록
```csharp
// 애플리케이션 시작 시
[ModuleInitializer]
public static void Initialize()
{
    // Core 모듈 초기화
    Ev2.Core.FS.ModuleInitializer.Initialize();
    
    // 확장 TypeFactory 등록
    TypeFactory = new MyTypeFactory();
}
```

### 4. 확장 타입 사용
```csharp
// 확장 타입으로 객체 생성
var project = TypeFactory.CreateRuntime(typeof(CustomProject)) as CustomProject;
project.Location = "Seoul";

// JSON 직렬화 (확장 속성 포함)
var json = JsonConvert.SerializeObject(project.ToNj());

// 데이터베이스 저장 (확장 속성 포함)
await project.Insert(connection, transaction);
```

## 로깅 및 설정

### 로깅 시스템
- **프레임워크**: log4net 2.0.17
- **로그 레벨**: DEBUG, INFO, WARN, ERROR, FATAL
- **출력 대상**: 콘솔, 파일, 데이터베이스 (설정 가능)

### 설정 파일
- **appSettings.json**: 실제 설정 파일
- **appSettings.json.template**: 설정 템플릿
- **AppSettings.fs**: 설정 로딩 및 검증 로직

```json
{
  "Database": {
    "ConnectionString": "Data Source=:memory:",
    "Provider": "SQLite"
  },
  "Logging": {
    "Level": "INFO",
    "EnableConsole": true,
    "EnableFile": true
  }
}
```

## 성능 최적화

### 메모리 관리
- **불변 객체**: 메모리 안전성 보장
- **객체 풀링**: 자주 사용되는 객체 재사용
- **지연 로딩**: 필요시에만 객체 생성

### 데이터베이스 최적화
- **연결 풀링**: Dapper 연결 풀 활용
- **배치 처리**: 대량 데이터 처리 시 배치 단위로 실행
- **인덱싱**: 자주 조회되는 컬럼에 인덱스 생성

### 직렬화 최적화
- **스트리밍**: 대용량 JSON 처리 시 스트리밍 방식
- **압축**: 네트워크 전송 시 데이터 압축
- **캐싱**: 자주 사용되는 직렬화 결과 캐시

## 주요 특징

- ✅ **확장 시스템**: Third Party 타입 확장 지원 (ITypeFactory)
- ✅ **다중 직렬화**: JSON, Database, AAS 형식 완벽 지원
- ✅ **C# 호환성**: F#/C# 간 완벽한 상호 운용성
- ✅ **타입 안전성**: F# 타입 시스템을 통한 컴파일 타임 검증
- ✅ **모듈화**: 계층별 명확한 분리와 단일 책임 원칙
- ✅ **성능**: 함수형 프로그래밍을 통한 효율적인 메모리 사용
- ✅ **확장성**: 플러그인 아키텍처를 통한 무한 확장 가능

## 관련 프로젝트

### 핵심 엔진
- **[Ev2.Aas.FS](../Ev2.Aas.FS/Ev2.Aas.FS.md)** - AAS 직렬화/역직렬화
- **[UnitTest.Core](../unit-test/UnitTest.Core/UnitTest.Core.md)** - 핵심 기능 테스트

### 확장 시스템 예시
- **[Hmc.Aas](../../../src/Hmc.Aas/Hmc.Aas.md)** - Third Party 확장 구현 예시
- **[HmcConsoleApp](../../../src/HmcConsoleApp/HmcConsoleApp.md)** - 콘솔 데모 애플리케이션
- **[UnitTest.Hmc](../../../src/UnitTest.Hmc/UnitTest.Hmc.md)** - 확장 시스템 테스트

### 공통 라이브러리
- **Dual.Common.Base.FS** - 기본 유틸리티
- **Dual.Common.Core.FS** - 함수형 프로그래밍 유틸리티
- **Dual.Common.Db.FS** - 데이터베이스 지원

## 버전 정보

- **프레임워크**: .NET Standard 2.0 (cross-platform 지원)
- **F# 버전**: 9.0
- **호환성**: .NET 5.0+ / .NET Framework 4.6.1+
- **개발 환경**: Visual Studio 2022, VS Code, Rider

---

이 프로젝트는 산업 자동화 시스템을 위한 견고하고 확장 가능한 도메인 모델을 제공하며, 함수형 프로그래밍의 장점을 활용하여 타입 안전성과 유지보수성을 보장합니다. Third Party 확장 시스템을 통해 Core 엔진의 수정 없이도 비즈니스 요구사항에 맞는 확장이 가능합니다.