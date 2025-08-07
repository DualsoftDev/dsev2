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













// C# 친화적인 정적 팩토리 클래스 - C#에서 직접 접근 가능
type DsObjectFactory =

    /// C#에서 AppSettings 초기화를 위한 헬퍼 메서드
    static member InitializeAppSettings() =
        try
            // AppSettings 인스턴스가 존재하지 않으면 새로 생성
            AppSettings() |> ignore
            "AppSettings initialized successfully"
        with
        | ex -> $"AppSettings initialization failed: {ex.Message}"


    /// 새로운 패턴: createExtended 사용
    static member CreateDsSystemExtended() =
        createExtended<DsSystem>()

    static member CreateDsSystem(flows:Flow[], works:Work[],
        arrows:ArrowBetweenWorks[], apiDefs:ApiDef[], apiCalls:ApiCall[]
    ) =
        createExtensible (fun () ->
            DsSystem(flows, works, arrows, apiDefs, apiCalls)
            |> tee (fun z ->
                flows    |> iter (setParentI z)
                works    |> iter (setParentI z)
                arrows   |> iter (setParentI z)
                apiDefs  |> iter (setParentI z)
                apiCalls |> iter (setParentI z) ) )


    /// 새로운 패턴: createExtended 사용
    static member CreateWorkExtended() =
        createExtended<Work>()

    static member CreateWork(calls:Call seq, arrows:ArrowBetweenCalls seq, flow:Flow option) =
        let calls = calls |> toList
        let arrows = arrows |> toList

        createExtensible (fun () ->
            Work(calls, arrows, flow)
            |> tee (fun z ->
                calls  |> iter (setParentI z)
                arrows |> iter (setParentI z)
                flow   |> iter (setParentI z) ) )


    /// 새로운 패턴: createExtended 사용
    static member CreateCallExtended() =
        createExtended<Call>()

    static member CreateCall(callType:DbCallType, apiCalls:ApiCall seq,
        autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option
    ) =
        let apiCallGuids = apiCalls |-> _.Guid

        createExtensible (fun () ->
            Call(callType, apiCallGuids, autoConditions, commonConditions, isDisabled, timeout)
            |> tee (fun z ->
                apiCalls |> iter (setParentI z) ) )





//// C#에서 직접 호출 가능한 static factory methods
//module DsObjectCreate =
//    let createProject() = createExtensible (fun () -> Project([], []))
//    let createDsSystem() = createExtensible (fun () -> DsSystem([||], [||], [||], [||], [||]))
//    let createWork() = createExtensible (fun () -> Work([], [], None))
//    let createCall() = createExtensible (fun () -> Call(DbCallType.Normal, [], [], [], false, None))
//    let createFlow() = createExtensible (fun () -> Flow([], [], [], []))
//    let createApiDef() = createExtensible (fun () -> ApiDef(true))
//    let createApiCall() = createExtensible (fun () ->
//        ApiCall(emptyGuid, nullString, nullString, nullString, nullString,
//                  Option<IValueSpec>.None))

// 남은 extension들을 module로 유지 (helper functions)
[<AutoOpen>]
module DsObjectUtilsModule =

    // 기존 코드 호환성을 위해 AutoOpen 모듈에도 Create 확장 추가
    type Project with   // Create
        static member Create() =
            createExtended<Project>()

    type DsSystem with   // Create, Initialize
        static member Create(flows:Flow[], works:Work[],
            arrows:ArrowBetweenWorks[], apiDefs:ApiDef[], apiCalls:ApiCall[]
        ) =
            // 매개변수가 있는 Create는 기본 구현 유지 (확장 타입에서 override 가능)
            DsSystem(flows, works, arrows, apiDefs, apiCalls)
            |> tee (fun z ->
                flows    |> iter (setParentI z)
                works    |> iter (setParentI z)
                arrows   |> iter (setParentI z)
                apiDefs  |> iter (setParentI z)
                apiCalls |> iter (setParentI z) )

        static member Create() =
            createExtended<DsSystem>()

        /// 새로운 디자인 패턴: Initialize 메서드
        member x.Initialize(flows:Flow[], works:Work[], arrows:ArrowBetweenWorks[],
                           apiDefs:ApiDef[], apiCalls:ApiCall[]) =
            // 기존 컬렉션 초기화
            x.RawFlows.Clear()
            x.RawWorks.Clear()
            x.RawArrows.Clear()
            x.RawApiDefs.Clear()
            x.RawApiCalls.Clear()

            // 새로운 데이터로 초기화
            flows    |> iter (setParentI x)
            works    |> iter (setParentI x)
            arrows   |> iter (setParentI x)
            apiDefs  |> iter (setParentI x)
            apiCalls |> iter (setParentI x)

            x.RawFlows.AddRange(flows)
            x.RawWorks.AddRange(works)
            x.RawArrows.AddRange(arrows)
            x.RawApiDefs.AddRange(apiDefs)
            x.RawApiCalls.AddRange(apiCalls)

            x

    type Work with   // Create, Initialize
        static member Create(calls:Call seq, arrows:ArrowBetweenCalls seq, flow:Flow option) =
            // 매개변수가 있는 Create는 기본 구현 유지 (확장 타입에서 override 가능)
            let calls = calls |> toList
            let arrows = arrows |> toList

            Work(calls, arrows, flow)
            |> tee (fun z ->
                calls  |> iter (setParentI z)
                arrows |> iter (setParentI z)
                flow   |> iter (setParentI z) )

        static member Create() =
            createExtended<Work>()

        /// 새로운 디자인 패턴: Initialize 메서드
        member x.Initialize(calls:Call seq, arrows:ArrowBetweenCalls seq, flow:Flow option) =
            let calls = calls |> toList
            let arrows = arrows |> toList

            // 기존 컬렉션 초기화
            x.RawCalls.Clear()
            x.RawArrows.Clear()
            x.Flow <- None

            // 새로운 데이터로 초기화
            calls  |> iter (setParentI x)
            arrows |> iter (setParentI x)
            flow   |> iter (setParentI x)

            x.RawCalls.AddRange(calls)
            x.RawArrows.AddRange(arrows)
            x.Flow <- flow

            x

    type Call with   // Create, Initialize
        static member Create(callType:DbCallType, apiCalls:ApiCall seq,
            autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option
        ) =
            // 매개변수가 있는 Create는 기본 구현 유지 (확장 타입에서 override 가능)
            let apiCallGuids = apiCalls |-> _.Guid

            Call(callType, apiCallGuids, autoConditions, commonConditions, isDisabled, timeout)
            |> tee (fun z ->
                apiCalls |> iter (setParentI z) )

        static member Create() =
            createExtended<Call>()

        /// 새로운 디자인 패턴: Initialize 메서드
        member x.Initialize(callType:DbCallType, apiCalls:ApiCall seq,
                           autoConditions:string seq, commonConditions:string seq,
                           isDisabled:bool, timeout:int option) =
            let apiCallGuids = apiCalls |-> _.Guid

            // 기존 데이터 초기화
            x.ApiCallGuids.Clear()

            // 새로운 데이터로 초기화
            x.CallType <- callType
            x.IsDisabled <- isDisabled
            x.Timeout <- timeout

            x.ApiCallGuids.AddRange(apiCallGuids)
            apiCalls |> iter (setParentI x)

            x

    type Flow with   // Create
        static member Create() =
            createExtended<Flow>()

    type ApiDef with   // Create
        static member Create() =
            createExtended<ApiDef>()

    type ApiCall with   // Create
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
        | [] -> null
        | xs -> xs |> JsonConvert.SerializeObject

    let jsonDeserializeStrings(json:string): string[] =
        if json.IsNullOrEmpty() then
            [||]
        else
            JsonConvert.DeserializeObject<string[]>(json)

    let isStringsEqual (xs:string seq) (ys:string seq) =
        Set.ofSeq xs = Set.ofSeq ys


// =========================================================
// C# 친화적 Type Aliases - 짧은 네임스페이스로 접근 가능
// =========================================================

// NOTE: F# type abbreviation으로는 멤버 추가가 안되므로
// TypeFactory 클래스만 사용하여 C# 친화적 접근 제공

/// C# 친화적인 새로운 Factory 클래스
/// DsObjectFactory를 대체하여 더 간결한 네임스페이스 사용
/// 타입들이 namespace 최상위로 이동되어 더욱 간결해짐
type TypeFactory() =

    /// Project 인스턴스 생성 (확장 타입 지원)
    static member CreateProject() = createExtended<Project>()

    /// DsSystem 인스턴스 생성 (확장 타입 지원)
    static member CreateDsSystem() = createExtended<DsSystem>()

    /// Flow 인스턴스 생성 (확장 타입 지원)
    static member CreateFlow() = createExtended<Flow>()

    /// Work 인스턴스 생성 (확장 타입 지원)
    static member CreateWork() = createExtended<Work>()

    /// Call 인스턴스 생성 (확장 타입 지원)
    static member CreateCall() = createExtended<Call>()

    /// ApiDef 인스턴스 생성 (확장 타입 지원)
    static member CreateApiDef() = createExtended<ApiDef>()

    /// ApiCall 인스턴스 생성 (확장 타입 지원)
    static member CreateApiCall() = createExtended<ApiCall>()
