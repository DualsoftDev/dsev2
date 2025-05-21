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

    let internal now() = if AppSettings.TheAppSettings.UseUtcTime then DateTime.UtcNow else DateTime.Now

    [<AbstractClass>]
    type Unique(name:string, guid:Guid, dateTime:DateTime, ?id:Id, ?pGuid:Guid, ?parent:Unique) =
        interface IUnique

        /// Database 의 primary id key.  Database 에 삽입시 생성
        member val Id = id with get, set
        member val Name = name with get, set

        /// Guid: 메모리에 최초 객체 생성시 생성
        member val Guid:Guid = guid with get, set
        /// DateTime: 메모리에 최초 객체 생성시 생성
        member val DateTime = dateTime with get, set

        [<JsonIgnore>] member val RawParent = parent with get, set
        /// Parent Guid : Json 저장시에는 container 의 parent 를 추적하면 되므로 json 에는 저장하지 않음
        [<JsonIgnore>] member x.PGuid = x.RawParent |-> _.Guid

[<AutoOpen>]
module rec DsObjectModule =
    type Arrow<'T>(source:'T, target:'T, dateTime:DateTime, ?guid:Guid, ?id:Id) =
        inherit Unique(null, guid |? Guid.NewGuid(), dateTime, ?id=id)

        interface IArrow
        member val Source = source with get, set
        member val Target = target with get, set


    type DsProject(name, guid, activeSystems:DsSystem[], passiveSystems:DsSystem[], dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)
        interface IDsProject

        member val ActiveSystems = activeSystems |> toList
        member val PassiveSystems = passiveSystems |> toList
        member x.Systems = x.ActiveSystems @ x.PassiveSystems

    type DsSystem(name, guid, flows:DsFlow[], works:DsWork[], arrows:Arrow<DsWork>[], dateTime:DateTime, ?id) =
        inherit Unique(name, guid, ?id=id, dateTime=dateTime)
        interface IDsSystem

        member val Flows = flows |> toList
        member val Works = works |> toList
        member val Arrows = arrows |> toList

    type DsFlow(name, guid, pGuid, works:DsWork[], dateTime:DateTime, ?id) =
        inherit Unique(name, guid, pGuid=pGuid, ?id=id, dateTime=dateTime)

        let mutable works = if isNull works then [||] else works
        interface IDsFlow
        member internal x.forceSetWorks(ws:DsWork[]) = works <- ws
        [<JsonIgnore>] member x.Works = works
        [<JsonProperty("WorksGuids")>]
        member val internal WorksGuids: Guid[] = works |-> _.Guid |> toArray with get, set

    type DsWork(name, guid, pGuid, calls:DsCall[], arrows:Arrow<DsCall>[], optFlowGuid:Guid option, dateTime:DateTime, ?id) =
        inherit Unique(name, guid, pGuid=pGuid, ?id=id, dateTime=dateTime)

        let mutable optFlowGuid = optFlowGuid
        interface IDsWork
        member val Arrows = arrows |> toList
        member x.Calls = calls

        [<JsonIgnore>] member x.OptFlowGuid with get() = optFlowGuid and set v = optFlowGuid <- v
        [<JsonProperty>] member val internal FlowGuid = optFlowGuid |-> toString |? null with get, set

    type DsCall(name, guid, pGuid, dateTime:DateTime, ?id) =
        inherit Unique(name, guid, pGuid=pGuid, ?id=id, dateTime=dateTime)
        interface IDsCall



    // { OnDeserializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.
    type DsProject with
        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            let systems = ctx.DDic.Get<ResizeArray<DsSystem>>("systems")

            // flow 가 가진 WorksGuids 에 해당하는 work 들을 모아서 flow.Works 에 instance collection 으로 저장
            for s in systems do
                s.RawParent <- Some x

    type DsSystem with
        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            // System에 대한 serialize 종료 시점:
            let flows = ctx.DDic.Get<ResizeArray<DsFlow>>("flows")
            let works = ctx.DDic.Get<ResizeArray<DsWork>>("works")

            // flow 가 가진 WorksGuids 에 해당하는 work 들을 모아서 flow.Works 에 instance collection 으로 저장
            for f in flows do
                f.RawParent <- Some x
                let fWorks = works |> filter (fun w -> f.WorksGuids |> Seq.contains w.Guid) |> toArray
                for w in fWorks do
                    w.OptFlowGuid <- Some f.Guid

                f.forceSetWorks fWorks

            // works 의 Parent 를 this(system) 으로 설정
            for w in works do
                w.RawParent <- Some x
            ()

    type DsFlow with
        [<OnDeserializing>]
        member x.OnDeserializingMethod(ctx: StreamingContext) =
            let flows = ctx.DDic.Get<ResizeArray<DsFlow>>("flows") |> tee (fun xs -> xs.Add x)
            ()  // flow 목록 수집 -> Dynamic Dictionary 에 저장

    type DsWork with
        [<OnDeserializing>]
        member x.OnDeserializingMethod(ctx: StreamingContext) =
            let works = ctx.DDic.Get<ResizeArray<DsWork>>("works") |> tee (fun xs -> xs.Add x)
            ()  // work 목록 수집 -> Dynamic Dictionary 에 저장

        [<OnDeserialized>]
        member x.OnDeserializedMethod(ctx: StreamingContext) =
            tracefn $"Add works with guid: {x.Guid}"
            // Work 하나에 대한 serialize 종료 시점:
            // 현재까지 수집된 call 목록에 대해 parent 를 this 로 설정하고, call 목록을 clear 한다.
            let calls = ctx.DDic.Get<ResizeArray<DsCall>>("calls")
            for c in x.Calls do
                c.RawParent <- Some x
            calls.Clear()


    type DsCall with
        [<OnDeserializing>]
        member x.OnDeserializingMethod(ctx: StreamingContext) =
            let calls = ctx.DDic.Get<ResizeArray<DsCall>>("calls") |> tee (fun xs -> xs.Add x)
            ()  // call 목록 수집 -> Dynamic Dictionary 에 저장

    // } OnDeserializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.




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


