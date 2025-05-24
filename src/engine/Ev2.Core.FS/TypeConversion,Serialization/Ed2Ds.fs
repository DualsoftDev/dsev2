namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module rec Ed2DsModule =

    type EdFlow with
        member x.ToDsFlow() =
            let workDic = x.Works.ToDictionary(id, fun z -> z.ToDsWork())
            let works = workDic.Values |> toArray
            //let arrows = x.Arrows |-> (fun a -> ArrowBetweenWorks(a.Guid, workDic[a.Source], workDic[a.Target], a.DateTime)) |> Seq.toArray
            //DsFlow(x.Name, x.Guid, x.RawParent.Value.Guid, works, arrows, ?id=x.Id, dateTime=x.DateTime)
            DsFlow(x.Name, x.Guid, works, ?id=x.Id, dateTime=x.DateTime)
            |> tee(fun z -> z.RawParent <- Some x.RawParent.Value)

    type EdWork with
        member x.ToDsWork() =
            let callDic = x.Calls.ToDictionary(id, fun z -> z.ToDsCall())
            let arrows = x.Arrows |-> (fun a -> ArrowBetweenCalls(a.Guid, callDic[a.Source], callDic[a.Target], a.DateTime)) |> Seq.toArray
            let optOwnerFlowGuid = x.OptOwnerFlow |-> _.Guid
            let calls = callDic.Values |> toArray

            DsWork(x.Name, x.Guid, calls, arrows, optOwnerFlowGuid, ?id=x.Id, dateTime=x.DateTime)
            |> tee(fun w ->
                w.RawParent <- Some x.RawParent.Value
                arrows |> iter (fun z -> z.RawParent <- Some w)
                calls  |> iter (fun z -> z.RawParent <- Some w)
                )


    type EdCall with
        member x.ToDsCall() = DsCall(x.Name, x.Guid, ?id=x.Id, dateTime=x.DateTime)

    type EdSystem with
        member x.ToDsSystem() =
            let flows = x.Flows |> Seq.map (fun f -> f.ToDsFlow()) |> Seq.toArray
            let workDic = x.Works.ToDictionary(id, fun w -> w.ToDsWork())
            let works = workDic.Values |> toArray
            let arrows = x.Arrows |-> (fun a -> ArrowBetweenWorks(a.Guid, workDic[a.Source], workDic[a.Target], a.DateTime)) |> toArray
            let system = DsSystem(x.Name, x.Guid, flows, workDic.Values.ToArray(), arrows, ?id=x.Id, dateTime=x.DateTime)

            // parent 객체 할당
            flows |> iter (fun z -> z.RawParent <- Some system)
            works |> iter (fun z -> z.RawParent <- Some system)
            for w in works do
                w.Calls |> iter (fun c -> c.RawParent <- Some w)
            system

    type EdProject with
        member x.ToDsProject() =
            let activeSystems  = x.ActiveSystems  |> Seq.map (fun f -> f.ToDsSystem()) |> Seq.toArray
            let passiveSystems = x.PassiveSystems |> Seq.map (fun f -> f.ToDsSystem()) |> Seq.toArray
            let project = DsProject(x.Name, x.Guid, activeSystems, passiveSystems, ?id=x.Id, dateTime=x.DateTime)
            (activeSystems @ passiveSystems) |> iter (fun z -> z.RawParent <- Some project)
            project

        static member FromDsProject(p:DsProject) =
            let activeSystems  = p.ActiveSystems  |-> (fun s -> EdSystem.Create(s.Name, guid=s.Guid, ?id=s.Id, dateTime=s.DateTime))
            let passiveSystems = p.PassiveSystems |-> (fun s -> EdSystem.Create(s.Name, guid=s.Guid, ?id=s.Id, dateTime=s.DateTime))
            EdProject.Create(p.Name, activeSystems, passiveSystems, guid=p.Guid, ?id=p.Id, dateTime=p.DateTime)
            |> tee (fun z ->
                activeSystems |> iter (fun s -> s.RawParent <- Some z)
                passiveSystems |> iter (fun s -> s.RawParent <- Some z))
