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
            let dateTime = dateTime |?? now
            x.EnumerateRtObjects().OfType<IWithDateTime>() |> iter (fun z -> z.DateTime <- dateTime)

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
                | :? RtFlow as flow ->
                    yield! flow.Buttons    >>= _.EnumerateRtObjects()
                    yield! flow.Lamps      >>= _.EnumerateRtObjects()
                    yield! flow.Conditions >>= _.EnumerateRtObjects()
                    yield! flow.Actions    >>= _.EnumerateRtObjects()

                | (:? RtCall) | (:? RtApiCall) | (:? RtApiDef) | (:? RtArrowBetweenWorks) | (:? RtArrowBetweenCalls)  ->
                    ()
                | _ ->
                    tracefn $"Skipping {(x.GetType())} in EnumerateRtObjects"
                    ()
            } |> List.ofSeq



    type RtProject with // AddPrototypeSystem, AddActiveSystem, AddPassiveSystem, Instantiate
        member x.AddPrototypeSystem(system:RtSystem) =
            system.IsPrototype <- true
            system |> setParent x |> x.RawPrototypeSystems.Add
            system

        member x.AddActiveSystem(system:RtSystem) =
            system |> setParent x |> x.RawActiveSystems.Add

        member x.AddPassiveSystem(system:RtSystem) =
            system |> setParent x |> x.RawPassiveSystems.Add

        /// project 내에 prototypeGuid 를 가진 prototype system 을 복사하여 instance 로 만들어 반환
        member x.Instantiate(prototypeSystem:RtSystem): RtSystem =
            assert(x.PrototypeSystems.Contains prototypeSystem)
            fwdDuplicate prototypeSystem :?> RtSystem
            |> tee (fun z ->
                z.PrototypeSystemGuid <- Some prototypeSystem.Guid
                z.IsPrototype <- false
                z |> setParentI x
                x.AddPassiveSystem z)

    type RtSystem with
        member internal x.addWorks(works:RtWork seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            works |> iter (setParentI x)
            works |> verifyAddRangeAsSet x.RawWorks

        member internal x.removeWorks(works:RtWork seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            let system = works |-> _.System.Value |> distinct |> exactlyOne
            let arrows = system.Arrows.Where(fun a -> works.Contains a.Source || works.Contains a.Target)
            system.removeArrows(arrows, ?byUI = byUI)
            works |> iter (fun w -> w.RawParent <- None)
            works |> iter (x.RawWorks.Remove >> ignore)


        member internal x.addFlows(flows:RtFlow seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            flows |> iter (setParentI x)
            flows |> verifyAddRangeAsSet x.RawFlows

        member internal x.removeFlows(flows:RtFlow seq, ?byUI:bool) =
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


        member internal x.addArrows(arrows:RtArrowBetweenWorks seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            arrows |> iter (setParentI x)
            arrows |> verifyAddRangeAsSet x.RawArrows

        member internal x.removeArrows(arrows:RtArrowBetweenWorks seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            arrows |> iter clearParentI
            arrows |> iter (x.RawArrows.Remove >> ignore)


        member internal x.addApiDefs(apiDefs:RtApiDef seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            apiDefs |> iter (setParentI x)
            apiDefs |> verifyAddRangeAsSet x.RawApiDefs

        member internal x.removeApiDefs(apiDefs:RtApiDef seq, ?byUI:bool) =
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


        member internal x.addApiCalls(apiCalls:RtApiCall seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            apiCalls |> iter (setParentI x)
            apiCalls |> verifyAddRangeAsSet x.RawApiCalls

        member internal x.removeApiCalls(apiCalls:RtApiCall seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            apiCalls |> iter clearParentI
            apiCalls |> iter (x.RawApiCalls.Remove >> ignore)



        member x.AddWorks      (works:RtWork seq)               = x.addWorks(works, true)
        member x.RemoveWorks   (works:RtWork seq)               = x.removeWorks(works, true)

        member x.AddFlows      (flows:RtFlow seq)               = x.addFlows(flows, true)
        member x.RemoveFlows   (flows:RtFlow seq)               = x.removeFlows(flows, true)

        member x.AddArrows     (arrows:RtArrowBetweenWorks seq) = x.addArrows(arrows, true)
        member x.RemoveArrows  (arrows:RtArrowBetweenWorks seq) = x.removeArrows(arrows, true)

        member x.AddApiDefs    (apiDefs:RtApiDef seq)           = x.addApiDefs(apiDefs, true)
        member x.RemoveApiDefs (apiDefs:RtApiDef seq)           = x.removeApiDefs(apiDefs, true)

        member x.AddApiCalls   (apiCalls:RtApiCall seq)         = x.addApiCalls(apiCalls, true)
        member x.RemoveApiCalls(apiCalls:RtApiCall seq)         = x.removeApiCalls(apiCalls, true)




    type RtFlow with    // {Add/Remove}{Works, Buttons, Lamps, Conditions, Actions}
        // works 들이 flow 자신의 직접 child 가 아니므로 따로 관리 함수 필요
        member internal x.addWorks(ws:RtWork seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            ws |> iter (fun w -> w.Flow <- Some x)

        member internal x.removeWorks(ws:RtWork seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            ws |> iter (fun w -> w.Flow <- None)

        member internal x.addButtons(buttons:RtButton seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            buttons |> iter (setParentI x)
            buttons |> verifyAddRangeAsSet x.RawButtons
        member internal x.removeButtons(buttons:RtButton seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            buttons |> iter clearParentI
            buttons |> iter (x.RawButtons.Remove >> ignore)


        member internal x.addLamps(lamps:RtLamp seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            lamps |> iter (setParentI x)
            lamps |> verifyAddRangeAsSet x.RawLamps
        member internal x.removeLamps(lamps:RtLamp seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            lamps |> iter clearParentI
            lamps |> iter (x.RawLamps.Remove >> ignore)

        member internal x.addConditions(conditions:RtCondition seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            conditions |> iter (setParentI x)
            conditions |> verifyAddRangeAsSet x.RawConditions
        member internal x.removeConditions(conditions:RtCondition seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            conditions |> iter clearParentI
            conditions |> iter (x.RawConditions.Remove >> ignore)

        member internal x.addActions(actions:RtAction seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            actions |> iter (setParentI x)
            actions |> verifyAddRangeAsSet x.RawActions
        member internal x.removeActions(actions:RtAction seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            actions |> iter clearParentI
            actions |> iter (x.RawActions.Remove >> ignore)


        member x.AddWorks        (ws:RtWork seq)              = x.addWorks        (ws, true)
        member x.RemoveWorks     (ws:RtWork seq)              = x.removeWorks     (ws, true)
        member x.AddButtons      (buttons:RtButton seq)       = x.addButtons      (buttons, true)
        member x.RemoveButtons   (buttons:RtButton seq)       = x.removeButtons   (buttons, true)
        member x.AddLamps        (lamps:RtLamp seq)           = x.addLamps        (lamps, true)
        member x.RemoveLamps     (lamps:RtLamp seq)           = x.removeLamps     (lamps, true)
        member x.AddConditions   (conditions:RtCondition seq) = x.addConditions   (conditions, true)
        member x.RemoveConditions(conditions:RtCondition seq) = x.removeConditions(conditions, true)
        member x.AddActions      (actions:RtAction seq)       = x.addActions      (actions, true)
        member x.RemoveActions   (actions:RtAction seq)       = x.removeActions   (actions, true)







    type RtWork with    // AddCalls, RemoveCalls, AddArrows, RemoveArrows
        member internal x.addCalls(calls:RtCall seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            calls |> iter (setParentI x)
            calls |> verifyAddRangeAsSet x.RawCalls
        member internal x.removeCalls(calls:RtCall seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            let work = calls |-> _.RawParent.Value |> distinct |> exactlyOne :?> RtWork
            let arrows = work.Arrows.Where(fun a -> calls.Contains a.Source || calls.Contains a.Target)
            work.removeArrows(arrows, ?byUI = byUI)
            calls |> iter clearParentI
            calls |> iter (x.RawCalls.Remove >> ignore)

        member internal x.addArrows(arrows:RtArrowBetweenCalls seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            arrows |> iter (setParentI x)
            arrows |> verifyAddRangeAsSet x.RawArrows


        member internal x.removeArrows(arrows:RtArrowBetweenCalls seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            arrows |> iter clearParentI
            arrows |> iter (x.RawArrows.Remove >> ignore)

        member x.AddCalls    (calls:RtCall seq)               = x.addCalls    (calls, true)
        member x.RemoveCalls (calls:RtCall seq)               = x.removeCalls (calls, true)
        member x.AddArrows   (arrows:RtArrowBetweenCalls seq) = x.addArrows   (arrows, true)
        member x.RemoveArrows(arrows:RtArrowBetweenCalls seq) = x.removeArrows(arrows, true)

    type RtCall with    // AddApiCalls
        member internal x.addApiCalls(apiCalls:RtApiCall seq, ?byUI:bool) =
            if byUI = Some true then x.UpdateDateTime()
            apiCalls |> iter (setParentI x)
            apiCalls |> iter (fun z -> x.ApiCallGuids.Add z.Guid)

        member x.AddApiCalls (apiCalls:RtApiCall seq)         = x.addApiCalls (apiCalls, true)












[<AutoOpen>]
module DsObjectUtilsModule =
    type RtProject with
        static member Create() = RtProject([||], [||], [||])

    type RtSystem with
        static member Create(flows:RtFlow[], works:RtWork[],
            arrows:RtArrowBetweenWorks[], apiDefs:RtApiDef[], apiCalls:RtApiCall[]
        ) =
            RtSystem(flows, works, arrows, apiDefs, apiCalls)
            |> tee (fun z ->
                flows    |> iter (setParentI z)
                works    |> iter (setParentI z)
                arrows   |> iter (setParentI z)
                apiDefs  |> iter (setParentI z)
                apiCalls |> iter (setParentI z) )

        static member Create() = RtSystem([||], [||], [||], [||], [||])

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
            autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option
        ) =
            let apiCallGuids = apiCalls |-> _.Guid

            RtCall(callType, apiCallGuids, autoConditions, commonConditions, isDisabled, timeout)
            |> tee (fun z ->
                apiCalls |> iter (setParentI z) )

        static member Create() = RtCall(DbCallType.Normal, [], [], [], false, None)

    type RtFlow with
        static member Create() = RtFlow([], [], [], [])

    type RtApiDef with
        static member Create() = RtApiDef(true)

    type RtApiCall with
        static member Create() =
            RtApiCall(emptyGuid, nullString, nullString, nullString, nullString,
                      Option<IValueSpec>.None)

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
        member x.Validate(guidDicDebug:Guid2UniqDic) =
            verify (x.Guid <> emptyGuid)
            x |> tryCast<IWithDateTime> |> iter(fun z -> verify (z.DateTime <> minDate))
            match x with
            | :? RtProject | :? RtSystem | :? RtFlow  | :? RtWork  | :? RtCall -> verify (x.Name.NonNullAny())
            | _ -> ()

            match x with
            | :? RtProject as prj ->
                prj.Systems |> iter _.Validate(guidDicDebug)
                for s in prj.Systems do
                    verify (prj.Guid |> isParentGuid s)
                for p in prj.PrototypeSystems do
                    p.IsPrototype |> verify
                    p.RawParent.IsSome |> verify

                prj.Systems |> iter (_.IsPrototype >> not >> verify)

            | :? RtSystem as sys ->
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

            | :? RtFlow as flow ->
                let works = flow.Works
                works |> iter _.Validate(guidDicDebug)
                for w in works  do
                    verify (w.Flow = Some flow)


            | :? RtWork as work ->
                work.Calls |> iter _.Validate(guidDicDebug)
                for c in work.Calls do
                    verify (work.Guid |> isParentGuid c)

                work.Arrows |> iter _.Validate(guidDicDebug)
                for a in work.Arrows do
                    verify (work.Guid |> isParentGuid a)
                    work.Calls |> contains a.Source |> verify
                    work.Calls |> contains a.Target |> verify


            | :? RtCall as call ->
                ()

            | :? RtApiCall as ac ->
                //verify (ac.ValueSpec.IsSome)
                ()

            | :? RtApiDef as ad ->
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
