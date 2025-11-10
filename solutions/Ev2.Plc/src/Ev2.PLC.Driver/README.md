# Ev2.PLC.Driver

**Engine V2 PLC Driver** – 공통 PLC 드라이버 인프라와 제조사 통합 레이어

## 구성

```
Ev2.PLC.Driver/
├── Base/         # 공용 드라이버/스캔 기반 클래스
├── Utils/        # 주소 파서, 데이터 변환 등 유틸리티
└── Extensions/   # 공용 확장 모듈 (Async, Tag helpers)
```

## 의존성

- `Ev2.PLC.Common.FS` – 타입과 인터페이스
- `Microsoft.Extensions.Logging.Abstractions`
- `System.IO.Ports`

## 사용 예시

```fsharp
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Driver.Base

let logger = loggerFactory.CreateLogger("Demo")
let options = ConnectionOptions.CreateTcp("192.168.0.10", 2004)

let driver =
    new MyLsDriver(
        plcId = "LS-01",
        options = options,
        logger = logger)

let! connected = driver.ConnectAsync()
```

## 향후 계획

- `Vendors/` 디렉터리 아래에 Allen-Bradley, LS Electric(XGT), Mitsubishi, Siemens 통신 모듈을 순차적으로 통합
- `Ev2.PLC.Server`와 유닛 테스트 프로젝트가 `Ev2.PLC.Driver` 하나만 참조하도록 구조 단순화
