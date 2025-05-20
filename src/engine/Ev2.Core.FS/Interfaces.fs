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

    let private toN = Option.toNullable
    let nullId = Nullable<int>()
    let nullGuid = Nullable<Guid>()
    let nullDateTime = Nullable<DateTime>()
    let toResizeArray (xs:'x seq) = ResizeArray(xs)

    [<AbstractClass>]
    //[<JsonObject(MemberSerialization = MemberSerialization.OptOut)>]
    type Unique(name:string, guid:Guid, ?id:Id, ?pGuid:Guid, ?dateTime:DateTime) =
        interface IUnique

        //new() = Unique(null, Guid.Empty)

        member val Id = id with get, set
        member val Name = name with get, set

        member val Guid:Guid = guid with get, set
        //member val PGuid:Guid option = pGuid with get, set
        //member val DateTime:DateTime option = dateTime with get, set


        //[<JsonIgnore>] member val Guid = guid with get, set
        ///// Parent Guid : Json 저장시에는 container 의 parent 를 추적하면 되므로 json 에는 저장하지 않음
        [<JsonIgnore>] member val PGuid = pGuid with get, set
        [<JsonIgnore>] member val DateTime = dateTime with get, set

        //// 직렬화 대상: Nullable<Id> or other serializable type
        //[<JsonProperty("DateTime", NullValueHandling = NullValueHandling.Ignore)>]
        //member val (*internal*) rawDateTime: Nullable<DateTime> = dateTime |> Option.toNullable with get, set

        //[<JsonProperty("Guid", NullValueHandling = NullValueHandling.Ignore)>]
        //member val (*internal*) rawGuid: Nullable<Guid> = Nullable guid with get, set

        //[<JsonProperty("PGuid", NullValueHandling = NullValueHandling.Ignore)>]
        //member val (*internal*) rawPGuid = pGuid |> Option.toNullable with get, set

        //[<OnDeserialized>]
        //member x.OnDeserializedMethod(ctx: StreamingContext) =
        //    x.DateTime <- x.rawDateTime |> Option.ofNullable
        //    x.Guid     <- x.rawGuid.Value
        //    x.PGuid    <- x.rawPGuid  |> Option.ofNullable



    [<AutoOpen>]
    module rec DsObjectModule =
        type Arrow<'T>(source:'T, target:'T, ?guid:Guid, ?id:Id, ?dateTime:DateTime) =
            inherit Unique(null, guid=(guid |? Guid.NewGuid()), ?id=id, ?dateTime=dateTime)
            //new() = Arrow<'T>(Unchecked.defaultof<'T>, Unchecked.defaultof<'T>, Guid.Empty)      // for JSON
            interface IArrow
            member val Source = source with get, set
            member val Target = target with get, set


        type DsSystem(name, guid, flows:DsFlow[], works:DsWork[], arrows:Arrow<DsWork>[], ?id, ?dateTime:DateTime) =
            inherit Unique(name, guid, ?id=id, ?dateTime=dateTime)
            interface IDsSystem
            //new() = DsSystem(null, Guid.Empty, [||], [||], [||])      // for JSON

            member val Flows = flows |> toList
            member val Works = works |> toList
            member val Arrows = arrows |> toList

        type DsFlow(name, guid, pGuid, works:DsWork[], ?id, ?dateTime:DateTime) =
            inherit Unique(name, guid, pGuid=pGuid, ?id=id, ?dateTime=dateTime)
            let mutable works = if isNull works then [||] else works
            //new() = DsFlow(null, Guid.Empty, Guid.Empty, [||])      // for JSON
            interface IDsFlow
            member internal x.forceSetWorks(ws:DsWork[]) = works <- ws
            [<JsonIgnore>] member x.Works = works
            [<JsonProperty("WorksGuids")>]
            member val internal WorksGuids: Guid[] = works |-> _.Guid |> toArray with get, set

        type DsWork(name, guid, pGuid, calls:DsCall[], arrows:Arrow<DsCall>[], ?flowGuid:Guid, ?id, ?dateTime:DateTime) =
            inherit Unique(name, guid, pGuid=pGuid, ?id=id, ?dateTime=dateTime)

            //new() = DsWork(null, Guid.Empty, Guid.Empty, [||], [||])      // for JSON
            interface IDsWork
            [<JsonIgnore>] member val OptFlowGuid = flowGuid with get, set //!! get, set 삭제
            member val Arrows = arrows |> toList
            member x.Calls = calls

            [<JsonProperty>]
            //[<JsonProperty("FlowGuid", NullValueHandling = NullValueHandling.Ignore)>]
            //member val internal rawFlowGuid = flowGuid |> Option.toNullable with get, set
            member val internal rawFlowGuid = flowGuid |-> toString |? null with get, set

        type DsCall(name, guid, pGuid, ?id, ?dateTime:DateTime) =
            inherit Unique(name, guid, pGuid=pGuid, ?id=id, ?dateTime=dateTime)
            interface IDsCall
            //new() = DsCall(null, Guid.Empty, Guid.Empty)      // for JSON







        // OnDeserializ-[ing/ed] : 반드시 동일 파일, 동일 module 에 있어야 실행 됨.
        type DsSystem with
            [<OnDeserializing>]
            member x.OnDeserializingMethod(ctx: StreamingContext) =
                ()
            [<OnDeserialized>]
            member x.OnDeserializedMethod(ctx: StreamingContext) =
                let flows = ctx.DDic.Get<ResizeArray<DsFlow>>("flows")
                let works = ctx.DDic.Get<ResizeArray<DsWork>>("works")
                //    |> filter (fun w -> x.WorkGuids |> Seq.contains w.Guid)
                //x.forceSetWorks works
                for f in flows do
                    f.PGuid <- Some x.Guid
                    let fWorks = works |> filter (fun w -> f.WorksGuids |> Seq.contains w.Guid) |> toArray
                    for w in fWorks do
                        w.OptFlowGuid <- Some f.Guid

                    f.forceSetWorks fWorks
                for w in works do
                    w.PGuid <- Some x.Guid
                ()


        type DsWork with
            [<OnDeserializing>]
            member x.OnDeserializingMethod(ctx: StreamingContext) =
                let works = ctx.DDic.Get<ResizeArray<DsWork>>("works") |> tee (fun xs -> xs.Add x)
                ()
            [<OnDeserialized>]
            member x.OnDeserializedMethod(ctx: StreamingContext) =
                tracefn $"Add works with guid: {x.Guid}"
                //let works = ctx.DDic.Get<ResizeArray<DsWork>>("works") |> tee (fun xs -> xs.Add x)
                let calls = ctx.DDic.Get<ResizeArray<DsCall>>("calls")
                for c in x.Calls do
                    c.PGuid <- Some x.Guid
                calls.Clear()

                //x.OptFlowGuid <- x.rawFlowGuid |> Option.ofNullable
                x.OptFlowGuid <-
                    match x.rawFlowGuid with
                    | null | "" -> None
                    | g -> Guid.Parse g |> Some


        type DsFlow with
            [<OnDeserializing>]
            member x.OnDeserializingMethod(ctx: StreamingContext) =
                let flows = ctx.DDic.Get<ResizeArray<DsFlow>>("flows") |> tee (fun xs -> xs.Add x)
                ()
            //[<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = ()


        type DsCall with
            [<OnDeserializing>]
            member x.OnDeserializingMethod(ctx: StreamingContext) =
                let calls = ctx.DDic.Get<ResizeArray<DsCall>>("calls") |> tee (fun xs -> xs.Add x)
                ()






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


