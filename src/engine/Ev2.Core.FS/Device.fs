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
        [<JsonProperty(Order = -97)>] member val ObjectHolder = ObjectHolder.Create() with get, set

    type InputParam<'T>(address: string, ?min:'T, ?max:'T) =
        inherit TypedAddress(address, typedefof<'T>)
        member val Min = min with get, set
        member val Max = max with get, set

    type OutputParam<'T>(address: string, ?value:'T) =
        inherit TypedAddress(address, typedefof<'T>)

    type IOParam<'T>(input:InputParam<'T>, output:OutputParam<'T>) =
        [< JsonConverter(typeof<TypeAwareConverter<obj>> )>]
        member val Input:InputParam<'T> = input
        [<JsonConverter(typeof<TypeAwareConverter<obj>> )>]
        member val Output = output
        member val Others = ["Hello"; "World"]




    // ✅ 컨테이너 클래스 (필요한 필드만 `TypeNameHandling.Auto` 적용)
    type Container() =
        member val Name = "MainContainer" with get, set

        [<JsonConverter(typeof<TypeAwareConverter<obj list>>)>]
        member val Parameters: obj list = [] with get, set


    let inputParam = InputParam<double>("address", min=0.0, max=1.1)
    let xxx = EmJson.ToJson(inputParam)
    let inputParam2 = EmJson.FromJson<InputParam<double>>(xxx)
    ()

    let f (jobj:JObject) = jobj.["Type"].ToString() |> Type.GetType
    let inputParam3 = EmJson.FromJson<InputParam<_>>(xxx, f)
    ()

    let testMe() =
        // ✅ 테스트 데이터
        let param1 = InputParam<uint>("param1", min = 10u, max = 100u)
        let param2 = OutputParam<uint>("param2", value = 20u)
        let param3 = InputParam<int>("param1", min = 10, max = 100)

        let ioParam = IOParam<uint>(input=param1, output=param2)


        let container = Container()
        container.Parameters <- [ box param1; box param2 ]  // obj 리스트에 저장

        // 🔹 JSON 직렬화
        let json = EmJson.ToJson(container)
        let json = EmJson.ToJson(ioParam)
        printfn "Serialized JSON:\n%s\n" json


        // 🔹 JSON 역직렬화
        let deserializedContainer = EmJson.FromJson<Container>(json)
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