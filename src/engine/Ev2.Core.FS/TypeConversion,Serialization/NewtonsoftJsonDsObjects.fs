namespace Ev2.Core.FS

open System
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base
open System.IO
open System.Linq
open System.Collections.Generic
open System.Text.RegularExpressions

/// [N]ewtonsoft [J]son serialize 를 위한 DS 객체들.
[<AutoOpen>]
module NewtonsoftJsonModules =
    type INjObject  = inherit IDsObject
    type INjProject = inherit INjObject inherit IDsProject
    type INjSystem  = inherit INjObject inherit IDsSystem
    type INjFlow    = inherit INjObject inherit IDsFlow
    type INjWork    = inherit INjObject inherit IDsWork
    type INjCall    = inherit INjObject inherit IDsCall
    type INjApiCall = inherit INjObject inherit IDsApiCall
    type INjApiDef  = inherit INjObject inherit IDsApiDef
    type INjArrow   = inherit INjObject inherit IArrow

    [<AbstractClass>]
    type NjUnique() as this =
        //inherit Unique()
        interface IUnique


        /// JSON 파일에 대한 comment.  눈으로 debugging 용도.  code 에서 사용하지 말 것.
        [<JsonProperty(Order = -101)>] member val private RuntimeType = let name = this.GetType().Name in Regex.Replace(name, "^Nj", "")

        /// DB 저장시의 primary key id.  DB read/write 수행한 경우에만 Non-null
        [<JsonProperty(Order = -100)>] member val internal Id = nullableId with get, set
        ///// Database 의 primary id key.  Database 에 삽입시 생성
        [<JsonIgnore>] member x.OptId = x.Id |> Option.ofNullable

        [<JsonProperty(Order = -99)>] member val Name = nullString with get, set

        /// Guid: 메모리에 최초 객체 생성시 생성
        [<JsonProperty(Order = -98)>] member val Guid:Guid = emptyGuid with get, set

        /// DateTime: 메모리에 최초 객체 생성시 생성
        [<JsonProperty(Order = -97)>] member val DateTime = minDate with get, set

        /// 자신의 container 에 해당하는 parent DS 객체.  e.g call -> work -> system -> project, flow -> system
        [<JsonIgnore>] member val RawParent = Option<NjUnique>.None with get, set

        /// 내부 구현 전용.  serialize 대상에서 제외됨
        [<JsonIgnore>] member val internal DDic = DynamicDictionary()
        [<JsonIgnore>]
        member internal x.DsObject
            with get():Unique =
                match x.DDic.TryGet("RtObject") |-> box with
                | Some (:? Unique as rt) -> rt
                | _ -> failwith "RtObject not found in DynamicDictionary.  This is a bug."
            and set (v:Unique) = x.DDic.Set("RtObject", v)


    type Nj2RtBag() =
        member val RtDic = Dictionary<Guid, RtUnique>()
        member val NjDic = Dictionary<Guid, NjUnique>()
        member x.Add(u:RtUnique) = x.RtDic.TryAdd(u.Guid, u) |> ignore
        member x.Add(u:NjUnique) = x.NjDic.TryAdd(u.Guid, u) |> ignore
        member x.Add2 (ed:RtUnique) (nj:NjUnique) = x.Add ed; x.Add nj

    let mutable internal fwdOnNsJsonSerializing:  Nj2RtBag->INjObject option->INjObject->unit = let dummy (bag:Nj2RtBag) (parent:INjObject option) (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy
    let mutable internal fwdOnNsJsonDeserialized: Nj2RtBag->INjObject option->INjObject->unit = let dummy (bag:Nj2RtBag) (parent:INjObject option) (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy

/// Newtonsoft Json 호환 버젼
[<AutoOpen>]
module rec NewtonsoftJsonObjects =

    /// NjUnique 객체의 속성정보 (Id, Name, Guid, DateTime)를 Unique 객체에 저장
    let internal fromNjUniqINGD (src:#NjUnique) (dst:#Unique): #Unique =
        dst.Id <- n2o src.Id
        dst.Name <- src.Name
        dst.Guid <- src.Guid
        dst.DateTime <- src.DateTime
        dst

    /// Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 NjUnique 객체에 저장
    let internal toNjUniqINGD (src:#Unique) (dst:#NjUnique): #NjUnique =
        dst.Id <- o2n src.Id
        dst.Name <- src.Name
        dst.Guid <- src.Guid
        dst.DateTime <- src.DateTime
        match box src with
        | :? Unique as ds ->
            dst.DDic.Set("RtObject", ds)
        | _ ->
            ()
        dst


    type ReferenceInstance = {
        InstanceName: string
        PrototypeGuid: Guid
        InstanceGuid: Guid
    }
    /// project 를 Json serialize 시, system 저장 방식
    type NjSystemLoadType = // do not inherit NjUnique
        | LocalDefinition of NjSystem
        | Reference of ReferenceInstance


    type NjProject() =
        inherit NjUnique()
        interface INjProject

        member val LastConnectionString = null:string     with get, set
        member val Description          = null:string     with get, set
        member val Author               = null:string     with get, set
        member val Version              = Version()       with get, set

        [<JsonProperty(Order = 100)>] member val SystemPrototypes = [||]:NjSystem[] with get, set

        [<JsonProperty(Order = 101)>] member val ActiveSystems    = [||]:NjSystemLoadType[]     with get, set
        [<JsonProperty(Order = 102)>] member val PassiveSystems   = [||]:NjSystemLoadType[]     with get, set

        static member FromRuntime(rt:RtProject) =
            NjProject(LastConnectionString=rt.LastConnectionString
                , Author=rt.Author
                , Version=rt.Version
                , Description=rt.Description)
            |> toNjUniqINGD rt

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnNsJsonSerializing  (Nj2RtBag()) None x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnNsJsonDeserialized (Nj2RtBag()) None x


    type NjSystem() =
        inherit NjUnique()
        interface INjSystem


        [<JsonProperty(Order = 101)>] member val Flows    = [||]:NjFlow[]    with get, set
        [<JsonProperty(Order = 102)>] member val Works    = [||]:NjWork[]    with get, set
        [<JsonProperty(Order = 103)>] member val Arrows   = [||]:NjArrow[]   with get, set
        [<JsonProperty(Order = 104)>] member val ApiDefs  = [||]:NjApiDef[]  with get, set
        [<JsonProperty(Order = 104)>] member val ApiCalls = [||]:NjApiCall[] with get, set

        member val OriginGuid    = Nullable<Guid>() with get, set
        member val Prototype     = false      with get, set
        member val Author        = nullString with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set
        //[<JsonIgnore>] member val IsSaveAsReference = false with get, set

        member x.ShouldSerializeFlows   () = x.Flows   .NonNullAny()
        member x.ShouldSerializeWorks   () = x.Works   .NonNullAny()
        member x.ShouldSerializeArrows  () = x.Arrows  .NonNullAny()
        member x.ShouldSerializeApiDefs () = x.ApiDefs .NonNullAny()
        member x.ShouldSerializeApiCalls() = x.ApiCalls.NonNullAny()


        [<OnSerializing>]
        member x.OnSerializingMethod (ctx: StreamingContext) =
            let bag = ctx.Context |> cast<Nj2RtBag>
            fwdOnNsJsonSerializing bag (x.RawParent >>= tryCast<INjObject>) x
        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            let bag = ctx.Context |> cast<Nj2RtBag>
            fwdOnNsJsonDeserialized bag (x.RawParent >>= tryCast<INjObject>) x

        static member FromRuntime(rt:RtSystem) =
            let originGuid = rt.OriginGuid |> Option.toNullable

            NjSystem(OriginGuid=originGuid, Author=rt.Author,
                LangVersion=rt.LangVersion, EngineVersion=rt.EngineVersion, Description=rt.Description)
            |> toNjUniqINGD rt
            |> tee (fun z ->
                z.Flows    <- rt.Flows    |-> NjFlow.FromRuntime    |> toArray
                z.Arrows   <- rt.Arrows   |-> NjArrow.FromRuntime   |> toArray
                z.Works    <- rt.Works    |-> NjWork.FromRuntime    |> toArray
                z.ApiDefs  <- rt.ApiDefs  |-> NjApiDef.FromRuntime  |> toArray
                z.ApiCalls <- rt.ApiCalls |-> NjApiCall.FromRuntime |> toArray
            )

    type NjFlow () =
        inherit NjUnique()
        interface INjFlow

        static member FromRuntime(rt:RtFlow) =
            NjFlow()
            |> toNjUniqINGD rt

    type NjWork () =
        inherit NjUnique()
        interface INjWork
        member val FlowGuid = null:string with get, set
        member val Calls: NjCall[] = [||] with get, set
        member val Arrows:NjArrow[] = [||] with get, set

        member x.ShouldSerializeCalls() = x.Calls.NonNullAny()
        member x.ShouldSerializeArrows() = x.Arrows.NonNullAny()

        static member FromRuntime(rt:RtWork) =
            NjWork()
            |> toNjUniqINGD rt
            |> tee (fun z ->
                z.Calls    <- rt.Calls   |-> NjCall.FromRuntime  |> toArray
                z.Arrows   <- rt.Arrows  |-> NjArrow.FromRuntime |> toArray
                z.FlowGuid <- rt.Flow |-> (fun flow -> guid2str flow.Guid) |? null
            )

    type NjArrow() =
        inherit NjUnique()

        interface INjArrow
        member val Source = null:string with get, set
        member val Target = null:string with get, set
        member val Type = DbArrowType.None.ToString() with get, set

        static member FromRuntime(rt:IArrow) =
            assert(isItNotNull rt)
            NjArrow()
            |> toNjUniqINGD (rt :?> Unique)
            |> tee (fun z ->
                z.Source <- guid2str (rt.GetSource().Guid)
                z.Target <- guid2str (rt.GetTarget().Guid)
                z.Type <- rt.GetArrowType().ToString()
            )

    type NjCall() =
        inherit NjUnique()

        interface INjCall
        member val CallType = DbCallType.Normal.ToString() with get, set
        /// Json serialize 용 API call 에 대한 Guid
        member val ApiCalls   = [||]:Guid[]     with get, set
        member val AutoPre    = nullString      with get, set
        member val Safety     = nullString      with get, set
        member val IsDisabled = false           with get, set
        member val Timeout    = Nullable<int>() with get, set

        (* 특별한 조건일 때에만 json 표출 *)
        member x.ShouldSerializeApiCalls()   = x.ApiCalls.NonNullAny()
        member x.ShouldSerializeIsDisabled() = x.IsDisabled
        member x.ShouldSerializeCallType()   = x.CallType <> DbCallType.Normal.ToString()

        static member FromRuntime(rt:RtCall) =
            NjCall(CallType = rt.CallType.ToString(), AutoPre=rt.AutoPre, Safety=rt.Safety, Timeout=o2n rt.Timeout)
            |> toNjUniqINGD rt
            |> tee (fun z ->
                z.ApiCalls <- rt.ApiCalls |-> _.Guid |> toArray
            )


    type NjApiCall() =
        inherit NjUnique()

        interface INjApiCall
        member val ApiDef     = emptyGuid  with get, set
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val Value      = nullString with get, set
        member val ValueType  = DbDataType.None.ToString() with get, set

        static member FromRuntime(rt:RtApiCall) =
            NjApiCall(ApiDef=rt.ApiDefGuid, InAddress=rt.InAddress, OutAddress=rt.OutAddress,
                InSymbol=rt.InSymbol, OutSymbol=rt.OutSymbol,
                Value=rt.Value, ValueType=rt.ValueType.ToString() )
            |> toNjUniqINGD rt

    type NjApiDef() =
        inherit NjUnique()
        interface INjApiDef

        member val IsPush = false with get, set

        static member FromRuntime(rt:RtApiDef) =
            assert(isItNotNull rt)
            NjApiDef(IsPush=rt.IsPush)
            |> toNjUniqINGD rt














    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec internal onNsJsonSerializing (bag:Nj2RtBag) (njParent:INjObject option) (njObj:INjObject) =
        match njObj with
        | :? NjUnique as uniq ->
            bag.Add uniq
            uniq |> toNjUniqINGD uniq.DsObject |> ignore
        | _ ->
            ()

        match njObj with
        | :? NjProject as njp ->
            let rtp = njp.DsObject :?> RtProject

            njp.SystemPrototypes <-
                rtp.PrototypeSystems
                |> distinct
                |-> NjSystem.FromRuntime
                |> toArray

            let rtToSystemLoadType (rt:RtSystem) =
                match rt.PrototypeSystemGuid with
                | Some guid ->
                    NjSystemLoadType.Reference { InstanceName=rt.Name; PrototypeGuid=guid; InstanceGuid=rt.Guid }
                | None ->
                    let nj = rt |> NjSystem.FromRuntime
                    NjSystemLoadType.LocalDefinition nj

            njp.ActiveSystems  <- rtp.ActiveSystems  |-> rtToSystemLoadType |> toArray
            njp.PassiveSystems <- rtp.PassiveSystems |-> rtToSystemLoadType |> toArray

            njp.LastConnectionString <- rtp.LastConnectionString
            njp.SystemPrototypes |> iter (onNsJsonSerializing bag (Some njp))

        | :? NjSystem as njs ->
            njs.Arrows   |> iter (onNsJsonSerializing bag (Some njs))
            njs.Flows    |> iter (onNsJsonSerializing bag (Some njs))
            njs.Works    |> iter (onNsJsonSerializing bag (Some njs))
            njs.ApiDefs  |> iter (onNsJsonSerializing bag (Some njs))
            njs.ApiCalls |> iter (onNsJsonSerializing bag (Some njs))

        | :? NjWork as njw ->
            njw.Arrows |> iter (onNsJsonSerializing bag (Some njw))
            njw.Calls  |> iter (onNsJsonSerializing bag (Some njw))


        | (:? NjFlow) | (:? NjCall) | (:? NjArrow) | (:? NjApiDef) | (:? NjApiCall) ->
            (* NjXXX.FromDS 에서 이미 다 채운 상태임.. *)
            ()

        | _ ->
            failwith "ERROR.  확장 필요?"




    /// JSON 읽고 나서 메모리 구조에 후처리 작업
    let rec internal onNsJsonDeserialized (bag:Nj2RtBag) (njParent:INjObject option) (njObj:INjObject) =
        // 공통 처리
        match njObj with
        | :? NjUnique as uniq ->
            bag.Add uniq
        | _ ->
            failwith "ERROR"

        // 개별 처리
        match njObj with
        | :? NjProject as njp ->
            let protos = njp.SystemPrototypes |-> (fun z -> z.DsObject :?> RtSystem)
            let load (loadType:NjSystemLoadType):RtSystem =
                match loadType with
                | NjSystemLoadType.LocalDefinition sys ->
                    sys.DsObject :?> RtSystem
                | NjSystemLoadType.Reference { InstanceName=name; PrototypeGuid=protoGuid; InstanceGuid=instanceGuid } ->
                    protos
                    |> find (fun p -> p.Guid = protoGuid)
                    |> (fun z -> fwdDuplicate z :?> RtSystem)
                    |> tee(fun s ->
                        s.Name <- name
                        s.Guid <- instanceGuid
                        s.PrototypeSystemGuid <- Some protoGuid
                        )

            njp.SystemPrototypes |> iter (onNsJsonDeserialized bag (Some njp))
            njp.DsObject <-
                noop()
                let actives  = njp.ActiveSystems  |-> load
                let passives = njp.PassiveSystems |-> load
                let prototypeSystems = njp.SystemPrototypes |-> (fun s -> s.DsObject :?> RtSystem )

                RtProject(prototypeSystems, actives, passives
                    , Author=njp.Author
                    , Version=njp.Version
                    , Description=njp.Description
                    , LastConnectionString=njp.LastConnectionString )
                |> fromNjUniqINGD njp
                |> tee (fun z ->
                    actives @ passives
                    |> iter (fun s -> s.RawParent <- Some z)

                    bag.Add2 z njp)

        | :? NjSystem as njs ->
            // flows, works, arrows 의 Parent 를 this(system) 으로 설정
            njs.Arrows   |> iter (fun z -> z.RawParent <- Some njs; bag.Add z)
            njs.Flows    |> iter (fun z -> z.RawParent <- Some njs; bag.Add z)
            njs.Works    |> iter (fun z -> z.RawParent <- Some njs; bag.Add z)
            njs.ApiDefs  |> iter (fun z -> z.RawParent <- Some njs; bag.Add z)
            njs.ApiCalls |> iter (fun z -> z.RawParent <- Some njs; bag.Add z)

            // 하부 구조에 대해서 재귀적으로 호출 : dependancy 가 적은 것부터 먼저 생성할 것.
            njs.ApiDefs  |> iter (fun z -> onNsJsonDeserialized bag (Some njs) z)
            njs.ApiCalls |> iter (fun z -> onNsJsonDeserialized bag (Some njs) z)
            njs.Flows    |> iter (fun z -> onNsJsonDeserialized bag (Some njs) z)
            njs.Works    |> iter (fun z -> onNsJsonDeserialized bag (Some njs) z)

            let flows = njs.Flows |-> (fun z -> z.DsObject :?> RtFlow)

            let works = [|
                for w in njs.Works do
                    let optFlow =
                        if w.FlowGuid.NonNullAny() then
                            flows |> tryFind (fun f -> f.Guid = s2guid w.FlowGuid)
                        else
                            None
                    let calls  = w.Calls  |-> (fun z -> z.DsObject :?> RtCall)
                    let arrows = w.Arrows |-> (fun z -> z.DsObject :?> RtArrowBetweenCalls)

                    let dsWork =
                        RtWork.Create(calls, arrows, optFlow)
                        |> fromNjUniqINGD w |> tee (fun z -> bag.Add2 z w)

                    yield dsWork
                    w.DsObject <- dsWork
            |]

            njs.Arrows
            |> iter (fun (a:NjArrow) ->
                let works = njs.Works |-> (fun z -> z.DsObject :?> RtWork)
                let src = works |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = works |> find(fun w -> w.Guid = s2guid a.Target)

                let arrowType =
                    a.Type
                    |> Enum.TryParse<DbArrowType>
                    |> tryParseToOption
                    |? DbArrowType.None

                a.DsObject <-
                    RtArrowBetweenWorks(src, tgt, arrowType)
                    |> fromNjUniqINGD a |> tee (fun z -> bag.Add2 z a) )

            let arrows   = njs.Arrows   |-> (fun z -> z.DsObject :?> RtArrowBetweenWorks)
            let apiDefs  = njs.ApiDefs  |-> (fun z -> z.DsObject :?> RtApiDef)
            let apiCalls = njs.ApiCalls |-> (fun z -> z.DsObject :?> RtApiCall)

            njs.DsObject <-
                noop()
                let protoGuid:Guid option =
                    match njs.RawParent >>= tryCast<NjProject> with
                    | Some njp -> [
                        let loadTypes = njp.ActiveSystems @ njp.PassiveSystems
                        for loadType in loadTypes do
                            match loadType with
                            | NjSystemLoadType.Reference r -> Some r.PrototypeGuid
                            | _ -> None ] |> choose id |> tryHead
                    | None -> None

                RtSystem.Create(protoGuid, flows, works, arrows, apiDefs, apiCalls
                                , Author=njs.Author
                                , LangVersion=njs.LangVersion
                                , EngineVersion=njs.EngineVersion
                                , Description=njs.Description
                                , OriginGuid=n2o njs.OriginGuid)
                |> fromNjUniqINGD njs
                |> tee (fun z -> bag.Add2 z njs)

        | :? NjFlow as njf ->
            njf.DsObject <-
                RtFlow()
                |> fromNjUniqINGD njf |> tee (fun z -> bag.Add2 z njf)
            ()

        | :? NjWork as njw ->
            njw.Calls  |> iter (fun z -> z.RawParent <- Some njw)
            njw.Calls  |> iter (onNsJsonDeserialized bag (Some njw))
            njw.Arrows |> iter (fun z -> z.RawParent <- Some njw)

            njw.Arrows
            |> iter (fun (a:NjArrow) ->
                let calls = njw.Calls |-> (fun z -> z.DsObject :?> RtCall)
                let src = calls |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = calls |> find(fun w -> w.Guid = s2guid a.Target)
                let arrowType =
                    a.Type
                    |> Enum.TryParse<DbArrowType>
                    |> tryParseToOption
                    |? DbArrowType.None

                a.DsObject <-
                    RtArrowBetweenCalls(src, tgt, arrowType)
                    |> fromNjUniqINGD a |> tee (fun z -> bag.Add2 z a))

            (* DsWork 객체 생성은 flow guid 생성 시까지 지연 *)

            ()

        | :? NjCall as njc ->
            let callType =
                njc.CallType
                |> Enum.TryParse<DbCallType>
                |> tryParseToOption
                |? DbCallType.Normal

            njc.DsObject <-
                RtCall(callType, njc.ApiCalls, njc.AutoPre, njc.Safety, njc.IsDisabled, n2o njc.Timeout)
                |> fromNjUniqINGD njc |> tee (fun z -> bag.Add2 z njc)
            ()

        | :? NjApiCall as njac ->
            let valueType =
                njac.ValueType
                |> Enum.TryParse<DbDataType>
                |> tryParseToOption
                |? DbDataType.None

            njac.DsObject <-
                RtApiCall(njac.ApiDef, njac.InAddress, njac.OutAddress, njac.InSymbol, njac.OutSymbol, valueType, njac.Value)
                |> fromNjUniqINGD njac |> tee (fun z -> bag.Add2 z njac)

        | :? NjApiDef as njad ->
            njad.DsObject <-
                RtApiDef(njad.IsPush)
                |> fromNjUniqINGD njad |> tee (fun z -> bag.Add2 z njad)
            ()

        | _ -> failwith "ERROR.  확장 필요?"



/// Ds Object 를 JSON 으로 변환하기 위한 모듈
[<AutoOpen>]
module Ds2JsonModule =

    type NjProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string =
            (* Withh context version *)
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            settings.Context <- new StreamingContext(StreamingContextStates.All, Nj2RtBag())

            EmJson.ToJson(x, settings)

        member x.ToJsonFile(jsonFilePath:string) =
            x.ToJson()
            |> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): NjProject =
            (* Simple version *)
            //EmJson.FromJson<DsProject>(json)

            (* Withh context version *)
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            settings.Context <- new StreamingContext(StreamingContextStates.All, Nj2RtBag())

            EmJson.FromJson<NjProject>(json, settings)


    /// Runtime 객체의 validation
    let internal validateRuntime (rtObj:#RtUnique): #RtUnique =
        let xxx = rtObj.EnumerateRtObjects() |> toArray
        let guidDic = rtObj.EnumerateRtObjects().ToDictionary(_.Guid, id)
        rtObj.Validate(guidDic)
        rtObj



    type RtProject with // // ToJson, FromJson
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = NjProject.FromRuntime(x).ToJson()
        member x.ToJson(jsonFilePath:string) = NjProject.FromRuntime(x).ToJsonFile(jsonFilePath)

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): RtProject =
            json |> NjProject.FromJson |> _.DsObject :?> RtProject |> validateRuntime
