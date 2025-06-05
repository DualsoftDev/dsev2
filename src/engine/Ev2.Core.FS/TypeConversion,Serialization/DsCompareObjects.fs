namespace Ev2.Core.FS

open System
open Dual.Common.Core.FS
open Dual.Common.Base
open System.ComponentModel

[<AutoOpen>]
module rec DsCompareObjects =
    type IUnique with   // GetGuid, GetName, TryGetId, GetDateTime, TryGetRawParent
        member x.GetGuid():Guid =
            match x with
            | :? Unique as u    -> u.Guid
            | :? NjUnique as u  -> u.Guid
            | :? ORMUnique as u -> u.Guid
            | _ -> failwith "ERROR"

        member x.GetName() =
            match x with
            | :? Unique as u    -> u.Name
            | :? NjUnique as u  -> u.Name
            | :? ORMUnique as u -> u.Name
            | _ -> failwith "ERROR"

        member x.TryGetId():Id option =
            match x with
            | :? Unique as u    -> u.Id
            | :? NjUnique as u  -> n2o u.Id
            | :? ORMUnique as u -> n2o u.Id
            | _ -> failwith "ERROR"

        member x.GetDateTime() =
            match x with
            | :? Unique as u    -> u.DateTime
            | :? NjUnique as u  -> u.DateTime
            | :? ORMUnique as u -> u.DateTime
            | _ -> failwith "ERROR"

        member x.TryGetRawParent():IUnique option =
            match x with
            | :? Unique as u    -> u.RawParent >>= tryCast<IUnique>
            | :? NjUnique as u  -> u.RawParent >>= tryCast<IUnique>
            | :? ORMUnique as u -> u.RawParent >>= tryCast<IUnique>
            | _ -> failwith "ERROR"

    type UniqueCompareCriteria(?id:bool, ?guid:bool, ?dateTime:bool, ?parentGuid) =
        let id       = id       |? true
        let guid     = guid     |? true
        let dateTime = dateTime |? true
        let parentGuid = parentGuid |? true
        member val Id       = id       with get, set
        member val Guid     = guid     with get, set
        member val DateTime = dateTime with get, set
        member val ParentGuid = parentGuid with get, set

        (* Project/System 속성 *)
        member val Author = true with get, set
        member val Version = true with get, set
        member val Description = true with get, set
        member val LastConnectionString = true with get, set
        member val EngineVersion = true with get, set
        member val LangVersion   = true with get, set

    /// 객체 비교 결과 반환용....
    type UniqueCompareResult =
        | Equal
        | LeftOnly of IRtUnique
        | RightOnly of IRtUnique
        /// (diff property name) * left * right
        | Diff of Name * IRtUnique * IRtUnique

    /// abberviation
    type internal Ucc = UniqueCompareCriteria
    type internal Ucr = UniqueCompareResult

    type IRtUnique with // ComputeDiffUnique
        member internal x.ComputeDiffUnique(y:IRtUnique, criteria:Ucc): Ucr seq =
            let c = criteria
            seq {
                if x.GetName() <> y.GetName() then yield Diff("Name", x, y)
                if c.Id       && x.TryGetId()    <> y.TryGetId()    then yield Diff("Id", x, y)
                if c.Guid     && x.GetGuid()     <> y.GetGuid()     then yield Diff("Guid", x, y)
                if c.DateTime && x.GetDateTime() <> y.GetDateTime() then yield Diff("DateTime", x, y)

                let xp = x.TryGetRawParent() |-> _.GetGuid()
                let yp = y.TryGetRawParent() |-> _.GetGuid()
                if c.ParentGuid && ( xp <> yp ) then yield Diff("Parent", x, y)
            }

    let private sortByGuid (xs:#IRtUnique list): #IRtUnique list = xs |> List.sortBy (fun x -> x.GetGuid())

    /// xs 와 ys 의 collection 간 비교
    let private computeDiffList<'T when 'T :> IRtUnique>
        (xs: 'T seq)
        (ys: 'T seq)
        (criteria: Ucc option): Ucr seq =

        let xs = xs.ToDictionary(_.GetGuid(), id)
        let ys = ys.ToDictionary(_.GetGuid(), id)

        let allGuids = xs.Keys @ ys.Keys |> Set.ofSeq

        seq {
            for guid in allGuids do
                match xs.TryGet(guid), ys.TryGet(guid) with
                | Some x, Some y ->
                    let diffs = x.ComputeDiff(y, ?criteria=criteria)
                    yield! diffs
                | Some x, None ->
                    yield LeftOnly x
                | None, Some y ->
                    yield RightOnly y
                | _ -> ()
        }


    type RtProject with // ComputeDiff
        member x.ComputeDiff(y:RtProject, ?criteria:Ucc): Ucr seq =
            let c = criteria |? Ucc()
            seq {
                yield! x.ComputeDiffUnique(y, criteria=c)

                (* System 들 비교*)
                yield! (x.PrototypeSystems, y.PrototypeSystems,  Some c) |||> computeDiffList
                yield! (x.ActiveSystems,    y.ActiveSystems,     Some c) |||> computeDiffList
                yield! (x.PassiveSystems,   y.PassiveSystems,    Some c) |||> computeDiffList

                (* 기타 속성 비교 *)
                if c.Author && x.Author <> y.Author then yield Diff("Author", x, y)
            }

    type RtSystem with // ComputeDiff
        member x.ComputeDiff(y:RtSystem, ?criteria:Ucc): Ucr seq =
            seq {
                yield! (x.Flows   , y.Flows   , criteria) |||> computeDiffList
                yield! (x.Works   , y.Works   , criteria) |||> computeDiffList
                yield! (x.Arrows  , y.Arrows  , criteria) |||> computeDiffList
                yield! (x.ApiDefs , y.ApiDefs , criteria) |||> computeDiffList
                yield! (x.ApiCalls, y.ApiCalls, criteria) |||> computeDiffList

                if x.PrototypeSystemGuid <> y.PrototypeSystemGuid then yield Diff("PrototypeSystemGuid", x, y)
                if x.Author        <> y.Author        then yield Diff("Author", x, y)
                if x.EngineVersion <> y.EngineVersion then yield Diff("EngineVersion", x, y)
                if x.LangVersion   <> y.LangVersion   then yield Diff("LangVersion", x, y)
                if x.Description   <> y.Description   then yield Diff("Description", x, y)
            }


    type RtFlow with // ComputeDiff
        member x.ComputeDiff(y:RtFlow, ?criteria:Ucc): Ucr seq =
            seq {
                if (x.System |-> _.Guid) <> (y.System |-> _.Guid)   then yield Diff("OwnerSystem", x, y)

                // System 의 works 에서 비교할 것이기 때문에 여기서 비교하면 중복 비교가 됨.
                //yield! (x.Works, y.Works, criteria) |||> computeDiffList
            }

    type RtWork with // ComputeDiff
        member x.ComputeDiff(y:RtWork, ?criteria:Ucc): Ucr seq =
            seq {
                if (x.System |-> _.Guid) <> (y.System |-> _.Guid) then yield Diff("OwnerSystem", x, y)

                let xp = x.Flow |-> _.Guid
                let yp = y.Flow |-> _.Guid
                if xp <> yp then yield Diff("OwnerFlow", x, y)

                yield! (x.Calls, y.Calls, criteria) |||> computeDiffList
                yield! (x.Arrows, y.Arrows, criteria) |||> computeDiffList
            }

    type RtCall with // ComputeDiff
        member x.ComputeDiff(y:RtCall, ?criteria:Ucc): Ucr seq =
            seq {
                if (x.Work |-> _.Guid)  <> (y.Work |-> _.Guid)  then yield Diff("Work", x, y)
                if x.CallType   <> y.CallType   then yield Diff("CallType", x, y)
                if x.AutoPre    <> y.AutoPre    then yield Diff("AutoPre", x, y)
                if x.Safety     <> y.Safety     then yield Diff("Safety", x, y)
                if x.IsDisabled <> y.IsDisabled then yield Diff("IsDisabled", x, y)
                if x.Timeout    <> y.Timeout    then yield Diff("Timeout", x, y)

                let d1 = (x.ApiCallGuids, y.ApiCallGuids) ||> setEqual |> not
                if d1 then yield Diff("ApiCalls", x, y)
            }

    type RtApiDef with // ComputeDiff
        member x.ComputeDiff(y:RtApiDef, ?criteria:Ucc): Ucr seq =
            seq {
                if x.IsPush <> y.IsPush   then yield Diff("IsPush", x, y)
            }

    type RtApiCall with // ComputeDiff
        member x.ComputeDiff(y:RtApiCall, ?criteria:Ucc): Ucr seq =
            seq {
                if x.ApiDefGuid <> y.ApiDefGuid then yield Diff("ApiDefGuid", x, y)
                if x.InAddress  <> y.InAddress  then yield Diff("InAddress", x, y)
                if x.OutAddress <> y.OutAddress then yield Diff("OutAddress", x, y)
                if x.InSymbol   <> y.InSymbol   then yield Diff("InSymbol", x, y)
                if x.OutSymbol  <> y.OutSymbol  then yield Diff("OutSymbol", x, y)
                if x.ValueType  <> y.ValueType  then yield Diff("ValueType", x, y)
                if x.RangeType  <> y.RangeType  then yield Diff("RangeType", x, y)
                if x.Value1     <> y.Value1     then yield Diff("Value1", x, y)
                if x.Value2     <> y.Value2     then yield Diff("Value2", x, y)
            }

    type RtArrowBetweenWorks with // ComputeDiff
        member x.ComputeDiff(y:RtArrowBetweenWorks, ?criteria:Ucc): Ucr seq =
            seq {
                if x.Source.Guid <> y.Source.Guid then yield Diff("Source", x, y)
                if x.Target.Guid <> y.Target.Guid then yield Diff("Target", x, y)
                if x.Type <> y.Type then yield Diff("Type", x, y)
            }

    type RtArrowBetweenCalls with // ComputeDiff
        member x.ComputeDiff(y:RtArrowBetweenCalls, ?criteria:Ucc): Ucr seq =
            seq {
                if x.Source.Guid <> y.Source.Guid then yield Diff("Source", x, y)
                if x.Target.Guid <> y.Target.Guid then yield Diff("Target", x, y)
                if x.Type <> y.Type then yield Diff("Type", x, y)
            }

    type IRtUnique with // ComputeDiff, IsEqual
        member internal x.ComputeDiff(y:IRtUnique, ?criteria:Ucc): Ucr seq =
            seq {
                let c = criteria |? Ucc()
                yield! x.ComputeDiffUnique(y, criteria=c)

                match x, y with
                | (:? RtProject as u), (:? RtProject as v)  -> yield! u.ComputeDiff(v, criteria=c)
                | (:? RtSystem  as u), (:? RtSystem  as v)  -> yield! u.ComputeDiff(v, criteria=c)
                | (:? RtFlow    as u), (:? RtFlow    as v)  -> yield! u.ComputeDiff(v, criteria=c)
                | (:? RtWork    as u), (:? RtWork    as v)  -> yield! u.ComputeDiff(v, criteria=c)
                | (:? RtCall    as u), (:? RtCall    as v)  -> yield! u.ComputeDiff(v, criteria=c)
                | (:? RtApiDef  as u), (:? RtApiDef  as v)  -> yield! u.ComputeDiff(v, criteria=c)
                | (:? RtApiCall as u), (:? RtApiCall as v)  -> yield! u.ComputeDiff(v, criteria=c)
                | (:? RtArrowBetweenWorks as u), (:? RtArrowBetweenWorks as v)  -> yield! u.ComputeDiff(v, criteria=c)
                | (:? RtArrowBetweenCalls as u), (:? RtArrowBetweenCalls as v)  -> yield! u.ComputeDiff(v, criteria=c)

                | _ -> failwith "ERROR"
            }
        member x.IsEqual(y:RtProject, ?criteria:Ucc) =
            x.ComputeDiff(y, ?criteria=criteria)
            |> forall (function Equal -> true | _-> false)      // _.IsEqual() : not working
