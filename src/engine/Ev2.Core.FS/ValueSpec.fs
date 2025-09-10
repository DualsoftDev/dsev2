namespace Ev2.Core.FS

open System
open System.Text.RegularExpressions
open Newtonsoft.Json.Linq
open Newtonsoft.Json
open Dual.Common.Core.FS


[<AutoOpen>]
module ValueRangeModule =

    type BoundType = | Open | Closed
    type Bound<'T> = 'T * BoundType

    type RangeSegment<'T> = {
        Lower: option<Bound<'T>>
        Upper: option<Bound<'T>>
    }

    let mutable internal fwdValueSpecFromString:  string->IValueSpec = let dummy (text:string) = failwith "Should be reimplemented." in dummy
    let mutable internal fwdValueSpecFromJson:  string->IValueSpec = let dummy (json:string) = failwith "Should be reimplemented." in dummy
    type ValueSpec =
        /// text: "x ∈ {1, 2, 3}"
        static member FromString(text: string) : IValueSpec = fwdValueSpecFromString text

        /// json: {
        ///   "valueType": "Double",
        ///   "value": {
        ///     "Case": "Single",
        ///     "Fields": [ 3.14156952 ]
        ///   }
        /// }
        static member FromJson(json: string) : IValueSpec = fwdValueSpecFromJson json

    type ValueSpec<'T> = // Jsonize, Stringify
        | Single of 'T
        | Multiple of 'T list
        | Ranges of RangeSegment<'T> list   // 단일 or 복수 범위 모두 표현 가능
        with
            // 공통 로직을 static member로 정의
            static member internal CreateJObjectCore(value: ValueSpec<'T>) =
                let typeName = typeof<'T>.Name
                let jroot = JObject()
                jroot["$type"] <- JToken.FromObject(typeName)
                jroot["Value"] <- JToken.FromObject(value)
                jroot

            static member internal StringifyCore(value: ValueSpec<'T>) =
                let stringifyRange (r: RangeSegment<'T>) =
                    let format (v: 'T, b: BoundType) isLower =
                        match b, isLower with
                        | Open  , true  -> sprintf "%A < x" v
                        | Closed, true  -> sprintf "%A <= x" v
                        | Open  , false -> sprintf "x < %A" v
                        | Closed, false -> sprintf "x <= %A" v

                    match r.Lower, r.Upper with
                    | Some l, Some u ->
                        let left  = format l true
                        let right = format u false
                        if left.EndsWith("x") && right.StartsWith("x ") then
                            let lval = left .Split(' ').[0]
                            let lop  = left .Split(' ').[1]
                            let rop  = right.Split(' ').[1]
                            let rval = right.Split(' ').[2]
                            sprintf "%s %s x %s %s" lval lop rop rval
                        else
                            sprintf "%s && %s" left right
                    | Some l, None -> format l true
                    | None, Some u -> format u false
                    | None, None -> "true"

                match value with
                | Single v -> sprintf "x = %A" v
                | Multiple vs -> vs |> List.map string |> String.concat ", " |> sprintf "x ∈ {%s}"
                | Ranges rs -> rs |> List.map stringifyRange |> String.concat " || "

            member x.ToJObject() = ValueSpec<'T>.CreateJObjectCore(x)

            interface IValueSpec with
                member x.Jsonize() =
                    let jobj = x.ToJObject()
                    jobj.ToString(Formatting.Indented)

                member x.Stringify() = ValueSpec<'T>.StringifyCore(x)

            override x.ToString() = (x :> IValueSpec).Stringify()

    [<AbstractClass>]
    type AbstractValueSpec() =

        abstract member ToJObject: unit -> JObject

        abstract member Jsonize: unit -> string
        default x.Jsonize() =
            let jobj = x.ToJObject()
            jobj.ToString(Formatting.Indented)
        abstract member ToJson: unit -> string
        default x.ToJson() = x.Jsonize()

        abstract member Stringify: unit -> string

        interface IValueSpec with
            member x.Jsonize() = x.Jsonize()
            member x.Stringify() = x.Stringify()

    /// C# 에서 상속 가능한 ValueSpec wrapper 클래스
    type ValueSpecWrapper<'T>(value: ValueSpec<'T>) =
        inherit AbstractValueSpec()

        member val InnerValue = value with get, set

        /// 확장을 위한 가상 메서드 - 커스텀 속성 추가용
        abstract member AddCustomProperties: JObject -> unit
        default x.AddCustomProperties(jobj) = ()

        override x.ToJObject() =
            let jroot = ValueSpec<'T>.CreateJObjectCore(x.InnerValue)
            x.AddCustomProperties(jroot)  // 확장 포인트
            jroot

        override x.Stringify() = ValueSpec<'T>.StringifyCore(x.InnerValue)

        override x.ToString() = x.Stringify()



    let deserializeWithType (json: string) : IValueSpec =
        let jroot = JObject.Parse(json)
        let typeName = jroot.["$type"].ToString()
        let valueJson = jroot.["Value"].ToString()

        let ty =
            match typeName with
            | t when t = typedefof<single>.Name -> typeof<ValueSpec<single>>
            | t when t = typedefof<double>.Name -> typeof<ValueSpec<double>>   // = float
            | t when t = typedefof<int8>  .Name -> typeof<ValueSpec<int8>>
            | t when t = typedefof<int16> .Name -> typeof<ValueSpec<int16>>
            | t when t = typedefof<int32> .Name -> typeof<ValueSpec<int32>>
            | t when t = typedefof<int64> .Name -> typeof<ValueSpec<int64>>
            | t when t = typedefof<uint8> .Name -> typeof<ValueSpec<uint8>>
            | t when t = typedefof<uint16>.Name -> typeof<ValueSpec<uint16>>
            | t when t = typedefof<uint32>.Name -> typeof<ValueSpec<uint32>>
            | t when t = typedefof<uint64>.Name -> typeof<ValueSpec<uint64>>
            | t when t = typedefof<char>  .Name -> typeof<ValueSpec<char>>
            | t when t = typedefof<bool>  .Name -> typeof<ValueSpec<bool>>
            | _ -> failwith $"Unsupported type hint: {typeName}"

        JsonConvert.DeserializeObject(valueJson, ty) |> box :?> IValueSpec


    let rTryParseValueSpec (text: string) : Result<IValueSpec, string> =
        if text.IsNullOrEmpty() then
            Error "Input is empty"
        else

            let trimmed = text.Trim()

            let tryParseFloat (s: string) = Double .TryParse(s.Trim())  |> tryParseToOption
            let tryParseInt   (s: string) = Int32  .TryParse(s.Trim())  |> tryParseToOption
            let tryParseBool  (s: string) = Boolean.TryParse(s.Trim())  |> tryParseToOption
            let tryParseChar  (s: string) =
                let s = s.Trim().Trim('\'', '"')
                if s.Length = 1 then Some s.[0] else None

            // x = VALUE
            if Regex.IsMatch(trimmed, @"^x\s*=\s*.+$") then
                let raw = trimmed.Substring(2).TrimStart('=').Trim()

                match tryParseInt raw with
                | Some i -> Ok (Single i :> IValueSpec)
                | None ->
                    match tryParseFloat raw with
                    | Some f -> Ok (Single f :> IValueSpec)
                    | None ->
                        match tryParseBool raw with
                        | Some b -> Ok (Single b :> IValueSpec)
                        | None ->
                            match tryParseChar raw with
                            | Some c -> Ok (Single c :> IValueSpec)
                            | None -> Ok (Single raw :> IValueSpec)

            // x ∈ {a, b, c}
            elif Regex.IsMatch(trimmed, @"^x\s*∈\s*\{(.+)\}$") then
                let inner = Regex.Match(trimmed, @"\{(.+)\}").Groups[1].Value
                let parts = inner.Split(',') |> Array.map (fun s -> s.Trim())

                let tryAllParsers =
                    seq {
                        let allInts = parts |> Array.choose tryParseInt
                        if allInts.Length = parts.Length then
                            yield Some (Multiple (Array.toList allInts) :> IValueSpec)

                        let allFloats = parts |> Array.choose tryParseFloat
                        if allFloats.Length = parts.Length then
                            yield Some (Multiple (Array.toList allFloats) :> IValueSpec)

                        let allBools = parts |> Array.choose tryParseBool
                        if allBools.Length = parts.Length then
                            yield Some (Multiple (Array.toList allBools) :> IValueSpec)

                        let allChars = parts |> Array.choose tryParseChar
                        if allChars.Length = parts.Length then
                            yield Some (Multiple (Array.toList allChars) :> IValueSpec)
                    }

                match tryAllParsers |> Seq.tryPick id with
                | Some result -> Ok result
                | None -> Ok (Multiple (Array.toList parts) :> IValueSpec)

            // 범위 표현: 3 < x <= 7 || 10 <= x
            elif Regex.IsMatch(trimmed, @"(\d+\s*(<|<=)\s*x)|x\s*(<|<=)\s*\d+") then
                let parts = trimmed.Split([| "||" |], StringSplitOptions.RemoveEmptyEntries)

                let parseSegment (part: string) : Result<RangeSegment<float>, string> =
                    let part = part.Trim()

                    let m1 = Regex.Match(part, @"^(.+)\s(<|<=)\s+x\s(<|<=)\s(.+)$")
                    if m1.Success then
                        let lraw, lop, rop, rraw =
                            m1.Groups[1].Value.Trim(), m1.Groups[2].Value, m1.Groups[3].Value, m1.Groups[4].Value.Trim()
                        match tryParseFloat lraw, tryParseFloat rraw with
                        | Some lval, Some rval ->
                            Ok {
                                Lower = Some (lval, if lop = "<" then Open else Closed)
                                Upper = Some (rval, if rop = "<" then Open else Closed)
                            }
                        | _ -> Error $"Invalid numeric bounds: '{part}'"

                    else
                        let m2 = Regex.Match(part, @"^x\s(<|<=)\s(.+)$")
                        if m2.Success then
                            let op = m2.Groups[1].Value
                            let raw = m2.Groups[2].Value.Trim()
                            match tryParseFloat raw with
                            | Some v -> Ok { Lower = None; Upper = Some (v, if op = "<" then Open else Closed) }
                            | _ -> Error $"Invalid upper bound: '{part}'"
                        else
                            let m3 = Regex.Match(part, @"^(.+)\s(<|<=)\s+x$")
                            if m3.Success then
                                let raw = m3.Groups[1].Value.Trim()
                                let op = m3.Groups[2].Value
                                match tryParseFloat raw with
                                | Some v -> Ok { Lower = Some (v, if op = "<" then Open else Closed); Upper = None }
                                | _ -> Error $"Invalid lower bound: '{part}'"
                            else
                                Error $"Unrecognized range format: '{part}'"

                // 모든 구간이 성공적으로 파싱되는 경우만 Ok
                let parsedSegments = parts |> Array.map parseSegment |> Array.toList

                match parsedSegments |> List.partition (function Ok _ -> true | _ -> false) with
                | oks, [] ->
                    let segments = oks |> List.choose (function Ok v -> Some v | _ -> None)
                    Ok (Ranges segments :> IValueSpec)
                | _, errs ->
                    let messages = errs |> List.choose (function Error msg -> Some msg | _ -> None)
                    Error (String.concat "\n" messages)

            else
                Error $"Unrecognized ValueCondition syntax: {text}"

    let parseValueSpec text = rTryParseValueSpec text |> Result.get


    type IValueSpec with // Deserialize, Parse, RTryParse, TryDeserialize, TryParse
        /// JSON 역직렬화 함수.
        static member Deserialize(json: string) = deserializeWithType json
        static member TryDeserialize(json: string) =
            if json.IsNullOrEmpty() then
                None
            else
                Some <| deserializeWithType json

        /// 사용자 편의 텍스트 parsing 함수.  e.g "3 < x <= 7 || 10 <= x"
        static member Parse(text: string) : IValueSpec = parseValueSpec text
        static member RTryParse(text: string) = rTryParseValueSpec text
        static member TryParse(text: string) = rTryParseValueSpec text |> Option.ofResult


    /// Guid를 가진 ValueSpec - ValueSpecWrapper를 상속받아 구현
    type GuidedValueSpec<'T>(guid:Guid, value: ValueSpec<'T>) =
        inherit ValueSpecWrapper<'T>(value)

        member val Guid = guid with get, set

        // ValueSpec 속성을 InnerValue로 리다이렉트
        member x.ValueSpec
            with get() = x.InnerValue
            and set(v) = x.InnerValue <- v

        // ToJObject를 override하여 Guid 추가
        override x.ToJObject() =
            let jobj = base.ToJObject()
            jobj["Guid"] <- JToken.FromObject(x.Guid)
            jobj

        // JSON 직렬화 (Guid 포함)
        override x.ToJson() =
            let jobj = x.ToJObject()
            jobj.ToString(Formatting.Indented)

        // JSON 역직렬화 - 타입 정보 필요
        static member FromJson(json: string) : GuidedValueSpec<'T> =
            let jobj = JObject.Parse(json)
            let guid = jobj["Guid"].ToObject<Guid>()
            let valueJson = jobj["Value"].ToString()
            let value = JsonConvert.DeserializeObject<ValueSpec<'T>>(valueJson)
            GuidedValueSpec<'T>(guid, value)

        //// IValueSpec 유지를 위한 비제네릭 버전 제공
        //static member FromJsonDynamic(json: string) : IValueSpec =
        //    let jobj = JObject.Parse(json)
        //    let guid = jobj["Guid"].ToObject<Guid>()
        //    let typeName = jobj["$type"].ToString()

        //    // 타입에 따라 적절한 GuidedValueSpec<T> 생성
        //    match typeName with
        //    | t when t = typedefof<single>.Name ->
        //        GuidedValueSpec<single>.FromJson(json) :> IValueSpec
        //    | t when t = typedefof<double>.Name ->
        //        GuidedValueSpec<double>.FromJson(json) :> IValueSpec
        //    | t when t = typedefof<int32>.Name ->
        //        GuidedValueSpec<int32>.FromJson(json) :> IValueSpec
        //    | t when t = typedefof<int64>.Name ->
        //        GuidedValueSpec<int64>.FromJson(json) :> IValueSpec
        //    | t when t = typedefof<bool>.Name ->
        //        GuidedValueSpec<bool>.FromJson(json) :> IValueSpec
        //    | t when t = typedefof<string>.Name ->
        //        GuidedValueSpec<string>.FromJson(json) :> IValueSpec
        //    | _ -> failwith $"Unsupported type for GuidedValueSpec: {typeName}"

(*


1. 단일 범위: 3 < x <= 7

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

    /// ValueSpec 팩토리 함수 및 헬퍼 메서드
    module ValueSpec =
        /// 기존 DU를 wrapper로 변환
        let wrap (spec: ValueSpec<'T>) =
            ValueSpecWrapper<'T>(spec)

        /// 단일 값 생성 헬퍼
        let single value =
            ValueSpecWrapper(Single value)

        /// 복수 값 생성 헬퍼
        let multiple values =
            ValueSpecWrapper(Multiple values)

        /// 범위 생성 헬퍼
        let ranges rangeList =
            ValueSpecWrapper(Ranges rangeList)

        /// wrapper에서 inner value 추출
        let unwrap (wrapper: ValueSpecWrapper<'T>) =
            wrapper.InnerValue

        /// IValueSpec을 ValueSpecWrapper로 캐스팅 시도
        let tryAsWrapper (spec: IValueSpec) =
            match spec with
            | :? ValueSpecWrapper<_> as wrapper -> Some wrapper
            | _ -> None
