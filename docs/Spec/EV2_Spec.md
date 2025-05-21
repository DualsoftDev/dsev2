# EV2 (Engine Version 2) 개발 가이드

## Part 1: 시스템 개요 

### 1.1 프로젝트 개요

- **프로젝트 명칭**: EV2 (Engine Version 2)
- **개발 주체**: DualSoft
- **개발 목적**:
  - 기존 DS 시스템 구조의 한계를 극복하고 확장 가능성과 재사용성을 강화한 범용 실행 엔진 개발
  - 다양한 UI 플랫폼(WinForms, Blazor, PowerPoint 등)과 디바이스(PLC, HMI, 시뮬레이터 등)를 대상으로 한 통합 구조 구현
  - 실제 설비 및 디지털 트윈 환경과 연계 가능한 공통 메타모델 정의 및 실행

### 1.2 개발 배경 및 필요성

#### 기존 DS 엔진의 한계

- 단일 목적, 단일 UI 구조에 최적화된 설계로 인해 다양한 응용 확장 어려움
- 하드코딩된 동작/제어 흐름으로 로직 재사용성과 추론 불가
- UI, 실행 로직, 저장 구조가 결합되어 모듈화·분산 불가능

#### EV2의 설계 방향

- **모델 기반 구조화**: Work / Call 를 기반으로 한 추론 가능한 구조 설계
- **View-Model-Storage 분리**: 사용자 인터페이스와 로직을 분리하여 다양한 클라이언트 플랫폼 대응
- **저장 구조의 표준화**: JSON, AASX, SQLite 등으로 저장 포맷 통일
- **디지털 트윈 대응**: AAS 기반 모델 구성과 OPC-UA 연동 구조 설계

### 1.3 핵심 설계 철학

1. **구조 중심 설계 (Structure-Oriented Design)**
   - 실행 단위를 정점(Vertex), 흐름을 간선(Edge)으로 표현하는 그래프 기반 구조
   - `System → Work → Call → ApiCall(System.ApiDef)`흐름 구조

2. **기본 저장구조를 DSL (Dualsoft Language)에서 Json 형식으로 전환**
   - 확장성을 고려해 json 규격으로 전환 json <-> DS or AASX
   - 모델링된 UI 기반 정보 → Json형태로 변환 → 실행 엔진에 의해 로직화

3. **사이클 지원 그래프 구조**
    - Work 내부 Call 연결은 반드시 비순환(Directed Acyclic Graph) 구조
    - Work 간 연결은 순환 그래프(Cyclic Directed Graph) 허용을 하며 런타임 변환 시 안전성 확보

4. **디지털 트윈 정합성 확보**
   - AAS 기반 구조를 통해 각 System, Work, Call, Api가 하나의 Submodel로 변환 가능
   - 물리 자산(ChildSystem-ApiDef)과 논리(ParentSystem-ApiCall) 흐름 사이의 1:1 연결 매핑 보장

### 1.4 사용자 시나리오

- PowerPoint, 전용 WinForms 또는 Web 기반 모델러에서 구성
- 각 도형은 Work, Call에 대응하는 논리 요소
- 구성된 흐름은 `.json` 또는 `.aasx` 파일로 저장되어 시뮬레이터 또는 PLC로 전달
- 동작 이력, 로그 추적, API 통계 수집 가능

### 1.5 주요 기술 스택 및 구성 요소

| 영역       | 기술 요소                                           |
|------------|--------------------------------------------------|
| UI         | WinForms, Blazor, PowerPoint VSTO               |
| 그래프 엔진 | Directed Graph, Vertex-Edge 구조, 순환 처리 지원     |
| 저장 구조   | JSON (.json), AASX (.aasx), SQLite (.db)        |
| 직렬화     | Json, AasxLib                        |
| 분석 도구   | Job 실행 통계, API 실행 카운터, 트랜잭션 추적 도구     |
| 디지털트윈  | AAS 구조 기반 Submodel 매핑, OPC-UA 연동           |
  - Json 은 필요에 따라서 System.Text.Json 나 NewtonSoft.Json 중 선택
  - DS 의 일반 json 저장 : NewtonSoft.Json
  - DS 의 AAS xml 대응 json 저장 : System.Text.Json

### 1.6 기대 효과

- **모델 중심 설계로 유지보수 비용 절감**: 정의-실행-시각화 구조의 통합
- **클라우드 및 시뮬레이터 통합 용이**: 표준 저장 포맷과 분리 구조
- **다양한 디바이스 연계 가능**: OPC-UA, PLC, 시뮬레이터 등과 직접 연동
- **사용자 정의 모델링 시나리오 확장 가능**: 라이브러리 형태로 직관적 확장 및 검증 가능


## Part 2: 핵심 모델 설계

### 2.1 구성 요소 계층 구조

EV2 실행 모델은 다음과 같은 계층 구조를 가집니다:
- **Project**:   다수의 System을 포함하는 최상위 단위. TargetSystems 필드를 통해 제어코드 생성 대상 명시 가능
- **System**:  Work 간 전역 흐름 그래프(`WorkGraph`, Start, Reset 가능) 포함
- **Work**: 작업 단위. 내부적으로 Call을 포함하며, `CallGraph`로 Call 간 흐름(Reset 금지, DAG만 가능) 구성. `Vertex`를 상속함
- **Flow**: 논리 단위로서 여러 Work를 포함하는 그룹
- **Call**: 특정 API(동시 호출 가능)를 호출하는 노드. `Vertex`를 상속함
- **ApiCall**: 실제 API 호출을 수행. 디바이스 연계 IO 정의 (입출력 주소)
- **ApiDef**: Child System의 Interface 정의 부분

```plaintext
Project
└── System[]                   // 하나의 프로젝트에 여러 시스템 포함
     ├── Work[]                // 각 시스템 내 작업 단위
     │    ├── Flow             // 논리적 그룹 (공정 조작단위)
     │    ├── Call[]           // Work 내 호출 노드
     │    │    └── ApiCall[]   // 실제 API 호출 (디바이스 연동)
     │    │         └── ApiDef // 다른 System의 디바이스 정의 참조
     │    └── CallGraph        // Call 간 흐름 (Directed Acyclic Graph)
     └── WorkGraph             // Work 간 흐름 (Cyclic Directed Graph, Start/Reset 포함)
```
  - x:y 는 `부모` 대 `나` 와의 관계

### 2.2 공통 베이스 클래스

#### SystemUsage
```fsharp
type SystemUsage =
  | Target   // 프로젝트에 정의되어 있고, 직접 제어 대상
  | Linked   // 외부 프로젝트에서 정의된 간접 제어 대상
  | Device   // 이 프로젝트에서 정의되었으나 간접 제어 대상
  | Unused   // 이 프로젝트에서 사용되지 않음 (정의 및 참조 없음)
```

```fsharp
type IUnique =
  abstract Id: Guid
  abstract Name: string

type IParameter = interface end

type IArrow = IUnique * IUnique
```
모든 요소는 고유 ID와 이름(Name)을 가짐

### 2.3 주요 클래스 및 속성

#### Project
```fsharp
type ProjectParam = {
    Name: string
    Version: string
    Description: string option
    Author: string option
    CreatedAt: System.DateTime
    TargetSystems: string list // 제어 대상 시스템 이름
    LinkSystems: string list    // 참조 링크 시스템
} with interface IParameter

type Project(idOpt: Guid option, name: string, systems: DsSystem list, param: ProjectParam) =
    let id = defaultArg idOpt (Guid.NewGuid())
    interface IUnique with
        member _.Name = name
        member _.Id = id
    member _.Systems = systems
    member _.Param = param
    member _.GetTargetSystems() =
        systems |> List.filter (fun s -> param.TargetSystems |> List.contains s.Name)
```

#### System
```fsharp
type DsSystem(idOpt: Guid option, name: string, flows: Flow[], jobs: Job[], edges: IArrow[], devices: device[], param: IParameter ) =
    let id = defaultArg idOpt (Guid.NewGuid())
    interface IUnique with
        member _.Name = name
        member _.Id = id
    member val Guid = id
    member _.Flows = flows
    member _.WorkGraph = edges
    member _.Jobs = jobs
    member _.Devices = devices
    member _.Param = param
```
- Flows: 여러 Flow 그룹
- WorkGraph: 전역 작업 흐름 정의 (Work 간 연결)

#### Flow
```fsharp
type Flow(idOpt: Guid option, name: string, param: IParameter) =
    let id = defaultArg idOpt (Guid.NewGuid())
    interface IUnique with
        member _.Id = id
        member _.Name = name
    member _.Param = param
```

#### Work
```fsharp
type Work(idOpt: Guid option, name: string, flow: Flow, calls: Call[], edges: IArrow[], param: IParameter) =
    let id = defaultArg idOpt (Guid.NewGuid())
    interface IUnique with
        member _.Id = id
        member _.Name = name
    member _.Flow = flow
    member _.Calls = calls
    member _.CallGraph = edges
    member _.Param = param
```
- 순환 구조 허용 (Cyclic Directed Graph)
- 내부 Call 흐름 정의 가능

#### Call
```fsharp
type Call(idOpt: Guid option, name: string, apiCalls: ApiCall[], param: IParameter) =
    let id = defaultArg idOpt (Guid.NewGuid())
    interface IUnique with ...
    member _.Param = param
    member _.ApiCalls = apiCalls
```
- `CallGraph`에 따라 연결됨

#### ApiCall
```fsharp
type ApiCall(deviceName: string, apiDef: ApiDef, param: IParameter) =
    member this.DeviceName = deviceName
    member _.Param = param
```

#### ApiDef
```fsharp
type ApiDef(idOpt: Guid option, name: string, param: IParameter) =
    let id = defaultArg idOpt (Guid.NewGuid())
    interface IUnique with ...
    member _.Param = param
```
### 2.4 파라미터 모델

모든 주요 객체는 공통적으로 `Param` 속성을 갖고 있음. 각 객체에 대한 파라미터 정의는 다음 별도 문서로 분리됨:

- [ProjectParam](./params/ProjectParam.md)
- [SystemParam](./params/SystemParam.md)
- [WorkParam](./params/WorkParam.md)
- [CallParam](./params/CallParam.md)
- [ApiCallParam](./params/ApiCallParam.md)
- [ApiDefParam](./params/ApiDefParam.md)
- [FlowParam](./params/FlowParam.md)

이를 통해 UI 또는 Json 구조에서도 명확하게 각 객체의 의미와 구성 가능


### ~~2.5 예시 코드 Obsolete version~~
```fsharp
let sys = DsSystem("Example")
let flow = Flow("Main")
sys.Flows.Add(flow)

let w1 = Work("W1")
let w2 = Work("W2")
flow.Works.Add(w1)
flow.Works.Add(w2)
flow.WorkGraph.Add((w1.Id, w2.Id))

let job = Job("JobA", "Device1.API")
sys.Jobs.Add(job)

let c1 = Call("Device1.API")
let c2 = Call("Device1.API")
w1.Calls.Add(c1)
w1.Calls.Add(c2)
w1.CallGraph.Add((c1.Id, c2.Id))
```

### 2.5 예시 코드
```fsharp
let c1 = Call("Device1.API")
let c2 = Call("Device1.API")
let w1 = Work("W1", [c1; c2], [(c1.Id, c2.Id)])
let w2 = Work("W2")
let flow = Flow("Main", [w1; w2], [(w1.Id, w2.Id)])
let job = Job("JobA", "Device1.API")

let sys = DsSystem("Example", [flow], [job])
```
- Bottom up build 를 통해 DsSystem 등의 ResizeArray member 제거하고 불변 list 화 수행



### 2.6 정리

- **그래프 기반 구성**으로 복잡한 실행 흐름을 시각적, 논리적으로 명확히 표현
- **순환 허용**은 Work 단위에서 가능하며, Flow는 비순환으로 구성하여 전체 실행 경로 안정성 확보
- **모든 객체는 IUnique 기반**으로 ID-Name 기준 구조화되어 직렬화/저장/추적 가능

> 다음 파트에서는 Part 3: 저장 구조 및 DB 스키마로 이어집니다.



## Part 3: 자료구조 및 데이터베이스 설계

### 3.1 개요

EV2 시스템은 다양한 실행 단위(`System`, `Flow`, `Work`, `Call`, `ApiCall`, `ApiDef`)를 효율적으로 저장 및 조회할 수 있도록 관계형 데이터베이스 기반으로 모델링됩니다. 각 시스템은 **타입(Type)** 과 **인스턴스(Instance)** 로 구분되며, 향후 **AASX (Asset Administration Shell XML)** 파일로 확장 가능하도록 설계됩니다.

### 3.2 시스템 모델: 타입과 인스턴스

- **System 타입**: 메타 정의 역할, 실행 상태 없음. 정의된 구조만 포함합니다.
- **System 인스턴스**: 실행 단위이며 다음 중 하나입니다:
  - **Device**: 자식 시스템 포함 (내장 생성)
  - **ExternalSystem**: 외부 시스템 참조 (외부 불러오기)

> 실행 인스턴스는 최소 구성만 유지하며, 연관된 `ApiDef`, `ApiCall`를 통해 외부 연동됩니다.

### 3.3 주요 테이블 구조

| 테이블 명     | 설명 |
|---------------|------|
| `system`     | 시스템 정의 및 인스턴스 구분, 버전 및 IRI 포함 |
| `flow`       | 시스템 내 작업 흐름 정의 (Work 포함) |
| `work`       | 개별 실행 단위, 내부에 Call 및 CallGraph 포함 |
| `call`       | `Job` 호출 노드, 조건 및 시간 정보 포함 |

#### 3.3.1 Database notation 규칙
- table, filed 명 소문자로 시작하는 camelCase  
- SQL 문법에 해당하는 부분은 대문자
- table 명 끝에는 's' 를 제거.  (의미적으로 모두 s 가 붙으므로 무의미)
- 모든 table 에는 `id` 이름의 int type primary key 

### 3.4 데이터 무결성 및 인덱싱 전략

- PK, FK 제약조건으로 무결성 보장
- 이름+버전 조합으로 `Systems`, `Jobs`, `ApiItems`는 Unique 인덱스 필요
- `Calls`, `TaskDevs`는 복합 인덱스로 빠른 탐색 지원
- GUID 는 성능 문제로 Primary key 로 사용하지 않음

```
▶ SQLite Primary Key 성능 비교 테스트 시작
[INT PK]               Inserted 1,000,000 rows in 0.62 seconds
[GUID as PK]           Inserted 1,000,000 rows in 8.34 seconds
[INT PK + GUID COL]    Inserted 1,000,000 rows in 1.81 seconds
[INT PK + GUID + IDX]  Inserted 1,000,000 rows in 8.26 seconds
[DB Size] test_pk_perf.db: 279.20 MB
▶ 테스트 완료
```

### 3.5 테이블 생성 예시 (SQL)

```sql
CREATE TABLE system (
    id int PRIMARY KEY,
    name TEXT NOT NULL,
    createdAt TIMESTAMP DEFAULT NOW(),
    langVersion TEXT,
    engineVersion TEXT,
    isDevice BOOLEAN DEFAULT FALSE,
    isExternal BOOLEAN DEFAULT FALSE,
    isInstance BOOLEAN DEFAULT FALSE,
    iri TEXT
);

CREATE TABLE flow (
    id int PRIMARY KEY,
    systemId int  REFERENCES system(id),
    Name TEXT NOT NULL
);

CREATE TABLE work (
    id int PRIMARY KEY,
    flowId int REFERENCES flow(id),
    Name TEXT NOT NULL
);

CREATE TABLE call (
    id int PRIMARY KEY,
    workId int REFERENCES work(id),
    jobId int REFERENCES job(id),
    callTimeout INT,
    isDisabled BOOLEAN,
    autoPre BOOLEAN DEFAULT FALSE
);

CREATE TABLE job (
    id int PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT
);

CREATE TABLE taskDev (
    id int PRIMARY KEY,
    jobId int REFERENCES jobs(id),
    deviceSystemId int REFERENCES system(id),
    deviceName TEXT,
    apiItemId int REFERENCES apiItem(id)
);

CREATE TABLE apiItem (
    id int PRIMARY KEY,
    Name TEXT NOT NULL
);

CREATE TABLE apiStatistic (
    id int PRIMARY KEY,
    apiItemId int REFERENCES apiItem(id),
    deviceSystemId int REFERENCES system(id),
    avgTime INT,
    stdDevTime INT,
    executionCount INT,
    updatedAt TIMESTAMP DEFAULT NOW()
);

--- ???? 위에서 정의한 parameter type 이 다양한데 어떻게 담을지?  3.6 방식???
CREATE TABLE param (
    id int PRIMARY KEY,
    ownerId int,
    paramKey TEXT,
    paramValue TEXT
);


-- 모든 객체의 UUID 정보 저장 table
CREATE TABLE guidMap (
    id int PRIMARY KEY,
    tableName:Text, -- e.g 'someTable'
    tableId:int, -- 의미적으로 REFERENCES someTable(id)
    guid:UUID
);

```

### 3.6 파라미터 직렬화 및 저장 방식

F# 코드에서는 각 객체의 파라미터 정보를 다음과 같이 키-값으로 변환하여 `Params` 테이블에 저장합니다:

```fsharp
let private serializeCallParam (p: CallParam) =
    [
        nameof(p.CallType), p.CallType
        nameof(p.Timeout), string p.Timeout
        nameof(p.ActionType), p.ActionType
    ]
    @ (p.AutoPreConditions |> Seq.map (fun v -> nameof(p.AutoPreConditions), v) |> Seq.toList)
    @ (p.SafetyConditions  |> Seq.map (fun v -> nameof(p.SafetyConditions), v) |> Seq.toList)
```

### 3.7 실전 SQL 연산 예시 : 위 수정 사항 fix 후 update 필요!!

#### 1. 특정 Job의 Device API 추적
```sql
SELECT td.DeviceName, ai.Name AS ApiName
FROM TaskDevs td
JOIN ApiItems ai ON td.ApiItemId = ai.ApiItemId
JOIN Jobs j ON td.JobId = j.JobId
WHERE j.Name = 'SampleJob';
```

#### 2. AutoPre 조건이 활성화된 Call 목록
```sql
SELECT c.CallId, w.Name AS WorkName, j.Name AS JobName
FROM Calls c
JOIN Works w ON c.WorkId = w.WorkId
JOIN Jobs j ON c.JobId = j.JobId
WHERE c.AutoPre = TRUE;
```

#### 3. 시스템 내 전체 트랜잭션 흐름 조회
```sql
SELECT s.Name AS SystemName, f.Name AS FlowName, w.Name AS WorkName, j.Name AS JobName
FROM Systems s
JOIN Flows f ON s.SystemId = f.SystemId
JOIN Works w ON f.FlowId = w.FlowId
JOIN Calls c ON w.WorkId = c.WorkId
JOIN Jobs j ON c.JobId = j.JobId
WHERE s.Name = 'MainSystem';
```

#### 4. API 실행 통계 조회
```sql
SELECT ai.Name AS ApiName, ast.AvgTime, ast.StdDevTime, ast.ExecutionCount
FROM ApiStatistics ast
JOIN ApiItems ai ON ast.ApiItemId = ai.ApiItemId
JOIN Systems s ON ast.DeviceSystemId = s.SystemId
WHERE s.Name = 'Device_1';
```

---

> 이 구조는 EV2 런타임에서 DB 저장과 빠른 질의(Query)를 동시에 만족시키며, 추후 JSON 및 AASX 포맷으로의 변환도 용이하게 합니다.





---
## Part 4: EV2 런타임 구성 저장 구조

### 4.1 EV1 -> EV2: 구조적 변화 개요

EV1은 구조적인 `.ds` 도메인 언어 기반 정의를 사용했지만, EV2에서는 모든 시스템 정의가 **표준 JSON 포맷**으로 저장되며, **런타임 시점에 관계형 DB**로 실시간 변환되어 동작됩니다. 각 시스템, 작업 흐름, 장치 구성, API, 조건, 버튼 및 램프가 JSON 기반으로 명확히 정의되며, 이후 AASX 메타 정의에도 확장 가능하도록 설계됩니다.

---

### 4.2 JSON 예제: 시스템 HelloDS

```json
{
  "System": {
    "Id": "ec5d7a91-1bc2-47cd-a8a6-fc5f9b9de111",
    "Name": "HelloDS",
    "LangVersion": "1.0.0.1",
    "EngineVersion": "0.9.10.17",
    "Flows": [
      {
        "Id": "b20f5f11-72b7-4e9e-94c7-abc104a1ef01",
        "Name": "STN1",
        "WorkGraph": [
          { "SourceId": "d3f6a9de-21eb-4861-aaa9-cf25d7348d20", "TargetId": "f39dd69f-8869-4655-9b10-006e4cf443d0" },
          { "SourceId": "f39dd69f-8869-4655-9b10-006e4cf443d0", "TargetId": "d3f6a9de-21eb-4861-aaa9-cf25d7348d20" }
        ],
        "Works": [
          {
            "Id": "d3f6a9de-21eb-4861-aaa9-cf25d7348d20",
            "Name": "Work1",
            "Calls": [
              { "Id": "b2d3ae21-a3e4-11ee-b9d1-0242ac120002", "Job": "Device1.ADV" },
              { "Id": "b2d3b002-a3e4-11ee-b9d1-0242ac120002", "Job": "Device2.ADV" },
              { "Id": "b2d3b0f3-a3e4-11ee-b9d1-0242ac120002", "Job": "Device3.ADV" },
              { "Id": "b2d3b1e4-a3e4-11ee-b9d1-0242ac120002", "Job": "Device4.ADV" },
              { "Id": "b2d3b2d5-a3e4-11ee-b9d1-0242ac120002", "Job": "Device1.RET" },
              { "Id": "b2d3b3c6-a3e4-11ee-b9d1-0242ac120002", "Job": "Device2.RET" },
              { "Id": "b2d3b4b7-a3e4-11ee-b9d1-0242ac120002", "Job": "Device3.RET" },
              { "Id": "b2d3b5a8-a3e4-11ee-b9d1-0242ac120002", "Job": "Device4.RET" }
            ],
            "CallGraph": [
              { "SourceId": "b2d3ae21-a3e4-11ee-b9d1-0242ac120002", "TargetId": "b2d3b002-a3e4-11ee-b9d1-0242ac120002" },
              { "SourceId": "b2d3b002-a3e4-11ee-b9d1-0242ac120002", "TargetId": "b2d3b0f3-a3e4-11ee-b9d1-0242ac120002" },
              { "SourceId": "b2d3b0f3-a3e4-11ee-b9d1-0242ac120002", "TargetId": "b2d3b1e4-a3e4-11ee-b9d1-0242ac120002" }
            ]
          }
        ]
      }
    ],
    "Jobs": [
      { "Id": "j1", "Name": "STN1.Device1.ADV", "Target": "STN1__Device1.ADV" },
      { "Id": "j2", "Name": "STN1.Device1.RET", "Target": "STN1__Device1.RET" },
      { "Id": "j3", "Name": "STN1.Device2.ADV", "Target": "STN1__Device2.ADV" },
      { "Id": "j4", "Name": "STN1.Device2.RET", "Target": "STN1__Device2.RET" },
      { "Id": "j5", "Name": "STN1.Device3.ADV", "Target": "STN1__Device3.ADV" },
      { "Id": "j6", "Name": "STN1.Device3.RET", "Target": "STN1__Device3.RET" },
      { "Id": "j7", "Name": "STN1.Device4.ADV", "Target": "STN1__Device4.ADV" },
      { "Id": "j8", "Name": "STN1.Device4.RET", "Target": "STN1__Device4.RET" }
    ],
    "Buttons": {
      "Auto": [ { "Id": "btn1", "Name": "AutoSelect" }, { "Id": "btn2", "Name": "AutoBTN1" } ],
      "Manual": [ { "Id": "btn3", "Name": "ManualSelect" }, { "Id": "btn4", "Name": "ManualBTN1" } ]
    },
    "Lamps": {
      "Auto": [ { "Id": "lamp1", "Name": "AutoModeLamp", "In": "-", "Out": "On" } ]
    },
    "DeviceLayouts": {
      "STN1__Device1": [554, 580, 220, 80]
    },
    "Devices": [
      { "Id": "d1", "Name": "STN1__Device1", "Type": "Device" },
      { "Id": "d2", "Name": "STN1__Device2", "Type": "Device" },
      { "Id": "d3", "Name": "STN1__Device3", "Type": "Device" },
      { "Id": "d4", "Name": "STN1__Device4", "Type": "Device" }
    ]
  }
}
```

---

### 4.3 요약

* 모든 객체는 고유한 `Id`로 식별됩니다 (System, Flow, Work, Call, Job, Button, Lamp 등).
* 고유한 Id 는 GUID 일 수도 있고, 아닐 수도 있습니다.  고유함을 보장하기만 하면 됩니다.  import/export 시에는 GUID 가 필수.
* 이름(Name)은 UI 편의용이며, 내부 연산 및 DB 저장 시에는 Id 기준.
* 관계(WorkGraph, CallGraph 등)는 모두 고유한 ID 기반으로 연결.
* 향후 AASX 파일 export 시에도 이 구조를 사용하여 타입-인스턴스 명확 구분 가능.

---

### 4.4 AASX 타입/인스턴스 구조 예시

#### HelloDS.aasx (System 인스턴스)

```json
{
  "assetAdministrationShells": [
    {
      "id": "ec5d7a91-1bc2-47cd-a8a6-fc5f9b9de111",
      "idShort": "HelloDS",
      "asset": {
        "type": "Instance",
        "kind": "Instance",
        "assetType": "System",
        "globalAssetId": {
          "value": "urn:dualsoft:system:HelloDS"
        }
      },
      "submodels": [
        {
          "idShort": "Flows",
          "submodelElements": [
            {
              "idShort": "STN1",
              "value": [
                {
                  "idShort": "WorkGraph",
                  "first": "d3f6a9de-21eb-4861-aaa9-cf25d7348d20",
                  "second": "f39dd69f-8869-4655-9b10-006e4cf443d0"
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

#### STN1\_\_Device1.aasx (디바이스 인스턴스)

```json
{
  "assetAdministrationShells": [
    {
      "id": "da3b312a-558b-49dc-8f44-cfd77620fd22",
      "idShort": "STN1__Device1",
      "asset": {
        "type": "Instance",
        "kind": "Instance",
        "assetType": "Device",
        "globalAssetId": {
          "value": "urn:dualsoft:device:STN1__Device1"
        }
      },
      "submodels": [
        {
          "idShort": "APIs",
          "submodelElements": [
            { "idShort": "ADV", "value": "AverageTime=1500, Deviation=20, Count=50" },
            { "idShort": "RET", "value": "AverageTime=1300, Deviation=10, Count=55" }
          ]
        }
      ]
    }
  ]
}
```

#### STN1\_\_Device1\_type.aasx (디바이스 타입 정의)

```json
{
  "conceptDescriptions": [
    {
      "idShort": "DoubleCylinder",
      "id": "urn:dualsoft:type:DoubleCylinder",
      "isCaseOf": [ { "value": "https://dualsoft.com/aasx/models/cylinder" } ]
    }
  ],
  "submodels": [
    {
      "idShort": "DoubleCylinderTemplate",
      "submodelElements": [
        { "idShort": "ADV", "value": "Command:Extend, Sensor:Extended" },
        { "idShort": "RET", "value": "Command:Retract, Sensor:Retracted" }
      ]
    }
  ]
}
```
