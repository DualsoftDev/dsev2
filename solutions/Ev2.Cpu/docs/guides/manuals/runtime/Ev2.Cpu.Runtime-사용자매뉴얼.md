# Ev2.Cpu.Runtime 사용자 매뉴얼

## 개요

`Ev2.Cpu.Runtime`은 IEC 61131-3 표준 PLC 프로그램을 실행하는 런타임 엔진입니다. 표현식 평가, 문장 실행, 메모리 관리, 스캔 사이클 제어, 런타임 중 코드 업데이트 등 PLC 실행에 필요한 모든 기능을 제공합니다.

---

## 주요 기능

### 1. 실행 컨텍스트 (ExecutionContext)

PLC 런타임의 전체 상태를 관리하는 중심 객체입니다.

#### 컨텍스트 생성

```fsharp
open Ev2.Cpu.Runtime

// 기본 컨텍스트
let ctx = Context.create()

// 사이클 시간 설정
let ctx = Context.withCycleTime 100 ctx  // 100ms 주기

// 메모리 크기 설정
let ctx = Context.withMemorySize 5000 ctx  // 최대 5000개 변수
```

#### 컨텍스트 속성

```fsharp
type ExecutionContext = {
    // 메모리 관리
    Memory: Memory

    // 실행 상태
    State: ExecutionState  // Running, Stopped, Paused

    // 타이밍
    CycleTime: int         // 사이클 시간 (ms)
    LastCycle: DateTime    // 마지막 스캔 시각
    LastCycleTicks: int64  // 마지막 스캔 틱

    // 디버그
    Trace: string list     // 트레이스 로그
    ErrorCount: int        // 에러 카운트
    WarningCount: int      // 경고 카운트

    // 브레이크포인트
    BreakPoints: Set<string>  // 브레이크포인트 위치

    // 워치리스트
    WatchList: Set<string>    // 감시 변수 목록
}
```

#### 실행 상태

```fsharp
type ExecutionState =
    | Stopped   // 정지
    | Running   // 실행 중
    | Paused    // 일시 정지
```

#### 컨텍스트 조작

```fsharp
// 상태 변경
Context.start ctx       // Running으로 전환
Context.stop ctx        // Stopped로 전환
Context.pause ctx       // Paused로 전환

// 브레이크포인트
Context.addBreakPoint ctx "step:5"
Context.removeBreakPoint ctx "step:5"
Context.clearBreakPoints ctx

// 워치리스트
Context.addWatch ctx "temperature"
Context.removeWatch ctx "temperature"
Context.clearWatchList ctx

// 로그
Context.trace ctx "Starting pump sequence"
Context.warning ctx "Pressure high"
Context.error ctx "Emergency stop triggered"

// 리셋
Context.reset ctx  // 에러/경고 카운트, 로그 초기화
```

#### 상태 조회

```fsharp
// 전체 상태 스냅샷
let status = Context.getStatus ctx

printfn "State: %A" status.State
printfn "Scan: %d" status.ScanNumber
printfn "Errors: %d" status.ErrorCount
printfn "Uptime: %.2f sec" status.UptimeSec
```

---

### 2. 메모리 시스템 (Memory)

PLC 변수를 관리하는 고성능 메모리 시스템입니다.

#### 메모리 영역

```fsharp
type MemoryArea =
    | Input     // 입력 (I:) - 읽기 전용
    | Output    // 출력 (O:) - 읽기/쓰기
    | Local     // 로컬 (L:) - 읽기/쓰기
    | Internal  // 내부 (V:) - 읽기/쓰기
```

#### 변수 선언

```fsharp
// 명시적 선언
ctx.Memory.DeclareVariable("temperature", DsDataType.TDouble, MemoryArea.Internal)
ctx.Memory.DeclareInput("sensor1", DsDataType.TBool)
ctx.Memory.DeclareOutput("valve1", DsDataType.TBool)
ctx.Memory.DeclareLocal("counter", DsDataType.TInt)

// Retain 변수 (전원 재투입 시 값 유지)
ctx.Memory.DeclareInternal("totalCount", DsDataType.TInt, retain = true)
```

#### 값 읽기/쓰기

```fsharp
// 값 설정 (변수가 선언되어 있어야 함)
ctx.Memory.Set("temperature", box 25.5)
ctx.Memory.Set("valve1", box true)

// 값 읽기
let temp = ctx.Memory.Get("temperature")  // obj
let tempVal = unbox<double> temp

// 입력 시뮬레이션
ctx.Memory.SetInput("sensor1", box true)
```

#### 변경 감지 (Selective Execution)

```fsharp
// 변경 확인
let changed = ctx.Memory.HasChanged("temperature")

// 변경된 변수 목록
let changedVars = ctx.Memory.GetChangedVariables()

// 모든 변수를 변경됨으로 표시
ctx.Memory.MarkAllChanged()

// 변경 플래그 초기화 (스캔 종료 시)
ctx.Memory.ClearChangeFlags()
```

#### 의존성 추적

```fsharp
// 의존성 맵 설정
let dependencies = DependencyAnalyzer.buildDependencyMap statements
ctx.Memory.SetDependencyMap dependencies

// output은 input1, input2에 의존
// dependencies = Map ["output", Set ["input1"; "input2"]]
```

#### 메모리 스냅샷

```fsharp
// 스냅샷 생성
let snapshot = ctx.Memory.Snapshot()

// 스냅샷 복원
ctx.Memory.Restore(snapshot)

// Retain 스냅샷 (retain 변수만)
let retainSnapshot = ctx.Memory.CreateRetainSnapshot()
ctx.Memory.RestoreFromSnapshot(retainSnapshot)
```

#### 히스토리

```fsharp
// 변경 히스토리 조회 (디버깅용)
let history = ctx.Memory.GetHistory()
for (varName, value, timestamp) in history do
    printfn "%s: %s = %A" (timestamp.ToString("HH:mm:ss")) varName value
```

---

### 3. 표현식 평가 (ExprEvaluator)

PLC 표현식을 런타임에 평가합니다.

#### 기본 평가

```fsharp
open Ev2.Cpu.Runtime

// 표현식 평가
let result = ExprEvaluator.eval ctx expr  // obj 반환

// 예제
ctx.Memory.Set("a", box 10)
ctx.Memory.Set("b", box 20)

let expr = DsExpr.eVar "a" None .+ DsExpr.eVar "b" None
let result = ExprEvaluator.eval ctx expr  // box 30
```

#### 타입별 평가

```fsharp
// Bool 평가
let boolResult = ExprEvaluator.evalBool ctx (DsExpr.eBool true)

// Int 평가
let intResult = ExprEvaluator.evalInt ctx (DsExpr.eInt 42)

// Double 평가
let doubleResult = ExprEvaluator.evalDouble ctx (DsExpr.eDouble 3.14)

// String 평가
let strResult = ExprEvaluator.evalString ctx (DsExpr.eString "Hello")
```

#### 안전한 평가

```fsharp
// Try 패턴
match ExprEvaluator.tryEval ctx expr with
| Ok value -> printfn "Result: %A" value
| Error msg -> printfn "Evaluation error: %s" msg
```

---

### 4. 문장 실행 (StmtEvaluator)

PLC 문장을 실행하고 스캔 사이클을 관리합니다.

#### 단일 문장 실행

```fsharp
// Assign 문장
let assignStmt = Statement.assign 1 (DsTag.create "output" TInt) (DsExpr.eInt 100)
StmtEvaluator.execOne ctx assignStmt

// Command 문장
let commandStmt =
    Statement.command 2
        (DsExpr.eVar "temp" None .> DsExpr.eInt 100)
        (DsExpr.eCall "SET" [DsExpr.eVar "alarm" None])
StmtEvaluator.execOne ctx commandStmt
```

#### 문장 리스트 실행

```fsharp
let statements = [
    Statement.assign 1 (DsTag.create "counter" TInt) (DsExpr.eInt 0)
    Statement.command 2
        (DsExpr.eVar "increment" None)
        (DsExpr.eCall "MOV" [
            DsExpr.eVar "counter" None .+ DsExpr.eInt 1
            DsExpr.eVar "counter" None
        ])
]

StmtEvaluator.execList ctx statements
```

#### 스캔 사이클

```fsharp
// 전체 스캔 실행
StmtEvaluator.execScan ctx statements
// - 스캔 카운터 증가
// - 모든 문장 실행
// - 실행 시간 측정
// - 오버런 경고
// - LastCycle 업데이트

// 선택적 스캔 (변경된 변수만)
StmtEvaluator.execScanSelective ctx statements
// - 의존성 분석으로 필요한 문장만 실행
// - 성능 최적화
```

#### 연속 실행

```fsharp
open System.Threading

// 취소 토큰 생성
let cts = new CancellationTokenSource()

// 연속 스캔 루프 (비동기)
task {
    StmtEvaluator.execContinuous ctx statements cts.Token
}

// 중지
cts.Cancel()
```

---

### 5. 스캔 엔진 (CpuScanEngine)

Task 기반의 고급 스캔 엔진입니다.

#### 스캔 설정

```fsharp
type ScanConfig = {
    CycleTimeMs: int option      // 사이클 시간 (None이면 ctx 사용)
    WarnIfOverMs: int option     // 오버런 경고 임계값
    SelectiveMode: bool          // 선택적 스캔 모드
}

let config = {
    CycleTimeMs = Some 100      // 100ms 주기
    WarnIfOverMs = Some 5000    // 5초 초과 시 경고
    SelectiveMode = true        // 선택적 스캔 활성화
}
```

#### 엔진 생성

```fsharp
let program = { Name = "Main"; Body = statements; Description = None }

// 기본 생성
let engine = CpuScan.createDefault program

// 고급 생성
let engine =
    CpuScan.create(
        program,
        Some ctx,                    // 실행 컨텍스트
        Some config,                 // 스캔 설정
        Some updateManager,          // 런타임 업데이트 매니저
        Some retainStorage           // Retain 저장소
    )
```

#### 엔진 실행

```fsharp
// 단발 스캔
let elapsedMs = engine.ScanOnce()
printfn "Scan completed in %d ms" elapsedMs

// 연속 실행 시작
task {
    do! engine.StartAsync()
}

// 외부 취소 토큰과 연결
let cts = new CancellationTokenSource()
task {
    do! engine.StartAsync(externalToken = cts.Token)
}

// 중지
task {
    do! engine.StopAsync(timeoutMs = 5000)
}
```

---

### 6. 런타임 업데이트 (RuntimeUpdateManager)

실행 중인 PLC 프로그램을 안전하게 업데이트합니다.

#### 업데이트 설정

```fsharp
type UpdateConfig = {
    ForceValidation: bool        // 검증 강제 (기본: true)
    AutoRollback: bool           // 자동 롤백 (기본: true)
    MaxSnapshotHistory: int      // 스냅샷 히스토리 최대 개수
    UpdateTimeoutMs: int option  // 업데이트 타임아웃
}

let updateConfig = UpdateConfig.Default
```

#### 업데이트 매니저 생성

```fsharp
let userLib = UserLibrary.create()
let updateManager = RuntimeUpdateManager(ctx, userLib, Some updateConfig)
```

#### 업데이트 요청

```fsharp
open Ev2.Cpu.Core.UserDefined

// UserFC 업데이트
let fcRequest = UpdateRequest.updateFC newFC
updateManager.EnqueueUpdate(fcRequest)

// UserFB 업데이트
let fbRequest = UpdateRequest.updateFB newFB
updateManager.EnqueueUpdate(fbRequest)

// FB 인스턴스 업데이트
let instRequest = UpdateRequest.updateInstance newInstance
updateManager.EnqueueUpdate(instRequest)

// Program.Body 업데이트
let bodyRequest = UpdateRequest.updateBody newStatements
updateManager.EnqueueUpdate(bodyRequest)

// 메모리 값 업데이트
let memRequest = UpdateRequest.updateMemory "counter" (box 100)
updateManager.EnqueueUpdate(memRequest)

// 배치 업데이트 (원자적 트랜잭션)
let batchRequest = UpdateRequest.batch [fcRequest; fbRequest; instRequest]
updateManager.EnqueueUpdate(batchRequest)
```

#### 업데이트 처리

```fsharp
// 대기 중인 업데이트 처리 (스캔 사이클 시작 시)
let results = updateManager.ProcessPendingUpdates()

for result in results do
    match result with
    | UpdateResult.Success msg ->
        printfn "Success: %s" msg

    | UpdateResult.ValidationFailed errors ->
        for err in errors do
            printfn "Validation error: %s" (err.Format())

    | UpdateResult.ApplyFailed error ->
        printfn "Apply failed: %s" error

    | UpdateResult.RolledBack (reason, originalError) ->
        printfn "Rolled back: %s" reason

    | UpdateResult.PartialSuccess (succeeded, failed, errors) ->
        printfn "Partial: %d succeeded, %d failed" succeeded failed
```

#### 수동 롤백

```fsharp
// 마지막 스냅샷으로 롤백
match updateManager.Rollback() with
| Ok () -> printfn "Rollback successful"
| Error msg -> printfn "Rollback failed: %s" msg
```

#### 업데이트 통계

```fsharp
let stats = updateManager.GetStatistics()

printfn "Total Requests: %d" stats.TotalRequests
printfn "Success: %d" stats.SuccessCount
printfn "Failed: %d" stats.FailedCount
printfn "Rolled Back: %d" stats.RolledBackCount
printfn "Success Rate: %.1f%%" stats.SuccessRate
```

#### 이벤트 로그

```fsharp
let events = updateManager.GetEventLog()

for event in events do
    printfn "%s" event.Description
```

---

### 7. 버전 관리 (VersionManager)

상태 스냅샷과 히스토리를 관리합니다.

#### 스냅샷 생성

```fsharp
let versionMgr = VersionManager(maxHistory = 10)

// 스냅샷 생성
let snapshot = versionMgr.CreateSnapshot(userLib, Some programBody, "Before update")

// 스냅샷 저장
let savedSnapshot = versionMgr.CreateAndSave(userLib, Some programBody, "Auto-snapshot")
```

#### 스냅샷 복원

```fsharp
let getProgramBody () = Some currentProgramBody
let updateProgramBody (body: DsStmt list) =
    currentProgramBody <- body

match versionMgr.RestoreSnapshot(snapshot, userLib, Some getProgramBody, Some updateProgramBody) with
| Ok () -> printfn "Restored successfully"
| Error msg -> printfn "Restore failed: %s" msg
```

#### 히스토리 관리

```fsharp
// 최근 스냅샷
let latest = versionMgr.GetLatestSnapshot()

// 전체 히스토리
let history = versionMgr.GetHistory()

for snap in history do
    printfn "%s" (snap.Summary())

// 히스토리 초기화
versionMgr.ClearHistory()
```

---

### 8. Retain 메모리

전원 재투입 시에도 값을 유지하는 Retain 변수를 지원합니다.

#### Retain 저장소

```fsharp
type IRetainStorage =
    abstract Save: RetainSnapshot -> Result<unit, string>
    abstract Load: unit -> Result<RetainSnapshot option, string>

// 바이너리 파일 저장소
let retainStorage = BinaryRetainStorage("retain.dat")
```

#### 자동 저장/복원

```fsharp
// 엔진 생성 시 Retain 저장소 지정
let engine =
    CpuScan.create(
        program,
        Some ctx,
        Some config,
        None,
        Some retainStorage  // 자동 저장/복원 활성화
    )

// 엔진 시작 시 자동 복원
// 엔진 정지 시 자동 저장
```

#### 수동 저장/복원

```fsharp
// 스냅샷 생성
let snapshot = ctx.Memory.CreateRetainSnapshot()

// 저장
match retainStorage.Save(snapshot) with
| Ok () -> printfn "Retain data saved"
| Error err -> printfn "Save failed: %s" err

// 로드
match retainStorage.Load() with
| Ok (Some snapshot) ->
    ctx.Memory.RestoreFromSnapshot(snapshot)
    printfn "Retain data restored"
| Ok None ->
    printfn "No retain data found"
| Error err ->
    printfn "Load failed: %s" err
```

---

### 9. 내장 함수

런타임이 제공하는 표준 함수들입니다.

#### 수학 함수

```fsharp
// 기본 산술
ADD(a, b, ...)      // 합계
SUB(a, b)           // 뺄셈
MUL(a, b, ...)      // 곱셈
DIV(a, b)           // 나눗셈
MOD(a, b)           // 나머지

// 수학 함수
ABS(x)              // 절댓값
SQRT(x)             // 제곱근
POWER(x, y)         // 거듭제곱
EXP(x)              // 지수
LOG(x)              // 자연로그
LOG10(x)            // 상용로그

// 삼각함수
SIN(x), COS(x), TAN(x)
ASIN(x), ACOS(x), ATAN(x)

// 반올림
ROUND(x)            // 반올림
FLOOR(x)            // 내림
CEIL(x)             // 올림
TRUNC(x)            // 정수 부분
```

#### 비교/선택 함수

```fsharp
MIN(a, b, ...)      // 최솟값
MAX(a, b, ...)      // 최댓값
CLAMP(x, min, max)  // 범위 제한
LIMIT(x, min, max)  // 별칭
IF(cond, then, else) // 조건 선택
```

#### 문자열 함수

```fsharp
LEN(str)            // 문자열 길이
CONCAT(s1, s2, ...) // 문자열 연결
LEFT(str, n)        // 왼쪽 n자
RIGHT(str, n)       // 오른쪽 n자
MID(str, pos, len)  // 부분 문자열
FIND(str, sub)      // 부분 문자열 검색
REPLACE(str, old, new) // 치환
UPPER(str)          // 대문자 변환
LOWER(str)          // 소문자 변환
TRIM(str)           // 공백 제거
```

#### 타입 변환

```fsharp
TO_BOOL(x)          // Bool 변환
TO_INT(x)           // Int 변환
TO_DOUBLE(x)        // Double 변환
TO_STRING(x)        // String 변환
```

#### 비트 연산

```fsharp
SHL(x, n)           // 왼쪽 시프트
SHR(x, n)           // 오른쪽 시프트
ROL(x, n)           // 왼쪽 로테이트
ROR(x, n)           // 오른쪽 로테이트
```

#### 시스템 함수

```fsharp
PRINT(...)          // 콘솔 출력 (디버깅)
NOW()               // 현재 시간 (ms)
RANDOM()            // 난수 [0.0, 1.0)
RANDOM(max)         // 난수 [0, max)
RANDOM(min, max)    // 난수 [min, max)
```

---

### 10. 성능 프로파일링

런타임 성능을 측정하고 분석합니다.

#### 프로파일러 생성

```fsharp
let profiler = PerformanceProfiler.create()
```

#### 스캔 통계

```fsharp
// 스캔 기록
profiler.RecordScan(elapsedMs)

// 통계 조회
let scanStats = profiler.GetScanStats()

printfn "Total Scans: %d" scanStats.TotalScans
printfn "Average: %.2f ms" scanStats.AverageScanMs
printfn "Min: %d ms" scanStats.MinScanMs
printfn "Max: %d ms" scanStats.MaxScanMs
printfn "Overruns: %d" scanStats.OverrunCount
printfn "Overrun Rate: %.1f%%" scanStats.OverrunRate
```

#### 메모리 통계

```fsharp
let memStats = profiler.GetMemoryStats()

printfn "Heap Size: %d MB" (memStats.HeapSizeMB)
printfn "Gen0 Collections: %d" memStats.Gen0Collections
printfn "Gen1 Collections: %d" memStats.Gen1Collections
printfn "Gen2 Collections: %d" memStats.Gen2Collections
```

#### 리셋

```fsharp
profiler.Reset()
```

---

## 통합 시나리오

### 시나리오 1: 기본 PLC 프로그램 실행

```fsharp
open Ev2.Cpu.Runtime
open Ev2.Cpu.Core

// 1. 컨텍스트 생성
let ctx = Context.create()

// 2. 변수 선언
ctx.Memory.DeclareInput("start_button", TBool)
ctx.Memory.DeclareOutput("motor", TBool)
ctx.Memory.DeclareInternal("running", TBool, retain = true)

// 3. 프로그램 작성
let program = {
    Name = "MotorControl"
    Body = [
        // IF start_button THEN running := TRUE
        Statement.command 1
            (DsExpr.eVar "start_button" None)
            (DsExpr.eCall "SET" [DsExpr.eVar "running" None])

        // motor := running
        Statement.assign 2
            (DsTag.create "motor" TBool)
            (DsExpr.eVar "running" None)
    ]
    Description = Some "Simple motor control"
}

// 4. 입력 시뮬레이션
ctx.Memory.SetInput("start_button", box true)

// 5. 실행
StmtEvaluator.execScan ctx program.Body

// 6. 출력 확인
let motorState = ctx.Memory.Get("motor")
printfn "Motor: %b" (unbox<bool> motorState)
```

### 시나리오 2: 연속 실행 with 런타임 업데이트

```fsharp
open System.Threading.Tasks

// 1. 초기 설정
let ctx = Context.create()
let userLib = UserLibrary.create()
let updateManager = RuntimeUpdateManager(ctx, userLib, None)
let retainStorage = BinaryRetainStorage("retain.dat")

let config = {
    CycleTimeMs = Some 100
    WarnIfOverMs = Some 5000
    SelectiveMode = true
}

// 2. 프로그램 생성
let mutable program = {
    Name = "Main"
    Body = [
        // 초기 로직
    ]
    Description = None
}

// 3. 엔진 시작
let engine =
    CpuScan.create(program, Some ctx, Some config, Some updateManager, Some retainStorage)

task {
    do! engine.StartAsync()
}

// 4. 런타임 중 프로그램 수정
task {
    do! Task.Delay(5000)  // 5초 후

    let newStatements = [
        // 새로운 로직
    ]

    let updateReq = UpdateRequest.updateBody newStatements
    updateManager.EnqueueUpdate(updateReq)

    // 다음 스캔 사이클에서 자동 적용
}

// 5. 중지
task {
    do! Task.Delay(60000)  // 1분 후 종료
    do! engine.StopAsync()
}
```

### 시나리오 3: UserFB 핫 스왑

```fsharp
// 1. 초기 FB 등록
let counterFB_v1 = (* 버전 1 FB 정의 *)
userLib.RegisterFB(counterFB_v1) |> ignore

let counterInst = FBInstance.create counterFB_v1 "counter1"
userLib.RegisterInstance(counterInst) |> ignore

// 2. 실행 중...

// 3. FB 업그레이드 (새 Static 추가)
let counterFB_v2 = (* 버전 2 - 새 Static 변수 추가 *)

let fbUpdate = UpdateRequest.updateFB counterFB_v2
updateManager.EnqueueUpdate(fbUpdate)

// 4. 다음 스캔에서 자동 적용
// - FB 정의 업데이트
// - 모든 인스턴스 자동 마이그레이션
// - 기존 Static 값 보존
// - 새 Static 초기화
// - 실패 시 자동 롤백
```

---

## 모범 사례

### 1. 메모리 관리

```fsharp
// 항상 변수를 명시적으로 선언
ctx.Memory.DeclareVariable("temp", TDouble, Internal)
// Memory.Set은 선언된 변수만 설정 가능

// Retain 변수는 신중하게 사용
ctx.Memory.DeclareInternal("totalCount", TInt, retain = true)
// 너무 많은 Retain 변수는 저장/복원 시간 증가
```

### 2. 스캔 주기 최적화

```fsharp
// 선택적 스캔 모드 활성화 (대규모 프로그램)
let config = { SelectiveMode = true; ... }

// 의존성 맵 설정
let deps = DependencyAnalyzer.buildDependencyMap statements
ctx.Memory.SetDependencyMap deps

// 변경 플래그 관리
ctx.Memory.MarkAllChanged()  // 첫 스캔
StmtEvaluator.execScanSelective ctx statements
ctx.Memory.ClearChangeFlags()  // 스캔 종료
```

### 3. 에러 처리

```fsharp
// Try 패턴 사용
try
    StmtEvaluator.execScan ctx statements
with
| ex ->
    Context.error ctx (sprintf "Scan failed: %s" ex.Message)
    // 에러 복구 로직

// 오버런 모니터링
if elapsedMs > ctx.CycleTime then
    Context.warning ctx (sprintf "Scan overrun: %dms" elapsedMs)
```

### 4. 런타임 업데이트

```fsharp
// 배치 업데이트로 원자성 보장
let batchReq = UpdateRequest.batch [
    UpdateRequest.updateFB newFB
    UpdateRequest.updateInstance newInstance
    UpdateRequest.updateBody newProgram
]
// 하나라도 실패하면 전체 롤백

// 검증 강제
let config = { ForceValidation = true; ... }
// 모든 업데이트 전에 검증 수행
```

### 5. 디버깅

```fsharp
// 브레이크포인트 설정
Context.addBreakPoint ctx "step:10"

// 워치리스트로 변수 추적
Context.addWatch ctx "temperature"
Context.addWatch ctx "pressure"

// 히스토리 확인
let history = ctx.Memory.GetHistory()
```

---

## API 참조

### 주요 모듈

| 모듈 | 설명 |
|------|------|
| `Context` | 실행 컨텍스트 관리 |
| `Memory` | 메모리 시스템 |
| `ExprEvaluator` | 표현식 평가 |
| `StmtEvaluator` | 문장 실행 |
| `CpuScanEngine` | 스캔 엔진 |
| `RuntimeUpdateManager` | 런타임 업데이트 |
| `VersionManager` | 버전/스냅샷 관리 |
| `DependencyAnalyzer` | 의존성 분석 |
| `PerformanceProfiler` | 성능 프로파일링 |

### 주요 타입

| 타입 | 용도 |
|------|------|
| `ExecutionContext` | 실행 컨텍스트 |
| `ExecutionState` | 실행 상태 |
| `Memory` | 메모리 관리자 |
| `MemoryArea` | 메모리 영역 |
| `ScanConfig` | 스캔 설정 |
| `UpdateRequest` | 업데이트 요청 |
| `UpdateResult` | 업데이트 결과 |
| `RuntimeSnapshot` | 상태 스냅샷 |
| `RetainSnapshot` | Retain 스냅샷 |

---

## 추가 리소스

- **CPU 스팩문서.md**: 런타임 스펙 전체 문서
- **Ev2.Cpu.Core-사용자매뉴얼.md**: 핵심 타입 참조
- **프로젝트 테스트**: `src/UnitTest/cpu/Ev2.Cpu.Runtime.Tests/` 디렉토리

---

## 버전 정보

- **현재 버전**: 1.0.0
- **대상 프레임워크**: .NET 8.0
- **언어**: F# 8.0

---

## 라이선스

이 프로젝트는 회사 내부 라이선스에 따라 배포됩니다.
