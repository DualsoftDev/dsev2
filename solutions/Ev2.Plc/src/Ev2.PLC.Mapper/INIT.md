# Ev2.PLC.Mapper 초기화 가이드

## 개요

`Ev2.PLC.Mapper`는 다양한 PLC 제조사의 프로그램 파일을 파싱하고 변수를 분석하여 표준화된 I/O 매핑을 생성하는 라이브러리입니다. 이 문서는 라이브러리를 처음 사용하는 개발자를 위한 상세한 초기화 가이드입니다.

## 시스템 요구사항

### 최소 요구사항
- **.NET Standard 2.0** 이상
- **F# 런타임** 지원
- **메모리**: 최소 512MB (대용량 PLC 프로그램 처리 시 더 많이 필요)
- **디스크 공간**: 100MB (설정 파일 및 임시 파일 포함)

### 지원되는 플랫폼
- **Windows**: Windows 10 이상
- **Linux**: Ubuntu 18.04 이상, CentOS 7 이상
- **macOS**: macOS 10.15 이상

## 의존성 패키지

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="System.Text.Json" Version="8.0.5" />
<PackageReference Include="System.Xml.XDocument" Version="4.3.0" />
<PackageReference Include="CsvHelper" Version="33.0.1" />
```

## 프로젝트 구조

```
Ev2.PLC.Mapper/
├── Core/
│   ├── Configuration/          # 외부 설정 시스템
│   │   ├── ConfigurationTypes.fs      # 설정 타입 정의
│   │   └── ConfigurationLoader.fs     # 설정 로더 및 제공자
│   ├── Engine/                 # 핵심 분석 엔진
│   │   └── VariableAnalyzer.fs        # 변수 분석 엔진
│   ├── Interfaces/             # 인터페이스 정의
│   │   ├── IPlcProgramParser.fs       # PLC 파서 인터페이스
│   │   └── IVariableAnalyzer.fs       # 변수 분석기 인터페이스
│   └── Types/                  # 타입 정의
│       ├── ProjectTypes.fs            # 프로젝트 관련 타입
│       ├── VariableTypes.fs           # 변수 관련 타입
│       ├── MappingTypes.fs            # 매핑 관련 타입
│       ├── LogicTypes.fs              # 로직 관련 타입
│       ├── AnalysisTypes.fs           # 분석 관련 타입
│       ├── ResultTypes.fs             # 결과 관련 타입
│       └── ValidationTypes.fs         # 검증 관련 타입
├── Parsers/                    # PLC 제조사별 파서
│   ├── LSElectric/
│   │   └── LSElectricParser.fs        # LS일렉트릭 파서
│   ├── AllenBradley/
│   │   └── AllenBradleyParser.fs      # Allen-Bradley 파서
│   ├── Mitsubishi/                    # 미쓰비시 파서 (미구현)
│   └── Siemens/                       # 지멘스 파서 (미구현)
├── Config/                     # 설정 파일
│   └── mapper-config.json             # 기본 설정 파일
├── MapperFactory.fs            # 메인 팩토리 클래스
├── SampleUsage.fs              # 사용 예제
├── README_Configuration.md     # 설정 가이드
└── Ev2.PLC.Mapper.fsproj      # 프로젝트 파일
```

## 초기 설정

### 1. 프로젝트 참조 추가

**.csproj 파일에 참조 추가:**
```xml
<ProjectReference Include="path/to/Ev2.PLC.Mapper/Ev2.PLC.Mapper.fsproj" />
```

**또는 패키지 참조:**
```xml
<PackageReference Include="Ev2.PLC.Mapper" Version="2.0.0" />
```

### 2. 기본 설정 파일 준비

프로젝트 루트에 `Config` 폴더를 생성하고 `mapper-config.json` 파일을 복사합니다:

```bash
mkdir Config
cp Ev2.PLC.Mapper/Config/mapper-config.json ./Config/
```

### 3. 로깅 설정

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

// 로깅 팩토리 생성
var services = new ServiceCollection();
services.AddLogging(builder => 
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var serviceProvider = services.BuildServiceProvider();
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
```

## 빠른 시작

### 1. 기본 사용법

```fsharp
open Ev2.PLC.Mapper
open System.Threading.Tasks

// 간단한 파일 처리
let processBasic() = task {
    let! result = MapperApi.processFileAsync "path/to/plc-program.xml"
    
    match result.Success with
    | true -> 
        printfn "매핑 성공: %d개 변수 처리됨" result.Statistics.MappedVariables
    | false ->
        printfn "매핑 실패: %s" (String.concat "; " result.Errors)
    
    return result
}
```

### 2. 커스텀 설정 사용

```fsharp
// 외부 설정 파일 사용
let processWithConfig() = task {
    let configPath = "Config/my-custom-config.json"
    let! result = MapperApi.processFileWithConfigFileAsync 
        "path/to/plc-program.xml" 
        configPath
    
    return result
}
```

### 3. 고급 팩토리 사용

```fsharp
open Microsoft.Extensions.Logging
open Ev2.PLC.Mapper.Core.Configuration

// 커스텀 팩토리 생성
let createAdvancedFactory() =
    let loggerFactory = LoggerFactory.Create(fun builder -> 
        builder.AddConsole().SetMinimumLevel(LogLevel.Debug) |> ignore)
    
    let configLogger = loggerFactory.CreateLogger<ConfigurationProvider>()
    let configProvider = ConfigurationFactory.createProvider configLogger
    
    // 설정 로드
    let config = configProvider.LoadConfiguration(Some "Config/mapper-config.json")
    
    // 팩토리 생성
    let factory = MapperFactory(loggerFactory, configProvider)
    factory
```

## 설정 시스템

### 1. 설정 파일 구조

**mapper-config.json의 주요 섹션:**

```json
{
  "mappingConfiguration": {
    "deviceTypeHints": {
      "MOTOR": "Motor",
      "CYL": "Cylinder",
      "SENSOR": "Sensor"
    },
    "apiTypeHints": {
      "commands": {
        "START": "Command",
        "STOP": "Command"
      },
      "status": {
        "RUNNING": "Status",
        "ERROR": "Status"
      }
    },
    "namingConventions": [
      {
        "name": "Standard",
        "pattern": "^(?<area>[A-Z0-9]+)_(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$",
        "description": "AREA_DEVICE_API format",
        "priority": 1
      }
    ]
  },
  "mappingOptions": {
    "analyzeLogicFlow": true,
    "validateNaming": true,
    "parallelProcessing": true
  }
}
```

### 2. 런타임 설정 변경

```fsharp
// 설정 재로드
let factory = createAdvancedFactory()
let analyzer = factory.CreateVariableAnalyzer()

match analyzer with
| :? VariableAnalyzer as va ->
    va.ReloadConfiguration(Some "Config/new-config.json")
    printfn "설정 재로드 완료"
| _ -> ()
```

## 지원되는 PLC 제조사

### 1. LS일렉트릭 (완전 지원)
- **파일 형식**: XML (.xml)
- **프로그램**: XG5000
- **지원 기능**: 
  - 심볼 테이블 파싱
  - 래더 로직 분석
  - 프로젝트 정보 추출

```fsharp
// LS일렉트릭 파일 처리
let processLSElectric() = task {
    let vendor = PlcVendor.CreateLSElectric()
    let! result = factory.ProcessPlcProgramAsync("program.xml")
    return result
}
```

### 2. Allen-Bradley (부분 지원)
- **파일 형식**: L5K (.l5k)
- **프로그램**: RSLogix 5000, Studio 5000
- **지원 기능**: 
  - 기본 태그 파싱
  - 프로젝트 정보 추출

```fsharp
// Allen-Bradley 파일 처리
let processAllenBradley() = task {
    let vendor = PlcVendor.CreateAllenBradley()
    let! result = factory.ProcessPlcProgramAsync("program.l5k")
    return result
}
```

### 3. 미쓰비시 (계획됨)
- **파일 형식**: CSV (.csv)
- **프로그램**: GX Works
- **상태**: 미구현

### 4. 지멘스 (계획됨)
- **파일 형식**: XML (.xml)
- **프로그램**: TIA Portal
- **상태**: 미구현

## 일반적인 워크플로우

### 1. 전체 매핑 프로세스

```fsharp
let completeMapping() = task {
    // 1. 팩토리 생성
    let factory = MapperApi.createFactory(Some "Config/mapper-config.json")
    
    // 2. 파일 검증
    let! validation = factory.ValidateFileAsync("input.xml")
    if not validation.IsValid then
        printfn "파일 검증 실패: %s" validation.Message
        return None
    
    // 3. 파싱 및 분석
    let! result = factory.ProcessPlcProgramAsync("input.xml")
    
    // 4. 결과 처리
    if result.Success then
        printfn "=== 매핑 결과 ==="
        printfn "총 변수: %d" result.Statistics.TotalVariables
        printfn "매핑된 변수: %d" result.Statistics.MappedVariables
        printfn "영역 수: %d" result.Statistics.TotalAreas
        printfn "장치 수: %d" result.Statistics.TotalDevices
        printfn "API 수: %d" result.Statistics.TotalApis
        
        // 5. 결과 저장 (선택사항)
        // saveResults(result)
        
        return Some result
    else
        printfn "매핑 실패:"
        result.Errors |> List.iter (printfn "  - %s")
        return None
}
```

### 2. 배치 처리

```fsharp
let processBatch(filePaths: string list) = task {
    let factory = MapperApi.createFactory()
    let mutable results = []
    
    for filePath in filePaths do
        try
            let! result = factory.ProcessPlcProgramAsync(filePath)
            results <- result :: results
            printfn "처리 완료: %s" filePath
        with
        | ex -> printfn "처리 실패 %s: %s" filePath ex.Message
    
    return results
}
```

## 커스터마이징

### 1. 커스텀 명명 규칙 추가

```json
{
  "mappingConfiguration": {
    "namingConventions": [
      {
        "name": "KoreanStyle",
        "pattern": "^(?<area>구역[0-9]+)_(?<device>[가-힣A-Z0-9_]+)_(?<api>[A-Z]+)$",
        "description": "한국어 구역 명명 규칙",
        "deviceTypeHints": {
          "모터": "Motor",
          "센서": "Sensor"
        },
        "priority": 1
      }
    ]
  }
}
```

### 2. 커스텀 장치 타입 추가

```json
{
  "mappingConfiguration": {
    "deviceTypeHints": {
      "ROBOT": "Custom",
      "AGV": "Conveyor",
      "VISION": "Sensor",
      "BARCODE": "Sensor"
    }
  }
}
```

### 3. 커스텀 파서 구현

```fsharp
open Ev2.PLC.Mapper.Core.Interfaces

type CustomPlcParser(logger: ILogger<CustomPlcParser>) =
    interface IPlcProgramParser with
        member this.SupportedVendor = PlcVendor.Custom("MyPLC", "1.0")
        member this.SupportedFormats = [CustomFormat("myplc", "MyPLC Format")]
        member this.CanParse(format) = 
            match format with
            | CustomFormat("myplc", _) -> true
            | _ -> false
        member this.ParseAsync(filePath) = task {
            // 커스텀 파싱 로직 구현
            return {
                ProjectInfo = // ...
                Variables = // ...
                Logic = // ...
                Comments = // ...
                Metadata = Map.empty
            }
        }
        // 기타 인터페이스 멤버 구현...
```

## 성능 최적화

### 1. 메모리 최적화

```fsharp
// 대용량 파일 처리 시 스트리밍 사용
let processLargeFile() = task {
    let options = {
        MappingOptions.Default with
            ParallelProcessing = true
            MaxConcurrency = Environment.ProcessorCount / 2
    }
    
    let! result = factory.ProcessPlcProgramAsync("large-program.xml", options = options)
    return result
}
```

### 2. 캐싱 활용

```fsharp
// 설정 캐싱
let mutableFactory = factory
let mutableConfig = None

let getConfiguredFactory(configPath: string) =
    match mutableConfig with
    | Some (cachedPath, cachedFactory) when cachedPath = configPath -> 
        cachedFactory
    | _ ->
        let newFactory = MapperApi.createFactory(Some configPath)
        mutableConfig <- Some (configPath, newFactory)
        newFactory
```

## 디버깅 및 문제해결

### 1. 로깅 레벨 조정

```fsharp
// 상세 디버깅
let loggerFactory = LoggerFactory.Create(fun builder ->
    builder
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Trace)
    |> ignore)
```

### 2. 파싱 문제 진단

```fsharp
let diagnoseParsingIssue(filePath: string) = task {
    let factory = createAdvancedFactory()
    
    // 1. 파일 검증
    let! validation = factory.ValidateFileAsync(filePath)
    printfn "파일 검증: %A" validation
    
    // 2. 벤더 추론
    let vendor = factory.InferVendorFromFile(filePath)
    printfn "추론된 벤더: %A" vendor
    
    // 3. 파서 가용성
    match vendor with
    | Some v ->
        let parser = factory.CreateParser(v)
        printfn "파서 사용 가능: %b" parser.IsSome
    | None ->
        printfn "벤더를 식별할 수 없음"
}
```

### 3. 변수 분석 디버깅

```fsharp
let debugVariableAnalysis(variables: RawVariable list) = task {
    let analyzer = factory.CreateVariableAnalyzer()
    let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())
    
    for variable in variables |> List.take 10 do // 처음 10개만 분석
        let! pattern = analyzer.AnalyzeVariableNameAsync(variable, config.NamingConventions)
        printfn "변수: %s -> 패턴: %A" variable.Name pattern
}
```

## 에러 처리

### 1. 일반적인 에러 시나리오

```fsharp
let robustProcessing(filePath: string) = task {
    try
        let! result = MapperApi.processFileAsync filePath
        return Ok result
    with
    | :? FileNotFoundException ->
        return Error "파일을 찾을 수 없습니다"
    | :? UnauthorizedAccessException ->
        return Error "파일 접근 권한이 없습니다"
    | :? InvalidOperationException as ex ->
        return Error $"잘못된 작업: {ex.Message}"
    | ex ->
        return Error $"예상치 못한 오류: {ex.Message}"
}
```

### 2. 부분 실패 처리

```fsharp
let handlePartialFailure(result: MappingResult) =
    if result.Success then
        printfn "완전 성공"
    else if result.Statistics.MappedVariables > 0 then
        printfn "부분 성공: %d/%d 변수 매핑됨" 
            result.Statistics.MappedVariables 
            result.Statistics.TotalVariables
        
        // 경고 및 에러 리포트
        result.Warnings |> List.iter (printfn "경고: %s")
        result.Errors |> List.iter (printfn "에러: %s")
    else
        printfn "완전 실패"
```

## 확장 가능성

### 1. 새로운 PLC 제조사 추가

새로운 PLC 제조사 지원을 추가하려면:

1. `Parsers/` 하위에 새 폴더 생성
2. `IPlcProgramParser` 인터페이스 구현
3. `MapperFactory`에 새 파서 등록
4. 테스트 케이스 작성

### 2. 새로운 분석 엔진 추가

새로운 분석 기능을 추가하려면:

1. `Core/Interfaces/` 에 새 인터페이스 정의
2. `Core/Engine/` 에 구현체 작성
3. `MapperFactory`에 새 엔진 통합

## 배포 고려사항

### 1. 설정 파일 배포

```xml
<!-- 설정 파일을 출력 디렉터리에 복사 -->
<ItemGroup>
  <None Include="Config\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### 2. 종속성 관리

```xml
<!-- NuGet 패키지 의존성 최소화 -->
<PropertyGroup>
  <TrimUnusedDependencies>true</TrimUnusedDependencies>
</PropertyGroup>
```

## 마이그레이션 가이드

### v1.x에서 v2.x로 업그레이드

1. **네임스페이스 변경**:
   ```fsharp
   // 이전
   open Ev2.PLC.Mapper
   
   // 현재
   open Ev2.PLC.Mapper
   open Ev2.PLC.Mapper.Core.Configuration
   ```

2. **팩토리 생성 방법 변경**:
   ```fsharp
   // 이전
   let factory = MapperFactory(loggerFactory)
   
   // 현재
   let factory = MapperApi.createFactory(Some "config.json")
   ```

3. **설정 시스템 활용**:
   ```fsharp
   // 이전: 하드코딩된 설정
   // 현재: JSON 기반 외부 설정
   ```

이 가이드를 통해 `Ev2.PLC.Mapper`를 성공적으로 초기화하고 활용할 수 있습니다. 추가 질문이나 문제가 있으면 개발팀에 문의하세요.