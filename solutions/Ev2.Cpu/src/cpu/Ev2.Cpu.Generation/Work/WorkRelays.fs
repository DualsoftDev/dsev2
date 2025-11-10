namespace Ev2.Cpu.Generation.Work

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core

// ═════════════════════════════════════════════════════════════════════
// Work Relay Patterns (RuntimeSpec.md 기반)
// ═════════════════════════════════════════════════════════════════════

/// Work 릴레이 종류
[<RequireQualifiedAccess>]
type WorkRelayKind =
    /// W-01: Work 시작 릴레이 (SW)
    | StartWork
    /// W-02: Work 종료 릴레이 (EW)
    | EndWork
    /// W-03: Work 리셋 릴레이 (RW)
    | ResetWork
    /// Work Ready 상태
    | ReadyWork
    /// Work Going 상태
    | GoingWork
    /// Work Finish 상태
    | FinishWork
    /// Work Homing 상태
    | HomingWork
    /// Work 에러
    | ErrorWork

/// Work 상태 전이 헬퍼
module WorkState =

    /// Work 상태 enum (RuntimeSpec.md 기반)
    type State =
        | Ready = 0
        | Going = 1
        | Finish = 2
        | Homing = 3

    /// Work 상태 태그 생성
    let stateTag (workName: string) =
        DsTag.Int($"{workName}_State")

    /// 상태 체크 표현식
    let isState (workName: string) (state: State) =
        Terminal(stateTag workName) ==. intConst (int state)

    /// 상태 설정 Statement
    let setState (workName: string) (state: State) =
        assign (stateTag workName) (intConst (int state))

/// Work 릴레이 생성 모듈
module WorkRelays =

    /// W-01: Work 시작 릴레이 (SW)
    /// SET := (선행조건 AND 보조조건 AND 안전조건)
    /// RST := (리셋조건 OR 에러조건)
    let createStartWork
        (workName: string)
        (prevWorkComplete: DsExpr option)
        (apiStart: DsExpr option)
        (auxCondition: DsExpr option)
        (safetyOk: DsExpr option)
        (resetCondition: DsExpr option)
        : Relay =

        let tag = DsTag.Bool($"{workName}.SW")

        // SET 조건 구성
        let setConditions = [
            prevWorkComplete |> Option.defaultValue (boolConst true)
            apiStart |> Option.defaultValue (boolConst false)
            auxCondition |> Option.defaultValue (boolConst true)
            safetyOk |> Option.defaultValue (boolConst true)
        ]
        let setExpr = all setConditions

        // RST 조건 구성
        let resetConditions = [
            resetCondition |> Option.defaultValue (boolConst false)
            boolTag $"{workName}.Error"
            boolTag "System.Emergency"
        ]
        let resetExpr = any resetConditions

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// W-02: Work 종료 릴레이 (EW)
    /// SET := (모든Call완료 AND 시작됨 AND 에러없음)
    /// RST := (리셋)
    let createEndWork
        (workName: string)
        (allCallsComplete: DsExpr)
        (workGoing: DsExpr option)
        (resetCondition: DsExpr option)
        : Relay =

        let tag = DsTag.Bool($"{workName}.EW")

        let setConditions = [
            allCallsComplete
            workGoing |> Option.defaultValue (boolTag $"{workName}.Going")
            !!. (boolTag $"{workName}.Error")
        ]
        let setExpr = all setConditions

        let resetExpr = resetCondition |> Option.defaultValue (boolTag $"{workName}.RW")

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// W-03: Work 리셋 릴레이 (RW)
    /// 1-cycle 펄스 형태
    let createResetWork
        (workName: string)
        (trigger: DsExpr option)
        : Relay =

        let tag = DsTag.Bool($"{workName}.RW")
        let triggerExpr = trigger |> Option.defaultValue (boolTag $"{workName}_reset_trigger")

        Relay.CreateWithMode(tag, triggerExpr, boolConst false, RelayMode.Pulse)

    /// Work Ready 상태 릴레이
    let createReadyWork
        (workName: string)
        (systemReady: DsExpr option)
        (originConfirmed: DsExpr option)
        : Relay =

        let tag = DsTag.Bool($"{workName}.Ready")

        let setConditions = [
            systemReady |> Option.defaultValue (boolTag "System.Ready")
            originConfirmed |> Option.defaultValue (boolTag $"{workName}.OG")
            !!. (boolTag $"{workName}.Error")
        ]
        let setExpr = all setConditions

        let resetConditions = [
            boolTag $"{workName}.SW"
            boolTag "System.Emergency"
        ]
        let resetExpr = any resetConditions

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// Work Going 상태 릴레이
    let createGoingWork
        (workName: string)
        : Relay =

        let tag = DsTag.Bool($"{workName}.Going")
        let swTag = boolTag $"{workName}.SW"
        let ewTag = boolTag $"{workName}.EW"
        let rwTag = boolTag $"{workName}.RW"

        let setExpr = swTag &&. (!!. ewTag)
        let resetExpr = ewTag ||. rwTag

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// Work Finish 상태 릴레이
    let createFinishWork
        (workName: string)
        : Relay =

        let tag = DsTag.Bool($"{workName}.Finish")
        let ewTag = boolTag $"{workName}.EW"
        let rwTag = boolTag $"{workName}.RW"

        let setExpr = ewTag
        let resetExpr = rwTag

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// Work 타임아웃 에러 릴레이
    /// RuntimeSpec.md E-01 사양
    let createTimeoutError
        (workName: string)
        (timeLimit: int option)
        : Relay =

        let tag = DsTag.Bool($"{workName}.TimeoutError")
        let timeLimitMs = timeLimit |> Option.defaultValue 30000

        // 타이머 조건: Work.Going AND !Work.EW
        let timerCondition =
            boolTag $"{workName}.Going" &&. (!!. (boolTag $"{workName}.EW"))

        // TON 함수 호출 (timer name required)
        let timerName = sprintf "%s_TimeoutTimer" workName
        let timerExpr = fn "TON" [timerCondition; strConst timerName; intConst timeLimitMs]

        let setExpr = timerExpr
        let resetExpr = boolTag "System.ClearButton" ||. boolTag $"{workName}.RW"

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// Work 전체 릴레이 그룹 생성 (기본 패턴)
    let createBasicWorkGroup
        (workName: string)
        (prevWorkComplete: DsExpr option)
        (allCallsComplete: DsExpr)
        : Relay list =
        [
            createStartWork workName prevWorkComplete None None None None
            createEndWork workName allCallsComplete None None
            createResetWork workName None
            createReadyWork workName None None
            createGoingWork workName
            createFinishWork workName
            createTimeoutError workName None
        ]

/// Work 릴레이 그룹 빌더 (Fluent API)
type WorkRelayBuilder(workName: string) =
    let mutable prevWork : DsExpr option = None
    let mutable apiStart : DsExpr option = None
    let mutable auxCond : DsExpr option = None
    let mutable safetyCond : DsExpr option = None
    let mutable resetCond : DsExpr option = None
    let mutable allCalls : DsExpr option = None
    let mutable timeLimit : int option = None

    member this.WithPreviousWork(expr: DsExpr) =
        prevWork <- Some expr
        this

    member this.WithApiStart(expr: DsExpr) =
        apiStart <- Some expr
        this

    member this.WithAuxCondition(expr: DsExpr) =
        auxCond <- Some expr
        this

    member this.WithSafety(expr: DsExpr) =
        safetyCond <- Some expr
        this

    member this.WithReset(expr: DsExpr) =
        resetCond <- Some expr
        this

    member this.WithAllCallsComplete(expr: DsExpr) =
        allCalls <- Some expr
        this

    member this.WithTimeLimit(ms: int) =
        timeLimit <- Some ms
        this

    member _.Build() : Relay list =
        let allCallsExpr = allCalls |> Option.defaultValue (boolConst true)
        WorkRelays.createBasicWorkGroup workName prevWork allCallsExpr
