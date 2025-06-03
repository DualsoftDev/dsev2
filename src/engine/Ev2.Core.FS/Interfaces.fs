namespace Ev2.Core.FS

open System
open System.Linq

open Dual.Common.Base
open Dual.Common.Core.FS
open System.Collections.Generic

[<AutoOpen>]
module DsRuntimeObjectInterfaceModule =
    /// Runtime 객체 인터페이스
    type IRtObject  = inherit IDsObject
    /// Guid, Name, DateTime
    type IRtUnique    = inherit IRtObject inherit IUnique

    type IRtParameter = inherit IRtUnique inherit IParameter
    type IRtParameterContainer = inherit IRtUnique inherit IParameterContainer

    type IRtArrow     = inherit IRtUnique inherit IArrow

    type IRtProject = inherit IRtUnique inherit IDsProject
    type IRtSystem  = inherit IRtUnique inherit IDsSystem
    type IRtFlow    = inherit IRtUnique inherit IDsFlow
    type IRtWork    = inherit IRtUnique inherit IDsWork
    type IRtCall    = inherit IRtUnique inherit IDsCall
    type IRtApiCall = inherit IRtUnique inherit IDsApiCall
    type IRtApiDef  = inherit IRtUnique inherit IDsApiDef


[<AutoOpen>]
module rec DsObjectModule =
    [<AbstractClass>]
    type RtUnique() =
        inherit Unique()
        interface IRtUnique

    type internal Arrow<'T when 'T :> Unique>(source:'T, target:'T, typ:DbArrowType) =
        member val Source = source with get, set
        member val Target = target with get, set
        member val Type = typ with get, set

    /// Call 간 화살표 연결.  Work 내에 존재
    type RtArrowBetweenCalls(source:RtCall, target:RtCall, typ:DbArrowType) =
        inherit RtUnique()
        let arrow = Arrow<RtCall>(source, target, typ)

        interface IRtArrow
        member x.Source with get() = arrow.Source and set v = arrow.Source <- v
        member x.Target with get() = arrow.Target and set v = arrow.Target <- v
        member x.Type   with get() = arrow.Type   and set v = arrow.Type <- v

    /// Work 간 화살표 연결.  System 이나 Flow 내에 존재
    type RtArrowBetweenWorks(source:RtWork, target:RtWork, typ:DbArrowType) =
        inherit RtUnique()
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

        interface IRtProject
        interface IParameterContainer

        // { JSON 용
        /// 마지막 저장 db 에 대한 connection string
        member val LastConnectionString:string = null with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨

        member val Author        = $"{Environment.UserName}@{Environment.UserDomainName}" with get, set
        member val Version       = Version()  with get, set
        //member val LangVersion   = langVersion   |? Version()  with get, set
        //member val EngineVersion = engineVersion |? Version()  with get, set
        member val Description   = nullString with get, set

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
        inherit RtUnique()

        (* RtSystem.Name 은 prototype 인 경우, prototype name 을, 아닌 경우 loaded system name 을 의미한다. *)
        interface IParameterContainer
        interface IRtSystem
        member val internal RawFlows    = ResizeArray flows
        member val internal RawWorks    = ResizeArray works
        member val internal RawArrows   = ResizeArray arrows
        member val internal RawApiDefs  = ResizeArray apiDefs
        member val internal RawApiCalls = ResizeArray apiCalls
        /// Origin Guid: 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null
        member val OriginGuid = noneGuid with get, set
        member val PrototypeSystemGuid = protoGuid with get, set

        member val Author        = Environment.UserName with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set

        // serialize 대상 아님
        member x.Project = x.RawParent >>= tryCast<RtProject>

        member x.Flows    = x.RawFlows    |> toList
        member x.Works    = x.RawWorks    |> toList
        member x.Arrows   = x.RawArrows   |> toList
        member x.ApiDefs  = x.RawApiDefs  |> toList
        member x.ApiCalls = x.RawApiCalls |> toList



    type RtFlow() =
        inherit RtUnique()

        interface IRtFlow

        member x.System = x.RawParent >>= tryCast<RtSystem>

        member x.Works:RtWork[] =
            x.System
            |-> (fun s ->
                s.Works
                |> filter (fun w -> w.Flow = Some x)
                |> toArray)
            |? [||]

    // see static member Create
    type RtWork internal(calls:RtCall seq, arrows:RtArrowBetweenCalls seq, flow:RtFlow option) as this =
        inherit RtUnique()
        do
            calls  |> iter (setParentI this)
            arrows |> iter (setParentI this)

        interface IRtWork
        member val internal RawCalls  = ResizeArray calls
        member val internal RawArrows = ResizeArray arrows
        member val Flow   = flow with get, set
        member val Status4 = Option<DbStatus4>.None with get, set

        member x.Calls  = x.RawCalls  |> toList
        member x.Arrows = x.RawArrows |> toList
        member x.System = x.RawParent >>= tryCast<RtSystem>


    // see static member Create
    type RtCall(callType:DbCallType, apiCallGuids:Guid seq, autoPre:string, safety:string, isDisabled:bool, timeout:int option) =
        inherit RtUnique()
        interface IRtCall
        member val CallType   = callType   with get, set
        member val AutoPre    = autoPre    with get, set
        member val Safety     = safety     with get, set
        member val IsDisabled = isDisabled with get, set
        member val Timeout    = timeout    with get, set
        member val Status4 = Option<DbStatus4>.None with get, set
        member val ApiCallGuids = ResizeArray apiCallGuids    // DB 저장시에는 callId 로 저장

        member x.Work = x.RawParent >>= tryCast<RtWork>
        member x.ApiCalls =
            let sys = (x.RawParent >>= _.RawParent).Value :?> RtSystem
            sys.ApiCalls |> filter(fun ac -> x.ApiCallGuids |> contains ac.Guid ) |> toList    // DB 저장시에는 callId 로 저장




    type RtApiCall(apiDefGuid:Guid, inAddress:string, outAddress:string,
                   inSymbol:string, outSymbol:string,
                   valueType:DbDataType, rangeType:DbRangeType, value1:string, value2:string
    ) =
        inherit RtUnique()
        interface IRtApiCall
        member val ApiDefGuid = apiDefGuid  with get, set
        member val InAddress  = inAddress   with get, set
        member val OutAddress = outAddress  with get, set
        member val InSymbol   = inSymbol    with get, set
        member val OutSymbol  = outSymbol   with get, set
        member val ValueType  = valueType   with get, set
        member val RangeType  = rangeType   with get, set
        member val Value1     = value1       with get, set
        member val Value2     = value2       with get, set


        member x.System   = x.RawParent >>= tryCast<RtSystem>
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
        inherit RtUnique()
        interface IRtApiDef

        member val IsPush = isPush
        member x.System   = x.RawParent >>= tryCast<RtSystem>

        // system 에서 현재 ApiDef 을 사용하는 ApiCall 들
        member x.ApiUsers:RtApiCall[] =
            x.System
            |-> (fun s ->
                s.ApiCalls
                |> filter (fun c -> c.ApiDef = x)
                |> toArray)
            |? [||]

