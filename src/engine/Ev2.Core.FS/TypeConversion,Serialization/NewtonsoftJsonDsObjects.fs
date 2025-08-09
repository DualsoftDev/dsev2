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
open System.Diagnostics
open System.Runtime.CompilerServices

/// [N]ewtonsoft [J]son serialize 를 위한 DS 객체들.
[<AutoOpen>]
module NewtonsoftJsonModules =
    [<AbstractClass>]
    type NjUnique() as this =
        inherit Unique()
        interface INjUnique

        /// JSON 파일에 대한 comment.  눈으로 debugging 용도.  code 에서 사용하지 말 것.
        [<JsonProperty(Order = -101)>] member val private RuntimeType = let name = this.GetType().Name in Regex.Replace(name, "^Nj", "")

        // RtUnique     -> NjUnique -> json 저장 시, RuntimeObject Some 값 이어야 함.
        // AAS Submodel -> NjUnique -> json 저장 시, RuntimeObject None 값 허용
        [<JsonIgnore>]
        member internal x.RuntimeObject
            with get():Unique =
                x.RtObject
                >>= tryCast<Unique>
                |?? (fun () ->
                    Trace.WriteLine "RtObject not found in DynamicDictionary.  이 상황은 AAS 로부터 생성한 경우에 한해 허용됨."
                    getNull<Unique>())
            and set (v:Unique) =
                x.RtObject <- Some (box v :?> IRtUnique)


    let mutable internal fwdOnNsJsonSerializing:  INjObject->unit = let dummy (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy
    let mutable internal fwdOnNsJsonDeserialized: INjObject->unit = let dummy (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy


    /// NjUnique 객체의 RuntimeObject 를 'T type 으로 casting 해서 가져온다.
    let getRuntimeObject<'T when 'T :> RtUnique and 'T : not struct> (njObj:NjUnique) : 'T =
        if isItNull njObj.RuntimeObject then
            getNull<'T>()
        else
            njObj.RuntimeObject :?> 'T



/// Newtonsoft Json 호환 버젼
[<AutoOpen>]
module rec NewtonsoftJsonObjects =
    //let njSetParentI (parent:NjUnique) (x:#NjUnique): unit = x.RawParent <- Some parent


    /// Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 NjUnique 객체에 저장
    let internal fromNjUniqINGD (src:#Unique) (dst:#NjUnique): #NjUnique =
        if isItNotNull src then
            replicateProperties src dst |> ignore
        dst



    [<AbstractClass>]
    type NjProjectEntity() =
        inherit NjUnique()
        [<JsonIgnore>] member x.Project = x.RawParent >>= tryCast<NjProject>

    /// NjSystem 객체에 포함되는 member 들이 상속할 base class.  e.g NjFlow, NjWork, NjArrowBetweenWorks, NjApiDef, NjApiCall
    [<AbstractClass>]
    type NjSystemEntity() =
        inherit NjUnique()
        [<JsonIgnore>] member x.System  = x.RawParent >>= tryCast<NjSystem>
        [<JsonIgnore>] member x.Project = x.RawParent >>= _.RawParent >>= tryCast<NjProject>

    [<AbstractClass>]
    type NjFlowEntity() =
        inherit NjUnique()
        [<JsonIgnore>] member x.Flow    = x.RawParent >>= tryCast<NjFlow>
        [<JsonIgnore>] member x.System  = x.RawParent >>= _.RawParent >>= tryCast<NjSystem>
        [<JsonIgnore>] member x.Project = x.RawParent >>= _.RawParent>>= _.RawParent >>= tryCast<NjProject>

    [<AbstractClass>]
    type NjWorkEntity() =
        inherit NjUnique()
        [<JsonIgnore>] member x.Work    = x.RawParent >>= tryCast<NjWork>
        [<JsonIgnore>] member x.System  = x.RawParent >>= _.RawParent >>= tryCast<NjSystem>
        [<JsonIgnore>] member x.Project = x.RawParent >>= _.RawParent>>= _.RawParent >>= tryCast<NjProject>

    [<AbstractClass>]
    type NjCallEntity() =
        inherit NjUnique()
        [<JsonIgnore>] member x.Call    = x.RawParent >>= tryCast<NjCall>
        [<JsonIgnore>] member x.Work    = x.RawParent >>= _.RawParent >>= tryCast<NjWork>
        [<JsonIgnore>] member x.System  = x.RawParent >>= _.RawParent >>= _.RawParent >>= tryCast<NjSystem>
        [<JsonIgnore>] member x.Project = x.RawParent >>= _.RawParent >>= _.RawParent >>= _.RawParent >>= tryCast<NjProject>

    type NjProject() =
        inherit NjUnique()
        interface INjProject with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v


        member val Database    = getNull<DbProvider>() with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨
        member val Description = null:string with get, set
        member val Author      = null:string with get, set
        member val Version     = Version()   with get, set
        member val DateTime    = minDate     with get, set

        [<JsonProperty(Order = 101)>] member val ActiveSystems    = [||]:NjSystem[] with get, set
        [<JsonProperty(Order = 102)>] member val PassiveSystems   = [||]:NjSystem[] with get, set

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnNsJsonSerializing  x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnNsJsonDeserialized x

        /// Initialize 메서드 - abstract/default 패턴으로 가상함수 구현
        abstract Initialize : activeSystems:NjSystem[] * passiveSystems:NjSystem[] -> NjProject
        default x.Initialize(activeSystems:NjSystem[], passiveSystems:NjSystem[]) =
            x.ActiveSystems <- activeSystems
            x.PassiveSystems <- passiveSystems
            x


    type NjSystem() =
        inherit NjProjectEntity()
        interface INjSystem with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v

        [<JsonProperty(Order = 101)>] member val Flows    = [||]:NjFlow[]    with get, set
        [<JsonProperty(Order = 102)>] member val Works    = [||]:NjWork[]    with get, set
        [<JsonProperty(Order = 103)>] member val Arrows   = [||]:NjArrow[]   with get, set
        [<JsonProperty(Order = 104)>] member val ApiDefs  = [||]:NjApiDef[]  with get, set
        [<JsonProperty(Order = 104)>] member val ApiCalls = [||]:NjApiCall[] with get, set

        /// this system 이 prototype 으로 정의되었는지 여부
        member val internal IsPrototype = false with get, set
        /// this system 이 Instance 로 사용될 때에만 Some 값.
        member val PrototypeSystemGuid = Option<Guid>.None with get, set

        member val OriginGuid    = Option<Guid>.None with get, set
        member val IRI           = nullString with get, set
        member val Author        = nullString with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set
        member val DateTime      = minDate    with get, set

        member x.ShouldSerializeFlows   () = x.Flows   .NonNullAny()
        member x.ShouldSerializeWorks   () = x.Works   .NonNullAny()
        member x.ShouldSerializeArrows  () = x.Arrows  .NonNullAny()
        member x.ShouldSerializeApiDefs () = x.ApiDefs .NonNullAny()
        member x.ShouldSerializeApiCalls() = x.ApiCalls.NonNullAny()


        [<OnSerializing>]
        member x.OnSerializingMethod (ctx: StreamingContext) =
            fwdOnNsJsonSerializing x
        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            fwdOnNsJsonDeserialized x

        static member internal fromRuntime(rt:DsSystem) =
            // TypeFactory를 통한 확장 타입 지원
            let njSystem =
                if isItNotNull TypeFactoryModule.TypeFactory then
                    let jsonObj = TypeFactoryModule.TypeFactory.CreateJson(rt.GetType(), rt)
                    if isItNotNull jsonObj then
                        jsonObj :?> NjSystem
                    else
                        NjSystem() |> fromNjUniqINGD rt
                else
                    NjSystem() |> fromNjUniqINGD rt

            njSystem
            |> tee (fun z ->
                let flows = rt.Flows |-> NjFlow.fromRuntime |> toArray
                let works = rt.Works |-> NjWork.fromRuntime |> toArray
                let arrows = rt.Arrows |-> NjArrow.fromRuntime |> toArray
                let apiDefs = rt.ApiDefs |-> NjApiDef.fromRuntime |> toArray
                let apiCalls = rt.ApiCalls |-> NjApiCall.fromRuntime |> toArray
                z.Initialize(flows, works, arrows, apiDefs, apiCalls) |> ignore
            )

        /// Initialize 메서드 - abstract/default 패턴으로 가상함수 구현
        abstract Initialize : flows:NjFlow[] * works:NjWork[] * arrows:NjArrow[] * apiDefs:NjApiDef[] * apiCalls:NjApiCall[] -> NjSystem
        default x.Initialize(flows:NjFlow[], works:NjWork[], arrows:NjArrow[],
                           apiDefs:NjApiDef[], apiCalls:NjApiCall[]) =
            x.Flows <- flows
            x.Works <- works
            x.Arrows <- arrows
            x.ApiDefs <- apiDefs
            x.ApiCalls <- apiCalls
            x

    type NjFlow () =
        inherit NjSystemEntity()
        interface INjFlow

        [<JsonProperty(Order = 101)>] member val Buttons    = [||]:NjButton    []    with get, set
        [<JsonProperty(Order = 102)>] member val Lamps      = [||]:NjLamp      []    with get, set
        [<JsonProperty(Order = 103)>] member val Conditions = [||]:NjCondition []    with get, set
        [<JsonProperty(Order = 104)>] member val Actions    = [||]:NjAction    []    with get, set

        member x.ShouldSerializeButtons    () = x.Buttons   .NonNullAny()
        member x.ShouldSerializeLamps      () = x.Lamps     .NonNullAny()
        member x.ShouldSerializeConditions () = x.Conditions.NonNullAny()
        member x.ShouldSerializeActions    () = x.Actions   .NonNullAny()


        static member internal fromRuntime(rt:Flow) =
            // TypeFactory를 통한 확장 타입 지원
            let njFlow =
                if isItNotNull TypeFactoryModule.TypeFactory then
                    let jsonObj = TypeFactoryModule.TypeFactory.CreateJson(rt.GetType(), rt)
                    if isItNotNull jsonObj then
                        jsonObj :?> NjFlow
                    else
                        NjFlow() |> fromNjUniqINGD rt
                else
                    NjFlow() |> fromNjUniqINGD rt

            njFlow
            |> tee(fun z ->
                z.Buttons    <- rt.Buttons    |-> NjButton   .fromRuntime |> toArray
                z.Lamps      <- rt.Lamps      |-> NjLamp     .fromRuntime |> toArray
                z.Conditions <- rt.Conditions |-> NjCondition.fromRuntime |> toArray
                z.Actions    <- rt.Actions    |-> NjAction   .fromRuntime |> toArray
            )

        /// Initialize 메서드 - abstract/default 패턴으로 가상함수 구현
        abstract Initialize : buttons:NjButton[] * lamps:NjLamp[] * conditions:NjCondition[] * actions:NjAction[] -> NjFlow
        default x.Initialize(buttons:NjButton[], lamps:NjLamp[],
                           conditions:NjCondition[], actions:NjAction[]) =
            x.Buttons <- buttons
            x.Lamps <- lamps
            x.Conditions <- conditions
            x.Actions <- actions
            x

    type NjButton() =
        inherit NjFlowEntity()

        interface INjButton
        static member internal fromRuntime(rt:DsButton) =
            NjButton()
            |> fromNjUniqINGD rt

    type NjLamp() =
        inherit NjFlowEntity()

        interface INjLamp
        static member internal fromRuntime(rt:Lamp) =
            NjLamp()
            |> fromNjUniqINGD rt

    type NjCondition() =
        inherit NjFlowEntity()

        interface INjCondition
        static member internal fromRuntime(rt:DsCondition) =
            NjCondition()
            |> fromNjUniqINGD rt

    type NjAction() =
        inherit NjFlowEntity()

        interface INjAction
        static member internal fromRuntime(rt:DsAction) =
            NjAction()
            |> fromNjUniqINGD rt


    type NjWork () =
        inherit NjSystemEntity()
        interface INjWork
        member val FlowGuid   = null:string with get, set
        member val Motion     = nullString  with get, set
        member val Script     = nullString  with get, set
        member val IsFinished = false       with get, set
        member val NumRepeat  = 0           with get, set
        member val Period     = 0           with get, set
        member val Delay      = 0           with get, set

        // JSON 에는 RGFH 상태값 을 저장하지 않는다.   member val Status4    = DbStatus4.Ready with get, set

        member val Calls: NjCall[] = [||] with get, set
        member val Arrows:NjArrow[] = [||] with get, set

        [<JsonIgnore>]
        member val Status4 = Option<DbStatus4>.None with get, set

        member x.Status
            with get() = x.Status4 |> Option.map (_.ToString()) |> Option.toObj
            and set v = x.Status4 <- if isNull v then None else Enum.TryParse<DbStatus4>(v) |> tryParseToOption

        member x.ShouldSerializeCalls()      = x.Calls.NonNullAny()
        member x.ShouldSerializeArrows()     = x.Arrows.NonNullAny()
        member x.ShouldSerializeIsFinished() = x.IsFinished
        member x.ShouldSerializeNumRepeat()  = x.NumRepeat > 0
        member x.ShouldSerializePeriod()     = x.Period > 0
        member x.ShouldSerializeDelay()      = x.Period > 0
        member x.ShouldSerializeStatus()     = x.Status4.IsSome

        static member internal fromRuntime(rt:Work) =
            // TypeFactory를 통한 확장 타입 지원
            let njWork =
                if isItNotNull TypeFactoryModule.TypeFactory then
                    let jsonObj = TypeFactoryModule.TypeFactory.CreateJson(rt.GetType(), rt)
                    if isItNotNull jsonObj then
                        jsonObj :?> NjWork
                    else
                        NjWork() |> fromNjUniqINGD rt
                else
                    NjWork() |> fromNjUniqINGD rt

            njWork
            |> tee (fun z ->
                z.Calls    <- rt.Calls   |-> NjCall.fromRuntime  |> toArray
                z.Arrows   <- rt.Arrows  |-> NjArrow.fromRuntime |> toArray
                z.FlowGuid <- rt.Flow |-> (fun flow -> guid2str flow.Guid) |? null
                z.Status4 <- rt.Status4
            )

        /// Initialize 메서드 - abstract/default 패턴으로 가상함수 구현
        abstract Initialize : calls:NjCall[] * arrows:NjArrow[] * flowGuid:string -> NjWork
        default x.Initialize(calls:NjCall[], arrows:NjArrow[], flowGuid:string) =
            x.Calls <- calls
            x.Arrows <- arrows
            x.FlowGuid <- flowGuid
            x

    type NjArrow() =
        inherit NjUnique()

        interface INjArrow
        member val Source = null:string with get, set
        member val Target = null:string with get, set
        member val Type = DbArrowType.None.ToString() with get, set

        static member internal fromRuntime(rt:IArrow) =
            assert(isItNotNull rt)
            NjArrow()
            |> fromNjUniqINGD (rt :?> Unique)
            |> tee (fun z ->
                z.Source <- guid2str (rt.GetSource().Guid)
                z.Target <- guid2str (rt.GetTarget().Guid)
                z.Type <- rt.GetArrowType().ToString()
            )

    type NjCall() =
        inherit NjWorkEntity()

        interface INjCall
        [<JsonProperty(Order = 101)>]
        member val CallType = DbCallType.Normal.ToString() with get, set
        /// Json serialize 용 API call 에 대한 Guid
        [<JsonProperty(Order = 102)>]
        member val ApiCalls   = [||]:Guid[]     with get, set

        // JSON 에는 RGFH 상태값 을 저장하지 않는다.   member val Status4    = DbStatus4.Ready with get, set

        [<JsonProperty(Order = 103)>]
        member val AutoConditions   = nullString with get, set
        [<JsonProperty(Order = 104)>]
        member val CommonConditions = nullString with get, set
        [<JsonProperty(Order = 105)>]
        member val IsDisabled = false            with get, set
        [<JsonProperty(Order = 106)>]
        member val Timeout    = Option<int>.None with get, set

        [<JsonIgnore>]
        member val Status4 = Option<DbStatus4>.None with get, set

        member x.Status
            with get() = x.Status4 |> Option.map (_.ToString()) |> Option.toObj
            and set v = x.Status4 <- if isNull v then None else Enum.TryParse<DbStatus4>(v) |> tryParseToOption

        (* 특별한 조건일 때에만 json 표출 *)
        member x.ShouldSerializeApiCalls()   = x.ApiCalls.NonNullAny()
        member x.ShouldSerializeIsDisabled() = x.IsDisabled
        member x.ShouldSerializeCallType()   = x.CallType <> DbCallType.Normal.ToString()
        member x.ShouldSerializeStatus()     = x.Status4.IsSome
        member x.ShouldSerializeAutoConditions() = not (String.IsNullOrEmpty(x.AutoConditions))
        member x.ShouldSerializeCommonConditions() = not (String.IsNullOrEmpty(x.CommonConditions))
        member x.ShouldSerializeTimeout()    = x.Timeout.IsSome

        static member internal fromRuntime(rt:Call) =
            let ac = rt.AutoConditions |> jsonSerializeStrings
            let cc = rt.CommonConditions |> jsonSerializeStrings
            // TypeFactory를 통한 확장 타입 지원
            let njCall =
                if isItNotNull TypeFactoryModule.TypeFactory then
                    let jsonObj = TypeFactoryModule.TypeFactory.CreateJson(rt.GetType(), rt)
                    if isItNotNull jsonObj then
                        jsonObj :?> NjCall
                    else
                        NjCall(CallType = rt.CallType.ToString(), AutoConditions=ac, CommonConditions=cc, Timeout=rt.Timeout)
                        |> fromNjUniqINGD rt
                else
                    NjCall(CallType = rt.CallType.ToString(), AutoConditions=ac, CommonConditions=cc, Timeout=rt.Timeout)
                    |> fromNjUniqINGD rt

            njCall
            |> tee (fun z ->
                z.ApiCalls <- rt.ApiCalls |-> _.Guid |> toArray
                z.Status4 <- rt.Status4
            )

        /// Initialize 메서드 - abstract/default 패턴으로 가상함수 구현
        abstract Initialize : callType:string * apiCalls:Guid[] * autoConditions:string * commonConditions:string * isDisabled:bool * timeout:int option -> NjCall
        default x.Initialize(callType:string, apiCalls:Guid[],
                           autoConditions:string, commonConditions:string,
                           isDisabled:bool, timeout:int option) =
            x.CallType <- callType
            x.ApiCalls <- apiCalls
            x.AutoConditions <- autoConditions
            x.CommonConditions <- commonConditions
            x.IsDisabled <- isDisabled
            x.Timeout <- timeout
            x


    type NjApiCall() =
        inherit NjSystemEntity()

        interface INjApiCall
        member val ApiDef     = emptyGuid  with get, set
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val ValueSpec  = nullString with get, set

        static member internal fromRuntime(rt:ApiCall) =
            let valueSpec = rt.ValueSpec |-> _.Jsonize() |? null
            NjApiCall(ApiDef=rt.ApiDefGuid, InAddress=rt.InAddress, OutAddress=rt.OutAddress,
                InSymbol=rt.InSymbol, OutSymbol=rt.OutSymbol,
                ValueSpec=valueSpec )
            |> fromNjUniqINGD rt

    type NjApiDef() =
        inherit NjSystemEntity()
        interface INjApiDef

        member val IsPush = false with get, set
        member val TopicIndex = Option<int>.None with get, set
        member val IsTopicOrigin = Option<bool>.None with get, set

        static member internal fromRuntime(rt:ApiDef) =
            assert(isItNotNull rt)
            NjApiDef(IsPush=rt.IsPush, TopicIndex=rt.TopicIndex, IsTopicOrigin=rt.IsTopicOrigin)
            |> fromNjUniqINGD rt


    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec internal onNsJsonSerializing (njObj:INjObject) =
        let njUnique = njObj |> tryCast<NjUnique>
        match njUnique |-> _.RuntimeObject with
        // RuntimeObject 가 없는 경우는 AAS Submodel 에서 생성한 경우임.
        // 이 경우는 RuntimeObject 를 채우지 않고, 그냥 넘어감.
        | Some runtimeObj when isItNull runtimeObj -> ()
        | None -> ()

        | Some runtimeObj ->
            fromNjUniqINGD runtimeObj njUnique.Value |> ignore

            match njObj with
            | :? NjProject as njp ->
                let rtp = njp |> getRuntimeObject<Project>

                njp.ActiveSystems  <- rtp.ActiveSystems  |-> NjSystem.fromRuntime |> toArray
                njp.PassiveSystems <- rtp.PassiveSystems |-> NjSystem.fromRuntime |> toArray

                njp.Database <- rtp.Database

            | :? NjSystem as njs ->
                if isItNotNull njs.RuntimeObject then
                    njs.RuntimeObject |> replicateProperties njs |> ignore

                njs.Arrows   |> iter onNsJsonSerializing
                njs.Flows    |> iter onNsJsonSerializing
                njs.Works    |> iter onNsJsonSerializing
                njs.ApiDefs  |> iter onNsJsonSerializing
                njs.ApiCalls |> iter onNsJsonSerializing

            | :? NjWork as njw ->
                let rtw = njw |> getRuntimeObject<Work>
                njw.Arrows |> iter onNsJsonSerializing
                njw.Calls  |> iter onNsJsonSerializing

            | :? NjCall as njc ->
                let rtc = njc |> getRuntimeObject<Call>
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
            njp.RuntimeObject <-
                noop()
                let actives  = njp.ActiveSystems  |-> getRuntimeObject<DsSystem>
                let passives = njp.PassiveSystems |-> getRuntimeObject<DsSystem>

                Project.Create(actives, passives)
                |> replicateProperties njp
                |> tee (fun rtp ->
                    actives @ passives
                    |> iter (setParentI rtp) )

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

            let flows = njs.Flows |-> getRuntimeObject<Flow>

            let works = [|
                for njw in njs.Works do
                    let optFlow =
                        if njw.FlowGuid.NonNullAny() then
                            flows |> tryFind (fun f -> f.Guid = s2guid njw.FlowGuid)
                        else
                            None
                    let calls  = njw.Calls  |-> getRuntimeObject<Call>
                    let arrows = njw.Arrows |-> getRuntimeObject<ArrowBetweenCalls>

                    let dsWork =
                        Work.Create(calls, arrows, optFlow)
                        |> replicateProperties njw

                    yield dsWork
                    njw.RuntimeObject <- dsWork
            |]

            njs.Arrows
            |> iter (fun (a:NjArrow) ->
                let works = njs.Works |-> getRuntimeObject<Work>
                let src = works |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = works |> find(fun w -> w.Guid = s2guid a.Target)

                let arrowType =
                    a.Type
                    |> Enum.TryParse<DbArrowType>
                    |> tryParseToOption
                    |? DbArrowType.None

                a.RuntimeObject <-
                    ArrowBetweenWorks(src, tgt, arrowType)
                    |> replicateProperties a)

            let arrows   = njs.Arrows   |-> getRuntimeObject<ArrowBetweenWorks>
            let apiDefs  = njs.ApiDefs  |-> getRuntimeObject<ApiDef>
            let apiCalls = njs.ApiCalls |-> getRuntimeObject<ApiCall>

            njs.RuntimeObject <-
                DsSystem.Create((*protoGuid, *)flows, works, arrows, apiDefs, apiCalls)
                |> replicateProperties njs

        | :? NjFlow as njf ->
            njf.Buttons    |> iter (fun z -> z.RuntimeObject <- DsButton()     |> replicateProperties z)
            njf.Lamps      |> iter (fun z -> z.RuntimeObject <- Lamp()       |> replicateProperties z)
            njf.Conditions |> iter (fun z -> z.RuntimeObject <- DsCondition()  |> replicateProperties z)
            njf.Actions    |> iter (fun z -> z.RuntimeObject <- DsAction()     |> replicateProperties z)



            let buttons    = njf.Buttons    |-> getRuntimeObject<DsButton>
            let lamps      = njf.Lamps      |-> getRuntimeObject<Lamp>
            let conditions = njf.Conditions |-> getRuntimeObject<DsCondition>
            let actions    = njf.Actions    |-> getRuntimeObject<DsAction>

            let rtFlow = Flow.Create(buttons, lamps, conditions, actions) |> replicateProperties njf
            let all:NjUnique seq =
                njf.Buttons     .Cast<NjUnique>()
                @ njf.Lamps     .Cast<NjUnique>()
                @ njf.Conditions.Cast<NjUnique>()
                @ njf.Actions   .Cast<NjUnique>()
            all |> iter (fun z -> z.RawParent <- Some njf)

            njf.RuntimeObject <-  rtFlow
            ()

        | :? NjWork as njw ->
            njw.Calls  |> iter (fun z -> z.RawParent <- Some njw)
            njw.Calls  |> iter onNsJsonDeserialized
            njw.Arrows |> iter (fun z -> z.RawParent <- Some njw)

            njw.Arrows
            |> iter (fun (a:NjArrow) ->
                let calls = njw.Calls |-> getRuntimeObject<Call>
                let src = calls |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = calls |> find(fun w -> w.Guid = s2guid a.Target)
                let arrowType =
                    a.Type
                    |> Enum.TryParse<DbArrowType>
                    |> tryParseToOption
                    |? DbArrowType.None

                a.RuntimeObject <-
                    ArrowBetweenCalls(src, tgt, arrowType)
                    |> replicateProperties a )

            (* DsWork 객체 생성은 flow guid 생성 시까지 지연 *)

            ()

        | :? NjCall as njc ->
            let callType =
                njc.CallType
                |> Enum.TryParse<DbCallType>
                |> tryParseToOption
                |? DbCallType.Normal

            njc.RuntimeObject <-
                let acs = njc.AutoConditions |> jsonDeserializeStrings
                let ccs = njc.CommonConditions |> jsonDeserializeStrings
                Call.Create(callType, njc.ApiCalls, acs, ccs, njc.IsDisabled, njc.Timeout)
                |> replicateProperties njc
            ()

        | :? NjApiCall as njac ->
            njac.RuntimeObject <-
                let valueParam =
                    match njac.ValueSpec with
                    | null | "" -> None
                    | p -> deserializeWithType p |> Some
                noop()
                ApiCall(njac.ApiDef, njac.InAddress, njac.OutAddress, njac.InSymbol, njac.OutSymbol,
                    valueParam)
                |> replicateProperties njac

        | :? NjApiDef as njad ->
            njad.RuntimeObject <-
                ApiDef(njad.IsPush, ?topicIndex=njad.TopicIndex, ?isTopicOrigin=njad.IsTopicOrigin)
                |> replicateProperties njad
            ()


        | :? NjButton as njx ->
            njx.RuntimeObject <-
                DsButton()
                |> replicateProperties njx



        | _ -> failwith "ERROR.  확장 필요?"



/// Ds Object 를 JSON 으로 변환하기 위한 모듈
[<AutoOpen>]
module Ds2JsonModule =
    /// Runtime 객체의 validation
    let validateRuntime (rtObj:#RtUnique): #RtUnique =
        let guidDic = rtObj.EnumerateRtObjects().ToDictionary(_.Guid, fun z -> z :> Unique)
        rtObj.Validate(guidDic)
        rtObj


    type NjProject with
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string =
            (* Withh context version *)
            let settings = EmJson.CreateDefaultSettings()
            settings.DateFormatString <- DateFormatString
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

        static member internal fromRuntime(rt:Project) =
            // TypeFactory를 통한 확장 타입 지원
            let njProject =
                if isItNotNull TypeFactoryModule.TypeFactory then
                    let jsonObj = TypeFactoryModule.TypeFactory.CreateJson(rt.GetType(), rt)
                    if isItNotNull jsonObj then
                        jsonObj :?> NjProject
                    else
                        NjProject(Database=rt.Database
                            , Author=rt.Author
                            , Version=rt.Version
                            , Description=rt.Description)
                        |> fromNjUniqINGD rt
                else
                    NjProject(Database=rt.Database
                        , Author=rt.Author
                        , Version=rt.Version
                        , Description=rt.Description)
                    |> fromNjUniqINGD rt

            njProject |> tee(fun n ->
                // TypeFactory로 생성된 경우 RuntimeObject가 설정되지 않을 수 있음
                if not (isItNotNull n.RuntimeObject) then n.RuntimeObject <- rt
                verify (n.RuntimeObject = rt)) // serialization 연결 고리


    type Project with // // ToJson, FromJson
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string =
            let njProject = NjProject.fromRuntime(x)
            njProject.ToJson()
        member x.ToJson(jsonFilePath:string) =
            let njProject = NjProject.fromRuntime(x)
            njProject.ToJsonFile(jsonFilePath)

        /// JSON 문자열을 DsProject 로 변환
        static member FromJson(json:string): Project =
            json
            |> NjProject.FromJson
            |> getRuntimeObject<Project>        // de-serialization 연결 고리
            |> validateRuntime




    type NjSystem with
        /// DsSystem 를 JSON 문자열로 변환
        member x.ExportToJson():string =
            let settings = EmJson.CreateDefaultSettings()
            settings.DateFormatString <- DateFormatString
            EmJson.ToJson(x, settings)
        member x.ExportToJsonFile(jsonFilePath:string) =
            x.ExportToJson()
            |> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsSystem 로 변환
        static member ImportFromJson(json:string): NjSystem = EmJson.FromJson<NjSystem>(json)

        static member internal fromRuntime(rt:DsSystem) =
            // 기본 NjSystem 생성 - TypeFactory 사용 제거 (JSON 타입은 확장 불필요)
            let njSystem =
                NjSystem(IRI=rt.IRI
                    , Author=rt.Author
                    , LangVersion=rt.LangVersion
                    , EngineVersion=rt.EngineVersion
                    , Description=rt.Description)
                |> fromNjUniqINGD rt

            njSystem |> tee(fun n -> verify (n.RuntimeObject = rt)) // serialization 연결 고리

    type DsSystem with // // ToJson, FromJson
        /// DsSystem 를 JSON 문자열로 변환
        member x.ExportToJson():string =
            let njSystem = NjSystem.fromRuntime(x)
            njSystem.ExportToJson()
        member x.ExportToJson(jsonFilePath:string) =
            let njSystem = NjSystem.fromRuntime(x)
            njSystem.ExportToJsonFile(jsonFilePath)

        /// JSON 문자열을 DsSystem 로 변환
        static member ImportFromJson(json:string): DsSystem =
            let jObject = JObject.Parse(json)
            match jObject.TryGet("RuntimeType") with
            | Some jValue when jValue.ToString() = "System" ->
                json
                |> NjSystem.ImportFromJson
                |> getRuntimeObject<DsSystem>        // de-serialization 연결 고리
                |> validateRuntime
            | _ -> // RuntimeType 이 없거나, 잘못된 경우
                failwith "Invalid system JSON file.  'RuntimeType' not found or mismatch."
        static member FromJson(json) = DsSystem.ImportFromJson(json)


    /// IRtUnique 전용 Runtime 객체를 JSON 타입으로 변환
    let rtObj2NjObj (rtObj:IRtUnique): INjUnique =
        /// TypeFactory를 통한 확장 타입 생성 헬퍼 함수 - xxx 스타일 적용
        let createWithTypeFactory (rtObj: IRtUnique) (fallbackFactory: 'T -> INjUnique) : INjUnique =
            let xxx =
                if isItNotNull TypeFactory then
                    let jsonObj = TypeFactory.CreateJson(rtObj.GetType(), rtObj)
                    if isItNotNull jsonObj then jsonObj :?> INjUnique
                    else fallbackFactory (rtObj :?> 'T)
                else fallbackFactory (rtObj :?> 'T)
            xxx

        if isItNull rtObj then
            getNull<NjUnique>()
        else
            match rtObj with
            | :? Project  as p -> createWithTypeFactory rtObj (NjProject.fromRuntime >> fun x -> x :> INjUnique)
            | :? DsSystem as s -> createWithTypeFactory rtObj (NjSystem.fromRuntime  >> fun x -> x :> INjUnique)
            | :? Flow     as f -> createWithTypeFactory rtObj (NjFlow.fromRuntime    >> fun x -> x :> INjUnique)
            | :? Work     as w -> createWithTypeFactory rtObj (NjWork.fromRuntime    >> fun x -> x :> INjUnique)
            | :? Call     as c -> createWithTypeFactory rtObj (NjCall.fromRuntime    >> fun x -> x :> INjUnique)
            | _ -> failwith $"Unsupported runtime type: {rtObj.GetType().Name}"

