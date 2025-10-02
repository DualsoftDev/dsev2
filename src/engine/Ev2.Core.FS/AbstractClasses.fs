namespace Ev2.Core.FS

open System
open System.Linq
open System.Data
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base
open Dual.Common.Db.FS

(*
    Entity base classes : XXXEntity 는 XXX 내에 복수개로 존재할 수 있는 것들을 나타냄.
    e.g DsButton, Lamp, DsCondition, DsAction 등은 DsSystem 내에 복수개로 존재할 수 있고, DsSystemEntity 를 상속 받으며, systemEntity table 에 복수개로 저장한다.

    단수개만 존재하는 경우는, DB table 의 column 속성으로 저장됨.  e.g DsSystemProperties
 *)

[<AbstractClass>]
type ProjectEntity() =
    inherit RtUnique()
    member x.Project = x.RawParent >>= tryCast<Project>

/// DsSystem 객체에 포함되는 member 들이 상속할 base class.  e.g RtFlow, RtWork, RtArrowBetweenWorks, RtApiDef, RtApiCall
and [<AbstractClass>] DsSystemEntity() =
    inherit RtUnique()
    interface ISystemEntity
    member x.System  = x.RawParent >>= tryCast<DsSystem>
    member x.Project = x.RawParent >>= _.RawParent >>= tryCast<Project>

and [<AbstractClass>] FlowEntity() =
    inherit RtUnique()
    member x.Flow    = x.RawParent >>= tryCast<Flow>
    member x.System  = x.RawParent >>= _.RawParent >>= tryCast<DsSystem>
    member x.Project = x.RawParent >>= _.RawParent>>= _.RawParent >>= tryCast<Project>

and [<AbstractClass>] WorkEntity() =
    inherit RtUnique()
    member x.Work    = x.RawParent >>= tryCast<Work>
    member x.System  = x.RawParent >>= _.RawParent >>= tryCast<DsSystem>
    member x.Project = x.RawParent >>= _.RawParent>>= _.RawParent >>= tryCast<Project>

and [<AbstractClass>] CallEntity() =
    inherit RtUnique()
    member x.Call    = x.RawParent >>= tryCast<Call>
    member x.Work    = x.RawParent >>= _.RawParent >>= tryCast<Work>
    member x.System  = x.RawParent >>= _.RawParent >>= _.RawParent >>= tryCast<DsSystem>
    member x.Project = x.RawParent >>= _.RawParent >>= _.RawParent >>= _.RawParent >>= tryCast<Project>


// Main domain types
/// Call 간 화살표 연결.  Work 내에 존재
and ArrowBetweenCalls(sourceGuid:Guid, targetGuid:Guid, typ:DbArrowType) = // Create
    inherit WorkEntity()
    member private x.getCallByGuid (guid:Guid) =
        x.Work |-> _.Calls >>= tryFind (fun (c:Call) -> c.Guid = guid) |> Option.get

    new() = new ArrowBetweenCalls(emptyGuid, emptyGuid, DbArrowType.None)
    static member Create(sourceGuid:Guid, targetGuid:Guid, typ:DbArrowType) =
        createExtended<ArrowBetweenCalls>()
        |> tee(fun z ->
            z.XSourceGuid <- sourceGuid
            z.XTargetGuid <- targetGuid
            z.Type <- typ )

    static member Create(source:Call, target:Call, typ:DbArrowType) =
        ArrowBetweenCalls.Create(source.Guid, target.Guid, typ)

    interface IRtArrow
    member val XSourceGuid = sourceGuid with get, set
    member val XTargetGuid = targetGuid with get, set
    member val Type = typ with get, set

    member x.Source = x.getCallByGuid(x.XSourceGuid)
    member x.Target = x.getCallByGuid(x.XTargetGuid)
    member x.XTypeId:Id = DbApi.GetEnumId x.Type

/// Work 간 화살표 연결.  System 내에 존재
and ArrowBetweenWorks(sourceGuid:Guid, targetGuid:Guid, typ:DbArrowType) = // Create
    inherit DsSystemEntity()

    member private x.getWorkByGuid (guid:Guid) =
        x.System |-> _.Works >>= tryFind (fun (w:Work) -> w.Guid = guid) |> Option.get

    new() = new ArrowBetweenWorks(emptyGuid, emptyGuid, DbArrowType.None)

    static member Create(sourceGuid:Guid, targetGuid:Guid, typ:DbArrowType) =
        createExtended<ArrowBetweenWorks>()
        |> tee(fun z ->
            z.XSourceGuid <- sourceGuid
            z.XTargetGuid <- targetGuid
            z.Type <- typ )
    static member Create(source:Work, target:Work, typ:DbArrowType) =
        ArrowBetweenWorks.Create(source.Guid, target.Guid, typ)

    interface IRtArrow
    member val XSourceGuid = sourceGuid with get, set
    member val XTargetGuid = targetGuid with get, set
    member val Type = typ with get, set

    member x.Source = x.getWorkByGuid(x.XSourceGuid)
    member x.Target = x.getWorkByGuid(x.XTargetGuid)
    member x.XTypeId:Id = DbApi.GetEnumId x.Type
    //member val TypeId:Id = DbApi.GetEnumId typ with get, set

and Project() as this = // Create, Initialize, OnSaved, OnLoaded
    inherit RtUnique()

    interface IRtProject with
        member x.DateTime
            with get() = x.Properties.DateTime
            and set v = x.Properties.DateTime <- v
    interface IParameterContainer

    member val DbApi = Option<DbApi>.None with get, set

    // { JSON 용
    member val internal RawActiveSystems    = ResizeArray<DsSystem>() with get, set
    member val internal RawPassiveSystems   = ResizeArray<DsSystem>() with get, set
    // { Runtime/DB 용
    member x.ActiveSystems  = x.RawActiveSystems  |> toList
    member x.PassiveSystems = x.RawPassiveSystems |> toList
    /// Project 내의 systems: 참조되는 PasssiveSystems 을 먼저 배치
    member x.Systems = (x.PassiveSystems @ x.ActiveSystems) |> toList
    // } Runtime/DB 용

    member val Properties    = ProjectProperties.Create(this) with get, set
    member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
    member x.PropertiesJson
        with get() = x.Properties.ToJson()
        and set (json:string) =
            x.Properties <- DsPropertiesHelper.assignFromJson x (fun () -> ProjectProperties.Create(this)) json

    static member Create() = createExtended<Project>()

    /// Creates a Project with the specified systems using parameterless constructor + Initialize pattern
    static member Create(activeSystems: DsSystem seq, passiveSystems: DsSystem seq, njProject:INjProject) =
        let x = createExtended<Project>()
        // Clear existing systems
        x.RawActiveSystems.Clear()
        x.RawPassiveSystems.Clear()

        // Add new systems and set parent relationships
        activeSystems |> iter (fun s ->
            x.RawActiveSystems.Add(s)
            setParentI x s)
        passiveSystems |> iter (fun s ->
            x.RawPassiveSystems.Add(s)
            setParentI x s)
        x

    abstract OnSaved : IDbConnection * IDbTransaction  -> unit
    /// DB 저장 직후에 호출되는 메서드
    default _.OnSaved(conn:IDbConnection, tr:IDbTransaction) = ()

    abstract member OnLoaded: unit -> unit
    /// Runtime 객체 생성 및 속성 다 채운 후, validation 수행.  (필요시 추가 작업 수행)
    default x.OnLoaded() = ()

    abstract OnLoaded : IDbConnection * IDbTransaction  -> unit
    /// DB load 이후에 호출되는 메서드
    default _.OnLoaded(conn:IDbConnection, tr:IDbTransaction) = ()

    static member FromJson(json:string): Project =
        fwdProjectFromJson json :?> Project

and DsSystem() as this = // Create
    inherit ProjectEntity()

    member val PolymorphicJsonEntities = PolymorphicJsonCollection<JsonPolymorphic>() with get, set
    member x.Entities = x.PolymorphicJsonEntities.Items
    member x.AddEntitiy(entity:JsonPolymorphic) = entity.RawParent <- Some x;  x.PolymorphicJsonEntities.AddItem entity//; x.UpdateDateTime()
    member x.AddEntities(entities:JsonPolymorphic seq) = entities |> iter x.AddEntitiy
    member x.RemoveEntitiy(entity:JsonPolymorphic) = entity.RawParent <- None; x.PolymorphicJsonEntities.RemoveItem entity
    member x.Buttons    = x.Entities.OfType<DsButton>()    |> toArray
    member x.Lamps      = x.Entities.OfType<Lamp>()        |> toArray
    member x.Conditions = x.Entities.OfType<DsCondition>() |> toArray
    member x.Actions    = x.Entities.OfType<DsAction>()    |> toArray

    member val Properties = DsSystemProperties.Create(this) with get, set
    member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
    member x.PropertiesJson
        with get() = x.Properties.ToJson()
        and set (json:string) =
            x.Properties <- DsPropertiesHelper.assignFromJson x (fun () -> DsSystemProperties.Create(this)) json

    (* RtSystem.Name 은 prototype 인 경우, prototype name 을, 아닌 경우 loaded system name 을 의미한다. *)
    interface IParameterContainer
    interface IRtSystem with
        member x.DateTime
            with get() = x.Properties.DateTime
            and set v = x.Properties.DateTime <- v
    member val internal RawFlows    = ResizeArray<Flow>() with get, set
    member val internal RawWorks    = ResizeArray<Work>() with get, set
    member val internal RawArrows   = ResizeArray<ArrowBetweenWorks>() with get, set
    member val internal RawApiDefs  = ResizeArray<ApiDef>() with get, set
    member val internal RawApiCalls = ResizeArray<ApiCall>() with get, set

    member x.OwnerProjectId = x.Project >>= (fun p -> if p.ActiveSystems.Contains(x) then p.Id else None)

    member val IRI           = nullString with get, set
    (*** 기존 직행 접근자는 Properties 로 마이그레이션 완료 후 주석 처리합니다. 필요 시 x.Properties.Author 등으로 직접 접근하세요.
    member x.Author
        with get() = x.Properties.Author
        and set value = x.Properties.Author <- value
    member x.EngineVersion
        with get() = x.Properties.EngineVersion
        and set value =
            let v = value |> Option.ofObj |? Version()
            x.Properties.EngineVersion <- v
    member x.LangVersion
        with get() = x.Properties.LangVersion
        and set value =
            let v = value |> Option.ofObj |? Version()
            x.Properties.LangVersion <- v
    member x.Description
        with get() = x.Properties.Description
        and set value = x.Properties.Description <- value
    /// DateTime: 메모리에 최초 객체 생성시 생성
    member x.DateTime
        with get() = x.Properties.DateTime
        and set value = x.Properties.DateTime <- value
    ***)

    member x.Flows      = x.RawFlows      |> toList
    member x.Works      = x.RawWorks      |> toList
    member x.Arrows     = x.RawArrows     |> toList
    member x.ApiDefs    = x.RawApiDefs    |> toList
    member x.ApiCalls   = x.RawApiCalls   |> toList

    abstract member OnLoaded: unit -> unit
    /// Runtime 객체 생성 및 속성 다 채운 후, validation 수행.  (필요시 추가 작업 수행)
    default x.OnLoaded() = ()


    static member Create() = createExtended<DsSystem>()

    /// Creates a DsSystem with the specified components using parameterless constructor + Initialize pattern
    static member Create(flows: Flow seq, works: Work seq, arrows: ArrowBetweenWorks seq, apiDefs: ApiDef seq, apiCalls: ApiCall seq) =
        let system = createExtended<DsSystem>()
        // Clear existing components
        system.RawFlows.Clear()
        system.RawWorks.Clear()
        system.RawArrows.Clear()
        system.RawApiDefs.Clear()
        system.RawApiCalls.Clear()

        // Add new components and set parent relationships
        flows      |> iter (fun f -> system.RawFlows     .Add(f); setParentI system f)
        works      |> iter (fun w -> system.RawWorks     .Add(w); setParentI system w)
        arrows     |> iter (fun a -> system.RawArrows    .Add(a); setParentI system a)
        apiDefs    |> iter (fun d -> system.RawApiDefs   .Add(d); setParentI system d)
        apiCalls   |> iter (fun c -> system.RawApiCalls  .Add(c); setParentI system c)

        system



and Flow() as this = // Create
    inherit DsSystemEntity()

    interface IRtFlow

    member x.System = x.RawParent >>= tryCast<DsSystem>

    member val Properties = FlowProperties.Create(this) with get, set
    member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
    member x.PropertiesJson
        with get() = x.Properties.ToJson()
        and set (json:string) =
            x.Properties <- assignFromJson x (fun () -> FlowProperties.Create this) json

    member x.Buttons    = x.System |-> (fun s -> s.Buttons    |> filter (fun b -> b.Flows |> Seq.contains x)) |? [||]
    member x.Lamps      = x.System |-> (fun s -> s.Lamps      |> filter (fun l -> l.Flows.Contains x)) |? [||]
    member x.Conditions = x.System |-> (fun s -> s.Conditions |> filter (fun c -> c.Flows.Contains x)) |? [||]
    member x.Actions    = x.System |-> (fun s -> s.Actions    |> filter (fun a -> a.Flows.Contains x)) |? [||]

    member x.Works:Work[] =
        x.System
        |-> (fun s ->
            s.Works
            |> filter (fun w -> w.Flow = Some x)
            |> toArray)
        |? [||]

    static member Create() = createExtended<Flow>()


and DsButton() = // Create
    inherit BLCABase()

    interface IRtButton
    static member Create() = createExtended<DsButton>()

and Lamp() = // Create
    inherit BLCABase()

    interface IRtLamp
    static member Create() = createExtended<Lamp>()

and DsCondition() = // Create
    inherit BLCABase()

    interface IRtCondition
    static member Create() = createExtended<DsCondition>()

and DsAction() = // Create
    inherit BLCABase()

    interface IRtAction
    static member Create() = createExtended<DsAction>()



// see static member Create
and Work() as this = // Create
    inherit DsSystemEntity()

    interface IRtWork
    member val internal RawCalls  = ResizeArray<Call>() with get, set
    member val internal RawArrows = ResizeArray<ArrowBetweenCalls>() with get, set

    member val Motion       = nullString with get, set
    member val Script       = nullString with get, set
    member val ExternalStart = nullString with get, set
    member val IsFinished   = false      with get, set
    member val NumRepeat    = 0          with get, set
    member val Period       = 0          with get, set
    member val Delay        = 0          with get, set

    member val Status4 = Option<DbStatus4>.None with get, set

    member val Properties = WorkProperties.Create(this) with get, set
    member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
    member x.PropertiesJson
        with get() = x.Properties.ToJson()
        and set (json:string) =
            x.Properties <- assignFromJson x (fun () -> WorkProperties.Create this) json

    member x.Calls  = x.RawCalls  |> toList
    member x.Arrows = x.RawArrows |> toList

    member val FlowGuid = noneGuid with get, set
    member val FlowId = Option<Id>.None with get, set
    member x.Flow:Flow option =
        match x.FlowId with
        | Some id -> x.System |-> _.Flows >>= tryFind(fun f -> f.Id = Some id)
        | None -> x.System |-> _.Flows >>= tryFind(fun f -> (Some f.Guid) = x.FlowGuid)

    static member Create() = createExtended<Work>()
    static member Create(calls: Call seq, arrows: ArrowBetweenCalls seq, flowGuid: Guid option) =
        let work = createExtended<Work>()
        // Set flow
        work.FlowGuid <- flowGuid

        // Add new components and set parent relationships
        calls  |> iter (fun c -> work.RawCalls. Add(c); setParentI work c)
        arrows |> iter (fun a -> work.RawArrows.Add(a); setParentI work a)

        work

and ApiCallValueSpecs(specs:IApiCallValueSpec seq) =
    inherit ResizeArray<IApiCallValueSpec>(specs)
    new() = ApiCallValueSpecs([])

// see static member Create
and Call() as this = // Create
    inherit WorkEntity()

    interface IRtCall
    member val CallType         = DbCallType.Normal      with get, set    // 호출 유형 (예: "Normal", "Parallel", "Repeat")
    member val IsDisabled       = false                  with get, set
    member val Timeout          = Option<int>.None       with get, set    // 실행 타임아웃(ms)
    member val AutoConditions   = ApiCallValueSpecs()    with get, set    // 사전 조건 식 (자동 실행 조건)
    member val CommonConditions = ApiCallValueSpecs()    with get, set    // 안전 조건 식 (실행 보호조건)
    member val ApiCallGuids     = ResizeArray<Guid>()    with get, set    // DB 저장시에는 callId 로 저장
    member val Status4          = Option<DbStatus4>.None with get, set

    member x.ApiCalls =
        match (x.RawParent >>= _.RawParent) with
        | Some parent ->
            match parent with
            | :? DsSystem as sys ->
                sys.ApiCalls |> filter(fun ac -> x.ApiCallGuids |> contains ac.Guid ) |> toList
            | _ -> []  // NjSystem 등 다른 타입인 경우 빈 리스트 반환
        | None -> []

    static member Create() = createExtended<Call>()

    /// Creates a Call with the specified parameters using parameterless constructor + Initialize pattern
    static member Create(callType: DbCallType, apiCallGuids: Guid seq, autoConditions: ApiCallValueSpecs, commonConditions: ApiCallValueSpecs, isDisabled: bool, timeout: int option) =
        let call = createExtended<Call>()
        // Set call properties
        call.CallType   <- callType
        call.IsDisabled <- isDisabled
        call.Timeout    <- timeout
        call.AutoConditions <- autoConditions
        call.CommonConditions <- commonConditions

        apiCallGuids     |> iter call.ApiCallGuids.Add

        call

    member val Properties = CallProperties.Create(this) with get, set
    member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
    member x.PropertiesJson
        with get() = x.Properties.ToJson()
        and set (json:string) =
            x.Properties <- assignFromJson x (fun () -> CallProperties.Create this) json


and ApiCall(apiDefGuid:Guid, inAddress:string, outAddress:string, // Create, Callers, ApiDef
    inSymbol:string, outSymbol:string,
    valueSpec:IValueSpec option
) as this =
    inherit DsSystemEntity()

    new() = new ApiCall(emptyGuid, nullString, nullString, nullString, nullString, Option<IValueSpec>.None)

    static member Create() = createExtended<ApiCall>()

    interface IRtApiCall
    member val ApiDefGuid = apiDefGuid  with get, set
    member val InAddress  = inAddress   with get, set
    member val OutAddress = outAddress  with get, set
    member val InSymbol   = inSymbol    with get, set
    member val OutSymbol  = outSymbol   with get, set

    member val ValueSpec = valueSpec with get, set
    member val IOTags = IOTagsWithSpec() with get, set
    member x.IOTagsJson = IOTagsWithSpec.Jsonize x.IOTags

    member val Properties = ApiCallProperties.Create this with get, set
    member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
    member x.PropertiesJson
        with get() = x.Properties.ToJson()
        and set (json:string) =
            x.Properties <- assignFromJson x (fun () -> ApiCallProperties.Create this) json

    /// system 에서 현재 ApiCall 을 호출하는 Call 들
    member x.Callers:Call[] =
        x.System
        |-> (fun s ->
            s.Works >>= _.Calls
            |> filter (fun c -> c.ApiCalls.Contains x)
            |> toArray)
        |? [||]


    [<JsonIgnore>]
    member x.ApiDef
        with get() =
            x.Project
            |-> (fun proj ->
                    fwdEnumerateRtObjects proj
                    |> _.OfType<ApiDef>()
                    |> tryFind (fun ad -> ad.Guid = x.ApiDefGuid )
                    |?? (fun () -> failwith $"ApiDef with Guid {x.ApiDefGuid} not found in System") )
            |?? (fun () -> failwith "Parent is not DsSystem type")
        and set (v:ApiDef) = x.ApiDefGuid <- v.Guid


and ApiDef(isPush:bool, txGuid:Guid, rxGuid:Guid) as this = // Create, ApiUsers
    inherit DsSystemEntity()

    let getWork (system:DsSystem option) (guid:Guid): Work =
        system >>= (fun s -> s.Works |> tryFind (fun w -> w.Guid = guid))
        |? getNull<Work>()

    new() = new ApiDef(true, emptyGuid, emptyGuid)

    static member Create() = createExtended<ApiDef>()

    interface IRtApiDef
    member val IsPush = isPush with get, set

    member val TxGuid = txGuid with get, set
    member val RxGuid = rxGuid with get, set

    member x.TX: Work = getWork x.System x.TxGuid
    member x.RX: Work = getWork x.System x.RxGuid

    member x.Period = x.TX.Period


    member x.System = x.RawParent >>= tryCast<DsSystem>

    member val Properties = ApiDefProperties.Create this with get, set
    member x.PropertiesJsonB = x.PropertiesJson |> JsonbString
    member x.PropertiesJson
        with get() = x.Properties.ToJson()
        and set (json:string) =
            x.Properties <- assignFromJson x (fun () -> ApiDefProperties.Create this) json

    // system 에서 현재 ApiDef 을 사용하는 ApiCall 들
    member x.ApiUsers:ApiCall[] =
        x.System
        |-> (fun s ->
                s.ApiCalls
                |> filter (fun c ->
                    try c.ApiDef = x
                    with _ -> false)  // ApiDef 접근 실패 시 false
                |> toArray)
        |? [||]
