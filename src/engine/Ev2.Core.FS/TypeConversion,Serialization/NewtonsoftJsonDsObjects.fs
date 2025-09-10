namespace Ev2.Core.FS

open System
open System.Runtime.Serialization
open System.IO
open System.Linq
open System.Text.RegularExpressions
open System.Diagnostics

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Core.FS
open Dual.Common.Base
open Dual.Common.Db.FS
open Newtonsoft.Json.Serialization

// ApiCallValueSpecs를 위한 커스텀 JsonConverter - OnSerializing/OnDeserialized 패턴으로 대체됨
// type ApiCallValueSpecsConverter() =
//     inherit JsonConverter<ApiCallValueSpecs>()
//
//     override x.WriteJson(writer: JsonWriter, value: ApiCallValueSpecs, serializer: JsonSerializer) =
//         // ToJson()을 사용하여 JSON 문자열 배열로 직렬화
//         let json = value.ToJson()
//         writer.WriteRawValue(json)
//
//     override x.ReadJson(reader: JsonReader, objectType: Type, existingValue: ApiCallValueSpecs, hasExistingValue: bool, serializer: JsonSerializer) =
//         // JSON 문자열을 읽어서 FromJson()으로 역직렬화
//         let token = JToken.Load(reader)
//         match token.Type with
//         | JTokenType.String ->
//             // 문자열인 경우 직접 파싱
//             let json = token.ToString()
//             ApiCallValueSpecs.FromJson(json)
//         | JTokenType.Array ->
//             // 배열인 경우 JSON 문자열로 변환 후 파싱
//             let json = token.ToString()
//             ApiCallValueSpecs.FromJson(json)
//         | _ ->
//             // 기타 타입은 빈 컬렉션 반환
//             ApiCallValueSpecs()

/// [N]ewtonsoft [J]son serialize 를 위한 DS 객체들.
[<AutoOpen>]
module NewtonsoftJsonModules =
    [<AbstractClass>]
    type NjUnique() as this =     // RuntimeObject
        inherit Unique()
        interface INjUnique

        /// JSON 파일에 대한 comment.  눈으로 debugging 용도.  code 에서 사용하지 말 것.
        [<JsonProperty(Order = -101)>] member val private RuntimeType = let name = this.GetType().Name in Regex.Replace(name, "^Nj", "")

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


    [<AbstractClass>]
    type NjProjectEntity() =
        inherit NjUnique()
        [<JsonIgnore>] member x.Project = x.RawParent >>= tryCast<NjProject>

    /// NjSystem 객체에 포함되는 member 들이 상속할 base class.  e.g NjFlow, NjWork, NjArrowBetweenWorks, NjApiDef, NjApiCall
    [<AbstractClass>]
    type NjSystemEntity() =
        inherit NjUnique()
        interface ISystemEntity
        [<JsonIgnore>] member x.System  = x.RawParent >>= tryCast<NjSystem>
        [<JsonIgnore>] member x.Project = x.RawParent >>= _.RawParent >>= tryCast<NjProject>

    [<AbstractClass>]
    type NjSystemEntityWithFlow() =
        inherit NjSystemEntity()
        interface ISystemEntityWithFlow
        member val FlowGuid = null:string with get, set
        [<JsonIgnore>] member x.Flow = x.System |-> (fun s -> s.Flows |> tryFind(fun (f:NjFlow) -> f.Guid.ToString() = x.FlowGuid))

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

    type NjProject() = // Create, Initialize
        inherit NjUnique()
        interface INjProject with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v

        static member Create() = createExtended<NjProject>()

        member val Database    = getNull<DbProvider>() with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨
        member val AasxPath    = nullString with get, set // AASX 파일 경로.

        member val Description = null:string with get, set
        member val Author      = null:string with get, set
        member val Version     = Version()   with get, set
        member val DateTime    = minDate     with get, set

        [<JsonProperty(Order = 101)>] member val ActiveSystems    = [||]:NjSystem[] with get, set
        [<JsonProperty(Order = 102)>] member val PassiveSystems   = [||]:NjSystem[] with get, set

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnNsJsonSerializing  x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnNsJsonDeserialized x

        member x.Initialize(activeSystems:NjSystem[], passiveSystems:NjSystem[], project:Project, isDeserialization:bool) =
            x.ActiveSystems <- activeSystems
            x.PassiveSystems <- passiveSystems
            x


    type NjSystem() = // Create, Initialize, OnDeserializedMethod, OnSerializingMethod, ShouldSerializeApiCalls, ShouldSerializeApiDefs, ShouldSerializeArrows, ShouldSerializeFlows, ShouldSerializeWorks
        inherit NjProjectEntity()
        interface INjSystem with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v

        [<JsonProperty(Order = 101)>] member val Flows    = [||]:NjFlow[]    with get, set
        [<JsonProperty(Order = 102)>] member val Works    = [||]:NjWork[]    with get, set
        [<JsonProperty(Order = 103)>] member val Arrows   = [||]:NjArrow[]   with get, set
        [<JsonProperty(Order = 104)>] member val ApiDefs  = [||]:NjApiDef[]  with get, set
        [<JsonProperty(Order = 105)>] member val ApiCalls = [||]:NjApiCall[] with get, set
        [<JsonProperty(Order = 106)>] member val Buttons    = [||]:NjButton[]    with get, set
        [<JsonProperty(Order = 107)>] member val Lamps      = [||]:NjLamp[]      with get, set
        [<JsonProperty(Order = 108)>] member val Conditions = [||]:NjCondition[] with get, set
        [<JsonProperty(Order = 109)>] member val Actions    = [||]:NjAction[]    with get, set

        static member Create() = createExtended<NjSystem>()

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
        member x.ShouldSerializeButtons    () = x.Buttons   .NonNullAny()
        member x.ShouldSerializeLamps      () = x.Lamps     .NonNullAny()
        member x.ShouldSerializeConditions () = x.Conditions.NonNullAny()
        member x.ShouldSerializeActions    () = x.Actions   .NonNullAny()


        [<OnSerializing>]
        member x.OnSerializingMethod (ctx: StreamingContext) =
            fwdOnNsJsonSerializing x
        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            fwdOnNsJsonDeserialized x


        member x.Initialize(flows:NjFlow[], works:NjWork[], arrows:NjArrow[],
                           apiDefs:NjApiDef[], apiCalls:NjApiCall[],
                           buttons:NjButton[], lamps:NjLamp[], conditions:NjCondition[], actions:NjAction[]) =
            x.Flows    <- flows
            x.Works    <- works
            x.Arrows   <- arrows
            x.ApiDefs  <- apiDefs
            x.ApiCalls <- apiCalls
            x.Buttons    <- buttons
            x.Lamps      <- lamps
            x.Conditions <- conditions
            x.Actions    <- actions
            x

    type NjFlow () = // Create, Initialize
        inherit NjSystemEntity()
        interface INjFlow

        static member Create() = createExtended<NjFlow>()

        member x.Initialize() =
            x

    type NjButton() = // Create
        inherit NjSystemEntityWithFlow()

        interface INjButton
        static member Create() = createExtended<NjButton>()

    type NjLamp() = // Create
        inherit NjSystemEntityWithFlow()

        interface INjLamp
        static member Create() = createExtended<NjLamp>()

    type NjCondition() = // Create
        inherit NjSystemEntityWithFlow()

        interface INjCondition
        static member Create() = createExtended<NjCondition>()

    type NjAction() = // Create
        inherit NjSystemEntityWithFlow()

        interface INjAction
        static member Create() = createExtended<NjAction>()


    type NjWork () = // Create, Initialize, ShouldSerializeArrows, ShouldSerializeCalls, ShouldSerializeDelay, ShouldSerializeIsFinished, ShouldSerializeNumRepeat, ShouldSerializePeriod, ShouldSerializeStatus
        inherit NjSystemEntity()
        interface INjWork
        member val FlowGuid   = null:string with get, set
        member val Motion       = nullString  with get, set
        member val Script       = nullString  with get, set
        member val ExternalStart = nullString  with get, set
        member val IsFinished   = false       with get, set
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

        static member Create() = createExtended<NjWork>()

        member x.ShouldSerializeCalls()      = x.Calls.NonNullAny()
        member x.ShouldSerializeArrows()     = x.Arrows.NonNullAny()
        member x.ShouldSerializeIsFinished() = x.IsFinished
        member x.ShouldSerializeNumRepeat()  = x.NumRepeat > 0
        member x.ShouldSerializePeriod()     = x.Period > 0
        member x.ShouldSerializeDelay()      = x.Period > 0
        member x.ShouldSerializeStatus()     = x.Status4.IsSome


        member x.Initialize(calls:NjCall[], arrows:NjArrow[], flowGuid:string) =
            x.Calls <- calls
            x.Arrows <- arrows
            x.FlowGuid <- flowGuid
            x

    type NjArrow() = // Create
        inherit NjUnique()

        interface INjArrow
        member val Source = null:string with get, set
        member val Target = null:string with get, set
        member val Type = DbArrowType.None.ToString() with get, set
        static member Create() = createExtended<NjArrow>()

        [<JsonIgnore>] member val XSourceGuid = emptyGuid with get, set
        [<JsonIgnore>] member val XTargetGuid = emptyGuid with get, set
        [<JsonIgnore>] member val XTypeId:Id = 0 with get, set



    type NjCall() = // Create, Initialize, ShouldSerializeApiCalls, ShouldSerializeAutoConditions, ShouldSerializeCallType, ShouldSerializeCommonConditions, ShouldSerializeIsDisabled, ShouldSerializeStatus, ShouldSerializeTimeout
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

        [<JsonIgnore>]
        member val AutoConditionsObj   = ApiCallValueSpecs() with get, set
        [<JsonIgnore>]
        member val CommonConditionsObj = ApiCallValueSpecs() with get, set
        [<JsonProperty(Order = 105)>]
        member val IsDisabled = false            with get, set
        [<JsonProperty(Order = 106)>]
        member val Timeout    = Option<int>.None with get, set
        [<JsonProperty(Order = 107)>]
        member val CallValueSpec = nullString    with get, set

        [<JsonIgnore>]
        member val Status4 = Option<DbStatus4>.None with get, set

        member x.Status
            with get() = x.Status4 |> Option.map (_.ToString()) |> Option.toObj
            and set v = x.Status4 <- if isNull v then None else Enum.TryParse<DbStatus4>(v) |> tryParseToOption

        static member Create() = createExtended<NjCall>()

        (* 특별한 조건일 때에만 json 표출 *)
        member x.ShouldSerializeApiCalls()   = x.ApiCalls.NonNullAny()
        member x.ShouldSerializeIsDisabled() = x.IsDisabled
        member x.ShouldSerializeCallType()   = x.CallType <> DbCallType.Normal.ToString()
        member x.ShouldSerializeStatus()     = x.Status4.IsSome
        member x.ShouldSerializeAutoConditions() = x.AutoConditionsObj.NonNullAny()
        member x.ShouldSerializeCommonConditions() = x.CommonConditionsObj.NonNullAny()
        member x.ShouldSerializeTimeout()    = x.Timeout.IsSome

        [<OnSerializing>]
        member x.OnSerializingMethod (ctx: StreamingContext) =
            // ApiCallValueSpecs 객체를 JSON 문자열로 변환
            if x.AutoConditionsObj.Any() then
                x.AutoConditions <- x.AutoConditionsObj.ToJson()
            else
                x.AutoConditions <- null

            if x.CommonConditionsObj.Any() then
                x.CommonConditions <- x.CommonConditionsObj.ToJson()
            else
                x.CommonConditions <- null

            fwdOnNsJsonSerializing x

        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            // JSON 문자열을 ApiCallValueSpecs 객체로 변환
            if x.AutoConditions.NonNullAny() then
                x.AutoConditionsObj <- ApiCallValueSpecs.FromJson(x.AutoConditions)

            if x.CommonConditions.NonNullAny() then
                x.CommonConditionsObj <- ApiCallValueSpecs.FromJson(x.CommonConditions)

            fwdOnNsJsonDeserialized x

        member x.Initialize(
            callType:string, apiCalls:Guid[],
            autoConditions: ApiCallValueSpecs, commonConditions: ApiCallValueSpecs,
            isDisabled:bool, timeout:int option
        ) =
            x.CallType   <- callType
            x.ApiCalls   <- apiCalls
            x.IsDisabled <- isDisabled
            x.Timeout    <- timeout
            x.AutoConditionsObj <- autoConditions
            x.CommonConditionsObj <- commonConditions
            x


    type NjApiCall() = // Create
        inherit NjSystemEntity()

        interface INjApiCall
        member val ApiDef     = emptyGuid  with get, set
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val ValueSpec  = nullString with get, set
        static member Create() = createExtended<NjApiCall>()


    type NjApiDef() = // Create
        inherit NjSystemEntity()
        interface INjApiDef

        member val IsPush = false with get, set
        member val TxGuid = emptyGuid with get, set
        member val RxGuid = emptyGuid with get, set
        static member Create() = createExtended<NjApiDef>()
        member x.ShouldSerializeTxGuid() = x.TxGuid <> Guid.Empty
        member x.ShouldSerializeRxGuid() = x.RxGuid <> Guid.Empty



    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec (*internal*) onNsJsonSerializing (njObj:INjObject) =
        let njUnique = njObj |> tryCast<NjUnique>
        match njUnique |-> _.RuntimeObject with
        // RuntimeObject 가 없는 경우는 AAS Submodel 에서 NjObject 를 생성한 경우임.
        // 이 경우는 RuntimeObject 를 채우지 않고, 그냥 넘어감.
        | Some runtimeObj when isItNull runtimeObj ->
            //Debugger.Break()
            ()
        | None -> ()

        | Some runtimeObj ->
            replicateProperties runtimeObj njUnique.Value |> ignore

            match njObj with
            | :? NjProject as njp ->
                let rtp = njp |> getRuntimeObject<Project>

                njp.ActiveSystems  <- rtp.ActiveSystems  |-> _.ToNj<NjSystem>() |> toArray
                njp.PassiveSystems <- rtp.PassiveSystems |-> _.ToNj<NjSystem>() |> toArray

                njp.Database <- rtp.Database

            | :? NjSystem as njs ->
                //if isItNotNull njs.RuntimeObject then
                //    njs.RuntimeObject |> replicateProperties njs |> ignore

                let rts = njs |> getRuntimeObject<DsSystem>
                njs.Works <- rts.Works |-> _.ToNj<NjWork>() |> toArray
                njs.Flows <- rts.Flows |-> _.ToNj<NjFlow>() |> toArray
                njs.ApiDefs  <- rts.ApiDefs |-> _.ToNj<NjApiDef>() |> toArray
                njs.ApiCalls <- rts.ApiCalls |-> _.ToNj<NjApiCall>() |> toArray

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

            | ( (:? NjFlow) | (:? NjArrow) | (:? NjApiDef) | (:? NjApiCall) )  ->
                (* NjXXX.FromDS 에서 이미 다 채운 상태임.. *)
                ()

            | ( (:? NjButton) | (:? NjLamp) | (:? NjCondition) | (:? NjAction) ) ->
                (* UI 요소들도 replicateProperties 호출 필요 *)
                match njUnique |-> _.RuntimeObject with
                | Some runtimeObj when isItNotNull runtimeObj ->
                    replicateProperties runtimeObj njUnique.Value |> ignore
                | _ -> ()

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
                    let flowGuid =
                        if njw.FlowGuid.NonNullAny() then
                            flows |> tryFind (fun f -> f.Guid = s2guid njw.FlowGuid) |-> _.Guid
                        else
                            None
                    let calls  = njw.Calls  |-> getRuntimeObject<Call>
                    let arrows = njw.Arrows |-> getRuntimeObject<ArrowBetweenCalls>

                    let dsWork =
                        Work.Create(calls, arrows, flowGuid)
                        |> replicateProperties njw

                    // Status4 속성 복사
                    dsWork.Status4 <- njw.Status4

                    yield dsWork
                    njw.RuntimeObject <- dsWork
            |]

            njs.Arrows
            |> iter (fun (a:NjArrow) ->
                let arrowType =
                    a.Type
                    |> Enum.TryParse<DbArrowType>
                    |> tryParseToOption
                    |? DbArrowType.None

                a.RuntimeObject <-
                    ArrowBetweenWorks.Create(s2guid a.Source, s2guid a.Target, arrowType)
                    |> replicateProperties a)

            // UI 요소들의 RuntimeObject 생성
            njs.Buttons    |> iter (fun z -> z.RuntimeObject <- DsButton.Create()    |> replicateProperties z)
            njs.Lamps      |> iter (fun z -> z.RuntimeObject <- Lamp.Create()        |> replicateProperties z)
            njs.Conditions |> iter (fun z -> z.RuntimeObject <- DsCondition.Create() |> replicateProperties z)
            njs.Actions    |> iter (fun z -> z.RuntimeObject <- DsAction.Create()    |> replicateProperties z)

            let arrows   = njs.Arrows   |-> getRuntimeObject<ArrowBetweenWorks>
            let apiDefs  = njs.ApiDefs  |-> getRuntimeObject<ApiDef>
            let apiCalls = njs.ApiCalls |-> getRuntimeObject<ApiCall>
            let buttons    = njs.Buttons    |-> getRuntimeObject<DsButton>
            let lamps      = njs.Lamps      |-> getRuntimeObject<Lamp>
            let conditions = njs.Conditions |-> getRuntimeObject<DsCondition>
            let actions    = njs.Actions    |-> getRuntimeObject<DsAction>

            let rts =
                DsSystem.Create((*protoGuid, *)flows, works, arrows, apiDefs, apiCalls, buttons, lamps, conditions, actions)
                |> replicateProperties njs
            njs.RuntimeObject <- rts

        | :? NjFlow as njf ->
            // Flow는 이제 UI 요소를 직접 소유하지 않음
            let rtFlow =
                Flow.Create()
                |> replicateProperties njf

            njf.RuntimeObject <- rtFlow
            ()

        | :? NjWork as njw ->
            njw.Calls  |> iter (fun z -> z.RawParent <- Some njw)
            njw.Calls  |> iter onNsJsonDeserialized
            njw.Arrows |> iter (fun z -> z.RawParent <- Some njw)

            njw.Arrows
            |> iter (fun (a:NjArrow) ->
                let arrowType =
                    a.Type
                    |> Enum.TryParse<DbArrowType>
                    |> tryParseToOption
                    |? DbArrowType.None

                a.RuntimeObject <-
                    ArrowBetweenCalls.Create(s2guid a.Source, s2guid a.Target, arrowType)
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
                let acs = njc.AutoConditionsObj
                let ccs = njc.CommonConditionsObj
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
                ApiCall.Create(njac.ApiDef, njac.InAddress, njac.OutAddress, njac.InSymbol, njac.OutSymbol,
                    valueParam)
                |> replicateProperties njac

        | :? NjApiDef as njad ->
            njad.RuntimeObject <-
                ApiDef.Create(njad.IsPush(*, ?topicIndex=njad.TopicIndex, ?isTopicOrigin=njad.IsTopicOrigin*))
                |> replicateProperties njad
            ()


        | :? NjButton as njx ->
            njx.RuntimeObject <-
                DsButton.Create()
                |> replicateProperties njx



        | _ -> failwith "ERROR.  확장 필요?"



/// Ds Object 를 JSON 으로 변환하기 위한 모듈
[<AutoOpen>]
module Ds2JsonModule =
    /// Runtime 객체의 validation
    let validateRuntime (rtObj:#RtUnique): #RtUnique =
        let guidDic = rtObj.EnumerateRtObjects().ToDictionary(_.Guid, fun z -> z :> Unique) |> DuplicateBag
        rtObj.Validate(guidDic)
        rtObj


    type NjProject with // FromJson, ToJson, ToJsonFile
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
            // JSON을 JObject로 파싱하여 RuntimeType 확인
            let jObj = Newtonsoft.Json.Linq.JObject.Parse(json)
            let settings = EmJson.CreateDefaultSettings()
            // TypeFactory를 통해 RuntimeType에 맞는 JSON 타입 찾기
            let njProj =
                getTypeFactory()
                |-> (fun factory ->
                        let runtimeTypeName =
                            match jObj.["RuntimeType"] with
                            | null -> "NjProject"
                            | token -> token.ToString()
                        let njObj = factory.DeserializeJson(runtimeTypeName, json, settings)
                        njObj :?> NjProject)
                |?? (fun () ->                 // TypeFactory가 없으면 기본 NjProject로 역직렬화
                    EmJson.FromJson<NjProject>(json, settings))
            njProj |> tee(_.OnConstructed())

    type Project with // ToJson
        /// DsProject 를 JSON 문자열로 변환
        member x.ToJson():string =
            let njProject = x.ToNjObj() :?> NjProject
            njProject.ToJson()
        member x.ToJson(jsonFilePath:string) =
            let njProject = x.ToNjObj() :?> NjProject
            njProject.ToJsonFile(jsonFilePath)

        /// JSON 문자열을 DsProject 로 변환
        static member internal fromJson(json:string): Project =
            let project =
                json
                |> NjProject.FromJson
                |> getRuntimeObject<Project>        // de-serialization 연결 고리
                |> validateRuntime
                |> tee(_.OnConstructed())
            project

    type NjSystem with // ExportToJson, ExportToJsonFile, ImportFromJson
        /// DsSystem 를 JSON 문자열로 변환
        member x.ExportToJson(): string =
            let settings = EmJson.CreateDefaultSettings()
            settings.DateFormatString <- DateFormatString
            EmJson.ToJson(x, settings)
        member x.ExportToJsonFile(jsonFilePath:string): string =
            x.ExportToJson()
            |> tee(fun json -> File.WriteAllText(jsonFilePath, json))

        /// JSON 문자열을 DsSystem 로 변환
        static member ImportFromJson(json:string): NjSystem = EmJson.FromJson<NjSystem>(json) |> tee(_.OnConstructed())

    type DsSystem with // ExportToJson, FromJson, ImportFromJson
        /// DsSystem 를 JSON 문자열로 변환
        member x.ExportToJson(): string =
            let njSystem = x.ToNj<NjSystem>()
            njSystem.ExportToJson()

        member x.ExportToJson(jsonFilePath:string): string =
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
                |> tee(_.OnConstructed())
            | _ -> // RuntimeType 이 없거나, 잘못된 경우
                failwith "Invalid system JSON file.  'RuntimeType' not found or mismatch."
        static member FromJson(json) = DsSystem.ImportFromJson(json)


    /// IRtUnique 전용 Runtime 객체를 JSON 타입으로 변환
    let rtObj2NjObj (rtObj:IRtUnique): INjUnique =
        let rtObj = rtObj :?> RtUnique

        if isItNull rtObj then
            getNull<NjUnique>()
        else
            let njObj =
                match rtObj with
                | :? Project as p  ->
                    let rt = p
                    NjProject.Create(Database=rt.Database
                        , Author=rt.Author
                        , Version=rt.Version
                        , Description=rt.Description)
                    |> replicateProperties rt
                    |> tee (fun z ->
                        let activeSystems  = rt.ActiveSystems  |-> _.ToNj<NjSystem>() |> toArray
                        let passiveSystems = rt.PassiveSystems |-> _.ToNj<NjSystem>() |> toArray
                        z.Initialize(activeSystems, passiveSystems, rt, isDeserialization=false) |> ignore)
                    |> tee(fun n ->
                        // TypeFactory로 생성된 경우 RuntimeObject가 설정되지 않을 수 있음
                        if not (isItNotNull n.RuntimeObject) then n.RuntimeObject <- rt
                        verify (n.RuntimeObject = rt)) // serialization 연결 고리
                    :> INjUnique

                | :? DsSystem as s ->
                    let rt = s
                    NjSystem.Create(IRI=rt.IRI
                        , Author=rt.Author
                        , LangVersion=rt.LangVersion
                        , EngineVersion=rt.EngineVersion
                        , Description=rt.Description)
                    |> replicateProperties rt
                    |> tee (fun z ->
                        let flows    = rt.Flows    |-> _.ToNj<NjFlow>()   |> toArray
                        let works    = rt.Works    |-> _.ToNj<NjWork>()   |> toArray
                        let arrows   = rt.Arrows   |-> _.ToNj<NjArrow>()  |> toArray
                        let apiDefs  = rt.ApiDefs  |-> _.ToNj<NjApiDef>() |> toArray
                        let apiCalls = rt.ApiCalls |-> _.ToNj<NjApiCall>() |> toArray
                        let buttons    = rt.Buttons    |-> _.ToNj<NjButton>()   |> toArray
                        let lamps      = rt.Lamps      |-> _.ToNj<NjLamp>()     |> toArray
                        let conditions = rt.Conditions |-> _.ToNj<NjCondition>() |> toArray
                        let actions    = rt.Actions    |-> _.ToNj<NjAction>()   |> toArray
                        z.Initialize(flows, works, arrows, apiDefs, apiCalls, buttons, lamps, conditions, actions) |> ignore
                    ) |> tee(fun n -> verify (n.RuntimeObject = rt)) // serialization 연결 고리
                    :> INjUnique

                | :? Flow as f ->
                    let rt = f
                    NjFlow.Create()
                    |> replicateProperties rt
                    |> tee(fun z ->
                        // Flow는 이제 UI 요소를 직접 소유하지 않음
                        z.Initialize() |> ignore)
                    :> INjUnique

                | :? Work as w ->
                    let rt = w
                    NjWork.Create()
                    |> replicateProperties rt
                    |> tee (fun z ->
                        let calls    = rt.Calls   |-> _.ToNj<NjCall>()  |> toArray
                        let arrows   = rt.Arrows  |-> _.ToNj<NjArrow>() |> toArray
                        let flowGuid = rt.Flow |-> (fun flow -> guid2str flow.Guid) |? null
                        z.Initialize(calls, arrows, flowGuid) |> ignore
                        z.Status4 <- rt.Status4)
                    :> INjUnique


                | :? Call as c ->
                    let rt = c
                    let ac = rt.AutoConditions
                    let cc = rt.CommonConditions
                    NjCall.Create()
                    |> replicateProperties rt
                    |> tee (fun z ->
                        let apiCalls = rt.ApiCalls |-> _.Guid |> toArray
                        z.Initialize(rt.CallType.ToString(), apiCalls, ac, cc, rt.IsDisabled, rt.Timeout) |> ignore
                        z.Status4 <- rt.Status4)
                    :> INjUnique



                | :? DsButton    as b -> NjButton.Create()    |> replicateProperties b :> INjUnique
                | :? Lamp        as l -> NjLamp.Create()      |> replicateProperties l :> INjUnique
                | :? DsCondition as d -> NjCondition.Create() |> replicateProperties d :> INjUnique
                | :? DsAction    as a -> NjAction.Create()    |> replicateProperties a :> INjUnique

                | (:? ArrowBetweenWorks | :? ArrowBetweenCalls) ->
                    NjArrow.Create()
                    |> replicateProperties rtObj
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
                    ) :> INjUnique


                | :? ApiCall as ac ->
                    let rt = ac
                    let valueSpec = rt.ValueSpec |-> _.Jsonize() |? null
                    NjApiCall.Create(ApiDef=rt.ApiDefGuid, InAddress=rt.InAddress, OutAddress=rt.OutAddress,
                        InSymbol=rt.InSymbol, OutSymbol=rt.OutSymbol,
                        ValueSpec=valueSpec )
                    |> replicateProperties rt
                    :> INjUnique

                | :? ApiDef as ad -> NjApiDef.Create(IsPush=ad.IsPush) |> replicateProperties ad :> INjUnique

                | _ -> failwith $"Unsupported runtime type: {rtObj.GetType().Name}"
                :?> NjUnique

            njObj.RuntimeObject <- rtObj // serialization 연결 고리
            onNsJsonSerializing njObj
            njObj.OnConstructed()
            njObj
