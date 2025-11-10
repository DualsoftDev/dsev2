namespace Ev2.Cpu.Runtime

open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// Operator Mapping
// ─────────────────────────────────────────────────────────────────────
// DsOp → BuiltinFunctions 이름 매핑을 위한 공유 모듈
// ExprEvaluator와 ExprOptimizer 간 코드 중복 제거
// ─────────────────────────────────────────────────────────────────────

module OperatorMapping =

    /// DsOp를 대문자 이름으로 변환
    /// 예: Add → "ADD", Sub → "SUB", Custom("Foo") → "CUSTOM"
    let opName (op: DsOp) : string =
        let raw = sprintf "%A" op
        let nameOnly =
            let i = raw.IndexOf '('
            if i >= 0 then raw.Substring(0, i) else raw
        nameOnly.Trim().ToUpperInvariant()

    /// 단항 연산자를 Builtin 함수 이름으로 매핑
    let mapUnary (op: DsOp) : string =
        match opName op with
        | "NEG" | "-"                 -> "NEG"
        | "NOT" | "!"                 -> "NOT"
        | "TOINT"                     -> "TOINT"
        | "TODOUBLE" | "TOFLOAT"      -> "TODOUBLE"
        | "TOSTRING"                  -> "TOSTRING"
        | "TOBOOL"                    -> "TOBOOL"
        | other                       -> other

    /// 이항 연산자를 Builtin 함수 이름으로 매핑
    let mapBinary (op: DsOp) : string =
        match opName op with
        // 산술
        | "ADD" | "+"                 -> "ADD"
        | "SUB" | "-"                 -> "SUB"
        | "MUL" | "*"                 -> "MUL"
        | "DIV" | "/"                 -> "DIV"
        | "MOD" | "%"                 -> "MOD"
        | "POW" | "^"                 -> "POW"
        // 논리
        | "AND" | "&&"                -> "AND"
        | "OR"  | "||"                -> "OR"
        | "XOR"                       -> "XOR"
        // 비교
        | "EQ"  | "="  | "=="         -> "EQ"
        | "NE"  | "<>" | "!="         -> "NE"
        | "LT"  | "<"                 -> "LT"
        | "GT"  | ">"                 -> "GT"
        | "LE"  | "<="                -> "LE"
        | "GE"  | ">="                -> "GE"
        // 문자열
        | "CONCAT"                    -> "CONCAT"
        // 그 외 → 동일 이름으로 Builtin에 위임
        | other                       -> other
