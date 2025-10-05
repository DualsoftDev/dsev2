namespace Ev2.Core.FS

open System
open System.Linq
open System.Data
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base
open Dual.Common.Db.FS


[<AbstractClass>]
type RtUnique() = // ToNjObj, ToNj
    inherit Unique()
    interface IRtUnique

    abstract member ToNjObj : unit -> INjUnique
    /// Runtime 객체를 Newtonsoft JSON 객체로 변환
    default x.ToNjObj() = fwdRtObj2NjObj x

    member x.ToNj<'T when 'T :> INjUnique>() : 'T = x.ToNjObj() :?> 'T

/// 다형성(polymorphic)을 갖는 system entity
type [<AbstractClass>] JsonPolymorphic() =
    inherit RtUnique()

    static member private getSettings(settings:JsonSerializerSettings option) =
        settings |?? (fun () -> EmJson.CreateDefaultSettings(DateFormatString = DateFormatString))

    member x.ToJson(?settings:JsonSerializerSettings) = EmJson.ToJson(x, JsonPolymorphic.getSettings(settings))

    static member FromJson<'T when 'T :> JsonPolymorphic and 'T :> IUnique and 'T : (new : unit -> 'T) and 'T : not struct>(json:string, ?settings:JsonSerializerSettings) : 'T =
        let settings = JsonPolymorphic.getSettings settings
        match JToken.Parse(json) with
        | :? JObject as jobj ->
            let mutable typeToken = Unchecked.defaultof<JToken>
            let hasTypeMetadata =
                jobj.TryGetValue("$type", StringComparison.Ordinal, &typeToken)
                && typeToken.Type = JTokenType.String
                && not (String.IsNullOrWhiteSpace(typeToken.Value<string>()))

            if hasTypeMetadata then
                EmJson.FromJson<'T>(json, settings)
            else
                let instance = createExtended<'T>()
                JsonConvert.PopulateObject(json, instance, settings)
                instance
        | _ -> EmJson.FromJson<'T>(json, settings)

    static member internal FromJson(json:string, targetType:Type, ?settings:JsonSerializerSettings) : JsonPolymorphic =
        if isNull targetType then invalidArg "targetType" "타겟 타입이 null 입니다."
        if String.IsNullOrWhiteSpace json then invalidArg "json" "Json 문자열이 비어 있습니다."
        let settings = JsonPolymorphic.getSettings(settings)
        JsonConvert.DeserializeObject(json, targetType, settings) :?> JsonPolymorphic

    member x.DeepClone<'T when 'T :> JsonPolymorphic and 'T :> IUnique and 'T : (new : unit -> 'T) and 'T : not struct>() : 'T =
        x.ToJson() |> JsonPolymorphic.FromJson<'T>

    member x.DeepClone() : JsonPolymorphic =
        x.ToJson() |> fun json -> JsonPolymorphic.FromJson(json, x.GetType())

type [<AbstractClass>] DsPropertiesBase() =
    inherit JsonPolymorphic()
    override x.ShouldSerializeId() = false
    override x.ShouldSerializeGuid() = false
    static member CreateExtended<'T when 'T :> DsPropertiesBase and 'T : (new : unit -> 'T) and 'T : not struct>(container:Unique option) : 'T =
        createExtended<'T>() |> tee (fun p -> p.RawParent <- container)

type ProjectProperties() =
    inherit DsPropertiesBase()
    member val Database = getNull<DbProvider>() with get, set
    member val AasxPath = nullString with get, set
    member val Author = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
    member val Version = Version() with get, set
    member val Description = nullString with get, set
    member val DateTime = now().TruncateToSecond() with get, set
    member val ProjectMemo = nullString with get, set
    static member Create(?container:IRtProject) = DsPropertiesBase.CreateExtended<ProjectProperties>(container.Cast<Unique>())

type DsSystemProperties() =
    inherit DsPropertiesBase()
    member val Author = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
    member val EngineVersion = Version() with get, set
    member val LangVersion = Version() with get, set
    member val Description = nullString with get, set
    member val DateTime = now().TruncateToSecond() with get, set

    // 이하는 sample attributes. // TODO: remove samples
    member val Text    = nullString with get, set

    static member Create(?container:IRtSystem) = DsPropertiesBase.CreateExtended<DsSystemProperties>(container.Cast<Unique>())

type FlowProperties() =
    inherit DsPropertiesBase()
    member val FlowMemo = nullString with get, set
    static member Create(?container:IRtFlow) = DsPropertiesBase.CreateExtended<FlowProperties>(container.Cast<Unique>())

type WorkProperties() =
    inherit DsPropertiesBase()
    member val WorkMemo = nullString with get, set
    static member Create(?container:IRtWork) = DsPropertiesBase.CreateExtended<WorkProperties>(container.Cast<Unique>())

type CallProperties() =
    inherit DsPropertiesBase()
    member val CallMemo = nullString with get, set
    static member Create(?container:IRtCall) = DsPropertiesBase.CreateExtended<CallProperties>(container.Cast<Unique>())

type ApiCallProperties() =
    inherit DsPropertiesBase()
    member val ApiCallMemo = nullString with get, set
    static member Create(?container:IRtApiCall) = DsPropertiesBase.CreateExtended<ApiCallProperties>(container.Cast<Unique>())

type ApiDefProperties() =
    inherit DsPropertiesBase()
    member val ApiDefMemo = nullString with get, set
    static member Create(?container:IRtApiDef) = DsPropertiesBase.CreateExtended<ApiDefProperties>(container.Cast<Unique>())


type ButtonProperties() =
    inherit DsPropertiesBase()
    member val ButtonMemo = nullString with get, set
    static member Create(?container:IRtButton) = DsPropertiesBase.CreateExtended<ButtonProperties>(container.Cast<Unique>())

type LampProperties() =
    inherit DsPropertiesBase()
    member val LampMemo = nullString with get, set
    static member Create(?container:IRtLamp) = DsPropertiesBase.CreateExtended<LampProperties>(container.Cast<Unique>())

type ConditionProperties() =
    inherit DsPropertiesBase()
    member val ConditionMemo = nullString with get, set
    static member Create(?container:IRtCondition) = DsPropertiesBase.CreateExtended<ConditionProperties>(container.Cast<Unique>())

type ActionProperties() =
    inherit DsPropertiesBase()
    member val ActionMemo = nullString with get, set
    static member Create(?container:IRtAction) = DsPropertiesBase.CreateExtended<ActionProperties>(container.Cast<Unique>())



/// Button, Lamp, Condition, Action 의 base class: 다형성(polymorphic)을 갖는 system entity
type [<AbstractClass>] BLCABase() =
    inherit JsonPolymorphic()
    interface IWithTagWithSpecs
    member val IOTags = IOTagsWithSpec() with get, set
    [<JsonIgnore>] member x.IOTagsJson = IOTagsWithSpec.Jsonize x.IOTags
    [<JsonIgnore>] member val Flows = ResizeArray<IRtFlow>() with get, set


[<AutoOpen>]
module internal DsPropertiesHelper =
    let inline assignFromJson<'TOwner,'T when 'TOwner :> Unique and 'T :> DsPropertiesBase and 'T :> JsonPolymorphic and 'T : (new : unit -> 'T) and 'T : not struct>
        (owner:'TOwner) (create: unit -> 'T) (json:string) : 'T
        =
        json |> String.toOption |-> JsonPolymorphic.FromJson<'T> |?? create
        |> tee ( setParentI owner )


    let inline cloneProperties<'TOwner,'T when 'TOwner :> Unique and 'T :> DsPropertiesBase and 'T :> JsonPolymorphic and 'T : (new : unit -> 'T) and 'T : not struct>
        (owner:'TOwner) (source:'T) (create: unit -> 'T) : 'T
        =
        source |> toOption |-> _.DeepClone<'T>() |?? create
        |> tee ( setParentI owner )
