namespace PLC.CodeGen.Common

open System.Diagnostics
open Dual.Common.Core.FS
open Engine.Core

[<AutoOpen>]
module FlatExpressionModule =
    type Op =
        | And
        | Or
        | Neg

        | RisingAfter
        | FallingAfter

        | OpUnit // Logical XOR 는 function 인 '<>' 로 구현됨
        | OpCompare of operator: string
        | OpArithmetic of operator: string

        member x.ToText() = sprintf "%A" x

        member x.Negate() =
            match x with
            | And -> Or
            | Or -> And
            | Neg -> OpUnit
            | OpUnit -> Neg
            | OpCompare op ->
                match op with
                | ">" -> "<="
                | ">=" -> "<"
                | "<" -> ">="
                | "<=" -> ">"
                | "==" -> "!="
                | "!=" -> "=="
                | _ -> failwithlog "ERROR"
                |> OpCompare
            | OpArithmetic _ -> failwith "ERROR: Negation not supported for Arithmetic operator."
            | _ -> failwith "ERROR"

    [<DebuggerDisplay("{ToText()}")>]
    type FlatExpression =
        /// pulse identifier 및 negation 여부 (pulse coil 은 지원하지 않을 예정)
        ///
        /// pulse : None 이면 pulse 없음, Some true 이면 rising edge, Some false 이면 falling edge
        | FlatTerminal of terminal: IExpressionizableTerminal * pulse: bool option * negated: bool

        /// N-ary Expressions : And / Or 및 terms
        | FlatNary of Op * FlatExpression list

        interface IFlatExpression

        interface IType with
            member x.DataType = x.DataType

        member x.DataType =
            match x with
            | FlatTerminal(terminal, _pulse, _neg) -> terminal.DataType
            | FlatNary(_op, arg0::_) -> arg0.DataType
            | _ -> failwithlog "ERROR"

        member x.ToText() =
            match x with
            | FlatTerminal(value, _pulse, neg) -> sprintf "%s%s" (if neg then "!" else "") (value.ToText())
            | FlatNary(op, terms) ->
                let termsStr = terms |> Seq.map (fun t -> t.ToText()) |> String.concat ", "
                sprintf "%s(%s)" (op.ToText()) termsStr

        member x.Negate() =
            match x with
            | FlatTerminal(value, pulse, neg) -> FlatTerminal(value, pulse, not neg)
            | FlatNary(op, [ FlatTerminal(t, p, n) ]) when op = Neg || op = OpUnit ->
                let negated = if op = Neg then n else not n
                FlatTerminal(t, p, negated)

            | FlatNary(_op, [ FlatTerminal(_t, _p, _n) ]) -> failwithlog "ERROR"

            | FlatNary(op, terms) ->
                let opNeg = op.Negate()
                let termsNeg = terms |> map (fun t -> t.Negate())
                FlatNary(opNeg, termsNeg)

(*
    let flatten (exp: IExpression) = exp.Flatten() :?> FlatExpression
    fwdFlattenExpression(..)
*)
