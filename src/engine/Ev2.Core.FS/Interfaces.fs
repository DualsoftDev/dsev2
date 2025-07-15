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
    type ProjectEntity() =
        inherit RtUnique()
        member x.Project = x.RawParent >>= tryCast<Project>

    /// RtSystem 객체에 포함되는 member 들이 상속할 base class.  e.g RtFlow, RtWork, RtArrowBetweenWorks, RtApiDef, RtApiCall
    [<AbstractClass>]
    type DsSystemEntity() =
        inherit RtUnique()
        member x.System  = x.RawParent >>= tryCast<DsSystem>
        member x.Project = x.RawParent >>= _.RawParent >>= tryCast<Project>

    [<AbstractClass>]
    type FlowEntity() =
        inherit RtUnique()
        member x.Flow    = x.RawParent >>= tryCast<Flow>
        member x.System  = x.RawParent >>= _.RawParent >>= tryCast<DsSystem>
        member x.Project = x.RawParent >>= _.RawParent>>= _.RawParent >>= tryCast<Project>

    [<AbstractClass>]
    type WorkEntity() =
        inherit RtUnique()
        member x.Work    = x.RawParent >>= tryCast<Work>
        member x.System  = x.RawParent >>= _.RawParent >>= tryCast<DsSystem>
        member x.Project = x.RawParent >>= _.RawParent>>= _.RawParent >>= tryCast<Project>

    [<AbstractClass>]
    type CallEntity() =
        inherit RtUnique()
        member x.Call    = x.RawParent >>= tryCast<Call>
        member x.Work    = x.RawParent >>= _.RawParent >>= tryCast<Work>
        member x.System  = x.RawParent >>= _.RawParent >>= _.RawParent >>= tryCast<DsSystem>
        member x.Project = x.RawParent >>= _.RawParent >>= _.RawParent >>= _.RawParent >>= tryCast<Project>



    type internal Arrow<'T when 'T :> Unique>(source:'T, target:'T, typ:DbArrowType) =
        member val Source = source with get, set
        member val Target = target with get, set
        member val Type = typ with get, set

    /// Call 간 화살표 연결.  Work 내에 존재
    type ArrowBetweenCalls(source:Call, target:Call, typ:DbArrowType) =
        inherit WorkEntity()
        let arrow = Arrow<Call>(source, target, typ)

        interface IRtArrow
        member x.Source with get() = arrow.Source and set v = arrow.Source <- v
        member x.Target with get() = arrow.Target and set v = arrow.Target <- v
        member x.Type   with get() = arrow.Type   and set v = arrow.Type <- v

    /// Work 간 화살표 연결.  System 내에 존재
    type ArrowBetweenWorks(source:Work, target:Work, typ:DbArrowType) =
        inherit DsSystemEntity()
        let arrow = Arrow<Work>(source, target, typ)

        interface IRtArrow
        member x.Source with get() = arrow.Source and set v = arrow.Source <- v
        member x.Target with get() = arrow.Target and set v = arrow.Target <- v
        member x.Type   with get() = arrow.Type   and set v = arrow.Type <- v


    type Project(myPrototypeSystems:DsSystem seq, importedPrototypeSystems:DsSystem seq, activeSystems:DsSystem seq, passiveSystems:DsSystem seq) as this =
        inherit RtUnique()
        do
            activeSystems  |> iter (setParentI this)
            passiveSystems |> iter (setParentI this)
            myPrototypeSystems |> forall(_.IsPrototype) |> verify // prototypeSystems must be prototype systems
            importedPrototypeSystems |> forall(_.IsPrototype) |> verify // prototypeSystems must be prototype systems

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
        member val DateTime = now().TruncateToSecond() with get, set

        member val internal RawActiveSystems    = ResizeArray activeSystems
        member val internal RawPassiveSystems   = ResizeArray passiveSystems
        member val internal RawMyPrototypeSystems       = ResizeArray myPrototypeSystems
        member val internal RawImportedPrototypeSystems = ResizeArray importedPrototypeSystems

        member x.MyPrototypeSystems       = x.RawMyPrototypeSystems       |> toList
        member x.ImportedPrototypeSystems = x.RawImportedPrototypeSystems |> toList
        // { Runtime/DB 용
        member x.ActiveSystems  = x.RawActiveSystems  |> toList
        member x.PassiveSystems = x.RawPassiveSystems |> toList
        member x.Systems = (x.ActiveSystems @ x.PassiveSystems) |> toList
        // } Runtime/DB 용


    type DsSystem internal(flows:Flow seq, works:Work seq,
            arrows:ArrowBetweenWorks seq, apiDefs:ApiDef seq, apiCalls:ApiCall seq
    ) =
        inherit ProjectEntity()

        //internal new() = RtSystem(Seq.empty, Seq.empty, Seq.empty, Seq.empty, Seq.empty)

        (* RtSystem.Name 은 prototype 인 경우, prototype name 을, 아닌 경우 loaded system name 을 의미한다. *)
        interface IParameterContainer
        interface IRtSystem with
            member x.DateTime  with get() = x.DateTime and set v = x.DateTime <- v
        member val internal RawFlows    = ResizeArray flows
        member val internal RawWorks    = ResizeArray works
        member val internal RawArrows   = ResizeArray arrows
        member val internal RawApiDefs  = ResizeArray apiDefs
        member val internal RawApiCalls = ResizeArray apiCalls

        member x.OwnerProjectId = x.Project >>= (fun p -> if p.ActiveSystems.Contains(x) then p.Id else None)

        /// Origin Guid: 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null
        member val OriginGuid = noneGuid with get, set


        /// this system 이 prototype 으로 정의되었는지 여부
        member val IsPrototype = false with get, set
        /// this system 이 Instance 로 사용될 때에만 Some 값.
        member val PrototypeSystemGuid = Option<Guid>.None with get, set


        member x.Prototype = x.Project >>= (fun z -> (z.MyPrototypeSystems @ z.ImportedPrototypeSystems).TryFind(fun s -> Some s.Guid = x.PrototypeSystemGuid))


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




    type Flow(buttons:DsButton seq, lamps:Lamp seq, conditions:DsCondition seq, actions:DsAction seq) as this =
        inherit DsSystemEntity()
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

    type DsButton() =
        inherit FlowEntity()

        interface IRtButton

    type Lamp() =
        inherit FlowEntity()

        interface IRtLamp

    type DsCondition() =
        inherit FlowEntity()

        interface IRtCondition

    type DsAction() =
        inherit FlowEntity()

        interface IRtAction


    // see static member Create
    type Work internal(calls:Call seq, arrows:ArrowBetweenCalls seq, flow:Flow option) as this =
        inherit DsSystemEntity()
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
    type Call(callType:DbCallType, apiCallGuids:Guid seq, autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option) =
        inherit WorkEntity()
        interface IRtCall
        member val CallType   = callType   with get, set    // 호출 유형 (예: "Normal", "Parallel", "Repeat")
        member val IsDisabled = isDisabled with get, set
        member val Timeout    = timeout    with get, set    // 실행 타임아웃(ms)
        member val AutoConditions   = ResizeArray autoConditions   with get, set    // 사전 조건 식 (자동 실행 조건)
        member val CommonConditions = ResizeArray commonConditions with get, set    // 안전 조건 식 (실행 보호조건)
        member val ApiCallGuids = ResizeArray apiCallGuids    // DB 저장시에는 callId 로 저장
        member val Status4 = Option<DbStatus4>.None with get, set

        member x.ApiCalls =
            let sys = (x.RawParent >>= _.RawParent).Value :?> DsSystem
            sys.ApiCalls |> filter(fun ac -> x.ApiCallGuids |> contains ac.Guid ) |> toList    // DB 저장시에는 callId 로 저장




    type ApiCall(apiDefGuid:Guid, inAddress:string, outAddress:string,
                   inSymbol:string, outSymbol:string,
                   valueSpec:IValueSpec option
    ) =
        inherit DsSystemEntity()
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
                let sys = x.RawParent.Value :?> DsSystem
                sys.ApiDefs |> find (fun ad -> ad.Guid = x.ApiDefGuid )
            and set (v:ApiDef) = x.ApiDefGuid <- v.Guid


    type ApiDef(isPush:bool) =
        inherit DsSystemEntity()
        interface IRtApiDef

        member val IsPush = isPush with get, set
        member x.System   = x.RawParent >>= tryCast<DsSystem>

        // system 에서 현재 ApiDef 을 사용하는 ApiCall 들
        member x.ApiUsers:ApiCall[] =
            x.System
            |-> (fun s ->
                s.ApiCalls
                |> filter (fun c -> c.ApiDef = x)
                |> toArray)
            |? [||]

