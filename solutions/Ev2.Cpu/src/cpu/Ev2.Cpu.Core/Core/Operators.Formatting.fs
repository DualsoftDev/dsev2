namespace Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// 연산자 포맷팅 (Operator Formatting)
// ─────────────────────────────────────────────────────────────────────
// DsOp를 문자열로 변환
// ─────────────────────────────────────────────────────────────────────

/// 연산자 포맷팅 유틸리티
module OperatorFormatter =

    /// 연산자를 문자열로 변환
    let format (op: DsOp) : string = op.ToString()

    /// 연산자를 주 별칭으로 변환
    let formatPrimary (op: DsOp) : string =
        match op.Aliases with
        | primary :: _ -> primary
        | [] -> op.ToString()

    /// 연산자를 심볼 형식으로 변환 (기호 우선)
    let formatAsSymbol (op: DsOp) : string =
        match op with
        | And -> "&&" | Or -> "||" | Not -> "!"
        | Xor -> "⊕" | Nand -> "NAND" | Nor -> "NOR"
        | Eq -> "==" | Ne -> "!=" | Gt -> ">"
        | Ge -> ">=" | Lt -> "<" | Le -> "<="
        | Add -> "+" | Sub -> "-" | Mul -> "*"
        | Div -> "/" | Mod -> "%" | Pow -> "^"
        | BitAnd -> "&" | BitOr -> "|" | BitXor -> "⊕"
        | BitNot -> "~" | ShiftLeft -> "<<" | ShiftRight -> ">>"
        | Rising -> "↑" | Falling -> "↓" | Edge -> "⇅"
        | Assign -> ":=" | Move -> "MOV" | Coalesce -> "??"

    /// 연산자를 단어 형식으로 변환 (키워드 우선)
    let formatAsWord (op: DsOp) : string =
        match op with
        | And -> "AND" | Or -> "OR" | Not -> "NOT"
        | Xor -> "XOR" | Nand -> "NAND" | Nor -> "NOR"
        | Eq -> "EQ" | Ne -> "NE" | Gt -> "GT"
        | Ge -> "GE" | Lt -> "LT" | Le -> "LE"
        | Add -> "ADD" | Sub -> "SUB" | Mul -> "MUL"
        | Div -> "DIV" | Mod -> "MOD" | Pow -> "POW"
        | BitAnd -> "BITAND" | BitOr -> "BITOR" | BitXor -> "BITXOR"
        | BitNot -> "BITNOT" | ShiftLeft -> "SHL" | ShiftRight -> "SHR"
        | Rising -> "RISING" | Falling -> "FALLING" | Edge -> "EDGE"
        | Assign -> "ASSIGN" | Move -> "MOVE" | Coalesce -> "COALESCE"
