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

type ProjectProperties() =
    inherit DsPropertiesBase()
    member val ProjectMemo = nullString with get, set

type DsSystemProperties() =
    inherit DsPropertiesBase()
    // 이하는 sample attributes. // TODO: remove samples
    member val Boolean = false with get, set
    member val Integer = 0     with get, set
    member val UInt64  = 0UL   with get, set
    member val Single  = 0.0f  with get, set
    member val Double  = 0.0   with get, set
    member val Text    = nullString with get, set
    static member Create(?dsSystem:IRtSystem) = createExtended<DsSystemProperties>() |> tee (fun p -> p.RawParent <- dsSystem.Cast<Unique>())

type FlowProperties() =
    inherit DsPropertiesBase()
    member val FlowMemo = nullString with get, set
    static member Create(?flow:IRtFlow) = createExtended<FlowProperties>() |> tee (fun p -> p.RawParent <- flow.Cast<Unique>())

type WorkProperties() =
    inherit DsPropertiesBase()
    member val WorkMemo = nullString with get, set
    static member Create(?work:IRtWork) = createExtended<WorkProperties>() |> tee (fun p -> p.RawParent <- work.Cast<Unique>())

type CallProperties() =
    inherit DsPropertiesBase()
    member val CallMemo = nullString with get, set
    static member Create(?call:IRtCall) = createExtended<CallProperties>() |> tee (fun p -> p.RawParent <- call.Cast<Unique>())

/// Button, Lamp, Condition, Action 의 base class: 다형성(polymorphic)을 갖는 system entity
type [<AbstractClass>] BLCABase() =
    inherit JsonPolymorphic()
    interface IWithTagWithSpecs
    member val IOTags = IOTagsWithSpec() with get, set
    [<JsonIgnore>] member x.IOTagsJson = IOTagsWithSpec.Jsonize x.IOTags
    [<JsonIgnore>] member val Flows = ResizeArray<IRtFlow>() with get, set



