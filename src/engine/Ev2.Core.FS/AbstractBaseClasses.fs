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

    static member FromJson<'T when 'T :> JsonPolymorphic and 'T : (new : unit -> 'T) and 'T : not struct>(json:string, ?settings:JsonSerializerSettings) : 'T =
        match json |> String.toOption with
        | None -> Unchecked.defaultof<'T>
        | Some json ->
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
                    let instance = createExtendedHelper<'T>()
                    JsonConvert.PopulateObject(json, instance, settings)
                    instance :?> 'T
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
    interface IDsProperties
    override x.ShouldSerializeId() = false
    override x.ShouldSerializeGuid() = false

type ProjectProperties() =
    inherit DsPropertiesBase()
    member val Database = getNull<DbProvider>() with get, set
    member val AasxPath = nullString with get, set
    member val Author = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
    member val Version = Version() with get, set
    member val Description = nullString with get, set
    member val DateTime = now().TruncateToSecond() with get, set
    member val ProjectMemo = nullString with get, set
    static member Create() = createExtendedProperties<ProjectProperties>()

type DsSystemProperties() =
    inherit DsPropertiesBase()
    member val Author = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
    member val EngineVersion = Version() with get, set
    member val LangVersion = Version() with get, set
    member val Description = nullString with get, set
    member val DateTime = now().TruncateToSecond() with get, set

    // 이하는 sample attributes. // TODO: remove samples
    member val Text    = nullString with get, set

    static member Create() = createExtendedProperties<DsSystemProperties>()

type FlowProperties() =
    inherit DsPropertiesBase()
    member val FlowMemo = nullString with get, set
    static member Create() = createExtendedProperties<FlowProperties>()

type WorkProperties() =
    inherit DsPropertiesBase()
    member val Motion = nullString with get, set
    member val Script = nullString with get, set
    member val ExternalStart = nullString with get, set
    member val IsFinished = false with get, set
    member val NumRepeat = 0 with get, set
    member val Period = 0 with get, set
    member val Delay = 0 with get, set
    member val WorkMemo = nullString with get, set
    static member Create() = createExtendedProperties<WorkProperties>()

type CallProperties() =
    inherit DsPropertiesBase()
    member val CallType = DbCallType.Normal with get, set
    member val IsDisabled = false with get, set
    member val Timeout = Option<int>.None with get, set
    member val ApiCallGuids = ResizeArray<Guid>() with get, set
    member val CallMemo = nullString with get, set
    static member Create() = createExtendedProperties<CallProperties>()

type ApiCallProperties() =
    inherit DsPropertiesBase()
    member val ApiDefGuid = emptyGuid with get, set
    member val InAddress = nullString with get, set
    member val OutAddress = nullString with get, set
    member val InSymbol = nullString with get, set
    member val OutSymbol = nullString with get, set
    member val ApiCallMemo = nullString with get, set
    static member Create() = createExtendedProperties<ApiCallProperties>()

type ApiDefProperties() =
    inherit DsPropertiesBase()
    member val IsPush = true with get, set
    member val TxGuid = emptyGuid with get, set
    member val RxGuid = emptyGuid with get, set
    member val Period = 0 with get, set
    member val ApiDefMemo = nullString with get, set
    static member Create() = createExtendedProperties<ApiDefProperties>()


type ButtonProperties() =
    inherit DsPropertiesBase()
    member val ButtonMemo = nullString with get, set
    static member Create() = createExtendedProperties<ButtonProperties>()

type LampProperties() =
    inherit DsPropertiesBase()
    member val LampMemo = nullString with get, set
    static member Create() = createExtendedProperties<LampProperties>()

type ConditionProperties() =
    inherit DsPropertiesBase()
    member val ConditionMemo = nullString with get, set
    static member Create() = createExtendedProperties<ConditionProperties>()

type ActionProperties() =
    inherit DsPropertiesBase()
    member val ActionMemo = nullString with get, set
    static member Create() = createExtendedProperties<ActionProperties>()



/// Button, Lamp, Condition, Action 의 base class: 다형성(polymorphic)을 갖는 system entity
type [<AbstractClass>] BLCABase() =
    inherit JsonPolymorphic()
    interface IWithTagWithSpecs
    interface IDsObject
    member val IOTags = IOTagsWithSpec() with get, set
    [<JsonIgnore>] member x.IOTagsJson = IOTagsWithSpec.Jsonize x.IOTags
    [<JsonIgnore>] member val Flows = ResizeArray<IRtFlow>() with get, set


//[<AutoOpen>]
//module internal DsPropertiesHelper =
//    let inline assignFromJson<'TOwner,'T when 'TOwner :> Unique and 'T :> DsPropertiesBase and 'T :> JsonPolymorphic and 'T : (new : unit -> 'T) and 'T : not struct>
//        (owner:'TOwner) (create: unit -> 'T) (json:string) : 'T
//        =
//        json |> String.toOption |-> JsonPolymorphic.FromJson<'T> |?? create
//        |> tee ( setParentI owner )


//    let inline cloneProperties<'TOwner,'T when 'TOwner :> Unique and 'T :> DsPropertiesBase and 'T :> JsonPolymorphic and 'T : (new : unit -> 'T) and 'T : not struct>
//        (owner:'TOwner) (source:'T) (create: unit -> 'T) : 'T
//        =
//        source |> toOption |-> _.DeepClone<'T>() |?? create
//        |> tee ( setParentI owner )
