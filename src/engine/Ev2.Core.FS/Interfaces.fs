namespace Ev2.Core.FS

open System

open Dual.Common.Base
open Dual.Common.Core.FS
open System.Collections.Generic

[<AutoOpen>]
module DsRuntimeObjectInterfaceModule =
    /// Runtime 객체 인터페이스
    type IRtObject  = interface end
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

    //type IRtUnique with
    //    member x.GetGuid() = (x :?> RtUnique).Guid


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
            activeSystems  |> iter (fun z -> z.RawParent <- Some this)
            passiveSystems |> iter (fun z -> z.RawParent <- Some this)

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
        member val Flows    = ResizeArray flows
        member val Works    = ResizeArray works
        member val Arrows   = ResizeArray arrows
        member val ApiDefs  = ResizeArray apiDefs
        member val ApiCalls = ResizeArray apiCalls
        /// Origin Guid: 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null
        member val OriginGuid = noneGuid with get, set
        member val PrototypeSystemGuid = protoGuid with get, set

        member val Author        = Environment.UserName with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set

    type RtSystem with
        member x.TryGetProject() = x.RawParent |-> (fun z -> z :?> RtProject)
        member x.Project = x.TryGetProject() |? getNull<RtProject>()



    type RtFlow() =
        inherit RtUnique()

        interface IRtFlow
        member x.System = x.RawParent >>= tryCast<RtSystem> |? getNull<RtSystem>()
        member x.Works = x.System.Works |> filter (fun w -> w.OptFlow = Some x) |> toArray

    // see static member Create
    type RtWork internal(calls:RtCall seq, arrows:RtArrowBetweenCalls seq, optFlow:RtFlow option) as this =
        inherit RtUnique()
        do
            calls  |> iter (fun z -> z.RawParent <- Some this)
            arrows |> iter (fun z -> z.RawParent <- Some this)

        interface IRtWork
        member val Calls  = ResizeArray calls
        member val Arrows = ResizeArray arrows
        member val OptFlow  = optFlow with get, set
        member x.System   = x.RawParent >>= tryCast<RtSystem> |? getNull<RtSystem>()


    // see static member Create
    type RtCall(callType:DbCallType, apiCallGuids:Guid seq, autoPre:string, safety:string, isDisabled:bool, timeout:int option) =
        inherit RtUnique()
        interface IRtCall
        member x.Work = x.RawParent >>= tryCast<RtWork> |? getNull<RtWork>()
        member val CallType   = callType   with get, set
        member val AutoPre    = autoPre    with get, set
        member val Safety     = safety     with get, set
        member val IsDisabled = isDisabled with get, set
        member val Timeout    = timeout    with get, set
        member val ApiCallGuids = ResizeArray apiCallGuids    // DB 저장시에는 callId 로 저장
        member x.ApiCalls =
            let sys = (x.RawParent >>= _.RawParent).Value :?> RtSystem
            sys.ApiCalls |> filter(fun ac -> x.ApiCallGuids |> contains ac.Guid ) |> toList    // DB 저장시에는 callId 로 저장




    type RtApiCall(apiDefGuid:Guid, inAddress:string, outAddress:string,
                   inSymbol:string, outSymbol:string, valueType:DbDataType, value:string
    ) =
        inherit RtUnique()
        interface IRtApiCall
        member val ApiDefGuid = apiDefGuid  with get, set
        member val InAddress  = inAddress   with get, set
        member val OutAddress = outAddress  with get, set
        member val InSymbol   = inSymbol    with get, set
        member val OutSymbol  = outSymbol   with get, set
        member val ValueType  = valueType   with get, set
        member val Value      = value       with get, set


        member x.Call = x.RawParent >>= tryCast<RtCall> |? getNull<RtCall>()
        member x.ApiDef
            with get() =
                let sys = x.RawParent.Value :?> RtSystem
                sys.ApiDefs |> find (fun ad -> ad.Guid = x.ApiDefGuid )
            and set (v:RtApiDef) = x.ApiDefGuid <- v.Guid


    type RtApiDef(isPush:bool) =
        inherit RtUnique()
        interface IRtApiDef
        member val IsPush = isPush

[<AutoOpen>]
module rec TmpCompatibility =
    type RtUnique with
        /// DS object 의 모든 상위 DS object 의 DateTime 을 갱신.  (tree 구조를 따라가면서 갱신)
        member x.UpdateDateTime(?dateTime:DateTime) =
            let dateTime = dateTime |?? now
            x.EnumerateRtObjects() |> iter (fun z -> z.DateTime <- dateTime)

        (* see also EdUnique.EnumerateRtObjects *)
        member x.EnumerateRtObjects(?includeMe): RtUnique list =
            seq {
                let includeMe = includeMe |? true
                if includeMe then
                    yield x
                match x with
                | :? RtProject as prj ->
                    yield! prj.PrototypeSystems >>= _.EnumerateRtObjects()
                    yield! prj.Systems   >>= _.EnumerateRtObjects()
                | :? RtSystem as sys ->
                    yield! sys.Works     >>= _.EnumerateRtObjects()
                    yield! sys.Flows     >>= _.EnumerateRtObjects()
                    yield! sys.Arrows    >>= _.EnumerateRtObjects()
                    yield! sys.ApiDefs   >>= _.EnumerateRtObjects()
                    yield! sys.ApiCalls  >>= _.EnumerateRtObjects()
                | :? RtWork as work ->
                    yield! work.Calls    >>= _.EnumerateRtObjects()
                    yield! work.Arrows   >>= _.EnumerateRtObjects()
                | :? RtCall as call ->
                    //yield! call.ApiCalls >>= _.EnumerateRtObjects()
                    ()
                | _ ->
                    tracefn $"Skipping {(x.GetType())} in EnumerateRtObjects"
                    ()
            } |> List.ofSeq

        member x.Validate(guidDic:Dictionary<Guid, RtUnique>) =
            verify (x.Guid <> emptyGuid)
            verify (x.DateTime <> minDate)
            match x with
            | (:? RtProject | :? RtSystem | :? RtFlow  | :? RtWork  | :? RtCall) ->
                verify (x.Name.NonNullAny())
            | _ -> ()

            match x with
            | :? RtProject as prj ->
                prj.Systems |> iter _.Validate(guidDic)
                for s in prj.Systems do
                    verify (s.RawParent |-> _.Guid = Some prj.Guid)

            | :? RtSystem as sys ->
                sys.Works |> iter _.Validate(guidDic)
                for w in sys.Works  do
                    verify (w.RawParent |-> _.Guid = Some sys.Guid)
                    for c in w.Calls do
                        c.ApiCalls |-> _.Guid |> forall(guidDic.ContainsKey) |> verify
                        for ac in c.ApiCalls do
                            ac.ApiDef.Guid = ac.ApiDefGuid |> verify

                sys.Arrows |> iter _.Validate(guidDic)
                for a in sys.Arrows do
                    verify (a.RawParent |-> _.Guid = Some sys.Guid)
                    sys.Works |> contains a.Source |> verify
                    sys.Works |> contains a.Target |> verify

                sys.ApiDefs |> iter _.Validate(guidDic)
                for w in sys.ApiDefs  do
                    verify (w.RawParent |-> _.Guid = Some sys.Guid)

                sys.ApiCalls |> iter _.Validate(guidDic)
                for ac in sys.ApiCalls  do
                    verify (ac.RawParent |-> _.Guid = Some sys.Guid)

            | :? RtFlow as flow ->
                let works = flow.Works
                works |> iter _.Validate(guidDic)
                for w in works  do
                    verify (w.OptFlow = Some flow)


            | :? RtWork as work ->
                work.Calls |> iter _.Validate(guidDic)
                for c in work.Calls do
                    verify (c.RawParent |-> _.Guid = Some work.Guid)

                work.Arrows |> iter _.Validate(guidDic)
                for a in work.Arrows do
                    verify (a.RawParent |-> _.Guid = Some work.Guid)
                    work.Calls |> contains a.Source |> verify
                    work.Calls |> contains a.Target |> verify


            | :? RtCall as call ->
                ()

            | _ ->
                tracefn $"Skipping {(x.GetType())} in Validate"
                ()




    type RtProject with
        static member Create() = RtProject([||], [||], [||])

        member x.AddPrototypeSystem(system:RtSystem) =
            x.RawPrototypeSystems.Add system

        member x.AddActiveSystem(system:RtSystem) =
            system.RawParent <- Some x
            x.RawActiveSystems.Add system

        member x.AddPassiveSystem(system:RtSystem) =
            system.RawParent <- Some x
            x.RawPassiveSystems.Add system

        member x.Instantiate(prototypeGuid:Guid, asActive:bool):RtSystem =
            x.PrototypeSystems
            |> tryFind(fun s -> s.Guid = prototypeGuid ) |?? (fun () -> failwith "Prototype system not found")
            |> (fun z -> fwdDuplicate z :?> RtSystem)
            |> tee (fun z ->
                z.PrototypeSystemGuid <- Some prototypeGuid
                if asActive then x.AddActiveSystem z
                else x.AddPassiveSystem z)

        member x.Fix() =
            x.ActiveSystems @ x.PassiveSystems |> iter (fun sys -> sys.RawParent <- Some x; sys.Fix())
            //x.UpdateDateTime()

    type RtSystem with
        member x.Fix() =
            x.UpdateDateTime()
            x.Flows    |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            x.Works    |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            x.Arrows   |> iter (fun z -> z.RawParent <- Some x)
            x.ApiDefs  |> iter (fun z -> z.RawParent <- Some x)
            x.ApiCalls |> iter (fun z -> z.RawParent <- Some x)
            ()
    type RtFlow with
        // works 들이 flow 자신의 직접 child 가 아니므로 따로 관리 함수 필요
        member x.AddWorks(ws:RtWork seq) =
            x.UpdateDateTime()
            ws |> iter (fun w -> w.OptFlow <- Some x)

        member x.RemoveWorks(ws:RtWork seq) =
            x.UpdateDateTime()
            ws |> iter (fun w -> w.OptFlow <- None)

        member x.Fix() = ()

    type RtWork with
        member x.Fix() =
            x.UpdateDateTime()
            x.Calls  |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            x.Arrows |> iter (fun z -> z.RawParent <- Some x)
            ()

    type RtCall with
        member x.AddApiCalls(apiCalls:RtApiCall seq) =
            x.UpdateDateTime()
            apiCalls |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            apiCalls |> iter (fun z -> x.ApiCallGuids.Add z.Guid)

        member x.Fix() =
            x.UpdateDateTime()
            x.ApiCalls |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            ()
    type RtApiDef with
        static member Create() = RtApiDef(true)
        member x.Fix() =
            x.UpdateDateTime()
            ()
    type RtApiCall with
        static member Create() = RtApiCall(emptyGuid, nullString, nullString, nullString, nullString, DbDataType.None, nullString)
        member x.Fix() =
            x.UpdateDateTime()
            ()
