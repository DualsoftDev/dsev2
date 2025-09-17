namespace Ev2.Core.FS

open System
open System.Linq
open System.Data

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Newtonsoft.Json


[<AbstractClass>]
type RtUnique() = // ToNjObj, ToNj
    inherit Unique()
    interface IRtUnique

    abstract member ToNjObj : unit -> INjUnique
    /// Runtime 객체를 Newtonsoft JSON 객체로 변환
    default x.ToNjObj() = fwdRtObj2NjObj x

    member x.ToNj<'T when 'T :> INjUnique>() : 'T = x.ToNjObj() :?> 'T

// Entity base classes
[<AbstractClass>]
type ProjectEntity() = // Actions, ActiveSystems, ApiCalls, ApiDefs, ApiUsers, Arrows, Buttons, Call, Callers, Calls, Conditions, Create, Flow, Flows, Initialize, Lamps, OwnerProjectId, PassiveSystems, Project, System, Systems, Work, Works
    inherit RtUnique()
    member x.Project = x.RawParent >>= tryCast<Project>

/// DsSystem 객체에 포함되는 member 들이 상속할 base class.  e.g RtFlow, RtWork, RtArrowBetweenWorks, RtApiDef, RtApiCall
and [<AbstractClass>] DsSystemEntity() =
    inherit RtUnique()
    interface ISystemEntity
    member x.System  = x.RawParent >>= tryCast<DsSystem>
    member x.Project = x.RawParent >>= _.RawParent >>= tryCast<Project>

and [<AbstractClass>] DsSystemEntityWithFlow() =
    inherit DsSystemEntity()
    interface ISystemEntityWithFlow
    member val FlowGuid = noneGuid with get, set
    member val FlowId = Option<Id>.None with get, set
    member x.Flow:Flow option =
        match x.FlowId with
        | Some id -> x.System |-> _.Flows >>= tryFind(fun f -> f.Id = Some id)
        | None -> x.System |-> _.Flows >>= tryFind(fun f -> (Some f.Guid) = x.FlowGuid)

/// Button, Lamp, Condition, Action
and [<AbstractClass>] BLCA() =
    inherit DsSystemEntityWithFlow()
    interface IWithTagWithSpecs
    member val IOTags = IOTagsWithSpec() with get, set
    member x.IOTagsJson = IOTagsWithSpec.Jsonize x.IOTags

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

and Project() = // Create, Initialize, OnSaved, OnLoaded
    inherit RtUnique()

    static member Create() = createExtended<Project>()

    /// Creates a Project with the specified systems using parameterless constructor + Initialize pattern
    static member Create(activeSystems: DsSystem seq, passiveSystems: DsSystem seq, njProject:INjProject) =
        let project = createExtended<Project>()
        project.Initialize(activeSystems, passiveSystems, njProject)


    interface IRtProject with
        member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v
    interface IParameterContainer

    // { JSON 용
    /// 마지막 저장 db 에 대한 connection string
    member val Database = getNull<DbProvider>() with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨
    member val AasxPath = nullString with get, set // AASX 파일 경로.

    member val Author        = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
    member val Version       = Version()  with get, set
    //member val LangVersion   = langVersion   |? Version()  with get, set
    //member val EngineVersion = engineVersion |? Version()  with get, set
    member val Description   = nullString with get, set

    /// DateTime: 메모리에 최초 객체 생성시 생성
    member val DateTime = now().TruncateToSecond() with get, set

    member val internal RawActiveSystems    = ResizeArray<DsSystem>() with get, set
    member val internal RawPassiveSystems   = ResizeArray<DsSystem>() with get, set
    // { Runtime/DB 용
    member x.ActiveSystems  = x.RawActiveSystems  |> toList
    member x.PassiveSystems = x.RawPassiveSystems |> toList
    /// Project 내의 systems: 참조되는 PasssiveSystems 을 먼저 배치
    member x.Systems = (x.PassiveSystems @ x.ActiveSystems) |> toList
    // } Runtime/DB 용

    member x.Initialize(activeSystems: DsSystem seq, passiveSystems: DsSystem seq, njProj:INjProject): Project =
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
    default this.OnSaved(conn:IDbConnection, tr:IDbTransaction) = ()

    abstract member OnLoaded: unit -> unit
    /// Runtime 객체 생성 및 속성 다 채운 후, validation 수행.  (필요시 추가 작업 수행)
    default x.OnLoaded() = ()

    abstract OnLoaded : IDbConnection * IDbTransaction  -> unit
    /// DB load 이후에 호출되는 메서드
    default this.OnLoaded(conn:IDbConnection, tr:IDbTransaction) = ()

    static member FromJson(json:string): Project = fwdProjectFromJson json :?> Project


and DsSystem() = // Create
    inherit ProjectEntity()

    (* RtSystem.Name 은 prototype 인 경우, prototype name 을, 아닌 경우 loaded system name 을 의미한다. *)
    interface IParameterContainer
    interface IRtSystem with
        member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v
    member val internal RawFlows    = ResizeArray<Flow>() with get, set
    member val internal RawWorks    = ResizeArray<Work>() with get, set
    member val internal RawArrows   = ResizeArray<ArrowBetweenWorks>() with get, set
    member val internal RawApiDefs  = ResizeArray<ApiDef>() with get, set
    member val internal RawApiCalls = ResizeArray<ApiCall>() with get, set
    member val internal RawButtons    = ResizeArray<DsButton>() with get, set
    member val internal RawLamps      = ResizeArray<Lamp>() with get, set
    member val internal RawConditions = ResizeArray<DsCondition>() with get, set
    member val internal RawActions    = ResizeArray<DsAction>() with get, set

    member x.OwnerProjectId = x.Project >>= (fun p -> if p.ActiveSystems.Contains(x) then p.Id else None)

    member val IRI           = nullString with get, set
    member val Author        = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
    member val EngineVersion = Version()  with get, set
    member val LangVersion   = Version()  with get, set
    member val Description   = nullString with get, set
    /// DateTime: 메모리에 최초 객체 생성시 생성
    member val DateTime      = now().TruncateToSecond() with get, set

    member x.Flows      = x.RawFlows      |> toList
    member x.Works      = x.RawWorks      |> toList
    member x.Arrows     = x.RawArrows     |> toList
    member x.ApiDefs    = x.RawApiDefs    |> toList
    member x.ApiCalls   = x.RawApiCalls   |> toList
    member x.Buttons    = x.RawButtons    |> toList
    member x.Lamps      = x.RawLamps      |> toList
    member x.Conditions = x.RawConditions |> toList
    member x.Actions    = x.RawActions    |> toList

    abstract member OnLoaded: unit -> unit
    /// Runtime 객체 생성 및 속성 다 채운 후, validation 수행.  (필요시 추가 작업 수행)
    default x.OnLoaded() = ()


    static member Create() = createExtended<DsSystem>()

    /// Creates a DsSystem with the specified components using parameterless constructor + Initialize pattern
    static member Create(flows: Flow seq, works: Work seq, arrows: ArrowBetweenWorks seq, apiDefs: ApiDef seq, apiCalls: ApiCall seq,
                         buttons: DsButton seq, lamps: Lamp seq, conditions: DsCondition seq, actions: DsAction seq) =
        let system = createExtended<DsSystem>()
        // Clear existing components
        system.RawFlows.Clear()
        system.RawWorks.Clear()
        system.RawArrows.Clear()
        system.RawApiDefs.Clear()
        system.RawApiCalls.Clear()
        system.RawButtons.Clear()
        system.RawLamps.Clear()
        system.RawConditions.Clear()
        system.RawActions.Clear()

        // Add new components and set parent relationships
        flows      |> iter (fun f -> system.RawFlows     .Add(f); setParentI system f)
        works      |> iter (fun w -> system.RawWorks     .Add(w); setParentI system w)
        arrows     |> iter (fun a -> system.RawArrows    .Add(a); setParentI system a)
        apiDefs    |> iter (fun d -> system.RawApiDefs   .Add(d); setParentI system d)
        apiCalls   |> iter (fun c -> system.RawApiCalls  .Add(c); setParentI system c)
        buttons    |> iter (fun b -> system.RawButtons   .Add(b); setParentI system b)
        lamps      |> iter (fun l -> system.RawLamps     .Add(l); setParentI system l)
        conditions |> iter (fun c -> system.RawConditions.Add(c); setParentI system c)
        actions    |> iter (fun a -> system.RawActions   .Add(a); setParentI system a)

        system



and Flow() = // Create
    inherit DsSystemEntity()

    interface IRtFlow

    member x.System = x.RawParent >>= tryCast<DsSystem>

    member x.Buttons    = x.System |-> (fun s -> s.Buttons    |> filter (fun b -> b.Flow = Some x)) |? []
    member x.Lamps      = x.System |-> (fun s -> s.Lamps      |> filter (fun l -> l.Flow = Some x)) |? []
    member x.Conditions = x.System |-> (fun s -> s.Conditions |> filter (fun c -> c.Flow = Some x)) |? []
    member x.Actions    = x.System |-> (fun s -> s.Actions    |> filter (fun a -> a.Flow = Some x)) |? []

    member x.Works:Work[] =
        x.System
        |-> (fun s ->
            s.Works
            |> filter (fun w -> w.Flow = Some x)
            |> toArray)
        |? [||]

    static member Create() = createExtended<Flow>()

and DsButton() = // Create
    inherit BLCA()

    interface IRtButton
    static member Create() = createExtended<DsButton>()

and Lamp() = // Create
    inherit BLCA()

    interface IRtLamp
    static member Create() = createExtended<Lamp>()

and DsCondition() = // Create
    inherit BLCA()

    interface IRtCondition
    static member Create() = createExtended<DsCondition>()

and DsAction() = // Create
    inherit BLCA()

    interface IRtAction
    static member Create() = createExtended<DsAction>()


// see static member Create
and Work() = // Create
    inherit DsSystemEntityWithFlow()

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

    member x.Calls  = x.RawCalls  |> toList
    member x.Arrows = x.RawArrows |> toList

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
and Call() = // Create
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


and ApiCall(apiDefGuid:Guid, inAddress:string, outAddress:string, // Create, Callers, ApiDef
    inSymbol:string, outSymbol:string,
    valueSpec:IValueSpec option
) =
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


and ApiDef(isPush:bool, txGuid:Guid, rxGuid:Guid) = // Create, ApiUsers
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

