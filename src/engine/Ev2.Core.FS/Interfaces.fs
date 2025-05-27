namespace Ev2.Core.FS

open System

open Dual.Common.Base
open Dual.Common.Core.FS



[<AutoOpen>]
module rec DsObjectModule =
    [<AbstractClass>]
    type DsUnique(name, guid, dateTime, ?id:Id, ?parent:Unique) =
        inherit Unique(name, guid, dateTime, ?id=id, ?parent=parent)
        do
            assert(parent |-> (fun p -> p :? DsUnique) |? true)


    [<AbstractClass>]
    type Arrow<'T when 'T :> Unique>(source:'T, target:'T, dateTime:DateTime, guid:Guid, ?id:Id) =
        inherit DsUnique(null, guid, dateTime, ?id=id)

        interface IArrow
        member val Source = source with get, set
        member val Target = target with get, set

    /// Call 간 화살표 연결.  Work 내에 존재
    type ArrowBetweenCalls(guid:Guid, source:DsCall, target:DsCall, dateTime:DateTime, ?id:Id) =
        inherit Arrow<DsCall>(source, target, dateTime, guid, ?id=id)

    /// Work 간 화살표 연결.  System 이나 Flow 내에 존재
    type ArrowBetweenWorks(guid:Guid, source:DsWork, target:DsWork, dateTime:DateTime, ?id:Id) =
        inherit Arrow<DsWork>(source, target, dateTime, guid, ?id=id)


    type DsProject(name, guid, activeSystems:DsSystem[], passiveSystems:DsSystem[], dateTime:DateTime, ?id, ?author, ?version, (*?langVersion, ?engineVersion,*) ?description) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)

        interface IParameterContainer

        //new() = DsProject(null, emptyGuid, [||], [||], minDate, ?id=None)
        // { JSON 용
        /// 마지막 저장 db 에 대한 connection string
        member val LastConnectionString:string = null with get, set // DB 연결 문자열.  JSON 저장시에는 사용하지 않음.  DB 저장시에는 사용됨

        member val Author        = author        |? System.Environment.UserName with get, set
        member val Version       = version       |? Version()  with get, set
        //member val LangVersion   = langVersion   |? Version()  with get, set
        //member val EngineVersion = engineVersion |? Version()  with get, set
        member val Description   = description   |? nullString with get, set

        // { Runtime/DB 용
        member val ActiveSystems = activeSystems |> toList
        member val PassiveSystems = passiveSystems |> toList
        member val Systems = (activeSystems @ passiveSystems) |> toList
        // } Runtime/DB 용


    type DsSystem internal(name, guid, flows:DsFlow[], works:DsWork[], arrows:ArrowBetweenWorks[], dateTime:DateTime,
            ?originGuid:Guid, ?id, ?author, ?langVersion, ?engineVersion, ?description
    ) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)

        //internal new() = DsSystem(nullString, emptyGuid, [||], [||], [||], minDate)
        interface IParameterContainer

        member val Flows = flows |> toList
        member val Works = works |> toList
        member val Arrows = arrows |> toList
        /// Origin Guid: 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null
        member val OriginGuid = originGuid with get, set

        member x.Project = x.RawParent |-> (fun z -> z :?> DsProject) |?? (fun () -> getNull<DsProject>())

        member val Author        = author        |? Environment.UserName with get, set
        member val EngineVersion = engineVersion |? Version()  with get, set
        member val LangVersion   = langVersion   |? Version()  with get, set
        member val Description   = description   |? nullString with get, set


    type DsFlow(name, guid, dateTime:DateTime, ?id) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)

        //internal new() = DsFlow(null, emptyGuid, minDate, ?id=None)
        interface IDsFlow
        member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())
        member x.Works = x.System.Works |> filter (fun w -> w.OptFlow = Some x)

    // see static member Create
    type DsWork internal(name, guid, calls:DsCall seq, arrows:ArrowBetweenCalls seq, optFlow:DsFlow option, dateTime:DateTime, ?id) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)

        interface IDsWork
        //new() = DsWork(null, emptyGuid, Seq.empty, Seq.empty, None, minDate, ?id=None)
        member val Calls = calls |> toList
        member val Arrows = arrows |> toList
        member x.OptFlow = optFlow
        member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())


    // see static member Create
    type DsCall(name, guid, callType:DbCallType, apiCalls:DsApiCall seq, dateTime:DateTime, ?id) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)
        interface IDsCall
        member x.Work = x.RawParent |-> (fun z -> z :?> DsWork) |?? (fun () -> getNull<DsWork>())
        member val CallType = callType
        member val ApiCalls = apiCalls |> toList


    type DsApiCall(guid, dateTime:DateTime, ?id) =
        inherit DsUnique(nullString, guid, ?id=id, dateTime=dateTime)
        interface IDsApiCall
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val ValueType  = DbDataType.None with get, set
        member val Value = nullString with get, set


