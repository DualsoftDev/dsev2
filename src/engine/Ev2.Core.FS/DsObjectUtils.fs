namespace Ev2.Core.FS

open System
open System.Linq
open System.Collections.Generic
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base

[<AutoOpen>]
module rec TmpCompatibility =
    type Guid2UniqDic = Dictionary<Guid, Unique>

    type RtUnique with  // UpdateDateTime, EnumerateRtObjects
        /// DS object 의 모든 상위 DS object 의 DateTime 을 갱신.  (tree 구조를 따라가면서 갱신)
        ///
        /// project, system 만 date time 가지는 걸로 변경 고려 중..
        member x.UpdateDateTime(?dateTime:DateTime) =
            let dateTime = dateTime |?? (fun () -> now().TruncateToSecond())
            x.EnumerateRtObjects().OfType<IWithDateTime>() |> iter (fun z -> z.DateTime <- dateTime)

        (* see also EdUnique.EnumerateRtObjects *)
        member x.EnumerateRtObjects(?includeMe): RtUnique list =
            seq {
                let includeMe = includeMe |? true
                if includeMe then
                    yield x
                match x with
                | :? Project as prj ->
                    yield! prj.Systems   >>= _.EnumerateRtObjects()
                | :? DsSystem as sys ->
                    yield! sys.Works     >>= _.EnumerateRtObjects()
                    yield! sys.Flows     >>= _.EnumerateRtObjects()
                    yield! sys.Arrows    >>= _.EnumerateRtObjects()
                    yield! sys.ApiDefs   >>= _.EnumerateRtObjects()
                    yield! sys.ApiCalls  >>= _.EnumerateRtObjects()
                | :? Work as work ->
                    yield! work.Calls    >>= _.EnumerateRtObjects()
                    yield! work.Arrows   >>= _.EnumerateRtObjects()
                | :? Flow as flow ->
                    yield! flow.Buttons    >>= _.EnumerateRtObjects()
                    yield! flow.Lamps      >>= _.EnumerateRtObjects()
                    yield! flow.Conditions >>= _.EnumerateRtObjects()
                    yield! flow.Actions    >>= _.EnumerateRtObjects()

                | (:? Call) | (:? ApiCall) | (:? ApiDef) | (:? ArrowBetweenWorks) | (:? ArrowBetweenCalls)  ->
                    ()
                | _ ->
                    tracefn $"Skipping {(x.GetType())} in EnumerateRtObjects"
                    ()
            } |> List.ofSeq



    type Project with // AddActiveSystem, AddPassiveSystem, Instantiate
        member x.AddActiveSystem(system:DsSystem) =
            system |> setParent x |> ignore
            x.RawActiveSystems.AddAsSet(system, Unique.isDuplicated)

        member x.AddPassiveSystem(system:DsSystem) =
            system |> setParent x |> ignore
            x.RawPassiveSystems.AddAsSet(system, Unique.isDuplicated)

    type DsSystem with
        member internal x.addWorks(works:Work seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            works |> iter (setParentI x)
            x.RawWorks.VerifyAddRangeAsSet(works, Unique.isDuplicated)

        member internal x.removeWorks(works:Work seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            let system = works |-> _.System.Value |> distinct |> exactlyOne
            let arrows = system.Arrows.Where(fun a -> works.Contains a.Source || works.Contains a.Target)
            system.removeArrows(arrows, ?byUI = byUI)
            works |> iter (fun w -> w.RawParent <- None)
            works |> iter (x.RawWorks.Remove >> ignore)


        member internal x.addFlows(flows:Flow seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            flows |> iter (setParentI x)
            x.RawFlows.VerifyAddRangeAsSet(flows, Unique.isDuplicated)

        member internal x.removeFlows(flows:Flow seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            let flowsDic = flows |> HashSet
            // 삭제 대상인 flows 를 쳐다보고 있는 works 들을 찾아서, 그들의 Flow 를 None 으로 설정
            x.Works
            |> choose( fun w ->
                w.Flow >>= flowsDic.TryGet      // work 중에서 w.Flow 가 삭제 대상인 flows 에 포함된 것들만 선택
                |-> fun f -> w)
            |> iter (fun w -> w.Flow <- None)  // 선택된 works 의 Flow 를 None 으로 설정

            flows |> iter clearParentI
            flows |> iter (x.RawFlows.Remove >> ignore)


        member internal x.addArrows(arrows:ArrowBetweenWorks seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            arrows |> iter (setParentI x)
            x.RawArrows.VerifyAddRangeAsSet(arrows)

        member internal x.removeArrows(arrows:ArrowBetweenWorks seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            arrows |> iter clearParentI
            arrows |> iter (x.RawArrows.Remove >> ignore)


        member internal x.addApiDefs(apiDefs:ApiDef seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            apiDefs |> iter (setParentI x)
            x.RawApiDefs.VerifyAddRangeAsSet(apiDefs, Unique.isDuplicated)

        member internal x.removeApiDefs(apiDefs:ApiDef seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()

            // 삭제 대상인 ApiDef 을 쳐다보고 있는 ApiCall 들을 삭제
            let apiDefsDic = apiDefs |> HashSet
            let apiCallsToRemove =
                x.ApiCalls
                |> choose(fun ac ->
                    apiDefsDic.TryGet(ac.ApiDef)      // apiCall 중에서 ac.ApiDef 가 삭제 대상인 apiDefs 에 포함된 것들만 선택
                    |-> fun f -> ac)
            apiCallsToRemove |> iter (x.RawApiCalls.Remove >> ignore) // 선택된 works 의 Flow 를 None 으로 설정

            apiDefs |> iter clearParentI
            apiDefs |> iter (x.RawApiDefs.Remove >> ignore)


        member internal x.addApiCalls(apiCalls:ApiCall seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            apiCalls |> iter (setParentI x)
            x.RawApiCalls.VerifyAddRangeAsSet(apiCalls, Unique.isDuplicated)

        member internal x.removeApiCalls(apiCalls:ApiCall seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            apiCalls |> iter clearParentI
            apiCalls |> iter (x.RawApiCalls.Remove >> ignore)



        member x.AddWorks      (works:Work seq)               = x.addWorks(works, true)
        member x.RemoveWorks   (works:Work seq)               = x.removeWorks(works, true)

        member x.AddFlows      (flows:Flow seq)               = x.addFlows(flows, true)
        member x.RemoveFlows   (flows:Flow seq)               = x.removeFlows(flows, true)

        member x.AddArrows     (arrows:ArrowBetweenWorks seq) = x.addArrows(arrows, true)
        member x.RemoveArrows  (arrows:ArrowBetweenWorks seq) = x.removeArrows(arrows, true)

        member x.AddApiDefs    (apiDefs:ApiDef seq)           = x.addApiDefs(apiDefs, true)
        member x.RemoveApiDefs (apiDefs:ApiDef seq)           = x.removeApiDefs(apiDefs, true)

        member x.AddApiCalls   (apiCalls:ApiCall seq)         = x.addApiCalls(apiCalls, true)
        member x.RemoveApiCalls(apiCalls:ApiCall seq)         = x.removeApiCalls(apiCalls, true)




    type Flow with    // {Add/Remove}{Works, Buttons, Lamps, Conditions, Actions}
        // works 들이 flow 자신의 직접 child 가 아니므로 따로 관리 함수 필요
        member internal x.addWorks(ws:Work seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            ws |> iter (fun w -> w.Flow <- Some x)

        member internal x.removeWorks(ws:Work seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            ws |> iter (fun w -> w.Flow <- None)

        member internal x.addButtons(buttons:DsButton seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            buttons |> iter (setParentI x)
            x.RawButtons.VerifyAddRangeAsSet(buttons)
        member internal x.removeButtons(buttons:DsButton seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            buttons |> iter clearParentI
            buttons |> iter (x.RawButtons.Remove >> ignore)


        member internal x.addLamps(lamps:Lamp seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            lamps |> iter (setParentI x)
            x.RawLamps.VerifyAddRangeAsSet(lamps)
        member internal x.removeLamps(lamps:Lamp seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            lamps |> iter clearParentI
            lamps |> iter (x.RawLamps.Remove >> ignore)

        member internal x.addConditions(conditions:DsCondition seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            conditions |> iter (setParentI x)
            x.RawConditions.VerifyAddRangeAsSet(conditions)
        member internal x.removeConditions(conditions:DsCondition seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            conditions |> iter clearParentI
            conditions |> iter (x.RawConditions.Remove >> ignore)

        member internal x.addActions(actions:DsAction seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            actions |> iter (setParentI x)
            x.RawActions.VerifyAddRangeAsSet(actions)
        member internal x.removeActions(actions:DsAction seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            actions |> iter clearParentI
            actions |> iter (x.RawActions.Remove >> ignore)


        member x.AddWorks        (ws:Work seq)              = x.addWorks        (ws, true)
        member x.RemoveWorks     (ws:Work seq)              = x.removeWorks     (ws, true)
        member x.AddButtons      (buttons:DsButton seq)       = x.addButtons      (buttons, true)
        member x.RemoveButtons   (buttons:DsButton seq)       = x.removeButtons   (buttons, true)
        member x.AddLamps        (lamps:Lamp seq)           = x.addLamps        (lamps, true)
        member x.RemoveLamps     (lamps:Lamp seq)           = x.removeLamps     (lamps, true)
        member x.AddConditions   (conditions:DsCondition seq) = x.addConditions   (conditions, true)
        member x.RemoveConditions(conditions:DsCondition seq) = x.removeConditions(conditions, true)
        member x.AddActions      (actions:DsAction seq)       = x.addActions      (actions, true)
        member x.RemoveActions   (actions:DsAction seq)       = x.removeActions   (actions, true)







    type Work with    // AddCalls, RemoveCalls, AddArrows, RemoveArrows
        member internal x.addCalls(calls:Call seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            calls |> iter (setParentI x)
            x.RawCalls.VerifyAddRangeAsSet(calls)
        member internal x.removeCalls(calls:Call seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            let work = calls |-> _.RawParent.Value |> distinct |> exactlyOne :?> Work
            let arrows = work.Arrows.Where(fun a -> calls.Contains a.Source || calls.Contains a.Target)
            work.removeArrows(arrows, ?byUI = byUI)
            calls |> iter clearParentI
            calls |> iter (x.RawCalls.Remove >> ignore)

        member internal x.addArrows(arrows:ArrowBetweenCalls seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            arrows |> iter (setParentI x)
            x.RawArrows.VerifyAddRangeAsSet(arrows)


        member internal x.removeArrows(arrows:ArrowBetweenCalls seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            arrows |> iter clearParentI
            arrows |> iter (x.RawArrows.Remove >> ignore)

        member x.AddCalls    (calls:Call seq)               = x.addCalls    (calls, true)
        member x.RemoveCalls (calls:Call seq)               = x.removeCalls (calls, true)
        member x.AddArrows   (arrows:ArrowBetweenCalls seq) = x.addArrows   (arrows, true)
        member x.RemoveArrows(arrows:ArrowBetweenCalls seq) = x.removeArrows(arrows, true)

    type Call with    // AddApiCalls
        member internal x.addApiCalls(apiCalls:ApiCall seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            apiCalls |> iter (setParentI x)
            apiCalls |> iter (fun z -> x.ApiCallGuids.Add z.Guid)

        member x.AddApiCalls (apiCalls:ApiCall seq)         = x.addApiCalls (apiCalls, true)












[<AutoOpen>]
module DsObjectUtilsModule =
    type Project with
        static member Create() = Project([], [])

    type DsSystem with
        static member Create(flows:Flow[], works:Work[],
            arrows:ArrowBetweenWorks[], apiDefs:ApiDef[], apiCalls:ApiCall[]
        ) =
            DsSystem(flows, works, arrows, apiDefs, apiCalls)
            |> tee (fun z ->
                flows    |> iter (setParentI z)
                works    |> iter (setParentI z)
                arrows   |> iter (setParentI z)
                apiDefs  |> iter (setParentI z)
                apiCalls |> iter (setParentI z) )

        static member Create() = DsSystem([||], [||], [||], [||], [||])

    type Work with
        static member Create(calls:Call seq, arrows:ArrowBetweenCalls seq, flow:Flow option) =
            let calls = calls |> toList
            let arrows = arrows |> toList

            Work(calls, arrows, flow)
            |> tee (fun z ->
                calls  |> iter (setParentI z)
                arrows |> iter (setParentI z)
                flow   |> iter (setParentI z) )

        static member Create() = Work([], [], None)

    type Call with
        static member Create(callType:DbCallType, apiCalls:ApiCall seq,
            autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option
        ) =
            let apiCallGuids = apiCalls |-> _.Guid

            Call(callType, apiCallGuids, autoConditions, commonConditions, isDisabled, timeout)
            |> tee (fun z ->
                apiCalls |> iter (setParentI z) )

        static member Create() = Call(DbCallType.Normal, [], [], [], false, None)

    type Flow with
        static member Create() = Flow([], [], [], [])

    type ApiDef with
        static member Create() = ApiDef(true)

    type ApiCall with
        static member Create() =
            ApiCall(emptyGuid, nullString, nullString, nullString, nullString,
                      Option<IValueSpec>.None)

    type IArrow with
        member x.GetSource(): Unique =
            match x with
            | :? ArrowBetweenCalls as a -> a.Source
            | :? ArrowBetweenWorks as a -> a.Source
            | _ -> failwith "ERROR"

        member x.GetTarget(): Unique =
            match x with
            | :? ArrowBetweenCalls as a -> a.Target
            | :? ArrowBetweenWorks as a -> a.Target
            | _ -> failwith "ERROR"

        member x.GetArrowType(): DbArrowType =
            match x with
            | :? ArrowBetweenCalls as a -> a.Type
            | :? ArrowBetweenWorks as a -> a.Type
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
        member x.Validate(guidDicDebug:Guid2UniqDic) =
            verify (x.Guid <> emptyGuid)
            x |> tryCast<IWithDateTime> |> iter(fun z -> verify (z.DateTime <> minDate))
            match x with
            | :? Project | :? DsSystem | :? Flow  | :? Work  | :? Call -> verify (x.Name.NonNullAny())
            | _ -> ()

            match x with
            | :? Project as prj ->
                prj.Systems |> iter _.Validate(guidDicDebug)
                for s in prj.Systems do
                    verify (prj.Guid |> isParentGuid s)

            | :? DsSystem as sys ->
                sys.Works |> iter _.Validate(guidDicDebug)


                for w in sys.Works  do
                    verify (sys.Guid |> isParentGuid w)
                    for c in w.Calls do
                        c.ApiCalls |-> _.Guid |> forall(guidDicDebug.ContainsKey) |> verify
                        c.ApiCalls |> forall (fun z -> sys.ApiCalls |> contains z) |> verify
                        for ac in c.ApiCalls do
                            ac.ApiDef.Guid = ac.ApiDefGuid |> verify
                            sys.ApiDefs |> contains ac.ApiDef |> verify

                sys.Arrows |> iter _.Validate(guidDicDebug)
                for a in sys.Arrows do
                    verify (sys.Guid |> isParentGuid a)
                    sys.Works |> contains a.Source |> verify
                    sys.Works |> contains a.Target |> verify

                sys.ApiDefs |> iter _.Validate(guidDicDebug)
                for w in sys.ApiDefs do
                    verify (sys.Guid |> isParentGuid w)

                sys.ApiCalls |> iter _.Validate(guidDicDebug)
                for ac in sys.ApiCalls  do
                    verify (sys.Guid |> isParentGuid ac)

            | :? Flow as flow ->
                let works = flow.Works
                works |> iter _.Validate(guidDicDebug)
                for w in works  do
                    verify (w.Flow = Some flow)


            | :? Work as work ->
                work.Calls |> iter _.Validate(guidDicDebug)
                for c in work.Calls do
                    verify (work.Guid |> isParentGuid c)

                work.Arrows |> iter _.Validate(guidDicDebug)
                for a in work.Arrows do
                    verify (work.Guid |> isParentGuid a)
                    work.Calls |> contains a.Source |> verify
                    work.Calls |> contains a.Target |> verify


            | :? Call as call ->
                ()

            | :? ApiCall as ac ->
                //verify (ac.ValueSpec.IsSome)
                ()

            | :? ApiDef as ad ->
                ()


            | _ ->
                //tracefn $"Skipping {(x.GetType())} in Validate"
                ()

    // for debugging/test only
    type RtTypeTest() =
        member val Id           = Nullable<int>()   with get, set
        member val Guid         = Guid.Empty        with get, set
        member val OptionGuid   = Option<Guid>.None with get, set
        member val NullableGuid = Nullable<Guid>()  with get, set
        member val OptionInt    = Option<int>.None  with get, set
        member val NullableInt  = Nullable<int>()   with get, set
        member val Jsonb        = null:string       with get, set
        member val DateTime     = DateTime.MinValue with get, set

    let jsonSerializeStrings(strings:string seq) =
        match strings |> toList with
        | [] -> null
        | xs -> xs |> JsonConvert.SerializeObject

    let jsonDeserializeStrings(json:string): string[] =
        if json.IsNullOrEmpty() then
            [||]
        else
            JsonConvert.DeserializeObject<string[]>(json)

    let isStringsEqual (xs:string seq) (ys:string seq) =
        Set.ofSeq xs = Set.ofSeq ys
