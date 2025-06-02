namespace Ev2.Core.FS

[<AutoOpen>]
module ConstEnums =

    type DbCallType =
        | Normal = 0
        | Parallel = 1
        | Repeat = 2

    type DbDataType =
        | None = 0
        | Bool = 1

        | Int8 = 30
        | Int16 = 31
        | Int32 = 32
        | Int64 = 33
        | Uint8 = 34
        | Uint16 = 35
        | Uint32 = 36
        | Uint64 = 37

        | Single = 48
        | Double = 49

    type DbArrowType =
        | None = 0
        | Start = 1
        | Reset = 2

[<AutoOpen>]
module Ev2PreludeModule =
    open Dual.Common.Core.FS

    let addAsSet            (arr: ResizeArray<'T>) (item: 'T)      = arr.AddAsSet(item)
    let addRangeAsSet       (arr: ResizeArray<'T>) (items: 'T seq) = arr.AddRangeAsSet(items)
    let verifyAddAsSet      (arr: ResizeArray<'T>) (item: 'T)      = arr.AddAsSet(item, fun x -> failwith $"ERROR: {x} duplicated.")
    let verifyAddRangeAsSet (arr: ResizeArray<'T>) (items: 'T seq) = arr.AddRangeAsSet(items, fun x -> failwith $"ERROR: {x} duplicated.")
