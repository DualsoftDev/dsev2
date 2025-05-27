namespace Ev2.Core.FS

open System
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base
open System.IO

[<AutoOpen>]
module NewtonsoftJsonForwardDecls =
    type INjObject  = interface end
    type INjProject = inherit INjObject inherit IDsProject
    type INjSystem  = inherit INjObject inherit IDsSystem
    type INjFlow    = inherit INjObject inherit IDsFlow
    type INjWork    = inherit INjObject inherit IDsWork
    type INjCall    = inherit INjObject inherit IDsCall
    type INjApiCall = inherit INjObject inherit IDsCall
    type INjApiDef  = inherit INjObject inherit IDsCall
    type INjArrow   = inherit INjObject


    let mutable fwdOnNsJsonSerializing:  INjObject option->INjObject->unit = let dummy (parent:INjObject option) (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy
    let mutable fwdOnNsJsonDeserialized: INjObject option->INjObject->unit = let dummy (parent:INjObject option) (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy

/// Newtonsoft Json 호환 버젼
[<AutoOpen>]
module rec NewtonsoftJsonObjects =

    [<AbstractClass>]
    type NjUnique() as this =
        //inherit Unique()
        interface IUnique


        /// DB 저장시의 primary key id.  DB read/write 수행한 경우에만 Non-null
        [<JsonProperty(Order = -100)>] member val internal Id = nullableId with get, set
        ///// Database 의 primary id key.  Database 에 삽입시 생성
        [<JsonIgnore>] member x.OptId = x.Id |> Option.ofNullable

        [<JsonProperty(Order = -99)>] member val Name = nullString with get, set
        /// JSON 파일에 대한 comment.  눈으로 debugging 용도.  code 에서 사용하지 말 것.
        [<JsonProperty(Order = -98)>] member val private Type = this.GetType().Name

        /// Guid: 메모리에 최초 객체 생성시 생성
        [<JsonProperty(Order = -98)>] member val Guid:Guid = emptyGuid with get, set

        /// DateTime: 메모리에 최초 객체 생성시 생성
        [<JsonProperty(Order = -97)>] member val DateTime = minDate with get, set

        /// 자신의 container 에 해당하는 parent DS 객체.  e.g call -> work -> system -> project, flow -> system
        [<JsonIgnore>] member val RawParent = Option<NjUnique>.None with get, set

        /// Parent Guid : Json 저장시에는 container 의 parent 를 추적하면 되므로 json 에는 저장하지 않음
        [<JsonIgnore>] member x.PGuid = x.RawParent |-> _.Guid



        [<JsonIgnore>] member val DsObject:Unique = getNull<Unique>() with get, set
        [<JsonIgnore>] member x.DsRawParent:Unique option = x.DsObject.RawParent
        [<JsonIgnore>] member x.NjRawParent:NjUnique option = x.RawParent
        member x.Import(src:Unique) =
            match src with
            | :? DsUnique -> x.DsObject <- src
            | _ ->
                failwith "ERROR"

            x.Id        <- src.Id |> Option.toNullable
            x.Name      <- src.Name
            x.Guid      <- src.Guid
            x.DateTime  <- src.DateTime
            //x.RawParent <- src.RawParent
            //base.Import src

    type NjProject() =
        inherit NjUnique()
        interface INjProject

        member val LastConnectionString = null:string     with get, set
        member val Description          = null:string     with get, set
        member val Author               = null:string     with get, set
        member val Version              = Version()       with get, set
        [<JsonProperty(Order = 100)>] member val SystemPrototypes     = [||]:NjSystem[] with get, set

        [<JsonProperty(Order = 101)>] member val ActiveSystemGuids    = [||]:Guid[]     with get, set
        [<JsonProperty(Order = 102)>] member val PassiveSystemGuids   = [||]:Guid[]     with get, set

        static member FromDs(ds:DsProject) =
            NjProject(LastConnectionString=ds.LastConnectionString, Author=ds.Author, Version=ds.Version, Description=ds.Description)
            |> tee (fun z -> z.Import ds)

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnNsJsonSerializing  None x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnNsJsonDeserialized None x


    type NjSystem() =
        inherit NjUnique()
        interface INjSystem


        [<JsonProperty(Order = 101)>] member val Flows         = [||]:NjFlow[]     with get, set
        [<JsonProperty(Order = 102)>] member val Works         = [||]:NjWork[]     with get, set
        [<JsonProperty(Order = 103)>] member val Arrows        = [||]:NjArrow[]    with get, set
        member val OriginGuid    = Nullable<Guid>() with get, set

        member val Author        = nullString        with get, set
        member val EngineVersion = Version()         with get, set
        member val LangVersion   = Version()         with get, set
        member val Description   = nullString        with get, set

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnNsJsonSerializing  (x.RawParent >>= tryCast<INjObject>) x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnNsJsonDeserialized (x.RawParent >>= tryCast<INjObject>) x
        static member FromDs(ds:DsSystem) =
            let originGuid = ds.OriginGuid |> Option.toNullable
            NjSystem(OriginGuid=originGuid, Author=ds.Author, LangVersion=ds.LangVersion, EngineVersion=ds.EngineVersion, Description=ds.Description)
            |> tee (fun z ->
                z.Import ds
                z.Flows  <- ds.Flows  |-> NjFlow.FromDs  |> toArray
                z.Arrows <- ds.Arrows |-> NjArrow.FromDs |> toArray
                z.Works  <- ds.Works  |-> NjWork.FromDs  |> toArray
            )

    type NjFlow () =
        inherit NjUnique()
        interface INjFlow

        static member FromDs(ds:DsFlow) =
            NjFlow() |> tee (fun z -> z.Import ds)

    type NjWork () =
        inherit NjUnique()
        interface INjWork
        member val FlowGuid = null:string with get, set
        member val Calls: NjCall[] = [||] with get, set
        member val Arrows:NjArrow[] = [||] with get, set
        static member FromDs(ds:DsWork) =
            NjWork()
            |> tee (fun z ->
                z.Import ds
                z.Calls <- ds.Calls |-> NjCall.FromDs |> toArray
                z.Arrows <- ds.Arrows |-> NjArrow.FromDs |> toArray
                z.FlowGuid <- ds.OptFlow |-> (fun flow -> guid2str flow.Guid) |? null
            )

    type NjArrow() =
        inherit NjUnique()
        interface INjArrow
        member val Source = null:string with get, set
        member val Target = null:string with get, set
        static member FromDs(ds:IArrow) =
            NjArrow()
            |> tee (fun z ->
                z.Import (ds :?> Unique)
                z.Source <- guid2str (ds.GetSource().Guid)
                z.Target <- guid2str (ds.GetTarget().Guid)
            )

    type NjCall() =
        inherit NjUnique()
        interface INjCall
        member val CallType = DbCallType.Normal.ToString() with get, set
        member val ApiCalls: NjApiCall[] = [||] with get, set

        static member FromDs(ds:DsCall) =
            NjCall() |> tee (fun z -> z.Import ds)

    type NjApiCall() =
        inherit NjUnique()
        interface INjApiCall


    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec internal onNsJsonSerializing (njParent:INjObject option) (njObj:INjObject) =
        match njObj with
        | :? NjUnique as uniq ->
            uniq.Import uniq.DsObject
        | _ -> ()

        match njObj with
        | :? NjProject as nj ->
            let ds = nj.DsObject :?> DsProject
            nj.SystemPrototypes <-
                let originals, copies = ds.ActiveSystems |> partition (fun s -> s.OriginGuid.IsNone)
                let distinctCopies = copies |> distinctBy _.Guid
                originals @ distinctCopies |-> NjSystem.FromDs |> toArray
            nj.ActiveSystemGuids  <- ds.ActiveSystems  |-> _.Guid |> toArray
            nj.PassiveSystemGuids <- ds.PassiveSystems |-> _.Guid |> toArray
            nj.LastConnectionString <- ds.LastConnectionString

            nj.SystemPrototypes |> iter (onNsJsonSerializing (Some nj))

        | :? NjSystem as sys ->
            sys.Arrows |> iter (onNsJsonSerializing (Some sys))
            sys.Flows  |> iter (onNsJsonSerializing (Some sys))
            sys.Works  |> iter (onNsJsonSerializing (Some sys))

        | :? NjFlow as flow ->
            ()

        | :? NjWork as work ->
            work.Arrows |> iter (onNsJsonSerializing (Some work))
            ()
            //work.Calls |> iter onSerializing
            //work.TryGetFlow() |> iter (fun f -> work.FlowGuid <- guid2str f.Guid)
        | :? NjCall as call ->
            ()
        | :? NjArrow as arrow ->
            ()
        | _ -> failwith "ERROR.  확장 필요?"




    /// JSON 읽고 나서 메모리 구조에 후처리 작업
    let rec internal onNsJsonDeserialized (njParent:INjObject option) (njObj:INjObject) =
        match njObj with
        | :? NjUnique as uniq ->
            ()
        | _ ->
            ()

        match njObj with
        | :? NjProject as proj ->
            proj.SystemPrototypes |> iter (onNsJsonDeserialized (Some proj))
            proj.DsObject <-
                let systems = proj.SystemPrototypes |-> (fun z -> z.DsObject :?> DsSystem)
                let actives = systems |> filter (fun s -> proj.ActiveSystemGuids |> contains (s.Guid))
                let passives = systems |> filter (fun s -> proj.PassiveSystemGuids |> contains (s.Guid))
                noop()
                let id = n2o proj.Id
                DsProject(proj.Name, proj.Guid, actives, passives, proj.DateTime, ?id=id,
                    author=proj.Author, version=proj.Version, description=proj.Description,
                    LastConnectionString=proj.LastConnectionString)
                |> tee(fun z -> systems |> iter (fun s -> s.RawParent <- Some z))

        | :? NjSystem as nj ->
            nj.Arrows
            |> iter (fun (a:NjArrow) ->
                let works = nj.Works |-> (fun z -> z.DsObject :?> DsWork)
                let src = works |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = works |> find(fun w -> w.Guid = s2guid a.Target)
                a.DsObject <- ArrowBetweenWorks(a.Guid, src, tgt, a.DateTime, ?id=a.OptId)
                ()
                )

            // flows, works, arrows 의 Parent 를 this(system) 으로 설정
            nj.Arrows |> iter (fun z -> z.RawParent <- Some nj)
            nj.Flows  |> iter (fun z -> z.RawParent <- Some nj)
            nj.Works  |> iter (fun z -> z.RawParent <- Some nj)

            // 하부 구조에 대해서 재귀적으로 호출
            nj.Flows |> iter (onNsJsonDeserialized (Some nj))
            nj.Works |> iter (onNsJsonDeserialized (Some nj))

            let flows = nj.Flows |-> (fun z -> z.DsObject :?> DsFlow)

            let works = [|
                for w in nj.Works do
                    let optFlow =
                        if w.FlowGuid.NonNullAny() then
                            flows |> tryFind (fun f -> f.Guid = s2guid w.FlowGuid)
                        else
                            None
                    let calls = w.Calls |-> (fun z -> z.DsObject :?> DsCall)
                    let arrows = w.Arrows |-> (fun z -> z.DsObject :?> ArrowBetweenCalls)
                    let dsWork = DsWork.Create(w.Name, w.Guid, calls, arrows, optFlow, w.DateTime, ?id=w.OptId)
                    yield dsWork
                    w.DsObject <- dsWork
            |]
            let arrows = nj.Arrows |-> (fun z -> z.DsObject :?> ArrowBetweenWorks)
            nj.DsObject <-
                DsSystem.Create(nj.Name, nj.Guid, flows, works, arrows, nj.DateTime, ?id=nj.OptId,
                                author=nj.Author, langVersion=nj.LangVersion, engineVersion=nj.EngineVersion, description=nj.Description)

        | :? NjFlow as nj ->
            nj.DsObject <- DsFlow(nj.Name, nj.Guid, nj.DateTime, ?id=nj.OptId)
            ()

        | :? NjWork as work ->
            work.Calls  |> iter (fun z -> z.RawParent <- Some work)
            work.Calls  |> iter (onNsJsonDeserialized (Some work))
            work.Arrows |> iter (fun z -> z.RawParent <- Some work)
            work.Arrows
            |> iter (fun (a:NjArrow) ->
                let calls = work.Calls |-> (fun z -> z.DsObject :?> DsCall)
                let src = calls |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = calls |> find(fun w -> w.Guid = s2guid a.Target)
                a.DsObject <- ArrowBetweenCalls(a.Guid, src, tgt, a.DateTime, ?id=a.OptId)
                ()
                )

            (* DsWork 객체 생성은 flow guid 생성 시까지 지연 *)

            ()

        | :? NjCall as call ->
            call.ApiCalls |> iter (fun z -> z.RawParent <- Some call)
            call.ApiCalls |> iter (onNsJsonDeserialized (Some call))

            let callType = call.CallType |> Enum.TryParse<DbCallType> |> tryParseToOption |? DbCallType.Normal
            let apiCalls = [
                for ac in call.ApiCalls do
                    let dsac = DsApiCall(ac.Guid, ac.DateTime, ?id=ac.OptId)
                    ac.DsObject <- dsac
                    yield dsac ]
            call.DsObject <- DsCall(call.Name, call.Guid, callType, apiCalls, call.DateTime, ?id=call.OptId)
            ()

        | _ -> failwith "ERROR.  확장 필요?"



/// Ds Object 를 JSON 으로 변환하기 위한 모듈
[<AutoOpen>]
module Ds2JsonModule =
    type NjProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = EmJson.ToJson(x)
        member x.ToJson(jsonFilePath:string) =
            EmJson.ToJson(x)
            |> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): NjProject =
            (* Simple version *)
            //EmJson.FromJson<DsProject>(json)

            (* Withh context version *)
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            let ddic = DynamicDictionary() |> tee(fun dic -> ())
            settings.Context <- new StreamingContext(StreamingContextStates.All, ddic)

            EmJson.FromJson<NjProject>(json, settings)


    //type DsSystem with
    //    member x.ToJson():string = EmJson.ToJson(x)
    //    static member FromJson(json:string): DsSystem = EmJson.FromJson<DsSystem>(json)


    type DsProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = EmJson.ToJson(x)
        member x.ToJson(jsonFilePath:string) =
            NjProject.FromDs(x).ToJson(jsonFilePath)
            //EmJson.ToJson(x)
            //|> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): DsProject = json |> NjProject.FromJson |> _.DsObject :?> DsProject
