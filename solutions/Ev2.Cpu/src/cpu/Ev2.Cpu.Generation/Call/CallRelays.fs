namespace Ev2.Cpu.Generation.Call

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core

// ═════════════════════════════════════════════════════════════════════
// Call Relay Patterns (RuntimeSpec.md 기반)
// ═════════════════════════════════════════════════════════════════════

/// Call 릴레이 종류
[<RequireQualifiedAccess>]
type CallRelayKind =
    /// C-01: Call 시작 릴레이 (SC)
    | StartCall
    /// C-02: Call 종료 릴레이 (EC)
    | EndCall
    /// C-03: Call 리셋 릴레이 (RC)
    | ResetCall

/// Call 시퀀스 헬퍼
module CallSequence =

    /// 이전 Call 완료 조건 생성
    let prevCallComplete (prevCallName: string option) =
        match prevCallName with
        | Some name -> boolTag $"{name}.EC"
        | None -> boolConst true

    /// 모든 Call 완료 조건 생성
    let allCallsComplete (callNames: string list) =
        match callNames with
        | [] -> boolConst true
        | names ->
            names
            |> List.map (fun name -> boolTag $"{name}.EC")
            |> all

    /// Call 체인 조건 생성 (이전 Call 완료 OR 부모 Work 시작)
    let chainCondition (parentWork: string) (prevCall: string option) =
        match prevCall with
        | Some prev ->
            // 이전 Call 완료 OR (첫 Call이고 부모 Work 시작)
            boolTag $"{prev}.EC"
        | None ->
            // 첫 번째 Call: 부모 Work 시작
            boolTag $"{parentWork}.SW"

/// Call 릴레이 생성 모듈
module CallRelays =

    /// C-01: Call 시작 릴레이 (SC)
    /// SET := (이전Call완료 OR 부모Work시작) AND 보조조건
    /// RST := (Call리셋 OR Call종료)
    let createStartCall
        (callName: string)
        (parentWork: string)
        (prevCall: string option)
        (auxCondition: DsExpr option)
        : Relay =

        let tag = DsTag.Bool($"{callName}.SC")

        // SET 조건: 체인 조건 + 보조 조건
        let chainExpr = CallSequence.chainCondition parentWork prevCall
        let auxExpr = auxCondition |> Option.defaultValue (boolConst true)
        let setExpr = chainExpr &&. auxExpr

        // RST 조건: Call 리셋 OR Call 종료
        let resetExpr = any [
            boolTag $"{callName}.RC"
            boolTag $"{callName}.EC"
        ]

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// C-02: Call 종료 릴레이 (EC)
    /// SET := (Call 완료조건 AND Call 시작됨)
    /// RST := (Call 리셋)
    let createEndCall
        (callName: string)
        (completeCondition: DsExpr option)
        : Relay =

        let tag = DsTag.Bool($"{callName}.EC")

        // SET 조건: 완료 조건 + Call 시작됨
        let completeExpr = completeCondition |> Option.defaultValue (boolConst false)
        let scTag = boolTag $"{callName}.SC"
        let setExpr = completeExpr &&. scTag

        // RST 조건: Call 리셋
        let resetExpr = boolTag $"{callName}.RC"

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// C-03: Call 리셋 릴레이 (RC)
    /// 1-cycle 펄스
    let createResetCall
        (callName: string)
        (trigger: DsExpr option)
        : Relay =

        let tag = DsTag.Bool($"{callName}.RC")

        // 트리거: 명시적 리셋 OR 부모 Work 리셋
        let defaultTrigger = boolTag $"{callName}_reset_trigger"
        let triggerExpr = trigger |> Option.defaultValue defaultTrigger

        Relay.CreateWithMode(tag, triggerExpr, boolConst false, RelayMode.Pulse)

    /// Call 기본 릴레이 그룹 생성
    let createBasicCallGroup
        (callName: string)
        (parentWork: string)
        (prevCall: string option)
        (completeCondition: DsExpr option)
        : Relay list =
        [
            createStartCall callName parentWork prevCall None
            createEndCall callName completeCondition
            createResetCall callName None
        ]

    /// Call 체인 생성 (순차 실행)
    let createCallChain
        (parentWork: string)
        (calls: (string * DsExpr option) list)
        : Relay list =

        let rec buildChain prev remaining =
            match remaining with
            | [] -> []
            | (callName, completeCondition) :: rest ->
                let relays = createBasicCallGroup callName parentWork prev completeCondition
                relays @ buildChain (Some callName) rest

        buildChain None calls

/// API 연동 Call 릴레이
module ApiCallRelays =

    /// A-01: API 호출 시작 (apiItemSet)
    /// apiItemSet = (Call.SC AND !apiItemEnd)
    let createApiCallStart
        (callName: string)
        (apiName: string)
        : Relay =

        let tag = DsTag.Bool($"{apiName}.apiItemSet")
        let scTag = boolTag $"{callName}.SC"
        let endTag = boolTag $"{apiName}.apiItemEnd"

        let setExpr = scTag &&. (!!. endTag)
        let resetExpr = endTag

        Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.SR)

    /// API 완료 감지 (Call.EC 자동 설정)
    let createApiCallComplete
        (callName: string)
        (apiName: string)
        : Relay =

        let tag = DsTag.Bool($"{callName}.EC")
        let apiEndTag = boolTag $"{apiName}.apiItemEnd"

        Relay.CreateWithMode(tag, apiEndTag, boolConst false, RelayMode.SR)

    /// API 연동 Call 전체 그룹
    let createApiCallGroup
        (callName: string)
        (parentWork: string)
        (prevCall: string option)
        (apiName: string)
        : Relay list =
        [
            CallRelays.createStartCall callName parentWork prevCall None
            createApiCallStart callName apiName
            createApiCallComplete callName apiName
            CallRelays.createResetCall callName None
        ]

/// Call 릴레이 빌더 (Fluent API)
type CallRelayBuilder(callName: string, parentWork: string) =
    let mutable prevCall : string option = None
    let mutable auxCond : DsExpr option = None
    let mutable completeCond : DsExpr option = None
    let mutable apiName : string option = None

    member this.WithPreviousCall(name: string) =
        prevCall <- Some name
        this

    member this.WithAuxCondition(expr: DsExpr) =
        auxCond <- Some expr
        this

    member this.WithCompleteCondition(expr: DsExpr) =
        completeCond <- Some expr
        this

    member this.WithApi(name: string) =
        apiName <- Some name
        this

    member _.Build() : Relay list =
        match apiName with
        | Some api ->
            ApiCallRelays.createApiCallGroup callName parentWork prevCall api
        | None ->
            CallRelays.createBasicCallGroup callName parentWork prevCall completeCond

/// Call 그룹 생성 헬퍼
module CallGroups =

    /// 순차 실행 Call 그룹
    let sequential (parentWork: string) (calls: (string * DsExpr option) list) =
        CallRelays.createCallChain parentWork calls

    /// 병렬 실행 Call 그룹 (모두 동시 시작)
    let parallelCalls (parentWork: string) (calls: (string * DsExpr option) list) =
        calls
        |> List.collect (fun (callName, completeCondition) ->
            CallRelays.createBasicCallGroup callName parentWork None completeCondition)

    /// 조건부 분기 Call 그룹
    let conditional
        (parentWork: string)
        (condition: DsExpr)
        (trueCalls: (string * DsExpr option) list)
        (falseCalls: (string * DsExpr option) list)
        : Relay list =

        let trueRelays =
            trueCalls
            |> List.collect (fun (callName, completeCondition) ->
                let auxCond = Some condition
                [
                    CallRelays.createStartCall callName parentWork None auxCond
                    CallRelays.createEndCall callName completeCondition
                    CallRelays.createResetCall callName None
                ])

        let falseRelays =
            falseCalls
            |> List.collect (fun (callName, completeCondition) ->
                let auxCond = Some (!!. condition)
                [
                    CallRelays.createStartCall callName parentWork None auxCond
                    CallRelays.createEndCall callName completeCondition
                    CallRelays.createResetCall callName None
                ])

        trueRelays @ falseRelays
