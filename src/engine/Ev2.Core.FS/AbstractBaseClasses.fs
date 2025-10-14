namespace Ev2.Core.FS

open System
open Newtonsoft.Json
open Newtonsoft.Json.Converters;

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
type [<AbstractClass>] DsJsonPolymorphic() =
    inherit RtUnique()

    let getSettings(settings:JsonSerializerSettings option) =
        settings |?? (fun () -> EmJson.CreateDefaultSettings(DateFormatString = DateFormatString))

    interface IJsonPolymorphic with
        member x.ToJson(?settings:JsonSerializerSettings) = x.ToJson(?settings=settings)
    member x.ToJson(?settings:JsonSerializerSettings) = EmJson.ToJson(x, getSettings(settings))


type [<AbstractClass>] DsPropertiesBase() =
    inherit DsJsonPolymorphic()
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
    [<JsonConverter(typeof<StringEnumConverter>)>] member val CallType = DbCallType.Normal with get, set
    member val IsDisabled = false with get, set
    member val Timeout = Option<int>.None with get, set
    [<JsonProperty("ApiCalls")>]member val ApiCallGuids = ResizeArray<Guid>() with get, set
    member val CallMemo = nullString with get, set
    static member Create() = createExtendedProperties<CallProperties>()
    member x.ShouldSerializeApiCalls()         = x.ApiCallGuids.NonNullAny()
    member x.ShouldSerializeCallType()         = x.CallType <> DbCallType.Normal
    member x.ShouldSerializeIsDisabled()       = x.IsDisabled
    member x.ShouldSerializeTimeout()          = x.Timeout.IsSome


type ApiCallProperties() =
    inherit DsPropertiesBase()
    [<JsonProperty("ApiDef")>] member val ApiDefGuid = emptyGuid with get, set
    member val ApiDefId = Option<Id>.None with get, set
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
    member x.ShouldSerializeTxGuid() = x.TxGuid <> Guid.Empty
    member x.ShouldSerializeRxGuid() = x.RxGuid <> Guid.Empty


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
    inherit DsJsonPolymorphic()
    interface IWithTagWithSpecs
    interface IDsObject
    member val IOTags = IOTagsWithSpec() with get, set
    [<JsonIgnore>] member x.IOTagsJson = IOTagsWithSpec.Jsonize x.IOTags
    [<JsonIgnore>] member val Flows = ResizeArray<IRtFlow>() with get, set


