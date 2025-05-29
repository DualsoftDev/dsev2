namespace Ev2.Core.FS

open System
open Dual.Common.Core.FS
open Dual.Common.Base
open System.Collections.Generic

/// 편집 가능한 버젼
[<AutoOpen>]
module rec EditableDsObjects =

    type IEdObject  = interface end
    type IEdUnique  = inherit IEdObject inherit IUnique
    type IEdProject = inherit IEdUnique inherit IDsProject
    type IEdSystem  = inherit IEdUnique inherit IDsSystem
    type IEdFlow    = inherit IEdUnique inherit IDsFlow
    type IEdWork    = inherit IEdUnique inherit IDsWork
    type IEdCall    = inherit IEdUnique inherit IDsCall
    type IEdApiCall = inherit IEdUnique inherit IDsApiCall
    type IEdApiDef  = inherit IEdUnique inherit IDsApiDef
    type IEdArrow   = inherit IEdUnique inherit IArrow

    type EdUnique() =
        inherit Unique()
        interface IEdUnique
        // 임시 저장 구조
        member val internal RtUnique = getNull<RtUnique>() with get, set

    type EdProject () =
        inherit EdUnique()
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
        inherit EdUnique()
        interface IEdSystem

        member val Flows       = ResizeArray<EdFlow>()
        member val Works       = ResizeArray<EdWork>()
        member val Arrows      = ResizeArray<EdArrowBetweenWorks>()
        member val ApiDefs     = ResizeArray<EdApiDef>()
        member val ApiCalls    = ResizeArray<EdApiCall>()
        member val IsPrototype = false with get, set
        member val OriginGuid  = noneGuid with get, set

        member x.Fix() =
            x.UpdateDateTime()
            x.Flows    |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            x.Works    |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            x.Arrows   |> iter (fun z -> z.RawParent <- Some x)
            x.ApiDefs  |> iter (fun z -> z.RawParent <- Some x)
            x.ApiCalls |> iter (fun z -> z.RawParent <- Some x)
            ()


    type EdFlow () =
        inherit EdUnique()
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
        inherit EdUnique()
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
        inherit EdUnique()
        interface IEdCall
        member val ApiCalls = ResizeArray<EdApiCall>()
        member val CallType = DbCallType.Normal with get, set
        member val AutoPre  = nullString with get, set
        member val Safety   = nullString with get, set
        member val Timeout  = Option<int>.None with get, set
        member x.Fix() =
            x.UpdateDateTime()
            x.ApiCalls |> iter (fun z -> z.RawParent <- Some x; z.Fix())
            ()


    type EdApiCall(apiDef:EdApiDef) =
        inherit EdUnique()
        member x.Call = x.RawParent |-> (fun z -> z :?> EdCall) |?? (fun () -> getNull<EdCall>())
        member val ApiDef     = apiDef with get, set
        member val InAddress  = nullString with get, set
        member val OutAddress = nullString with get, set
        member val InSymbol   = nullString with get, set
        member val OutSymbol  = nullString with get, set
        member val ValueType  = DbDataType.None with get, set
        member val Value = nullString with get, set
        member x.Fix() =
            x.UpdateDateTime()
            ()

    type EdApiDef() =
        inherit EdUnique()
        member val IsPush = false with get, set
        member x.Fix() =
            x.UpdateDateTime()
            ()



    type EdArrowBetweenCalls(source:EdCall, target:EdCall, typ:DbArrowType) =
        inherit EdUnique()
        let arrow = Arrow<EdCall>(source, target, typ)

        interface IEdUnique
        interface IEdArrow
        member x.Source with get() = arrow.Source and set v = arrow.Source <- v
        member x.Target with get() = arrow.Target and set v = arrow.Target <- v
        member x.Type   with get() = arrow.Type   and set v = arrow.Type <- v


    type EdArrowBetweenWorks(source:EdWork, target:EdWork, typ:DbArrowType) =
        inherit EdUnique()
        let arrow = Arrow<EdWork>(source, target, typ)

        interface IEdUnique
        interface IEdArrow
        member x.Source with get() = arrow.Source and set v = arrow.Source <- v
        member x.Target with get() = arrow.Target and set v = arrow.Target <- v
        member x.Type   with get() = arrow.Type   and set v = arrow.Type <- v


[<AutoOpen>]
module Ed2DsModule =
    type Ed2RtBag() =
        member val EdDic = Dictionary<Guid, EdUnique>()
        member val RtDic = Dictionary<Guid, RtUnique>()
        member x.Add(u:EdUnique) = x.EdDic.Add(u.Guid, u)
        member x.Add(u:RtUnique) = x.RtDic.Add(u.Guid, u)
        member x.Add2 (ed:EdUnique) (rt:RtUnique) =
            x.RtDic.TryAdd(rt.Guid, rt) |> ignore
            x.EdDic.TryAdd(ed.Guid, ed) |> ignore
        member x.AddRE (rt:RtUnique) (ed:EdUnique) = x.Add2 ed rt

    type EdFlow with
        member x.ToRtFlow(bag:Ed2RtBag) =
            RtFlow() |> uniqReplicate x |> tee (bag.Add2 x)

    type RtFlow with
        member x.ToEdFlow(bag:Ed2RtBag) =
            EdFlow() |> uniqReplicate x |> tee (bag.AddRE x)


    type EdCall with
        member x.ToRtCall(bag:Ed2RtBag) =
            let rtApiCalls =
                noop()
                x.ApiCalls
                |-> (fun (a:EdApiCall) -> bag.RtDic[a.Guid] :?> RtApiCall)

            RtCall(x.CallType, rtApiCalls, x.AutoPre, x.Safety, x.Timeout)
            |> uniqINGD_fromObj x
            |> tee (bag.Add2 x)

    type RtCall with
        member x.ToEdCall(bag:Ed2RtBag) =
            let edApiCalls =
                noop()
                x.ApiCalls
                |-> (fun (a:RtApiCall) -> bag.EdDic[a.Guid] :?> EdApiCall)

            EdCall(CallType=x.CallType, AutoPre=x.AutoPre, Safety=x.Safety, Timeout=x.Timeout)
            |> tee(fun z -> edApiCalls |> z.ApiCalls.AddRange)
            |> uniqINGD_fromObj x
            |> tee (bag.AddRE x)

    type EdWork with
        member x.ToRtWork(bag:Ed2RtBag, flows:RtFlow[]) =
            x.Calls |> iter bag.Add
            x.Arrows |> iter bag.Add

            let callDic = x.Calls.ToDictionary(id, _.ToRtCall(bag))
            let arrows =
                x.Arrows
                |-> fun a -> RtArrowBetweenCalls(callDic[a.Source], callDic[a.Target], a.Type)
                            |> uniqINGD_fromObj a
                            |> tee (bag.Add2 a)

            let optFlowGuid = x.OptOwnerFlow >>= (fun ownerFlow -> flows |> tryFind(fun f -> f.Guid = ownerFlow.Guid))
            let calls = callDic.Values |> toArray

            RtWork.Create(calls, arrows, optFlowGuid)
            |> uniqINGD_fromObj x
            |> tee (bag.Add2 x)

        static member Create(calls:EdCall seq, arrows:EdArrowBetweenCalls seq, optFlow:EdFlow option) =
            let calls = calls |> ResizeArray
            let arrows = arrows |> ResizeArray
            EdWork(OptOwnerFlow=optFlow)
            |> tee (fun (z:EdWork) ->
                arrows  |> z.Arrows.AddRange
                calls   |> z.Calls.AddRange

                calls   |> iter (fun y -> y.RawParent <- Some z)
                arrows  |> iter (fun y -> y.RawParent <- Some z)
                optFlow |> iter (fun y -> y.RawParent <- Some z) )


    type RtWork with
        member x.ToEdWork(bag:Ed2RtBag, flows:EdFlow[]) =
            x.Calls |> iter bag.Add
            x.Arrows |> iter bag.Add

            let callDic = x.Calls.ToDictionary(id, _.ToEdCall(bag))
            let arrows =
                x.Arrows
                |-> fun a -> EdArrowBetweenCalls(callDic[a.Source], callDic[a.Target], a.Type)
                            |> uniqINGD_fromObj a
                            |> tee (bag.AddRE a)

            let optFlowGuid = x.OptFlow >>= (fun ownerFlow -> flows |> tryFind(fun f -> f.Guid = ownerFlow.Guid))
            let calls = callDic.Values |> toArray

            EdWork.Create(calls, arrows, optFlowGuid)
            |> uniqINGD_fromObj x
            |> tee (bag.AddRE x)


    type EdSystem with
        member x.ToRtSystem(bag:Ed2RtBag) =
            bag.Add x
            x.Flows |> iter bag.Add
            x.Works |> iter bag.Add
            x.Arrows |> iter bag.Add
            x.ApiDefs |> iter bag.Add
            x.ApiCalls |> iter bag.Add

            let apiDefs =
                x.ApiDefs |-> (fun z ->
                    RtApiDef(z.IsPush)
                    |> uniqINGD_fromObj z |> tee (bag.Add2 z))
                |> toArray

            let apiCalls =
                x.ApiCalls
                |-> (fun z ->
                        let rtApiDef = bag.RtDic[z.ApiDef.Guid] :?> RtApiDef
                        RtApiCall(rtApiDef, z.InAddress, z.OutAddress, z.InSymbol, z.OutSymbol, z.ValueType, z.Value)
                        |> uniqINGD_fromObj z |> tee (bag.Add2 z))
                |> toArray

            let flows = x.Flows |-> _.ToRtFlow(bag) |> toArray
            let workDic = x.Works.ToDictionary(id, _.ToRtWork(bag, flows))
            let works = workDic.Values |> toArray
            let arrows =
                x.Arrows |-> (fun z ->
                    RtArrowBetweenWorks(workDic[z.Source], workDic[z.Target], z.Type)
                    |> uniqINGD_fromObj z |> tee (bag.Add2 z))
                |> toArray

            let system =
                RtSystem.Create(x.IsPrototype, flows, works, arrows, apiDefs, apiCalls)
                |> uniqINGD_fromObj x |> tee (bag.Add2 x)

            // parent 객체 확인
            for w in works do
                w.Calls |> iter (fun c -> assert (c.RawParent = Some w))

            system


        static member Create(isPrototype:bool, flows:EdFlow[], works:EdWork[], arrows:EdArrowBetweenWorks[], apiDefs:EdApiDef[], apiCalls:EdApiCall[]) =
            EdSystem(IsPrototype=isPrototype)
            |> tee (fun z ->
                flows    |> z.Flows   .AddRange
                works    |> z.Works   .AddRange
                arrows   |> z.Arrows  .AddRange
                apiDefs  |> z.ApiDefs .AddRange
                apiCalls |> z.ApiCalls.AddRange
                flows    |> iter (fun y -> y.RawParent <- Some z)
                works    |> iter (fun y -> y.RawParent <- Some z)
                arrows   |> iter (fun y -> y.RawParent <- Some z)
                apiDefs  |> iter (fun y -> y.RawParent <- Some z)
                apiCalls |> iter (fun y -> y.RawParent <- Some z) )

        static member FromRt(x:RtSystem, bag:Ed2RtBag):EdSystem =
            bag.Add x
            x.Flows |> iter bag.Add
            x.Works |> iter bag.Add
            x.Arrows |> iter bag.Add
            x.ApiDefs |> iter bag.Add
            x.ApiCalls |> iter bag.Add

            let apiDefs =
                x.ApiDefs |-> (fun z ->
                    EdApiDef(IsPush = z.IsPush)
                    |> uniqINGD_fromObj z |> tee (bag.AddRE z))
                |> toArray

            let apiCalls =
                x.ApiCalls
                |-> (fun z ->
                        let edApiDef = bag.EdDic[z.ApiDef.Guid] :?> EdApiDef
                        EdApiCall(edApiDef, InAddress=z.InAddress, OutAddress=z.OutAddress, InSymbol=z.InSymbol, OutSymbol=z.OutSymbol, ValueType=z.ValueType, Value=z.Value)
                        |> uniqINGD_fromObj z |> tee (bag.AddRE z))
                |> toArray

            let flows = x.Flows |-> _.ToEdFlow(bag) |> toArray
            let workDic = x.Works.ToDictionary(id, _.ToEdWork(bag, flows))
            let works = workDic.Values |> toArray
            let arrows =
                x.Arrows |-> (fun z ->
                    EdArrowBetweenWorks(workDic[z.Source], workDic[z.Target], z.Type)
                    |> uniqINGD_fromObj z |> tee (bag.AddRE z))
                |> toArray

            let system =
                EdSystem.Create(x.IsPrototype, flows, works, arrows, apiDefs, apiCalls)
                |> uniqINGD_fromObj x |> tee (bag.AddRE x)

            // parent 객체 확인
            for w in works do
                w.Calls |> iter (fun c -> assert (c.RawParent = Some w))

            system



    type EdProject with
        member x.ToRtProject() =
            let bag = Ed2RtBag()
            bag.EdDic.Add(x.Guid, x)
            let activeSystems  = x.ActiveSystems  |-> _.ToRtSystem(bag) |> toArray
            let passiveSystems = x.PassiveSystems |-> _.ToRtSystem(bag) |> toArray
            let project = RtProject(activeSystems, passiveSystems) |> uniqINGD_fromObj x
            (activeSystems @ passiveSystems) |> iter (fun z -> z.RawParent <- Some project)
            project

        static member FromRt(p:RtProject) =
            let bag = Ed2RtBag()
            let activeSystems  = p.ActiveSystems  |-> (fun s -> EdSystem.FromRt(s, bag) |> uniqReplicate s)
            let passiveSystems = p.PassiveSystems |-> (fun s -> EdSystem() |> uniqReplicate s)
            EdProject() |> uniqReplicate p //  (p.Name, activeSystems, passiveSystems, guid=p.Guid, ?id=p.Id, dateTime=p.DateTime)
            |> tee (fun z ->
                activeSystems  |> tee (fun xs -> xs |> iter (fun s -> s.RawParent <- Some z)) |> z.ActiveSystems.AddRange
                passiveSystems |> tee (fun xs -> xs |> iter (fun s -> s.RawParent <- Some z)) |> z.PassiveSystems.AddRange
            )

    type RtProject with
        member x.ToEdProject() = EdProject.FromRt x
        static member FromEd(p:EdProject) = p.ToRtProject()

    type RtSystem with
        member x.ToEdSystem() = EdSystem.FromRt(x, Ed2RtBag())
        static member FromEd(p:EdSystem) = p.ToRtSystem(Ed2RtBag())
