namespace Ev2.Core.FS

open System

open Dual.Common.Base
open Dual.Common.Core.FS



[<AutoOpen>]
module rec DsObjectModule =
    [<AbstractClass>]
    type DsUnique() = inherit Unique()


    [<AbstractClass>]
    type Arrow<'T when 'T :> Unique>(source:'T, target:'T) =
        inherit DsUnique()

        interface IArrow
        member val Source = source with get, set
        member val Target = target with get, set

    /// Call 간 화살표 연결.  Work 내에 존재
    type ArrowBetweenCalls(source:DsCall, target:DsCall) =
        inherit Arrow<DsCall>(source, target)

    /// Work 간 화살표 연결.  System 이나 Flow 내에 존재
    type ArrowBetweenWorks(source:DsWork, target:DsWork) =
        inherit Arrow<DsWork>(source, target)


    type DsProject(activeSystems:DsSystem[], passiveSystems:DsSystem[]) as this =
        inherit DsUnique()
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


    type DsSystem internal(flows:DsFlow[], works:DsWork[], arrows:ArrowBetweenWorks[]) =
        inherit DsUnique()

        interface IParameterContainer
        member val Flows = flows |> toList
        member val Works = works |> toList
        member val Arrows = arrows |> toList
        /// Origin Guid: 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null
        member val OriginGuid = noneGuid with get, set

        member x.Project = x.RawParent |-> (fun z -> z :?> DsProject) |?? (fun () -> getNull<DsProject>())

        member val Author        = Environment.UserName with get, set
        member val EngineVersion = Version()  with get, set
        member val LangVersion   = Version()  with get, set
        member val Description   = nullString with get, set


    type DsFlow() =
        inherit DsUnique()

        interface IDsFlow
        member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())
        member x.Works = x.System.Works |> filter (fun w -> w.OptFlow = Some x)

    // see static member Create
    type DsWork internal(calls:DsCall seq, arrows:ArrowBetweenCalls seq, optFlow:DsFlow option) as this =
        inherit DsUnique()
        do
            calls  |> iter (fun z -> z.RawParent <- Some this)
            arrows |> iter (fun z -> z.RawParent <- Some this)

        interface IDsWork
        member val Calls = calls |> toList
        member val Arrows = arrows |> toList
        member x.OptFlow = optFlow
        member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())


    // see static member Create
    type DsCall(callType:DbCallType, apiCalls:DsApiCall seq) =
        inherit DsUnique()
        interface IDsCall
        member x.Work = x.RawParent |-> (fun z -> z :?> DsWork) |?? (fun () -> getNull<DsWork>())
        member val CallType = callType
        member val ApiCalls = apiCalls |> toList


    type DsApiCall() =
        inherit DsUnique()
        interface IDsApiCall
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val ValueType  = DbDataType.None with get, set
        member val Value = nullString with get, set


