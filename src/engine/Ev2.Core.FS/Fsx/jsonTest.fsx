


#r "nuget: Newtonsoft.Json"
open Newtonsoft.Json
open Newtonsoft.Json.Linq


#r @"F:\Git\ds\Submodules\nuget\bin\net8.0\Dual.Common.Core.dll"
#r @"F:\Git\ds\Submodules\nuget\bin\net8.0\Dual.Common.Core.FS.dll"
#r @"F:\Git\ds\Submodules\nuget\bin\net8.0\Dual.Common.Base.CS.dll"
#r @"F:\Git\ds\Submodules\nuget\bin\net8.0\Dual.Common.Base.FS.dll"


open System
open System.IO
open System.Collections.Generic

open Dual.Common
open Dual.Common.Core.FS
open Dual.Common.Base.CS
open Dual.Common.Base.FS


// ✅ `TypeNameHandling.Auto`를 특정 필드에만 적용하는 커스텀 변환기
type TypeAwareConverter<'T>() =
    inherit JsonConverter()

    override _.CanConvert(objectType) =
        objectType = typeof<'T>

    override _.WriteJson(writer, value, serializer) =
        let settings = JsonSerializerSettings(TypeNameHandling = TypeNameHandling.Auto)
        let json = JsonConvert.SerializeObject(value, settings)
        writer.WriteRawValue(json)

    override _.ReadJson(reader, objectType, existingValue, serializer) =
        let jToken = JToken.Load(reader)
        let settings = JsonSerializerSettings(TypeNameHandling = TypeNameHandling.Auto)
        JsonConvert.DeserializeObject<'T>(jToken.ToString(), settings)


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

// ✅ 컨테이너 클래스 (필요한 필드만 `TypeNameHandling.Auto` 적용)
type Container() =
    member val Name = "MainContainer" with get, set

    [<JsonConverter(typeof<TypeAwareConverter<obj list>>)>]
    member val Parameters: obj list = [] with get, set



// ✅ 테스트 데이터
let param1 = InputParam<int>("param1", min = 10, max = 100)
let param2 = InputParam<float>("param2", min = 1.5, max = 9.8)
let container = Container()
container.Parameters <- [ box param1; box param2 ]  // obj 리스트에 저장

// 🔹 JSON 직렬화
let json = EmJson.ToJson(container)
printfn "Serialized JSON:\n%s\n" json


// 🔹 JSON 역직렬화
let deserializedContainer = EmJson.FromJson<Container>(json)
printfn "Deserialized Container: %A" deserializedContainer








// https://github.com/manuc66/JsonSubTypes/blob/master/JsonSubTypes.Tests/DemoCustomSubclassMappingTests.cs

#r "nuget: JsonSubTypes"
#r "nuget: Newtonsoft.Json"
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open JsonSubTypes

[<AutoOpen>]
module rec TypePropName =
    [<JsonConverter(typedefof<JsonSubtypes>, "Kind")>]
    type IAnimal =
        abstract member Kind: string with get

    type Dog() =
        interface IAnimal with
            member _.Kind = "Dog"
        member val Breed = "" with get, set


    type Cat() =
        interface IAnimal with
            member _.Kind = "Cat"
        member val Declawed = false with get, set

    let animal = JsonConvert.DeserializeObject<IAnimal>("{\"Kind\":\"Dog\",\"Breed\":\"Jack Russell Terrier\"}");

[<AutoOpen>]
module rec Module =
    [<AbstractClass>]
    [<JsonConverter(typedefof<JsonSubtypes>, "Sound")>]
    [<JsonSubtypes.KnownSubType(typedefof<Dog>, "Bark")>]
    [<JsonSubtypes.KnownSubType(typedefof<Cat>, "Meow")>]
    type Animal() =
        abstract member Sound: string
        member val Color = "" with get, set

    type Dog() =
        inherit Animal()
        override x.Sound = "Bark";
        member val Breed = "" with get, set


    type Cat() =
        inherit Animal()
        override x.Sound = "Meow";
        member val Declawed = false with get, set


    let animal1 = JsonConvert.DeserializeObject<Animal>("{\"Sound\":\"Bark\",\"Breed\":\"Jack Russell Terrier\"}");
    //Assert.AreEqual("Jack Russell Terrier", (animal as Dog)?.Breed);

    let animal2 = JsonConvert.DeserializeObject<Animal>("{\"Sound\":\"Meow\",\"Declawed\":\"true\"}");
    //Assert.AreEqual(true, (animal as Cat)?.Declawed);












open Newtonsoft.Json
open Newtonsoft.Json.Linq

type TypeNameConverter() =
    inherit JsonConverter()

    override _.CanConvert(objectType) = true

    override _.WriteJson(writer, value, serializer) =
        let settings = JsonSerializerSettings(TypeNameHandling = TypeNameHandling.Auto)
        let json = JsonConvert.SerializeObject(value, settings)
        writer.WriteRawValue(json)

    override _.ReadJson(reader, objectType, existingValue, serializer) =
        let settings = JsonSerializerSettings(TypeNameHandling = TypeNameHandling.Auto)
        let token = JToken.Load(reader)
        JsonConvert.DeserializeObject(token.ToString(), objectType, settings)

type GenericContainer<'T>(value: 'T) =
    member _.Value = value

    [<JsonConverter(typeof<TypeNameConverter>)>]
    member val Wrapped = value with get, set

let obj = GenericContainer<int>(42)

let json = JsonConvert.SerializeObject(obj)
printfn "Serialized: %s" json

let deserialized = JsonConvert.DeserializeObject<GenericContainer<int>>(json)
printfn "Deserialized: %A" deserialized.Wrapped
//Serialized: {"Wrapped":{"$type":"System.Int32, mscorlib","value":42}}
//Deserialized: 42








type IShape =
    abstract member Area: unit -> float

type Circle(radius: float) =
    interface IShape with
        member _.Area() = System.Math.PI * radius * radius

type ShapeContainer<'T when 'T :> IShape>(shape: 'T) =
    [<JsonConverter(typeof<TypeNameConverter>)>]
    member val Shape = shape with get, set

let circle = Circle(5.0) :> IShape
let shapeContainer = ShapeContainer<IShape>(circle)

let json = JsonConvert.SerializeObject(shapeContainer)
printfn "Serialized: %s" json

let deserialized = JsonConvert.DeserializeObject<ShapeContainer<IShape>>(json)
printfn "Deserialized Area: %f" (deserialized.Shape.Area())

//Serialized: {"Shape":{"$type":"Circle, YourNamespace","radius":5.0}}
//Deserialized Area: 78.539816
























#r "nuget: Newtonsoft.Json"
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq

type ObjectHolder (typ: Type, ?value: obj) =
    member val TypeName = typ.AssemblyQualifiedName with get
    member val Value = value with get, set

    new () = ObjectHolder(typeof<obj>, null)

    member this.ToJson () : string =
        JsonConvert.SerializeObject(box this, ObjectHolder.JsonSettings)  // 명확한 타입 지정

    static member FromJson (json: string) : ObjectHolder =
        JsonConvert.DeserializeObject<ObjectHolder>(json, ObjectHolder.JsonSettings)

    static member private JsonSettings =
        let settings = JsonSerializerSettings()
        settings.TypeNameHandling <- TypeNameHandling.Auto
        settings.NullValueHandling <- NullValueHandling.Ignore
        settings.Converters.Add(ObjectHolderConverter())
        settings

and ObjectHolderConverter() =
    inherit JsonConverter<ObjectHolder>()

    override this.WriteJson(writer, value, serializer) =
        let obj = JObject()
        obj["TypeName"] <- JToken.FromObject(value.TypeName)
        match value.Value with
        | null -> obj["Value"] <- JValue.CreateNull()
        | _ -> obj["Value"] <- JToken.FromObject(value.Value, serializer)
        obj.WriteTo(writer)

    override this.ReadJson(reader, objectType, existingValue, hasExistingValue, serializer) =
        let obj = JObject.Load(reader)
        let typeName = obj["TypeName"].ToObject<string>()
        let typ = Type.GetType(typeName) // 타입 정보를 가져옴
        let value =
            match obj.TryGetValue("Value") with
            | (true, token) when token.Type <> JTokenType.Null -> token.ToObject(typ, serializer)
            | _ -> null
        ObjectHolder(typ, value)

/// ObjectHolder 를 포함하는 클래스
type ContainerClass() =
    member val Holder1 = ObjectHolder(typeof<int>, 100) with get, set
    member val Holder2 = ObjectHolder(typeof<string>, "Hello, World!") with get, set

    member this.ToJson () : string =
        JsonConvert.SerializeObject(this :> obj, ObjectHolder.JsonSettings)  // 명확한 타입 지정

    static member FromJson (json: string) : ContainerClass =
        JsonConvert.DeserializeObject<ContainerClass>(json, ObjectHolder.JsonSettings)




let container = ContainerClass()
let json = container.ToJson()
printfn "Serialized JSON: %s" json

let deserializedContainer = ContainerClass.FromJson(json)
printfn "Deserialized Holder1: Type = %s, Value = %O" deserializedContainer.Holder1.TypeName deserializedContainer.Holder1.Value
printfn "Deserialized Holder2: Type = %s, Value = %O" deserializedContainer.Holder2.TypeName deserializedContainer.Holder2.Value
