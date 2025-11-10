namespace Ev2.Cpu.Ast

open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// AST 검증 유틸리티 (AST Validation)
// ─────────────────────────────────────────────────────────────────────
// 표현식과 문장의 일관성 검증 및 정적 분석
// ─────────────────────────────────────────────────────────────────────

/// Validation error types
type ValidationError =
    | UndefinedVariable of name:string * location:string
    | TypeMismatch of expected:DsDataType * actual:DsDataType * location:string
    | InvalidOperation of op:string * reason:string
    | DuplicateDefinition of name:string * locations:string list
    | UnusedVariable of name:string
    | CircularDependency of chain:string list

    member this.Format() =
        match this with
        | UndefinedVariable(name, loc) ->
            sprintf "Undefined variable '%s' at %s" name loc
        | TypeMismatch(expected, actual, loc) ->
            sprintf "Type mismatch at %s: expected %A but got %A" loc expected actual
        | InvalidOperation(op, reason) ->
            sprintf "Invalid operation '%s': %s" op reason
        | DuplicateDefinition(name, locs) ->
            sprintf "Duplicate definition of '%s' at: %s" name (String.concat ", " locs)
        | UnusedVariable name ->
            sprintf "Variable '%s' is defined but never used" name
        | CircularDependency chain ->
            sprintf "Circular dependency detected: %s" (String.concat " → " chain)

/// Validation context
type ValidationContext = {
    DefinedVariables: Map<string, DsDataType>
    UsedVariables: Set<string>
    AllowImplicitTypes: bool
}
with
    static member Empty = {
        DefinedVariables = Map.empty
        UsedVariables = Set.empty
        AllowImplicitTypes = true  // Allow function calls and expressions with implicit types
    }

    member this.DefineVariable(name: string, typ: DsDataType) =
        { this with DefinedVariables = Map.add name typ this.DefinedVariables }

    member this.UseVariable(name: string) =
        { this with UsedVariables = Set.add name this.UsedVariables }

/// AST validation utilities
module AstValidation =

    /// Validate expression against context
    let rec validateExpression (ctx: ValidationContext) (expr: DsExpr) : Result<ValidationContext, ValidationError list> =
        match expr with
        | EConst(value, typ) ->
            try
                typ.Validate(value) |> ignore
                Ok ctx
            with
            | ex -> Error [InvalidOperation("Constant", ex.Message)]

        | EVar(name, declaredType) ->
            match Map.tryFind name ctx.DefinedVariables with
            | Some definedType when definedType <> declaredType ->
                Error [TypeMismatch(definedType, declaredType, sprintf "Variable '%s'" name)]
            | None when not ctx.AllowImplicitTypes ->
                Error [UndefinedVariable(name, "expression")]
            | _ ->
                Ok (ctx.UseVariable name)

        | ETerminal(name, _) ->
            Ok (ctx.UseVariable name)

        | EUnary(op, operand) ->
            validateExpression ctx operand

        | EBinary(op, left, right) ->
            match validateExpression ctx left, validateExpression ctx right with
            | Ok ctx1, Ok ctx2 ->
                Ok { ctx1 with UsedVariables = Set.union ctx1.UsedVariables ctx2.UsedVariables }
            | Error e1, Error e2 -> Error (e1 @ e2)
            | Error e, _ | _, Error e -> Error e

        | ECall(funcName, args) ->
            args
            |> List.fold (fun acc arg ->
                match acc, validateExpression ctx arg with
                | Ok ctx1, Ok ctx2 ->
                    Ok { ctx1 with UsedVariables = Set.union ctx1.UsedVariables ctx2.UsedVariables }
                | Error e1, Error e2 -> Error (e1 @ e2)
                | Error e, _ -> Error e
                | Ok _, Error e -> Error e
            ) (Ok ctx)

        | EUserFC(fcName, args, _, _) ->
            // Same validation as ECall
            args
            |> List.fold (fun acc arg ->
                match acc, validateExpression ctx arg with
                | Ok ctx1, Ok ctx2 ->
                    Ok { ctx1 with UsedVariables = Set.union ctx1.UsedVariables ctx2.UsedVariables }
                | Error e1, Error e2 -> Error (e1 @ e2)
                | Error e, _ -> Error e
                | Ok _, Error e -> Error e
            ) (Ok ctx)

        | EMeta _ -> Ok ctx

    /// Validate statement against context
    let validateStatement (ctx: ValidationContext) (stmt: DsStatement) : Result<ValidationContext, ValidationError list> =
        match stmt with
        | SAssign(cond, (targetName, targetType)) ->
            match validateExpression ctx cond with
            | Error errors -> Error errors
            | Ok ctx1 ->
                match cond.InferType() with
                | Some exprType when not (targetType.IsCompatibleWith exprType) ->
                    Error [TypeMismatch(targetType, exprType, sprintf "Assignment to '%s'" targetName)]
                | None when not ctx.AllowImplicitTypes ->
                    Error [InvalidOperation("Assignment", "Cannot infer expression type")]
                | _ ->
                    Ok (ctx1.DefineVariable(targetName, targetType))

        | STimer(rungIn, reset, name, preset) ->
            if preset < 0 then
                Error [InvalidOperation("Timer", sprintf "Preset must be non-negative, got %d" preset)]
            else
                let ctxResult =
                    [rungIn; reset]
                    |> List.choose id
                    |> List.fold (fun acc expr ->
                        match acc, validateExpression ctx expr with
                        | Ok ctx1, Ok ctx2 ->
                            Ok { ctx1 with UsedVariables = Set.union ctx1.UsedVariables ctx2.UsedVariables }
                        | Error e1, Error e2 -> Error (e1 @ e2)
                        | Error e, _ -> Error e
                        | Ok _, Error e -> Error e
                    ) (Ok ctx)

                match ctxResult with
                | Ok ctx1 ->
                    // Define timer output variables
                    ctx1
                    |> fun c -> c.DefineVariable(sprintf "%s.EN" name, TBool)
                    |> fun c -> c.DefineVariable(sprintf "%s.TT" name, TBool)
                    |> fun c -> c.DefineVariable(sprintf "%s.DN" name, TBool)
                    |> fun c -> c.DefineVariable(sprintf "%s.ACC" name, TInt)
                    |> fun c -> c.DefineVariable(sprintf "%s.PRE" name, TInt)
                    |> Ok
                | Error e -> Error e

        | SCounter(up, down, reset, name, preset) ->
            if preset < 0 then
                Error [InvalidOperation("Counter", sprintf "Preset must be non-negative, got %d" preset)]
            else
                let ctxResult =
                    [up; down; reset]
                    |> List.choose id
                    |> List.fold (fun acc expr ->
                        match acc, validateExpression ctx expr with
                        | Ok ctx1, Ok ctx2 ->
                            Ok { ctx1 with UsedVariables = Set.union ctx1.UsedVariables ctx2.UsedVariables }
                        | Error e1, Error e2 -> Error (e1 @ e2)
                        | Error e, _ -> Error e
                        | Ok _, Error e -> Error e
                    ) (Ok ctx)

                match ctxResult with
                | Ok ctx1 ->
                    ctx1
                    |> fun c -> c.DefineVariable(sprintf "%s.DN" name, TBool)
                    |> fun c -> c.DefineVariable(sprintf "%s.CV" name, TInt)
                    |> fun c -> c.DefineVariable(sprintf "%s.PV" name, TInt)
                    |> Ok
                | Error e -> Error e

        | SCoil(setCond, resetCond, (coilName, coilType), _) ->
            match validateExpression ctx setCond, validateExpression ctx resetCond with
            | Ok ctx1, Ok ctx2 ->
                let mergedCtx = { ctx1 with UsedVariables = Set.union ctx1.UsedVariables ctx2.UsedVariables }
                Ok (mergedCtx.DefineVariable(coilName, coilType))
            | Error e1, Error e2 -> Error (e1 @ e2)
            | Error e, _ | _, Error e -> Error e

        | SUserFB(instanceName, fbName, inputs, outputs, stateLayout) ->
            // Validate all input expressions
            let inputsResult =
                inputs
                |> Map.toList
                |> List.fold (fun acc (_, expr) ->
                    match acc, validateExpression ctx expr with
                    | Ok ctx1, Ok ctx2 ->
                        Ok { ctx1 with UsedVariables = Set.union ctx1.UsedVariables ctx2.UsedVariables }
                    | Error e1, Error e2 -> Error (e1 @ e2)
                    | Error e, _ -> Error e
                    | Ok _, Error e -> Error e
                ) (Ok ctx)

            match inputsResult with
            | Ok ctx1 ->
                // Note: FB outputs are NOT pre-registered here because their types
                // depend on the FB definition which isn't available during AST validation.
                // They will be validated at runtime when the FB is executed.

                // Define state variables only (these have explicit types in stateLayout)
                let ctxWithState =
                    stateLayout
                    |> List.fold (fun (c: ValidationContext) (stateName, stateType) ->
                        c.DefineVariable(sprintf "%s.%s" instanceName stateName, stateType)) ctx1
                Ok ctxWithState
            | Error e -> Error e

        | SBreak ->
            // Break statement is a control flow instruction, no validation needed
            Ok ctx

        | SFor _ ->
            // For loop validation not yet implemented
            Ok ctx

        | SWhile _ ->
            // While loop validation not yet implemented
            Ok ctx

    /// Validate program (list of statements)
    let validateProgram (statements: DsStatement list) : Result<unit, ValidationError list> =
        let initialCtx = ValidationContext.Empty

        let result =
            statements
            |> List.fold (fun acc stmt ->
                match acc with
                | Error errors -> Error errors
                | Ok ctx ->
                    match validateStatement ctx stmt with
                    | Ok newCtx -> Ok newCtx
                    | Error errors -> Error errors
            ) (Ok initialCtx)

        match result with
        | Ok ctx ->
            // Check for unused variables
            let unused =
                ctx.DefinedVariables
                |> Map.toSeq
                |> Seq.filter (fun (name, _) -> not (Set.contains name ctx.UsedVariables))
                |> Seq.map fst
                |> Seq.toList

            if List.isEmpty unused then
                Ok ()
            else
                Error (unused |> List.map UnusedVariable)

        | Error errors -> Error errors

    /// Find circular dependencies in assignments
    let findCircularDependencies (statements: DsStatement list) : ValidationError list =
        let dependencies =
            statements
            |> List.choose (fun stmt ->
                match stmt with
                | SAssign(cond, (target, _)) ->
                    Some (target, cond.GetVariables() |> Set.toList)
                | _ -> None
            )
            |> List.groupBy fst  // Group by target variable
            |> List.map (fun (target, assignments) ->
                // Merge all dependencies from all assignments to this target
                let allDeps =
                    assignments
                    |> List.collect snd  // Get all dependency lists
                    |> List.distinct     // Remove duplicates
                (target, allDeps)
            )
            |> Map.ofList

        let rec findCycle (visited: Set<string>) (path: string list) (current: string) : string list option =
            if List.contains current path then
                Some (List.rev (current :: path))
            elif Set.contains current visited then
                None
            else
                match Map.tryFind current dependencies with
                | None -> None
                | Some deps ->
                    deps
                    |> List.tryPick (findCycle (Set.add current visited) (current :: path))

        dependencies
        |> Map.toSeq
        |> Seq.choose (fun (name, _) -> findCycle Set.empty [] name)
        |> Seq.map CircularDependency
        |> Seq.toList

    /// Full validation with all checks
    let validate (statements: DsStatement list) : Result<unit, ValidationError list> =
        match validateProgram statements with
        | Error errors -> Error errors
        | Ok () ->
            let cycles = findCircularDependencies statements
            if List.isEmpty cycles then
                Ok ()
            else
                Error cycles
