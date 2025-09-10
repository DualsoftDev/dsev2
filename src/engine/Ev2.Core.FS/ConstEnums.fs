namespace Ev2.Core.FS

open System
open System.Text.RegularExpressions
open Newtonsoft.Json.Linq
open Newtonsoft.Json
open Dual.Common.Core.FS
open System.Runtime.CompilerServices

[<AutoOpen>]
module ConstEnums =

    type DbCallType =
        | Normal = 0
        | Parallel = 1
        | Repeat = 2

    type DbArrowType =
        | None = 0
        | Start = 1
        | Reset = 2
        | StartReset = 3

    type DbStatus4 =
        | Ready = 1
        | Going = 2
        | Finished = 3
        | Homing = 4

[<AutoOpen>]
module Ev2PreludeModule =
    let addAsSet            (arr: ResizeArray<'T>) (item: 'T)      = arr.AddAsSet(item)
    let addRangeAsSet       (arr: ResizeArray<'T>) (items: 'T seq) = arr.AddRangeAsSet(items)

[<AutoOpen>]
module ResizeArrayExtensions =

    type System.Collections.Generic.List<'T> with // VerifyAddAsSet, VerifyAddRangeAsSet
        member xs.VerifyAddAsSet(item: 'T, ?isDuplicatedPredicate: ('T -> 'T -> bool)) =
            xs.AddAsSet(item, ?isDuplicatedPredicate=isDuplicatedPredicate, onDuplicated=(fun x -> failwith $"ERROR: {x} duplicated."))

        member xs.VerifyAddRangeAsSet(items: 'T seq, ?isDuplicatedPredicate: ('T -> 'T -> bool)) =
            xs.AddRangeAsSet(items, ?isDuplicatedPredicate=isDuplicatedPredicate, onDuplicated=(fun x -> failwith $"ERROR: {x} duplicated."))

    let [<Literal>] DateFormatString = "yyyy-MM-ddTHH:mm:ss"

    type DateTime with // TruncateToSecond
        [<Extension>]
        member x.TruncateToSecond() =
            DateTime(x.Year, x.Month, x.Day,
                     x.Hour, x.Minute, x.Second,
                     x.Kind)


