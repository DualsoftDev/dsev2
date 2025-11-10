namespace Ev2.Cpu.Ast

open System

// ─────────────────────────────────────────────────────────────────────
// AST ↔ Runtime Expression 변환 레이어
// ─────────────────────────────────────────────────────────────────────
// Ast.DsExpr (파서 출력, 타입 정보 풍부) ↔ Core.DsExpr (런타임 실행용)
// 두 표현식 타입 간 명확한 경계와 변환 로직 제공
// ─────────────────────────────────────────────────────────────────────

/// AST 변환 시 발생 가능한 에러
[<StructuralEquality; NoComparison>]
type ConversionError =
    /// 변수가 TagRegistry에 등록되지 않음
    | UndefinedVariable of name:string

    /// 타입 불일치 (예: Bool 변수에 Int 값 할당)
    | TypeMismatch of expected:Type * actual:Type

    /// 런타임에서 지원하지 않는 기능 (예: EMeta)
    | UnsupportedFeature of feature:string * details:string

    /// 함수 호출 시 인자 개수 불일치
    | InvalidArgumentCount of funcName:string * expected:int * actual:int

    member this.Format() =
        match this with
        | UndefinedVariable name ->
            sprintf "Undefined variable: '%s' not found in TagRegistry" name
        | TypeMismatch(expected, actual) ->
            sprintf "Type mismatch: expected typeof<%s> but got typeof<%s>" (Ev2.Cpu.Core.TypeHelpers.getTypeName expected) (Ev2.Cpu.Core.TypeHelpers.getTypeName actual)
        | UnsupportedFeature(feature, details) ->
            sprintf "Unsupported feature '%s': %s" feature details
        | InvalidArgumentCount(funcName, expected, actual) ->
            sprintf "Function '%s' expects %d arguments but got %d" funcName expected actual

/// AST 변환 유틸리티
module AstConverter =

    // ─────────────────────────────────────────────────────────────────
    // Ast.DsExpr → Core.DsExpr 변환 (파서 출력 → 런타임 실행)
    // ─────────────────────────────────────────────────────────────────

    /// Ast.DsExpr을 Core.DsExpr로 변환
    /// - EVar, ETerminal → Core.Terminal (TagRegistry에서 Tag 조회)
    /// - EConst → Core.Const (타입 정보 보존)
    /// - EUnary, EBinary, ECall → 동일 구조
    /// - EMeta → 메타데이터 제거하고 내부 표현식만 변환 (런타임 불필요)
    let rec toRuntimeExpr (astExpr: DsExpr) : Result<Ev2.Cpu.Core.Expression.DsExpr, ConversionError> =
        match astExpr with

        // 상수: 타입 정보 그대로 유지
        | EConst(value, typ) ->
            Ok (Ev2.Cpu.Core.Expression.Const(value, typ))

        // 변수: TagRegistry에서 Tag 조회 후 Terminal로 변환
        | EVar(name, declaredType) ->
            match Ev2.Cpu.Core.DsTagRegistry.tryFind name with
            | Some tag ->
                // 타입 일치 확인
                if tag.StructType = declaredType then
                    Ok (Ev2.Cpu.Core.Expression.Terminal tag)
                else
                    Error (TypeMismatch(declaredType, tag.StructType))
            | None ->
                // TagRegistry에 없으면 자동 등록 (기본 동작)
                let tag = Ev2.Cpu.Core.DsTag.Create(name, declaredType)
                Ev2.Cpu.Core.DsTagRegistry.register tag |> ignore
                Ok (Ev2.Cpu.Core.Expression.Terminal tag)

        // 터미널 (I/O): TagRegistry에서 조회 후 Terminal로 변환
        | ETerminal(name, declaredType) ->
            match Ev2.Cpu.Core.DsTagRegistry.tryFind name with
            | Some tag ->
                if tag.StructType = declaredType then
                    Ok (Ev2.Cpu.Core.Expression.Terminal tag)
                else
                    Error (TypeMismatch(declaredType, tag.StructType))
            | None ->
                // 터미널도 자동 등록
                let tag = Ev2.Cpu.Core.DsTag.Create(name, declaredType)
                Ev2.Cpu.Core.DsTagRegistry.register tag |> ignore
                Ok (Ev2.Cpu.Core.Expression.Terminal tag)

        // 단항 연산: 재귀 변환
        | EUnary(op, expr) ->
            match toRuntimeExpr expr with
            | Ok runtimeExpr -> Ok (Ev2.Cpu.Core.Expression.Unary(op, runtimeExpr))
            | Error e -> Error e

        // 이항 연산: 재귀 변환
        | EBinary(op, left, right) ->
            match toRuntimeExpr left, toRuntimeExpr right with
            | Ok leftRuntime, Ok rightRuntime ->
                Ok (Ev2.Cpu.Core.Expression.Binary(op, leftRuntime, rightRuntime))
            | Error e, _ -> Error e
            | _, Error e -> Error e

        // 함수 호출: 인자들 재귀 변환
        | ECall(funcName, args) ->
            let convertedArgs =
                args
                |> List.fold (fun (acc, error) arg ->
                    match error with
                    | Some e -> (acc, Some e)
                    | None ->
                        match toRuntimeExpr arg with
                        | Ok runtimeArg -> (runtimeArg :: acc, None)
                        | Error e -> (acc, Some e)
                ) ([], None)

            match convertedArgs with
            | (runtimeArgs, None) ->
                Ok (Ev2.Cpu.Core.Expression.Function(funcName, List.rev runtimeArgs))
            | (_, Some e) ->
                Error e

        | EUserFC(fcName, args, _, _) ->
            // Convert UserFC calls the same way as regular function calls
            let convertedArgs =
                args
                |> List.fold (fun (acc, error) arg ->
                    match error with
                    | Some e -> (acc, Some e)
                    | None ->
                        match toRuntimeExpr arg with
                        | Ok runtimeArg -> (runtimeArg :: acc, None)
                        | Error e -> (acc, Some e)
                ) ([], None)

            match convertedArgs with
            | (runtimeArgs, None) ->
                Ok (Ev2.Cpu.Core.Expression.Function(fcName, List.rev runtimeArgs))
            | (_, Some e) ->
                Error e

        // 메타데이터: 런타임에서는 불필요하므로 버림
        | EMeta(_, _) ->
            Error (UnsupportedFeature("EMeta", "Metadata expressions cannot be converted to runtime expressions"))

    // ─────────────────────────────────────────────────────────────────
    // Core.DsExpr → Ast.DsExpr 역변환 (디버깅/분석용)
    // ─────────────────────────────────────────────────────────────────

    /// Core.DsExpr을 Ast.DsExpr로 역변환
    /// - 주로 디버깅, 로깅, 분석 도구에서 사용
    /// - 타입 정보는 Core.DsExpr.InferType()으로 추론
    let rec fromRuntimeExpr (runtimeExpr: Ev2.Cpu.Core.Expression.DsExpr) : DsExpr =
        match runtimeExpr with

        // 상수: 타입 정보 그대로 유지
        | Ev2.Cpu.Core.Expression.Const(value, typ) ->
            EConst(value, typ)

        // 터미널: Tag에서 이름과 타입 추출하여 EVar로 변환
        // (EVar vs ETerminal 구분 불가능 - 둘 다 EVar로 변환)
        | Ev2.Cpu.Core.Expression.Terminal tag ->
            EVar(tag.Name, tag.StructType)

        // 단항 연산: 재귀 변환
        | Ev2.Cpu.Core.Expression.Unary(op, expr) ->
            EUnary(op, fromRuntimeExpr expr)

        // 이항 연산: 재귀 변환
        | Ev2.Cpu.Core.Expression.Binary(op, left, right) ->
            EBinary(op, fromRuntimeExpr left, fromRuntimeExpr right)

        // 함수 호출: 인자들 재귀 변환
        | Ev2.Cpu.Core.Expression.Function(funcName, args) ->
            ECall(funcName, args |> List.map fromRuntimeExpr)

    // ─────────────────────────────────────────────────────────────────
    // 변환 헬퍼 함수
    // ─────────────────────────────────────────────────────────────────

    /// 변환 결과를 unwrap (예외 발생 가능)
    let toRuntimeExprUnsafe (astExpr: DsExpr) : Ev2.Cpu.Core.Expression.DsExpr =
        match toRuntimeExpr astExpr with
        | Ok expr -> expr
        | Error e -> raise (InvalidOperationException($"Conversion failed: {e.Format()}"))

    /// 여러 표현식을 한번에 변환
    let toRuntimeExprs (astExprs: DsExpr list) : Result<Ev2.Cpu.Core.Expression.DsExpr list, ConversionError> =
        let rec convert acc remaining =
            match remaining with
            | [] -> Ok (List.rev acc)
            | head :: tail ->
                match toRuntimeExpr head with
                | Ok runtimeExpr -> convert (runtimeExpr :: acc) tail
                | Error e -> Error e
        convert [] astExprs

    /// 변환 성공 여부 확인
    let canConvertToRuntime (astExpr: DsExpr) : bool =
        match toRuntimeExpr astExpr with
        | Ok _ -> true
        | Error _ -> false

    /// 변환 시 발생하는 에러 목록 추출 (디버깅용)
    let validateConversion (astExpr: DsExpr) : ConversionError list =
        let rec collectErrors expr =
            match toRuntimeExpr expr with
            | Ok _ -> []
            | Error e -> [e]
        collectErrors astExpr
