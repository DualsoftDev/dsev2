namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System
open System.Runtime.Serialization

[<AutoOpen>]
module Interfaces =
    type Id = int64
    /// 기본 객체 인터페이스
    type IDsObject = interface end
    type IParameter = inherit IDsObject
    type IArrow = inherit IDsObject

    /// Guid, Name, DateTime
    type IUnique =
        inherit IDsObject


    type IDsSystem = inherit IDsObject
    type IDsFlow = inherit IDsObject
    type IDsWork = inherit IDsObject
    type IDsCall = inherit IDsObject


    [<AbstractClass>]
    type Unique(name:string, guid:Guid, ?id:Id, ?pGuid:Guid, ?dateTime:DateTime) =
        interface IUnique

        member val Id = id with get, set
        member val Name = name with get, set

        member val Guid:Guid = guid with get, set

        ///// Parent Guid : Json 저장시에는 container 의 parent 를 추적하면 되므로 json 에는 저장하지 않음
        [<JsonIgnore>] member val PGuid = pGuid with get, set
        [<JsonIgnore>] member val DateTime = dateTime with get, set

    [<AutoOpen>]
    module rec DsObjectModule =
        type Arrow<'T>(source:'T, target:'T, ?guid:Guid, ?id:Id, ?dateTime:DateTime) =
            inherit Unique(null, guid=(guid |? Guid.NewGuid()), ?id=id, ?dateTime=dateTime)

            interface IArrow
            member val Source = source with get, set
            member val Target = target with get, set


        type DsSystem(name, guid, flows:DsFlow[], works:DsWork[], arrows:Arrow<DsWork>[], ?id, ?dateTime:DateTime) =
            inherit Unique(name, guid, ?id=id, ?dateTime=dateTime)
            interface IDsSystem

            member val Flows = flows |> toList
            member val Works = works |> toList
            member val Arrows = arrows |> toList

        type DsFlow(name, guid, pGuid, works:DsWork[], ?id, ?dateTime:DateTime) =
            inherit Unique(name, guid, pGuid=pGuid, ?id=id, ?dateTime=dateTime)

            let mutable works = if isNull works then [||] else works
            interface IDsFlow
            member internal x.forceSetWorks(ws:DsWork[]) = works <- ws
            [<JsonIgnore>] member x.Works = works
            [<JsonProperty("WorksGuids")>]
            member val internal WorksGuids: Guid[] = works |-> _.Guid |> toArray with get, set

        type DsWork(name, guid, pGuid, calls:DsCall[], arrows:Arrow<DsCall>[], optFlowGuid:Guid option, ?id, ?dateTime:DateTime) =
            inherit Unique(name, guid, pGuid=pGuid, ?id=id, ?dateTime=dateTime)

            let mutable optFlowGuid = optFlowGuid
            interface IDsWork
            member val Arrows = arrows |> toList
            member x.Calls = calls

            [<JsonIgnore>] member x.OptFlowGuid with get() = optFlowGuid and set v = optFlowGuid <- v
            [<JsonProperty>] member val internal FlowGuid = optFlowGuid |-> toString |? null with get, set

        type DsCall(name, guid, pGuid, ?id, ?dateTime:DateTime) =
            inherit Unique(name, guid, pGuid=pGuid, ?id=id, ?dateTime=dateTime)
            interface IDsCall



        // { OnDeserializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.
        type DsSystem with
            [<OnDeserialized>]
            member x.OnDeserializedMethod(ctx: StreamingContext) =
                // System에 대한 serialize 종료 시점:
                let flows = ctx.DDic.Get<ResizeArray<DsFlow>>("flows")
                let works = ctx.DDic.Get<ResizeArray<DsWork>>("works")

                // flow 가 가진 WorksGuids 에 해당하는 work 들을 모아서 flow.Works 에 instance collection 으로 저장
                for f in flows do
                    f.PGuid <- Some x.Guid
                    let fWorks = works |> filter (fun w -> f.WorksGuids |> Seq.contains w.Guid) |> toArray
                    for w in fWorks do
                        w.OptFlowGuid <- Some f.Guid

                    f.forceSetWorks fWorks

                // works 의 Parent 를 this(system) 으로 설정
                for w in works do
                    w.PGuid <- Some x.Guid
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
                    c.PGuid <- Some x.Guid
                calls.Clear()


        type DsCall with
            [<OnDeserializing>]
            member x.OnDeserializingMethod(ctx: StreamingContext) =
                let calls = ctx.DDic.Get<ResizeArray<DsCall>>("calls") |> tee (fun xs -> xs.Add x)
                ()  // call 목록 수집 -> Dynamic Dictionary 에 저장

        // } OnDeserializ-[ing/ed] : 반드시 해당 type 과 동일 파일, 동일 module 에 있어야 실행 됨.





        type IEdObject = interface end
        type IEdSystem = inherit IEdObject inherit IDsSystem
        type IEdFlow   = inherit IEdObject inherit IDsFlow
        type IEdWork   = inherit IEdObject inherit IDsWork
        type IEdCall   = inherit IEdObject inherit IDsCall

        //{ 편집 가능한 버젼
        type EdSystem private (name:string, flows:ResizeArray<EdFlow>, works:ResizeArray<EdWork>, arrows:Arrow<EdWork> seq,  guid:Guid, dateTime:DateTime, ?id) =
            inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id)
            interface IEdSystem
            member x.Flows = flows |> toArray
            member x.Works = works |> toArray
            member x.Arrows = arrows
            member x.AddFlows(fs:EdFlow seq) =
                flows.AddRange(fs)
                fs |> iter (fun f -> f.OptParent <- Some x)
            member x.AddWorks(ws:EdWork seq) =
                works.AddRange(ws)
                ws |> iter (fun w -> w.OptParent <- Some x)

            static member Create(name:string, ?flows:EdFlow seq, ?works:EdWork seq, ?arrows:Arrow<EdWork> seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
                let guid = guid |? Guid.NewGuid()
                let dateTime = dateTime |? DateTime.UtcNow
                let flows = flows |? Seq.empty |> ResizeArray
                let works = works |? Seq.empty |> ResizeArray
                let arrows = arrows |? Seq.empty |> ResizeArray
                EdSystem(name, flows, works, arrows, guid, dateTime)

        type EdFlow private (name:string, guid:Guid, dateTime:DateTime, ?id, ?parent:EdSystem) =
            inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id)
            interface IEdFlow
            member val OptParent = parent with get, set
            static member Create(name, ?id, ?guid, ?dateTime) =
                let guid = guid |? Guid.NewGuid()
                let dateTime = dateTime |? DateTime.UtcNow
                EdFlow(name, guid, dateTime, ?id=id)

            member x.AddWorks(ws:EdWork seq) =
                ws |> iter (fun w -> w.OptOwnerFlow <- Some x)
            member x.Works = //x.OptParent |> map _.Works //|> choose id
                match x.OptParent with
                | Some p -> p.Works |> filter (fun w -> w.OptOwnerFlow = Some x) |> toArray
                | None -> failwith "Parent is not set. Cannot get works from flow."


        type EdWork private(name:string, guid:Guid, dateTime:DateTime, calls:ResizeArray<EdCall>, arrows:ResizeArray<Arrow<EdCall>>, ?parent:EdSystem, ?ownerFlow:#Unique, ?id) =
            inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime)
            interface IEdWork
            member val OptOwnerFlow = ownerFlow with get, set
            member x.Calls = calls |> toArray
            member x.Arrows = arrows
            member val OptParent = parent with get, set

            member x.AddCalls(cs:EdCall seq) =
                calls.AddRange(cs)
                cs |> iter (fun c -> c.OptParent <- Some x)

            static member Create(name:string, ?parent:EdSystem, ?calls:EdCall seq, ?ownerFlow:#Unique, ?arrows:Arrow<EdCall> seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
                let guid = guid |? Guid.NewGuid()
                let dateTime = dateTime |? DateTime.UtcNow
                let calls = calls |? Seq.empty |> ResizeArray
                let arrows = arrows |? Seq.empty |> ResizeArray
                EdWork(name, guid, dateTime, calls, arrows, ?parent=parent, ?ownerFlow=ownerFlow, ?id=id)


        type EdCall private(name:string, guid:Guid, dateTime:DateTime, ?parent:EdWork, ?id) =
            inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime)
            interface IEdCall
            member val OptParent = parent with get, set

            static member Create(name:string, ?parent:EdWork, ?id, ?guid:Guid, ?dateTime:DateTime) =
                let guid = guid |? Guid.NewGuid()
                let dateTime = dateTime |? DateTime.UtcNow
                EdCall(name, guid, dateTime, ?parent=parent, ?id=id)


        //} 편집 가능한 버젼


