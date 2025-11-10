namespace Ev2.Cpu.Runtime
open System
open Ev2.Cpu.Core

/// <summary>
/// 표현식 평가기 - PLC 제어 표현식을 런타임에 평가
/// </summary>
/// <remarks>
/// <para>
/// ExprEvaluator는 DsExpr 트리를 재귀적으로 순회하며 obj 타입 값을 계산합니다.
/// 모든 산술/논리/비교 연산은 BuiltinFunctions 모듈로 위임됩니다.
/// </para>
/// <para>
/// 평가 순서:
/// 1. 상수 → 즉시 값 반환
/// 2. 변수 → 메모리에서 값 조회
/// 3. 단항/이항 연산 → 재귀 평가 후 연산 수행
/// 4. 함수 호출 → 인자 평가 후 내장 함수 실행
/// </para>
/// </remarks>
module rec ExprEvaluator =

    // ─────────────────────────────────────────────────────────────────────
    // 보조: 인자 리스트 평가
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>함수 인자 리스트를 평가하여 obj 리스트로 변환</summary>
    let rec private evalArgs (ctx: ExecutionContext) (exprs: DsExpr list) : obj list =
        exprs |> List.map (eval ctx)

    // ─────────────────────────────────────────────────────────────────────
    // 단항/이항: BuiltinFunctions.call 로 위임
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>단항 연산 평가 (NOT, NEG, 타입 변환 등)</summary>
    let private evalUnary (ctx: ExecutionContext) (op: DsOp) (e: DsExpr) : obj =
        let name = OperatorMapping.mapUnary op
        let argv = [ eval ctx e ]
        BuiltinFunctions.call name argv (Some ctx)

    /// <summary>이항 연산 평가 (+, -, *, /, AND, OR, ==, != 등)</summary>
    let private evalBinary (ctx: ExecutionContext) (op: DsOp) (l: DsExpr) (r: DsExpr) : obj =
        let name = OperatorMapping.mapBinary op
        let argv = [ eval ctx l; eval ctx r ]
        BuiltinFunctions.call name argv (Some ctx)

    // ─────────────────────────────────────────────────────────────────────
    // 메인: 표현식 평가
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 표현식을 평가하여 결과 값 반환
    /// </summary>
    /// <param name="ctx">실행 컨텍스트 (메모리, 타이머, 카운터 등)</param>
    /// <param name="expr">평가할 표현식</param>
    /// <returns>평가 결과 (obj 타입)</returns>
    /// <remarks>
    /// - Const: 즉시 값 반환
    /// - Terminal: ctx.Memory에서 변수 값 조회
    /// - Unary/Binary: 재귀 평가 후 BuiltinFunctions 호출
    /// - Function: 내장 함수 실행
    /// </remarks>
    let eval (ctx: ExecutionContext) (expr: DsExpr) : obj =
        match expr with
        | DsExpr.Const    (value, _typ) ->
            // 필요 시 _typ 으로 추가 검증/보정 가능
            value

        | DsExpr.Terminal (dsTag)  ->
            ctx.Memory.Get dsTag.Name

        | DsExpr.Unary   (op, e)        ->
            evalUnary  ctx op e

        | DsExpr.Binary  (op, l, r)     ->
            evalBinary ctx op l r

        | DsExpr.Function(name, args)   ->
            // Special case: IF function needs lazy evaluation
            if name.ToUpperInvariant() = "IF" then
                match args with
                | [condition; trueExpr; falseExpr] ->
                    let condValue = eval ctx condition
                    if TypeConverter.toBool condValue then
                        eval ctx trueExpr
                    else
                        eval ctx falseExpr
                | _ ->
                    failwithf "IF requires exactly 3 arguments, got %d" (List.length args)
            else
                // All other functions: check for call relay integration
                let argv = evalArgs ctx args

                // Check if this function has a registered call relay (external API call)
                match ctx.RelayStateManager with
                | Some manager ->
                    match manager.TryGetCallRelay(name) with
                    | Some relay ->
                        // CRITICAL FIX (DEFECT-CRIT-15): Add lock for atomic state reads/transitions
                        // Previous code: Multiple threads could read CurrentState simultaneously
                        // Problem: Race between IsInProgress check and CurrentState check
                        // Solution: Lock entire state machine operation for atomicity
                        lock relay (fun () ->
                            // HIGH FIX: Call relay state machine must follow spec (RuntimeSpec.md §3.2)
                            // Poll for completion of previous call (if any)
                            if relay.IsInProgress then
                                let completed = relay.Poll()
                                if not completed && relay.CurrentState = CallRelayState.Faulted then
                                    // Only log error if truly faulted (timeout or error)
                                    match relay.LastError with
                                    | Some error ->
                                        ctx.LogRecoverable($"Call relay '{name}' failed: {error}", fbInstance = name)
                                    | None ->
                                        ctx.LogRecoverable($"Call relay '{name}' timed out", fbInstance = name)

                            // HIGH FIX: Only trigger new call if relay is in Waiting state (spec requirement)
                            if relay.CurrentState = CallRelayState.Waiting then
                                if relay.Trigger() then
                                    // Execute function (synchronous for now, async support requires design change)
                                    let result = BuiltinFunctions.call name argv (Some ctx)
                                    // Complete immediately (poll will transition to Waiting)
                                    relay.Poll() |> ignore
                                    result
                                else
                                    // Trigger failed - log and fallback
                                    ctx.LogWarning($"Call relay '{name}' trigger failed", fbInstance = name)
                                    BuiltinFunctions.call name argv (Some ctx)
                            elif relay.CurrentState = CallRelayState.AwaitingAck || relay.CurrentState = CallRelayState.Invoking then
                                // CRITICAL FIX (DEFECT-020-3): Do NOT re-execute after Poll() completes
                                // Previous code called BuiltinFunctions.call after completion, starting a new request
                                // Long-running calls never delivered results - they auto-retriggered instead
                                // Solution: Return completion payload WITHOUT re-invoking (spec §3.2 handshake)
                                // Note: Current synchronous design lacks completion payload storage
                                // Return default value; caller must track results externally until async support added
                                obj()  // Still in progress or just completed - return default
                            else
                                // CRITICAL FIX (DEFECT-016-2): Faulted recovery must use Trigger()/Poll() handshake
                                // Direct execution bypasses timeout/progress tracking (RuntimeSpec.md:53-58)
                                // Recover() transitions to Waiting, then Trigger() starts new attempt with full handshake
                                relay.Recover()  // Transition Faulted -> Waiting and reset retry count
                                ctx.LogWarning($"Call relay '{name}' recovered from faulted state, retrying with handshake", fbInstance = name)

                                // Now trigger new call attempt through proper handshake
                                if relay.Trigger() then
                                    let result = BuiltinFunctions.call name argv (Some ctx)
                                    relay.Poll() |> ignore  // Complete handshake
                                    result
                                else
                                    // Trigger failed again - return empty object
                                    ctx.LogWarning($"Call relay '{name}' trigger failed after recovery", fbInstance = name)
                                    obj()
                        )  // End of lock relay
                    | None ->
                        // No relay registered - direct execution
                        BuiltinFunctions.call name argv (Some ctx)
                | None ->
                    // No relay manager - direct execution
                    BuiltinFunctions.call name argv (Some ctx)
            
/// 표현식 최적화 (obj 런타임)
module ExprOptimizer =

    // ── 보조: obj → DsDataType 추론 ──────────────────────────────────────────
    let private tryInferType (v: obj) : DsDataType option =
        if isNull v then None
        else
            match v with
            | :? bool   -> Some DsDataType.TBool
            | :? int    -> Some DsDataType.TInt
            | :? float  -> Some DsDataType.TDouble
            | :? string -> Some DsDataType.TString
            | _         -> None

    // ── 순수 함수 판단(컨텍스트 의존/부작용 함수 제외) ─────────────────────
    let private isPureFunction (name: string) =
        match name.ToUpperInvariant() with
        // 부작용/컨텍스트 의존
        | "MOV" | "TON" | "TOF" | "CTU" | "CTD" | "PRINT" | "NOW" | "RANDOM" -> false
        // 나머지는 전부 순수로 간주
        | _ -> true

    // ── 상수 폴딩 ─────────────────────────────────────────────────────────
    let rec constantFold (expr: DsExpr) : DsExpr =
        let fold = constantFold

        match expr with
        // 상수/터미널
        | Const _ | Terminal _ -> expr

        // 단항
        | Unary (op, e) ->
            let e' = fold e
            match e' with
            | Const (v, _t) ->
                let name = OperatorMapping.mapUnary op
                if isPureFunction name then
                    try
                        let res = BuiltinFunctions.call name [v] None
                        match tryInferType res with
                        | Some t -> Const(res, t)
                        | None   -> Unary(op, e') // null 등은 타입 모호 → 유지
                    with _ ->
                        Unary(op, e')
                else
                    Unary(op, e')
            | _ -> Unary(op, e')

        // 이항
        | Binary (op, l, r) ->
            let l' = fold l
            let r' = fold r
            match l', r' with
            | Const (lv, _), Const (rv, _) ->
                let name = OperatorMapping.mapBinary op
                if isPureFunction name then
                    try
                        let res = BuiltinFunctions.call name [lv; rv] None
                        match tryInferType res with
                        | Some t -> Const(res, t)
                        | None   -> Binary(op, l', r')
                    with _ ->
                        Binary(op, l', r')
                else
                    Binary(op, l', r')
            | _ ->
                // 간단한 불리언 항등식
                match op, l', r' with
                | o, Const (v, DsDataType.TBool), e
                | o, e, Const (v, DsDataType.TBool) ->
                    let vb = unbox<bool> v
                    match OperatorMapping.opName o, vb with
                    | "AND", true  -> e
                    | "AND", false -> Const(false :> obj, DsDataType.TBool)
                    | "OR" , true  -> Const(true  :> obj, DsDataType.TBool)
                    | "OR" , false -> e
                    | _ -> Binary(op, l', r')
                | _ -> Binary(op, l', r')

        // 함수 호출
        | Function (name, args) ->
            let args' = args |> List.map constantFold
            if isPureFunction name &&
               args' |> List.forall (function Const _ -> true | _ -> false) then
                let values = args' |> List.map (function Const(v, _) -> v | _ -> null)
                try
                    let res = BuiltinFunctions.call name values None
                    match tryInferType res with
                    | Some t -> Const(res, t)
                    | None   -> Function(name, args')
                with _ ->
                    Function(name, args')
            else
                Function(name, args')

    // ── 대수적 단순화 ─────────────────────────────────────────────────────
    let rec algebraicSimplify (expr: DsExpr) : DsExpr =
        match expr with
        // NOT (NOT x) => x
        | Unary (op1, Unary (op2, e)) when OperatorMapping.opName op1 = "NOT" && OperatorMapping.opName op2 = "NOT" ->
            algebraicSimplify e

        // x = x => true,  x <> x => false
        | Binary (op, l, r) when obj.ReferenceEquals(l, r) ->
            match OperatorMapping.opName op with
            | "EQ" -> Const(true  :> obj, DsDataType.TBool)
            | "NE" -> Const(false :> obj, DsDataType.TBool)
            | _    -> Binary(op, algebraicSimplify l, algebraicSimplify r)

        // 재귀
        | Unary  (op, e)        -> Unary (op, algebraicSimplify e)
        | Binary (op, l, r)     -> Binary(op, algebraicSimplify l, algebraicSimplify r)
        | Function (n, args)    -> Function(n, args |> List.map algebraicSimplify)
        | e                     -> e
    // ── 고정점까지 반복 최적화 ────────────────────────────────────────────
    let optimize (expr: DsExpr) : DsExpr =
        let mutable cur = expr
        let mutable running = true
        while running do
            let next = cur |> constantFold |> algebraicSimplify
            if next = cur then running <- false else cur <- next
        cur
