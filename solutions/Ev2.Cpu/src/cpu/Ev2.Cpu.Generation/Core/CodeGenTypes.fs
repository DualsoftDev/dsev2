namespace Ev2.Cpu.Generation.Core

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement

// ═════════════════════════════════════════════════════════════════════
// Relay Types - RuntimeSpec.md 기반 확장
// ═════════════════════════════════════════════════════════════════════

/// Relay 동작 모드
[<RequireQualifiedAccess>]
type RelayMode =
    /// SR(Set-Reset) 래치: 상태 유지, 명시적 RST 필요
    | SR
    /// 펄스: 1스캔만 유지, 자동 리셋
    | Pulse
    /// 원샷: Rising edge 감지 후 1스캔
    | OneShot
    /// 조건부: SET 조건에 따라 직접 ON/OFF
    | Conditional

/// Relay 우선순위 규칙
[<RequireQualifiedAccess>]
type RelayPriority =
    /// RST 우선 (안전 우선 원칙)
    | ResetFirst
    /// SET 우선
    | SetFirst
    /// 동시 발생 시 OFF
    | SimultaneousOff

/// Relay 타입: Set/Reset 논리 (RuntimeSpec.md 기반)
type Relay = {
    /// 릴레이 태그
    Tag: DsTag
    /// SET 조건
    Set: DsExpr
    /// RESET 조건
    Reset: DsExpr
    /// 동작 모드 (기본: SR)
    Mode: RelayMode
    /// 우선순위 규칙 (기본: ResetFirst)
    Priority: RelayPriority
    /// 초기값 (기본: false)
    DefaultValue: bool
} with
    /// 기본 Relay 생성 (SR 모드, RST 우선)
    static member Create(tag, set', reset) =
        { Tag = tag
          Set = set'
          Reset = reset
          Mode = RelayMode.SR
          Priority = RelayPriority.ResetFirst
          DefaultValue = false }

    /// 모드 지정 Relay 생성
    static member CreateWithMode(tag, set', reset, mode) =
        { Tag = tag
          Set = set'
          Reset = reset
          Mode = mode
          Priority = RelayPriority.ResetFirst
          DefaultValue = false }

    /// 전체 옵션 지정 Relay 생성
    static member CreateFull(tag, set', reset, mode, priority, defaultVal) =
        { Tag = tag
          Set = set'
          Reset = reset
          Mode = mode
          Priority = priority
          DefaultValue = defaultVal }

    /// SR 래치 동작: (SET || self) && !RESET
    member r.ToLatch() =
        let self = Terminal(r.Tag)
        match r.Priority with
        | RelayPriority.ResetFirst ->
            // RST 우선: !RESET && (SET || self)
            (!!. r.Reset) &&. (r.Set ||. self)
        | RelayPriority.SetFirst ->
            // SET 우선: SET || (self && !RESET)
            r.Set ||. (self &&. (!!. r.Reset))
        | RelayPriority.SimultaneousOff ->
            // 동시 발생 시 OFF: (SET && !RESET) || (self && !SET && !RESET)
            (r.Set &&. (!!. r.Reset)) ||. (self &&. (!!. r.Set) &&. (!!. r.Reset))

    /// 조건부 동작: SET && !RESET
    member r.ToExpr() =
        match r.Priority with
        | RelayPriority.ResetFirst ->
            // RST 우선: SET && !RESET
            r.Set &&. (!!. r.Reset)
        | RelayPriority.SetFirst ->
            // SET 우선: SET || !RESET (SET이 있으면 무조건 ON)
            r.Set ||. (!!. r.Reset)
        | RelayPriority.SimultaneousOff ->
            // 동시 발생 시 OFF: SET && !RESET
            r.Set &&. (!!. r.Reset)

    /// 펄스 동작: Rising edge 감지 후 1스캔만 유지
    member r.ToPulse() =
        Unary(DsOp.Rising, r.Set)

    /// 모드에 따른 표현식 생성
    member r.ToModeExpr() =
        match r.Mode with
        | RelayMode.SR -> r.ToLatch()
        | RelayMode.Pulse -> r.ToPulse()
        | RelayMode.OneShot -> r.ToPulse()
        | RelayMode.Conditional -> r.ToExpr()

/// 시스템 상태 코드
type SystemState =
    | Idle = 0
    | Ready = 1
    | Running = 2
    | Paused = 3
    | Error = 4
    | Emergency = 5
    | Maintenance = 6

/// 에러 코드
type ErrorCode =
    | NoError = 0
    | TimeoutError = 1
    | CommunicationError = 2
    | HardwareError = 3
    | SoftwareError = 4
    | OperatorError = 5
    | SafetyError = 6

/// 코드 생성 결과
type GenerationResult =
    | Success of DsStmt list
    | Failure of string

/// 시스템 코드
type SystemCode = {
    Name: string
    Relays: Relay list
}

// ═════════════════════════════════════════════════════════════════════
// UserFB Types - PLC Function Block & Function
// ═════════════════════════════════════════════════════════════════════

/// 파라미터 방향 (PLC 표준)
[<RequireQualifiedAccess>]
type ParamDirection =
    /// 입력 파라미터 (읽기 전용)
    | Input
    /// 출력 파라미터 (쓰기 전용)
    | Output
    /// 입출력 파라미터 (읽기/쓰기)
    | InOut

/// 함수 파라미터 정의
type FunctionParam = {
    /// 파라미터 이름
    Name: string
    /// 데이터 타입
    DataType: Type
    /// 파라미터 방향
    Direction: ParamDirection
    /// 기본값 (옵션)
    DefaultValue: obj option
    /// 설명
    Description: string option
    /// 파라미터가 optional인지 (defaultValue가 있으면 optional)
    IsOptional: bool
} with
    static member Create(name, dataType, direction, ?defaultValue, ?description) =
        { Name = name
          DataType = dataType
          Direction = direction
          DefaultValue = defaultValue
          Description = description
          IsOptional = Option.isSome defaultValue }

    member this.Validate() : Result<unit, string> =
        if String.IsNullOrWhiteSpace(this.Name) then
            Error "Parameter name cannot be empty"
        elif this.Name.ToUpper() = "IF" || this.Name.ToUpper() = "THEN" || this.Name.ToUpper() = "ELSE" then
            Error (sprintf "Parameter name '%s' is a reserved keyword" this.Name)
        elif this.DefaultValue.IsSome then
            try
                TypeHelpers.validateType this.DataType this.DefaultValue.Value |> ignore
                Ok ()
            with ex ->
                Error (sprintf "Default value type mismatch for parameter '%s': %s" this.Name ex.Message)
        else
            Ok ()

/// UserFC 메타데이터
type UserFCMetadata = {
    Author: string option
    Version: string option
    Description: string option
    CreatedDate: System.DateTime
    ModifiedDate: System.DateTime
    Tags: string list
    Dependencies: string list  // 의존하는 다른 UserFC 목록
} with
    static member Empty = {
        Author = None
        Version = None
        Description = None
        CreatedDate = System.DateTime.UtcNow
        ModifiedDate = System.DateTime.UtcNow
        Tags = []
        Dependencies = []
    }

/// FC (Function) - 상태 없는 순수 함수
/// 입력만 받아 출력을 계산하며, 내부 상태를 유지하지 않음
/// 예: 수학 연산, 단위 변환, 데이터 변환 등
type UserFC = {
    /// 함수 이름
    Name: string
    /// 입력 파라미터
    Inputs: FunctionParam list
    /// 출력 (단일 반환값 또는 여러 출력)
    Outputs: FunctionParam list
    /// 함수 본문 (수식)
    Body: DsExpr
    /// 메타데이터
    Metadata: UserFCMetadata
} with
    /// FC 생성 (기본)
    static member Create(name, inputs, outputs, body, ?metadata) =
        { Name = name
          Inputs = inputs
          Outputs = outputs
          Body = body
          Metadata = defaultArg metadata UserFCMetadata.Empty }

    /// 반환 타입 (첫 번째 출력 파라미터의 타입)
    member this.ReturnType : Type =
        match this.Outputs with
        | { DataType = dataType } :: _ -> dataType
        | [] ->
            invalidOp (sprintf "FC '%s' does not define any output parameters" this.Name)

    /// FC 검증
    member this.Validate() : Result<unit, string> =
        if String.IsNullOrWhiteSpace(this.Name) then
            Error "FC name cannot be empty"
        elif List.isEmpty this.Outputs then
            Error (sprintf "FC '%s' must have at least one output parameter" this.Name)
        elif this.Outputs.Length > 1 then
            Error (sprintf "FC '%s' can have at most 1 output parameter" this.Name)
        else
            let paramResults =
                (this.Inputs @ this.Outputs)
                |> List.map (fun p -> p.Validate())
                |> List.tryFind (function | Error _ -> true | _ -> false)
            match paramResults with
            | Some (Error e) -> Error e
            | _ ->
                let allNames = (this.Inputs @ this.Outputs) |> List.map (fun p -> p.Name)
                let duplicates = allNames |> List.groupBy id |> List.filter (fun (_, g) -> List.length g > 1) |> List.map fst
                if not (List.isEmpty duplicates) then
                    Error (sprintf "FC '%s' has duplicate parameter names: %s" this.Name (String.concat ", " duplicates))
                else
                    Ok ()

    /// FC 시그니처 (디버깅/로깅용)
    member this.Signature : string =
        let inputsStr =
            this.Inputs
            |> List.map (fun p -> sprintf "%s: %O" p.Name p.DataType)
            |> String.concat ", "
        let outputStr = sprintf " : %O" this.ReturnType
        sprintf "%s(%s)%s" this.Name inputsStr outputStr

/// FB (Function Block) - 상태를 가진 함수 블록
/// 내부 상태(Static 변수)를 유지하며, 여러 출력과 내부 로직을 가짐
/// 예: TON, CTU, PID, 모터 제어, 시퀀스 제어 등
type UserFB = {
    /// 블록 이름
    Name: string
    /// 입력 파라미터
    Inputs: FunctionParam list
    /// 출력 파라미터
    Outputs: FunctionParam list
    /// InOut 파라미터
    InOuts: FunctionParam list
    /// 내부 상태 변수 (Static)
    Statics: (string * Type * obj option) list
    /// 임시 변수 (Temp)
    Temps: (string * Type) list
    /// 블록 본문 (명령문 리스트)
    Body: DsStmt list
    /// 메타데이터
    Metadata: UserFCMetadata
} with
    /// FB 생성 (기본)
    static member Create(name, inputs, outputs, inouts, statics, temps, body, ?metadata) =
        { Name = name
          Inputs = inputs
          Outputs = outputs
          InOuts = inouts
          Statics = statics
          Temps = temps
          Body = body
          Metadata = defaultArg metadata UserFCMetadata.Empty }

    /// FB 검증
    member this.Validate() : Result<unit, string> =
        if String.IsNullOrWhiteSpace(this.Name) then
            Error "FB name cannot be empty"
        else
            let paramResults =
                (this.Inputs @ this.Outputs @ this.InOuts)
                |> List.map (fun p -> p.Validate())
                |> List.tryFind (function | Error _ -> true | _ -> false)
            match paramResults with
            | Some (Error e) -> Error e
            | _ ->
                let paramNames = (this.Inputs @ this.Outputs @ this.InOuts) |> List.map (fun p -> p.Name)
                let staticNames = this.Statics |> List.map (fun (n, _, _) -> n)
                let tempNames = this.Temps |> List.map (fun (n, _) -> n)
                let allNames = paramNames @ staticNames @ tempNames
                let duplicates = allNames |> List.groupBy id |> List.filter (fun (_, g) -> List.length g > 1) |> List.map fst
                if not (List.isEmpty duplicates) then
                    Error (sprintf "FB '%s' has duplicate variable names: %s" this.Name (String.concat ", " duplicates))
                else
                    let staticValidation =
                        this.Statics
                        |> List.tryPick (fun (name, dt, initVal) ->
                            match initVal with
                            | Some v ->
                                try
                                    TypeHelpers.validateType dt v |> ignore
                                    None
                                with ex ->
                                    Some (Error (sprintf "Static variable '%s' initial value type mismatch: %s" name ex.Message))
                            | None -> None)
                    match staticValidation with
                    | Some err -> err
                    | None ->
                        if List.isEmpty this.Body then
                            Error (sprintf "FB '%s' must contain at least one statement" this.Name)
                        else
                            Ok ()

    /// FB 시그니처 (디버깅/로깅용)
    member this.Signature : string =
        let inputsStr =
            this.Inputs
            |> List.map (fun p -> sprintf "%s: %O" p.Name p.DataType)
            |> String.concat ", "
        let outputsStr =
            this.Outputs
            |> List.map (fun p -> sprintf "%s: %O" p.Name p.DataType)
            |> String.concat ", "
        let staticsStr =
            if this.Statics.IsEmpty then ""
            else sprintf " [Statics: %d]" this.Statics.Length
        sprintf "FB_%s(IN: %s; OUT: %s)%s" this.Name inputsStr outputsStr staticsStr

/// UserFB 인스턴스
/// FB를 실제로 사용하기 위한 인스턴스
type FBInstance = {
    /// 인스턴스 이름
    Name: string
    /// FB 타입 (참조)
    FBType: UserFB
    /// 인스턴스별 Static 변수 저장소 (이름 -> 값)
    /// 실제 실행 시 이 저장소에 상태가 유지됨
    StateStorage: Map<string, obj> option
} with
    static member Create(name, fbType) =
        { Name = name
          FBType = fbType
          StateStorage = None }

    /// 인스턴스 초기 상태 생성
    member this.InitializeState() : Map<string, obj> =
        this.FBType.Statics
        |> List.map (fun (name, dt, initVal) ->
            let value =
                match initVal with
                | Some v -> v
                | None ->
                    if dt = typeof<bool> then box false
                    elif dt = typeof<int> then box 0
                    elif dt = typeof<double> then box 0.0
                    elif dt = typeof<string> then box ""
                    else box null
            (name, value))
        |> Map.ofList

    /// 상태가 초기화되었는지 확인
    member this.IsInitialized : bool =
        this.StateStorage.IsSome

/// UserFC 호출
type FCCall = {
    /// FC 타입 (참조)
    FCType: UserFC
    /// 입력 인자
    Arguments: DsExpr list
}

/// UserFB 호출
type FBCall = {
    /// FB 인스턴스 (참조)
    Instance: FBInstance
    /// 입력 인자
    Arguments: DsExpr list
}
