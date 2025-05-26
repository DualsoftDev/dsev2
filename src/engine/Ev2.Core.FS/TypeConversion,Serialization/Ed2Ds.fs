namespace Ev2.Core.FS

open Dual.Common.Core.FS

[<AutoOpen>]
module Ed2DsModule =

    type EdFlow with
        member x.ToDsFlow() =
            DsFlow(x.Name, x.Guid, ?id=x.OptId, dateTime=x.DateTime)
            |> tee(fun z -> z.RawParent <- Some x.RawParent.Value )

    type EdCall with
        member x.ToDsCall() = DsCall(x.Name, x.Guid, ?id=x.OptId, dateTime=x.DateTime)

    type EdWork with
        member x.ToDsWork(flows:DsFlow[]) =
            let callDic = x.Calls.ToDictionary(id, _.ToDsCall())
            let arrows = x.Arrows |-> (fun a -> ArrowBetweenCalls(a.Guid, callDic[a.Source], callDic[a.Target], a.DateTime))
            let optFlowGuid = x.OptOwnerFlow >>= (fun ownerFlow -> flows |> tryFind(fun f -> f.Guid = ownerFlow.Guid))
            let calls = callDic.Values |> toArray

            DsWork.Create(x.Name, x.Guid, calls, arrows, optFlowGuid, ?id=x.OptId, dateTime=x.DateTime)


    type EdSystem with
        member x.ToDsSystem() =
            let flows = x.Flows |-> _.ToDsFlow() |> toArray
            let workDic = x.Works.ToDictionary(id, _.ToDsWork(flows))
            let works = workDic.Values |> toArray
            let arrows = x.Arrows |-> (fun a -> ArrowBetweenWorks(a.Guid, workDic[a.Source], workDic[a.Target], a.DateTime)) |> toArray
            let system = DsSystem.Create(x.Name, x.Guid, flows, works, arrows, ?id=x.OptId, dateTime=x.DateTime)

            // parent 객체 확인
            for w in works do
                w.Calls |> iter (fun c -> assert (c.RawParent = Some w))

            system

    type EdProject with
        member x.ToDsProject() =
            let activeSystems  = x.ActiveSystems  |-> _.ToDsSystem() |> toArray
            let passiveSystems = x.PassiveSystems |-> _.ToDsSystem() |> toArray
            let project = DsProject(x.Name, x.Guid, activeSystems, passiveSystems, ?id=x.OptId, dateTime=x.DateTime)
            (activeSystems @ passiveSystems) |> iter (fun z -> z.RawParent <- Some project)
            project

        static member FromDsProject(p:DsProject) =
            let activeSystems  = p.ActiveSystems  |-> (fun s -> EdSystem.Create(s.Name, guid=s.Guid, ?id=s.OptId, dateTime=s.DateTime))
            let passiveSystems = p.PassiveSystems |-> (fun s -> EdSystem.Create(s.Name, guid=s.Guid, ?id=s.OptId, dateTime=s.DateTime))
            EdProject.Create(p.Name, activeSystems, passiveSystems, guid=p.Guid, ?id=p.OptId, dateTime=p.DateTime)
            |> tee (fun z ->
                activeSystems  |> iter (fun s -> s.RawParent <- Some z)
                passiveSystems |> iter (fun s -> s.RawParent <- Some z))
