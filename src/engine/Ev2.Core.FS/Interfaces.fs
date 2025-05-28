namespace Ev2.Core.FS

open System

open Dual.Common.Base
open Dual.Common.Core.FS

[<AutoOpen>]
module DsRuntimeObjectInterfaceModule =
    /// Runtime 객체 인터페이스
    type IRtObject  = interface end
    type IRtParameter = inherit IParameter
    type IRtParameterContainer = inherit IParameterContainer
    type IRtArrow     = inherit IArrow

    /// Guid, Name, DateTime
    type IRtUnique    = inherit IUnique


    type IRtProject = inherit IDsProject
    type IRtSystem  = inherit IDsSystem
    type IRtFlow    = inherit IDsFlow
    type IRtWork    = inherit IDsWork
    type IRtCall    = inherit IDsCall
    type IRtApiCall = inherit IDsApiCall
    type IRtApiDef  = inherit IDsApiDef




[<AutoOpen>]
module rec DsObjectModule =
    [<AbstractClass>]
    type RtUnique() =
        inherit Unique()
        interface IRtUnique


    [<AbstractClass>]
    type Arrow<'T when 'T :> Unique>(source:'T, target:'T) =
        inherit Unique()

        interface IArrow
        member val Source = source with get, set
        member val Target = target with get, set

    /// Call 간 화살표 연결.  Work 내에 존재
    type RtArrowBetweenCalls(source:RtCall, target:RtCall) =
        inherit Arrow<RtCall>(source, target)
        interface IRtUnique
        interface IRtApiCall

    /// Work 간 화살표 연결.  System 이나 Flow 내에 존재
    type RtArrowBetweenWorks(source:RtWork, target:RtWork) =
        inherit Arrow<RtWork>(source, target)
        interface IRtUnique
        interface IRtApiCall


    type RtProject(activeSystems:RtSystem[], passiveSystems:RtSystem[]) as this =
        inherit RtUnique()
        do
            activeSystems  |> iter (fun z -> z.RawParent <- Some this)
            passiveSystems |> iter (fun z -> z.RawParent <- Some this)

        interface IParameterContainer

        // { JSON 용
        /// 마지막 저장 db 에 대한 connection string
        member val LastConnectionString:string = null with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨

        member val Author        = System.Environment.UserName with get, set
        member val Version       = Version()  with get, set
        //member val LangVersion   = langVersion   |? Version()  with get, set
        //member val EngineVersion = engineVersion |? Version()  with get, set
        member val Description   = nullString with get, set

        // { Runtime/DB 용
        member val ActiveSystems = activeSystems |> toList
        member val PassiveSystems = passiveSystems |> toList
        member val Systems = (activeSystems @ passiveSystems) |> toList
        // } Runtime/DB 용


    type RtSystem internal(flows:RtFlow[], works:RtWork[], arrows:RtArrowBetweenWorks[], apiDefs:RtApiDef[]) =
        inherit RtUnique()

        interface IParameterContainer
        member val Flows = flows |> toList
        member val Works = works |> toList
        member val Arrows = arrows |> toList
        member val ApiDefs = apiDefs |> toList
        /// Origin Guid: 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null
        member val OriginGuid = noneGuid with get, set

        member x.Project = x.RawParent |-> (fun z -> z :?> RtProject) |?? (fun () -> getNull<RtProject>())

        member val Author        = Environment.UserName with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set


    type RtFlow() =
        inherit RtUnique()

        interface IDsFlow
        member x.System = x.RawParent |-> (fun z -> z :?> RtSystem) |?? (fun () -> getNull<RtSystem>())
        member x.Works = x.System.Works |> filter (fun w -> w.OptFlow = Some x)

    // see static member Create
    type RtWork internal(calls:RtCall seq, arrows:RtArrowBetweenCalls seq, optFlow:RtFlow option) as this =
        inherit RtUnique()
        do
            calls  |> iter (fun z -> z.RawParent <- Some this)
            arrows |> iter (fun z -> z.RawParent <- Some this)

        interface IDsWork
        member val Calls = calls |> toList
        member val Arrows = arrows |> toList
        member x.OptFlow = optFlow
        member x.System = x.RawParent |-> (fun z -> z :?> RtSystem) |?? (fun () -> getNull<RtSystem>())


    // see static member Create
    type RtCall(callType:DbCallType, apiCalls:RtApiCall seq, autoPre:string, safety:string) =
        inherit RtUnique()
        interface IDsCall
        member x.Work = x.RawParent |-> (fun z -> z :?> RtWork) |?? (fun () -> getNull<RtWork>())
        member val CallType = callType
        member val ApiCalls = apiCalls |> toList
        member val AutoPre = autoPre
        member val Safety = safety



    type RtApiCall(inAddress:string, outAddress:string, inSymbol:string, outSymbol:string, valueType:DbDataType, value:string) =
        inherit RtUnique()
        interface IDsApiCall
        member val InAddress  = inAddress
        member val OutAddress = outAddress
        member val InSymbol   = inSymbol
        member val OutSymbol  = outSymbol
        member val ValueType  = valueType
        member val Value = value

    type RtApiDef(isPush:bool) =
        inherit RtUnique()
        interface IDsApiDef
        member val IsPush = isPush


