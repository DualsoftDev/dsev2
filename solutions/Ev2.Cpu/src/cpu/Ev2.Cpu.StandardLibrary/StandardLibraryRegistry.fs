namespace Ev2.Cpu.StandardLibrary

open Ev2.Cpu.Core
open Ev2.Cpu.Core.UserDefined
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.StandardLibrary.EdgeDetection
open Ev2.Cpu.StandardLibrary.Bistable
open Ev2.Cpu.StandardLibrary.Timers
open Ev2.Cpu.StandardLibrary.Counters
open Ev2.Cpu.StandardLibrary.Analog
open Ev2.Cpu.StandardLibrary.Math
open Ev2.Cpu.StandardLibrary.String

// 타입 alias (Generation.Core 타입 사용)
type StandardUserFC = Ev2.Cpu.Generation.Core.UserFC
type StandardUserFB = Ev2.Cpu.Generation.Core.UserFB

/// FC 또는 FB를 나타내는 공용 타입
type StandardFunctionOrBlock =
    | FC of StandardUserFC
    | FB of StandardUserFB

/// <summary>
/// IEC 61131-3 표준 Function Block 라이브러리 레지스트리
/// </summary>
/// <remarks>
/// 모든 표준 FB를 UserLibrary에 자동으로 등록합니다.
/// </remarks>
module StandardLibraryRegistry =

    /// 표준 라이브러리 버전
    let [<Literal>] Version = "1.0.0"

    /// 지원되는 모든 표준 FB 목록
    type StandardFunctionBlock =
        // Edge Detection
        | R_TRIG
        | F_TRIG
        // Bistable
        | SR
        | RS
        // Timers
        | TON
        | TOF
        | TP
        | TONR
        // Counters
        | CTU
        | CTD
        | CTUD
        // Analog Processing
        | SCALE
        | LIMIT
        | HYSTERESIS
        // Math Functions
        | AVERAGE
        | MIN
        | MAX
        // String Manipulation
        | CONCAT
        | LEFT
        | RIGHT
        | MID
        | FIND

    /// 표준 FB 이름 리스트
    let getAllStandardFBNames() : string list =
        [
            "R_TRIG"; "F_TRIG"
            "SR"; "RS"
            "TON"; "TOF"; "TP"; "TONR"
            "CTU"; "CTD"; "CTUD"
            "SCALE"; "LIMIT"; "HYSTERESIS"
            "AVERAGE"; "MIN"; "MAX"
            "CONCAT"; "LEFT"; "RIGHT"; "MID"; "FIND"
        ]

    /// 개별 FB 생성
    let createStandardFB (fbType: StandardFunctionBlock) : Result<StandardFunctionOrBlock, string> =
        match fbType with
        // Edge Detection (FB)
        | R_TRIG -> R_TRIG.create() |> Result.map FB
        | F_TRIG -> F_TRIG.create() |> Result.map FB
        // Bistable (FB)
        | SR -> SR.create() |> Result.map FB
        | RS -> RS.create() |> Result.map FB
        // Timers (FB)
        | TON -> TON.create() |> Result.map FB
        | TOF -> TOF.create() |> Result.map FB
        | TP -> TP.create() |> Result.map FB
        | TONR -> TONR.create() |> Result.map FB
        // Counters (FB)
        | CTU -> CTU.create() |> Result.map FB
        | CTD -> CTD.create() |> Result.map FB
        | CTUD -> CTUD.create() |> Result.map FB
        // Analog Processing
        | SCALE -> SCALE.create() |> Result.map FC     // FC
        | LIMIT -> LIMIT.create() |> Result.map FC     // FC
        | HYSTERESIS -> HYSTERESIS.create() |> Result.map FB  // FB (has state)
        // Math Functions (FC)
        | AVERAGE -> AVERAGE.create() |> Result.map FC
        | MIN -> MIN.create() |> Result.map FC
        | MAX -> MAX.create() |> Result.map FC
        // String Manipulation (FC)
        | CONCAT -> CONCAT.create() |> Result.map FC
        | LEFT -> LEFT.create() |> Result.map FC
        | RIGHT -> RIGHT.create() |> Result.map FC
        | MID -> MID.create() |> Result.map FC
        | FIND -> FIND.create() |> Result.map FC

    /// Helper: Clear registry and create FB
    let createWithClearRegistry fbType =
        DsTagRegistry.clear()
        createStandardFB fbType

    /// 모든 표준 FB 생성
    let createAllStandardFBs() : Map<string, Result<StandardFunctionOrBlock, string>> =
        [
            "R_TRIG", createWithClearRegistry R_TRIG
            "F_TRIG", createWithClearRegistry F_TRIG
            "SR", createWithClearRegistry SR
            "RS", createWithClearRegistry RS
            "TON", createWithClearRegistry TON
            "TOF", createWithClearRegistry TOF
            "TP", createWithClearRegistry TP
            "TONR", createWithClearRegistry TONR
            "CTU", createWithClearRegistry CTU
            "CTD", createWithClearRegistry CTD
            "CTUD", createWithClearRegistry CTUD
            "SCALE", createWithClearRegistry SCALE
            "LIMIT", createWithClearRegistry LIMIT
            "HYSTERESIS", createWithClearRegistry HYSTERESIS
            "AVERAGE", createWithClearRegistry AVERAGE
            "MIN", createWithClearRegistry MIN
            "MAX", createWithClearRegistry MAX
            "CONCAT", createWithClearRegistry CONCAT
            "LEFT", createWithClearRegistry LEFT
            "RIGHT", createWithClearRegistry RIGHT
            "MID", createWithClearRegistry MID
            "FIND", createWithClearRegistry FIND
        ]
        |> Map.ofList

    /// <summary>
    /// StandardUserFC를 Core UserFC로 변환
    /// </summary>
    let private convertToCoreFC (fc: StandardUserFC) : Result<Ev2.Cpu.Core.UserDefined.UserFC, UserDefinitionError> =
        try
            let coreInputs =
                fc.Inputs
                |> List.map (fun p ->
                    let dir = match p.Direction with
                              | ParamDirection.Input -> Ev2.Cpu.Core.UserDefined.ParamDirection.Input
                              | ParamDirection.Output -> Ev2.Cpu.Core.UserDefined.ParamDirection.Output
                              | ParamDirection.InOut -> Ev2.Cpu.Core.UserDefined.ParamDirection.InOut
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, dir,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))
            let coreOutputs =
                fc.Outputs
                |> List.map (fun p ->
                    let dir = match p.Direction with
                              | ParamDirection.Input -> Ev2.Cpu.Core.UserDefined.ParamDirection.Input
                              | ParamDirection.Output -> Ev2.Cpu.Core.UserDefined.ParamDirection.Output
                              | ParamDirection.InOut -> Ev2.Cpu.Core.UserDefined.ParamDirection.InOut
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, dir,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))

            let coreMetadata : Ev2.Cpu.Core.UserDefined.UserFCMetadata =
                { Author = fc.Metadata.Author
                  Version = fc.Metadata.Version
                  Description = fc.Metadata.Description
                  CreatedDate = fc.Metadata.CreatedDate
                  ModifiedDate = fc.Metadata.ModifiedDate
                  Tags = fc.Metadata.Tags
                  Dependencies = fc.Metadata.Dependencies }

            // Body를 UserExpr로 변환
            match Ev2.Cpu.Core.UserDefined.UserExprConverter.dsExprToUserExpr None fc.Body with
            | Some userBody ->
                let coreFC : Ev2.Cpu.Core.UserDefined.UserFC =
                    { Name = fc.Name
                      Inputs = coreInputs
                      Outputs = coreOutputs
                      Body = userBody
                      Metadata = coreMetadata }
                Ok coreFC
            | None ->
                Error (UserDefinitionError.create "StandardLibrary.FCBodyConversion" "Failed to convert FC body" ["Body"])
        with
        | ex ->
            Error (UserDefinitionError.create "StandardLibrary.FCException" ex.Message ["Conversion"])

    /// <summary>
    /// StandardUserFB를 Core UserFB로 변환
    /// </summary>
    let private convertToCoreFB (fb: StandardUserFB) : Result<Ev2.Cpu.Core.UserDefined.UserFB, UserDefinitionError> =
        // Generation.Core.UserFB와 Core.UserDefined.UserFB는 구조가 동일하므로
        // 필드만 복사하면 됨
        try
            let coreInputs =
                fb.Inputs
                |> List.map (fun p ->
                    let dir = match p.Direction with
                              | ParamDirection.Input -> Ev2.Cpu.Core.UserDefined.ParamDirection.Input
                              | ParamDirection.Output -> Ev2.Cpu.Core.UserDefined.ParamDirection.Output
                              | ParamDirection.InOut -> Ev2.Cpu.Core.UserDefined.ParamDirection.InOut
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, dir,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))
            let coreOutputs =
                fb.Outputs
                |> List.map (fun p ->
                    let dir = match p.Direction with
                              | ParamDirection.Input -> Ev2.Cpu.Core.UserDefined.ParamDirection.Input
                              | ParamDirection.Output -> Ev2.Cpu.Core.UserDefined.ParamDirection.Output
                              | ParamDirection.InOut -> Ev2.Cpu.Core.UserDefined.ParamDirection.InOut
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, dir,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))
            let coreInOuts =
                fb.InOuts
                |> List.map (fun p ->
                    let dir = match p.Direction with
                              | ParamDirection.Input -> Ev2.Cpu.Core.UserDefined.ParamDirection.Input
                              | ParamDirection.Output -> Ev2.Cpu.Core.UserDefined.ParamDirection.Output
                              | ParamDirection.InOut -> Ev2.Cpu.Core.UserDefined.ParamDirection.InOut
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, dir,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))

            let coreMetadata : Ev2.Cpu.Core.UserDefined.UserFCMetadata =
                { Author = fb.Metadata.Author
                  Version = fb.Metadata.Version
                  Description = fb.Metadata.Description
                  CreatedDate = fb.Metadata.CreatedDate
                  ModifiedDate = fb.Metadata.ModifiedDate
                  Tags = fb.Metadata.Tags
                  Dependencies = fb.Metadata.Dependencies }

            // Body를 UserStmt로 변환
            let userBodyResult =
                fb.Body
                |> List.fold (fun acc stmt ->
                    match acc with
                    | Error msg -> Error msg
                    | Ok accStmts ->
                        match Ev2.Cpu.Core.UserDefined.UserStmtConverter.dsStmtToUserStmt None stmt with
                        | Some userStmt -> Ok (userStmt :: accStmts)
                        | None -> Error "Failed to convert statement") (Ok [])

            match userBodyResult with
            | Error msg ->
                Error (UserDefinitionError.create "StandardLibrary.BodyConversion" msg ["Body"])
            | Ok reversedUserBody ->
                let coreFb : Ev2.Cpu.Core.UserDefined.UserFB =
                    { Name = fb.Name
                      Inputs = coreInputs
                      Outputs = coreOutputs
                      InOuts = coreInOuts
                      Statics = fb.Statics
                      Temps = fb.Temps
                      Body = List.rev reversedUserBody
                      Metadata = coreMetadata }
                Ok coreFb
        with
        | ex ->
            Error (UserDefinitionError.create "StandardLibrary.Exception" ex.Message ["Conversion"])

    /// <summary>
    /// UserLibrary에 모든 표준 FB 등록
    /// </summary>
    /// <param name="library">등록할 UserLibrary 인스턴스</param>
    /// <returns>성공/실패 결과 리스트</returns>
    let registerAllTo (library: Ev2.Cpu.Core.UserDefined.UserLibrary) : (string * Result<unit, UserDefinitionError>) list =
        let allFBs = createAllStandardFBs()

        allFBs
        |> Map.toList
        |> List.map (fun (name, result) ->
            match result with
            | Ok (FC fc) ->
                match convertToCoreFC fc with
                | Ok coreFC ->
                    let registerResult : Result<unit, UserDefinitionError> = library.RegisterFC(coreFC)
                    (name, registerResult)
                | Error err ->
                    (name, Error err)
            | Ok (FB fb) ->
                match convertToCoreFB fb with
                | Ok coreFB ->
                    let registerResult : Result<unit, UserDefinitionError> = library.RegisterFB(coreFB)
                    (name, registerResult)
                | Error err ->
                    (name, Error err)
            | Error msg ->
                let error = UserDefinitionError.create
                                "StandardLibrary.CreateFailed"
                                msg
                                ["StandardLibrary"; name]
                (name, Error error))

    /// <summary>
    /// 전역 UserLibrary에 표준 FB 등록
    /// </summary>
    /// <returns>성공/실패 결과 리스트</returns>
    let registerToGlobal() : (string * Result<unit, UserDefinitionError>) list =
        let library = Ev2.Cpu.Core.UserDefined.GlobalUserLibrary.getInstance()
        registerAllTo library

    /// <summary>
    /// 표준 라이브러리 초기화 (전역 등록)
    /// </summary>
    /// <returns>등록 성공 개수와 실패 개수</returns>
    let initialize() : (int * int) =
        let results = registerToGlobal()
        let successCount = results |> List.filter (fun (_, r) -> match r with Ok _ -> true | _ -> false) |> List.length
        let failureCount = results |> List.filter (fun (_, r) -> match r with Error _ -> true | _ -> false) |> List.length
        (successCount, failureCount)

    /// <summary>
    /// 표준 라이브러리 정보 출력
    /// </summary>
    let printLibraryInfo() : string =
        let fbNames = getAllStandardFBNames()
        sprintf """
╔══════════════════════════════════════════════════════════════════╗
║          IEC 61131-3 Standard Function Block Library            ║
║                      Version %s                                ║
╠══════════════════════════════════════════════════════════════════╣
║  Edge Detection:  R_TRIG, F_TRIG                                 ║
║  Bistable:        SR, RS                                         ║
║  Timers:          TON, TOF, TP, TONR                             ║
║  Counters:        CTU, CTD, CTUD                                 ║
║  Analog:          SCALE, LIMIT, HYSTERESIS                       ║
║  Math:            AVERAGE, MIN, MAX                              ║
║  String:          CONCAT, LEFT, RIGHT, MID, FIND                 ║
╠══════════════════════════════════════════════════════════════════╣
║  Total: %2d Function Blocks                                       ║
╚══════════════════════════════════════════════════════════════════╝
"""
            Version
            fbNames.Length

    /// <summary>
    /// 표준 FB 검증 (모든 FB가 올바르게 빌드되는지 확인)
    /// </summary>
    /// <returns>검증 결과</returns>
    let validateAll() : (string * Result<unit, string>) list =
        createAllStandardFBs()
        |> Map.toList
        |> List.map (fun (name, result) ->
            match result with
            | Ok (FC fc) ->
                match fc.Validate() with
                | Ok () -> (name, Ok ())
                | Error msg -> (name, Error msg)
            | Ok (FB fb) ->
                match fb.Validate() with
                | Ok () -> (name, Ok ())
                | Error msg -> (name, Error msg)
            | Error msg ->
                (name, Error msg))

    /// <summary>
    /// 표준 라이브러리 통계
    /// </summary>
    type LibraryStatistics = {
        TotalFBs: int
        EdgeDetection: int
        Bistable: int
        Timers: int
        Counters: int
        Analog: int
        Math: int
        String: int
        Version: string
    }

    /// 통계 조회
    let getStatistics() : LibraryStatistics =
        {
            TotalFBs = 22
            EdgeDetection = 2  // R_TRIG, F_TRIG
            Bistable = 2       // SR, RS
            Timers = 4         // TON, TOF, TP, TONR
            Counters = 3       // CTU, CTD, CTUD
            Analog = 3         // SCALE, LIMIT, HYSTERESIS
            Math = 3           // AVERAGE, MIN, MAX
            String = 5         // CONCAT, LEFT, RIGHT, MID, FIND
            Version = Version
        }

    /// <summary>
    /// 표준 FB 사용 예제 가져오기
    /// </summary>
    /// <param name="fbName">FB 이름</param>
    /// <returns>사용 예제 코드</returns>
    let getUsageExample (fbName: string) : string option =
        match fbName with
        | "R_TRIG" -> Some """
// Rising edge detection example
let builder = FBBuilder("MyProgram")
let rtrig = R_TRIG.create() |> Result.get

builder.AddInput("Sensor", typeof<bool>)
// Use R_TRIG to detect sensor activation
"""
        | "TON" -> Some """
// On-Delay Timer example
let builder = FBBuilder("DelayControl")
let ton = TON.create() |> Result.get

// Start motor after 3 second delay
builder.AddInput("StartButton", typeof<bool>)
builder.AddOutput("MotorOn", typeof<bool>)
// TON with PT=3000 (3 seconds)
"""
        | "CTU" -> Some """
// Count Up example
let builder = FBBuilder("ProductCounter")
let ctu = CTU.create() |> Result.get

// Count products up to 100
builder.AddInput("ProductSensor", typeof<bool>)
// CTU with PV=100
"""
        | _ -> None
