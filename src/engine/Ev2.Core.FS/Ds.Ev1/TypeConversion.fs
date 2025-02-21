namespace Dual.Ev2

open Dual.Common.Core.FS

[<AutoOpen>]
module TypeConversionModule =
    //let (|Float64|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1.0 else 0.0)     // toInt(false) 등에서의 casting 허용 위해 필요
    //    | :? byte   as n -> Some (double n)
    //    | :? double as n -> Some (double n)
    //    | :? int16  as n -> Some (double n)
    //    | :? int32  as n -> Some (double n)
    //    | :? int64  as n -> Some (double n)
    //    | :? sbyte  as n -> Some (double n)
    //    | :? single as n -> Some (double n)
    //    | :? uint16 as n -> Some (double n)
    //    | :? uint32 as n -> Some (double n)
    //    | :? uint64 as n -> Some (double n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to double"
    //        None

    //let (|Float32|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1.f else 0.f)
    //    | :? byte   as n -> Some (float32 n)
    //    | :? double as n -> Some (float32 n)
    //    | :? int16  as n -> Some (float32 n)
    //    | :? int32  as n -> Some (float32 n)
    //    | :? int64  as n -> Some (float32 n)
    //    | :? sbyte  as n -> Some (float32 n)
    //    | :? single as n -> Some (float32 n)
    //    | :? uint16 as n -> Some (float32 n)
    //    | :? uint32 as n -> Some (float32 n)
    //    | :? uint64 as n -> Some (float32 n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to float"
    //        None

    //let (|Byte|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1uy else 0uy)
    //    | :? byte   as n -> Some (byte n)
    //    | :? double as n -> Some (byte n)
    //    | :? int16  as n -> Some (byte n)
    //    | :? int32  as n -> Some (byte n)
    //    | :? int64  as n -> Some (byte n)
    //    | :? sbyte  as n -> Some (byte n)
    //    | :? single as n -> Some (byte n)
    //    | :? uint16 as n -> Some (byte n)
    //    | :? uint32 as n -> Some (byte n)
    //    | :? uint64 as n -> Some (byte n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to byte"
    //        None

    //let (|SByte|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1y else 0y)
    //    | :? byte   as n -> Some (sbyte n)
    //    | :? double as n -> Some (sbyte n)
    //    | :? int16  as n -> Some (sbyte n)
    //    | :? int32  as n -> Some (sbyte n)
    //    | :? int64  as n -> Some (sbyte n)
    //    | :? sbyte  as n -> Some (sbyte n)
    //    | :? single as n -> Some (sbyte n)
    //    | :? uint16 as n -> Some (sbyte n)
    //    | :? uint32 as n -> Some (sbyte n)
    //    | :? uint64 as n -> Some (sbyte n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to sbyte"
    //        None

    //let (|Int16|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1s else 0s)
    //    | :? byte   as n -> Some (int16 n)
    //    | :? double as n -> Some (int16 n)
    //    | :? int16  as n -> Some (int16 n)
    //    | :? int32  as n -> Some (int16 n)
    //    | :? int64  as n -> Some (int16 n)
    //    | :? sbyte  as n -> Some (int16 n)
    //    | :? single as n -> Some (int16 n)
    //    | :? uint16 as n -> Some (int16 n)
    //    | :? uint32 as n -> Some (int16 n)
    //    | :? uint64 as n -> Some (int16 n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to int16"
    //        None

    //let (|UInt16|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1us else 0us)
    //    | :? byte   as n -> Some (uint16 n)
    //    | :? double as n -> Some (uint16 n)
    //    | :? int16  as n -> Some (uint16 n)
    //    | :? int32  as n -> Some (uint16 n)
    //    | :? int64  as n -> Some (uint16 n)
    //    | :? sbyte  as n -> Some (uint16 n)
    //    | :? single as n -> Some (uint16 n)
    //    | :? uint16 as n -> Some (uint16 n)
    //    | :? uint32 as n -> Some (uint16 n)
    //    | :? uint64 as n -> Some (uint16 n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to uint16"
    //        None

    //let (|Int32|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1 else 0)     // toInt(false) 등에서의 casting 허용 위해 필요
    //    | :? byte   as n -> Some (int32 n)
    //    | :? double as n -> Some (int32 n)
    //    | :? int16  as n -> Some (int32 n)
    //    | :? int32  as n -> Some (int32 n)
    //    | :? int64  as n -> Some (int32 n)
    //    | :? sbyte  as n -> Some (int32 n)
    //    | :? single as n -> Some (int32 n)
    //    | :? uint16 as n -> Some (int32 n)
    //    | :? uint32 as n -> Some (int32 n)
    //    | :? uint64 as n -> Some (int32 n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to int32"
    //        None

    //let (|UInt32|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1u else 0u)
    //    | :? byte   as n -> Some (uint32 n)
    //    | :? double as n -> Some (uint32 n)
    //    | :? int16  as n -> Some (uint32 n)
    //    | :? int32  as n -> Some (uint32 n)
    //    | :? int64  as n -> Some (uint32 n)
    //    | :? sbyte  as n -> Some (uint32 n)
    //    | :? single as n -> Some (uint32 n)
    //    | :? uint16 as n -> Some (uint32 n)
    //    | :? uint32 as n -> Some (uint32 n)
    //    | :? uint64 as n -> Some (uint32 n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to uint32"
    //        None

    //let (|Int64|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1L else 0L)
    //    | :? byte   as n -> Some (int64 n)
    //    | :? double as n -> Some (int64 n)
    //    | :? int16  as n -> Some (int64 n)
    //    | :? int32  as n -> Some (int64 n)
    //    | :? int64  as n -> Some (int64 n)
    //    | :? sbyte  as n -> Some (int64 n)
    //    | :? single as n -> Some (int64 n)
    //    | :? uint16 as n -> Some (int64 n)
    //    | :? uint32 as n -> Some (int64 n)
    //    | :? uint64 as n -> Some (int64 n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to int64"
    //        None

    //let (|UInt64|_|) (x:obj) =
    //    match x with
    //    | :? bool as b -> Some (if b then 1UL else 0UL)
    //    | :? byte   as n -> Some (uint64 n)
    //    | :? double as n -> Some (uint64 n)
    //    | :? int16  as n -> Some (uint64 n)
    //    | :? int32  as n -> Some (uint64 n)
    //    | :? int64  as n -> Some (uint64 n)
    //    | :? sbyte  as n -> Some (uint64 n)
    //    | :? single as n -> Some (uint64 n)
    //    | :? uint16 as n -> Some (uint64 n)
    //    | :? uint32 as n -> Some (uint64 n)
    //    | :? uint64 as n -> Some (uint64 n)
    //    | _ ->
    //        logWarn $"Cannot convert {x} to uint64"
    //        None

    let inline tryConvert<'T> (x: obj) : 'T option =
        try
            match x with
            | :? bool as b -> Some (unbox (if b then 1 else 0))  // bool → 숫자 변환
            | :? byte   as n -> Some (unbox n)
            | :? double as n -> Some (unbox n)
            | :? int16  as n -> Some (unbox n)
            | :? int32  as n -> Some (unbox n)
            | :? int64  as n -> Some (unbox n)
            | :? sbyte  as n -> Some (unbox n)
            | :? single as n -> Some (unbox n)
            | :? uint16 as n -> Some (unbox n)
            | :? uint32 as n -> Some (unbox n)
            | :? uint64 as n -> Some (unbox n)
            | _ -> None
        with
        | _ -> None  // 변환 실패 시 None 반환

    let (|Float64|_|) (x: obj) = tryConvert<double> x
    let (|Float32|_|) (x: obj) = tryConvert<float32> x
    let (|Int16  |_|) (x: obj) = tryConvert<int16> x
    let (|UInt16 |_|) (x: obj) = tryConvert<uint16> x
    let (|Int32  |_|) (x: obj) = tryConvert<int32> x
    let (|UInt32 |_|) (x: obj) = tryConvert<uint32> x
    let (|Int64  |_|) (x: obj) = tryConvert<int64> x
    let (|UInt64 |_|) (x: obj) = tryConvert<uint64> x
    let (|SByte  |_|) (x: obj) = tryConvert<sbyte> x
    let (|Byte   |_|) (x: obj) = tryConvert<byte> x



    let (|Bool|_|) (x:obj) =
        match x with
        | :? bool as b -> Some b
        | :? decimal as n -> Some (n <> 0M)
        | :? single as n -> Some (n <> 0.f)
        | :? double as n -> Some (n <> 0.0)
        | Int32 n -> Some (n <> 0)      (* int32 로 변환 가능한 모든 numeric type 포함 *)
        | Int64 n -> Some (n <> 0)      (* int32 로 변환 가능한 모든 numeric type 포함 *)
        | _ -> None  // bool casting 실패

    let toBool    x = (|Bool|_|)    x |> Option.get
    let toFloat32 x = (|Float32|_|) x |> Option.get
    let toFloat64 x = (|Float64|_|) x |> Option.get
    let toInt16   x = (|Int16|_|)   x |> Option.get
    let toInt32   x = (|Int32|_|)   x |> Option.get
    let toInt64   x = (|Int64|_|)   x |> Option.get
    let toInt8    x = (|SByte|_|)   x |> Option.get
    let toUInt16  x = (|UInt16|_|)  x |> Option.get
    let toUInt32  x = (|UInt32|_|)  x |> Option.get
    let toUInt64  x = (|UInt64|_|)  x |> Option.get
    let toUInt8   x = (|Byte|_|)    x |> Option.get

    let isEqual (x:obj) (y:obj) =
        match x, y with
        | (:? bool as x), (:? bool as y) -> x = y
        | (:? string as a), (:? string as b) -> a = b
        | Float64 x, Float64 y -> x = y     // double 로 환산가능한 숫자만 비교하면 모든 type 의 숫자 비교는 OK
        | _ ->
            failwithlog "ERROR"
            false


