# Ev2.PLC.Mapper

**Engine V2 PLC Mapper** - PLC 프로그램 파싱 및 변수 매핑 엔진

## 개요

Ev2.PLC.Mapper는 다양한 PLC 제조사의 프로그램 파일을 파싱하여 표준화된 매핑 규격으로 변환하는 엔진입니다. 출력 변수명을 기준으로 Area, Device, API 정의, I/O 매핑 등을 자동으로 생성합니다.

## 핵심 기능

### 1. PLC 프로그램 파싱
- **LS Electric**: XG5000 XML 파일 지원
- **Allen-Bradley**: RSLogix 5000 L5K 파일 지원  
- **Mitsubishi**: GX Works CSV 파일 지원 (예정)
- **Siemens**: TIA Portal XML 파일 지원 (예정)

### 2. 변수명 분석 및 매핑
- 자동 명명 규칙 패턴 인식
- Area, Device, API 자동 추출
- 디바이스 타입 추론 (Motor, Cylinder, Sensor 등)
- API 타입 분류 (Command, Status, Parameter, Feedback)

### 3. 스마트 분석
- 변수 사용 패턴 분석
- 로직 흐름 분석 (예정)
- API 의존성 추출 (예정)
- 최적화 제안 (예정)

## 아키텍처

```
Ev2.PLC.Mapper/
├── Core/
│   ├── Types/              # 핵심 타입 정의
│   ├── Interfaces/         # 인터페이스 정의
│   └── Engine/            # 핵심 분석 엔진
├── Parsers/               # 제조사별 파서
│   ├── LSElectric/
│   ├── AllenBradley/
│   ├── Mitsubishi/        # (예정)
│   └── Siemens/          # (예정)
└── Utils/                # 유틸리티
```

## 사용 방법

### 기본 사용법

```fsharp
open Ev2.PLC.Mapper

// 간단한 파일 처리
let processFile filePath = async {
    let! result = MapperApi.processFileAsync filePath
    match result.Success with
    | true -> 
        printfn "성공: %d개 변수 매핑" result.Statistics.MappedVariables
        return result
    | false ->
        printfn "실패: %A" result.Errors
        return result
}
```

### 고급 사용법

```fsharp
open Ev2.PLC.Mapper.Core.Types
open Microsoft.Extensions.Logging

// 로거 팩토리 생성
let loggerFactory = LoggerFactory.Create(fun builder ->
    builder.AddConsole().SetMinimumLevel(LogLevel.Information) |> ignore)

// MapperFactory 생성
let factory = MapperFactory(loggerFactory)

// 매핑 설정
let config = {
    MappingConfiguration.Default(LSElectric) with
        NamingConventions = [
            {
                Name = "Custom"
                Pattern = @"^(?<area>[A-Z0-9]+)_(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$"
                Description = "AREA_DEVICE_API format"
                DeviceTypeHints = Map.ofList [("MOTOR", Motor); ("CYL", Cylinder)]
                ApiTypeHints = Map.ofList [("FWD", Command); ("RUNNING", Status)]
                Priority = 1
            }
        ]
}

// 파일 처리
let processWithConfig filePath = async {
    let! result = factory.ProcessPlcProgramAsync(filePath, config)
    return result
}
```

### 결과 구조

```fsharp
type MappingResult = {
    Success: bool
    ProjectInfo: ProjectInfo          // 프로젝트 정보
    Areas: Area list                  // 추출된 영역들
    Devices: Device list              // 추출된 디바이스들  
    ApiDefinitions: ApiDefinition list // API 정의들
    IOMapping: IOMapping              // I/O 매핑
    Statistics: MappingStatistics     // 처리 통계
    Warnings: string list             // 경고 메시지
    Errors: string list               // 오류 메시지
}
```

## 명명 규칙 패턴

### 표준 패턴
- **AREA_DEVICE_API**: `AREA1_MOTOR01_FWD`
- **DEVICE_API**: `MOTOR01_FWD`

### 지원하는 디바이스 타입
- **Motor**: `MOTOR`, `MTR`, `M`
- **Cylinder**: `CYLINDER`, `CYL`, `CY`  
- **Sensor**: `SENSOR`, `SEN`, `S`
- **Valve**: `VALVE`, `VLV`, `V`
- **Conveyor**: `CONVEYOR`, `CONV`, `CV`
- **기타**: `BUTTON`, `LAMP`, `COUNTER`, `TIMER`

### 지원하는 API 타입
- **Command**: `FWD`, `BACK`, `START`, `STOP`, `UP`, `DOWN`
- **Status**: `RUNNING`, `ERROR`, `DETECT`, `POSITION`
- **Parameter**: `SPEED`, `TIMEOUT`, `SETPOINT`
- **Feedback**: `VALUE`, `CURRENT`, `FEEDBACK`

## 예시

### LS Electric XML 입력
```xml
<Symbol Name="AREA1_MOTOR01_FWD" Address="Q0.1" DataType="BOOL" Comment="모터1 전진"/>
<Symbol Name="AREA1_MOTOR01_BACK" Address="Q0.2" DataType="BOOL" Comment="모터1 후진"/>
<Symbol Name="AREA1_MOTOR01_RUNNING" Address="I0.1" DataType="BOOL" Comment="모터1 운전중"/>
```

### 매핑 결과
```fsharp
{
    Areas = [{ Name = "AREA1"; Devices = ["MOTOR01"] }]
    Devices = [{
        Name = "MOTOR01"
        Type = Motor
        Area = "AREA1"
        SupportedApis = [
            { Name = "FWD"; Type = Command; Direction = Output }
            { Name = "BACK"; Type = Command; Direction = Output }
            { Name = "RUNNING"; Type = Status; Direction = Input }
        ]
    }]
    IOMapping = {
        Outputs = [
            { LogicalName = "MOTOR01_FWD"; PhysicalAddress = "Q0.1" }
            { LogicalName = "MOTOR01_BACK"; PhysicalAddress = "Q0.2" }
        ]
        Inputs = [
            { LogicalName = "MOTOR01_RUNNING"; PhysicalAddress = "I0.1" }
        ]
    }
}
```

## 지원 파일 형식

| 제조사 | 형식 | 확장자 | 상태 |
|--------|------|--------|------|
| LS Electric | XG5000 XML | .xml | ✅ 완료 |
| Allen-Bradley | RSLogix L5K | .L5K | ✅ 완료 |  
| Mitsubishi | GX Works CSV | .csv | 🚧 예정 |
| Siemens | TIA Portal XML | .xml | 🚧 예정 |

## 성능

- **파싱 속도**: 1000개 변수/초
- **메모리 사용량**: 최적화된 스트리밍 처리
- **파일 크기**: 최대 100MB XML/L5K 파일 지원

## 검증 기능

### 자동 검증
- 파일 형식 유효성
- 명명 규칙 준수
- 주소 충돌 검사
- 데이터 타입 호환성

### 경고 및 제안
- 명명 규칙 불일치
- 최적화 가능한 주소 배치
- 미사용 변수 감지

## 확장성

### 사용자 정의 패턴
```fsharp
let customPattern = {
    Name = "MyCompany"
    Pattern = @"^(?<line>L\d+)_(?<station>ST\d+)_(?<device>\w+)_(?<action>\w+)$"
    DeviceTypeHints = Map.ofList [("ROBOT", Custom "Robot")]
    ApiTypeHints = Map.ofList [("PICK", Command); ("PLACE", Command)]
    Priority = 1
}
```

### 플러그인 아키텍처
- 새로운 제조사 파서 추가 가능
- 사용자 정의 분석 로직 등록
- 커스텀 검증 규칙 추가

## 의존성

- **.NET Standard 2.0**
- **Ev2.PLC.Common.FS** (공통 타입)
- **Microsoft.Extensions.Logging**
- **System.Text.Json**
- **CsvHelper** (CSV 파싱용)

## 개발 상태

- ✅ **핵심 아키텍처**: 완료
- ✅ **LS Electric 파서**: 완료
- ✅ **Allen-Bradley 파서**: 완료
- ✅ **변수 분석 엔진**: 완료
- 🚧 **Mitsubishi 파서**: 진행중
- 🚧 **Siemens 파서**: 진행중
- 🚧 **로직 흐름 분석**: 계획중
- 🚧 **API 의존성 분석**: 계획중

## 라이선스

Dualsoft - DS PLC Engine V2