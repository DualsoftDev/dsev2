namespace Ev2.Core.FS

[<AutoOpen>]
module ConstEnums =

    type DbCallType =
        | Normal = 0
        | Parallel = 1
        | Repeat = 2

    /// Value Type
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

    type DbRangeType =
        | None = 0
        /// 단일 값 지정
        | Single  = 1
        /// 미포함 최소값 지정. "(3..".  x > 3
        | min_    = 2
        /// 포함 최소값 지정. "[3..".  x >= 3
        | MIN_    = 3
        /// 미포함 최대값 지정. "..3)".  x < 3
        | _max    = 4
        /// 포함 최대값 지정. "..3]".  x <= 3
        | _MAX    = 5
        /// 미포함 최소, 미포함 최대값 지정. "(3..6)".  3 < x < 6
        | min_max = 6
        /// 미포함 최소, 포함 최대값 지정. "(3..6]".  3 < x <= 6
        | min_MAX = 7
        /// 포함 최소, 미포함 최대값 지정. "[3..6)".  3 <= x < 6
        | MIN_max = 8
        /// 포함 최소, 포함 최대값 지정. "[3..6]".  3 <= x <= 6
        | MIN_MAX = 9

        /// x < 3 or 6 < x
        | max_min = 10
        /// x <= 3 or 6 < x
        | MAX_min = 11
        /// x < 3 or 6 <= x
        | max_MIN = 12
        /// x <= 3 or 6 <= x
        | MAX_MIN = 13



    type DbArrowType =
        | None = 0
        | Start = 1
        | Reset = 2

    type DbStatus4 =
        | Ready = 1
        | Going = 2
        | Finished = 3
        | Homing = 4

[<AutoOpen>]
module Ev2PreludeModule =
    open Dual.Common.Core.FS

    let addAsSet            (arr: ResizeArray<'T>) (item: 'T)      = arr.AddAsSet(item)
    let addRangeAsSet       (arr: ResizeArray<'T>) (items: 'T seq) = arr.AddRangeAsSet(items)
    let verifyAddAsSet      (arr: ResizeArray<'T>) (item: 'T)      = arr.AddAsSet(item, fun x -> failwith $"ERROR: {x} duplicated.")
    let verifyAddRangeAsSet (arr: ResizeArray<'T>) (items: 'T seq) = arr.AddRangeAsSet(items, fun x -> failwith $"ERROR: {x} duplicated.")
