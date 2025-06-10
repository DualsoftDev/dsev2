namespace Ev2.Core.FS

open System
open Dual.Common.Core.FS
open Dual.Common.Base
open System.ComponentModel
open System.Diagnostics

[<AutoOpen>]
module rec DsCompareObjects =
    type IUnique with   // GetGuid, GetName, TryGetId, GetDateTime, TryGetRawParent
        member private x.tryGet(): Unique option = x |> tryCast<Unique>
        member private x.get(): Unique = x.tryGet() |?? (fun () -> failwith "ERROR")

        member x.GetGuid():Guid                  = x.get().Guid
        member x.GetName()                       = x.get().Name
        member x.GetParameter()                  = x.get().Parameter
        member x.TryGetId():Id option            = x |> tryCast<Unique> >>= _.Id
        member x.TryGetRawParent():Unique option = x.tryGet() >>= _.RawParent

    type CompareCriteria(?id:bool, ?guid:bool, ?dateTime:bool, ?parentGuid, ?parameter, ?runtimeStatus) =
        let id         = id         |? true
        let guid       = guid       |? true
        let dateTime   = dateTime   |? true
        let parentGuid = parentGuid |? true
        let parameter  = parameter  |? true
        let runtimeStatus = runtimeStatus |? false
        member val Id         = id         with get, set
        member val Guid       = guid       with get, set
        member val DateTime   = dateTime   with get, set
        member val ParentGuid = parentGuid with get, set
        member val Parameter  = parameter  with get, set
        member val RuntimeStatus = runtimeStatus with get, set

        (* Project/System 속성 *)
        member val IRI                  = true with get, set
        member val Author               = true with get, set
        member val Version              = true with get, set
        member val Description          = true with get, set
        member val LastConnectionString = true with get, set
        member val EngineVersion        = true with get, set
        member val LangVersion          = true with get, set

    /// 객체 비교 결과 반환용....
    [<DebuggerDisplay("{ToString()}")>]
    type CompareResult =
        | Equal
        | LeftOnly of IRtUnique
        | RightOnly of IRtUnique
        /// (diff property name) * left * right
        | Diff of Name * IRtUnique * IRtUnique
    with
        override x.ToString() =
            match x with
            | Equal -> "Equal"
            | LeftOnly  (:? RtUnique as u) -> $"<- {u.Name}/{u.Id}/{guid2str u.Guid}"
            | RightOnly (:? RtUnique as u) -> $"-> {u.Name}/{u.Id}/{guid2str u.Guid}"
            | Diff (name, (:? RtUnique as left), (:? RtUnique as right)) ->
                let l = $"{left.Name}/{left.Id}/{guid2str left.Guid}"
                let r = $"{right.Name}/{right.Id}/{guid2str right.Guid}"
                $"Diff({name}): {l} <> {r}"

    /// abberviation
    type internal Cc = CompareCriteria
    type internal Cr = CompareResult

    type IRtUnique with // ComputeDiffUnique
        member internal x.ComputeDiffUnique(y:IRtUnique, ?criteria:Cc): Cr seq =
            let c = criteria |? Cc()
            seq {
                if x.GetName() <> y.GetName() then yield Diff("Name", x, y)
                if c.Id        && x.TryGetId()     <> y.TryGetId()     then yield Diff("Id", x, y)
                if c.Guid      && x.GetGuid()      <> y.GetGuid()      then yield Diff("Guid", x, y)
                if c.Parameter && x.GetParameter() <> y.GetParameter() then yield Diff("Parameter", x, y)

                let xp = x.TryGetRawParent() |-> _.GetGuid()
                let yp = y.TryGetRawParent() |-> _.GetGuid()
                if c.ParentGuid && ( xp <> yp ) then
                    yield Diff("Parent", x, y)
            }

    let private sortByGuid (xs:#IRtUnique list): #IRtUnique list = xs |> List.sortBy (fun x -> x.GetGuid())

    /// xs 와 ys 의 collection 간 비교
    let private computeDiffRecursively<'T when 'T :> IRtUnique>
        (xs: 'T seq)
        (ys: 'T seq)
        (criteria: Cc): Cr seq =

        let xs = xs.ToDictionary(_.GetGuid(), id)
        let ys = ys.ToDictionary(_.GetGuid(), id)

        let allGuids = xs.Keys @ ys.Keys |> Set.ofSeq

        seq {
            for guid in allGuids do
                match xs.TryGet(guid), ys.TryGet(guid) with
                | Some x, Some y ->
                    let diffs = x.ComputeDiff(y, criteria)
                    yield! diffs
                | Some x, None ->
                    yield LeftOnly x
                | None, Some y ->
                    yield RightOnly y
                | _ -> ()
        }


    type RtProject with // ComputeDiff
        member x.ComputeDiff(y:RtProject, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)

                (* System 들 비교*)
                yield! (x.PrototypeSystems, y.PrototypeSystems,  criteria) |||> computeDiffRecursively
                yield! (x.ActiveSystems,    y.ActiveSystems,     criteria) |||> computeDiffRecursively
                yield! (x.PassiveSystems,   y.PassiveSystems,    criteria) |||> computeDiffRecursively

                (* 기타 속성 비교 *)
                if criteria.Author && x.Author <> y.Author then yield Diff("Author", x, y)
                if criteria.DateTime && x.DateTime <> y.DateTime then yield Diff("DateTime", x, y)
            }
        member x.ComputeDiff(y) = x.ComputeDiff(y, Cc())

    type RtSystem with // ComputeDiff
        member x.ComputeDiff(y:RtSystem, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)

                yield! (x.Flows   , y.Flows   , criteria) |||> computeDiffRecursively
                yield! (x.Works   , y.Works   , criteria) |||> computeDiffRecursively
                yield! (x.Arrows  , y.Arrows  , criteria) |||> computeDiffRecursively
                yield! (x.ApiDefs , y.ApiDefs , criteria) |||> computeDiffRecursively
                yield! (x.ApiCalls, y.ApiCalls, criteria) |||> computeDiffRecursively

                if x.PrototypeSystemGuid <> y.PrototypeSystemGuid then yield Diff("PrototypeSystemGuid", x, y)
                if x.Author        <> y.Author        then yield Diff("Author", x, y)
                if x.IRI           <> y.IRI           then yield Diff("IRI", x, y)
                if x.EngineVersion <> y.EngineVersion then yield Diff("EngineVersion", x, y)
                if x.LangVersion   <> y.LangVersion   then yield Diff("LangVersion", x, y)
                if x.Description   <> y.Description   then yield Diff("Description", x, y)
                if criteria.DateTime && x.DateTime <> y.DateTime then yield Diff("DateTime", x, y)
            }
        member x.ComputeDiff(y) = x.ComputeDiff(y, Cc())


    type RtFlow with // ComputeDiff
        member x.ComputeDiff(y:RtFlow, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if (x.System |-> _.Guid) <> (y.System |-> _.Guid)   then yield Diff("OwnerSystem", x, y)

                // System 의 works 에서 비교할 것이기 때문에 여기서 비교하면 중복 비교가 됨.
                //yield! (x.Works, y.Works, criteria) |||> computeDiffList

                yield! (x.Buttons,     y.Buttons,     criteria) |||> computeDiffRecursively
                yield! (x.Lamps,       y.Lamps,       criteria) |||> computeDiffRecursively
                yield! (x.Conditions,  y.Conditions,  criteria) |||> computeDiffRecursively
                yield! (x.Actions,     y.Actions,     criteria) |||> computeDiffRecursively
            }

    type RtWork with // ComputeDiff
        member x.ComputeDiff(y:RtWork, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)

                if (x.System |-> _.Guid) <> (y.System |-> _.Guid) then yield Diff("OwnerSystem", x, y)

                let xp = x.Flow |-> _.Guid
                let yp = y.Flow |-> _.Guid
                if xp <> yp then yield Diff("OwnerFlow", x, y)

                if x.Motion     <> y.Motion     then yield Diff("Motion", x, y)
                if x.Script     <> y.Script     then yield Diff("Script", x, y)
                if x.IsFinished <> y.IsFinished then yield Diff("IsFinished", x, y)
                if x.NumRepeat  <> y.NumRepeat  then yield Diff("NumRepeat", x, y)
                if x.Period     <> y.Period     then yield Diff("Period", x, y)
                if x.Delay      <> y.Delay      then yield Diff("Delay", x, y)
                if criteria.RuntimeStatus && x.Status4 <> y.Status4      then yield Diff("Status", x, y)

                yield! (x.Calls,  y.Calls,  criteria) |||> computeDiffRecursively
                yield! (x.Arrows, y.Arrows, criteria) |||> computeDiffRecursively
            }

    type RtCall with // ComputeDiff
        member x.ComputeDiff(y:RtCall, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)

                if (x.Work |-> _.Guid)  <> (y.Work |-> _.Guid)  then yield Diff("Work", x, y)
                if not <| isStringsEqual x.AutoConditions   y.AutoConditions   then yield Diff("AutoConditions", x, y)
                if not <| isStringsEqual x.CommonConditions y.CommonConditions then yield Diff("CommonConditions", x, y)
                if x.CallType   <> y.CallType    then yield Diff("CallType", x, y)
                if x.IsDisabled <> y.IsDisabled  then yield Diff("IsDisabled", x, y)
                if x.Timeout    <> y.Timeout     then yield Diff("Timeout", x, y)
                if criteria.RuntimeStatus && x.Status4 <> y.Status4 then yield Diff("Status", x, y)

                let d1 = (x.ApiCallGuids, y.ApiCallGuids) ||> setEqual |> not
                if d1 then yield Diff("ApiCalls", x, y)
            }

    type RtApiDef with // ComputeDiff
        member x.ComputeDiff(y:RtApiDef, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if x.IsPush <> y.IsPush   then yield Diff("IsPush", x, y)
            }

    type RtApiCall with // ComputeDiff
        member x.ComputeDiff(y:RtApiCall, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if x.ApiDefGuid <> y.ApiDefGuid then yield Diff("ApiDefGuid", x, y)
                if x.InAddress  <> y.InAddress  then yield Diff("InAddress", x, y)
                if x.OutAddress <> y.OutAddress then yield Diff("OutAddress", x, y)
                if x.InSymbol   <> y.InSymbol   then yield Diff("InSymbol", x, y)
                if x.OutSymbol  <> y.OutSymbol  then yield Diff("OutSymbol", x, y)
                if x.ValueSpec  <> y.ValueSpec  then yield Diff("ValueSpec", x, y)
            }

    type RtArrowBetweenWorks with // ComputeDiff
        member x.ComputeDiff(y:RtArrowBetweenWorks, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if x.Source.Guid <> y.Source.Guid then yield Diff("Source", x, y)
                if x.Target.Guid <> y.Target.Guid then yield Diff("Target", x, y)
                if x.Type <> y.Type then yield Diff("Type", x, y)
            }

    type RtArrowBetweenCalls with // ComputeDiff
        member x.ComputeDiff(y:RtArrowBetweenCalls, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if x.Source.Guid <> y.Source.Guid then yield Diff("Source", x, y)
                if x.Target.Guid <> y.Target.Guid then yield Diff("Target", x, y)
                if x.Type <> y.Type then yield Diff("Type", x, y)
            }

    type RtButton with // ComputeDiff
        member x.ComputeDiff(y:RtButton, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
            }
    type RtLamp with // ComputeDiff
        member x.ComputeDiff(y:RtLamp, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
            }
    type RtCondition with // ComputeDiff
        member x.ComputeDiff(y:RtCondition, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
            }
    type RtAction with // ComputeDiff
        member x.ComputeDiff(y:RtAction, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
            }


    type IRtUnique with // ComputeDiff, IsEqual
        member internal x.ComputeDiff(y:IRtUnique, criteria:Cc): Cr seq =
            seq {
                match x, y with
                | (:? RtProject as u), (:? RtProject as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtSystem  as u), (:? RtSystem  as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtFlow    as u), (:? RtFlow    as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtWork    as u), (:? RtWork    as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtCall    as u), (:? RtCall    as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtApiDef  as u), (:? RtApiDef  as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtApiCall as u), (:? RtApiCall as v)  -> yield! u.ComputeDiff(v, criteria)

                | (:? RtButton    as u), (:? RtButton    as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtLamp      as u), (:? RtLamp      as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtCondition as u), (:? RtCondition as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtAction    as u), (:? RtAction    as v)  -> yield! u.ComputeDiff(v, criteria)

                | (:? RtArrowBetweenWorks as u), (:? RtArrowBetweenWorks as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? RtArrowBetweenCalls as u), (:? RtArrowBetweenCalls as v)  -> yield! u.ComputeDiff(v, criteria)

                | _ -> failwith "ERROR"
            }
        member x.IsEqual(y:RtProject, ?criteria:Cc) =
            let criteria = criteria |? Cc()
            let xxx = x.ComputeDiff(y, criteria).ToArray()
            x.ComputeDiff(y, criteria)
            |> forall (function Equal -> true | _-> false)      // _.IsEqual() : not working
