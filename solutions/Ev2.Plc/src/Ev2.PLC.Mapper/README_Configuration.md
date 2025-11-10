# Ev2.PLC.Mapper 외부 설정 가이드

`Ev2.PLC.Mapper`가 이제 JSON 형태의 외부 설정 파일을 지원합니다. 이를 통해 `deviceTypeHints`, `apiTypeHints`, 명명 규칙 등 모든 설정을 코드 수정 없이 외부에서 관리할 수 있습니다.

## 주요 변경사항

### 1. 외부 설정 지원
- **하드코딩된 설정 제거**: `VariableAnalyzer.fs`의 하드코딩된 `deviceTypeHints`, `apiTypeHints` 제거
- **JSON 설정 파일**: 모든 설정을 JSON 파일로 외부화
- **런타임 설정 변경**: 실행 중에도 설정을 동적으로 변경 가능

### 2. 새로운 구성 요소
- `Core/Configuration/ConfigurationTypes.fs`: 설정 타입 정의
- `Core/Configuration/ConfigurationLoader.fs`: JSON 설정 로더 및 제공자
- `Config/mapper-config.json`: 기본 설정 파일 예제

## 설정 파일 구조

### 기본 설정 파일 위치
```
/Config/mapper-config.json
```

### 주요 설정 섹션

#### 1. 장치 타입 힌트 (deviceTypeHints)
```json
{
  "mappingConfiguration": {
    "deviceTypeHints": {
      "MOTOR": "Motor",
      "CYL": "Cylinder",
      "SENSOR": "Sensor",
      "VALVE": "Valve"
    }
  }
}
```

#### 2. API 타입 힌트 (apiTypeHints)
```json
{
  "apiTypeHints": {
    "commands": {
      "START": "Command",
      "STOP": "Command",
      "FWD": "Command"
    },
    "status": {
      "RUNNING": "Status",
      "ERROR": "Status"
    },
    "parameters": {
      "SPEED": "Parameter",
      "POSITION": "Parameter"
    },
    "feedback": {
      "VALUE": "Feedback",
      "ACTUAL": "Feedback"
    }
  }
}
```

#### 3. 명명 규칙 (namingConventions)
```json
{
  "namingConventions": [
    {
      "name": "Standard",
      "pattern": "^(?<area>[A-Z0-9]+)_(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$",
      "description": "AREA_DEVICE_API format",
      "priority": 1
    }
  ]
}
```

#### 4. 추론 규칙 (deviceInferencePatterns, apiInferenceRules)
```json
{
  "deviceInferencePatterns": {
    "fallbackPatterns": [
      {
        "pattern": "MOTOR|DRIVE",
        "deviceType": "Motor"
      }
    ]
  },
  "apiInferenceRules": {
    "deviceSpecificRules": [
      {
        "deviceType": "Motor",
        "patterns": [
          {
            "pattern": "SPEED|RPM",
            "apiType": "Parameter"
          }
        ]
      }
    ]
  }
}
```

## 사용 방법

### 1. 기본 사용법
```fsharp
// 기본 설정으로 팩토리 생성
let factory = MapperApi.createFactory()

// 또는 특정 설정 파일 지정
let factory = MapperApi.createFactory("/path/to/custom-config.json")
```

### 2. 설정 파일과 함께 처리
```fsharp
// 설정 파일 경로를 지정하여 PLC 프로그램 처리
let! result = MapperApi.processFileWithConfigFileAsync 
    "/path/to/plc-program.xml" 
    "/path/to/config.json"
```

### 3. 커스텀 설정 제공자 사용
```fsharp
let loggerFactory = LoggerFactory.Create(fun builder -> 
    builder.AddConsole() |> ignore)

let configLogger = loggerFactory.CreateLogger<ConfigurationProvider>()
let configProvider = ConfigurationFactory.createProvider configLogger

// 설정 로드
let config = configProvider.LoadConfiguration(Some "/path/to/config.json")

// 팩토리 생성
let factory = MapperFactory(loggerFactory, configProvider)
```

### 4. 런타임 설정 변경
```fsharp
let analyzer = factory.CreateVariableAnalyzer()

// 설정 재로드
match analyzer with
| :? VariableAnalyzer as va ->
    va.ReloadConfiguration(Some "/path/to/new-config.json")
| _ -> ()
```

### 5. 설정 저장
```fsharp
let configProvider = ConfigurationFactory.createProvider configLogger
let config = configProvider.LoadConfiguration(None)

// 설정 수정
config.MappingConfiguration.DeviceTypeHints.["ROBOT"] <- "Custom"

// 저장
match configProvider.SaveConfiguration(config, "/path/to/new-config.json") with
| Ok () -> printfn "설정 저장 성공"
| Error msg -> printfn "설정 저장 실패: %s" msg
```

## 설정 파일 예제

완전한 설정 파일 예제는 `Config/mapper-config.json`을 참조하세요.

## 장점

1. **유연성**: 코드 수정 없이 매핑 규칙 변경 가능
2. **재사용성**: 프로젝트별로 다른 설정 파일 사용 가능
3. **유지보수성**: 설정과 코드의 분리로 유지보수 용이
4. **확장성**: 새로운 설정 항목 쉽게 추가 가능
5. **버전 관리**: 설정 파일의 독립적인 버전 관리 가능

## 마이그레이션 가이드

기존 하드코딩된 설정을 사용하던 코드는 다음과 같이 변경:

### 변경 전
```fsharp
let factory = MapperFactory(loggerFactory)
let analyzer = factory.CreateVariableAnalyzer()
```

### 변경 후
```fsharp
// 방법 1: 기본 설정 사용
let factory = MapperApi.createFactory()

// 방법 2: 특정 설정 파일 사용
let factory = MapperApi.createFactory("/path/to/config.json")

// 방법 3: 커스텀 설정 제공자 사용
let configProvider = ConfigurationFactory.createProvider configLogger
let factory = MapperFactory(loggerFactory, configProvider)
```

이제 `Ev2.PLC.Mapper`는 완전히 설정 기반으로 작동하며, 다양한 PLC 환경과 명명 규칙에 쉽게 적응할 수 있습니다.