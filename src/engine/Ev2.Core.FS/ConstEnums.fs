namespace Ev2.Core.FS

open Dual.Common.Base
open Newtonsoft.Json.Linq
open Newtonsoft.Json

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



[<AutoOpen>]
module ValueRangeModule =

    type BoundType = | Open | Closed
    type Bound<'T> = 'T * BoundType

    type RangeSegment<'T> = {
        Lower: option<Bound<'T>>
        Upper: option<Bound<'T>>
    }

    type IValueParameter =
        abstract member Jsonize:   unit -> string
        abstract member Stringify: unit -> string

    type ValueCondition<'T> =
        | Single of 'T
        | Multiple of 'T list
        | Ranges of RangeSegment<'T> list   // 단일 or 복수 범위 모두 표현 가능
        with
            interface IValueParameter with
                member x.Jsonize() =
                    let typeName = typeof<'T>.Name   // 예: "float", "int"
                    let jroot = JObject()
                    jroot["valueType"] <- JToken.FromObject(typeName)
                    jroot["value"]     <- JToken.FromObject(x)
                    jroot.ToString(Formatting.Indented)

                member x.Stringify() =
                    let stringifyRange (r: RangeSegment<'T>) =
                        let format (v: 'T, b: BoundType) isLower =
                            match b, isLower with
                            | Open  , true  -> sprintf "%A < x" v
                            | Closed, true  -> sprintf "%A ≤ x" v
                            | Open  , false -> sprintf "x < %A" v
                            | Closed, false -> sprintf "x ≤ %A" v

                        match r.Lower, r.Upper with
                        | Some l, Some u ->   // 예: 5 < x < 6
                            let left  = format l true
                            let right = format u false
                            // 예: "5 < x" + " and " + "x < 6" → "5 < x < 6"
                            if left.EndsWith("x") && right.StartsWith("x ") then
                                let lval = left .Split(' ')[0]
                                let lop  = left .Split(' ')[1]
                                let rop  = right.Split(' ')[1]
                                let rval = right.Split(' ')[2]
                                sprintf "%s %s x %s %s" lval lop rop rval
                            else
                                sprintf "%s && %s" left right

                        | Some l, None -> format l true
                        | None, Some u -> format u false
                        | None, None -> "true"


                    match x with
                    | Single v -> sprintf "x = %A" v
                    | Multiple vs -> vs |> List.map string |> String.concat ", " |> sprintf "x ∈ {%s}"
                    | Ranges rs -> rs |> List.map stringifyRange |> String.concat " || "

            override x.ToString() = (x :> IValueParameter).Stringify()




    let deserializeWithType (json: string) : IValueParameter =
        let jroot = JObject.Parse(json)
        let typeName = jroot.["valueType"].ToString()
        let valueJson = jroot.["value"].ToString()

        let ty =
            match typeName with
            | t when t = typedefof<single>.Name -> typeof<ValueCondition<single>>
            | t when t = typedefof<double>.Name -> typeof<ValueCondition<double>>   // = float
            | t when t = typedefof<int8>  .Name -> typeof<ValueCondition<int8>>
            | t when t = typedefof<int16> .Name -> typeof<ValueCondition<int16>>
            | t when t = typedefof<int32> .Name -> typeof<ValueCondition<int32>>
            | t when t = typedefof<int64> .Name -> typeof<ValueCondition<int64>>
            | t when t = typedefof<uint8> .Name -> typeof<ValueCondition<uint8>>
            | t when t = typedefof<uint16>.Name -> typeof<ValueCondition<uint16>>
            | t when t = typedefof<uint32>.Name -> typeof<ValueCondition<uint32>>
            | t when t = typedefof<uint64>.Name -> typeof<ValueCondition<uint64>>
            | t when t = typedefof<char>  .Name -> typeof<ValueCondition<char>>
            | _ -> failwith $"Unsupported type hint: {typeName}"

        JsonConvert.DeserializeObject(valueJson, ty) |> box :?> IValueParameter

(*


1. 단일 범위: 3 < x ≤ 7

let v1 = Ranges [
    { Lower = Some (3.0, Open); Upper = Some (7.0, Closed) }
]

2. 복수 범위: x < 3.14 || (5.0 < x < 6.0) || 7.1 <= x

let v2 = Ranges [
    { Lower = None; Upper = Some (3.14, Open) }
    { Lower = Some (5.0, Open); Upper = Some (6.0, Open) }
    { Lower = Some (7.1, Closed); Upper = None }
]

3. 단일 값 / 복수 값

let v3 = Single 42
let v4 = Multiple [1; 2; 3]








1단계: JSON 구조 예시

{
  "valueType": "float",
  "value": {
    "Case": "Ranges",
    "Fields": [
      {
        "Lower": [3.0, "Open"],
        "Upper": [7.0, "Closed"]
      }
    ]
  }
}

    valueType: 실제 'T 타입의 문자열 표현 ("float", "int", "string" 등)

    value: ValueCondition<'T>를 serialize한 결과 (DU 구조)

🧠 2단계: 타입 힌트로 deserialize 수행

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System

let deserializeWithType (json: string) : obj =
    let jroot = JObject.Parse(json)
    let typeName = jroot.["valueType"].ToString()
    let valueJson = jroot.["value"].ToString()

    let ty =
        match typeName.ToLower() with
        | "float" -> typedefof<ValueCondition<float>>
        | "int"   -> typedefof<ValueCondition<int>>
        | "bool"  -> typedefof<ValueCondition<bool>>
        | "string"-> typedefof<ValueCondition<string>>
        | _ -> failwith $"Unsupported type hint: {typeName}"

    JsonConvert.DeserializeObject(valueJson, ty)

    반환 타입은 obj지만, 이후 :?> ValueCondition<float> 등으로 안전하게 캐스팅 가능

    typedefof<ValueCondition<_>> 사용으로 제네릭 형태 유지 가능

✨ 3단계: 활용 예시

let exampleJson = """
{
  "valueType": "float",
  "value": {
    "Case": "Single",
    "Fields": [3.14]
  }
}
"""

let resultObj = deserializeWithType exampleJson

match resultObj with
| :? ValueCondition<float> as fcond ->
    printfn "It's a ValueCondition<float>: %A" fcond
| _ ->
    printfn "Unexpected type"

🔧 4단계: JSON 생성 (직렬화 시에도 타입 힌트 추가)

let serializeWithType<'T> (value: ValueCondition<'T>) (typeName: string) : string =
    let jroot = JObject()
    jroot["valueType"] <- JToken.FromObject(typeName)
    jroot["value"] <- JToken.FromObject(value)
    jroot.ToString(Formatting.Indented)

사용 예:

let cond = Ranges [ { Lower = Some(3.0, Open); Upper = Some(7.0, Closed) } ]
let json = serializeWithType cond "float"



*)
