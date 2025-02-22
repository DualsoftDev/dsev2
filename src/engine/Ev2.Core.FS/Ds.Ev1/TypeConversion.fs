namespace Dual.Ev2

open System
open Dual.Common.Core.FS

[<AutoOpen>]
module TypeConversionModule =
    //data 타입 지원 항목 : 알파벳 순 정렬 (Alt+Shift+L, Alt+Shift+S)
    let [<Literal>] BOOL    = "Boolean"
    let [<Literal>] CHAR    = "Char"
    let [<Literal>] FLOAT32 = "Single"
    let [<Literal>] FLOAT64 = "Double"
    let [<Literal>] INT16   = "Int16"
    let [<Literal>] INT32   = "Int32"
    let [<Literal>] INT64   = "Int64"
    let [<Literal>] INT8    = "SByte"
    let [<Literal>] STRING  = "String"
    let [<Literal>] UINT16  = "UInt16"
    let [<Literal>] UINT32  = "UInt32"
    let [<Literal>] UINT64  = "UInt64"
    let [<Literal>] UINT8   = "Byte"


    let tryConvert<'T> (x: obj) : 'T option =
        try
            let t = typeof<'T>
            let sub v = Some (unbox<'T> v)
            match box x with
            // 정수 변환 (오버플로우 방지)
            | :? float as v when t = typeof<int>   -> sub (int v)
            | :? float as v when t = typeof<int64> -> sub (int64 v)
            | :? int64 as v when t = typeof<int>   ->
                if v >= int64 Int32.MinValue && v <= int64 Int32.MaxValue then sub (int v)
                else None
            | :? int   as v when t = typeof<int64> -> sub (int64 v)  // ✅ `int -> int64` 변환 추가
            | :? int   as v when t = typeof<float> -> sub (float v)
            | :? int64 as v when t = typeof<float> -> sub (float v)
            | :? int64 as v when t = typeof<int64> -> sub v

            // 문자열 변환
            | :? string as v ->
                match v with
                | _ when t = typeof<int32> ->
                    match Int32.TryParse(v) with
                    | true, res -> sub res
                    | _ -> None
                | _ when t = typeof<int64> ->
                    match Int64.TryParse(v) with
                    | true, res -> sub res
                    | _ -> None
                | _ when t = typeof<double> ->
                    match Double.TryParse(v) with
                    | true, res -> sub res
                    | _ -> None
                | _ when t = typeof<bool> ->
                    match v.ToLower() with
                    | "true" -> sub true
                    | "false" -> sub false
                    | _ -> None
                | _ when t = typeof<char> && v.Length = 1 -> sub v.[0]
                | _ -> None

            // bool 변환
            | :? bool as v when t = typeof<string> -> sub (v.ToString())
            | :? bool as v -> sub v

            // char 변환
            | :? char as v when t = typeof<int> -> sub (int v)

            // 숫자 → 문자열 변환
            | :? int   as v when t = typeof<string> -> sub (v.ToString())
            | :? int64 as v when t = typeof<string> -> sub (v.ToString())
            | :? float as v when t = typeof<string> -> sub (v.ToString())

            // 기본 변환
            | _ when x <> null -> sub x

            | _ -> None
        with
        | :? InvalidCastException
        | :? FormatException
        | :? OverflowException -> None



    // 테스트 예제

    (*
    // 테스트 예제
        // 정수와 실수 변환
        let myAssert(x) = if x then () else failwith "Assertion failed"

        myAssert(tryConvert<int> 2.9              = Some 2)
        myAssert(tryConvert<int> -2.9             = Some(-2))
        myAssert(tryConvert<int> 3.5              = Some(3))
        myAssert(tryConvert<float> 100            = Some(100.0))
        myAssert(tryConvert<float> 42.5           = Some(42.5))

        // 문자열 변환
        myAssert(tryConvert<int> "42"             = Some(42))
        myAssert(tryConvert<int> "3.14"           = None)
        myAssert(tryConvert<float> "3.14"         = Some(3.14))
        myAssert(tryConvert<float> "-99.9"        = Some(-99.9))
        myAssert(tryConvert<int> "123abc"         = None)
        myAssert(tryConvert<double> "abc"         = None)

        // `int64`, `int` 간 변환
        myAssert(tryConvert<int> 123L             = Some(123))
        myAssert(tryConvert<int64> 123            = Some(123L))
        myAssert(tryConvert<int> 999999999999L    = None)
        myAssert(tryConvert<int64> 999999999999L  = Some(999999999999L))

        // `bool` 변환
        myAssert(tryConvert<bool> "true"          = Some(true))
        myAssert(tryConvert<bool> "false"         = Some(false))
        myAssert(tryConvert<bool> "True"          = Some(true))
        myAssert(tryConvert<bool> "FALSE"         = Some(false))
        myAssert(tryConvert<bool> "yes"           = None)
        myAssert(tryConvert<bool> 1               = None)
        myAssert(tryConvert<bool> 0               = None)

        // 기타 타입 변환
        myAssert(tryConvert<char> "A"             = Some('A'))
        myAssert(tryConvert<char> 'B'             = Some('B'))
        myAssert(tryConvert<char> "Hello"         = None)
        myAssert(tryConvert<int> '9'              = Some(57))
        myAssert(tryConvert<int> 'A'              = Some(65))

        // 문자열 변환 추가
        myAssert(tryConvert<string> 123           = Some("123"))
        myAssert(tryConvert<string> true          = Some("True"))
        myAssert(tryConvert<string> 3.14          = Some("3.14"))

        // null 처리
        myAssert(tryConvert<string> null          = None)

    *)



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


