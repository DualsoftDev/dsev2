namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// Math Functions
// ─────────────────────────────────────────────────────────────────────
// 수학 함수: abs, neg, sqrt, round, floor, ceiling, clamp
// ─────────────────────────────────────────────────────────────────────

module MathFunctions =

    let abs (v: obj) =
        match v with
        | :? int as i   -> box (Math.Abs i)
        | :? float as d -> box (Math.Abs d)
        | _             -> box (Math.Abs (TypeHelpers.toDouble v))

    let neg (v: obj) =
        match v with
        | :? int as i   -> box (-i)
        | :? float as d -> box (-d)
        | _             -> box (-(TypeHelpers.toDouble v))

    let sqrt (v: obj) = box (Math.Sqrt(TypeHelpers.toDouble v))

    let round (args: obj list) =
        match args with
        | [v]                  -> box (Math.Round(TypeHelpers.toDouble v))  // Return double, not int
        | [v; :? int as n]     -> box (Math.Round(TypeHelpers.toDouble v, n))
        | _                    -> failwith "ROUND requires 1-2 arguments"

    let floor (v: obj) = box (Math.Floor(TypeHelpers.toDouble v))

    let ceiling (v: obj) = box (Math.Ceiling(TypeHelpers.toDouble v))

    let clamp (v: obj) (lo: obj) (hi: obj) =
        if Comparison.lt v lo then lo
        elif Comparison.gt v hi then hi
        else v
