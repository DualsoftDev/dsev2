namespace Ev2.Core.FS

open System
open System.Linq

open Dual.Common.Base
open Dual.Common.Core.FS
open System.Collections.Generic
open Dual.Common.Db.FS
open Newtonsoft.Json

[<AutoOpen>]
module DsRuntimeObjectInterfaceModule =
    type IRtParameter = inherit IRtUnique inherit IParameter
    type IRtParameterContainer = inherit IRtUnique inherit IParameterContainer

    type IRtArrow     = inherit IRtUnique inherit IArrow

    type IRtProject = inherit IRtUnique inherit IWithDateTime
    type IRtSystem  = inherit IRtUnique inherit IWithDateTime
    type IRtFlow    = inherit IRtUnique inherit IDsFlow
    type IRtWork    = inherit IRtUnique inherit IDsWork
    type IRtCall    = inherit IRtUnique inherit IDsCall
    type IRtApiCall = inherit IRtUnique inherit IDsApiCall
    type IRtApiDef  = inherit IRtUnique inherit IDsApiDef

    type IRtButton    = inherit IRtUnique inherit IDsButton
    type IRtLamp      = inherit IRtUnique inherit IDsLamp
    type IRtCondition = inherit IRtUnique inherit IDsCondition
    type IRtAction    = inherit IRtUnique inherit IDsAction


[<AutoOpen>]
module rec DsObjectModule =
    [<AbstractClass>]
    type RtUnique() =
        inherit Unique()
        interface IRtUnique

    (* RtXXXEntity 들의 멤버는 serialize 대상 아님 *)

    [<AbstractClass>]
    type RtProjectEntity() =
        inherit RtUnique()
        member x.Project = x.RawParent >>= tryCast<RtProject>

    /// RtSystem 객체에 포함되는 member 들이 상속할 base class.  e.g RtFlow, RtWork, RtArrowBetweenWorks, RtApiDef, RtApiCall
    [<AbstractClass>]
    type RtSystemEntity() =
        inherit RtUnique()
        member x.System  = x.RawParent >>= tryCast<RtSystem>
        member x.Project = x.RawParent >>= _.RawParent >>= tryCast<RtProject>

    [<AbstractClass>]
    type RtFlowEntity() =
        inherit RtUnique()
        member x.Flow    = x.RawParent >>= tryCast<RtFlow>
        member x.System  = x.RawParent >>= _.RawParent >>= tryCast<RtSystem>
        member x.Project = x.RawParent >>= _.RawParent>>= _.RawParent >>= tryCast<RtProject>

    [<AbstractClass>]
    type RtWorkEntity() =
        inherit RtUnique()
        member x.Work    = x.RawParent >>= tryCast<RtWork>
        member x.System  = x.RawParent >>= _.RawParent >>= tryCast<RtSystem>
        member x.Project = x.RawParent >>= _.RawParent>>= _.RawParent >>= tryCast<RtProject>

    [<AbstractClass>]
    type RtCallEntity() =
        inherit RtUnique()
        member x.Call    = x.RawParent >>= tryCast<RtCall>
        member x.Work    = x.RawParent >>= _.RawParent >>= tryCast<RtWork>
        member x.System  = x.RawParent >>= _.RawParent >>= _.RawParent >>= tryCast<RtSystem>
        member x.Project = x.RawParent >>= _.RawParent >>= _.RawParent >>= _.RawParent >>= tryCast<RtProject>



    type internal Arrow<'T when 'T :> Unique>(source:'T, target:'T, typ:DbArrowType) =
        member val Source = source with get, set
        member val Target = target with get, set
        member val Type = typ with get, set

    /// Call 간 화살표 연결.  Work 내에 존재
    type RtArrowBetweenCalls(source:RtCall, target:RtCall, typ:DbArrowType) =
        inherit RtWorkEntity()
        let arrow = Arrow<RtCall>(source, target, typ)

        interface IRtArrow
        member x.Source with get() = arrow.Source and set v = arrow.Source <- v
        member x.Target with get() = arrow.Target and set v = arrow.Target <- v
        member x.Type   with get() = arrow.Type   and set v = arrow.Type <- v

    /// Work 간 화살표 연결.  System 내에 존재
    type RtArrowBetweenWorks(source:RtWork, target:RtWork, typ:DbArrowType) =
        inherit RtSystemEntity()
        let arrow = Arrow<RtWork>(source, target, typ)

        interface IRtArrow
        member x.Source with get() = arrow.Source and set v = arrow.Source <- v
        member x.Target with get() = arrow.Target and set v = arrow.Target <- v
        member x.Type   with get() = arrow.Type   and set v = arrow.Type <- v


    type RtProject(prototypeSystems:RtSystem[], activeSystems:RtSystem[], passiveSystems:RtSystem[]) as this =
        inherit RtUnique()
        do
            activeSystems  |> iter (setParentI this)
            passiveSystems |> iter (setParentI this)

        interface IRtProject with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v
        interface IParameterContainer

        // { JSON 용
        /// 마지막 저장 db 에 대한 connection string
        member val Database = getNull<DbProvider>() with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨

        member val Author        = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
        member val Version       = Version()  with get, set
        //member val LangVersion   = langVersion   |? Version()  with get, set
        //member val EngineVersion = engineVersion |? Version()  with get, set
        member val Description   = nullString with get, set

        /// DateTime: 메모리에 최초 객체 생성시 생성
        member val DateTime = now() with get, set

        member val internal RawActiveSystems    = ResizeArray activeSystems
        member val internal RawPassiveSystems   = ResizeArray passiveSystems
        member val internal RawPrototypeSystems = ResizeArray prototypeSystems

        member x.PrototypeSystems = x.RawPrototypeSystems |> toList
        // { Runtime/DB 용
        member x.ActiveSystems = x.RawActiveSystems |> toList
        member x.PassiveSystems = x.RawPassiveSystems |> toList
        member x.Systems = (x.ActiveSystems @ x.PassiveSystems) |> toList
        // } Runtime/DB 용


    type RtSystem internal(protoGuid:Guid option, flows:RtFlow[], works:RtWork[],
            arrows:RtArrowBetweenWorks[], apiDefs:RtApiDef[], apiCalls:RtApiCall[]
    ) =
        inherit RtProjectEntity()

        (* RtSystem.Name 은 prototype 인 경우, prototype name 을, 아닌 경우 loaded system name 을 의미한다. *)
        interface IParameterContainer
        interface IRtProject with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v
        member val internal RawFlows    = ResizeArray flows
        member val internal RawWorks    = ResizeArray works
        member val internal RawArrows   = ResizeArray arrows
        member val internal RawApiDefs  = ResizeArray apiDefs
        member val internal RawApiCalls = ResizeArray apiCalls

        member x.SupervisorProjectId = x.Project >>= (fun p -> if p.ActiveSystems.Contains(x) then p.Id else None)

        /// Origin Guid: 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null
        member val OriginGuid = noneGuid with get, set
        member val PrototypeSystemGuid = protoGuid with get, set

        member val IRI           = nullString with get, set
        member val Author        = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set
        /// DateTime: 메모리에 최초 객체 생성시 생성
        member val DateTime      = now()      with get, set

        member x.Flows    = x.RawFlows    |> toList
        member x.Works    = x.RawWorks    |> toList
        member x.Arrows   = x.RawArrows   |> toList
        member x.ApiDefs  = x.RawApiDefs  |> toList
        member x.ApiCalls = x.RawApiCalls |> toList



    type RtFlow(buttons:RtButton seq, lamps:RtLamp seq, conditions:RtCondition seq, actions:RtAction seq) as this =
        inherit RtSystemEntity()
        do
            buttons    |> iter (fun z -> z.RawParent <- Some this)
            lamps      |> iter (fun z -> z.RawParent <- Some this)
            conditions |> iter (fun z -> z.RawParent <- Some this)
            actions    |> iter (fun z -> z.RawParent <- Some this)

        interface IRtFlow
        member val internal RawButtons    = ResizeArray buttons
        member val internal RawLamps      = ResizeArray lamps
        member val internal RawConditions = ResizeArray conditions
        member val internal RawActions    = ResizeArray actions

        member x.System = x.RawParent >>= tryCast<RtSystem>

        member x.Buttons    = x.RawButtons    |> toList
        member x.Lamps      = x.RawLamps      |> toList
        member x.Conditions = x.RawConditions |> toList
        member x.Actions    = x.RawActions    |> toList

        member x.Works:RtWork[] =
            x.System
            |-> (fun s ->
                s.Works
                |> filter (fun w -> w.Flow = Some x)
                |> toArray)
            |? [||]

    type RtButton() =
        inherit RtFlowEntity()

        interface IRtButton

    type RtLamp() =
        inherit RtFlowEntity()

        interface IRtLamp

    type RtCondition() =
        inherit RtFlowEntity()

        interface IRtCondition

    type RtAction() =
        inherit RtFlowEntity()

        interface IRtAction


    // see static member Create
    type RtWork internal(calls:RtCall seq, arrows:RtArrowBetweenCalls seq, flow:RtFlow option) as this =
        inherit RtSystemEntity()
        do
            calls  |> iter (setParentI this)
            arrows |> iter (setParentI this)

        interface IRtWork
        member val internal RawCalls  = ResizeArray calls
        member val internal RawArrows = ResizeArray arrows
        member val Flow = flow with get, set

        member val Motion     = nullString with get, set
        member val Script     = nullString with get, set
        member val IsFinished = false      with get, set
        member val NumRepeat  = 0          with get, set
        member val Period     = 0          with get, set
        member val Delay      = 0          with get, set

        member val Status4 = Option<DbStatus4>.None with get, set

        member x.Calls  = x.RawCalls  |> toList
        member x.Arrows = x.RawArrows |> toList


    // see static member Create
    type RtCall(callType:DbCallType, apiCallGuids:Guid seq, autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option) =
        inherit RtWorkEntity()
        interface IRtCall
        member val CallType   = callType   with get, set    // 호출 유형 (예: "Normal", "Parallel", "Repeat")
        member val IsDisabled = isDisabled with get, set
        member val Timeout    = timeout    with get, set    // 실행 타임아웃(ms)
        member val AutoConditions   = ResizeArray autoConditions   with get, set    // 사전 조건 식 (자동 실행 조건)
        member val CommonConditions = ResizeArray commonConditions with get, set    // 안전 조건 식 (실행 보호조건)
        member val ApiCallGuids = ResizeArray apiCallGuids    // DB 저장시에는 callId 로 저장
        member val Status4 = Option<DbStatus4>.None with get, set

        member x.ApiCalls =
            let sys = (x.RawParent >>= _.RawParent).Value :?> RtSystem
            sys.ApiCalls |> filter(fun ac -> x.ApiCallGuids |> contains ac.Guid ) |> toList    // DB 저장시에는 callId 로 저장




    type RtApiCall(apiDefGuid:Guid, inAddress:string, outAddress:string,
                   inSymbol:string, outSymbol:string,
                   valueSpec:IValueSpec option
    ) =
        inherit RtSystemEntity()
        interface IRtApiCall
        member val ApiDefGuid = apiDefGuid  with get, set
        member val InAddress  = inAddress   with get, set
        member val OutAddress = outAddress  with get, set
        member val InSymbol   = inSymbol    with get, set
        member val OutSymbol  = outSymbol   with get, set

        member val ValueSpec = valueSpec with get, set


        /// system 에서 현재 ApiCall 을 호출하는 Call 들
        member x.Callers:RtCall[] =
            x.System
            |-> (fun s ->
                s.Works >>= _.Calls
                |> filter (fun c -> c.ApiCalls.Contains x)
                |> toArray)
            |? [||]


        member x.ApiDef
            with get() =
                let sys = x.RawParent.Value :?> RtSystem
                sys.ApiDefs |> find (fun ad -> ad.Guid = x.ApiDefGuid )
            and set (v:RtApiDef) = x.ApiDefGuid <- v.Guid


    type RtApiDef(isPush:bool) =
        inherit RtSystemEntity()
        interface IRtApiDef

        member val IsPush = isPush with get, set
        member x.System   = x.RawParent >>= tryCast<RtSystem>

        // system 에서 현재 ApiDef 을 사용하는 ApiCall 들
        member x.ApiUsers:RtApiCall[] =
            x.System
            |-> (fun s ->
                s.ApiCalls
                |> filter (fun c -> c.ApiDef = x)
                |> toArray)
            |? [||]

