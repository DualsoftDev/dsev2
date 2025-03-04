namespace PLC.CodeGen.Common

open Dual.Ev2

[<AutoOpen>]
module FakeTypesModule =

    type OpComp =
        | GT
        | GE
        | EQ
        | LE
        | LT
        | NE

        member x.ToText() =
            match x with
            | GT -> "GT" // <
            | GE -> "GE" // <=
            | EQ -> "EQ" // ==
            | LE -> "LE" // >=
            | LT -> "LT" // >
            | NE -> "NE" // !=
