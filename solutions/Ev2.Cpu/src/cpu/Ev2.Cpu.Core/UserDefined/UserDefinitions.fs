
namespace Ev2.Cpu.Core.UserDefined

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// User-Defined Function / Function Block Definitions
// ═════════════════════════════════════════════════════════════════════════════
// Generation 및 Runtime 계층이 공유할 수 있도록 Core에 공통 도메인 모델과
// 검증 로직, 오류 타입을 정의한다.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>파라미터 방향 (입력, 출력, 입출력)</summary>
/// <remarks>
/// IEC 61131-3 표준의 VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT에 대응됩니다.
/// </remarks>
[<RequireQualifiedAccess>]
type ParamDirection =
    /// <summary>입력 파라미터 (VAR_INPUT)</summary>
    | Input
    /// <summary>출력 파라미터 (VAR_OUTPUT)</summary>
    | Output
    /// <summary>입출력 파라미터 (VAR_IN_OUT)</summary>
    | InOut

/// <summary>사용자 정의 엔터티 검증 오류</summary>
/// <remarks>
/// 구조화된 에러 정보를 제공합니다:
/// - Code: 에러 코드 (예: "FC.NameInvalid")
/// - Message: 사람이 읽을 수 있는 메시지
/// - Path: 계층적 경로 (예: ["FC", "MyFunction", "param", "IN"])
/// </remarks>
[<StructuralEquality; NoComparison>]
type UserDefinitionError = {
    /// <summary>에러 코드 (예: "FC.NameInvalid")</summary>
    Code: string
    /// <summary>에러 메시지</summary>
    Message: string
    /// <summary>에러 발생 경로 (계층적)</summary>
    Path: string list
} with
    /// <summary>에러를 포맷된 문자열로 변환합니다.</summary>
    /// <returns>경로와 메시지가 결합된 문자열</returns>
    member this.Format() =
        match this.Path with
        | [] -> this.Message
        | path -> sprintf "%s: %s" (String.concat "." path) this.Message

/// <summary>UserDefinitionError 생성 및 조작 헬퍼</summary>
module UserDefinitionError =
    /// <summary>에러 생성</summary>
    let create code message path =
        { Code = code; Message = message; Path = path }

    /// <summary>경로 앞에 세그먼트 추가</summary>
    let prepend segment error =
        { error with Path = segment :: error.Path }

    /// <summary>경로 앞에 여러 세그먼트 추가</summary>
    let prependMany segments error =
        { error with Path = segments @ error.Path }

/// 식별자 검증 유틸리티 (내부)
module private IdentifierValidation =
    let private normalizeName (name: string) =
        if String.IsNullOrWhiteSpace name then "<empty>"
        else name

    let reservedKeywords : Set<string> =
        Set.ofList [
            "IF"; "THEN"; "ELSE"; "END_IF"; "ELSIF"
            "FOR"; "TO"; "BY"; "DO"; "END_FOR"
            "WHILE"; "END_WHILE"; "REPEAT"; "UNTIL"; "END_REPEAT"
            "CASE"; "OF"; "END_CASE"
            "VAR"; "END_VAR"; "VAR_INPUT"; "VAR_OUTPUT"; "VAR_IN_OUT"; "VAR_TEMP"
            "FUNCTION"; "END_FUNCTION"; "FUNCTION_BLOCK"; "END_FUNCTION_BLOCK"
            "PROGRAM"; "END_PROGRAM"
            "TRUE"; "FALSE"; "NULL"
            "AND"; "OR"; "NOT"; "XOR"; "MOD"
        ]

    let isValidIdentifier (name: string) =
        if String.IsNullOrWhiteSpace name then false
        else
            let firstChar = name.[0]
            let isFirstValid = Char.IsLetter(firstChar) || firstChar = '_'
            let isRestValid =
                name
                |> Seq.skip 1
                |> Seq.forall (fun c -> Char.IsLetterOrDigit(c) || c = '_')
            isFirstValid && isRestValid

    let isReservedKeyword (name: string) =
        Set.contains (name.ToUpperInvariant()) reservedKeywords

    let validate kind (name: string) : Result<unit, UserDefinitionError> =
        let segment = normalizeName name
        if String.IsNullOrWhiteSpace name then
            UserDefinitionError.create (sprintf "%s.NameEmpty" kind) (sprintf "%s name cannot be empty." kind) ["name"; segment]
            |> Error
        elif not (isValidIdentifier name) then
            UserDefinitionError.create (sprintf "%s.NameInvalid" kind) (sprintf "%s name '%s' is not a valid identifier." kind name) ["name"; segment]
            |> Error
        elif isReservedKeyword name then
            UserDefinitionError.create (sprintf "%s.NameReserved" kind) (sprintf "%s name '%s' is a reserved keyword." kind name) ["name"; segment]
            |> Error
        else
            Ok ()

    let normalize = normalizeName

/// <summary>사용자 정의 함수/블록 파라미터</summary>
/// <remarks>
/// FC/FB의 입력, 출력, 입출력 파라미터를 표현합니다.
/// 기본값이 있는 경우 자동으로 optional로 처리됩니다.
/// </remarks>
[<StructuralEquality; NoComparison>]
type FunctionParam = {
    /// <summary>파라미터 이름</summary>
    Name: string
    /// <summary>파라미터 데이터 타입</summary>
    DataType: DsDataType
    /// <summary>파라미터 방향 (Input/Output/InOut)</summary>
    Direction: ParamDirection
    /// <summary>기본값 (optional)</summary>
    DefaultValue: obj option
    /// <summary>파라미터 설명 (optional)</summary>
    Description: string option
    /// <summary>선택적 파라미터 여부 (기본값이 있으면 true)</summary>
    IsOptional: bool
} with
    /// <summary>FunctionParam 생성 팩토리 메서드</summary>
    /// <param name="name">파라미터 이름</param>
    /// <param name="dataType">데이터 타입</param>
    /// <param name="direction">파라미터 방향</param>
    /// <param name="defaultValue">기본값 (optional)</param>
    /// <param name="description">설명 (optional)</param>
    /// <returns>생성된 FunctionParam</returns>
    static member Create(name, dataType, direction, ?defaultValue, ?description) =
        { Name = name
          DataType = dataType
          Direction = direction
          DefaultValue = defaultValue
          Description = description
          IsOptional = Option.isSome defaultValue }

    /// <summary>파라미터 유효성 검증</summary>
    /// <returns>검증 성공 시 Ok (), 실패 시 Error with UserDefinitionError</returns>
    /// <remarks>
    /// 검증 항목:
    /// - 파라미터 이름이 유효한 식별자인지
    /// - 기본값이 데이터 타입과 일치하는지
    /// </remarks>
    member this.Validate() : Result<unit, UserDefinitionError> =
        let paramName = IdentifierValidation.normalize this.Name
        let basePath = ["param"; paramName]

        let defaultValueCheck () =
            match this.DefaultValue with
            | None -> Ok ()
            | Some value ->
                try
                    this.DataType.Validate(value) |> ignore
                    Ok ()
                with ex ->
                    let message =
                        sprintf "Default value for parameter '%s' must be of type %O: %s"
                            paramName this.DataType ex.Message
                    Error (UserDefinitionError.create "Param.DefaultTypeMismatch" message (basePath @ ["default"]))

        IdentifierValidation.validate "Param" this.Name
        |> Result.mapError (UserDefinitionError.prepend "param")
        |> Result.bind defaultValueCheck

/// <summary>사용자 정의 FC/FB 메타데이터</summary>
/// <remarks>
/// 사용자 정의 함수/블록의 버전 관리 및 문서화를 위한 메타데이터입니다.
/// </remarks>
[<StructuralEquality; NoComparison>]
type UserFCMetadata = {
    /// <summary>작성자 이름</summary>
    Author: string option
    /// <summary>버전 문자열 (예: "1.0.0")</summary>
    Version: string option
    /// <summary>설명</summary>
    Description: string option
    /// <summary>생성 일시 (UTC)</summary>
    CreatedDate: DateTime
    /// <summary>최종 수정 일시 (UTC)</summary>
    ModifiedDate: DateTime
    /// <summary>태그 목록 (분류, 검색용)</summary>
    Tags: string list
    /// <summary>의존성 목록 (다른 FC/FB 이름)</summary>
    Dependencies: string list
} with
    /// <summary>빈 메타데이터 (기본값)</summary>
    /// <remarks>생성/수정 일시는 현재 UTC 시간으로 설정됩니다.</remarks>
    static member Empty =
        { Author = None
          Version = None
          Description = None
          CreatedDate = DateTime.UtcNow
          ModifiedDate = DateTime.UtcNow
          Tags = []
          Dependencies = [] }

/// <summary>사용자 정의 함수 (User Function, 상태 없음)</summary>
/// <remarks>
/// IEC 61131-3의 FUNCTION에 해당합니다.
/// - 입력 파라미터를 받아 단일 출력 값을 반환
/// - 내부 상태를 유지하지 않음 (stateless)
/// - 동일한 입력에 대해 항상 동일한 출력 보장
/// </remarks>
[<StructuralEquality; NoComparison>]
type UserFC = {
    /// <summary>함수 이름</summary>
    Name: string
    /// <summary>입력 파라미터 목록</summary>
    Inputs: FunctionParam list
    /// <summary>출력 파라미터 목록 (정확히 1개여야 함)</summary>
    Outputs: FunctionParam list
    /// <summary>함수 본문 (표현식)</summary>
    Body: UserExpr
    /// <summary>메타데이터 (작성자, 버전 등)</summary>
    Metadata: UserFCMetadata
} with
    /// <summary>UserFC 생성 팩토리 메서드</summary>
    /// <param name="name">함수 이름</param>
    /// <param name="inputs">입력 파라미터 목록</param>
    /// <param name="outputs">출력 파라미터 목록 (1개)</param>
    /// <param name="body">함수 본문</param>
    /// <param name="metadata">메타데이터 (생략 시 Empty)</param>
    /// <returns>생성된 UserFC</returns>
    static member Create(name, inputs, outputs, body, ?metadata) =
        { Name = name
          Inputs = inputs
          Outputs = outputs
          Body = body
          Metadata = defaultArg metadata UserFCMetadata.Empty }

    /// <summary>함수의 반환 타입</summary>
    /// <returns>첫 번째 출력 파라미터의 데이터 타입</returns>
    /// <exception cref="System.InvalidOperationException">출력 파라미터가 없는 경우</exception>
    member this.ReturnType : DsDataType =
        match this.Outputs with
        | { DataType = dataType } :: _ -> dataType
        | [] ->
            invalidOp (sprintf "FC '%s' does not define any output parameters." this.Name)

    member private this.ValidateParameters(prefix: string, parameters: FunctionParam list) =
        parameters
        |> List.tryPick (fun param ->
            match param.Validate() with
            | Ok () -> None
            | Error err ->
                let context = ["parameters"; prefix]
                Some (UserDefinitionError.prependMany context err))
        |> function
            | Some err -> Error err
            | None -> Ok ()

    /// <summary>UserFC 전체 유효성 검증</summary>
    /// <returns>검증 성공 시 Ok (), 실패 시 Error with UserDefinitionError</returns>
    /// <remarks>
    /// 검증 항목:
    /// - 출력 파라미터가 정확히 1개인지
    /// - 모든 파라미터가 유효한지
    /// - 파라미터 이름 중복이 없는지
    /// </remarks>
    member this.Validate() : Result<unit, UserDefinitionError> =
        let fcName = IdentifierValidation.normalize this.Name
        let prependContext error =
            UserDefinitionError.prependMany ["FC"; fcName] error

        let ensureOutputs () =
            if List.isEmpty this.Outputs then
                UserDefinitionError.create "FC.NoOutput"
                    (sprintf "FC '%s' must declare at least one output parameter." fcName)
                    ["outputs"]
                |> Error
            elif this.Outputs.Length > 1 then
                UserDefinitionError.create "FC.MultipleOutputs"
                    (sprintf "FC '%s' can declare at most one output parameter." fcName)
                    ["outputs"]
                |> Error
            else
                Ok ()

        let duplicates =
            (this.Inputs @ this.Outputs)
            |> List.map (fun p -> p.Name)
            |> List.groupBy id
            |> List.choose (fun (name, group) ->
                if String.IsNullOrWhiteSpace name then None
                elif List.length group > 1 then Some name
                else None)

        match ensureOutputs () with
        | Error e -> Error e
        | Ok () ->
            match this.ValidateParameters("inputs", this.Inputs) with
            | Error e -> Error e
            | Ok () ->
                match this.ValidateParameters("outputs", this.Outputs) with
                | Error e -> Error e
                | Ok () ->
                    if not duplicates.IsEmpty then
                        let message =
                            sprintf "Duplicate parameter names detected: %s"
                                (String.concat ", " duplicates)
                        UserDefinitionError.create "FC.DuplicateParameters"
                            message ["parameters"]
                        |> Error
                    else
                        Ok ()
        |> Result.mapError prependContext

    /// <summary>함수 시그니처 문자열</summary>
    /// <returns>함수 시그니처 (예: "MyFunc(a: Int, b: Double) : Bool")</returns>
    /// <remarks>디버깅 및 로깅에 유용합니다.</remarks>
    member this.Signature : string =
        let inputsStr =
            this.Inputs
            |> List.map (fun p -> sprintf "%s: %O" p.Name p.DataType)
            |> String.concat ", "
        let outputStr =
            if List.isEmpty this.Outputs then ""
            else sprintf " : %O" this.Outputs.Head.DataType
        sprintf "%s(%s)%s" this.Name inputsStr outputStr

/// <summary>사용자 정의 함수 블록 (User Function Block, 상태 보유)</summary>
/// <remarks>
/// IEC 61131-3의 FUNCTION_BLOCK에 해당합니다.
/// - 입력, 출력, 입출력 파라미터 지원
/// - 내부 상태 변수 보유 (Statics)
/// - 임시 변수 지원 (Temps)
/// - 여러 문장으로 구성된 본문 실행
/// </remarks>
[<StructuralEquality; NoComparison>]
type UserFB = {
    /// <summary>함수 블록 이름</summary>
    Name: string
    /// <summary>입력 파라미터 목록</summary>
    Inputs: FunctionParam list
    /// <summary>출력 파라미터 목록</summary>
    Outputs: FunctionParam list
    /// <summary>입출력 파라미터 목록</summary>
    InOuts: FunctionParam list
    /// <summary>정적 변수 목록 (이름, 타입, 초기값)</summary>
    /// <remarks>호출 간 상태 유지</remarks>
    Statics: (string * DsDataType * obj option) list
    /// <summary>임시 변수 목록 (이름, 타입)</summary>
    /// <remarks>실행 중에만 사용되는 지역 변수</remarks>
    Temps: (string * DsDataType) list
    /// <summary>함수 블록 본문 (문장 목록)</summary>
    Body: UserStmt list
    /// <summary>메타데이터 (작성자, 버전 등)</summary>
    Metadata: UserFCMetadata
} with
    /// <summary>UserFB 생성 팩토리 메서드</summary>
    /// <param name="name">함수 블록 이름</param>
    /// <param name="inputs">입력 파라미터 목록</param>
    /// <param name="outputs">출력 파라미터 목록</param>
    /// <param name="inouts">입출력 파라미터 목록</param>
    /// <param name="statics">정적 변수 목록</param>
    /// <param name="temps">임시 변수 목록</param>
    /// <param name="body">본문 (문장 목록)</param>
    /// <param name="metadata">메타데이터 (생략 시 Empty)</param>
    /// <returns>생성된 UserFB</returns>
    static member Create(name, inputs, outputs, inouts, statics, temps, body, ?metadata) =
        { Name = name
          Inputs = inputs
          Outputs = outputs
          InOuts = inouts
          Statics = statics
          Temps = temps
          Body = body
          Metadata = defaultArg metadata UserFCMetadata.Empty }

    member private this.ValidateParameters(prefix: string, parameters: FunctionParam list) =
        parameters
        |> List.tryPick (fun param ->
            match param.Validate() with
            | Ok () -> None
            | Error err ->
                let context = ["parameters"; prefix]
                Some (UserDefinitionError.prependMany context err))
        |> function
            | Some err -> Error err
            | None -> Ok ()

    member private this.ValidateStatics() =
        let fbName = IdentifierValidation.normalize this.Name
        this.Statics
        |> List.tryPick (fun (name, dataType, initValue) ->
            let staticName = IdentifierValidation.normalize name
            if String.IsNullOrWhiteSpace name then
                UserDefinitionError.create "FB.Static.NameEmpty"
                    "Static variable name cannot be empty."
                    ["statics"; staticName]
                |> Some
            else
                try
                    match initValue with
                    | Some v -> dataType.Validate(v) |> ignore; None
                    | None -> None
                with ex ->
                    let message =
                        sprintf "Static '%s' initial value must be of type %O: %s"
                            name dataType ex.Message
                    UserDefinitionError.create "FB.Static.InitTypeMismatch"
                        message ["statics"; staticName]
                    |> Some)
        |> function
            | Some err -> Error err
            | None -> Ok ()

    /// <summary>UserFB 전체 유효성 검증</summary>
    /// <returns>검증 성공 시 Ok (), 실패 시 Error with UserDefinitionError</returns>
    /// <remarks>
    /// 검증 항목:
    /// - 모든 파라미터가 유효한지
    /// - 모든 정적 변수가 유효한지
    /// - 본문이 비어있지 않은지
    /// - 변수 이름 중복이 없는지 (inputs, outputs, inouts, statics, temps 전체)
    /// </remarks>
    member this.Validate() : Result<unit, UserDefinitionError> =
        let fbName = IdentifierValidation.normalize this.Name
        let prependContext error =
            UserDefinitionError.prependMany ["FB"; fbName] error

        match this.ValidateParameters("inputs", this.Inputs) with
        | Error e -> Error e
        | Ok () ->
            match this.ValidateParameters("outputs", this.Outputs) with
            | Error e -> Error e
            | Ok () ->
                match this.ValidateParameters("inouts", this.InOuts) with
                | Error e -> Error e
                | Ok () ->
                    match this.ValidateStatics() with
                    | Error e -> Error e
                    | Ok () ->
                        if List.isEmpty this.Body then
                            UserDefinitionError.create "FB.BodyEmpty"
                                (sprintf "FB '%s' must contain at least one statement." fbName)
                                ["body"]
                            |> Error
                        else
                            let allNames =
                                [
                                    yield! this.Inputs |> List.map (fun p -> ("inputs", p.Name))
                                    yield! this.Outputs |> List.map (fun p -> ("outputs", p.Name))
                                    yield! this.InOuts |> List.map (fun p -> ("inouts", p.Name))
                                    yield! this.Statics |> List.map (fun (n, _, _) -> ("statics", n))
                                    yield! this.Temps |> List.map (fun (n, _) -> ("temps", n))
                                ]

                            let duplicates =
                                allNames
                                |> List.groupBy snd
                                |> List.choose (fun (name, group) ->
                                    if String.IsNullOrWhiteSpace name then None
                                    elif List.length group > 1 then Some(group |> List.map fst |> Set.ofList, name)
                                    else None)

                            match duplicates with
                            | [] -> Ok ()
                            | conflicts ->
                                let conflictText =
                                    conflicts
                                    |> List.map (fun (categories, name) ->
                                        sprintf "'%s' (%s)" name (String.concat ", " (categories |> Seq.toList)))
                                    |> String.concat "; "
                                UserDefinitionError.create "FB.DuplicateNames"
                                    (sprintf "Duplicate variable names detected: %s" conflictText)
                                    ["names"]
                                |> Error
        |> Result.mapError prependContext

/// <summary>사용자 정의 FB 인스턴스</summary>
/// <remarks>
/// FB는 타입이고, FBInstance는 해당 타입의 실제 인스턴스입니다.
/// 각 인스턴스는 독립적인 상태를 유지합니다.
/// </remarks>
[<StructuralEquality; NoComparison>]
type FBInstance = {
    /// <summary>인스턴스 이름</summary>
    Name: string
    /// <summary>FB 타입 (정의)</summary>
    FBType: UserFB
    /// <summary>상태 저장소 (초기화 전에는 None)</summary>
    StateStorage: Map<string, obj> option
} with
    /// <summary>FBInstance 생성 팩토리 메서드</summary>
    /// <param name="name">인스턴스 이름</param>
    /// <param name="fbType">FB 타입</param>
    /// <returns>생성된 FBInstance (상태는 미초기화)</returns>
    static member Create(name, fbType) =
        { Name = name
          FBType = fbType
          StateStorage = None }

    /// <summary>상태 초기화</summary>
    /// <returns>초기화된 상태 Map (변수 이름 -> 초기값)</returns>
    /// <remarks>
    /// 각 정적 변수에 대해:
    /// - 초기값이 지정된 경우 해당 값 사용
    /// - 그렇지 않으면 타입별 기본값 사용 (Bool: false, Int: 0, Double: 0.0, String: "")
    /// </remarks>
    member this.InitializeState() : Map<string, obj> =
        this.FBType.Statics
        |> List.map (fun (name, dataType, initValue) ->
            let value =
                match initValue with
                | Some v -> v
                | None ->
                    match dataType with
                    | DsDataType.TBool -> box false
                    | DsDataType.TInt -> box 0
                    | DsDataType.TDouble -> box 0.0
                    | DsDataType.TString -> box ""
            (name, value))
        |> Map.ofList

    /// <summary>상태 초기화 여부</summary>
    /// <returns>StateStorage가 Some이면 true, None이면 false</returns>
    member this.IsInitialized =
        this.StateStorage.IsSome

/// <summary>UserFC/FB 전체 검증 유틸리티 모듈</summary>
/// <remarks>
/// UserFC 및 UserFB의 검증을 위한 편의 함수들을 제공합니다.
/// </remarks>
module UserDefinitionValidation =
    /// <summary>식별자 유효성 검증 (re-export)</summary>
    /// <remarks>IdentifierValidation.isValidIdentifier와 동일</remarks>
    let isValidIdentifier = IdentifierValidation.isValidIdentifier

    /// <summary>예약어 검증 (re-export)</summary>
    /// <remarks>IdentifierValidation.isReservedKeyword와 동일</remarks>
    let isReservedKeyword = IdentifierValidation.isReservedKeyword

    /// <summary>UserFC 전체 검증 (이름 + 내부 검증)</summary>
    /// <param name="fc">검증할 UserFC</param>
    /// <returns>검증 성공 시 Ok (), 실패 시 Error with UserDefinitionError</returns>
    /// <remarks>
    /// 검증 순서:
    /// 1. FC 이름이 유효한 식별자인지
    /// 2. FC 내부 검증 (fc.Validate())
    /// </remarks>
    let validateUserFC (fc: UserFC) : Result<unit, UserDefinitionError> =
        let fcName = IdentifierValidation.normalize fc.Name
        match IdentifierValidation.validate "FC" fc.Name with
        | Error err -> Error (UserDefinitionError.prependMany ["FC"; fcName] err)
        | Ok () -> fc.Validate()

    /// <summary>UserFB 전체 검증 (이름 + 내부 검증)</summary>
    /// <param name="fb">검증할 UserFB</param>
    /// <returns>검증 성공 시 Ok (), 실패 시 Error with UserDefinitionError</returns>
    /// <remarks>
    /// 검증 순서:
    /// 1. FB 이름이 유효한 식별자인지
    /// 2. FB 내부 검증 (fb.Validate())
    /// </remarks>
    let validateUserFB (fb: UserFB) : Result<unit, UserDefinitionError> =
        let fbName = IdentifierValidation.normalize fb.Name
        match IdentifierValidation.validate "FB" fb.Name with
        | Error err -> Error (UserDefinitionError.prependMany ["FB"; fbName] err)
        | Ok () -> fb.Validate()
