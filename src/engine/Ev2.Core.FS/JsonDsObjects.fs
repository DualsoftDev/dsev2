namespace Ev2.Core.FS

open System
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base

[<AutoOpen>]
module NewtonsoftJsonForwardDecls =
    type INjObject  = interface end
    type INjProject = inherit INjObject inherit IDsProject
    type INjSystem  = inherit INjObject inherit IDsSystem
    type INjFlow    = inherit INjObject inherit IDsFlow
    type INjWork    = inherit INjObject inherit IDsWork
    type INjCall    = inherit INjObject inherit IDsCall
    type INjArrow   = inherit INjObject


    let mutable fwdOnSerializing:  INjObject option->INjObject->unit = let dummy (parent:INjObject option) (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy
    let mutable fwdOnDeserialized: INjObject option->INjObject->unit = let dummy (parent:INjObject option) (dsObj:INjObject) = failwithlog "Should be reimplemented." in dummy

/// Newtonsoft Json 호환 버젼
[<AutoOpen>]
module rec NewtonsoftJsonObjects =

    [<AbstractClass>]
    type NjUnique() =
        inherit Unique()
        [<JsonIgnore>] member val DsObject:Unique = getNull<Unique>() with get, set
        [<JsonIgnore>] member x.DsRawParent:Unique option = x.DsObject.RawParent
        [<JsonIgnore>] member x.NjRawParent:Unique option = x.RawParent
        member x.Import(src:Unique) =
            match src with
            | :? DsUnique -> x.DsObject <- src
            | :? NjUnique as z ->
                failwith "ERROR"
                x.RawParent <- z.RawParent
            base.Import src

    type NjProject() =
        inherit NjUnique()
        interface INjProject

        //member val DbId = Nullable<Id>() with get, set
        member val LastConnectionString = null:string with get, set
        member val Author = null:string with get, set
        member val Version = Version() with get, set
        member val SystemPrototypes: NjSystem[] = [||] with get, set

        member val ActiveSystemGuids:Guid[] = [||] with get, set
        member val PassiveSystemGuids:Guid[] = [||] with get, set

        static member FromDs(ds:DsProject) =
            NjProject() |> tee (fun z -> z.Import ds)

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnSerializing  None x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnDeserialized None x


    type NjSystem() =
        inherit NjUnique()
        interface INjSystem


        member val Flows:NjFlow[] = [||] with get, set
        member val Works:NjWork[] = [||] with get, set
        member val Arrows:NjArrow[] = [||] with get, set
        member val OriginGuid = Option<Guid>.None with get, set

        [<OnSerializing>]  member x.OnSerializingMethod (ctx: StreamingContext) = fwdOnSerializing  (x.RawParent >>= tryCast<INjObject>) x
        [<OnDeserialized>] member x.OnDeserializedMethod(ctx: StreamingContext) = fwdOnDeserialized (x.RawParent >>= tryCast<INjObject>) x
        static member FromDs(ds:DsSystem) =
            NjSystem()
            |> tee (fun z ->
                z.Import ds
                z.Flows  <- ds.Flows  |-> NjFlow.FromDs  |> toArray
                z.Arrows <- ds.Arrows |-> NjArrow.FromDs |> toArray
                z.Works  <- ds.Works  |-> NjWork.FromDs  |> toArray
            )

    type NjFlow () =
        inherit NjUnique()
        interface INjFlow

        static member FromDs(ds:DsFlow) =
            NjFlow() |> tee (fun z -> z.Import ds)

    type NjWork () =
        inherit NjUnique()
        interface INjWork
        member val FlowGuid = null:string with get, set
        member val Calls: NjCall[] = [||] with get, set
        member val Arrows:NjArrow[] = [||] with get, set
        static member FromDs(ds:DsWork) =
            NjWork()
            |> tee (fun z ->
                z.Import ds
                z.Calls <- ds.Calls |-> NjCall.FromDs |> toArray
                z.Arrows <- ds.Arrows |-> NjArrow.FromDs |> toArray
                z.FlowGuid <- ds.OptFlow |-> (fun flow -> guid2str flow.Guid) |? null
            )

    type NjArrow() =
        inherit NjUnique()
        interface INjArrow
        member val Source = null:string with get, set
        member val Target = null:string with get, set
        static member FromDs(ds:IArrow) =
            NjArrow()
            |> tee (fun z ->
                z.Import (ds :?> Unique)
                z.Source <- guid2str (ds.GetSource().Guid)
                z.Target <- guid2str (ds.GetTarget().Guid)
            )

    type NjCall() =
        inherit NjUnique()
        interface INjCall

        static member FromDs(ds:DsCall) =
            NjCall() |> tee (fun z -> z.Import ds)


    /// JSON 쓰기 전에 메모리 구조에 전처리 작업
    let rec internal onSerializing (njParent:INjObject option) (njObj:INjObject) =
        match njObj with
        | :? NjUnique as uniq ->
            uniq.Import uniq.DsObject
            uniq.DbId <- uniq.Id |> Option.toNullable
        | _ -> ()

        match njObj with
        | :? NjProject as nj ->
            let ds = nj.DsObject :?> DsProject
            nj.SystemPrototypes <-
                let originals, copies = ds.ActiveSystems |> partition (fun s -> s.OriginGuid.IsNone)
                let distinctCopies = copies |> distinctBy _.Guid
                originals @ distinctCopies |-> NjSystem.FromDs |> toArray
            nj.ActiveSystemGuids  <- ds.ActiveSystems  |-> _.Guid |> toArray
            nj.PassiveSystemGuids <- ds.PassiveSystems |-> _.Guid |> toArray

            nj.SystemPrototypes |> iter (onSerializing (Some nj))

        | :? NjSystem as sys ->
            sys.Arrows |> iter (onSerializing (Some sys))
            sys.Flows  |> iter (onSerializing (Some sys))
            sys.Works  |> iter (onSerializing (Some sys))

        | :? NjFlow as flow ->
            ()

        | :? NjWork as work ->
            work.Arrows |> iter (onSerializing (Some work))
            ()
            //work.Calls |> iter onSerializing
            //work.TryGetFlow() |> iter (fun f -> work.FlowGuid <- guid2str f.Guid)
        | :? NjCall as call ->
            ()
        | :? NjArrow as arrow ->
            ()
        | _ -> failwith "ERROR.  확장 필요?"




    /// JSON 읽고 나서 메모리 구조에 후처리 작업
    let rec internal onDeserialized (njParent:INjObject option) (njObj:INjObject) =
        match njObj with
        | :? Unique as uniq ->
            uniq.Id <- uniq.DbId |> Option.ofNullable
        | _ -> ()

        match njObj with
        | :? NjProject as proj ->
            proj.SystemPrototypes |> iter (onDeserialized (Some proj))

            //[
            //    for guid in proj.ActiveSystemGuids |-> (fun g -> Guid.Parse g) do
            //        proj.SystemPrototypes |> find (fun s -> s.Guid = guid)
            //] |> proj.forceSetActiveSystems

            //[
            //    for guid in proj.PassiveSystemGuids |-> (fun g -> Guid.Parse g) do
            //        proj.SystemPrototypes |> find (fun s -> s.Guid = guid)
            //] |> proj.forceSetPassiveSystems

            //proj.Systems |> iter (fun z -> z.RawParent <- Some proj)

    //type DsProject(name, guid, activeSystems:DsSystem[], passiveSystems:DsSystem[], dateTime:DateTime, ?id, ?author, ?version, (*?langVersion, ?engineVersion,*) ?description) =
            proj.DsObject <-
                let systems = proj.SystemPrototypes |-> (fun z -> z.DsObject :?> DsSystem)
                let actives = systems |> filter (fun s -> proj.ActiveSystemGuids |> contains (s.Guid))
                let passives = systems |> filter (fun s -> proj.PassiveSystemGuids |> contains (s.Guid))
                DsProject(proj.Name, proj.Guid, actives, passives, proj.DateTime)
                |> tee(fun z ->
                    systems |> iter (fun s -> s.RawParent <- Some z)
                    z.Import z)

        | :? NjSystem as nj ->
            nj.Arrows
            |> iter (fun (a:NjArrow) ->
                let works = nj.Works |-> (fun z -> z.DsObject :?> DsWork)
                let src = works |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = works |> find(fun w -> w.Guid = s2guid a.Target)
                a.DsObject <- ArrowBetweenWorks(a.Guid, src, tgt, a.DateTime, ?id=a.Id)
                ()
                )

            // flows, works, arrows 의 Parent 를 this(system) 으로 설정
            nj.Arrows |> iter (fun z -> z.RawParent <- Some nj)
            nj.Flows  |> iter (fun z -> z.RawParent <- Some nj)
            nj.Works  |> iter (fun z -> z.RawParent <- Some nj)

            // 하부 구조에 대해서 재귀적으로 호출
            nj.Flows |> iter (onDeserialized (Some nj))
            nj.Works |> iter (onDeserialized (Some nj))

    //type DsSystem(name, guid, flows:DsFlow[], works:DsWork[], arrows:ArrowBetweenWorks[], dateTime:DateTime, ?originGuid:Guid, ?id, ?author, ?langVersion, ?engineVersion, ?description) =
            let flows = nj.Flows |-> (fun z -> z.DsObject :?> DsFlow)

            let works = [|
                for w in nj.Works do
                    let optFlow =
                        if w.FlowGuid.NonNullAny() then
                            flows |> tryFind (fun f -> f.Guid = s2guid w.FlowGuid)
                        else
                            None
                    let calls = w.Calls |-> (fun z -> z.DsObject :?> DsCall)
                    let arrows = w.Arrows |-> (fun z -> z.DsObject :?> ArrowBetweenCalls)
                    let dsWork = DsWork.Create(w.Name, w.Guid, calls, arrows, optFlow, w.DateTime, ?id=w.Id)
                    yield dsWork
                    w.DsObject <- dsWork
            |]
            let arrows = nj.Arrows |-> (fun z -> z.DsObject :?> ArrowBetweenWorks)
            nj.DsObject <- DsSystem.Create(nj.Name, nj.Guid, flows, works, arrows, nj.DateTime, ?id=nj.Id)

        | :? NjFlow as flow ->
            flow.DsObject <- DsFlow() |> tee(fun f -> f.Import flow)
            ()

        | :? NjWork as work ->
            work.Calls |> iter (fun z -> z.RawParent <- Some work)
            work.Calls |> iter (onDeserialized (Some work))
            //work.DtoArrows
            //|-> getArrowInfos work.Calls
            //|-> (fun (guid, src, tgt, dateTime, id) -> ArrowBetweenCalls(guid, src, tgt, dateTime, ?id=id))
            //|> work.forceSetArrows

            work.Arrows
            |> iter (fun (a:NjArrow) ->
                let calls = work.Calls |-> (fun z -> z.DsObject :?> DsCall)
                let src = calls |> find(fun w -> w.Guid = s2guid a.Source)
                let tgt = calls |> find(fun w -> w.Guid = s2guid a.Target)
                a.DsObject <- ArrowBetweenCalls(a.Guid, src, tgt, a.DateTime, ?id=a.Id)
                ()
                )


            //work.Arrows |> iter (fun z -> z.RawParent <- Some work)
            //if work.FlowGuid.NonNullAny() then
            //    work.OptFlowGuid <- Guid.Parse work.FlowGuid |> Some


            // DsWork 객체 생성은 flow guid 생성 시까지 지연
            ()

        | :? NjCall as call ->
            call.DsObject <- DsCall(call.Name, call.Guid, call.DateTime, ?id=call.Id)
            ()

        | _ -> failwith "ERROR.  확장 필요?"


