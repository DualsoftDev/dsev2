namespace Ev2.Core.FS

open Dual.Common.Core.FS
open Dual.Common.Base
open System
open Newtonsoft.Json.Linq

/// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
[<AutoOpen>]
module internal rec DsObjectCopyImpl =
    let uniqReplicateWithBag (bag:DuplicateBag) (src:#Unique) (dst:#Unique) : #Unique =
        dst
        |> replicateProperties src
        |> tee(fun z -> bag.OldGuid2NewObjectMap.TryAdd(src.Guid, z))

    type Project with // replicate
        /// Project 복제.
        member x.replicate(): Project =
            Project.Create([], [])
            |> tee(fun newProject ->
                let passives   = x.PassiveSystems |-> _.replicate() |> toArray
                let actives    = x.ActiveSystems  |-> _.replicate() |> toArray

                newProject
                |> replicateProperties x
                |> tee(fun z ->
                    (actives @ passives) |> iter (fun (s:DsSystem) -> setParentI z s)
                    actives    |> z.RawActiveSystems   .AddRange
                    passives   |> z.RawPassiveSystems  .AddRange) )

    type DsSystem with // replicate
        /// DsSystem 복제. 지정된 newSystem 객체에 현재 시스템의 내용을 복사
        member x.replicate() =
            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            let emptyPolys = PolymorphicJsonCollection<JsonPolymorphic>()
            DsSystem.Create([||], [||], [||], [||], [||], emptyPolys, DsSystemProperties.Create())
            |> tee(fun newSystem ->
                // flow, work 상호 참조때문에 일단 flow 만 shallow copy
                let apiDefs    = x.ApiDefs    |-> _.replicate() |> toArray
                let apiCalls   = x.ApiCalls   |-> _.replicate() |> toArray
                let flows      = x.Flows      |-> _.replicate() |> toArray
                let works      = x.Works      |-> _.replicate() |> toArray // work 에서 shallow  copy 된 flow 참조 가능해짐.
                let arrows     = x.Arrows     |-> _.replicate() |> toArray


                // 복제된 데이터를 newSystem에 설정
                flows      |> newSystem.RawFlows     .AddRange
                works      |> newSystem.RawWorks     .AddRange
                arrows     |> newSystem.RawArrows    .AddRange
                apiDefs    |> newSystem.RawApiDefs   .AddRange
                apiCalls   |> newSystem.RawApiCalls  .AddRange

                // 먼저 bag에 등록하고 속성 복사 (GUID 포함)
                newSystem |> replicateProperties x |> ignore
                // UI 요소들도 복제
                newSystem.PolymorphicJsonEntities <- x.PolymorphicJsonEntities.DeepClone()
                newSystem.PolymorphicJsonEntities.SyncToValues()
                newSystem.Properties <- x.PropertiesJson |> JsonPolymorphic.FromJson<DsSystemProperties>

                // 그 다음 parent 설정 - GUID가 확정된 후에 설정해야 함
                flows      |> iter (setParentI newSystem)
                works      |> iter (setParentI newSystem)
                arrows     |> iter (setParentI newSystem)
                apiDefs    |> iter (setParentI newSystem)
                apiCalls   |> iter (setParentI newSystem)
                newSystem.Entities |> iter (setParentI newSystem)

                // 검증
                arrows
                |> iter (fun (a:ArrowBetweenWorks) ->
                    works |> contains a.Source |> verify
                    works |> contains a.Target |> verify))



    type Work with // replicate
        /// Work 복제. 지정된 newWork 객체에 현재 작업의 내용을 복사
        member x.replicate() =
            Work.Create([], [], None)
            |> tee(fun newWork ->
                let calls = x.Calls |-> _.replicate() |> List.ofSeq

                let arrows:ArrowBetweenCalls list =
                    x.Arrows |-> _.replicate() |> List.ofSeq

                // 복제된 데이터를 newWork에 설정
                calls  |> newWork.RawCalls.AddRange
                arrows |> newWork.RawArrows.AddRange
                newWork.FlowGuid <- x.FlowGuid

                // 먼저 bag에 등록하고 속성 복사 (GUID 포함)
                newWork |> replicateProperties x |> ignore
                newWork.PropertiesJson <- x.PropertiesJson

                // 그 다음 parent 설정 - GUID가 확정된 후에 설정해야 함
                calls  |> iter (setParentI newWork)
                arrows |> iter (setParentI newWork)

                // 검증
                arrows
                |> iter (fun (a:ArrowBetweenCalls) ->
                    calls |> contains a.Source |> verify
                    calls |> contains a.Target |> verify) )


    /// flow 는 이제 UI 요소를 직접 소유하지 않음 (System이 소유)
    type Flow with // replicate
        /// Flow 복제. 지정된 newFlow 객체에 현재 플로우의 내용을 복사
        member x.replicate() =
            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            Flow.Create()
            |> tee(fun newFlow ->
                // Flow는 이제 UI 요소를 직접 소유하지 않으므로 속성 복사만 수행
                // UI 요소들은 System 레벨에서 복제됨

                // 속성 복사 (GUID 포함)
                newFlow |> replicateProperties x |> ignore
                newFlow.PropertiesJson <- x.PropertiesJson )


    //type DsButton with // replicate
    //    member x.replicate() = DsButton.Create() |> replicateProperties x


    type Call with // replicate
        /// Call 복제. 지정된 newCall 객체에 현재 호출의 내용을 복사
        member x.replicate() =
            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            Call.Create(x.CallType, x.ApiCallGuids |> toList, x.AutoConditions, x.CommonConditions, x.IsDisabled, x.Timeout)
            |> tee(fun newCall ->
                // ApiCall들은 시스템 레벨에서 복제되므로 그대로 유지
                let apiCallGuids = x.ApiCallGuids |> toList

                // 복제된 데이터를 newCall에 설정
                newCall.CallType   <- x.CallType
                newCall.IsDisabled <- x.IsDisabled
                newCall.Timeout    <- x.Timeout

                newCall.AutoConditions.Clear()
                newCall.CommonConditions.Clear()
                newCall.ApiCallGuids.Clear()
                newCall.AutoConditions.AddRange(x.AutoConditions)
                newCall.CommonConditions.AddRange(x.CommonConditions)
                newCall.ApiCallGuids.AddRange(apiCallGuids)

                newCall
                |> replicateProperties x
                |> tee(fun c ->
                    c.Status4 <- x.Status4
                    c.PropertiesJson <- x.PropertiesJson))


    type ApiCall with // replicate
        member x.replicate() =
            ApiCall.Create(x.ApiDefGuid, x.InAddress, x.OutAddress, x.InSymbol, x.OutSymbol, x.ValueSpec)
            |> replicateProperties x
            |> tee(fun apiCall -> apiCall.PropertiesJson <- x.PropertiesJson)

    type ApiDef with // replicate
        member x.replicate() =
            ApiDef.Create(IsPush=x.IsPush, TxGuid=x.TxGuid, RxGuid=x.RxGuid)
            |> replicateProperties x
            |> tee(fun apiDef -> apiDef.PropertiesJson <- x.PropertiesJson)

    type ArrowBetweenWorks with // replicate
        member x.replicate() =
            ArrowBetweenWorks.Create(x.XSourceGuid, x.XTargetGuid, x.Type)
            |> replicateProperties x

    type ArrowBetweenCalls with // replicate
        member x.replicate() =
            ArrowBetweenCalls.Create(x.XSourceGuid, x.XTargetGuid, x.Type)
            |> replicateProperties x

[<AutoOpen>]
module DsObjectCopyAPIModule =
    /// 객체 복제시 새로운 guid 할당을 위한 map 구성
    let buildUniqGenerateMap(bag:DuplicateBag) (rtObj:RtUnique) =
        rtObj.EnumerateRtObjects()
        |> iter (fun rt -> bag.Add(rt.Guid, Guid.NewGuid()))

    /// 객체 복제시, id, guid 등을 새로 부여하고, 이름 중복 등을 해결
    let rec uniqGenerateNew (bag:DuplicateBag) (rtObj:RtUnique) =
        let map = bag.OldGuid2NewGuidMap
        let proc (rt:RtUnique) = uniqGenerateNew bag rt |> ignore
        rtObj.Guid <- map[rtObj.Guid]
        rtObj.Id <- None
        rtObj.UpdateDateTime()
        bag.Disambiguate.Invoke(rtObj)
        match box rtObj with
        | :? Project as rt ->
            rt.Systems |> iter proc

        | :? DsSystem as rt ->
            rt.ApiDefs    |> iter proc
            rt.ApiCalls   |> iter proc
            rt.Flows      |> iter proc
            rt.Works      |> iter proc
            rt.Arrows     |> iter proc

            // TODO: PolymorphicJsonCollection 에 대한 RtUnique 처리
            //rt.Buttons    |> iter proc
            //rt.Lamps      |> iter proc
            //rt.Conditions |> iter proc
            //rt.Actions    |> iter proc

        | :? Work as rt ->
            rt.Calls    |> iter proc
            rt.Arrows   |> iter proc
            rt.FlowGuid |> iter (fun guid -> rt.FlowGuid <- Some map[guid])

        | :? ArrowBetweenWorks as rt ->
            rt.XSourceGuid <- map[rt.XSourceGuid]
            rt.XTargetGuid <- map[rt.XTargetGuid]

        | :? Call as rt ->
            rt.ApiCallGuids
            |> List.ofSeq
            |> List.map (fun g -> map[g])
            |> fun gs ->
                rt.ApiCallGuids.Clear()
                rt.ApiCallGuids.AddRange gs

        | :? ArrowBetweenCalls as rt ->
            rt.XSourceGuid <- map[rt.XSourceGuid]
            rt.XTargetGuid <- map[rt.XTargetGuid]

        | :? ApiDef as rt ->
            rt.TxGuid <- map[rt.TxGuid]
            rt.RxGuid <- map[rt.RxGuid]

        | :? Flow as rt ->
            // Flow는 더 이상 UI 요소를 직접 소유하지 않음
            ()

        | :? ApiCall as rt ->
            rt.ApiDefGuid <- map[rt.ApiDefGuid]
            ()


        | _ -> failwith "Not Project"


    type DsSystem with // Duplicate, Replicate
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate() =
            x.EnumerateRtObjects()
            |> iter (fun z ->
                z.RtObject <- None
                z.NjObject <- None
                z.ORMObject <- None
                z.DDic.Clear())

            x.replicate()

        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate(?bag:DuplicateBag) =
            let bag = bag |?? (fun () -> DuplicateBag())
            x.Replicate()
            |> tee( fun z ->
                buildUniqGenerateMap bag z
                uniqGenerateNew bag z)

    type Project with // Duplicate, Replicate
        /// RtProject 객체 완전히 동일하게 복사 생성.  (Id, Guid 및 DateTime 포함 모두 동일하게 복사)
        member x.Replicate() =  // RtProject
            x.EnumerateRtObjects()
            |> iter (fun z ->
                z.RtObject <- None
                z.NjObject <- None
                z.ORMObject <- None
                z.DDic.Clear())

            x.replicate()

        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate(?bag:DuplicateBag) =  // RtProject
            let bag = bag |?? (fun () -> DuplicateBag())
            x.Replicate()
            |> tee( fun z ->
                buildUniqGenerateMap bag z
                uniqGenerateNew bag z)
