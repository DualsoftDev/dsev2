namespace Ev2.Core.FS

open System
open System.Linq

open Dual.Common.Base
open Dual.Common.Core.FS
open System.Collections.Generic
open Dual.Common.Db.FS
open Newtonsoft.Json
open System.Data


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

// Internal helper class
and internal Arrow<'T when 'T :> Unique>(source:'T, target:'T, typ:DbArrowType) =
    member val Source = source with get, set
    member val Target = target with get, set
    member val Type = typ with get, set



// Main domain types
/// Call 간 화살표 연결.  Work 내에 존재
and ArrowBetweenCalls() = // Create
    inherit WorkEntity()
    let mutable arrow : Arrow<Call> option = None
    member private x.Initialize(source:Call, target:Call, typ:DbArrowType) =
        arrow <- Some (Arrow<Call>(source, target, typ))

    static member Create(source:Call, target:Call, typ:DbArrowType) =
        let instance = createExtended<ArrowBetweenCalls>()
        instance.Initialize(source, target, typ)
        instance

    interface IRtArrow
    member x.Source with get() = arrow.Value.Source and set v = arrow.Value.Source <- v
    member x.Target with get() = arrow.Value.Target and set v = arrow.Value.Target <- v
    member x.Type   with get() = arrow.Value.Type   and set v = arrow.Value.Type <- v

/// Work 간 화살표 연결.  System 내에 존재
and ArrowBetweenWorks() = // Create
    inherit DsSystemEntity()

    let mutable arrow : Arrow<Work> option = None
    member private x.Initialize(source:Work, target:Work, typ:DbArrowType) =
        arrow <- Some (Arrow<Work>(source, target, typ))

    static member Create(source:Work, target:Work, typ:DbArrowType) =
        let instance = createExtended<ArrowBetweenWorks>()
        instance.Initialize(source, target, typ)
        instance


    interface IRtArrow
    member x.Source with get() = arrow.Value.Source and set v = arrow.Value.Source <- v
    member x.Target with get() = arrow.Value.Target and set v = arrow.Value.Target <- v
    member x.Type   with get() = arrow.Value.Type   and set v = arrow.Value.Type <- v

and Project() = // Create, Initialize, OnAfterSave, OnAfterLoad
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
    member x.Systems = (x.ActiveSystems @ x.PassiveSystems) |> toList
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


    abstract OnAfterSave : IDbConnection * IDbTransaction  -> unit
    /// DB 저장 직후에 호출되는 메서드
    default this.OnAfterSave(conn:IDbConnection, tr:IDbTransaction) = ()

    abstract OnAfterLoad : IDbConnection * IDbTransaction  -> unit
    /// DB load 이후에 호출되는 메서드
    default this.OnAfterLoad(conn:IDbConnection, tr:IDbTransaction) = ()

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

    member x.OwnerProjectId = x.Project >>= (fun p -> if p.ActiveSystems.Contains(x) then p.Id else None)

    member val IRI           = nullString with get, set
    member val Author        = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
    member val EngineVersion = Version()  with get, set
    member val LangVersion   = Version()  with get, set
    member val Description   = nullString with get, set
    /// DateTime: 메모리에 최초 객체 생성시 생성
    member val DateTime      = now().TruncateToSecond() with get, set

    member x.Flows    = x.RawFlows    |> toList
    member x.Works    = x.RawWorks    |> toList
    member x.Arrows   = x.RawArrows   |> toList
    member x.ApiDefs  = x.RawApiDefs  |> toList
    member x.ApiCalls = x.RawApiCalls |> toList

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
        flows |> iter (fun f -> system.RawFlows.Add(f); setParentI system f)
        works |> iter (fun w -> system.RawWorks.Add(w); setParentI system w)
        arrows |> iter (fun a -> system.RawArrows.Add(a); setParentI system a)
        apiDefs |> iter (fun d -> system.RawApiDefs.Add(d); setParentI system d)
        apiCalls |> iter (fun c -> system.RawApiCalls.Add(c); setParentI system c)

        system

    ///// Initialize method for parameterless constructor + Initialize pattern (virtual)
    //abstract member Initialize : Flow seq * Work seq * ArrowBetweenWorks seq * ApiDef seq * ApiCall seq -> DsSystem
    //default this.Initialize(flows: Flow seq, works: Work seq, arrows: ArrowBetweenWorks seq, apiDefs: ApiDef seq, apiCalls: ApiCall seq) =
    //    // Clear existing components
    //    this.RawFlows.Clear()
    //    this.RawWorks.Clear()
    //    this.RawArrows.Clear()
    //    this.RawApiDefs.Clear()
    //    this.RawApiCalls.Clear()

    //    // Add new components and set parent relationships
    //    flows |> iter (fun f -> this.RawFlows.Add(f); setParentI this f)
    //    works |> iter (fun w -> this.RawWorks.Add(w); setParentI this w)
    //    arrows |> iter (fun a -> this.RawArrows.Add(a); setParentI this a)
    //    apiDefs |> iter (fun d -> this.RawApiDefs.Add(d); setParentI this d)
    //    apiCalls |> iter (fun c -> this.RawApiCalls.Add(c); setParentI this c)

    //    this


and Flow() = // Create
    inherit DsSystemEntity()

    interface IRtFlow
    member val internal RawButtons    = ResizeArray<DsButton>() with get, set
    member val internal RawLamps      = ResizeArray<Lamp>() with get, set
    member val internal RawConditions = ResizeArray<DsCondition>() with get, set
    member val internal RawActions    = ResizeArray<DsAction>() with get, set

    member x.System = x.RawParent >>= tryCast<DsSystem>

    member x.Buttons    = x.RawButtons    |> toList
    member x.Lamps      = x.RawLamps      |> toList
    member x.Conditions = x.RawConditions |> toList
    member x.Actions    = x.RawActions    |> toList

    member x.Works:Work[] =
        x.System
        |-> (fun s ->
            s.Works
            |> filter (fun w -> w.Flow = Some x)
            |> toArray)
        |? [||]

    static member Create() = createExtended<Flow>()
    static member Create(buttons: DsButton seq, lamps: Lamp seq, conditions: DsCondition seq, actions: DsAction seq) =
        let flow = createExtended<Flow>()
        // Add new components and set parent relationships
        buttons    |> iter (fun z -> flow.RawButtons   .Add(z); z.RawParent <- Some flow)
        lamps      |> iter (fun z -> flow.RawLamps     .Add(z); z.RawParent <- Some flow)
        conditions |> iter (fun z -> flow.RawConditions.Add(z); z.RawParent <- Some flow)
        actions    |> iter (fun z -> flow.RawActions   .Add(z); z.RawParent <- Some flow)

        flow

and DsButton() = // Create
    inherit FlowEntity()

    interface IRtButton
    static member Create() = createExtended<DsButton>()

and Lamp() = // Create
    inherit FlowEntity()

    interface IRtLamp
    static member Create() = createExtended<Lamp>()

and DsCondition() = // Create
    inherit FlowEntity()

    interface IRtCondition
    static member Create() = createExtended<DsCondition>()

and DsAction() = // Create
    inherit FlowEntity()

    interface IRtAction
    static member Create() = createExtended<DsAction>()


// see static member Create
and Work() = // Create
    inherit DsSystemEntity()

    interface IRtWork
    member val internal RawCalls  = ResizeArray<Call>() with get, set
    member val internal RawArrows = ResizeArray<ArrowBetweenCalls>() with get, set
    member val Flow = Option<Flow>.None with get, set

    member val Motion     = nullString with get, set
    member val Script     = nullString with get, set
    member val IsFinished = false      with get, set
    member val NumRepeat  = 0          with get, set
    member val Period     = 0          with get, set
    member val Delay      = 0          with get, set

    member val Status4 = Option<DbStatus4>.None with get, set

    member x.Calls  = x.RawCalls  |> toList
    member x.Arrows = x.RawArrows |> toList

    static member Create() = createExtended<Work>()
    static member Create(calls: Call seq, arrows: ArrowBetweenCalls seq, flow: Flow option) =
        let work = createExtended<Work>()
        // Set flow
        work.Flow <- flow

        // Add new components and set parent relationships
        calls  |> iter (fun c -> work.RawCalls. Add(c); setParentI work c)
        arrows |> iter (fun a -> work.RawArrows.Add(a); setParentI work a)

        work

// see static member Create
and Call() = // Create
    inherit WorkEntity()

    interface IRtCall
    member val CallType   = DbCallType.Normal with get, set    // 호출 유형 (예: "Normal", "Parallel", "Repeat")
    member val IsDisabled = false with get, set
    member val Timeout    = Option<int>.None with get, set    // 실행 타임아웃(ms)
    member val AutoConditions   = ResizeArray<string>() with get, set    // 사전 조건 식 (자동 실행 조건)
    member val CommonConditions = ResizeArray<string>() with get, set    // 안전 조건 식 (실행 보호조건)
    member val ApiCallGuids = ResizeArray<Guid>() with get, set    // DB 저장시에는 callId 로 저장
    member val Status4 = Option<DbStatus4>.None with get, set

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
    static member Create(callType: DbCallType, apiCallGuids: Guid seq, autoConditions: string seq, commonConditions: string seq, isDisabled: bool, timeout: int option) =
        let call = createExtended<Call>()
        // Set call properties
        call.CallType <- callType
        call.IsDisabled <- isDisabled
        call.Timeout <- timeout

        autoConditions |> iter call.AutoConditions.Add
        commonConditions |> iter call.CommonConditions.Add
        apiCallGuids |> iter call.ApiCallGuids.Add

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


    /// system 에서 현재 ApiCall 을 호출하는 Call 들
    member x.Callers:Call[] =
        x.System
        |-> (fun s ->
            s.Works >>= _.Calls
            |> filter (fun c -> c.ApiCalls.Contains x)
            |> toArray)
        |? [||]


    member x.ApiDef
        with get() =
            match x.RawParent with
            | Some (:? DsSystem as sys) ->
                sys.ApiDefs
                |> List.tryFind (fun ad -> ad.Guid = x.ApiDefGuid )
                |> Option.defaultWith (fun () -> failwith $"ApiDef with Guid {x.ApiDefGuid} not found in System")
            | _ -> failwith "Parent is not DsSystem type"
        and set (v:ApiDef) = x.ApiDefGuid <- v.Guid


and ApiDef(isPush:bool) = // Create, ApiUsers
    inherit DsSystemEntity()

    new() = new ApiDef(true)

    static member Create() = createExtended<ApiDef>()

    interface IRtApiDef

    member val IsPush = isPush with get, set

    member x.System   = x.RawParent >>= tryCast<DsSystem>

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