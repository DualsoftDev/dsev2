namespace Ev2.Core.FS

open System
open Newtonsoft.Json.Linq
open Newtonsoft.Json
open Dual.Common.Core.FS
open Dual.Common.Base


[<AutoOpen>]
module GuidedValueSpecModule =
    /// Guid를 가진 ValueSpec - ValueSpecWrapper를 상속받아 구현
    type GuidedValueSpec<'T when 'T : equality and 'T : comparison>(guid:Guid, value: ValueSpec<'T>) =
        inherit ValueSpecWrapper<'T>(value)
        interface IGuidedValueSpec with
            member x.Guid with get() = x.Guid and set v = x.Guid <- v

        member val Guid = guid with get, set

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

    type ApiCallValueSpec<'T when 'T : equality and 'T : comparison>(apiCallGuid:Guid, value: ValueSpec<'T>) =
        inherit GuidedValueSpec<'T>(apiCallGuid, value)
        new (apiCall:ApiCall, value: ValueSpec<'T>) = ApiCallValueSpec<'T>(apiCall.Guid, value)
        interface IApiCallValueSpec
            with member x.ApiCall = x.ApiCall :> IRtApiCall
        member val ApiCall = getNull<ApiCall>() with get, set

    type ApiCallValueSpecs with
        // ToJson: ApiCallValueSpecs를 JSON 문자열로 직렬화
        // 현재는 단순히 문자열 배열로 저장하고, 향후 확장 가능
        member x.ToJson() =
            if x.Count = 0 then
                null
            else
                // 각 spec을 객체로 직접 직렬화 (이중 escape 방지)
                let objects =
                    x |-> (fun spec -> JObject.Parse(spec.Jsonize()))
                      |> toArray
                JsonConvert.SerializeObject(objects)

        // FromJson: JSON 문자열에서 ApiCallValueSpecs로 역직렬화
        // ToJson()에서 저장한 객체 배열을 다시 읽어옴
        static member FromJson(json: string) =
            let specs = ApiCallValueSpecs()
            json |> String.andDo (fun json ->
                try
                    // JSON 배열을 JArray로 파싱
                    let jarray = JArray.Parse(json)
                    if jarray <> null then
                        for jtoken in jarray do
                            let jobj = jtoken :?> JObject
                            let guid =
                                jobj.["Guid"] |> toOption
                                |-> _.ToObject<Guid>()
                                |? Guid.NewGuid()

                            // JObject를 다시 JSON 문자열로 변환하여 deserializeWithType 호출
                            let jsonStr = jobj.ToString()
                            let valueSpec = deserializeWithType jsonStr

                            // IValueSpec을 적절한 타입의 ApiCallValueSpec으로 변환
                            // ValueSpec<'T>의 실제 타입을 확인하여 처리
                            let apiCallValueSpec : IApiCallValueSpec =
                                match valueSpec with
                                | :? ValueSpec<single> as v -> ApiCallValueSpec<single>(guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<double> as v -> ApiCallValueSpec<double>(guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<int8>   as v -> ApiCallValueSpec<int8>  (guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<int16>  as v -> ApiCallValueSpec<int16> (guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<int32>  as v -> ApiCallValueSpec<int32> (guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<int64>  as v -> ApiCallValueSpec<int64> (guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<uint8>  as v -> ApiCallValueSpec<uint8> (guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<uint16> as v -> ApiCallValueSpec<uint16>(guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<uint32> as v -> ApiCallValueSpec<uint32>(guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<uint64> as v -> ApiCallValueSpec<uint64>(guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<char>   as v -> ApiCallValueSpec<char>  (guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<bool>   as v -> ApiCallValueSpec<bool>  (guid, v) :> IApiCallValueSpec
                                | :? ValueSpec<string> as v -> ApiCallValueSpec<string>(guid, v) :> IApiCallValueSpec
                                | _ -> failwith $"Unsupported ValueSpec type in ApiCallValueSpecs.FromJson: {valueSpec.GetType().FullName}"

                            specs.Add(apiCallValueSpec)
                with
                | _ -> ()
            )
            specs

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
            ValueSpecWrapper(ValueSpec.Single value)

        /// 복수 값 생성 헬퍼
        let multiple values =
            ValueSpecWrapper(Multiple values)

        /// 범위 생성 헬퍼
        let ranges rangeList =
            ValueSpecWrapper(Ranges rangeList)

        /// wrapper에서 inner value 추출
        let unwrap (wrapper: ValueSpecWrapper<'T>) =
            wrapper.ValueSpec

        /// IValueSpec을 ValueSpecWrapper로 캐스팅 시도
        let tryAsWrapper (spec: IValueSpec) =
            match spec with
            | :? ValueSpecWrapper<_> as wrapper -> Some wrapper
            | _ -> None

