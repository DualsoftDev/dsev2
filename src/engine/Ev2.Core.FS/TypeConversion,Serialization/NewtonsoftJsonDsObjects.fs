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

        /// 확장 타입에서 override하여 추가 속성을 AAS serialize에 포함시킬 수 있는 확장점
        /// AAS 변환 시에는 이 메서드를 호출하지 않고 별도 처리함
        abstract member CollectExtensionProperties : unit -> JToken[]
        default x.CollectExtensionProperties() = [||]

        // RtUnique     -> NjUnique -> json 저장 시, RuntimeObject Some 값 이어야 함.
        // AAS Submodel -> NjUnique -> json 저장 시, RuntimeObject None 값 허용
        [<JsonIgnore>]
        member (*internal*) x.RuntimeObject
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
        abstract Initialize : activeSystems:NjSystem[] * passiveSystems:NjSystem[] * project:Project * isDeserialization:bool -> NjProject
        default x.Initialize(activeSystems:NjSystem[], passiveSystems:NjSystem[], project:Project, isDeserialization:bool) =
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

    type NjLamp() =
        inherit NjFlowEntity()

        interface INjLamp

    type NjCondition() =
        inherit NjFlowEntity()

        interface INjCondition

    type NjAction() =
        inherit NjFlowEntity()

        interface INjAction


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


    type NjApiDef() =
        inherit NjSystemEntity()
        interface INjApiDef

        member val IsPush = false with get, set
        member val TopicIndex = Option<int>.None with get, set
        member val IsTopicOrigin = Option<bool>.None with get, set



    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec (*internal*) onNsJsonSerializing (njObj:INjObject) =
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

                njp.ActiveSystems  <- rtp.ActiveSystems  |-> _.ToNj<NjSystem>() |> toArray
                njp.PassiveSystems <- rtp.PassiveSystems |-> _.ToNj<NjSystem>() |> toArray

                njp.Database <- rtp.Database

            | :? NjSystem as njs ->
                if isItNotNull njs.RuntimeObject then
                    njs.RuntimeObject |> replicateProperties njs |> ignore

                let rts = njs |> getRuntimeObject<DsSystem>
                njs.Works <- rts.Works |-> _.ToNj<NjWork>() |> toArray
                njs.Flows <- rts.Flows |-> _.ToNj<NjFlow>() |> toArray

                njs.Arrows   |> iter onNsJsonSerializing
                njs.Flows    |> iter onNsJsonSerializing
                njs.Works    |> iter onNsJsonSerializing
                njs.ApiDefs  |> iter onNsJsonSerializing
                njs.ApiCalls |> iter onNsJsonSerializing

            | :? NjWork as njw ->
                let rtw = njw |> getRuntimeObject<Work>
                njw.Calls <- rtw.Calls |-> _.ToNj<NjCall>() |> toArray


                njw.Arrows |> iter onNsJsonSerializing
                njw.Calls  |> iter onNsJsonSerializing

            | :? NjCall as njc ->
                let rtc = njc |> getRuntimeObject<Call>
                ()

            | ( (:? NjFlow) | (:? NjArrow) | (:? NjApiDef) | (:? NjApiCall)
            |   (:? NjButton) | (:? NjLamp) | (:? NjCondition) | (:? NjAction) )  ->
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
            let actives  = njp.ActiveSystems  |-> getRuntimeObject<DsSystem>
            let passives = njp.PassiveSystems |-> getRuntimeObject<DsSystem>

            let rtp =
                Project.Create(actives, passives, njp)
                |> replicateProperties njp
            // TypeFactory가 있으면 확장 속성 복사
            getTypeFactory() |> Option.iter (fun factory -> factory.CopyExtensionProperties(njp, rtp))

            actives @ passives |> iter (setParentI rtp)
            njp.RuntimeObject <- rtp

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
                        |> tee (fun rtw ->
                            // TypeFactory가 있으면 확장 속성 복사
                            getTypeFactory() |> Option.iter (fun factory -> factory.CopyExtensionProperties(njw, rtw)))

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
            let rts =
                DsSystem.Create((*protoGuid, *)flows, works, arrows, apiDefs, apiCalls)
                |> replicateProperties njs
            getTypeFactory() |> Option.iter (fun factory -> factory.CopyExtensionProperties(njs, rts))
            njs.RuntimeObject <- rts

        | :? NjFlow as njf ->
            njf.Buttons    |> iter (fun z -> z.RuntimeObject <- DsButton()     |> replicateProperties z)
            njf.Lamps      |> iter (fun z -> z.RuntimeObject <- Lamp()       |> replicateProperties z)
            njf.Conditions |> iter (fun z -> z.RuntimeObject <- DsCondition()  |> replicateProperties z)
            njf.Actions    |> iter (fun z -> z.RuntimeObject <- DsAction()     |> replicateProperties z)



            let buttons    = njf.Buttons    |-> getRuntimeObject<DsButton>
            let lamps      = njf.Lamps      |-> getRuntimeObject<Lamp>
            let conditions = njf.Conditions |-> getRuntimeObject<DsCondition>
            let actions    = njf.Actions    |-> getRuntimeObject<DsAction>

            let rtFlow =
                Flow.Create(buttons, lamps, conditions, actions)
                |> replicateProperties njf
                |> tee (fun rtf ->
                    // TypeFactory가 있으면 확장 속성 복사
                    getTypeFactory() |> Option.iter (fun factory -> factory.CopyExtensionProperties(njf, rtf)))

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
                |> tee (fun rtc ->
                    // TypeFactory가 있으면 확장 속성 복사
                    getTypeFactory() |> Option.iter (fun factory -> factory.CopyExtensionProperties(njc, rtc)))
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
                |> tee (fun rtac ->
                    // TypeFactory가 있으면 확장 속성 복사
                    getTypeFactory() |> Option.iter (fun factory -> factory.CopyExtensionProperties(njac, rtac)))

        | :? NjApiDef as njad ->
            njad.RuntimeObject <-
                ApiDef(njad.IsPush, ?topicIndex=njad.TopicIndex, ?isTopicOrigin=njad.IsTopicOrigin)
                |> replicateProperties njad
                |> tee (fun rtad ->
                    // TypeFactory가 있으면 확장 속성 복사
                    getTypeFactory() |> Option.iter (fun factory -> factory.CopyExtensionProperties(njad, rtad)))
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


    type NjProject with // ToJson, FromJson
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

    type Project with // // ToJson, FromJson
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string =
            let njProject = x.ToNjObj() :?> NjProject
            njProject.ToJson()
        member x.ToJson(jsonFilePath:string) =
            let njProject = x.ToNjObj() :?> NjProject
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

    type DsSystem with // // ToJson, FromJson
        /// DsSystem 를 JSON 문자열로 변환
        member x.ExportToJson():string =
            let njSystem = x.ToNj<NjSystem>()
            njSystem.ExportToJson()
        member x.ExportToJson(jsonFilePath:string) =
            let njSystem = x.ToNj<NjSystem>()
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
        let rtObj = rtObj :?> RtUnique
        /// TypeFactory를 통한 확장 타입 생성 헬퍼 함수 - xxx 스타일 적용
        let createWithTypeFactory (rtObj: RtUnique) (fallbackFactory: unit -> INjUnique) : INjUnique =
            let njObj =
                getTypeFactory()
                >>= (fun factory -> factory.CreateJson(rtObj.GetType(), rtObj) |> Option.ofObj)
            njObj
            |?? (fun () -> fallbackFactory())

        let createFallbackNjProject() =
            let rt = rtObj :?> Project
            NjProject(Database=rt.Database
                , Author=rt.Author
                , Version=rt.Version
                , Description=rt.Description)
            |> fromNjUniqINGD rt
            |> tee (fun z ->
                let activeSystems  = rt.ActiveSystems  |-> _.ToNj<NjSystem>() |> toArray
                let passiveSystems = rt.PassiveSystems |-> _.ToNj<NjSystem>() |> toArray
                z.Initialize(activeSystems, passiveSystems, rt, isDeserialization=false) |> ignore)
            |> tee(fun n ->
                // TypeFactory로 생성된 경우 RuntimeObject가 설정되지 않을 수 있음
                if not (isItNotNull n.RuntimeObject) then n.RuntimeObject <- rt
                verify (n.RuntimeObject = rt)) // serialization 연결 고리
            :> INjUnique

        let createFallbackNjSystem() =
            let rt = rtObj :?> DsSystem
            NjSystem(IRI=rt.IRI
                , Author=rt.Author
                , LangVersion=rt.LangVersion
                , EngineVersion=rt.EngineVersion
                , Description=rt.Description)
            |> fromNjUniqINGD rt
            |> tee (fun z ->
                let flows    = rt.Flows    |-> _.ToNj<NjFlow>()   |> toArray
                let works    = rt.Works    |-> _.ToNj<NjWork>()   |> toArray
                let arrows   = rt.Arrows   |-> _.ToNj<NjArrow>()  |> toArray
                let apiDefs  = rt.ApiDefs  |-> _.ToNj<NjApiDef>() |> toArray
                let apiCalls = rt.ApiCalls |-> _.ToNj<NjApiCall>() |> toArray
                z.Initialize(flows, works, arrows, apiDefs, apiCalls) |> ignore
            ) |> tee(fun n -> verify (n.RuntimeObject = rt)) // serialization 연결 고리
            :> INjUnique

        let createFallbackNjFlow() =
            let rt = rtObj :?> Flow
            NjFlow()
            |> fromNjUniqINGD rt
            |> tee(fun z ->
                let buttons    = rt.Buttons    |-> _.ToNj<NjButton>()   |> toArray
                let lamps      = rt.Lamps      |-> _.ToNj<NjLamp>()     |> toArray
                let conditions = rt.Conditions |-> _.ToNj<NjCondition>() |> toArray
                let actions    = rt.Actions    |-> _.ToNj<NjAction>()   |> toArray
                z.Initialize(buttons, lamps, conditions, actions) |> ignore)
            :> INjUnique

        let createFallbackNjWork() =
            let rt = rtObj :?> Work
            NjWork()
            |> fromNjUniqINGD rt
            |> tee (fun z ->
                let calls    = rt.Calls   |-> _.ToNj<NjCall>()  |> toArray
                let arrows   = rt.Arrows  |-> _.ToNj<NjArrow>() |> toArray
                let flowGuid = rt.Flow |-> (fun flow -> guid2str flow.Guid) |? null
                z.Initialize(calls, arrows, flowGuid) |> ignore
                z.Status4 <- rt.Status4)
            :> INjUnique

        let createFallbackNjCall() =
            let rt = rtObj :?> Call
            let ac = rt.AutoConditions |> jsonSerializeStrings
            let cc = rt.CommonConditions |> jsonSerializeStrings
            NjCall()
            |> fromNjUniqINGD rt
            |> tee (fun z ->
                let apiCalls = rt.ApiCalls |-> _.Guid |> toArray
                z.Initialize(rt.CallType.ToString(), apiCalls, ac, cc, rt.IsDisabled, rt.Timeout) |> ignore
                z.Status4 <- rt.Status4)
            :> INjUnique

        let createFallbackNjButton() =
            let rt = rtObj :?> DsButton
            NjButton()
            |> fromNjUniqINGD rt
            :> INjUnique

        let createFallbackNjLamp() =
            let rt = rtObj :?> Lamp
            NjLamp()
            |> fromNjUniqINGD rt
            :> INjUnique

        let createFallbackNjCondition() =
            let rt = rtObj :?> DsCondition
            NjCondition()
            |> fromNjUniqINGD rt
            :> INjUnique

        let createFallbackNjAction() =
            let rt = rtObj :?> DsAction
            NjAction()
            |> fromNjUniqINGD rt
            :> INjUnique

        let createFallbackNjArrow() =
            NjArrow()
            |> fromNjUniqINGD rtObj
            |> tee (fun z ->
                match rtObj with
                | :? ArrowBetweenWorks as arrow ->
                    z.Source <- guid2str (arrow.GetSource().Guid)
                    z.Target <- guid2str (arrow.GetTarget().Guid)
                    z.Type <- arrow.GetArrowType().ToString()
                | :? ArrowBetweenCalls as arrow ->
                    z.Source <- guid2str (arrow.GetSource().Guid)
                    z.Target <- guid2str (arrow.GetTarget().Guid)
                    z.Type <- arrow.GetArrowType().ToString()
                | _ -> ()
            )
            :> INjUnique

        let createFallbackNjApiCall() =
            let rt = rtObj :?> ApiCall
            let valueSpec = rt.ValueSpec |-> _.Jsonize() |? null
            NjApiCall(ApiDef=rt.ApiDefGuid, InAddress=rt.InAddress, OutAddress=rt.OutAddress,
                InSymbol=rt.InSymbol, OutSymbol=rt.OutSymbol,
                ValueSpec=valueSpec )
            |> fromNjUniqINGD rt
            :> INjUnique

        let createFallbackNjApiDef() =
            let rt = rtObj :?> ApiDef
            NjApiDef(IsPush=rt.IsPush, TopicIndex=rt.TopicIndex, IsTopicOrigin=rt.IsTopicOrigin)
            |> fromNjUniqINGD rt
            :> INjUnique

        if isItNull rtObj then
            getNull<NjUnique>()
        else
            let njObj =
                match rtObj with
                | :? Project               as p  -> createWithTypeFactory rtObj createFallbackNjProject
                | :? DsSystem              as s  -> createWithTypeFactory rtObj createFallbackNjSystem
                | :? Flow                  as f  -> createWithTypeFactory rtObj createFallbackNjFlow
                | :? Work                  as w  -> createWithTypeFactory rtObj createFallbackNjWork
                | :? Call                  as c  -> createWithTypeFactory rtObj createFallbackNjCall
                | :? DsButton              as b  -> createWithTypeFactory rtObj createFallbackNjButton
                | :? Lamp                  as l  -> createWithTypeFactory rtObj createFallbackNjLamp
                | :? DsCondition           as d  -> createWithTypeFactory rtObj createFallbackNjCondition
                | :? DsAction              as a  -> createWithTypeFactory rtObj createFallbackNjAction
                | :? ArrowBetweenWorks     as r  -> createWithTypeFactory rtObj createFallbackNjArrow
                | :? ArrowBetweenCalls     as r  -> createWithTypeFactory rtObj createFallbackNjArrow
                | :? ApiCall               as ac -> createWithTypeFactory rtObj createFallbackNjApiCall
                | :? ApiDef                as ad -> createWithTypeFactory rtObj createFallbackNjApiDef
                | _ -> failwith $"Unsupported runtime type: {rtObj.GetType().Name}"
                :?> NjUnique
            replicateProperties rtObj njObj |> ignore
            njObj.RuntimeObject <- rtObj // serialization 연결 고리
            onNsJsonSerializing njObj
            njObj
