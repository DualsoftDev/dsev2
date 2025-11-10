namespace Ev2.Cpu.Ast

open System
open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// CPU 프로그램 명령어 정의 (4개 핵심 타입으로 단순화)
// ─────────────────────────────────────────────────────────────────────
// PLC/DCS에서 가장 중요한 4가지 명령어 타입만 지원하여 복잡성을 최소화:
// 1. SAssign: 변수 할당 (MOV, 계산 결과 저장)
// 2. STimer: 타이머 명령 (TON, 지연 제어)
// 3. SCounter: 카운터 명령 (CTU/CTD, 개수 세기)
// 4. SCoil: 코일/래치 명령 (SET/RESET, 상태 유지)
// ─────────────────────────────────────────────────────────────────────

/// <summary>CPU 프로그램 스테이트먼트 타입 (5개 핵심 타입)</summary>
/// <remarks>
/// PLC/DCS에서 사용되는 모든 로직을 5가지 스테이트먼트로 표현:
/// - SAssign: 변수 할당 (MOV, 계산 결과 저장)
/// - STimer: 타이머 명령 (TON, 지연 제어)
/// - SCounter: 카운터 명령 (CTU/CTD, 개수 세기)
/// - SCoil: 코일/래치 명령 (SET/RESET, 상태 유지)
/// - SUserFB: 사용자 정의 Function Block 호출
/// </remarks>
[<StructuralEquality; NoComparison>]
type DsStatement =
    /// 변수 할당 명령 (Assignment)
    /// 조건식의 결과값을 지정된 변수에 저장
    /// 예: "Motor_Speed := Setpoint * 0.8"
    | SAssign of condition:DsExpr * target:(string * Type)

    /// 타이머 명령 (Timer ON-delay)
    /// PLC 표준 TON 타이머 구현 - 입력 신호 지연 후 출력
    /// 예: "TIMER Motor_Start_Delay(RungIn: Start_Button, Preset: 5000ms)"
    | STimer of rungIn:DsExpr option * reset:DsExpr option * name:string * presetMs:int

    /// 카운터 명령 (Up/Down Counter)
    /// PLC 표준 CTU/CTD 카운터 구현 - 신호 개수 세기
    /// 예: "COUNTER Part_Count(Up: Sensor, Down: Reset_Button, Preset: 100)"
    | SCounter of up:DsExpr option * down:DsExpr option * reset:DsExpr option * name:string * preset:int

    /// 코일/래치 명령 (SET/RESET Coil)
    /// 비트 상태를 유지하는 래치 기능 - 모터 시동/정지 등에 사용
    /// 예: "COIL Motor_Run SET(Start_Button) RESET(Stop_Button) (Self-Hold)"
    | SCoil of setCond:DsExpr * resetCond:DsExpr * coil:(string * Type) * selfHold:bool

    /// 사용자 정의 Function Block 호출 (UserFB Invocation)
    /// FB 인스턴스 실행 - 입력 매핑, 출력 수집, 상태 유지
    /// 예: "Motor1(Start := Start_Button, Stop := Stop_Button) → Running, Fault"
    | SUserFB of instanceName:string * fbName:string * inputs:Map<string,DsExpr> * outputs:Set<string> * stateLayout:(string*Type) list
    with
        /// <summary>스테이트먼트가 쓰는 모든 변수 목록</summary>
        /// <returns>쓰기 대상 변수 이름 집합</returns>
        /// <remarks>
        /// 타이머와 카운터는 여러 변수를 생성합니다 (EN, TT, DN, ACC, PRE 등).
        /// UserFB는 출력 변수와 상태 변수를 모두 씁니다.
        /// </remarks>
        member this.GetWriteTargets() : Set<string> =
            match this with
            | SAssign(_, (name, _)) -> Set.singleton name
            | SCoil(_, _, (name, _), _) -> Set.singleton name
            | STimer(_, _, name, _) ->
                // Timer creates multiple variables
                Set.ofList [sprintf "%s.EN" name; sprintf "%s.TT" name; sprintf "%s.DN" name; sprintf "%s.ACC" name; sprintf "%s.PRE" name]
            | SCounter(_, _, _, name, _) ->
                Set.ofList [sprintf "%s.DN" name; sprintf "%s.CV" name; sprintf "%s.PV" name]
            | SUserFB(instanceName, _, _, outputs, stateLayout) ->
                // FB writes to outputs and state variables
                let outputSet = outputs |> Set.map (fun o -> sprintf "%s.%s" instanceName o)
                let stateSet = stateLayout |> List.map (fun (n, _) -> sprintf "%s.%s" instanceName n) |> Set.ofList
                Set.union outputSet stateSet

        /// <summary>스테이트먼트가 읽는 모든 변수 목록</summary>
        /// <returns>읽기 대상 변수 이름 집합 (모든 표현식의 변수 포함)</returns>
        member this.GetReadTargets() : Set<string> =
            let getExprVars (expr: DsExpr) = expr.GetVariables()

            match this with
            | SAssign(cond, _) -> getExprVars cond
            | STimer(rungIn, reset, _, _) ->
                let rungVars = rungIn |> Option.map getExprVars |> Option.defaultValue Set.empty
                let resetVars = reset |> Option.map getExprVars |> Option.defaultValue Set.empty
                Set.union rungVars resetVars
            | SCounter(up, down, reset, _, _) ->
                let upVars = up |> Option.map getExprVars |> Option.defaultValue Set.empty
                let downVars = down |> Option.map getExprVars |> Option.defaultValue Set.empty
                let resetVars = reset |> Option.map getExprVars |> Option.defaultValue Set.empty
                Set.unionMany [upVars; downVars; resetVars]
            | SCoil(setCond, resetCond, _, _) ->
                Set.union (getExprVars setCond) (getExprVars resetCond)
            | SUserFB(_, _, inputs, _, _) ->
                // FB reads from input expressions
                inputs
                |> Map.toSeq
                |> Seq.map snd
                |> Seq.map getExprVars
                |> Set.unionMany

        /// <summary>스테이트먼트 구조 및 제약 조건 검증</summary>
        /// <returns>검증 성공 시 Ok (), 실패 시 Error with 오류 메시지</returns>
        /// <remarks>
        /// 검증 항목:
        /// - 이름이 비어있지 않은지
        /// - 타입 호환성 (SAssign)
        /// - Preset 값이 음수가 아닌지 (STimer, SCounter)
        /// - 입력 매핑이 비어있지 않은지 (SUserFB)
        /// - 입력 표현식이 모두 유효한지 (SUserFB)
        /// </remarks>
        member this.Validate() : Result<unit, string> =
            match this with
            | SAssign(cond, (targetName, targetType)) ->
                if String.IsNullOrWhiteSpace targetName then
                    Error "Assignment target name cannot be empty"
                else
                    match cond.InferType() with
                    | Some exprType when not (TypeHelpers.areTypesCompatible exprType targetType) ->
                        Error (sprintf "Type mismatch in assignment: target is %s but expression is %s" (TypeHelpers.getTypeName targetType) (TypeHelpers.getTypeName exprType))
                    | None -> Error "Cannot infer expression type for assignment"
                    | _ -> Ok ()

            | STimer(_, _, name, preset) ->
                if String.IsNullOrWhiteSpace name then
                    Error "Timer name cannot be empty"
                elif preset < 0 then
                    Error (sprintf "Timer '%s' preset must be non-negative, got %d" name preset)
                else Ok ()

            | SCounter(_, _, _, name, preset) ->
                if String.IsNullOrWhiteSpace name then
                    Error "Counter name cannot be empty"
                elif preset < 0 then
                    Error (sprintf "Counter '%s' preset must be non-negative, got %d" name preset)
                else Ok ()

            | SCoil(_, _, (coilName, _), _) ->
                if String.IsNullOrWhiteSpace coilName then
                    Error "Coil name cannot be empty"
                else Ok ()

            | SUserFB(instanceName, fbName, inputs, outputs, stateLayout) ->
                if String.IsNullOrWhiteSpace instanceName then
                    Error "FB instance name cannot be empty"
                elif String.IsNullOrWhiteSpace fbName then
                    Error "FB type name cannot be empty"
                elif Map.isEmpty inputs then
                    Error (sprintf "FB instance '%s' has no input mappings" instanceName)
                else
                    // Validate all input expressions
                    inputs
                    |> Map.toList
                    |> List.tryPick (fun (paramName, expr) ->
                        match expr.Validate() with
                        | Error e -> Some (Error (sprintf "FB '%s' input '%s': %s" instanceName paramName e))
                        | Ok () -> None)
                    |> Option.defaultValue (Ok ())

        /// <summary>스테이트먼트를 사람이 읽을 수 있는 텍스트로 변환</summary>
        /// <param name="indentLevel">들여쓰기 레벨 (optional, 기본값: 0)</param>
        /// <returns>텍스트 표현 (예: "Motor_Speed := Setpoint * 0.8")</returns>
        member this.ToText(?indentLevel: int) : string =
            let indent = String.replicate (defaultArg indentLevel 0) "  "
            
            match this with
            | SAssign(cond, (target, _)) ->
                sprintf "%s%s := %s" indent target (cond.ToText())
            
            | STimer(rungIn, reset, name, preset) ->
                let rungText = rungIn |> Option.map (fun e -> sprintf "RungIn: %s" (e.ToText())) |> Option.defaultValue ""
                let resetText = reset |> Option.map (fun e -> sprintf ", Reset: %s" (e.ToText())) |> Option.defaultValue ""
                sprintf "%sTIMER %s(Preset: %dms%s%s)" indent name preset (if rungText <> "" then ", " + rungText else "") resetText
            
            | SCounter(up, down, reset, name, preset) ->
                let upText = up |> Option.map (fun e -> sprintf "Up: %s" (e.ToText())) |> Option.defaultValue ""
                let downText = down |> Option.map (fun e -> sprintf ", Down: %s" (e.ToText())) |> Option.defaultValue ""
                let resetText = reset |> Option.map (fun e -> sprintf ", Reset: %s" (e.ToText())) |> Option.defaultValue ""
                sprintf "%sCOUNTER %s(Preset: %d%s%s%s)" indent name preset (if upText <> "" then ", " + upText else "") downText resetText
            
            | SCoil(setCond, resetCond, (coil, _), selfHold) ->
                let holdText = if selfHold then " (Self-Hold)" else ""
                sprintf "%sCOIL %s SET(%s) RESET(%s)%s" indent coil (setCond.ToText()) (resetCond.ToText()) holdText

            | SUserFB(instanceName, fbName, inputs, outputs, _) ->
                let inputsText =
                    inputs
                    |> Map.toSeq
                    |> Seq.map (fun (k, v) -> sprintf "%s := %s" k (v.ToText()))
                    |> String.concat ", "
                let outputsText = outputs |> Set.toList |> String.concat ", "
                sprintf "%sUserFB_%s: %s(%s) → [%s]" indent instanceName fbName inputsText outputsText

/// <summary>스테이트먼트 생성 유틸리티 모듈</summary>
/// <remarks>
/// DsStatement 생성을 위한 편리한 헬퍼 함수들을 제공합니다.
/// </remarks>
module StmtBuilder =

    /// <summary>할당 스테이트먼트 생성</summary>
    let assign (target: string) (targetType: Type) (condition: DsExpr) =
        SAssign(condition, (target, targetType))

    /// <summary>타이머 스테이트먼트 생성</summary>
    let timer (name: string) (presetMs: int) (rungIn: DsExpr option) (reset: DsExpr option) =
        STimer(rungIn, reset, name, presetMs)

    /// <summary>카운터 스테이트먼트 생성</summary>
    let counter (name: string) (preset: int) (up: DsExpr option) (down: DsExpr option) (reset: DsExpr option) =
        SCounter(up, down, reset, name, preset)

    /// <summary>코일 스테이트먼트 생성</summary>
    let coil (name: string) (coilType: Type) (setCond: DsExpr) (resetCond: DsExpr) (selfHold: bool) =
        SCoil(setCond, resetCond, (name, coilType), selfHold)

    /// <summary>UserFB 호출 스테이트먼트 생성</summary>
    let userFB (instanceName: string) (fbName: string) (inputs: Map<string, DsExpr>) (outputs: Set<string>) (stateLayout: (string * Type) list) =
        SUserFB(instanceName, fbName, inputs, outputs, stateLayout)

/// <summary>스테이트먼트 분석 유틸리티 모듈</summary>
/// <remarks>
/// 스테이트먼트의 복잡도, 함수 호출 등을 분석하는 함수들을 제공합니다.
/// </remarks>
module StmtAnalysis =

    /// <summary>스테이트먼트 개수 계산 (재귀적으로 모든 스테이트먼트 카운트)</summary>
    let rec statementCount (stmt: DsStatement) : int = 1

    /// <summary>최대 중첩 깊이 계산 (재귀적으로 최대 깊이 측정)</summary>
    let rec nestingDepth (stmt: DsStatement) : int = 1

    /// <summary>루프 포함 여부 (재귀적으로 루프 검색)</summary>
    let rec hasLoops (stmt: DsStatement) : bool = false

    /// <summary>스테이트먼트에 포함된 모든 함수 호출 수집</summary>
    /// <param name="stmt">분석할 스테이트먼트</param>
    /// <returns>함수 이름 집합 (표현식 내부 함수 호출 + UserFB 이름)</returns>
    let rec getFunctionCalls (stmt: DsStatement) : Set<string> =
        match stmt with
        | SAssign(cond, _) -> cond.GetFunctionCalls()
        | STimer(rungIn, reset, _, _) ->
            let rungCalls = rungIn |> Option.map (fun e -> e.GetFunctionCalls()) |> Option.defaultValue Set.empty
            let resetCalls = reset |> Option.map (fun e -> e.GetFunctionCalls()) |> Option.defaultValue Set.empty
            Set.union rungCalls resetCalls
        | SCounter(up, down, reset, _, _) ->
            let upCalls = up |> Option.map (fun e -> e.GetFunctionCalls()) |> Option.defaultValue Set.empty
            let downCalls = down |> Option.map (fun e -> e.GetFunctionCalls()) |> Option.defaultValue Set.empty
            let resetCalls = reset |> Option.map (fun e -> e.GetFunctionCalls()) |> Option.defaultValue Set.empty
            Set.unionMany [upCalls; downCalls; resetCalls]
        | SCoil(setCond, resetCond, _, _) ->
            Set.union (setCond.GetFunctionCalls()) (resetCond.GetFunctionCalls())
        | SUserFB(_, fbName, inputs, _, _) ->
            // Collect function calls from input expressions and add FB name
            let inputCalls =
                inputs
                |> Map.toSeq
                |> Seq.map snd
                |> Seq.map (fun e -> e.GetFunctionCalls())
                |> Set.unionMany
            Set.add fbName inputCalls