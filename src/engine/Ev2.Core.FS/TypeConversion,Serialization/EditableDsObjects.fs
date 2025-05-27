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

    type EdProject () =
        inherit Unique()
        interface IEdProject

        member val Author        = System.Environment.UserName with get, set
        member val Version       = Version()  with get, set
        //member val LangVersion   = langVersion   |? Version()  with get, set
        //member val EngineVersion = engineVersion |? Version()  with get, set
        member val Description   = nullString with get, set

        member val ActiveSystems = ResizeArray<EdSystem>()
        member val PassiveSystems = ResizeArray<EdSystem>()

        member x.Fix() =
            x.ActiveSystems @ x.PassiveSystems |> iter (fun sys -> sys.RawParent <- Some x; sys.Fix())
            //x.UpdateDateTime()


    type EdSystem () =
        inherit Unique()
        interface IEdSystem

        member val Flows = ResizeArray<EdFlow>()
        member val Works = ResizeArray<EdWork>()
        member val Arrows = ResizeArray<EdArrowBetweenWorks>()

        member x.Fix() =
            x.UpdateDateTime()
            x.Flows |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            x.Works |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            x.Arrows |> iter (fun z -> z.RawParent <- Some x)
            ()


    type EdFlow () =
        inherit Unique()
        interface IEdFlow
        member x.Works = //x.OptParent |> map _.Works //|> choose id
            match x.RawParent with
            | Some (:? EdSystem as p) -> p.Works |> filter (fun w -> w.OptOwnerFlow = Some x) |> toArray
            | _ -> failwith "Parent is not set. Cannot get works from flow."

        // works 들이 flow 자신의 직접 child 가 아니므로 따로 관리 함수 필요
        member x.AddWorks(ws:EdWork seq) =
            x.UpdateDateTime()
            ws |> iter (fun w -> w.OptOwnerFlow <- Some x)
        member x.RemoveWorks(ws:EdWork seq) =
            x.UpdateDateTime()
            ws |> iter (fun w -> w.OptOwnerFlow <- None)
        member x.Fix() = ()


    type EdWork () =
        inherit Unique()
        interface IEdWork
        member val OptOwnerFlow = Option<EdFlow>.None with get, set
        member val Calls = ResizeArray<EdCall>()
        member val Arrows = ResizeArray<EdArrowBetweenCalls>()

        member x.Fix() =
            x.UpdateDateTime()
            x.Calls |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            x.Arrows |> iter (fun z -> z.RawParent <- Some x)
            ()



    type EdCall() =
        inherit Unique()
        interface IEdCall
        member val ApiCalls = ResizeArray<EdApiCall>() with get, set
        member val CallType = DbCallType.Normal with get, set
        member x.Fix() =
            x.UpdateDateTime()
            x.ApiCalls |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            ()


    type EdApiCall() =
        inherit Unique()
        member x.Call = x.RawParent |-> (fun z -> z :?> EdCall) |?? (fun () -> getNull<EdCall>())
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val ValueType  = DbDataType.None with get, set
        member val Value = nullString with get, set
        member x.Fix() =
            x.UpdateDateTime()
            ()



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
            let activeSystems  = p.ActiveSystems  |-> (fun s -> EdSystem() |> uniqReplicate s)
            let passiveSystems = p.PassiveSystems |-> (fun s -> EdSystem() |> uniqReplicate s)
            EdProject() |> uniqReplicate p //  (p.Name, activeSystems, passiveSystems, guid=p.Guid, ?id=p.Id, dateTime=p.DateTime)
            |> tee (fun z ->
                activeSystems  |> tee (fun xs -> xs |> iter (fun s -> s.RawParent <- Some z)) |> z.ActiveSystems.AddRange
                passiveSystems |> tee (fun xs -> xs |> iter (fun s -> s.RawParent <- Some z)) |> z.PassiveSystems.AddRange
            )
