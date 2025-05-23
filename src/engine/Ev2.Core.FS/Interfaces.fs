namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System
open System.Runtime.Serialization
open System.Collections.Generic

[<AutoOpen>]
module Interfaces =
    type Id = int   //int64
    /// 기본 객체 인터페이스
    type IDsObject  = interface end
    type IParameter = inherit IDsObject
    type IArrow     = inherit IDsObject

    /// Guid, Name, DateTime
    type IUnique    = inherit IDsObject


    type IDsProject = inherit IDsObject
    type IDsSystem  = inherit IDsObject
    type IDsFlow    = inherit IDsObject
    type IDsWork    = inherit IDsObject
    type IDsCall    = inherit IDsObject


    let internal nullDate = DateTime.MinValue
    let internal nullGuid = Guid.Empty
    let internal nullId = Nullable<Id>()
    let internal newGuid() = Guid.NewGuid()

    let mutable fwdOnSerializing: IDsObject->unit = let dummy (dsObj:IDsObject) = failwithlog "Should be reimplemented." in dummy
    let mutable fwdOnDeserialized:  IDsObject->unit = let dummy (dsObj:IDsObject) = failwithlog "Should be reimplemented." in dummy

    let internal now() = if AppSettings.TheAppSettings.UseUtcTime then DateTime.UtcNow else DateTime.Now

    [<AbstractClass>]
    type Unique(name:string, guid:Guid, dateTime:DateTime, ?id:Id, ?parent:Unique, ?originGuid:Guid) =
        interface IUnique

        /// Database 의 primary id key.  Database 에 삽입시 생성
        [<JsonIgnore>] member val Id = id with get, set
        member val Name = name with get, set

        /// Guid: 메모리에 최초 객체 생성시 생성
        member val Guid:Guid = guid with get, set

        /// DateTime: 메모리에 최초 객체 생성시 생성
        member val DateTime = dateTime with get, set

        /// 자신의 container 에 해당하는 parent DS 객체.  e.g call -> work -> system -> project, flow -> system
        [<JsonIgnore>] member val RawParent = parent with get, set

        /// Parent Guid : Json 저장시에는 container 의 parent 를 추적하면 되므로 json 에는 저장하지 않음
        [<JsonIgnore>] member x.PGuid = x.RawParent |-> _.Guid

        member val OriginGuid = originGuid with get, set

        /// DB 저장시의 primary key id.  DB read/write 수행한 경우에만 Non-null
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
        internal new () = DtoArrow(nullGuid, None, nullGuid, nullGuid, nullDate)
        member val Id       = id |> Option.toNullable with get, set
        member val Guid     = guid     with get, set
        member val Source   = source   with get, set
        member val Target   = target   with get, set
        member val DateTime = dateTime with get, set

    type DsProject(name, guid, activeSystems:DsSystem[], passiveSystems:DsSystem[], dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)

        interface IDsProject
        new() = DsProject(null, nullGuid, [||], [||], nullDate, ?id=None)
        [<JsonProperty>] member val internal SystemPrototypes:DsSystem list = [] with get, set
        [<JsonProperty>] member val internal ActiveSystemGuids:string list = [] with get, set
        [<JsonProperty>] member val internal PassiveSystemGuids:string list = [] with get, set

        [<JsonIgnore>] member val ActiveSystems = activeSystems |> toList with get, set
        [<JsonIgnore>] member val PassiveSystems = passiveSystems |> toList with get, set
        [<JsonIgnore>] member x.Systems = x.ActiveSystems @ x.PassiveSystems

    type DsSystem(name, guid, flows:DsFlow[], works:DsWork[], arrows:ArrowBetweenWorks[], dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)
        let mutable flows  = if isNull flows  then [] else flows  |> toList
        let mutable works  = if isNull works  then [] else works  |> toList
        let mutable arrows = if isNull arrows then [] else arrows |> toList

        interface IDsSystem

        member val Flows = flows
        member val Works = works
        [<JsonProperty>] member val internal DtoArrows:DtoArrow list = [] with get, set
        [<JsonIgnore>] member val Arrows = arrows with get, set
        [<JsonIgnore>] member x.Project = x.RawParent |-> (fun z -> z :?> DsProject) |?? (fun () -> getNull<DsProject>())
        member internal x.forceSet(newFlows, newWorks, newArrows) =
            flows  <- newFlows |> List.ofSeq
            works  <- newWorks |> List.ofSeq
            arrows <- newArrows|> List.ofSeq
            x.DateTime <- now()

    type DsFlow(name, guid, works:DsWork[], dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)

        let works = works |> ResizeArray

        internal new() = DsFlow(null, nullGuid, [||], nullDate, ?id=None)
        interface IDsFlow
        /// Flow 의 works.  flow 가 직접 work 를 child 로 갖지 않고, id 만 가지므로, deserialize 이후에 강제로 설정할 때 필요.
        member internal x.forceSetWorks(ws:DsWork[]) = works.Clear(); works.AddRange ws
        [<JsonIgnore>] member x.Works = works |> toArray // // JSON 에는 저장하지 않고, 대신 WorksGuids 를 저장하여 추적함
        [<JsonIgnore>] member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())
        [<JsonProperty>] member val internal DtoArrows:DtoArrow list = [] with get, set
        [<JsonProperty("WorksGuids")>] member val internal WorksGuids: Guid[] = works |-> _.Guid |> toArray with get, set

    type DsWork(name, guid, calls:DsCall[], arrows:ArrowBetweenCalls[], optFlowGuid:Guid option, dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)

        let mutable optFlowGuid = optFlowGuid
        let arrows = if isNull arrows then [] else arrows |> List.ofSeq

        interface IDsWork
        member x.Calls = calls
        [<JsonProperty>] member val internal DtoArrows:DtoArrow list = [] with get, set
        [<JsonIgnore>] member x.System = x.RawParent |-> (fun z -> z :?> DsSystem) |?? (fun () -> getNull<DsSystem>())
        [<JsonIgnore>] member val Arrows = arrows with get, set

        [<JsonIgnore>] member internal x.OptFlowGuid with get() = optFlowGuid and set v = optFlowGuid <- v
        member val internal FlowGuid = optFlowGuid |-> toString |? null with get, set

    type DsCall(name, guid, dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)
        interface IDsCall
        [<JsonIgnore>] member x.Work = x.RawParent |-> (fun z -> z :?> DsWork) |?? (fun () -> getNull<DsWork>())



    // { On(De)serializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.
    type DsProject with
        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnSerializing x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnDeserialized x

    type DsSystem with
        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnSerializing x
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
                    yield! sys.Works   |> Seq.bind(_.EnumerateDsObjects())
                    yield! sys.Flows   |> Seq.bind(_.EnumerateDsObjects())
                    yield! sys.Arrows  |> Seq.bind(_.EnumerateDsObjects())
                | :? DsWork as work ->
                    yield! work.Calls  |> Seq.bind(_.EnumerateDsObjects())
                    yield! work.Arrows |> Seq.bind(_.EnumerateDsObjects())
                | :? DsCall as call ->
                    ()
                | _ ->
                    tracefn $"Skipping {(x.GetType())} in EnumerateDsObjects"
                    ()
            } |> List.ofSeq

        member x.EnumerateAncestors(?includeMe): Unique list = [
            let includeMe = includeMe |? true
            if includeMe then
                yield x
            match x.RawParent with
            | Some parent ->
                yield! parent.EnumerateAncestors()
            | None -> ()
        ]

[<AutoOpen>]
module rec DsObjectCopyModule =
    type CopyBag = {
        /// OldGuid -> NewGuid map
        Oldies: Dictionary<Guid, Unique>
        Newbies: Dictionary<Guid, Unique>
        Old2NewMap: Dictionary<Guid, Guid>
    } with
        member x.Add(old:Unique) =
            let newGuid = newGuid()
            x.Oldies.Add(old.Guid, old)
            x.Newbies.Add(newGuid, old)
            x.Old2NewMap.Add(old.Guid, newGuid)
            newGuid
        static member Create() =
            { Oldies = Dictionary<Guid, Unique>()
              Newbies = Dictionary<Guid, Unique>()
              Old2NewMap = Dictionary<Guid, Guid>() }

    type DsProject with
        member x.Copy(?bag:CopyBag) =
            let bag = bag |?? (fun () -> CopyBag.Create())
            let guid = bag.Add(x)

            let activeSystems  = x.ActiveSystems  |-> _.Copy(bag) |> toArray
            let passiveSystems = x.PassiveSystems |-> _.Copy(bag) |> toArray
            DsProject(x.Name, guid, activeSystems, passiveSystems, now())

    type DsSystem with
        member x.Copy(?bag:CopyBag) =
            let bag = bag |?? (fun () -> CopyBag.Create())
            let guid = bag.Add(x)
            let cc = DsSystem(x.Name, guid, [||], [||], [||], now())
            let flows  = x.Flows  |-> _.Copy(bag) |> toArray
            let works  = x.Works  |-> _.Copy(bag) |> toArray
            let arrows = x.Arrows |-> _.Copy(bag) |> toArray
            cc.forceSet(flows, works, arrows)
            cc.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)
            cc

    type DsFlow with
        member x.Copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let cc = DsFlow(x.Name, guid, [||], now())
            //let works =

            cc.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)
            cc

    type DsWork with
        member x.Copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let optFlowGuid = x.OptFlowGuid |-> fun z -> bag.Old2NewMap[z]
            DsWork(x.Name, guid, [||], [||], optFlowGuid, now())
            |> tee(fun z ->
                z.RawParent <- Some bag.Oldies[x.PGuid.Value]
                z.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid))

    type ArrowBetweenWorks with
        member x.Copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let source = bag.Oldies[x.Source.Guid] :?> DsWork
            let target = bag.Oldies[x.Target.Guid] :?> DsWork
            ArrowBetweenWorks(guid, source, target, now())
            |> tee(fun z -> z.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid))


    type ArrowBetweenCalls with
        member x.Copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let source = bag.Oldies[x.Source.Guid] :?> DsCall
            let target = bag.Oldies[x.Target.Guid] :?> DsCall
            ArrowBetweenCalls(guid, source, target, now())
            |> tee(fun z -> z.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid))

