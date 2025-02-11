open System.Diagnostics.CodeAnalysis







#r "nuget: Newtonsoft.Json"
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Reflection


///// 기존 Newtonsoft.Json.JsonConverterAttribute 와 충돌 방지
//[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
//[<AllowNullLiteral>]
//type MyCustomJsonConverterAttribute(converterType: Type) =
//    inherit Attribute()
//    member val ConverterType = converterType with get



/// 기존 Newtonsoft.Json.JsonConverterAttribute 와 충돌 방지
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
[<AllowNullLiteral>]
type CustomJsonConverterAttribute(converterTypeName: string) =
    inherit Attribute()
    member val ConverterTypeName = converterTypeName with get

type ObjectHolder (typ: Type, ?value: obj) =
    member val TypeName = typ.AssemblyQualifiedName with get
    //member val Value = value |? null with get, set
    member val Value = defaultArg value null with get, set
    [<JsonIgnore>] member x.Type: Type = typ // Type 정보를 반환

    new () = ObjectHolder(typeof<obj>, null)

    static member internal JsonSettings =
        let settings = JsonSerializerSettings()
        settings.TypeNameHandling <- TypeNameHandling.Auto
        settings.NullValueHandling <- NullValueHandling.Ignore
        settings.Converters.Add(ObjectHolderConverter())
        settings

    //member x.ToJson () : string =
    //    JsonConvert.SerializeObject(x, ObjectHolder.JsonSettings)  // 명확한 타입 지정

    //static member FromJson (json: string) : ObjectHolder =
    //    JsonConvert.DeserializeObject<ObjectHolder>(json, ObjectHolder.JsonSettings)

and
    [<CustomJsonConverterAttribute("ObjectHolderConverter")>] // 문자열로 클래스 이름 지정
    ObjectHolderConverter() =
    inherit JsonConverter<ObjectHolder>()

    override this.WriteJson(writer, value:ObjectHolder, serializer) =
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
    member val Holder3 = ObjectHolder(typeof<uint64>, 9999UL) with get, set

    //member this.ToJson () : string =
    //    JsonConvert.SerializeObject(this :> obj, ObjectHolder.JsonSettings)  // 명확한 타입 지정

    //static member FromJson (json: string) : ContainerClass =
    //    JsonConvert.DeserializeObject<ContainerClass>(json, ObjectHolder.JsonSettings)


module GlobalJsonSettings =
    let private collectConverters () =
        // 현재 로드된 어셈블리에서 CustomJsonConverterAttribute가 있는 모든 클래스 검색
        let assemblies = AppDomain.CurrentDomain.GetAssemblies()
        let converterTypes =
            assemblies
            |> Seq.collect (fun asm -> asm.GetTypes())
            |> Seq.choose (fun t ->
                let attr = t.GetCustomAttribute<CustomJsonConverterAttribute>()
                if attr <> null then
                    let converterType = Type.GetType(attr.ConverterTypeName)
                    if converterType <> null then Some converterType else None
                else None)
            |> Seq.distinct
            |> Seq.toList

        // 변환기 인스턴스 생성
        converterTypes
        |> List.map (fun t -> Activator.CreateInstance(t) :?> JsonConverter)

    let JsonSettings =
        let settings = JsonSerializerSettings()
        settings.TypeNameHandling <- TypeNameHandling.Auto
        settings.NullValueHandling <- NullValueHandling.Ignore

        // 자동 수집된 컨버터 추가
        let converters = collectConverters()
        converters |> List.iter (fun c -> settings.Converters.Add(c))

        settings


let container = ContainerClass()
//let json = container.ToJson()
let json = JsonConvert.SerializeObject(container, GlobalJsonSettings.JsonSettings)

printfn "Serialized JSON: %s" json

//let deserializedContainer = ContainerClass.FromJson(json)
let deserializedContainer = JsonConvert.DeserializeObject<ContainerClass>(json)
printfn "Deserialized Holder1: Type = %s, Value = %O" deserializedContainer.Holder1.TypeName deserializedContainer.Holder1.Value
printfn "Deserialized Holder2: Type = %s, Value = %O" deserializedContainer.Holder2.TypeName deserializedContainer.Holder2.Value
printfn "Deserialized Holder3: Type = %s, Value = %O" deserializedContainer.Holder3.TypeName deserializedContainer.Holder3.Value

printfn "Deserialized Holder1: Type = %s" deserializedContainer.Holder1.Type.Name
