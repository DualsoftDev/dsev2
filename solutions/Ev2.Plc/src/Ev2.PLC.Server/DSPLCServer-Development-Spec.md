# DSPLCServer.FS 개발 스펙 문서

## 1. 프로젝트 개요

### 1.1 프로젝트 목적
- **프로젝트명**: DSPLCServer.FS
- **목적**: 다중 PLC 제조사 지원 데이터 수집 및 저장 서비스
- **언어**: F# (.NET 8.0)
- **아키텍처**: 콘솔 애플리케이션 기반 서비스

### 1.2 핵심 기능
1. **다중 PLC 제조사 지원**
   - LS Electric (XGT 시리즈)
   - Mitsubishi (MELSEC 시리즈)
   - Allen-Bradley (ControlLogix 시리즈)

2. **데이터 수집 및 저장**
   - 실시간 태그 스캐닝
   - 배치 데이터 수집
   - 다중 데이터베이스 지원 (SQLite, PostgreSQL)

3. **설정 관리**
   - JSON 기반 설정 파일
   - 환경변수 지원
   - 런타임 설정 변경

## 2. 시스템 아키텍처

### 2.1 레이어 구조
```
┌─────────────────────────────────────────┐
│            Program.fs (Entry Point)     │
├─────────────────────────────────────────┤
│              Console Layer              │
│  - ConsoleInterface.fs                  │
│  - ConsoleCommands.fs                   │
├─────────────────────────────────────────┤
│               Core Layer                │
│  - ConfigurationManager.fs              │
│  - TagManager.fs                        │
│  - ScanScheduler.fs                     │
│  - DataLogger.fs                        │
├─────────────────────────────────────────┤
│               PLC Layer                 │
│  - PLCManagerBase.fs                    │
│  - LSElectricManager.fs                 │
│  - MitsubishiManager.fs                 │
│  - AllenBradleyManager.fs               │
│  - PLCManagerFactory.fs                 │
├─────────────────────────────────────────┤
│             Database Layer              │
│  - IDataRepository.fs                   │
│  - SQLiteRepository.fs                  │
│  - PostgreSQLRepository.fs              │
│  - DatabaseFactory.fs                  │
├─────────────────────────────────────────┤
│               Common Layer              │
│  - LocalTypes.fs                        │
└─────────────────────────────────────────┘
```

### 2.2 데이터 플로우
```
PLC Device → PLC Manager → Tag Manager → Data Logger → Database Repository → Database
                                     ↘ Real-time Console Display
```

## 3. 기술 스택

### 3.1 프레임워크 및 런타임
- **.NET 8.0**: 최신 .NET 플랫폼
- **F# 8.0**: 함수형 프로그래밍 언어
- **Microsoft.Extensions.Hosting**: 호스트 서비스 프레임워크

### 3.2 주요 라이브러리
| 라이브러리 | 버전 | 용도 |
|-----------|------|------|
| Microsoft.Extensions.Configuration | 8.0.0 | 설정 관리 |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | 의존성 주입 |
| Microsoft.Extensions.Logging | 8.0.1 | 로깅 프레임워크 |
| Serilog | 4.2.0 | 구조화된 로깅 |
| Microsoft.Data.Sqlite | 9.0.1 | SQLite 데이터베이스 |
| Npgsql | 8.0.6 | PostgreSQL 데이터베이스 |
| System.Text.Json | 9.0.5 | JSON 직렬화 |
| Newtonsoft.Json | 13.0.3 | JSON 처리 (호환성) |

### 3.3 개발 환경
- **IDE**: Visual Studio 2022 / VS Code
- **빌드 도구**: .NET CLI
- **버전 관리**: Git
- **패키지 관리**: NuGet

## 4. 모듈별 상세 스펙

### 4.1 Common Layer (공통 계층)

#### 4.1.1 LocalTypes.fs
```fsharp
// 핵심 데이터 타입 정의
type PlcDataSizeType =
    | Boolean = 1 | SByte = 2 | Byte = 3
    | Int16 = 4 | UInt16 = 5 | Int32 = 6 | UInt32 = 7
    | Int64 = 8 | UInt64 = 9 | Float = 10 | Double = 11
    | String = 12

type TagStatus =
    | Active = 1 | Inactive = 2 | Error = 3

type ScanAddress = {
    Address: string
    DataType: PlcDataSizeType
    Count: int
}

type TagInfo = {
    TagName: string
    Address: string
    DataType: PlcDataSizeType
    Description: string
}
```

### 4.2 Database Layer (데이터베이스 계층)

#### 4.2.1 핵심 엔티티
```fsharp
// PLC 설정 정보
type PLCConfiguration = {
    Id: int
    PlcIP: string
    PlcType: string
    PlcName: string
    ScanInterval: int
    IsActive: bool
    ConnectionString: string option
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

// 태그 설정 정보
type TagConfiguration = {
    Id: int
    PlcId: int
    TagName: string
    Address: string
    DataType: PlcDataSizeType
    ScanGroup: string
    IsActive: bool
    Comment: string
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

// 데이터 포인트
type PLCDataPoint = {
    Id: int64
    TagName: string
    PlcIP: string
    PlcType: string
    Value: obj
    DataType: PlcDataSizeType
    Quality: string
    Timestamp: DateTime
    Address: string
}
```

#### 4.2.2 데이터베이스 인터페이스
```fsharp
type IDataRepository =
    // PLC 설정 관리
    abstract CreatePLCAsync: PLCConfiguration -> Task<int>
    abstract GetPLCByIdAsync: int -> Task<PLCConfiguration option>
    abstract GetAllPLCsAsync: unit -> Task<PLCConfiguration[]>
    abstract UpdatePLCAsync: PLCConfiguration -> Task<bool>
    abstract DeletePLCAsync: int -> Task<bool>
    abstract GetActivePLCsAsync: unit -> Task<PLCConfiguration[]>
    
    // 태그 설정 관리
    abstract CreateTagAsync: TagConfiguration -> Task<int>
    abstract GetTagsByPLCIdAsync: int -> Task<TagConfiguration[]>
    abstract GetAllTagsAsync: unit -> Task<TagConfiguration[]>
    abstract UpdateTagAsync: TagConfiguration -> Task<bool>
    abstract DeleteTagAsync: int -> Task<bool>
    
    // 데이터 기록 및 조회
    abstract InsertDataPointAsync: PLCDataPoint -> Task<bool>
    abstract InsertDataPointsAsync: PLCDataPoint[] -> Task<bool>
    abstract GetDataPointsAsync: tagName:string -> fromTime:DateTime -> toTime:DateTime -> Task<PLCDataPoint[]>
    abstract GetLatestDataPointsAsync: tagNames:string[] -> Task<PLCDataPoint[]>
    
    // 데이터베이스 관리
    abstract InitializeDatabaseAsync: unit -> Task<bool>
    abstract TestConnectionAsync: unit -> Task<bool>
    abstract CleanupOldDataAsync: olderThan:DateTime -> Task<int64>
```

### 4.3 PLC Layer (PLC 통신 계층)

#### 4.3.1 PLC Manager 기본 클래스
```fsharp
[<AbstractClass>]
type PLCManagerBase(plcIP: string, manufacturer: string) =
    abstract member ConnectAsync: unit -> Task<bool>
    abstract member DisconnectAsync: unit -> Task<bool>
    abstract member ReadTagAsync: string -> Task<obj option>
    abstract member WriteTagAsync: string -> obj -> Task<bool>
    abstract member ScanTagsAsync: string[] -> Task<Map<string, obj>>
    abstract member GetConnectionStatus: unit -> PLCConnectionStatus
```

#### 4.3.2 제조사별 구현
- **LSElectricManager**: XGT 시리즈 통신 프로토콜
- **MitsubishiManager**: MELSEC 시리즈 통신 프로토콜  
- **AllenBradleyManager**: ControlLogix 시리즈 통신 프로토콜

### 4.4 Core Layer (핵심 서비스 계층)

#### 4.4.1 TagManager (태그 관리자)
```fsharp
type TagManager(repository: IDataRepository) =
    member LoadAllActiveTagsAsync: unit -> Task<int>
    member GetActiveTagsByPLCAsync: int -> Task<TagConfiguration[]>
    member UpdateTagValueAsync: string -> obj -> Task<unit>
    member AddTagChangeHandler: (TagConfiguration -> unit) -> unit
```

#### 4.4.2 ScanScheduler (스캔 스케줄러)
```fsharp
type ScanScheduler(tagManager: TagManager, plcFactory: PLCManagerFactory) =
    member LoadAllScanGroupsAsync: unit -> Task<int>
    member StartAsync: unit -> Task<unit>
    member StopAsync: unit -> Task<unit>
    member AddScanGroup: string -> int -> Task<unit>
```

#### 4.4.3 DataLogger (데이터 로거)
```fsharp
type DataLogger(repository: IDataRepository) =
    member StartAsync: unit -> Task<unit>
    member StopAsync: unit -> Task<unit>
    member LogDataPointAsync: PLCDataPoint -> Task<unit>
    member LogDataPointsAsync: PLCDataPoint[] -> Task<unit>
```

### 4.5 Console Layer (콘솔 인터페이스 계층)

#### 4.5.1 ConsoleInterface
- 실시간 데이터 표시
- 명령어 처리
- 시스템 상태 모니터링

#### 4.5.2 지원 명령어
| 명령어 | 설명 | 사용법 |
|--------|------|--------|
| `status` | 시스템 상태 확인 | `status` |
| `plc list` | PLC 목록 조회 | `plc list` |
| `plc connect <ip>` | PLC 연결 | `plc connect 192.168.1.100` |
| `tag list <plc-id>` | 태그 목록 조회 | `tag list 1` |
| `tag read <tag>` | 태그 값 읽기 | `tag read D100` |
| `scan start <group>` | 스캔 시작 | `scan start Default` |
| `scan stop <group>` | 스캔 중지 | `scan stop Default` |
| `quit` | 프로그램 종료 | `quit` |

## 5. 설정 파일 스펙

### 5.1 appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/dsplcserver-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  },
  "Database": {
    "Type": "SQLite",
    "InitializeDatabase": true,
    "SQLite": {
      "FilePath": "Data/dsplcserver.db"
    },
    "PostgreSQL": {
      "Host": "localhost",
      "Port": 5432,
      "Database": "dsplcserver",
      "Username": "postgres",
      "Password": ""
    }
  },
  "PLCSettings": {
    "DefaultScanInterval": 1000,
    "ConnectionTimeout": 30000,
    "ReadTimeout": 5000,
    "WriteTimeout": 5000,
    "MaxRetryCount": 3
  }
}
```

### 5.2 환경변수 지원
| 변수명 | 설명 | 기본값 |
|--------|------|--------|
| `DS_PLC_DB_TYPE` | 데이터베이스 타입 | SQLite |
| `DS_PLC_DB_SQLITE_PATH` | SQLite 파일 경로 | Data/dsplcserver.db |
| `DS_PLC_DB_HOST` | PostgreSQL 호스트 | localhost |
| `DS_PLC_DB_PORT` | PostgreSQL 포트 | 5432 |
| `DS_PLC_DB_NAME` | 데이터베이스 이름 | dsplcserver |
| `DS_PLC_DB_USER` | 사용자 이름 | postgres |
| `DS_PLC_DB_PASSWORD` | 비밀번호 | (empty) |

## 6. 데이터베이스 스키마

### 6.1 테이블 구조

#### 6.1.1 PLCConfigurations
```sql
CREATE TABLE PLCConfigurations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PlcIP TEXT NOT NULL UNIQUE,
    PlcType TEXT NOT NULL,
    PlcName TEXT NOT NULL,
    ScanInterval INTEGER NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    ConnectionString TEXT,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
```

#### 6.1.2 TagConfigurations
```sql
CREATE TABLE TagConfigurations (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PlcId INTEGER NOT NULL,
    TagName TEXT NOT NULL,
    Address TEXT NOT NULL,
    DataType TEXT NOT NULL,
    ScanGroup TEXT NOT NULL DEFAULT 'Default',
    IsActive BOOLEAN NOT NULL DEFAULT 1,
    Comment TEXT NOT NULL DEFAULT '',
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (PlcId) REFERENCES PLCConfigurations(Id),
    UNIQUE(PlcId, TagName)
);
```

#### 6.1.3 PLCDataPoints
```sql
CREATE TABLE PLCDataPoints (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TagName TEXT NOT NULL,
    PlcIP TEXT NOT NULL,
    PlcType TEXT NOT NULL,
    Value TEXT NOT NULL,
    DataType TEXT NOT NULL,
    Quality TEXT NOT NULL DEFAULT 'Good',
    Timestamp TEXT NOT NULL,
    Address TEXT NOT NULL
);

CREATE INDEX idx_datapoints_tag_time ON PLCDataPoints(TagName, Timestamp);
CREATE INDEX idx_datapoints_plc_time ON PLCDataPoints(PlcIP, Timestamp);
```

## 7. 빌드 및 배포

### 7.1 빌드 명령어
```bash
# 개발 빌드
dotnet build

# 릴리즈 빌드
dotnet build --configuration Release

# 발행 (자체 포함)
dotnet publish --configuration Release --self-contained true --runtime win-x64
dotnet publish --configuration Release --self-contained true --runtime linux-x64
```

### 7.2 실행 방법
```bash
# 개발 환경
dotnet run

# 릴리즈 실행
dotnet DSPLCServer.FS.dll

# 명령줄 옵션
dotnet DSPLCServer.FS.dll --help
dotnet DSPLCServer.FS.dll --config appsettings.Production.json
```

## 8. 성능 요구사항

### 8.1 데이터 처리
- **태그 스캔 주기**: 최소 100ms ~ 최대 60초
- **동시 PLC 연결**: 최대 50개
- **태그 처리량**: PLC당 최대 1000개 태그
- **데이터 저장**: 초당 10,000건 이상

### 8.2 메모리 사용량
- **기본 메모리**: 100MB 이하
- **PLC당 추가 메모리**: 10MB 이하
- **데이터 캐싱**: 최대 1GB

### 8.3 안정성
- **가용성**: 99.9% 이상
- **데이터 손실**: 0.01% 이하
- **복구 시간**: 30초 이내

## 9. 개발 가이드라인

### 9.1 코딩 스타일
- **들여쓰기**: 공백 4개
- **함수명**: camelCase
- **타입명**: PascalCase
- **상수명**: UPPER_SNAKE_CASE
- **파일명**: PascalCase.fs

### 9.2 에러 핸들링
```fsharp
// Result 타입 사용
type PLCResult<'T> = Result<'T, string>

// try-catch 최소화, Option/Result 활용
let readTagSafe (manager: PLCManagerBase) (tagName: string) : Task<PLCResult<obj>> = task {
    try
        let! value = manager.ReadTagAsync(tagName)
        return Ok value
    with
    | ex -> return Error ex.Message
}
```

### 9.3 로깅 가이드
```fsharp
// 로그 레벨별 사용법
logger.LogTrace("세부 디버그 정보")
logger.LogDebug("디버그 정보")
logger.LogInformation("일반 정보")
logger.LogWarning("경고 상황")
logger.LogError(ex, "에러 발생: {TagName}", tagName)
logger.LogCritical("심각한 시스템 오류")
```

### 9.4 테스트 가이드
- **단위 테스트**: 각 모듈별 최소 80% 커버리지
- **통합 테스트**: PLC 통신 및 데이터베이스 연동
- **성능 테스트**: 대용량 데이터 처리

## 10. 보안 고려사항

### 10.1 네트워크 보안
- PLC 통신 암호화 (가능한 경우)
- 방화벽 규칙 설정
- VPN 연결 지원

### 10.2 데이터 보안
- 데이터베이스 연결 문자열 암호화
- 민감한 설정 정보 보호
- 로그 파일 접근 제한

### 10.3 인증 및 권한
- 사용자 인증 (향후 확장)
- 역할 기반 접근 제어
- API 키 관리

## 11. 모니터링 및 관리

### 11.1 상태 모니터링
- PLC 연결 상태
- 태그 스캔 성공률
- 데이터 저장 성공률
- 시스템 리소스 사용량

### 11.2 알림 시스템
- PLC 연결 끊김 알림
- 데이터베이스 연결 오류 알림
- 시스템 오류 알림

### 11.3 백업 및 복구
- 데이터베이스 자동 백업
- 설정 파일 백업
- 시스템 복구 절차

## 12. 향후 확장 계획

### 12.1 단기 계획 (3개월)
- 웹 기반 관리 인터페이스
- REST API 제공
- 알람 및 이벤트 시스템

### 12.2 중기 계획 (6개월)
- MQTT 브로커 연동
- 실시간 대시보드
- 데이터 분석 기능

### 12.3 장기 계획 (1년)
- 클라우드 연동
- 머신러닝 기반 예측 분석
- 분산 처리 지원

---

**문서 버전**: 1.0  
**작성일**: 2024-09-25  
**최종 수정일**: 2024-09-25  
**작성자**: DSPLCServer.FS 개발팀