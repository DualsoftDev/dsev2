namespace Ev2.Core.FS

open System
open Dual.Common.Core.FS
open Dual.Common.Base

/// 편집 가능한 버젼
[<AutoOpen>]
module rec EditableDsObjects =

    type IEdObject  = interface end
    type IEdProject = inherit IEdObject inherit IDsProject
    type IEdSystem  = inherit IEdObject inherit IDsSystem
    type IEdFlow    = inherit IEdObject inherit IDsFlow
    type IEdWork    = inherit IEdObject inherit IDsWork
    type IEdCall    = inherit IEdObject inherit IDsCall

    type EdProject private (name:string, activeSystems:ResizeArray<EdSystem>, passiveSystems:ResizeArray<EdSystem>, guid:Guid, dateTime:DateTime, ?id, ?author, ?version, (*?langVersion, ?engineVersion,*) ?description) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id)
        interface IEdProject

        member val Author        = author        |? System.Environment.UserName with get, set
        member val Version       = version       |? Version()  with get, set
        //member val LangVersion   = langVersion   |? Version()  with get, set
        //member val EngineVersion = engineVersion |? Version()  with get, set
        member val Description   = description   |? nullString with get, set

        member x.ActiveSystems = activeSystems |> toArray
        member x.PassiveSystems = passiveSystems |> toArray
        member x.AddActiveSystem (sys:EdSystem) =
            x.UpdateDateTime()
            sys.RawParent <- Some x
            activeSystems.Add(sys)
        member x.AddPassiveSystem(sys:EdSystem) =
            x.UpdateDateTime()
            sys.RawParent <- Some x
            passiveSystems.Add(sys)

        member x.RemoveActiveSystem (sys:EdSystem) =
            x.UpdateDateTime()
            sys.RawParent <- None
            activeSystems.Remove(sys)
        member x.RemovePassiveSystem(sys:EdSystem) =
            x.UpdateDateTime()
            sys.RawParent <- None
            passiveSystems.Remove(sys)


        static member Create(name:string, ?activeSystems:EdSystem seq, ?passiveSystems:EdSystem seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let activeSystems = activeSystems |? Seq.empty |> ResizeArray
            let passiveSystems = passiveSystems |? Seq.empty |> ResizeArray
            EdProject(name, activeSystems, passiveSystems, guid, dateTime)



    type EdSystem private (name:string, project:EdProject option, flows:ResizeArray<EdFlow>, works:ResizeArray<EdWork>, arrows:ResizeArray<EdArrowBetweenWorks>, guid:Guid, dateTime:DateTime, ?id) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id, ?parent=(project >>= tryCast<Unique>))
        interface IEdSystem


        member x.Flows = flows |> toArray
        member x.Works = works |> toArray
        member x.Arrows = arrows

        member x.AddFlows(fs:EdFlow seq) =
            x.UpdateDateTime()
            flows.AddRange(fs)
            fs |> iter (fun f -> f.RawParent <- Some x)
        member x.AddWorks(ws:EdWork seq) =
            x.UpdateDateTime()
            works.AddRange(ws)
            ws |> iter (fun w -> w.RawParent <- Some x)
        member x.AddArrows(arrs:EdArrowBetweenWorks seq) =
            x.UpdateDateTime()
            arrows.AddRange(arrs)
            arrs |> iter (fun c -> c.RawParent <- Some x)


        member x.RemoveFlows(fs:EdFlow seq) =
            x.UpdateDateTime()
            for f in fs do
                flows.Remove f |> ignore
                f.RawParent <- None
        member x.RemoveWorks(ws:EdWork seq) =
            x.UpdateDateTime()
            for w in works do
                works.Remove w |> ignore
                w.RawParent <- None
        member x.RemoveArrows(arrs:EdArrowBetweenWorks seq) =
            x.UpdateDateTime()
            for a in arrows do
                arrows.Remove a |> ignore
                a.RawParent <- None


        static member Create(name:string, ?project:EdProject, ?flows:EdFlow seq, ?works:EdWork seq, ?arrows:EdArrowBetweenWorks seq, ?id:Id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let flows = flows |? Seq.empty |> ResizeArray
            let works = works |? Seq.empty |> ResizeArray
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdSystem(name, project, flows, works, arrows, guid, dateTime, ?id=id)

    type EdFlow private (name:string, guid:Guid, dateTime:DateTime, ?system:EdSystem, ?id) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?parent=(system >>= tryCast<Unique>), ?id=id)
        interface IEdFlow
        static member Create(name, ?arrows:EdArrowBetweenWorks seq, ?id, ?guid, ?dateTime, ?system:EdSystem) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdFlow(name, guid, dateTime, ?system=system, ?id=id)
            |> tee(fun f -> system |> Option.iter (fun sys -> sys.AddFlows [f]))

        member x.Works = //x.OptParent |> map _.Works //|> choose id
            match x.RawParent with
            | Some (:? EdSystem as p) -> p.Works |> filter (fun w -> w.OptOwnerFlow = Some x) |> toArray
            | _ -> failwith "Parent is not set. Cannot get works from flow."

        member x.AddWorks(ws:EdWork seq) =
            x.UpdateDateTime()
            ws |> iter (fun w -> w.OptOwnerFlow <- Some x)
        member x.RemoveWorks(ws:EdWork seq) =
            x.UpdateDateTime()
            ws |> iter (fun w -> w.OptOwnerFlow <- None)


    type EdWork private(name:string, guid:Guid, dateTime:DateTime, calls:ResizeArray<EdCall>, arrows:ResizeArray<EdArrowBetweenCalls>, ?parent:EdSystem, ?ownerFlow:EdFlow, ?id) =
        inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime)
        interface IEdWork
        member val OptOwnerFlow = ownerFlow with get, set
        member x.Calls = calls |> toArray
        member x.Arrows = arrows |> toArray

        member x.AddCalls(cs:EdCall seq) =
            x.UpdateDateTime()
            calls.AddRange(cs)
            cs |> iter (fun c -> c.RawParent <- Some x)

        member x.AddArrows(arrs:EdArrowBetweenCalls seq) =
            x.UpdateDateTime()
            arrows.AddRange(arrs)
            arrs |> iter (fun c -> c.RawParent <- Some x)

        static member Create(name:string, system:EdSystem, ?calls:EdCall seq, ?ownerFlow:EdFlow, ?arrows:EdArrowBetweenCalls seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let calls = calls |? Seq.empty |> ResizeArray
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdWork(name, guid, dateTime, calls, arrows, parent=system, ?ownerFlow=ownerFlow, ?id=id)
            |> tee(fun w ->
                system.AddWorks [w]
                ownerFlow |> Option.iter(fun f -> f.AddWorks [w]))


    type EdCall private(name:string, guid:Guid, dateTime:DateTime, work:EdWork, apiCalls:EdApiCall seq, ?id) =
        inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime, parent=work)
        interface IEdCall
        member val ApiCalls = ResizeArray(apiCalls) with get, set
        member val CallType = DbCallType.Normal with get, set

        static member Create(name:string, work:EdWork, ?apiCalls:EdApiCall seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let apiCalls = apiCalls |? Seq.empty
            EdCall(name, guid, dateTime, work, apiCalls, ?id=id)
            |> tee(fun c ->
                apiCalls |> iter (fun a -> a.RawParent <- Some c)
                work.AddCalls [c] )

    type EdApiCall private(name:string, guid:Guid, dateTime:DateTime, ?call:EdCall, ?id) =
        inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime, ?parent=(call >>= tryCast<Unique>))
        member x.Call = x.RawParent |-> (fun z -> z :?> EdCall) |?? (fun () -> getNull<EdCall>())
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val ValueType  = DbDataType.None with get, set
        member val Value = nullString with get, set
        static member Create(name:string, ?guid:Guid, ?dateTime:DateTime, ?call:EdCall, ?id) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            EdApiCall(name, guid, dateTime, ?call=call, ?id=id)



    type EdArrowBetweenCalls(source:EdCall, target:EdCall, dateTime:DateTime, guid:Guid, ?id:Id) =
        inherit Arrow<EdCall>(source, target, dateTime, guid, ?id=id)

    type EdArrowBetweenWorks(source:EdWork, target:EdWork, dateTime:DateTime, guid:Guid, ?id:Id) =
        inherit Arrow<EdWork>(source, target, dateTime, guid, ?id=id)


[<AutoOpen>]
module Ed2DsModule =

    type EdFlow with
        member x.ToDsFlow() =
            DsFlow(x.Name, x.Guid, ?id=x.Id, dateTime=x.DateTime)
            |> tee(fun z -> z.RawParent <- Some x.RawParent.Value )

    type EdCall with
        member x.ToDsCall() =
            let apiCalls = x.ApiCalls |-> (fun a -> DsApiCall(a.Guid, a.DateTime, ?id=a.Id))
            DsCall.Create(x.Name, x.Guid, x.CallType, apiCalls, ?id=x.Id, dateTime=x.DateTime)

    type EdWork with
        member x.ToDsWork(flows:DsFlow[]) =
            let callDic = x.Calls.ToDictionary(id, _.ToDsCall())
            let arrows = x.Arrows |-> (fun a -> ArrowBetweenCalls(a.Guid, callDic[a.Source], callDic[a.Target], a.DateTime))
            let optFlowGuid = x.OptOwnerFlow >>= (fun ownerFlow -> flows |> tryFind(fun f -> f.Guid = ownerFlow.Guid))
            let calls = callDic.Values |> toArray

            DsWork.Create(x.Name, x.Guid, calls, arrows, optFlowGuid, ?id=x.Id, dateTime=x.DateTime)


    type EdSystem with
        member x.ToDsSystem() =
            let flows = x.Flows |-> _.ToDsFlow() |> toArray
            let workDic = x.Works.ToDictionary(id, _.ToDsWork(flows))
            let works = workDic.Values |> toArray
            let arrows = x.Arrows |-> (fun a -> ArrowBetweenWorks(a.Guid, workDic[a.Source], workDic[a.Target], a.DateTime)) |> toArray
            let system = DsSystem.Create(x.Name, x.Guid, flows, works, arrows, ?id=x.Id, dateTime=x.DateTime)

            // parent 객체 확인
            for w in works do
                w.Calls |> iter (fun c -> assert (c.RawParent = Some w))

            system

    type EdProject with
        member x.ToDsProject() =
            let activeSystems  = x.ActiveSystems  |-> _.ToDsSystem() |> toArray
            let passiveSystems = x.PassiveSystems |-> _.ToDsSystem() |> toArray
            let project = DsProject(x.Name, x.Guid, activeSystems, passiveSystems, ?id=x.Id, dateTime=x.DateTime)
            (activeSystems @ passiveSystems) |> iter (fun z -> z.RawParent <- Some project)
            project

        static member FromDsProject(p:DsProject) =
            let activeSystems  = p.ActiveSystems  |-> (fun s -> EdSystem.Create(s.Name, guid=s.Guid, ?id=s.Id, dateTime=s.DateTime))
            let passiveSystems = p.PassiveSystems |-> (fun s -> EdSystem.Create(s.Name, guid=s.Guid, ?id=s.Id, dateTime=s.DateTime))
            EdProject.Create(p.Name, activeSystems, passiveSystems, guid=p.Guid, ?id=p.Id, dateTime=p.DateTime)
            |> tee (fun z ->
                activeSystems  |> iter (fun s -> s.RawParent <- Some z)
                passiveSystems |> iter (fun s -> s.RawParent <- Some z))
