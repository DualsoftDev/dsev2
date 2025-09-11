namespace Ev2.Core.FS

open System
open System.Text.RegularExpressions
open Newtonsoft.Json.Linq
open Newtonsoft.Json
open Dual.Common.Core.FS
open Dual.Common.Base


[<AutoOpen>]
module ValueRangeModule =
    /// ValueSpec 인터페이스
    type IValueSpec =
        abstract member Jsonize:   unit -> string
        abstract member Stringify: unit -> string

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

    type ValueSpec<'T when 'T : equality and 'T : comparison> = // Jsonize, Stringify
        | Undefined
        | Single of 'T
        | Multiple of 'T list
        | Ranges of RangeSegment<'T> list   // 단일 or 복수 범위 모두 표현 가능
        with
            member x.Contains(value: obj): bool =
                // 타입 체크
                match value with
                | :? 'T as typedValue ->
                    match x with
                    | Undefined -> failwith "ERROR: ValueSpec is undefined."
                    | Single v ->
                        // 값 비교
                        v = typedValue
                    | Multiple values ->
                        // 리스트에 포함되는지 확인
                        values |> List.contains typedValue
                    | Ranges ranges ->
                        // 범위 내에 있는지 확인
                        ranges |> List.exists (fun range ->
                            let inLowerBound =
                                match range.Lower with
                                | None -> true
                                | Some (lowerVal, Open) -> typedValue > lowerVal
                                | Some (lowerVal, Closed) -> typedValue >= lowerVal

                            let inUpperBound =
                                match range.Upper with
                                | None -> true
                                | Some (upperVal, Open) -> typedValue < upperVal
                                | Some (upperVal, Closed) -> typedValue <= upperVal

                            inLowerBound && inUpperBound
                        )
                | _ -> false  // 타입이 맞지 않으면 false

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
                | Undefined -> failwith "ERROR: ValueSpec is undefined."
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
    type ValueSpecWrapper<'T when 'T : equality and 'T : comparison>(value: ValueSpec<'T>) =
        inherit AbstractValueSpec()

        member val ValueSpec = value with get, set

        /// 확장을 위한 가상 메서드 - 커스텀 속성 추가용
        abstract member AddCustomProperties: JObject -> unit
        default x.AddCustomProperties(jobj) = ()

        override x.ToJObject() =
            let jroot = ValueSpec<'T>.CreateJObjectCore(x.ValueSpec)
            x.AddCustomProperties(jroot)  // 확장 포인트
            jroot

        override x.Stringify() = ValueSpec<'T>.StringifyCore(x.ValueSpec)

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

