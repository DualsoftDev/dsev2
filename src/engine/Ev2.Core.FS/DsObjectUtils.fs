namespace Ev2.Core.FS

open System
open System.Linq
open System.Collections.Generic
open System.Runtime.CompilerServices
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base

[<AutoOpen>]
module DuplicateBagModule =
    /// Work <-> Flow, Arrow <-> Call, Arrow <-> Work 간의 참조를 찾기 위한 bag
    type DuplicateBag(src:IDictionary<Guid, Unique>) =
        new() = DuplicateBag(Dictionary<Guid, Unique>())
        /// OldGuid -> New object
        member val OldGuid2NewObjectMap = Dictionary<Guid, Unique>(src)
        member val OldGuid2NewGuidMap = Dictionary<Guid, Guid>()
        member x.Add(old:Guid, neo:Guid) = x.OldGuid2NewGuidMap.Add(old, neo)

        /// 복사할 때, 충돌나지 않도록 하기 위한 조치 수행
        member val Disambiguate =
            Action<Unique>(fun (rtObj:Unique) ->
                let num = Guid.NewGuid().ToString("N").Substring(0, 8)
                match rtObj with
                | :? Project as rt -> rt.Name <- $"{rt.Name}_{num}"
                | :? DsSystem as rt ->
                    rt.Name <- $"{rt.Name}_{num}"
                    rt.IRI <- $"{rt.IRI}_{num}"
                | _ -> ()
            ) with get, set


[<AutoOpen>]
module rec TmpCompatibility =
    let rec enumerateHelper (visited:HashSet<Guid>) (includeMe:bool option) (x:RtUnique) : RtUnique seq =
        if visited.Contains x.Guid then
            Seq.empty
        else
            visited.Add x.Guid |> ignore
            let enumerates (rtObjects:#RtUnique seq) =
                rtObjects
                |> Seq.collect (enumerateHelper visited None)

            seq {
                let includeMe = includeMe |? true
                if includeMe then
                    yield x
                match x with
                | :? Project as prj ->
                    yield! prj.Systems |> enumerates
                | :? DsSystem as sys ->
                    // TODO: Entities 누락.  RtUnique 아님!!
                    yield! sys.Works     |> enumerates
                    yield! sys.Flows     |> enumerates
                    yield! sys.Arrows    |> enumerates
                    yield! sys.ApiDefs   |> enumerates
                    yield! sys.ApiCalls  |> enumerates
                    yield! sys.Entities  |> enumerates
                | :? Work as work ->
                    yield! work.Calls    |> enumerates
                    yield! work.Arrows   |> enumerates
                | :? Flow as flow ->
                    // Flow는 이제 UI 요소를 직접 소유하지 않음
                    ()

                | (:? Call) | (:? ApiCall) | (:? ApiDef) | (:? ArrowBetweenWorks) | (:? ArrowBetweenCalls) ->
                    ()
                | _ ->
                    tracefn $"Skipping {(x.GetType())} in enumerateHelper : {x.Guid}"
                    ()
            }
    type RtUnique with // EnumerateRtObjects, UpdateDateTime
        (* see also EdUnique.EnumerateRtObjects *)
        member x.EnumerateRtObjects(?includeMe): RtUnique list =
            let hash = HashSet<Guid>()
            enumerateHelper hash includeMe x
            |> List.ofSeq

        member x.EnumerateRtObjectsT<'T(* when 'T:> RtUnique*)>(?includeMe): 'T list =
            x.EnumerateRtObjects(?includeMe=includeMe) |> List.choose tryCast<'T>

        /// DS object 의 모든 상위 DS object 의 DateTime 을 갱신
        /// project, system 만 date time 을 가짐
        member x.UpdateDateTime(?dateTime:DateTime) =
            let dateTime = dateTime |?? (fun () -> now().TruncateToSecond())
            match x with
            | :? Project as prj -> prj.Properties.DateTime <- dateTime
            | :? DsSystem as sys ->
                sys.Properties.DateTime <- dateTime
                sys.Project |> iter (fun p -> p.Properties.DateTime <- dateTime)
            | _ -> ()



    type Project with // AddActiveSystem, AddPassiveSystem
        member x.AddActiveSystem(system:DsSystem) =
            system |> setParent x |> ignore
            x.RawActiveSystems.AddAsSet(system, Unique.isDuplicated)

        member x.AddPassiveSystem(system:DsSystem) =
            system |> setParent x |> ignore
            x.RawPassiveSystems.AddAsSet(system, Unique.isDuplicated)

    type DsSystem with // AddApiCalls, AddApiDefs, AddArrows, AddFlows, AddWorks, RemoveApiCalls, RemoveApiDefs, RemoveArrows, RemoveFlows, RemoveWorks
        member internal x.addWorks(works:Work seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            works |> iter (setParentI x)
            x.RawWorks.VerifyAddRangeAsSet(works, Unique.isDuplicated)

        member internal x.removeWorks(works:Work seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            let system = works |-> _.System.Value |> distinct |> exactlyOne
            let arrows =
                system.Arrows
                    .Where(fun a -> works.Contains a.Source || works.Contains a.Target)
                    .ToArray()
            system.removeArrows(arrows, updateDateTime)
            works |> iter (fun w -> w.RawParent <- None)
            works |> iter (x.RawWorks.Remove >> ignore)


        member internal x.addFlows(flows:Flow seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            flows |> iter (setParentI x)
            x.RawFlows.VerifyAddRangeAsSet(flows, Unique.isDuplicated)

        member internal x.removeFlows(flows:Flow seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            let flowsDic = flows |-> _.Guid |> HashSet
            // 삭제 대상인 flows 를 쳐다보고 있는 works 들을 찾아서, 그들의 Flow 를 None 으로 설정
            x.Works
            |> filter(fun w -> w.FlowGuid |-> flowsDic.Contains |? false) // work 중에서 w.FlowGuid 가 삭제 대상인 flows 에 포함된 것들만 선택
            |> iter (fun w -> w.FlowGuid <- None)

            flows |> iter clearParentI
            flows |> iter (x.RawFlows.Remove >> ignore)


        member internal x.addArrows(arrows:ArrowBetweenWorks seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            arrows |> iter (setParentI x)
            x.RawArrows.VerifyAddRangeAsSet(arrows)

        member internal x.removeArrows(arrows:ArrowBetweenWorks seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            arrows |> iter clearParentI
            arrows |> iter (x.RawArrows.Remove >> ignore)


        member internal x.addApiDefs(apiDefs:ApiDef seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            apiDefs |> iter (setParentI x)
            x.RawApiDefs.VerifyAddRangeAsSet(apiDefs, Unique.isDuplicated)

        member internal x.removeApiDefs(apiDefs:ApiDef seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()

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


        member internal x.addApiCalls(apiCalls:ApiCall seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            apiCalls |> iter (setParentI x)
            x.RawApiCalls.VerifyAddRangeAsSet(apiCalls, Unique.isDuplicated)

        member internal x.removeApiCalls(apiCalls:ApiCall seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
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


        //member x.AddButtons      (buttons:NewDsButton seq)       = buttons.Cast<SystemEntityWithJsonPolymorphic>() |> x.AddEntities
        //member x.RemoveButtons   (buttons:NewDsButton seq)       = buttons.Cast<SystemEntityWithJsonPolymorphic>() |> iter x.RemoveEntitiy
        //member x.AddLamps        (lamps:NewLamp seq)             = x.addLamps        (lamps, true)
        //member x.RemoveLamps     (lamps:NewLamp seq)             = x.removeLamps     (lamps, true)
        //member x.AddConditions   (conditions:NewDsCondition seq) = x.addConditions   (conditions, true)
        //member x.RemoveConditions(conditions:NewDsCondition seq) = x.removeConditions(conditions, true)
        //member x.AddActions      (actions:NewDsAction seq)       = x.addActions      (actions, true)
        //member x.RemoveActions   (actions:NewDsAction seq)       = x.removeActions   (actions, true)



    type Flow with // AddWorks, RemoveWorks (더 이상 Button 등을 관리하지 않음)
        // works 들이 flow 자신의 직접 child 가 아니므로 따로 관리 함수 필요
        member internal x.addWorks(ws:Work seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            ws |> iter (fun w -> w.FlowGuid <- Some x.Guid)

        member internal x.removeWorks(ws:Work seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            ws |> iter (fun w -> w.FlowGuid <- None)

        member x.AddWorks        (ws:Work seq)                = x.addWorks        (ws, true)
        member x.RemoveWorks     (ws:Work seq)                = x.removeWorks     (ws, true)







    type Work with // AddArrows, AddCalls, RemoveArrows, RemoveCalls
        member internal x.addCalls(calls:Call seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            calls |> iter (setParentI x)
            x.RawCalls.VerifyAddRangeAsSet(calls)
        member internal x.removeCalls(calls:Call seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            let work = calls |-> _.RawParent.Value |> distinct |> exactlyOne :?> Work
            let arrows = work.Arrows.Where(fun a -> calls.Contains a.Source || calls.Contains a.Target)
            work.removeArrows(arrows, updateDateTime)
            calls |> iter clearParentI
            calls |> iter (x.RawCalls.Remove >> ignore)

        member internal x.addArrows(arrows:ArrowBetweenCalls seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            arrows |> iter (setParentI x)
            x.RawArrows.VerifyAddRangeAsSet(arrows)


        member internal x.removeArrows(arrows:ArrowBetweenCalls seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            arrows |> iter clearParentI
            arrows |> iter (x.RawArrows.Remove >> ignore)

        member x.AddCalls    (calls:Call seq)               = x.addCalls    (calls, true)
        member x.RemoveCalls (calls:Call seq)               = x.removeCalls (calls, true)
        member x.AddArrows   (arrows:ArrowBetweenCalls seq) = x.addArrows   (arrows, true)
        member x.RemoveArrows(arrows:ArrowBetweenCalls seq) = x.removeArrows(arrows, true)

    type Call with // AddApiCalls
        member internal x.addApiCalls(apiCalls:ApiCall seq, updateDateTime:bool) =
            if updateDateTime then x.UpdateDateTime()
            //apiCalls |> iter (setParentI x)
            apiCalls |> iter (fun z -> x.ApiCallGuids.Add z.Guid)

        member x.AddApiCalls (apiCalls:ApiCall seq)         = x.addApiCalls (apiCalls, true)













// C# 친화적인 정적 팩토리 클래스 - C#에서 직접 접근 가능
type DsObjectFactory = // CreateApiCall, CreateApiDef, CreateCall, CreateCallExtended, CreateDsSystem, CreateDsSystemExtended, CreateFlow, CreateProject, CreateWork, CreateWorkExtended, InitializeAppSettings

    /// C#에서 AppSettings 초기화를 위한 헬퍼 메서드
    static member InitializeAppSettings() =
        // AppSettings 인스턴스가 존재하지 않으면 새로 생성
        AppSettings() |> ignore
        "AppSettings initialized successfully"


    /// 새로운 패턴: createExtended 사용
    static member CreateProject()  = createExtended<Project>()
    static member CreateDsSystemExtended() = createExtended<DsSystem>()
    static member CreateDsSystem() = createExtended<DsSystem>()
    static member CreateWork()     = createExtended<Work>()
    static member CreateCall()     = createExtended<Call>()
    static member CreateFlow()     = createExtended<Flow>()
    static member CreateApiDef()   = createExtended<ApiDef>()
    static member CreateApiCall()  = createExtended<ApiCall>()

    //static member CreateDsSystem(flows:Flow[], works:Work[],
    //    arrows:ArrowBetweenWorks[], apiDefs:ApiDef[], apiCalls:ApiCall[],
    //    blcas: SystemEntityWithJsonPolymorphic[]    // Button, Lamp, Condition, Action
    //) =
    //    let system = createExtended<DsSystem>()
    //    system.RawFlows.Clear()
    //    system.RawWorks.Clear()
    //    system.RawArrows.Clear()
    //    system.RawApiDefs.Clear()
    //    system.RawApiCalls.Clear()
    //    system.RawFlows.AddRange(flows)
    //    system.RawWorks.AddRange(works)
    //    system.RawArrows.AddRange(arrows)
    //    system.RawApiDefs.AddRange(apiDefs)
    //    system.RawApiCalls.AddRange(apiCalls)
    //    system.PolymorphicJsonEntities.Clear()
    //    system.PolymorphicJsonEntities.AddItems(blcas)
    //    flows    |> iter (setParentI system)
    //    works    |> iter (setParentI system)
    //    arrows   |> iter (setParentI system)
    //    apiDefs  |> iter (setParentI system)
    //    apiCalls |> iter (setParentI system)
    //    system


    /// 새로운 패턴: createExtended 사용
    static member CreateWorkExtended() = createExtended<Work>()

    static member CreateWork(calls:Call seq, arrows:ArrowBetweenCalls seq, flowGuid:Guid option) =
        let calls = calls |> toList
        let arrows = arrows |> toList

        let work = createExtended<Work>()
        work.RawCalls.Clear()
        work.RawArrows.Clear()
        work.RawCalls.AddRange(calls)
        work.RawArrows.AddRange(arrows)
        work.FlowGuid <- flowGuid
        calls  |> iter (setParentI work)
        arrows |> iter (setParentI work)
        work


    /// 새로운 패턴: createExtended 사용
    static member CreateCallExtended() = createExtended<Call>()

    static member CreateCall(callType:DbCallType, apiCalls:ApiCall seq,
        autoConditions: ApiCallValueSpecs, commonConditions: ApiCallValueSpecs, isDisabled:bool, timeout:int option
    ) =
        let apiCallGuids = apiCalls |-> _.Guid

        let call = createExtended<Call>()
        call.CallType <- callType
        call.IsDisabled <- isDisabled
        call.Timeout <- timeout
        call.ApiCallGuids.Clear()
        call.AutoConditions <- autoConditions
        call.CommonConditions <- commonConditions
        call.ApiCallGuids.AddRange(apiCallGuids)
        apiCalls |> iter (setParentI call)
        call




// F# 타입 확장 및 helper functions (F#과 C#에서 사용)
[<AutoOpen>]
module DsObjectUtilsModule =

    // F# 코드 호환성을 위한 타입 확장 (static member)
    type Project with // Create
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

        static member Create() = createExtended<Project>()


    type DsSystem with // Create
        static member Create(flows:Flow[], works:Work[],
            arrows:ArrowBetweenWorks[], apiDefs:ApiDef[], apiCalls:ApiCall[],
            polys: PolymorphicJsonCollection<JsonPolymorphic>, properties: DsSystemProperties
        ) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let system = createExtended<DsSystem>()
            system.RawFlows.Clear()
            system.RawWorks.Clear()
            system.RawArrows.Clear()
            system.RawApiDefs.Clear()
            system.RawApiCalls.Clear()
            system.PolymorphicJsonEntities.Clear()
            system.RawFlows.AddRange(flows)
            system.RawWorks.AddRange(works)
            system.RawArrows.AddRange(arrows)
            system.RawApiDefs.AddRange(apiDefs)
            system.RawApiCalls.AddRange(apiCalls)
            system.PolymorphicJsonEntities <- polys
            system.Properties <- properties.DeepClone<DsSystemProperties>()
            // parent 관계 설정 추가
            flows    |> iter (setParentI system)
            works    |> iter (setParentI system)
            arrows   |> iter (setParentI system)
            apiDefs  |> iter (setParentI system)
            apiCalls |> iter (setParentI system)
            system

        // 매개변수가 없는 경우만 확장 타입 사용
        static member Create() = createExtended<DsSystem>()


    type Work with // Create
        static member Create(calls:Call seq, arrows:ArrowBetweenCalls seq, flowGuid:Guid option) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let work = createExtended<Work>()
            work.RawCalls.Clear()
            work.RawArrows.Clear()
            work.RawCalls.AddRange(calls)
            work.RawArrows.AddRange(arrows)
            work.FlowGuid <- flowGuid
            calls  |> iter (setParentI work)
            arrows |> iter (setParentI work)
            work

        static member Create() = createExtended<Work>()


    type Call with // Create
        static member Create(callType:DbCallType, apiCalls:ApiCall seq,
            autoConditions: ApiCallValueSpecs, commonConditions: ApiCallValueSpecs, isDisabled:bool, timeout:int option
        ) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let call = createExtended<Call>()
            call.CallType <- callType
            call.IsDisabled <- isDisabled
            call.Timeout <- timeout
            call.ApiCallGuids.Clear()
            call.AutoConditions <- autoConditions
            call.CommonConditions <- commonConditions
            call.ApiCallGuids.AddRange(apiCalls |-> _.Guid)
            call

        static member Create() = createExtended<Call>()


    // Flow는 더 이상 Create 메서드로 Button 등을 받지 않음

    type Flow with
        static member Create() = createExtended<Flow>()


    type ApiDef with // Create
        static member Create(isPush:bool) =
            // 매개변수가 있는 경우 확장 타입에서 initialize
            let apiDef = createExtended<ApiDef>()
            apiDef.IsPush <- isPush
            apiDef

        static member Create() =
            createExtended<ApiDef>()

    type ApiCall with // Create
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

        static member Create() = createExtended<ApiCall>()

    type IArrow with // GetArrowType, GetSource, GetTarget
        member x.GetSource(): Unique =
            match x with
            | :? ArrowBetweenCalls as a -> a.Source
            | :? ArrowBetweenWorks as a -> a.Source
            | _ -> fail()

        member x.GetTarget(): Unique =
            match x with
            | :? ArrowBetweenCalls as a -> a.Target
            | :? ArrowBetweenWorks as a -> a.Target
            | _ -> fail()

        member x.GetArrowType(): DbArrowType =
            match x with
            | :? ArrowBetweenCalls as a -> a.Type
            | :? ArrowBetweenWorks as a -> a.Type
            | _ -> fail()

    type Unique with // EnumerateAncestors, GetFQDN
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


    type RtUnique with // Validate
        member x.Validate(bag:DuplicateBag) =
            verify (x.Guid <> emptyGuid)

            x |> tryCast<IWithDateTime> |> iter(fun z -> verify (z.DateTime <> minDate))
            match x with
            | :? Project | :? DsSystem | :? Flow  | :? Work  | :? Call -> verify (x.Name.NonNullAny())
            | _ -> ()

            match x with
            | :? Project as prj ->
                prj.Systems |> iter _.Validate(bag)
                for s in prj.Systems do
                    verify (prj.Guid |> isParentGuid s)

            | :? DsSystem as sys ->
                sys.Works |> iter _.Validate(bag)


                for w in sys.Works  do
                    verify (sys.Guid |> isParentGuid w)
                    for c in w.Calls do
                        // ApiCalls가 비어있을 수 있음 (NjSystem 등의 경우)
                        if not (c.ApiCalls.IsEmpty) then
                            c.ApiCalls |-> _.Guid |> forall(bag.OldGuid2NewObjectMap.ContainsKey) |> verify
                            c.ApiCalls |> forall (fun z -> sys.ApiCalls |> contains z) |> verify
                            for ac in c.ApiCalls do
                                try
                                    ac.ApiDef.Guid = ac.ApiDefGuid |> verify
                                    match sys.Project with
                                    | Some proj -> proj.EnumerateRtObjectsT<ApiDef>() |> contains ac.ApiDef |> verify
                                    | None -> sys.ApiDefs |> contains ac.ApiDef |> verify

                                with ex ->
                                    logWarn $"Exception while validating ApiCall: {ex.Message}"
                                    ()  // NjSystem 등에서 ApiDef 접근 실패 시 무시

                sys.Arrows |> iter _.Validate(bag)
                for a in sys.Arrows do
                    verify (sys.Guid |> isParentGuid a)
                    sys.Works |> contains a.Source |> verify
                    sys.Works |> contains a.Target |> verify

                sys.ApiDefs |> iter _.Validate(bag)
                for w in sys.ApiDefs do
                    verify (sys.Guid |> isParentGuid w)

                sys.ApiCalls |> iter _.Validate(bag)
                for ac in sys.ApiCalls  do
                    verify (sys.Guid |> isParentGuid ac)

            | :? Flow as flow ->
                let works = flow.Works
                works |> iter _.Validate(bag)
                for w in works  do
                    verify (w.Flow = Some flow)


            | :? Work as work ->
                work.Calls |> iter _.Validate(bag)
                for c in work.Calls do
                    verify (work.Guid |> isParentGuid c)

                work.Arrows |> iter _.Validate(bag)
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
