namespace Ev2.Core.FS

open System
open System.Linq
open System.Collections.Generic
open System.Runtime.CompilerServices
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base

[<AutoOpen>]
module rec TmpCompatibility =
    type Guid2UniqDic = Dictionary<Guid, Unique>

    type RtUnique with     // UpdateDateTime, EnumerateRtObjects
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



    type Project with     // AddActiveSystem, AddPassiveSystem
        member x.AddActiveSystem(system:DsSystem) =
            system |> setParent x |> ignore
            x.RawActiveSystems.AddAsSet(system, Unique.isDuplicated)

        member x.AddPassiveSystem(system:DsSystem) =
            system |> setParent x |> ignore
            x.RawPassiveSystems.AddAsSet(system, Unique.isDuplicated)

    type DsSystem with     // AddWorks, RemoveWorks, AddFlows, RemoveFlows, AddArrows, RemoveArrows, AddApiDefs, RemoveApiDefs, AddApiCalls, RemoveApiCalls
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
                    try
                        apiDefsDic.TryGet(ac.ApiDef)      // apiCall 중에서 ac.ApiDef 가 삭제 대상인 apiDefs 에 포함된 것들만 선택
                        |-> fun f -> ac
                    with _ -> None)  // ApiDef 접근 실패 시 None
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













// C# 친화적인 정적 팩토리 클래스 - C#에서 직접 접근 가능
type DsObjectFactory =

    /// C#에서 AppSettings 초기화를 위한 헬퍼 메서드
    static member InitializeAppSettings() =
        // AppSettings 인스턴스가 존재하지 않으면 새로 생성
        AppSettings() |> ignore
        "AppSettings initialized successfully"


    /// 새로운 패턴: createExtended 사용
    static member CreateProject() = createExtended<Project>()
    static member CreateDsSystemExtended() =
        createExtended<DsSystem>()
    static member CreateDsSystem() = createExtended<DsSystem>()
    static member CreateWork() = createExtended<Work>()
    static member CreateCall() = createExtended<Call>()
    static member CreateFlow() = createExtended<Flow>()
    static member CreateApiDef() = createExtended<ApiDef>()
    static member CreateApiCall() = createExtended<ApiCall>()

    static member CreateDsSystem(flows:Flow[], works:Work[],
        arrows:ArrowBetweenWorks[], apiDefs:ApiDef[], apiCalls:ApiCall[]
    ) =
        let system = createExtended<DsSystem>()
        system.RawFlows.Clear()
        system.RawWorks.Clear()
        system.RawArrows.Clear()
        system.RawApiDefs.Clear()
        system.RawApiCalls.Clear()
        system.RawFlows.AddRange(flows)
        system.RawWorks.AddRange(works)
        system.RawArrows.AddRange(arrows)
        system.RawApiDefs.AddRange(apiDefs)
        system.RawApiCalls.AddRange(apiCalls)
        flows    |> iter (setParentI system)
        works    |> iter (setParentI system)
        arrows   |> iter (setParentI system)
        apiDefs  |> iter (setParentI system)
        apiCalls |> iter (setParentI system)
        system


    /// 새로운 패턴: createExtended 사용
    static member CreateWorkExtended() =
        createExtended<Work>()

    static member CreateWork(calls:Call seq, arrows:ArrowBetweenCalls seq, flow:Flow option) =
        let calls = calls |> toList
        let arrows = arrows |> toList

        let work = createExtended<Work>()
        work.RawCalls.Clear()
        work.RawArrows.Clear()
        work.RawCalls.AddRange(calls)
        work.RawArrows.AddRange(arrows)
        work.Flow <- flow
        calls  |> iter (setParentI work)
        arrows |> iter (setParentI work)
        work


    /// 새로운 패턴: createExtended 사용
    static member CreateCallExtended() =
        createExtended<Call>()

    static member CreateCall(callType:DbCallType, apiCalls:ApiCall seq,
        autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option
    ) =
        let apiCallGuids = apiCalls |-> _.Guid

        let call = createExtended<Call>()
        call.CallType <- callType
        call.IsDisabled <- isDisabled
        call.Timeout <- timeout
        call.AutoConditions.Clear()
        call.CommonConditions.Clear()
        call.ApiCallGuids.Clear()
        call.AutoConditions.AddRange(autoConditions)
        call.CommonConditions.AddRange(commonConditions)
        call.ApiCallGuids.AddRange(apiCallGuids)
        apiCalls |> iter (setParentI call)
        call




// F# 타입 확장 및 helper functions (F#과 C#에서 사용)
[<AutoOpen>]
module DsObjectUtilsModule =

    // F# 코드 호환성을 위한 타입 확장 (static member)
    type Project with   // Create, Initialize
        static member Create(activeSystems:DsSystem seq, passiveSystems:DsSystem seq) =
            // 매개변수가 있는 경우 직접 생성자 사용하거나 확장 타입에서 initialize
            let project = createExtended<Project>()
            project.RawActiveSystems.Clear()
            project.RawPassiveSystems.Clear()
            project.RawActiveSystems.AddRange(activeSystems)
            project.RawPassiveSystems.AddRange(passiveSystems)
            activeSystems  |> iter (setParentI project)
            passiveSystems |> iter (setParentI project)
            project

        static member Create() =
            createExtended<Project>()


    type DsSystem with   // Create, Initialize
        static member Create(flows:Flow[], works:Work[],
            arrows:ArrowBetweenWorks[], apiDefs:ApiDef[], apiCalls:ApiCall[]
        ) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let system = createExtended<DsSystem>()
            system.RawFlows.Clear()
            system.RawWorks.Clear()
            system.RawArrows.Clear()
            system.RawApiDefs.Clear()
            system.RawApiCalls.Clear()
            system.RawFlows.AddRange(flows)
            system.RawWorks.AddRange(works)
            system.RawArrows.AddRange(arrows)
            system.RawApiDefs.AddRange(apiDefs)
            system.RawApiCalls.AddRange(apiCalls)
            // parent 관계 설정 추가
            flows    |> iter (setParentI system)
            works    |> iter (setParentI system)
            arrows   |> iter (setParentI system)
            apiDefs  |> iter (setParentI system)
            apiCalls |> iter (setParentI system)
            system

        static member Create() =
            // 매개변수가 없는 경우만 확장 타입 사용
            createExtended<DsSystem>()


    type Work with   // Create, Initialize
        static member Create(calls:Call seq, arrows:ArrowBetweenCalls seq, flow:Flow option) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let work = createExtended<Work>()
            work.RawCalls.Clear()
            work.RawArrows.Clear()
            work.RawCalls.AddRange(calls)
            work.RawArrows.AddRange(arrows)
            work.Flow <- flow
            calls  |> iter (setParentI work)
            arrows |> iter (setParentI work)
            work

        static member Create() =
            createExtended<Work>()


    type Call with   // Create, Initialize
        static member Create(callType:DbCallType, apiCalls:ApiCall seq,
            autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option
        ) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let call = createExtended<Call>()
            call.CallType <- callType
            call.IsDisabled <- isDisabled
            call.Timeout <- timeout
            call.AutoConditions.Clear()
            call.CommonConditions.Clear()
            call.ApiCallGuids.Clear()
            call.AutoConditions.AddRange(autoConditions)
            call.CommonConditions.AddRange(commonConditions)
            call.ApiCallGuids.AddRange(apiCalls |-> _.Guid)
            call

        static member Create() =
            createExtended<Call>()


    type Flow with   // Create, Initialize
        static member Create(buttons:DsButton seq, lamps:Lamp seq, conditions:DsCondition seq, actions:DsAction seq) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let flow = createExtended<Flow>()
            flow.RawButtons.Clear()
            flow.RawLamps.Clear()
            flow.RawConditions.Clear()
            flow.RawActions.Clear()
            flow.RawButtons.AddRange(buttons)
            flow.RawLamps.AddRange(lamps)
            flow.RawConditions.AddRange(conditions)
            flow.RawActions.AddRange(actions)
            buttons    |> iter (fun z -> z.RawParent <- Some flow)
            lamps      |> iter (fun z -> z.RawParent <- Some flow)
            conditions |> iter (fun z -> z.RawParent <- Some flow)
            actions    |> iter (fun z -> z.RawParent <- Some flow)
            flow

        static member Create() =
            createExtended<Flow>()


    type ApiDef with   // Create
        static member Create(isPush:bool) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let apiDef = createExtended<ApiDef>()
            apiDef.IsPush <- isPush
            apiDef

        static member Create() =
            createExtended<ApiDef>()

    type ApiCall with   // Create
        static member Create(apiDefGuid:Guid, inAddress:string, outAddress:string, inSymbol:string, outSymbol:string, valueSpec:IValueSpec option) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let apiCall = createExtended<ApiCall>()
            apiCall.ApiDefGuid <- apiDefGuid
            apiCall.InAddress <- inAddress
            apiCall.OutAddress <- outAddress
            apiCall.InSymbol <- inSymbol
            apiCall.OutSymbol <- outSymbol
            apiCall.ValueSpec <- valueSpec
            apiCall

        static member Create() =
            createExtended<ApiCall>()

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
                        // ApiCalls가 비어있을 수 있음 (NjSystem 등의 경우)
                        if not (c.ApiCalls.IsEmpty) then
                            c.ApiCalls |-> _.Guid |> forall(guidDicDebug.ContainsKey) |> verify
                            c.ApiCalls |> forall (fun z -> sys.ApiCalls |> contains z) |> verify
                            for ac in c.ApiCalls do
                                try
                                    ac.ApiDef.Guid = ac.ApiDefGuid |> verify
                                    sys.ApiDefs |> contains ac.ApiDef |> verify
                                with ex ->
                                    logWarn $"Exception while validating ApiCall: {ex.Message}"
                                    ()  // NjSystem 등에서 ApiDef 접근 실패 시 무시

                sys.Arrows |> iter _.Validate(guidDicDebug)
                for a in sys.Arrows do
                    verify (sys.Guid |> isParentGuid a)
                    sys.Works |> contains a.Source |> verify
                    sys.Works |> contains a.Target |> verify

                sys.ApiDefs |> iter _.Validate(guidDicDebug)
                for w in sys.ApiDefs do
                    verify (sys.Guid |> isParentGuid w)

                // ApiDef 의 TopicIndex 점검
                // 1. 동일 TopicIndex 를 가진 ApiDef 의 갯수는 항상 2가 되어야 함
                sys.ApiDefs
                |> filter (_.TopicIndex.IsSome)
                |> groupBy (_.TopicIndex)
                |> iter (fun (topicIndex, apiDefs) ->
                    let count = apiDefs |> List.length
                    if count <> 2 then
                        failwith $"TopicIndex {topicIndex}를 가진 ApiDef의 개수가 {count}개입니다. 2개여야 합니다.")

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
        | [] -> null  // 빈 리스트는 null 반환
        | xs -> xs |> JsonConvert.SerializeObject

    let jsonDeserializeStrings(json:string): string[] =
        if json.IsNullOrEmpty() then
            [||]
        else
            JsonConvert.DeserializeObject<string[]>(json)

    let isStringsEqual (xs:string seq) (ys:string seq) =
        Set.ofSeq xs = Set.ofSeq ys
