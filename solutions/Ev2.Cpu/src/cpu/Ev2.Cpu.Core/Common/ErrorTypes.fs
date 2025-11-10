namespace Ev2.Cpu.Core.Common

open System

// ═════════════════════════════════════════════════════════════════════════════
// Common Error Infrastructure
// ═════════════════════════════════════════════════════════════════════════════
// 모든 모듈에서 공유할 수 있는 에러 처리 유틸리티와 공통 패턴을 제공합니다.
// 도메인별 에러 타입(ParseError, ValidationError 등)은 각 모듈에 유지하되,
// 공통 기능은 여기서 제공합니다.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 에러에 컨텍스트 경로를 추가할 수 있는 인터페이스
/// UserDefinitionError와 같이 계층적 에러 경로를 추적하는 타입에 사용
/// </summary>
type IPathableError =
    abstract member WithPath: string list -> IPathableError

/// <summary>
/// 포맷 가능한 에러 인터페이스
/// 모든 에러 타입이 일관된 방식으로 메시지를 생성할 수 있도록 합니다.
/// </summary>
type IFormattableError =
    abstract member Format: unit -> string

/// <summary>
/// 에러 코드를 가진 에러 인터페이스
/// </summary>
type ICodedError =
    abstract member Code: string

/// <summary>
/// 구조화된 에러의 기본 형태
/// </summary>
[<StructuralEquality; NoComparison>]
type StructuredError = {
    /// 에러 코드 (예: "VAR.NameEmpty", "PARSE.UnexpectedToken")
    Code: string
    /// 에러 메시지
    Message: string
    /// 에러 경로 (계층적 컨텍스트)
    Path: string list
    /// 추가 메타데이터
    Metadata: Map<string, obj>
} with
    interface IFormattableError with
        member this.Format() = this.Format()

    interface ICodedError with
        member this.Code = this.Code

    interface IPathableError with
        member this.WithPath(path) =
            { this with Path = path } :> IPathableError

    member this.Format() =
        match this.Path with
        | [] -> this.Message
        | path -> sprintf "%s: %s" (String.concat "." path) this.Message

/// <summary>
/// StructuredError 생성 및 조작 유틸리티
/// </summary>
module StructuredError =
    /// <summary>빈 메타데이터로 기본 에러 생성</summary>
    let create code message =
        { Code = code; Message = message; Path = []; Metadata = Map.empty }

    /// <summary>경로를 포함한 에러 생성</summary>
    let createWithPath code message path =
        { Code = code; Message = message; Path = path; Metadata = Map.empty }

    /// <summary>메타데이터를 포함한 에러 생성</summary>
    let createWithMetadata code message metadata =
        { Code = code; Message = message; Path = []; Metadata = metadata }

    /// <summary>경로 앞에 세그먼트 추가</summary>
    let prepend segment error =
        { error with Path = segment :: error.Path }

    /// <summary>경로 앞에 여러 세그먼트 추가</summary>
    let prependMany segments error =
        { error with Path = segments @ error.Path }

    /// <summary>메타데이터 추가</summary>
    let addMetadata key value error =
        { error with Metadata = Map.add key value error.Metadata }

    /// <summary>에러를 문자열로 포맷</summary>
    let format (error: StructuredError) = error.Format()

/// <summary>
/// Result 타입에 대한 공통 유틸리티
/// </summary>
module Result =
    /// <summary>Result를 다른 에러 타입으로 맵핑</summary>
    let mapError f result =
        match result with
        | Ok x -> Ok x
        | Error e -> Error (f e)

    /// <summary>여러 Result를 하나로 병합 (모두 Ok면 Ok, 하나라도 Error면 Error)</summary>
    let sequence (results: Result<'T, 'E> list) : Result<'T list, 'E list> =
        let rec loop acc remaining =
            match remaining with
            | [] -> Ok (List.rev acc)
            | (Ok x) :: rest -> loop (x :: acc) rest
            | (Error e) :: rest ->
                // 첫 번째 에러 발견 시 나머지도 수집
                let errors = e :: (rest |> List.choose (function Error e -> Some e | _ -> None))
                Error errors
        loop [] results

    /// <summary>Result 리스트를 모두 실행하고 첫 번째 에러만 반환</summary>
    let traverse (f: 'T -> Result<'U, 'E>) (list: 'T list) : Result<'U list, 'E> =
        let rec loop acc remaining =
            match remaining with
            | [] -> Ok (List.rev acc)
            | x :: rest ->
                match f x with
                | Ok y -> loop (y :: acc) rest
                | Error e -> Error e
        loop [] list

    /// <summary>Result를 Option으로 변환 (Error는 None)</summary>
    let toOption result =
        match result with
        | Ok x -> Some x
        | Error _ -> None

    /// <summary>Result의 에러를 문자열로 변환</summary>
    let errorToString (error: 'E when 'E :> IFormattableError) =
        error.Format()

    /// <summary>두 Result를 조합 (둘 다 Ok면 튜플로 반환)</summary>
    let zip (r1: Result<'T1, 'E>) (r2: Result<'T2, 'E>) : Result<'T1 * 'T2, 'E> =
        match r1, r2 with
        | Ok x, Ok y -> Ok (x, y)
        | Error e, _ -> Error e
        | _, Error e -> Error e

/// <summary>
/// Validation 결과 타입 - 여러 에러를 누적할 수 있음
/// </summary>
[<StructuralEquality; NoComparison>]
type ValidationResult<'T, 'E> =
    | Valid of 'T
    | Invalid of errors: 'E list
    with
        member this.Errors =
            match this with
            | Valid _ -> []
            | Invalid errors -> errors

/// <summary>
/// ValidationResult 유틸리티
/// </summary>
module ValidationResult =
    /// <summary>값을 Valid로 래핑</summary>
    let valid x = Valid x

    /// <summary>단일 에러를 Invalid로 래핑</summary>
    let invalid error = Invalid [error]

    /// <summary>여러 에러를 Invalid로 래핑</summary>
    let invalidMany errors = Invalid errors

    /// <summary>Result를 ValidationResult로 변환</summary>
    let ofResult result =
        match result with
        | Ok x -> Valid x
        | Error e -> Invalid [e]

    /// <summary>ValidationResult를 Result로 변환 (첫 번째 에러만)</summary>
    let toResult vr =
        match vr with
        | Valid x -> Ok x
        | Invalid [] -> Error (failwith "Invalid ValidationResult with no errors")
        | Invalid (e :: _) -> Error e

    /// <summary>여러 ValidationResult를 병합 (모든 에러 수집)</summary>
    let combine (results: ValidationResult<'T, 'E> list) : ValidationResult<'T list, 'E> =
        let rec loop validAcc errorAcc remaining =
            match remaining with
            | [] ->
                match errorAcc with
                | [] -> Valid (List.rev validAcc)
                | errors -> Invalid (List.rev errors)
            | (Valid x) :: rest -> loop (x :: validAcc) errorAcc rest
            | (Invalid errors) :: rest -> loop validAcc (List.append (List.rev errors) errorAcc) rest
        loop [] [] results

    /// <summary>함수 적용</summary>
    let map f vr =
        match vr with
        | Valid x -> Valid (f x)
        | Invalid errors -> Invalid errors

    /// <summary>에러 맵핑</summary>
    let mapErrors f vr =
        match vr with
        | Valid x -> Valid x
        | Invalid errors -> Invalid (List.map f errors)

/// <summary>
/// 공통 Result 타입 별칭들
/// </summary>
module CommonResults =
    /// <summary>문자열 에러를 사용하는 Result</summary>
    type StringResult<'T> = Result<'T, string>

    /// <summary>구조화된 에러를 사용하는 Result</summary>
    type StructuredResult<'T> = Result<'T, StructuredError>

    /// <summary>여러 에러를 담을 수 있는 Result</summary>
    type MultiErrorResult<'T, 'E> = Result<'T, 'E list>
