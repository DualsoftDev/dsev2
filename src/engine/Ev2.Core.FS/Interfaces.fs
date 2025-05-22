namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System
open System.Runtime.Serialization

[<AutoOpen>]
module Interfaces =
    type Id = int   //int64
    /// 기본 객체 인터페이스
    type IDsObject  = interface end
    type IParameter = inherit IDsObject
    type IArrow     = inherit IDsObject

    /// Guid, Name, DateTime
    type IUnique =
        inherit IDsObject


    type IDsProject = inherit IDsObject
    type IDsSystem  = inherit IDsObject
    type IDsFlow    = inherit IDsObject
    type IDsWork    = inherit IDsObject
    type IDsCall    = inherit IDsObject

    let mutable fwdOnSerializing: IDsObject->unit = let dummy (dsObj:IDsObject) = failwithlog "Should be reimplemented." in dummy
    let mutable fwdOnDeserialized:  IDsObject->unit = let dummy (dsObj:IDsObject) = failwithlog "Should be reimplemented." in dummy

    let internal now() = if AppSettings.TheAppSettings.UseUtcTime then DateTime.UtcNow else DateTime.Now

    [<AbstractClass>]
    type Unique(name:string, guid:Guid, dateTime:DateTime, ?id:Id, ?pGuid:Guid, ?parent:Unique) =
        interface IUnique

        /// Database 의 primary id key.  Database 에 삽입시 생성
        [<JsonIgnore>] member val Id = id with get, set
        member val Name = name with get, set

        /// Guid: 메모리에 최초 객체 생성시 생성
        member val Guid:Guid = guid with get, set
        /// DateTime: 메모리에 최초 객체 생성시 생성
        member val DateTime = dateTime with get, set

        [<JsonIgnore>] member val RawParent = parent with get, set
        /// Parent Guid : Json 저장시에는 container 의 parent 를 추적하면 되므로 json 에는 저장하지 않음
        [<JsonIgnore>] member x.PGuid = x.RawParent |-> _.Guid
        [<JsonProperty>] member val internal DbId = id |> Option.toNullable with get, set

[<AutoOpen>]
module rec DsObjectModule =
    [<AbstractClass>]
    type Arrow<'T when 'T :> Unique>(source:'T, target:'T, dateTime:DateTime, guid:Guid, ?id:Id) =
        inherit Unique(null, guid, dateTime, ?id=id)

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
        member val Id = id |> Option.toNullable with get, set
        member val Guid = guid with get, set
        member val Source = source with get, set
        member val Target = target with get, set
        member val DateTime = dateTime with get, set

    type DsProject(name, guid, activeSystems:DsSystem[], passiveSystems:DsSystem[], dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)
        interface IDsProject

        member val ActiveSystems = activeSystems |> toList
        member val PassiveSystems = passiveSystems |> toList
        [<JsonIgnore>] member x.Systems = x.ActiveSystems @ x.PassiveSystems

    type DsSystem(name, guid, flows:DsFlow[], works:DsWork[], arrows:ArrowBetweenWorks[], dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)
        let arrows = if isNull arrows then [||] else arrows

        interface IDsSystem

        member val Flows = flows |> toList
        member val Works = works |> toList
        [<JsonProperty>] member val internal DtoArrows:DtoArrow list = [] with get, set
        [<JsonIgnore>] member val Arrows = arrows |> toList with get, set
        [<JsonIgnore>] member x.Project = x.RawParent |-> (fun z -> z :?> DsProject) |?? (fun () -> getNull<DsProject>())

    type DsFlow(name, guid, pGuid, works:DsWork[], (*arrows:ArrowBetweenWorks[],*) dateTime:DateTime, ?id) =
        inherit Unique(name, guid, pGuid=pGuid, ?id=id, dateTime=dateTime)

        let mutable works = if isNull works then [||] else works
        //let arrows = if isNull arrows then [||] else arrows

        interface IDsFlow
        /// Flow 의 works.  flow 가 직접 work 를 child 로 갖지 않고, id 만 가지므로, deserialize 이후에 강제로 설정할 때 필요.
        member internal x.forceSetWorks(ws:DsWork[]) = works <- ws
        [<JsonIgnore>] member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())
        [<JsonIgnore>] member x.Works = works   // JSON 에는 저장하지 않고, 대신 WorksGuids 를 저장하여 추적함
        //[<JsonIgnore>] member val Arrows = arrows |> toList with get, set       // JSON only set
        [<JsonProperty>] member val internal DtoArrows:DtoArrow list = [] with get, set
        [<JsonProperty("WorksGuids")>] member val internal WorksGuids: Guid[] = works |-> _.Guid |> toArray with get, set

    type DsWork(name, guid, pGuid, calls:DsCall[], arrows:ArrowBetweenCalls[], optFlowGuid:Guid option, dateTime:DateTime, ?id) =
        inherit Unique(name, guid, pGuid=pGuid, ?id=id, dateTime=dateTime)

        let arrows = if isNull arrows then [||] else arrows
        let mutable optFlowGuid = optFlowGuid
        interface IDsWork
        member x.Calls = calls
        [<JsonProperty>] member val internal DtoArrows:DtoArrow list = [] with get, set
        [<JsonIgnore>] member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())
        [<JsonIgnore>] member val Arrows = arrows |> toList with get, set

        [<JsonIgnore>] member internal x.OptFlowGuid with get() = optFlowGuid and set v = optFlowGuid <- v
        [<JsonProperty>] member val internal FlowGuid = optFlowGuid |-> toString |? null with get, set

    type DsCall(name, guid, pGuid, dateTime:DateTime, ?id) =
        inherit Unique(name, guid, pGuid=pGuid, ?id=id, dateTime=dateTime)
        interface IDsCall
        [<JsonIgnore>] member x.Work = x.RawParent |-> (fun z -> z :?> DsWork) |?? (fun () -> getNull<DsWork>())



    // { On(De)serializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.
    type DsProject with
        [<OnSerializing>]   member x.OnSerializingMethod(ctx: StreamingContext) = fwdOnSerializing x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnDeserialized x

    type DsSystem with
        [<OnSerializing>] member x.OnSerializingMethod(ctx: StreamingContext) = fwdOnSerializing x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnDeserialized x
    // } On(De)serializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.



[<AutoOpen>]
module DsObjectUtilsModule =
    type Unique with
        member x.EnumerateDsObjects(?includeMe): Unique list =
            seq {
                let includeMe = includeMe |? true
                if includeMe then
                    yield x
                match x with
                | :? DsProject as prj ->
                    yield! prj.Systems |> Seq.bind(_.EnumerateDsObjects())
                | :? DsSystem as sys ->
                    yield! sys.Works |> Seq.bind(_.EnumerateDsObjects())
                    yield! sys.Flows |> Seq.bind(_.EnumerateDsObjects())
                | :? DsWork as work ->
                    yield! work.Calls |> Seq.bind(_.EnumerateDsObjects())
                    yield! work.Arrows|> Seq.bind(_.EnumerateDsObjects())
                //| :? DsCall as call ->
                //    yield! (call.Pa >>= (fun z -> z.EnumerateDsObjects())
                | _ ->
                    tracefn $"Skipping {(x.GetType())} in EnumerateDsObjects"
                    ()
            } |> List.ofSeq

    type Unique with
        member x.EnumerateAncestors(?includeMe): Unique list = [
            let includeMe = includeMe |? true
            if includeMe then
                yield x
            match x.RawParent with
            | Some parent ->
                yield! parent.EnumerateAncestors()
            | None -> ()
        ]


