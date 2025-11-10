namespace Ev2.Cpu.Core

open System
open System.Collections.Generic

// ─────────────────────────────────────────────────────────────────────
// 연산자 파싱 (Operator Parsing)
// ─────────────────────────────────────────────────────────────────────
// 문자열을 DsOp 타입으로 변환
// ─────────────────────────────────────────────────────────────────────

/// 연산자 파싱 유틸리티
module OperatorParser =

    let private all =
        [
            And; Or; Not; Xor; Nand; Nor
            Eq; Ne; Gt; Ge; Lt; Le
            Add; Sub; Mul; Div; Mod; Pow
            BitAnd; BitOr; BitXor; BitNot; ShiftLeft; ShiftRight
            Rising; Falling; Edge
            Assign; Move; Coalesce
        ]

    let private aliasPairs =
        all
        |> List.collect (fun op -> op.Aliases |> List.map (fun alias -> alias, op))

    let private aliasMap =
        let dict = Dictionary<string, DsOp>(StringComparer.OrdinalIgnoreCase)
        for alias, op in aliasPairs do
            dict.[alias] <- op
        dict

    let private aliasMapCaseSensitive =
        let dict = Dictionary<string, DsOp>(StringComparer.Ordinal)
        for alias, op in aliasPairs do
            dict.[alias] <- op
        dict

    /// 문자열을 연산자로 파싱
    let parse (s: string) : DsOp =
        if String.IsNullOrWhiteSpace(s) then
            failwith "Empty operator string"
        else
            let key = s.Trim()
            match aliasMap.TryGetValue(key) with
            | true, op -> op
            | false, _ -> failwithf "Unknown operator: '%s'" key

    /// 문자열을 연산자로 파싱 (Option 반환)
    let tryParse (s: string) : DsOp option =
        if String.IsNullOrWhiteSpace(s) then None
        else
            let key = s.Trim()
            match aliasMap.TryGetValue(key) with
            | true, op -> Some op
            | false, _ -> None

    /// 대소문자 구분 파싱
    let parseCaseSensitive (s: string) : DsOp =
        if String.IsNullOrWhiteSpace(s) then
            failwith "Empty operator string"
        else
            let key = s.Trim()
            match aliasMapCaseSensitive.TryGetValue(key) with
            | true, op -> op
            | false, _ -> failwithf "Unknown operator (case sensitive): '%s'" key
