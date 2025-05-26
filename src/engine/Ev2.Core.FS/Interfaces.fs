namespace Ev2.Core.FS

open System
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Base
open Dual.Common.Core.FS

[<AutoOpen>]
module Interfaces =
    type Id = int   //int64
    /// 기본 객체 인터페이스
    type IDsObject  = interface end
    type IParameter = inherit IDsObject
    type IParameterContainer = inherit IDsObject
    type IArrow     = inherit IDsObject

    /// Guid, Name, DateTime
    type IUnique    = inherit IDsObject


    type IDsProject = inherit IDsObject
    type IDsSystem  = inherit IDsObject
    type IDsFlow    = inherit IDsObject
    type IDsWork    = inherit IDsObject
    type IDsCall    = inherit IDsObject


    let internal minDate      = DateTime.MinValue
    let internal nullableId   = Nullable<Id>()
    let internal nullVersion  = null:Version
    let internal nullString   = null:string
    let internal nullableGuid = Nullable<Guid>()
    let internal emptyGuid    = Guid.Empty
    let internal newGuid()    = Guid.NewGuid()
    let internal s2guid (s:string) = Guid.Parse s
    let internal guid2str (g:Guid) = g.ToString("D")

    let mutable fwdOnSerializingDs:  IDsObject->unit = let dummy (dsObj:IDsObject) = failwithlog "Should be reimplemented." in dummy
    let mutable fwdOnDeserializedDs: IDsObject->unit = let dummy (dsObj:IDsObject) = failwithlog "Should be reimplemented." in dummy

    let internal now() = if AppSettings.TheAppSettings.UseUtcTime then DateTime.UtcNow else DateTime.Now

    [<AbstractClass>]
    type Unique(name:string, guid:Guid, dateTime:DateTime, ?id:Id, ?parent:Unique) as this =
        interface IUnique

        internal new() = Unique(nullString, emptyGuid, minDate, ?id=None, ?parent=None)

        /// Database 의 primary id key.  Database 에 삽입시 생성
        [<JsonIgnore>] member val Id = id with get, set
        [<JsonProperty(Order = -99)>] member val Name = name with get, set
        /// JSON 파일에 대한 comment.  눈으로 debugging 용도.  code 에서 사용하지 말 것.
        [<JsonProperty(Order = -98)>] member val private Type = this.GetType().Name

        /// Guid: 메모리에 최초 객체 생성시 생성
        [<JsonProperty(Order = -98)>] member val Guid:Guid = guid with get, set

        /// DateTime: 메모리에 최초 객체 생성시 생성
        [<JsonProperty(Order = -97)>] member val DateTime = dateTime with get, set

        /// 자신의 container 에 해당하는 parent DS 객체.  e.g call -> work -> system -> project, flow -> system
        [<JsonIgnore>] member val RawParent = parent with get, set

        /// Parent Guid : Json 저장시에는 container 의 parent 를 추적하면 되므로 json 에는 저장하지 않음
        [<JsonIgnore>] member x.PGuid = x.RawParent |-> _.Guid

        /// DB 저장시의 primary key id.  DB read/write 수행한 경우에만 Non-null
        [<JsonProperty(Order = -100)>] member val internal DbId = id |> Option.toNullable with get, set

        member x.Import(src:Unique) =
            x.Id        <- src.Id
            x.Name      <- src.Name
            x.Guid      <- src.Guid
            x.DateTime  <- src.DateTime
            x.RawParent <- src.RawParent

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

    /// Arrow 를 JSON 으로 저장하기 위한 DTO
    type DtoArrow(guid:Guid, id:Id option, source:Guid, target:Guid, dateTime:DateTime) =
        interface IArrow
        internal new () = DtoArrow(emptyGuid, None, emptyGuid, emptyGuid, minDate)
        member val DbId     = id |> Option.toNullable with get, set
        member val Guid     = guid     with get, set
        member val Source   = source   with get, set
        member val Target   = target   with get, set
        member val DateTime = dateTime with get, set

    type DsProject(name, guid, activeSystems:DsSystem[], passiveSystems:DsSystem[], dateTime:DateTime, ?id, ?author, ?version, (*?langVersion, ?engineVersion,*) ?description) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)

        interface IParameterContainer

        new() = DsProject(null, emptyGuid, [||], [||], minDate, ?id=None)

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


    type DsSystem private(name, guid, flows:DsFlow[], works:DsWork[], arrows:ArrowBetweenWorks[], dateTime:DateTime, ?originGuid:Guid, ?id, ?author, ?langVersion, ?engineVersion, ?description) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)

        internal new() = DsSystem(nullString, emptyGuid, [||], [||], [||], minDate)
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

        static member Create(name, guid, flows:DsFlow[], works:DsWork[], arrows:ArrowBetweenWorks[], dateTime:DateTime, ?originGuid, ?id, ?author, ?langVersion, ?engineVersion, ?description) =
            DsSystem(name, guid, flows, works, arrows, dateTime, ?originGuid=originGuid, ?id=id, ?author=author, ?langVersion=langVersion, ?engineVersion=engineVersion, ?description=description)
            |> tee (fun z ->
                flows  |> iter (fun y -> y.RawParent <- Some z)
                works  |> iter (fun y -> y.RawParent <- Some z)
                arrows |> iter (fun y -> y.RawParent <- Some z) )


    type DsFlow(name, guid, dateTime:DateTime, ?id) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)

        internal new() = DsFlow(null, emptyGuid, minDate, ?id=None)
        interface IDsFlow
        [<JsonIgnore>] member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())
        [<JsonProperty>] member val internal DtoArrows:DtoArrow list = [] with get, set
        [<JsonIgnore>] member x.Works = x.System.Works |> filter (fun w -> w.OptFlow = Some x)

    type DsWork private(name, guid, calls:DsCall seq, arrows:ArrowBetweenCalls seq, optFlow:DsFlow option, dateTime:DateTime, ?id) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)

        interface IDsWork
        new() = DsWork(null, emptyGuid, Seq.empty, Seq.empty, None, minDate, ?id=None)
        member val Calls = calls |> toList
        member val Arrows = arrows |> toList
        member x.OptFlow = optFlow
        [<JsonIgnore>] member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())

        static member Create(name, guid, calls:DsCall seq, arrows:ArrowBetweenCalls seq, optFlow:DsFlow option, dateTime:DateTime, ?id) =
            let calls = calls |> toList
            let arrows = arrows |> toList
            DsWork(name, guid, calls, arrows, optFlow, dateTime, ?id=id)
            |> tee (fun z ->
                noop()
                calls   |> iter (fun y -> y.RawParent <- Some z)
                arrows  |> iter (fun y -> y.RawParent <- Some z)
                optFlow |> iter (fun y -> y.RawParent <- Some z) )




    type DsCall(name, guid, dateTime:DateTime, ?id) =
        inherit DsUnique(name, guid, ?id=id, dateTime=dateTime)
        interface IDsCall
        [<JsonIgnore>] member x.Work = x.RawParent |-> (fun z -> z :?> DsWork) |?? (fun () -> getNull<DsWork>())



    // { On(De)serializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.
    type DsProject with
        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnSerializingDs x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnDeserializedDs x

    type DsSystem with
        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnSerializingDs x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnDeserializedDs x
    // } On(De)serializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.


