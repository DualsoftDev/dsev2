namespace Ev2.Cpu.Runtime

open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// Builtin Function Registry
// ─────────────────────────────────────────────────────────────────────
// Map 기반의 함수 레지스트리로 리팩토링
// 함수 추가/수정이 용이하고 타입 안전성 향상
// ─────────────────────────────────────────────────────────────────────

module BuiltinFunctions =

    /// <summary>
    /// 함수 시그니처 - 내장 함수의 인자 개수 제약 정의
    /// </summary>
    type FunctionSignature =
        /// <summary>최소 1개 이상의 가변 인자 (예: ADD, MUL, MAX)</summary>
        | Variadic1Plus
        /// <summary>정확히 N개의 인자 (예: ABS는 Exact 1, DIV는 Exact 2)</summary>
        | Exact of int
        /// <summary>최소~최대 인자 범위 (예: ROUND는 Range(1, 2))</summary>
        | Range of min:int * max:int

    /// <summary>
    /// 내장 함수 디스크립터 - 함수 시그니처와 구현을 포함
    /// </summary>
    type BuiltinFunctionDescriptor = {
        /// <summary>함수 인자 개수 제약</summary>
        Signature: FunctionSignature
        /// <summary>함수 구현 (인자 리스트 → 실행 컨텍스트 → 결과)</summary>
        Implementation: obj list -> ExecutionContext option -> obj
    }

    /// 함수 레지스트리 빌더 헬퍼
    let private makeFunc sig' impl = { Signature = sig'; Implementation = impl }
    let private exact1 impl = makeFunc (Exact 1) (fun args ctx -> impl (List.head args) ctx)
    let private exact2 impl = makeFunc (Exact 2) (fun args ctx -> match args with [a;b] -> impl a b ctx | _ -> failwith "Internal error")
    let private exact3 impl = makeFunc (Exact 3) (fun args ctx -> match args with [a;b;c] -> impl a b c ctx | _ -> failwith "Internal error")
    let private variadic impl = makeFunc Variadic1Plus impl

    /// 전역 함수 레지스트리 (초기화 시 한 번만 생성)
    let private registry: Map<string, BuiltinFunctionDescriptor> =
        [
            // ═══════════════════════════════════════════════════════════════════
            // 산술 연산 (Arithmetic Operations)
            // ═══════════════════════════════════════════════════════════════════
            "ADD", variadic (fun args _ -> args |> List.reduce Arithmetic.add)
            "SUB", exact2 (fun a b _ -> Arithmetic.sub a b)
            "MUL", variadic (fun args _ -> args |> List.reduce Arithmetic.mul)
            "DIV", exact2 (fun a b _ -> Arithmetic.divide a b)
            "MOD", exact2 (fun a b _ -> Arithmetic.modulo a b)
            "POW", exact2 (fun a b _ -> Arithmetic.power a b)

            // ═══════════════════════════════════════════════════════════════════
            // 수학 함수 (Mathematical Functions)
            // ═══════════════════════════════════════════════════════════════════
            "ABS",     exact1 (fun v _ -> MathFunctions.abs v)
            "NEG",     exact1 (fun v _ -> MathFunctions.neg v)
            "MAX",     variadic (fun args _ -> args |> List.reduce (fun a b -> if Comparison.gt a b then a else b))
            "MIN",     variadic (fun args _ -> args |> List.reduce (fun a b -> if Comparison.lt a b then a else b))
            "CLAMP",   exact3 (fun v lo hi _ -> MathFunctions.clamp v lo hi)
            "SQRT",    exact1 (fun v _ -> MathFunctions.sqrt v)
            "ROUND",   makeFunc (Range(1, 2)) (fun args _ -> MathFunctions.round args)
            "FLOOR",   exact1 (fun v _ -> MathFunctions.floor v)
            "CEIL",    exact1 (fun v _ -> MathFunctions.ceiling v)
            "CEILING", exact1 (fun v _ -> MathFunctions.ceiling v)

            // ═══════════════════════════════════════════════════════════════════
            // 논리 연산 (Logical Operations)
            // ═══════════════════════════════════════════════════════════════════
            "AND", variadic (fun args _ -> box (args |> List.forall TypeHelpers.toBool))
            "OR",  variadic (fun args _ -> box (args |> List.exists TypeHelpers.toBool))
            "NOT", exact1 (fun v _ -> box (not (TypeHelpers.toBool v)))
            "XOR", exact2 (fun a b _ -> box (TypeHelpers.toBool a <> TypeHelpers.toBool b))

            // ═══════════════════════════════════════════════════════════════════
            // 비교 연산 (Comparison Operations)
            // ═══════════════════════════════════════════════════════════════════
            "EQ", exact2 (fun a b _ -> box (Comparison.eq a b))
            "NE", exact2 (fun a b _ -> box (Comparison.ne a b))
            "GT", exact2 (fun a b _ -> box (Comparison.gt a b))
            "GE", exact2 (fun a b _ -> box (Comparison.ge a b))
            "LT", exact2 (fun a b _ -> box (Comparison.lt a b))
            "LE", exact2 (fun a b _ -> box (Comparison.le a b))

            // ═══════════════════════════════════════════════════════════════════
            // 문자열 함수 (String Functions)
            // ═══════════════════════════════════════════════════════════════════
            "CONCAT",    variadic (fun args _ -> StringFunctions.concat args)
            "LENGTH",    exact1 (fun v _ -> StringFunctions.length v)
            "SUBSTR",    makeFunc (Range(2, 3)) (fun args _ -> StringFunctions.substring args)
            "SUBSTRING", makeFunc (Range(2, 3)) (fun args _ -> StringFunctions.substring args)
            "MID",       exact3 (fun str start len _ -> StringFunctions.substring [str; start; len])
            "LEFT",      exact2 (fun str len _ -> StringFunctions.left [str; len])
            "RIGHT",     exact2 (fun str len _ -> StringFunctions.right [str; len])
            "FIND",      exact2 (fun str search _ -> StringFunctions.find [str; search])
            "UPPER",     exact1 (fun v _ -> StringFunctions.upper v)
            "LOWER",     exact1 (fun v _ -> StringFunctions.lower v)
            "TRIM",      exact1 (fun v _ -> StringFunctions.trim v)
            "REPLACE",   exact3 (fun str old new' _ -> StringFunctions.replace [str; old; new'])

            // ═══════════════════════════════════════════════════════════════════
            // 타입 변환 (Type Conversion)
            // ═══════════════════════════════════════════════════════════════════
            "TOINT",    exact1 (fun v _ -> box (TypeHelpers.toInt v))
            "TODOUBLE", exact1 (fun v _ -> box (TypeHelpers.toDouble v))
            "TOSTRING", exact1 (fun v _ -> box (cachedToString v))
            "TOBOOL",   exact1 (fun v _ -> box (TypeHelpers.toBool v))

            // ═══════════════════════════════════════════════════════════════════
            // 조건부/널 처리 (Conditional/Null Handling)
            // ═══════════════════════════════════════════════════════════════════
            "IF",       exact3 (fun c t e _ -> if TypeHelpers.toBool c then t else e)
            "COALESCE", variadic (fun args _ -> args |> List.tryFind (fun v -> not (isNull v)) |> Option.defaultValue null)
            "ISNULL",   exact1 (fun v _ -> box (isNull v))

            // ═══════════════════════════════════════════════════════════════════
            // PLC 전용 함수 (PLC-Specific Functions)
            // ═══════════════════════════════════════════════════════════════════
            "MOV",  makeFunc (Range(1, 2)) (fun args ctx -> PLCFunctions.mov args ctx)
            "MOVE", makeFunc (Range(1, 2)) (fun args ctx -> PLCFunctions.mov args ctx)
            "TON",  makeFunc (Exact 3) (fun args ctx -> PLCFunctions.ton args ctx)
            "TOF",  makeFunc (Range(2, 3)) (fun args ctx -> PLCFunctions.tof args ctx)
            "TP",   makeFunc (Range(2, 3)) (fun args ctx -> PLCFunctions.tp args ctx)
            "CTU",  makeFunc (Range(2, 3)) (fun args ctx -> PLCFunctions.ctu args ctx)
            "CTD",  makeFunc (Range(2, 4)) (fun args ctx -> PLCFunctions.ctd args ctx)
            "CTUD", makeFunc (Exact 5) (fun args ctx -> PLCFunctions.ctud args ctx)

            // ═══════════════════════════════════════════════════════════════════
            // 시스템 함수 (System Functions)
            // ═══════════════════════════════════════════════════════════════════
            "PRINT",  variadic (fun args ctx -> SystemFunctions.print args ctx)
            "NOW",    makeFunc (Exact 0) (fun _ ctx -> SystemFunctions.now ctx)  // DEFECT-003 fix: pass ctx
            "RANDOM", makeFunc (Range(0, 2)) (fun args _ -> SystemFunctions.random args)
        ]
        |> Map.ofList

    /// 인자 개수 검증
    let private validateSignature (funcName: string) (signature: FunctionSignature) (argCount: int) : unit =
        match signature with
        | Variadic1Plus ->
            if argCount < 1 then
                failwithf "%s requires at least 1 argument, got %d" funcName argCount
        | Exact n ->
            if argCount <> n then
                failwithf "%s requires %d argument%s, got %d" funcName n (if n = 1 then "" else "s") argCount
        | Range (minArgs, maxArgs) ->
            if argCount < minArgs || argCount > maxArgs then
                failwithf "%s requires %d-%d arguments, got %d" funcName minArgs maxArgs argCount

    /// <summary>
    /// 내장 함수 디스패처 - 함수 이름과 인자로 내장 함수 호출
    /// </summary>
    /// <param name="name">함수 이름 (대소문자 무관, 예: "ADD", "add", "Add")</param>
    /// <param name="args">함수 인자 리스트 (obj 타입)</param>
    /// <param name="ctx">실행 컨텍스트 (PLC 타이머/카운터 등에 필요)</param>
    /// <returns>함수 실행 결과 (obj 타입)</returns>
    /// <exception cref="System.Exception">알 수 없는 함수이거나 인자 개수가 잘못된 경우</exception>
    /// <example>
    /// <code>
    /// // 두 수의 합
    /// let result = BuiltinFunctions.call "ADD" [box 10; box 20] None
    /// // result = 30
    ///
    /// // 절대값
    /// let abs = BuiltinFunctions.call "ABS" [box -42] None
    /// // abs = 42
    /// </code>
    /// </example>
    let call (name: string) (args: obj list) (ctx: ExecutionContext option) : obj =
        let funcName = name.ToUpperInvariant()
        let argCount = List.length args

        match Map.tryFind funcName registry with
        | Some descriptor ->
            // 인자 개수 검증
            validateSignature funcName descriptor.Signature argCount
            // 함수 실행
            descriptor.Implementation args ctx
        | None ->
            failwithf "Unknown function: %s" name
