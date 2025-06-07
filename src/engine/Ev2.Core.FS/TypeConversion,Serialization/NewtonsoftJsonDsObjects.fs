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
open Newtonsoft.Json.Linq
open Dual.Common.Db.FS

/// [N]ewtonsoft [J]son serialize 를 위한 DS 객체들.
[<AutoOpen>]
module NewtonsoftJsonModules =
    type INjProject = inherit INjUnique inherit IDsProject
    type INjSystem  = inherit INjUnique inherit IDsSystem
    type INjFlow    = inherit INjUnique inherit IDsFlow
    type INjWork    = inherit INjUnique inherit IDsWork
    type INjCall    = inherit INjUnique inherit IDsCall
    type INjApiCall = inherit INjUnique inherit IDsApiCall
    type INjApiDef  = inherit INjUnique inherit IDsApiDef
    type INjArrow   = inherit INjUnique inherit IArrow

    [<AbstractClass>]
    type NjUnique() as this =
        inherit Unique()
        interface IUnique

        /// JSON 파일에 대한 comment.  눈으로 debugging 용도.  code 에서 사용하지 말 것.
        [<JsonProperty(Order = -101)>] member val private RuntimeType = let name = this.GetType().Name in Regex.Replace(name, "^Nj", "")

        [<JsonIgnore>]
        member internal x.RuntimeObject
            with get():Unique = x.RtObject >>= tryCast<Unique> |?? (fun () -> failwithlog "RtObject not found in DynamicDictionary.  This is a bug." )
            and set (v:Unique) = x.RtObject <- Some (box v :?> IRtUnique)


    let mutable internal fwdOnNsJsonSerializing:  INjObject->unit = let dummy (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy
    let mutable internal fwdOnNsJsonDeserialized: INjObject->unit = let dummy (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy

/// Newtonsoft Json 호환 버젼
[<AutoOpen>]
module rec NewtonsoftJsonObjects =
    //let njSetParentI (parent:NjUnique) (x:#NjUnique): unit = x.RawParent <- Some parent


    /// Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 NjUnique 객체에 저장
    let internal fromNjUniqINGD (src:#Unique) (dst:#NjUnique): #NjUnique =
        fromUniqINGD src dst |> ignore

        match box src with
        | :? Unique as ds ->
            dst.RtObject <- ds |> tryCast<IRtUnique>
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

        member val Database    = getNull<DbProvider>() with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨
        member val Description = null:string     with get, set
        member val Author      = null:string     with get, set
        member val Version     = Version()       with get, set

        /// serialize 직전에 Runtime 으로부터 채워지고,
        /// deserialize 직후에 Runtime 으로 변환시켜 채워 줌.
        [<JsonProperty(Order = 100)>] member val SystemPrototypes = [||]:NjSystem[] with get, set

        [<JsonProperty(Order = 101)>] member val ActiveSystems    = [||]:NjSystemLoadType[]     with get, set
        [<JsonProperty(Order = 102)>] member val PassiveSystems   = [||]:NjSystemLoadType[]     with get, set

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnNsJsonSerializing  x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnNsJsonDeserialized x


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
        member val IRI           = nullString with get, set
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
            //let bag = ctx.Context |> cast<Nj2RtBag>
            fwdOnNsJsonSerializing x
        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            //let bag = ctx.Context |> cast<Nj2RtBag>
            fwdOnNsJsonDeserialized x

        static member FromRuntime(rt:RtSystem) =
            let originGuid = rt.OriginGuid |> Option.toNullable

            NjSystem(OriginGuid=originGuid, IRI=rt.IRI, Author=rt.Author,
                LangVersion=rt.LangVersion, EngineVersion=rt.EngineVersion, Description=rt.Description)
            |> fromNjUniqINGD rt
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
            |> fromNjUniqINGD rt

    type NjWork () =
        inherit NjUnique()
        interface INjWork
        member val FlowGuid = null:string with get, set
        member val Motion     = nullString with get, set
        member val Script     = nullString with get, set
        member val IsFinished = false      with get, set
        member val NumRepeat  = 0          with get, set
        member val Period     = 0          with get, set
        member val Delay      = 0          with get, set

        // JSON 에는 RGFH 상태값 을 저장하지 않는다.   member val Status4    = DbStatus4.Ready with get, set

        member val Calls: NjCall[] = [||] with get, set
        member val Arrows:NjArrow[] = [||] with get, set

        member x.ShouldSerializeCalls() = x.Calls.NonNullAny()
        member x.ShouldSerializeArrows() = x.Arrows.NonNullAny()

        static member FromRuntime(rt:RtWork) =
            NjWork()
            |> fromNjUniqINGD rt
            |> tee (fun z ->
                z.Motion     <- rt.Motion
                z.Script     <- rt.Script
                z.IsFinished <- rt.IsFinished
                z.NumRepeat  <- rt.NumRepeat
                z.Period     <- rt.Period
                z.Delay      <- rt.Delay

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
            |> fromNjUniqINGD (rt :?> Unique)
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

        // JSON 에는 RGFH 상태값 을 저장하지 않는다.   member val Status4    = DbStatus4.Ready with get, set

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
            |> fromNjUniqINGD rt
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
        member val ValueType  = nullString with get, set
        member val RangeType  = nullString with get, set
        member val Value1     = nullString with get, set
        member val Value2     = nullString with get, set

        static member FromRuntime(rt:RtApiCall) =
            NjApiCall(ApiDef=rt.ApiDefGuid, InAddress=rt.InAddress, OutAddress=rt.OutAddress,
                InSymbol=rt.InSymbol, OutSymbol=rt.OutSymbol,
                ValueType=rt.ValueType.ToString(), RangeType=rt.RangeType.ToString(),
                Value1=rt.Value1, Value2=rt.Value2 )
            |> fromNjUniqINGD rt

    type NjApiDef() =
        inherit NjUnique()
        interface INjApiDef

        member val IsPush = false with get, set

        static member FromRuntime(rt:RtApiDef) =
            assert(isItNotNull rt)
            NjApiDef(IsPush=rt.IsPush)
            |> fromNjUniqINGD rt














    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec internal onNsJsonSerializing (njObj:INjObject) =
        match njObj with
        | :? NjUnique as uniq ->
            uniq |> fromNjUniqINGD uniq.RuntimeObject |> ignore
        | _ ->
            ()

        match njObj with
        | :? NjProject as njp ->
            let rtp = njp.RuntimeObject :?> RtProject

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

            njp.Database <- rtp.Database
            njp.SystemPrototypes |> iter onNsJsonSerializing

        | :? NjSystem as njs ->
            njs.Arrows   |> iter onNsJsonSerializing
            njs.Flows    |> iter onNsJsonSerializing
            njs.Works    |> iter onNsJsonSerializing
            njs.ApiDefs  |> iter onNsJsonSerializing
            njs.ApiCalls |> iter onNsJsonSerializing

        | :? NjWork as njw ->
            let rtw = njw.RuntimeObject :?> RtWork
            njw.Arrows |> iter onNsJsonSerializing
            njw.Calls  |> iter onNsJsonSerializing

        | :? NjCall as njc ->
            let rtc = njc.RuntimeObject :?> RtCall
            ()

        | (:? NjFlow) | (:? NjArrow) | (:? NjApiDef) | (:? NjApiCall) ->
            (* NjXXX.FromDS 에서 이미 다 채운 상태임.. *)
            ()

        | _ ->
            failwith "ERROR.  확장 필요?"




    /// JSON 읽고 나서 메모리 구조에 후처리 작업
    let rec internal onNsJsonDeserialized (njObj:INjObject) =
        // 공통 처리
        match njObj with
        | :? NjUnique as uniq -> ()
        | _ -> failwith "ERROR"

        // 개별 처리
        match njObj with
        | :? NjProject as njp ->
            let protos = njp.SystemPrototypes |-> (fun z -> z.RuntimeObject :?> RtSystem)
            let load (loadType:NjSystemLoadType):RtSystem =
                match loadType with
                | NjSystemLoadType.LocalDefinition sys ->
                    sys.RuntimeObject :?> RtSystem
                | NjSystemLoadType.Reference { InstanceName=name; PrototypeGuid=protoGuid; InstanceGuid=instanceGuid } ->
                    protos
                    |> find (fun p -> p.Guid = protoGuid)
                    |> (fun z -> fwdDuplicate z :?> RtSystem)
                    |> tee(fun s ->
                        s.Name <- name
                        s.Guid <- instanceGuid
                        s.PrototypeSystemGuid <- Some protoGuid
                        )

            njp.SystemPrototypes |> iter onNsJsonDeserialized
            njp.RuntimeObject <-
                noop()
                let actives  = njp.ActiveSystems  |-> load
                let passives = njp.PassiveSystems |-> load
                let prototypeSystems = njp.SystemPrototypes |-> (fun s -> s.RuntimeObject :?> RtSystem )

                RtProject(prototypeSystems, actives, passives
                    , Author=njp.Author
                    , Version=njp.Version
                    , Description=njp.Description
                    , Database=njp.Database )
                |> fromUniqINGD njp
                |> tee (fun z ->
                    actives @ passives
                    |> iter (setParentI z) )

        | :? NjSystem as njs ->
            // flows, works, arrows 의 Parent 를 this(system) 으로 설정
            njs.Arrows   |> iter (fun z -> z.RawParent <- Some njs)
            njs.Flows    |> iter (fun z -> z.RawParent <- Some njs)
            njs.Works    |> iter (fun z -> z.RawParent <- Some njs)
            njs.ApiDefs  |> iter (fun z -> z.RawParent <- Some njs)
            njs.ApiCalls |> iter (fun z -> z.RawParent <- Some njs)

            // 하부 구조에 대해서 재귀적으로 호출 : dependancy 가 적은 것부터 먼저 생성할 것.
            njs.ApiDefs  |> iter onNsJsonDeserialized
            njs.ApiCalls |> iter onNsJsonDeserialized
            njs.Flows    |> iter onNsJsonDeserialized
            njs.Works    |> iter onNsJsonDeserialized

            let flows = njs.Flows |-> (fun z -> z.RuntimeObject :?> RtFlow)

            let works = [|
                for njw in njs.Works do
                    let optFlow =
                        if njw.FlowGuid.NonNullAny() then
                            flows |> tryFind (fun f -> f.Guid = s2guid njw.FlowGuid)
                        else
                            None
                    let calls  = njw.Calls  |-> (fun z -> z.RuntimeObject :?> RtCall)
                    let arrows = njw.Arrows |-> (fun z -> z.RuntimeObject :?> RtArrowBetweenCalls)

                    let dsWork =
                        RtWork.Create(calls, arrows, optFlow)
                        |> fromUniqINGD njw
                        |> tee(fun z ->
                            z.Motion     <- njw.Motion
                            z.Script     <- njw.Script
                            z.IsFinished <- njw.IsFinished
                            z.NumRepeat  <- njw.NumRepeat
                            z.Period     <- njw.Period
                            z.Delay      <- njw.Delay )

                    yield dsWork
                    njw.RuntimeObject <- dsWork
            |]

            njs.Arrows
            |> iter (fun (a:NjArrow) ->
                let works = njs.Works |-> (fun z -> z.RuntimeObject :?> RtWork)
                let src = works |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = works |> find(fun w -> w.Guid = s2guid a.Target)

                let arrowType =
                    a.Type
                    |> Enum.TryParse<DbArrowType>
                    |> tryParseToOption
                    |? DbArrowType.None

                a.RuntimeObject <-
                    RtArrowBetweenWorks(src, tgt, arrowType)
                    |> fromUniqINGD a)

            let arrows   = njs.Arrows   |-> (fun z -> z.RuntimeObject :?> RtArrowBetweenWorks)
            let apiDefs  = njs.ApiDefs  |-> (fun z -> z.RuntimeObject :?> RtApiDef)
            let apiCalls = njs.ApiCalls |-> (fun z -> z.RuntimeObject :?> RtApiCall)

            njs.RuntimeObject <-
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
                                , IRI=njs.IRI
                                , Author=njs.Author
                                , LangVersion=njs.LangVersion
                                , EngineVersion=njs.EngineVersion
                                , Description=njs.Description
                                , OriginGuid=n2o njs.OriginGuid)
                |> fromUniqINGD njs

        | :? NjFlow as njf ->
            njf.RuntimeObject <- RtFlow() |> fromUniqINGD njf
            ()

        | :? NjWork as njw ->
            njw.Calls  |> iter (fun z -> z.RawParent <- Some njw)
            njw.Calls  |> iter onNsJsonDeserialized
            njw.Arrows |> iter (fun z -> z.RawParent <- Some njw)

            njw.Arrows
            |> iter (fun (a:NjArrow) ->
                let calls = njw.Calls |-> (fun z -> z.RuntimeObject :?> RtCall)
                let src = calls |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = calls |> find(fun w -> w.Guid = s2guid a.Target)
                let arrowType =
                    a.Type
                    |> Enum.TryParse<DbArrowType>
                    |> tryParseToOption
                    |? DbArrowType.None

                a.RuntimeObject <-
                    RtArrowBetweenCalls(src, tgt, arrowType)
                    |> fromUniqINGD a )

            (* DsWork 객체 생성은 flow guid 생성 시까지 지연 *)

            ()

        | :? NjCall as njc ->
            let callType =
                njc.CallType
                |> Enum.TryParse<DbCallType>
                |> tryParseToOption
                |? DbCallType.Normal

            njc.RuntimeObject <-
                RtCall(callType, njc.ApiCalls, njc.AutoPre, njc.Safety, njc.IsDisabled, n2o njc.Timeout)
                |> fromUniqINGD njc
            ()

        | :? NjApiCall as njac ->
            let valueType =
                njac.ValueType
                |> Enum.TryParse<DbDataType>
                |> tryParseToOption
                |? DbDataType.None

            let rangeType =
                njac.RangeType
                |> Enum.TryParse<DbRangeType>
                |> tryParseToOption
                |? DbRangeType.None

            njac.RuntimeObject <-
                RtApiCall(njac.ApiDef, njac.InAddress, njac.OutAddress, njac.InSymbol, njac.OutSymbol,
                    valueType, rangeType, njac.Value1, njac.Value2)
                |> fromUniqINGD njac

        | :? NjApiDef as njad ->
            njad.RuntimeObject <-
                RtApiDef(njad.IsPush)
                |> fromUniqINGD njad
            ()

        | _ -> failwith "ERROR.  확장 필요?"



/// Ds Object 를 JSON 으로 변환하기 위한 모듈
[<AutoOpen>]
module Ds2JsonModule =
    /// Runtime 객체의 validation
    let internal validateRuntime (rtObj:#RtUnique): #RtUnique =
        let xxx = rtObj.EnumerateRtObjects() |> toArray
        let guidDic = rtObj.EnumerateRtObjects().ToDictionary(_.Guid, id)
        rtObj.Validate(guidDic)
        rtObj


    type NjProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string =
            (* Withh context version *)
            let settings = EmJson.CreateDefaultSettings()
            // Json deserialize 중에 필요한 담을 그릇 준비
            //settings.Context <- new StreamingContext(StreamingContextStates.All, Nj2RtBag())

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
            //settings.Context <- new StreamingContext(StreamingContextStates.All, Nj2RtBag())

            EmJson.FromJson<NjProject>(json, settings)

        static member FromRuntime(rt:RtProject) =
            NjProject(Database=rt.Database
                , Author=rt.Author
                , Version=rt.Version
                , Description=rt.Description)
            |> fromNjUniqINGD rt
            |> tee(fun nj -> verify (nj.RuntimeObject = rt)) // serialization 연결 고리


    type RtProject with // // ToJson, FromJson
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string = NjProject.FromRuntime(x).ToJson()
        member x.ToJson(jsonFilePath:string) = NjProject.FromRuntime(x).ToJsonFile(jsonFilePath)

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): RtProject =
            json
            |> NjProject.FromJson
            |> _.RuntimeObject :?> RtProject        // de-serialization 연결 고리
            |> validateRuntime





    type NjSystem with
        /// DsSystem 를 JSON 문자열로 변환
        member x.ExportToJson():string = EmJson.ToJson(x)
        member x.ExportToJsonFile(jsonFilePath:string) =
            x.ExportToJson()
            |> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsSystem 로 변환
        static member ImportFromJson(json:string): NjSystem = EmJson.FromJson<NjSystem>(json)

        static member FromRuntime(rt:RtSystem) =
            NjSystem(IRI=rt.IRI
                , Author=rt.Author
                , LangVersion=rt.LangVersion
                , EngineVersion=rt.EngineVersion
                , Description=rt.Description)
            |> fromNjUniqINGD rt
            |> tee(fun nj -> verify (nj.RuntimeObject = rt)) // serialization 연결 고리

    type RtSystem with // // ToJson, FromJson
        /// DsSystem 를 JSON 문자열로 변환
        member x.ExportToJson():string = NjSystem.FromRuntime(x).ExportToJson()
        member x.ExportToJson(jsonFilePath:string) = NjSystem.FromRuntime(x).ExportToJsonFile(jsonFilePath)

        /// JSON 문자열을 DsSystem 로 변환
        static member ImportFromJson(json:string): RtSystem =
            let jObject = JObject.Parse(json)
            match jObject.TryGet("RuntimeType") with
            | Some jValue when jValue.ToString() = "System" ->
                json
                |> NjSystem.ImportFromJson
                |> _.RuntimeObject :?> RtSystem        // de-serialization 연결 고리
                |> validateRuntime
            | _ -> // RuntimeType 이 없거나, 잘못된 경우
                failwith "Invalid system JSON file.  'RuntimeType' not found or mismatch."



