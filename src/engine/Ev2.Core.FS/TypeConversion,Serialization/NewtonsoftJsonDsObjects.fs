namespace Ev2.Core.FS

open System
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base
open System.IO
open System.Linq
open System.Collections.Generic

[<AutoOpen>]
module NewtonsoftJsonForwardDecls =
    type INjObject  = interface end
    type INjProject = inherit INjObject inherit IDsProject
    type INjSystem  = inherit INjObject inherit IDsSystem
    type INjFlow    = inherit INjObject inherit IDsFlow
    type INjWork    = inherit INjObject inherit IDsWork
    type INjCall    = inherit INjObject inherit IDsCall
    type INjApiCall = inherit INjObject inherit IDsApiCall
    type INjApiDef  = inherit INjObject inherit IDsApiDef
    type INjArrow   = inherit INjObject inherit IArrow


    let mutable fwdOnNsJsonSerializing:  DynamicDictionary->INjObject option->INjObject->unit = let dummy (ddic:DynamicDictionary) (parent:INjObject option) (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy
    let mutable fwdOnNsJsonDeserialized: DynamicDictionary->INjObject option->INjObject->unit = let dummy (ddic:DynamicDictionary) (parent:INjObject option) (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy

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


        /// 자신과 관련된 Runtime Object
        [<JsonIgnore>] member val DsObject:Unique = getNull<Unique>() with get, set
        [<JsonIgnore>] member x.DsRawParent:Unique option = x.DsObject.RawParent
        [<JsonIgnore>] member x.NjRawParent:NjUnique option = x.RawParent

    /// NjUnique 객체의 속성정보 (Id, Name, Guid, DateTime)를 Unique 객체에 저장
    let fromNjUniqINGD (src:#NjUnique) (dst:#Unique): #Unique =
        dst.Id <- n2o src.Id
        dst.Name <- src.Name
        dst.Guid <- src.Guid
        dst.DateTime <- src.DateTime
        dst

    /// Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 NjUnique 객체에 저장
    let toNjUniqINGD (src:#Unique) (dst:#NjUnique): #NjUnique =
        dst.Id <- o2n src.Id
        dst.Name <- src.Name
        dst.Guid <- src.Guid
        dst.DateTime <- src.DateTime
        match box src with
        | :? Unique as ds ->
            dst.DsObject <- ds
        | _ ->
            ()
        dst


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

        static member FromDs(ds:RtProject) =
            NjProject(LastConnectionString=ds.LastConnectionString
                , Author=ds.Author
                , Version=ds.Version
                , Description=ds.Description)
            |> toNjUniqINGD ds

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnNsJsonSerializing  ctx.DDic None x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnNsJsonDeserialized ctx.DDic None x


    type NjSystem() =
        inherit NjUnique()
        interface INjSystem


        [<JsonProperty(Order = 101)>] member val Flows    = [||]:NjFlow[]    with get, set
        [<JsonProperty(Order = 102)>] member val Works    = [||]:NjWork[]    with get, set
        [<JsonProperty(Order = 103)>] member val Arrows   = [||]:NjArrow[]   with get, set
        [<JsonProperty(Order = 104)>] member val ApiDefs  = [||]:NjApiDef[]  with get, set
        [<JsonProperty(Order = 104)>] member val ApiCalls = [||]:NjApiCall[] with get, set
        member val OriginGuid    = Nullable<Guid>() with get, set
        member x.ShouldSerializeFlows   () = x.Flows   .NonNullAny()
        member x.ShouldSerializeWorks   () = x.Works   .NonNullAny()
        member x.ShouldSerializeArrows  () = x.Arrows  .NonNullAny()
        member x.ShouldSerializeApiDefs () = x.ApiDefs .NonNullAny()
        member x.ShouldSerializeApiCalls() = x.ApiCalls.NonNullAny()

        member val Prototype     = false      with get, set
        member val Author        = nullString with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set

        [<OnSerializing>]
        member x.OnSerializingMethod (ctx: StreamingContext) =
            fwdOnNsJsonSerializing ctx.DDic (x.RawParent >>= tryCast<INjObject>) x
        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            fwdOnNsJsonDeserialized ctx.DDic (x.RawParent >>= tryCast<INjObject>) x

        static member FromDs(ds:RtSystem) =
            let originGuid = ds.OriginGuid |> Option.toNullable

            NjSystem(Prototype=ds.IsPrototype, OriginGuid=originGuid, Author=ds.Author,
                LangVersion=ds.LangVersion, EngineVersion=ds.EngineVersion, Description=ds.Description)
            |> toNjUniqINGD ds
            |> tee (fun z ->
                z.Flows   <- ds.Flows   |-> NjFlow.FromDs   |> toArray
                z.Arrows  <- ds.Arrows  |-> NjArrow.FromDs  |> toArray
                z.Works   <- ds.Works   |-> NjWork.FromDs   |> toArray
                z.ApiDefs <- ds.ApiDefs |-> NjApiDef.FromDs |> toArray
            )

    type NjFlow () =
        inherit NjUnique()
        interface INjFlow

        static member FromDs(ds:RtFlow) =
            NjFlow() |> toNjUniqINGD ds

    type NjWork () =
        inherit NjUnique()
        interface INjWork
        member val FlowGuid = null:string with get, set
        member val Calls: NjCall[] = [||] with get, set
        member val Arrows:NjArrow[] = [||] with get, set

        member x.ShouldSerializeCalls() = x.Calls.NonNullAny()
        member x.ShouldSerializeArrows() = x.Arrows.NonNullAny()

        static member FromDs(ds:RtWork) =
            NjWork() |> toNjUniqINGD ds
            |> tee (fun z ->
                z.Calls    <- ds.Calls   |-> NjCall.FromDs  |> toArray
                z.Arrows   <- ds.Arrows  |-> NjArrow.FromDs |> toArray
                z.FlowGuid <- ds.OptFlow |-> (fun flow -> guid2str flow.Guid) |? null
            )

    type NjArrow() =
        inherit NjUnique()
        interface INjArrow
        member val Source = null:string with get, set
        member val Target = null:string with get, set
        member val Type = DbArrowType.None.ToString() with get, set
        static member FromDs(ds:IArrow) =
            assert(isItNotNull ds)
            NjArrow() |> toNjUniqINGD (ds :?> Unique)
            |> tee (fun z ->
                //z.Import (ds :?> Unique)
                z.Source <- guid2str (ds.GetSource().Guid)
                z.Target <- guid2str (ds.GetTarget().Guid)
                z.Type <- ds.GetArrowType().ToString()
            )

    type NjCall() =
        inherit NjUnique()
        interface INjCall
        member val CallType = DbCallType.Normal.ToString() with get, set
        /// Json serialize 용 API call 에 대한 Guid
        member val ApiCalls = [||]:Guid[] with get, set
        member val AutoPre  = nullString with get, set
        member val Safety   = nullString with get, set
        member val Timeout  = Nullable<int>() with get, set
        member x.ShouldSerializeApiCalls() = x.ApiCalls.NonNullAny()

        static member FromDs(ds:RtCall) =
            NjCall(CallType = ds.CallType.ToString(), AutoPre=ds.AutoPre, Safety=ds.Safety, Timeout=o2n ds.Timeout)
            |> toNjUniqINGD ds
            |> tee (fun z ->
                z.ApiCalls <- ds.ApiCalls |-> _.Guid |> toArray
            )


    type NjApiCall() =
        inherit NjUnique()
        interface INjApiCall
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val Value      = nullString with get, set
        member val ValueType  = DbDataType.None.ToString() with get, set
        static member FromDs(ds:RtApiCall) =
            NjApiCall(InAddress=ds.InAddress, OutAddress=ds.OutAddress,
                InSymbol=ds.InSymbol, OutSymbol=ds.OutSymbol,
                Value=ds.Value, ValueType=ds.ValueType.ToString() )
            |> toNjUniqINGD ds

    type NjApiDef() =
        inherit NjUnique()
        interface INjApiDef
        member val IsPush = false with get, set
        static member FromDs(ds:RtApiDef) =
            assert(isItNotNull ds)
            NjApiDef(IsPush=ds.IsPush) |> toNjUniqINGD ds


    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec internal onNsJsonSerializing (ddic:DynamicDictionary) (njParent:INjObject option) (njObj:INjObject) =
        match njObj with
        | :? NjUnique as uniq ->
            uniq |> toNjUniqINGD uniq.DsObject |> ignore
            if not <| ddic.Get<Dictionary<Guid, NjUnique>>("njGuids").TryAdd(uniq.Guid, uniq) then
                logWarn $"WARN: NjUnique Guid {uniq.Guid} already exists in the dictionary."
        | _ ->
            ()

        match njObj with
        | :? NjProject as nj ->
            let ds = nj.DsObject :?> RtProject
            nj.SystemPrototypes <-
                let originals, copies = ds.ActiveSystems |> partition (fun s -> s.OriginGuid.IsNone)
                let distinctCopies = copies |> distinctBy _.Guid
                originals @ distinctCopies |-> NjSystem.FromDs |> toArray
            nj.ActiveSystemGuids    <- ds.ActiveSystems  |-> _.Guid |> toArray
            nj.PassiveSystemGuids   <- ds.PassiveSystems |-> _.Guid |> toArray
            nj.LastConnectionString <- ds.LastConnectionString

            nj.SystemPrototypes |> iter (onNsJsonSerializing ddic (Some nj))

        | :? NjSystem as sys ->
            sys.Arrows  |> iter (onNsJsonSerializing ddic (Some sys))
            sys.Flows   |> iter (onNsJsonSerializing ddic (Some sys))
            sys.Works   |> iter (onNsJsonSerializing ddic (Some sys))
            sys.ApiDefs |> iter (onNsJsonSerializing ddic (Some sys))
            ()

        | :? NjFlow as flow ->
            ()

        | :? NjWork as work ->
            work.Arrows |> iter (onNsJsonSerializing ddic (Some work))
            ()
            //work.Calls |> iter onSerializing
            //work.TryGetFlow() |> iter (fun f -> work.FlowGuid <- guid2str f.Guid)

        | (:? NjCall) | (:? NjArrow) ->
            ()

        | :? NjApiDef as ad ->
            ()
        | _ -> failwith "ERROR.  확장 필요?"




    /// JSON 읽고 나서 메모리 구조에 후처리 작업
    let rec internal onNsJsonDeserialized (ddic:DynamicDictionary) (njParent:INjObject option) (njObj:INjObject) =
        let njGuidDic = ddic.Get<Dictionary<Guid, NjUnique>>("njGuids")
        let rtGuidDic = ddic.Get<Dictionary<Guid, Unique>>("rtGuids")
        match njObj with
        | :? NjUnique as uniq ->
            if not <| njGuidDic.TryAdd(uniq.Guid, uniq) then
                logWarn $"WARN: NjUnique Guid {uniq.Guid} already exists in the dictionary."
            ()
        | _ ->
            ()

        match njObj with
        | :? NjProject as proj ->
            proj.SystemPrototypes |> iter (onNsJsonDeserialized ddic (Some proj))
            proj.DsObject <-
                let systems = proj.SystemPrototypes |-> (fun z -> z.DsObject :?> RtSystem)
                let actives = systems |> filter (fun s -> proj.ActiveSystemGuids |> contains (s.Guid))
                let passives = systems |> filter (fun s -> proj.PassiveSystemGuids |> contains (s.Guid))
                noop()
                let id = n2o proj.Id
                RtProject(actives, passives
                    , Author=proj.Author
                    , Version=proj.Version
                    , Description=proj.Description
                    , LastConnectionString=proj.LastConnectionString )
                |> fromNjUniqINGD proj
                |> tee (fun z -> rtGuidDic.TryAdd(z.Guid, z))
                |> tee(fun z -> systems |> iter (fun s -> s.RawParent <- Some z))

        | :? NjSystem as nj ->
            // flows, works, arrows 의 Parent 를 this(system) 으로 설정
            nj.Arrows   |> iter (fun z -> z.RawParent <- Some nj)
            nj.Flows    |> iter (fun z -> z.RawParent <- Some nj)
            nj.Works    |> iter (fun z -> z.RawParent <- Some nj)
            nj.ApiDefs  |> iter (fun z -> z.RawParent <- Some nj)
            nj.ApiCalls |> iter (fun z -> z.RawParent <- Some nj)

            // 하부 구조에 대해서 재귀적으로 호출 : dependancy 가 적은 것부터 먼저 생성할 것.
            nj.ApiDefs  |> iter (onNsJsonDeserialized ddic (Some nj))
            nj.ApiCalls |> iter (onNsJsonDeserialized ddic (Some nj))
            nj.Flows    |> iter (onNsJsonDeserialized ddic (Some nj))
            nj.Works    |> iter (onNsJsonDeserialized ddic (Some nj))

            let flows = nj.Flows |-> (fun z -> z.DsObject :?> RtFlow)

            let works = [|
                for w in nj.Works do
                    let optFlow =
                        if w.FlowGuid.NonNullAny() then
                            flows |> tryFind (fun f -> f.Guid = s2guid w.FlowGuid)
                        else
                            None
                    let calls = w.Calls |-> (fun z -> z.DsObject :?> RtCall)
                    let arrows = w.Arrows |-> (fun z -> z.DsObject :?> RtArrowBetweenCalls)
                    let dsWork = RtWork.Create(calls, arrows, optFlow) |> fromNjUniqINGD w |> tee (fun z -> rtGuidDic.TryAdd(z.Guid, z))

                    yield dsWork
                    w.DsObject <- dsWork
            |]

            nj.Arrows
            |> iter (fun (a:NjArrow) ->
                let works = nj.Works |-> (fun z -> z.DsObject :?> RtWork)
                let src = works |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = works |> find(fun w -> w.Guid = s2guid a.Target)
                let arrowType = a.Type |> Enum.TryParse<DbArrowType> |> tryParseToOption |? DbArrowType.None
                a.DsObject <- RtArrowBetweenWorks(src, tgt, arrowType) |> fromNjUniqINGD a |> tee (fun z -> rtGuidDic.TryAdd(z.Guid, z))
                ()
                )
            let arrows   = nj.Arrows   |-> (fun z -> z.DsObject :?> RtArrowBetweenWorks)
            let apiDefs  = nj.ApiDefs  |-> (fun z -> z.DsObject :?> RtApiDef)
            let apiCalls = nj.ApiCalls |-> (fun z -> z.DsObject :?> RtApiCall)

            nj.DsObject <-
                noop()
                RtSystem.Create(nj.Prototype, flows, works, arrows, apiDefs, apiCalls
                                , Author=nj.Author
                                , LangVersion=nj.LangVersion
                                , EngineVersion=nj.EngineVersion
                                , Description=nj.Description)
                |> fromNjUniqINGD nj
                |> tee (fun z -> rtGuidDic.TryAdd(z.Guid, z))

        | :? NjFlow as nj ->
            nj.DsObject <- RtFlow() |> fromNjUniqINGD nj |> tee (fun z -> rtGuidDic.TryAdd(z.Guid, z))
            ()

        | :? NjWork as work ->
            work.Calls  |> iter (fun z -> z.RawParent <- Some work)
            work.Calls  |> iter (onNsJsonDeserialized ddic (Some work))
            work.Arrows |> iter (fun z -> z.RawParent <- Some work)
            work.Arrows
            |> iter (fun (a:NjArrow) ->
                let calls = work.Calls |-> (fun z -> z.DsObject :?> RtCall)
                let src = calls |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = calls |> find(fun w -> w.Guid = s2guid a.Target)
                let arrowType = a.Type |> Enum.TryParse<DbArrowType> |> tryParseToOption |? DbArrowType.None
                a.DsObject <- RtArrowBetweenCalls(src, tgt, arrowType) |> fromNjUniqINGD a |> tee (fun z -> rtGuidDic.TryAdd(z.Guid, z))
                ()
                )

            (* DsWork 객체 생성은 flow guid 생성 시까지 지연 *)

            ()

        | :? NjCall as call ->
            //let njSys = call.EnumerateAncestors().OfType<NjSystem>().First()
            //let dsSys = njSys.DsObject :?> RtSystem
            //let guidDic = dsSys.ApiCalls.ToDictionary(_.Guid, id)
            //let apiCalls = call.ApiCalls |-> (fun guid -> guidDic.[guid])

            let callType = call.CallType |> Enum.TryParse<DbCallType> |> tryParseToOption |? DbCallType.Normal

            let apiCalls = call.ApiCalls |-> (fun z -> rtGuidDic[z] :?> RtApiCall)


            //let apiCalls = [
            //    for ac in call.ApiCalls do
            //        let valueType = ac.ValueType |> Enum.TryParse<DbDataType> |> tryParseToOption |? DbDataType.None
            //        let dsac = RtApiCall(ac.InAddress, ac.OutAddress, ac.InSymbol, ac.OutSymbol, valueType, ac.Value) |> fromNjUniqINGD ac
            //        ac.DsObject <- dsac
            //        yield dsac ]

            call.DsObject <- RtCall(callType, apiCalls, call.AutoPre, call.Safety, n2o call.Timeout) |> fromNjUniqINGD call |> tee (fun z -> rtGuidDic.TryAdd(z.Guid, z))
            ()

        | :? NjApiCall as ac ->
            failwith "ERROR.  확장 필요?"

        | :? NjApiDef as ad ->
            ad.DsObject <- RtApiDef(ad.IsPush) |> fromNjUniqINGD ad |> tee (fun z -> rtGuidDic.TryAdd(z.Guid, z))
            ()

        | _ -> failwith "ERROR.  확장 필요?"

    type NjUnique with
        member x.EnumerateAncestors(?includeMe): NjUnique list = [
            let includeMe = includeMe |? true
            if includeMe then
                yield x
            match x.RawParent with
            | Some parent ->
                yield! parent.EnumerateAncestors()
            | None -> ()
        ]

/// Ds Object 를 JSON 으로 변환하기 위한 모듈
[<AutoOpen>]
module Ds2JsonModule =
    let private createDynamicDictionary() =
        DynamicDictionary()
        |> tee(fun dic ->
            dic.Set("njGuids", Dictionary<Guid, NjUnique>())
            dic.Set("rtGuids", Dictionary<Guid, Unique>()) )


    type NjProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = EmJson.ToJson(x)
        member x.ToJson(jsonFilePath:string) =
            (* Withh context version *)
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            settings.Context <- new StreamingContext(StreamingContextStates.All, createDynamicDictionary())

            EmJson.ToJson(x, settings)
            |> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): NjProject =
            (* Simple version *)
            //EmJson.FromJson<DsProject>(json)

            (* Withh context version *)
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            settings.Context <- new StreamingContext(StreamingContextStates.All, createDynamicDictionary())

            EmJson.FromJson<NjProject>(json, settings)


    //type DsSystem with
    //    member x.ToJson():string = EmJson.ToJson(x)
    //    static member FromJson(json:string): DsSystem = EmJson.FromJson<DsSystem>(json)


    type RtProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = EmJson.ToJson(x)
        member x.ToJson(jsonFilePath:string) =
            NjProject.FromDs(x).ToJson(jsonFilePath)
            //EmJson.ToJson(x)
            //|> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): RtProject = json |> NjProject.FromJson |> _.DsObject :?> RtProject
