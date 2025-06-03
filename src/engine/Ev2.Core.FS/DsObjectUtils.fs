namespace Ev2.Core.FS

open Dual.Common.Core.FS
open System
open System.Collections.Generic

[<AutoOpen>]
module rec TmpCompatibility =
    type RtUnique with  // UpdateDateTime, EnumerateRtObjects
        /// DS object 의 모든 상위 DS object 의 DateTime 을 갱신.  (tree 구조를 따라가면서 갱신)
        member x.UpdateDateTime(?dateTime:DateTime) =
            let dateTime = dateTime |?? now
            x.EnumerateRtObjects() |> iter (fun z -> z.DateTime <- dateTime)

        (* see also EdUnique.EnumerateRtObjects *)
        member x.EnumerateRtObjects(?includeMe): RtUnique list =
            seq {
                let includeMe = includeMe |? true
                if includeMe then
                    yield x
                match x with
                | :? RtProject as prj ->
                    yield! prj.PrototypeSystems >>= _.EnumerateRtObjects()
                    yield! prj.Systems   >>= _.EnumerateRtObjects()
                | :? RtSystem as sys ->
                    yield! sys.Works     >>= _.EnumerateRtObjects()
                    yield! sys.Flows     >>= _.EnumerateRtObjects()
                    yield! sys.Arrows    >>= _.EnumerateRtObjects()
                    yield! sys.ApiDefs   >>= _.EnumerateRtObjects()
                    yield! sys.ApiCalls  >>= _.EnumerateRtObjects()
                | :? RtWork as work ->
                    yield! work.Calls    >>= _.EnumerateRtObjects()
                    yield! work.Arrows   >>= _.EnumerateRtObjects()
                | (:? RtCall) | (:? RtApiCall) | (:? RtApiDef) | (:? RtArrowBetweenWorks) | (:? RtArrowBetweenCalls)  ->
                    ()
                | _ ->
                    tracefn $"Skipping {(x.GetType())} in EnumerateRtObjects"
                    ()
            } |> List.ofSeq



    type RtProject with // AddPrototypeSystem, AddActiveSystem, AddPassiveSystem, Instantiate
        member x.AddPrototypeSystem(system:RtSystem) =
            x.RawPrototypeSystems.Add system

        member x.AddActiveSystem(system:RtSystem) =
            system |> setParent x |> x.RawActiveSystems.Add

        member x.AddPassiveSystem(system:RtSystem) =
            system |> setParent x |> x.RawPassiveSystems.Add

        /// project 내에 prototypeGuid 를 가진 prototype system 을 복사하여 instance 로 만들어 반환
        member x.Instantiate(prototypeGuid:Guid, asActive:bool):RtSystem =
            x.PrototypeSystems
            |> tryFind(fun s -> s.Guid = prototypeGuid )
            |?? (fun () -> failwith "Prototype system not found")
            |> (fun z -> fwdDuplicate z :?> RtSystem)
            |> tee (fun z ->
                z.PrototypeSystemGuid <- Some prototypeGuid
                if asActive then x.AddActiveSystem z
                else x.AddPassiveSystem z)

    type RtSystem with
        member x.AddWorks(works:RtWork seq) =
            x.UpdateDateTime()
            works |> iter (setParentI x)
            works |> verifyAddRangeAsSet x.RawWorks
        member x.RemoveWorks(works:RtWork seq) =
            x.UpdateDateTime()
            works |> iter (fun w -> w.RawParent <- None)
            works |> iter (x.RawWorks.Remove >> ignore)

        member x.AddFlows(flows:RtFlow seq) =
            x.UpdateDateTime()
            flows |> iter (setParentI x)
            flows |> verifyAddRangeAsSet x.RawFlows
        member x.RemoveFlows(flows:RtFlow seq) =
            x.UpdateDateTime()
            flows |> iter clearParentI
            flows |> iter (x.RawFlows.Remove >> ignore)

        member x.AddArrows(arrows:RtArrowBetweenWorks seq) =
            x.UpdateDateTime()
            arrows |> iter (setParentI x)
            arrows |> verifyAddRangeAsSet x.RawArrows
        member x.RemoveArrows(arrows:RtArrowBetweenWorks seq) =
            x.UpdateDateTime()
            arrows |> iter clearParentI
            arrows |> iter (x.RawArrows.Remove >> ignore)

        member x.AddApiDefs(apiDefs:RtApiDef seq) =
            x.UpdateDateTime()
            apiDefs |> iter (setParentI x)
            apiDefs |> verifyAddRangeAsSet x.RawApiDefs
        member x.RemoveApiDefs(apiDefs:RtApiDef seq) =
            x.UpdateDateTime()
            apiDefs |> iter clearParentI
            apiDefs |> iter (x.RawApiDefs.Remove >> ignore)

        member x.AddApiCalls(apiCalls:RtApiCall seq) =
            x.UpdateDateTime()
            apiCalls |> iter (setParentI x)
            apiCalls |> verifyAddRangeAsSet x.RawApiCalls
        member x.RemoveApiCalls(apiCalls:RtApiCall seq) =
            x.UpdateDateTime()
            apiCalls |> iter clearParentI
            apiCalls |> iter (x.RawApiCalls.Remove >> ignore)




    type RtFlow with    // AddWorks, RemoveWorks
        // works 들이 flow 자신의 직접 child 가 아니므로 따로 관리 함수 필요
        member x.AddWorks(ws:RtWork seq) =
            x.UpdateDateTime()
            ws |> iter (fun w -> w.Flow <- Some x)

        member x.RemoveWorks(ws:RtWork seq) =
            x.UpdateDateTime()
            ws |> iter (fun w -> w.Flow <- None)

    type RtWork with    // AddCalls, RemoveCalls, AddArrows, RemoveArrows
        member x.AddCalls(calls:RtCall seq) =
            x.UpdateDateTime()
            calls |> iter (setParentI x)
            calls |> verifyAddRangeAsSet x.RawCalls
        member x.RemoveCalls(calls:RtCall seq) =
            x.UpdateDateTime()
            calls |> iter clearParentI
            calls |> iter (x.RawCalls.Remove >> ignore)

        member x.AddArrows(arrows:RtArrowBetweenCalls seq) =
            x.UpdateDateTime()
            arrows |> iter (setParentI x)
            arrows |> verifyAddRangeAsSet x.RawArrows
        member x.RemoveArrows(arrows:RtArrowBetweenCalls seq) =
            x.UpdateDateTime()
            arrows |> iter clearParentI
            arrows |> iter (x.RawArrows.Remove >> ignore)


    type RtCall with    // AddApiCalls
        member x.AddApiCalls(apiCalls:RtApiCall seq) =
            x.UpdateDateTime()
            apiCalls |> iter (setParentI x)
            apiCalls |> iter (fun z -> x.ApiCallGuids.Add z.Guid)



[<AutoOpen>]
module DsObjectUtilsModule =
    type RtProject with
        static member Create() = RtProject([||], [||], [||])

    type RtSystem with
        static member Create(protoGuid:Guid option, flows:RtFlow[], works:RtWork[],
            arrows:RtArrowBetweenWorks[], apiDefs:RtApiDef[], apiCalls:RtApiCall[]
        ) =
            RtSystem(protoGuid, flows, works, arrows, apiDefs, apiCalls)
            |> tee (fun z ->
                flows    |> iter (setParentI z)
                works    |> iter (setParentI z)
                arrows   |> iter (setParentI z)
                apiDefs  |> iter (setParentI z)
                apiCalls |> iter (setParentI z) )
        static member Create() = RtSystem(None, [||], [||], [||], [||], [||])

    type RtWork with
        static member Create(calls:RtCall seq, arrows:RtArrowBetweenCalls seq, flow:RtFlow option) =
            let calls = calls |> toList
            let arrows = arrows |> toList
            RtWork(calls, arrows, flow)
            |> tee (fun z ->
                calls  |> iter (setParentI z)
                arrows |> iter (setParentI z)
                flow   |> iter (setParentI z) )
        static member Create() = RtWork([], [], None)

    type RtCall with
        static member Create(callType:DbCallType, apiCalls:RtApiCall seq,
            autoPre:string, safety:string, isDisabled:bool, timeout:int option
        ) =
            let apiCallGuids = apiCalls |-> _.Guid
            RtCall(callType, apiCallGuids, autoPre, safety, isDisabled, timeout)
            |> tee (fun z ->
                apiCalls |> iter (setParentI z) )

        static member Create() = RtCall(DbCallType.Normal, [], nullString, nullString, false, None)

    type RtApiDef with
        static member Create() = RtApiDef(true)
    type RtApiCall with
        static member Create() = RtApiCall(emptyGuid, nullString, nullString, nullString, nullString, DbDataType.None, DbRangeType.Single, nullString, nullString)


    [<AbstractClass>]
    type ParameterBase() =
        interface IParameter

    type ProjectParameter() =
        // name, langVersion, engineVersion, description, author, dateTime, activeSystems, passiveSystems
        inherit ParameterBase()

    type SystemParameter() =
        inherit ParameterBase()

    type IParameterContainer with
        member x.GetParameter(): IParameter =
            match x with
            | :? RtProject as prj -> ProjectParameter() :> IParameter
            | :? RtSystem as sys -> SystemParameter()
            | _ -> failwith $"GetParameter not implemented for {x.GetType()}"


    type IArrow with
        member x.GetSource(): Unique =
            match x with
            | :? RtArrowBetweenCalls as a -> a.Source
            | :? RtArrowBetweenWorks as a -> a.Source
            | _ -> failwith "ERROR"

        member x.GetTarget(): Unique =
            match x with
            | :? RtArrowBetweenCalls as a -> a.Target
            | :? RtArrowBetweenWorks as a -> a.Target
            | _ -> failwith "ERROR"

        member x.GetArrowType(): DbArrowType =
            match x with
            | :? RtArrowBetweenCalls as a -> a.Type
            | :? RtArrowBetweenWorks as a -> a.Type
            | _ -> failwith "ERROR"

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

        member x.GetFQDN(): string =
            x.EnumerateAncestors()
            |> reverse
            |-> fun z -> if z.Name.IsNullOrEmpty() then $"[{z.GetType().Name}]" else z.Name
            |> String.concat "/"


    type RtUnique with
        member x.Validate(guidDic:Dictionary<Guid, RtUnique>) =
            verify (x.Guid <> emptyGuid)
            verify (x.DateTime <> minDate)
            match x with
            | :? RtProject | :? RtSystem | :? RtFlow  | :? RtWork  | :? RtCall -> verify (x.Name.NonNullAny())
            | _ -> ()

            match x with
            | :? RtProject as prj ->
                prj.Systems |> iter _.Validate(guidDic)
                for s in prj.Systems do
                    verify (s.RawParent.Value.Guid = prj.Guid)
            | :? RtSystem as sys ->
                sys.Works |> iter _.Validate(guidDic)


                for w in sys.Works  do
                    verify (w.RawParent.Value.Guid = sys.Guid)
                    for c in w.Calls do
                        c.ApiCalls |-> _.Guid |> forall(guidDic.ContainsKey) |> verify
                        c.ApiCalls |> forall (fun z -> sys.ApiCalls |> contains z) |> verify
                        for ac in c.ApiCalls do
                            ac.ApiDef.Guid = ac.ApiDefGuid |> verify
                            sys.ApiDefs |> contains ac.ApiDef |> verify

                sys.Arrows |> iter _.Validate(guidDic)
                for a in sys.Arrows do
                    verify (a.RawParent.Value.Guid = sys.Guid)
                    sys.Works |> contains a.Source |> verify
                    sys.Works |> contains a.Target |> verify

                sys.ApiDefs |> iter _.Validate(guidDic)
                for w in sys.ApiDefs do
                    verify (w.RawParent |-> _.Guid = Some sys.Guid)

                sys.ApiCalls |> iter _.Validate(guidDic)
                for ac in sys.ApiCalls  do
                    verify (ac.RawParent |-> _.Guid = Some sys.Guid)

            | :? RtFlow as flow ->
                let works = flow.Works
                works |> iter _.Validate(guidDic)
                for w in works  do
                    verify (w.Flow = Some flow)


            | :? RtWork as work ->
                work.Calls |> iter _.Validate(guidDic)
                for c in work.Calls do
                    verify (c.RawParent.Value.Guid = work.Guid)

                work.Arrows |> iter _.Validate(guidDic)
                for a in work.Arrows do
                    verify (a.RawParent.Value.Guid = work.Guid)
                    work.Calls |> contains a.Source |> verify
                    work.Calls |> contains a.Target |> verify


            | :? RtCall as call ->
                ()
            | _ ->
                //tracefn $"Skipping {(x.GetType())} in Validate"
                ()



