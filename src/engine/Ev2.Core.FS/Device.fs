namespace rec Dual.Ev2

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System
open Dual.Common.Base.FS

[<AutoOpen>]
module DeviceModule =
    (* Device 관련 기존 구조 들
        - Jobs
        - ApiItem
            Tx, Rx -> Real
        - TaskDev
        - TaskDevParam
            Address, Symbol, DataType
        - TaskDevParamIO
        - ValueParam
        - ValueParamIO



    System
        DevicePrototypes: [
            cylinder: "cylinder.ds",
            pin: "pin.ds",
            servo: "servo.ds",
            ]
    STN1:
        DeviceCalls: [
            "case": single {
                name: "C1",
                proto: "cylinder",
                apis: [
                    {
                        name: "ADV",
                        input: {
                            address: "%IX0.0",
                            type: bool,
                            // range: non-bool type 에서 필요할 경우
                        },
                        output: "%QX0.0" }
                    { name: "RET", input: "%IX0.1", output: "%QX0.1" }
                ]
            },

            "case": single {
                name: "C2",
                proto: "cylinder",
                apis: [
                    { name: "ADV", input: "%IX0.2", output: "%QX0.2" }
                    { name: "RET", input: "%IX0.3", output: "%QX0.3" }
                ]
            },

            "case": single {
                name: "SERVO",
                proto: "servo",
                apis: [
                    {
                        name: "POS1",
                        input: {
                            address: "%IW10",
                            type: int,
                        },
                        output: {
                            address: "%QW10",
                            type: int,
                            range: [0, 1000]
                        }
                    { name: "RET", input: "%IX0.3", output: "%QX0.3" }
                ]
            },


            "case": single {
                name: "PIN1",
                proto: "pin",
                apis: [
                    { name: "PUSH", input: "%IX0.4", output: "%QX0.4" }
                ]
            },


            "case": multi {
                name: "C1C2",
                devices: ["C1", "C2"]
            }


        C1.ADV -> C2.ADV -> PIN1.PUSH -> SERVO.POS1( (200, 1024): 500 ) -> C1C2.RET -> PIN1.PUSH
     *)



    [<AbstractClass>]
    type TypedAddress(address: string, typ:Type) =
        [<JsonProperty(Order = -98)>] member val Address = address with get, set
        [<JsonProperty(Order = -97)>] member val ObjectHolder = ObjHolder(typ) with get, set

    type InputParam(address: string, typ:Type, ?min:obj, ?max:obj) =
        inherit TypedAddress(address, typ)
        do
            assert(min.IsNone || min.Value.GetType() = typ)
            assert(max.IsNone || max.Value.GetType() = typ)
        let min = min |? null
        let max = max |? null

        member val Min = ObjHolder(typ, min) with get, set
        member val Max = ObjHolder(typ, max) with get, set

    type OutputParam(address: string, typ:Type, ?value:obj) =
        inherit TypedAddress(address, typ)
        do
            assert(value.IsNone || value.Value.GetType() = typ)

    type IOParam(input:InputParam, output:OutputParam) =
        member val Input:InputParam = input
        member val Output = output
        member val Others = ["Hello"; "World"]


    let testMe() =
        // ✅ 테스트 데이터
        let param1 = InputParam("address1", typedefof<UInt32>, min = 10u, max = 100u)
        let param2 = OutputParam("address1", typedefof<UInt32>, value = 20u)

        let ioParam = IOParam(param1, param2)


        let json = EmJson.ToJson(ioParam)
        printfn "Serialized JSON:\n%s\n" json


        // 🔹 JSON 역직렬화
        let deserializedContainer = EmJson.FromJson<IOParam>(json)
        printfn "Deserialized Container: %A" deserializedContainer

        ()

    ()

(*


open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System

[<AbstractClass>]
type TypedAddress(address: string, typ: Type) =
    [<JsonProperty(Order = -98)>] member val Address = address with get, set
    [<JsonProperty(Order = -97)>] member val Type = typ with get, set

type InputParam<'T>(address: string, ?min: 'T, ?max: 'T) =
    inherit TypedAddress(address, typedefof<'T>)
    member val Min = min with get, set
    member val Max = max with get, set

type OutputParam(address: string, typ: Type) =
    inherit TypedAddress(address, typ)

/// 타입 정보를 포함하여 JSON 직렬화
let serializeWithType (obj: obj) =
    let settings = JsonSerializerSettings(TypeNameHandling = TypeNameHandling.Auto)
    JsonConvert.SerializeObject(obj, settings)

/// JSON 문자열에서 타입을 확인한 후 동적으로 Deserialize 수행
let deserializeDynamic (json: string) =
    let jObject = JObject.Parse(json)
    let typeName = jObject.["Type"].ToString()  // 저장된 Type 정보 가져오기
    let resolvedType = Type.GetType(typeName)   // 해당 타입을 실제 타입으로 변환
    let genericType = typedefof<InputParam<_>>.MakeGenericType(resolvedType) // GenericType 생성
    JsonConvert.DeserializeObject(json, genericType) // 동적 Deserialize 수행

// ✅ 테스트 데이터
let inputParam = InputParam<int>("address", min = 0, max = 1)

// 🔹 JSON 직렬화
let jsonStr = serializeWithType inputParam
printfn "Serialized JSON:\n%s\n" jsonStr

// 🔹 동적 역직렬화
let deserializedObj = deserializeDynamic jsonStr
printfn "Deserialized Object: %A" deserializedObj


*)