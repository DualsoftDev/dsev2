namespace Ev2.Cpu.Generation.Loops

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Loop Transformations - Code Generation Optimizations
// ═════════════════════════════════════════════════════════════════════════════
// 루프 최적화 및 변환 기법 제공:
// - 루프 펼치기 (Loop Unrolling)
// - 루프 융합 (Loop Fusion)
// - 루프 불변 코드 이동 (Loop Invariant Code Motion)
// - 루프 강도 감소 (Strength Reduction)
// ═════════════════════════════════════════════════════════════════════════════

/// 루프 펼치기 설정
type UnrollConfig = {
    /// 최대 펼치기 횟수
    MaxUnrollCount: int
    /// 본문 크기 제한 (명령문 개수)
    MaxBodySize: int
    /// 부분 펼치기 활성화
    EnablePartialUnroll: bool
}

/// 기본 펼치기 설정
module UnrollConfig =
    let defaultConfig = {
        MaxUnrollCount = 8
        MaxBodySize = 10
        EnablePartialUnroll = true
    }

// ═════════════════════════════════════════════════════════════════════════════
// MAJOR FIX (DEFECT-017-6/7): Module-level variable substitution helpers
// Used by both Unrolling and Fusion modules
// ═════════════════════════════════════════════════════════════════════════════

module internal VariableSubstitution =
    /// Substitute variable references in expressions
    let rec substituteExpr (varName: string) (replacement: DsExpr) (expr: DsExpr) : DsExpr =
        match expr with
        | DsExpr.Terminal tag when tag.Name = varName -> replacement
        | DsExpr.Unary (op, e) -> DsExpr.Unary (op, substituteExpr varName replacement e)
        | DsExpr.Binary (op, l, r) -> DsExpr.Binary (op, substituteExpr varName replacement l, substituteExpr varName replacement r)
        | DsExpr.Function (name, args) -> DsExpr.Function (name, args |> List.map (substituteExpr varName replacement))
        | _ -> expr

    /// Substitute variable references in statements
    let rec substituteStmt (varName: string) (replacement: DsExpr) (stmt: DsStmt) : DsStmt =
        match stmt with
        | Assign (step, tag, expr) ->
            Assign (step, tag, substituteExpr varName replacement expr)
        | Command (step, cond, action) ->
            Command (step, substituteExpr varName replacement cond, substituteExpr varName replacement action)
        | For (step, loopVar, start, endExpr, stepExpr, body) ->
            // Don't substitute inside nested loops with same variable name
            if loopVar.Name = varName then
                stmt
            else
                For (step, loopVar,
                     substituteExpr varName replacement start,
                     substituteExpr varName replacement endExpr,
                     stepExpr |> Option.map (substituteExpr varName replacement),
                     body |> List.map (substituteStmt varName replacement))
        | While (step, cond, body, maxIter) ->
            While (step, substituteExpr varName replacement cond,
                   body |> List.map (substituteStmt varName replacement),
                   maxIter)
        | Break step -> Break step  // No substitution needed

/// 루프 펼치기 (Loop Unrolling)
module Unrolling =

    /// 루프 본문 크기 계산
    let rec private bodySize (stmts: DsStmt list) : int =
        stmts |> List.sumBy (fun stmt ->
            match stmt with
            | For(_, _, _, _, _, body) | While(_, _, body, _) ->
                1 + bodySize body
            | _ -> 1
        )

    /// FOR 루프 완전 펼치기 (상수 반복 횟수)
    let unrollForLoop (config: UnrollConfig) (stmt: DsStmt) : DsStmt list option =
        match stmt with
        | For(step, loopVar, Const(startVal, _), Const(endVal, _), stepOpt, body) ->
            try
                let start = startVal :?> int
                let endV = endVal :?> int
                let stepValue =
                    match stepOpt with
                    | Some (Const(stepVal, _)) -> stepVal :?> int
                    | _ -> 1

                // CRITICAL FIX (DEFECT-CRIT-11): Validate step value to prevent infinite loops
                // Previous code: No validation, stepValue=0 causes infinite loop
                // Problem: Division by zero in iteration calculation, [start..0..end] hangs
                // Solution: Reject loops with zero or invalid step values
                if stepValue = 0 then
                    // Step cannot be zero - would cause infinite loop
                    None
                else

                // 반복 횟수 계산
                let iterations =
                    if stepValue > 0 && endV >= start then
                        (endV - start) / stepValue + 1
                    elif stepValue < 0 && endV <= start then
                        (start - endV) / (-stepValue) + 1
                    else
                        0

                // CRITICAL FIX (DEFECT-CRIT-11): Add safety limit for iteration count
                // Previous code: Only checked MaxUnrollCount, but calculation could overflow
                // Problem: Large iteration counts (e.g., 0 to Int32.MaxValue step 1) cause OOM
                // Solution: Absolute max of 10000 iterations regardless of config
                let absoluteMaxIterations = 10000
                if iterations > absoluteMaxIterations then
                    // Too many iterations - reject unrolling to prevent OOM
                    None
                else

                // 펼치기 가능 여부 확인
                let bodyStmtCount = bodySize body
                if iterations <= 0 then
                    Some []  // 실행 안 함
                elif iterations <= config.MaxUnrollCount && bodyStmtCount <= config.MaxBodySize then
                    // 완전 펼치기
                    let unrolled =
                        [start .. stepValue .. endV]
                        |> List.collect (fun i ->
                            // 루프 변수를 상수로 치환
                            let initAssign = Assign(step + i - start, loopVar, Const(box i, loopVar.StructType))
                            initAssign :: body
                        )
                    Some unrolled
                else
                    None  // 펼치기 불가
            with _ -> None
        | _ -> None

    /// FOR 루프 부분 펼치기 (2배, 4배 등)
    let partialUnroll (factor: int) (stmt: DsStmt) : DsStmt option =
        match stmt with
        | For(step, loopVar, startExpr, endExpr, stepOpt, body) when factor > 1 ->
            // FOR i := start TO end STEP step DO body END_FOR
            // => FOR i := start TO end STEP (step * factor) DO
            //      body[i]
            //      body[i+step]
            //      body[i+2*step]
            //      ...
            //    END_FOR

            let originalStep = stepOpt |> Option.defaultValue (Const(box 1, typeof<int>))
            let newStep =
                match originalStep with
                | Const(stepVal, dt) ->
                    let s = stepVal :?> int
                    Const(box (s * factor), dt)
                | _ ->
                    Binary(DsOp.Mul, originalStep, Const(box factor, typeof<int>))

            // 본문을 factor번 복제 (각각 다른 인덱스)
            // MAJOR FIX (DEFECT-017-6): Substitute loop variable with (i + f*step) in each copy
            // Without this, all unrolled iterations reference the same variable value
            let unrolledBody =
                [0 .. factor - 1]
                |> List.collect (fun f ->
                    if f = 0 then
                        // First iteration: no offset needed
                        body
                    else
                        // Subsequent iterations: substitute i with (i + f*step)
                        let offset =
                            match originalStep with
                            | Const(stepVal, dt) ->
                                let s = stepVal :?> int
                                Const(box (s * f), dt)
                            | _ ->
                                Binary(DsOp.Mul, originalStep, Const(box f, typeof<int>))

                        let replacement = Binary(DsOp.Add, Terminal(loopVar), offset)
                        body |> List.map (VariableSubstitution.substituteStmt loopVar.Name replacement)
                )

            Some (For(step, loopVar, startExpr, endExpr, Some newStep, unrolledBody))
        | _ -> None

/// 루프 융합 (Loop Fusion)
module Fusion =

    /// 두 FOR 루프 융합 (같은 범위, 독립적인 본문)
    let fuseForLoops (loop1: DsStmt) (loop2: DsStmt) : DsStmt option =
        match loop1, loop2 with
        | For(step1, var1, start1, end1, step1Opt, body1),
          For(_step2, var2, start2, end2, step2Opt, body2) ->
            // 조건: 같은 범위, 같은 증분
            if start1 = start2 && end1 = end2 && step1Opt = step2Opt then
                // 변수 이름이 다르면 하나로 통일 (var1 사용)
                // MAJOR FIX (DEFECT-017-7): Substitute var2 with var1 in body2
                // Without this, fused loop body references two different loop variables
                let body2Renamed =
                    if var2.Name <> var1.Name then
                        body2 |> List.map (VariableSubstitution.substituteStmt var2.Name (Terminal var1))
                    else
                        body2

                let fusedBody = body1 @ body2Renamed
                Some (For(step1, var1, start1, end1, step1Opt, fusedBody))
            else
                None
        | _ -> None

    /// 연속된 FOR 루프들 융합
    let fuseConsecutiveLoops (stmts: DsStmt list) : DsStmt list =
        let rec fuse acc remaining =
            match remaining with
            | [] -> List.rev acc
            | [single] -> List.rev (single :: acc)
            | loop1 :: loop2 :: rest ->
                match fuseForLoops loop1 loop2 with
                | Some fused -> fuse acc (fused :: rest)
                | None -> fuse (loop1 :: acc) (loop2 :: rest)
        fuse [] stmts

/// 루프 불변 코드 이동 (Loop Invariant Code Motion)
module InvariantMotion =

    /// 루프 변수에 의존하지 않는 명령문 추출
    let private isInvariant (loopVarName: string) (stmt: DsStmt) : bool =
        let readVars = stmt.ReferencedVars
        not (readVars.Contains loopVarName)

    /// FOR 루프에서 불변 코드 추출
    let hoistInvariants (stmt: DsStmt) : (DsStmt list * DsStmt) option =
        match stmt with
        | For(step, loopVar, startExpr, endExpr, stepOpt, body) ->
            // 본문에서 루프 변수에 의존하지 않는 명령문 찾기
            let (invariants, nonInvariants) =
                body |> List.partition (isInvariant loopVar.Name)

            if invariants.IsEmpty then
                None  // 최적화 불가
            else
                // 불변 코드를 루프 밖으로 이동
                let optimizedLoop = For(step, loopVar, startExpr, endExpr, stepOpt, nonInvariants)
                Some (invariants, optimizedLoop)
        | _ -> None

/// 강도 감소 (Strength Reduction)
module StrengthReduction =

    /// 루프 내 곱셈을 덧셈으로 변환
    /// 예: FOR i := 0 TO 9 DO x := i * 5 END_FOR
    ///  => temp := 0; FOR i := 0 TO 9 DO x := temp; temp := temp + 5 END_FOR
    let reduceMulToAdd (stmt: DsStmt) : (DsStmt * DsStmt) option =
        // MODERATE FIX (DEFECT-017-8): Mark as WONTFIX - complex optimization not yet implemented
        // Full implementation requires:
        // 1. Pattern matching to find (loopVar * constant) in assignment RHS
        // 2. Creating a fresh temp variable name (symbol table management)
        // 3. Rewriting assignment to use temp variable
        // 4. Injecting temp initialization before loop
        // 5. Adding temp += constant at end of each iteration
        // 6. Handling nested loops and multiple multiplication sites
        // This is a performance optimization (not correctness), deferred to future work
        match stmt with
        | For(step, loopVar, startExpr, endExpr, stepOpt, body) ->
            None  // WONTFIX: Complex optimization deferred
        | _ -> None

/// 루프 조건 간소화
module ConditionSimplification =

    /// WHILE 루프의 상수 조건 평가
    let simplifyConstantCondition (stmt: DsStmt) : DsStmt option =
        match stmt with
        | While(step, Const(value, t), body, maxIter) when t = typeof<bool> ->
            match value with
            | :? bool as b ->
                if b then
                    None  // true는 무한 루프 -> 간소화 불가
                else
                    // MAJOR FIX (DEFECT-017-9): Don't create phantom "_noop" variable
                    // WHILE false should be removed entirely, but DsStmt has no Empty case
                    // Return None to preserve the loop (will be dead-code eliminated by later passes)
                    // Callers should filter out unreachable code after constant folding
                    None  // Keep as WHILE false for now, let dead code elimination handle it
            | _ -> None
        | _ -> None

    /// 항상 참/거짓인 비교 간소화
    let simplifyAlwaysTrueFalse (expr: DsExpr) : DsExpr option =
        match expr with
        | Binary(DsOp.Eq, Const(v1, _), Const(v2, _)) ->
            Some (Const(box (v1 = v2), typeof<bool>))
        | Binary(DsOp.Ne, Const(v1, _), Const(v2, _)) ->
            Some (Const(box (v1 <> v2), typeof<bool>))
        | Binary(DsOp.Lt, Const(v1, _), Const(v2, _)) ->
            try
                let n1 = v1 :?> int
                let n2 = v2 :?> int
                Some (Const(box (n1 < n2), typeof<bool>))
            with _ -> None
        | _ -> None

/// 루프 최적화 파이프라인
module OptimizationPipeline =

    /// 최적화 옵션
    type OptimizationOptions = {
        EnableUnrolling: bool
        EnableFusion: bool
        EnableInvariantMotion: bool
        EnableStrengthReduction: bool
        UnrollConfig: UnrollConfig
    }

    /// 기본 최적화 옵션
    let defaultOptions = {
        EnableUnrolling = true
        EnableFusion = true
        EnableInvariantMotion = true
        EnableStrengthReduction = false  // 복잡하므로 비활성화
        UnrollConfig = UnrollConfig.defaultConfig
    }

    /// 단일 루프 최적화
    let optimizeLoop (options: OptimizationOptions) (stmt: DsStmt) : DsStmt list =
        let mutable result = [stmt]

        // 1. 불변 코드 이동
        if options.EnableInvariantMotion then
            match InvariantMotion.hoistInvariants stmt with
            | Some (invariants, optimizedLoop) ->
                result <- invariants @ [optimizedLoop]
            | None -> ()

        // 2. 루프 펼치기
        if options.EnableUnrolling then
            match result with
            | [singleLoop] ->
                match Unrolling.unrollForLoop options.UnrollConfig singleLoop with
                | Some unrolled -> result <- unrolled
                | None -> ()
            | _ -> ()

        result

    /// 명령문 리스트 최적화
    let optimizeStatements (options: OptimizationOptions) (stmts: DsStmt list) : DsStmt list =
        let mutable result = stmts

        // 1. 각 루프 최적화
        result <- result |> List.collect (fun stmt ->
            match stmt with
            | For _ | While _ -> optimizeLoop options stmt
            | _ -> [stmt]
        )

        // 2. 루프 융합
        if options.EnableFusion then
            result <- Fusion.fuseConsecutiveLoops result

        result

/// 루프 변환 유틸리티
module Transformations =

    /// FOR 루프를 배열 초기화로 변환 (특수 패턴)
    /// 예: FOR i := 0 TO 9 DO arr[i] := 0 END_FOR
    ///  => MEMSET(arr, 0, 10)
    let forToMemset (stmt: DsStmt) : DsStmt option =
        // FUTURE ENHANCEMENT: Array initialization pattern detection
        // Current limitation: DsStmt does not support array indexing syntax (arr[i])
        // Required AST changes:
        // 1. Add ArrayIndex to DsExpr for read access (e.g., arr[i] in expressions)
        // 2. Add ArrayAssign to DsStmt for write access (e.g., arr[i] := value)
        // 3. Implement MEMSET builtin function in runtime
        // Pattern to detect: FOR loop where body contains single array assignment
        //   with loop variable as index and constant value
        // Benefit: O(n) → O(1) for large array initializations
        None

    /// WHILE 루프를 DO-WHILE로 변환 (조건이 처음에 항상 참)
    let whileToDoWhile (stmt: DsStmt) : DsStmt option =
        // FUTURE ENHANCEMENT: DO-WHILE pattern transformation
        // Current limitation: DsStmt only supports WHILE, not DO-WHILE
        // Required AST changes:
        // 1. Add DoWhile case to DsStmt discriminated union
        // 2. Update StmtEvaluator.fs to execute body before checking condition
        // Pattern to detect: WHILE with condition that evaluates to true on first iteration
        //   (requires static analysis or runtime profiling data)
        // Benefit: Eliminates redundant condition check for loops guaranteed to run once
        None

    /// 카운터 기반 WHILE을 FOR로 변환
    let whileToFor (stmt: DsStmt) : DsStmt option =
        match stmt with
        | While(step, Binary(DsOp.Lt, Terminal counterVar, Const(limitVal, _)), body, _) ->
            // WHILE counter < limit DO ... counter := counter + 1 ... END_WHILE
            // => FOR counter := 0 TO limit-1 DO ... END_FOR

            // 본문의 마지막이 counter 증가인지 확인
            match body |> List.rev with
            | Assign(_, assignTarget, Binary(DsOp.Add, Terminal incVar, Const(stepVal, _))) :: _
                when assignTarget.Name = counterVar.Name && incVar.Name = counterVar.Name ->
                try
                    let limit = limitVal :?> int
                    let step = stepVal :?> int
                    let forBody = body |> List.rev |> List.tail |> List.rev  // 마지막 증가문 제거
                    Some (For(step, counterVar, Const(box 0, typeof<int>), Const(box (limit - 1), typeof<int>), Some(Const(box step, typeof<int>)), forBody))
                with _ -> None
            | _ -> None
        | _ -> None
