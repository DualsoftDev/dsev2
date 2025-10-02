namespace Ev2.Core.FS

open System
open System.Linq
open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Dual.Common.Base
open System.ComponentModel
open System.Diagnostics

[<AutoOpen>]
module rec DsCompareObjects =
    type IUnique with // GetGuid, GetName, GetParameter, TryGetId, TryGetRawParent
        member private x.tryGet(): Unique option = x |> tryCast<Unique>
        member private x.get(): Unique = x.tryGet() |?? (fun () -> fail())

        member x.GetGuid():Guid       = x.get().Guid
        member x.GetName()            = x.get().Name
        member x.GetParameter()       = x.get().Parameter
        member x.TryGetId():Id option = x |> tryCast<Unique> >>= _.Id
        member x.TryGetRawParent():Unique option = x.tryGet() >>= _.RawParent

    type Unique with // GetGuid, GetName, GetParameter, TryGetId, TryGetRawParent
        member private current.findRoot () =
            match current.RawParent with
            | Some parent -> parent.findRoot()
            | None -> current
        /// parent tree 를 따라 최상의 parent 객체 반환.
        member x.Root = x.findRoot()


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


    type IRtUnique with // getTableName
        member x.getTableName() =
            match x with
            | :? Project     -> Tn.Project
            | :? DsSystem    -> Tn.System
            | :? Flow        -> Tn.Flow
            | :? ApiDef      -> Tn.ApiDef
            | :? ApiCall     -> Tn.ApiCall
            | :? Work        -> Tn.Work
            | :? Call        -> Tn.Call
            | :? ArrowBetweenCalls -> Tn.ArrowCall
            | :? ArrowBetweenWorks -> Tn.ArrowWork
            | _ -> failwith $"Unknown RtUnique type: {x.GetType().Name}"

    let nullUpdateSql = (null, null)
    let getUpdatePropertiesSql (entity:IRtUnique) =
        let tableName = entity.getTableName()
        let supportsJsonB = (entity :?> Unique).Root |> tryCast<Project> >>= _.DbApi |-> _.DapperJsonB |? ""
        $"UPDATE {tableName} SET Properties=@PropertiesJsonB WHERE id=@Id"

    /// 객체 비교 결과 반환용....
    [<DebuggerDisplay("{ToString()}")>]
    type CompareResult =
        | Equal
        | LeftOnly of IRtUnique
        | RightOnly of IRtUnique
        /// (diff property name) * left * right * (updateSql*param)
        | Diff of Name * IRtUnique * IRtUnique * (string*obj)
    with
        interface ICompareResult
        override x.ToString() =
            match x with
            | Equal -> "Equal"
            | LeftOnly  (:? RtUnique as u) -> $"<- {u.Name}/{u.Id}/{guid2str u.Guid}"
            | RightOnly (:? RtUnique as u) -> $"-> {u.Name}/{u.Id}/{guid2str u.Guid}"
            | Diff (name, (:? RtUnique as left), (:? RtUnique as right), _) ->
                let l = $"{left.Name}/{left.Id}/{guid2str left.Guid}"
                let r = $"{right.Name}/{right.Id}/{guid2str right.Guid}"
                $"Diff({name}): {l} <> {r}"
            | _ -> failwith "ERROR: CompareResult.ToString()"
        static member CreateDiff(name, left, right) = Diff(name, left, right, nullUpdateSql)

    /// abberviation
    type internal Cc = CompareCriteria
    type internal Cr = CompareResult

    type IRtUnique with // ComputeDiffUnique
        member internal x.ComputeDiffUnique(y:IRtUnique, ?criteria:Cc): Cr seq =
            let c = criteria |? Cc()
            seq {
                if x.GetName() <> y.GetName() then yield Diff("Name", x, y, nullUpdateSql)
                if c.Id        && x.TryGetId()     <> y.TryGetId()     then yield Diff("Id", x, y, nullUpdateSql)
                if c.Guid      && x.GetGuid()      <> y.GetGuid()      then yield Diff("Guid", x, y, nullUpdateSql)

                if (c.Parameter && !! EmJson.IsJsonEquals(x.GetParameter(), y.GetParameter())) then
                    yield Diff("Parameter", x, y, nullUpdateSql)

                let xp = x.TryGetRawParent() |-> _.GetGuid()
                let yp = y.TryGetRawParent() |-> _.GetGuid()
                if c.ParentGuid && ( xp <> yp ) then
                    yield Diff("Parent", x, y, nullUpdateSql)
            }

    let private sortByGuid (xs:#IRtUnique list): #IRtUnique list = xs |> List.sortBy (fun x -> x.GetGuid())

    /// xs 와 ys 의 collection 간 비교
    let private computeDiffRecursively<'T when 'T :> IRtUnique>
        (xs: 'T seq)
        (ys: 'T seq)
        (criteria: Cc): Cr seq
      =
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

    type Project with // ComputeDiff
        member x.ComputeDiff(y:Project, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)

                (* System 들 비교*)
                yield! (x.ActiveSystems,  y.ActiveSystems,  criteria) |||> computeDiffRecursively
                yield! (x.PassiveSystems, y.PassiveSystems, criteria) |||> computeDiffRecursively

                let xxx = x.Properties.Database
                let yyy = y.Properties.Database
                (* 기타 속성 비교 *)
                // AasXml 멤버 제거됨
                if x.PropertiesJson <> y.PropertiesJson then yield Diff("Properties", x, y, (getUpdatePropertiesSql x, y))
            }
        member x.ComputeDiff(y) = x.ComputeDiff(y, Cc())

    type DsSystem with // ComputeDiff
        member x.ComputeDiff(y:DsSystem, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)

                yield! (x.Flows     , y.Flows     , criteria) |||> computeDiffRecursively
                yield! (x.Works     , y.Works     , criteria) |||> computeDiffRecursively
                yield! (x.Arrows    , y.Arrows    , criteria) |||> computeDiffRecursively
                yield! (x.ApiDefs   , y.ApiDefs   , criteria) |||> computeDiffRecursively
                yield! (x.ApiCalls  , y.ApiCalls  , criteria) |||> computeDiffRecursively

                yield! (x.Buttons   , y.Buttons   , criteria) |||> computeDiffRecursively
                yield! (x.Lamps     , y.Lamps     , criteria) |||> computeDiffRecursively
                yield! (x.Conditions, y.Conditions, criteria) |||> computeDiffRecursively
                yield! (x.Actions   , y.Actions   , criteria) |||> computeDiffRecursively

                if x.IRI           <> y.IRI           then yield Diff(nameof x.IRI, x, y, nullUpdateSql)
                //if x.Properties.Author        <> y.Properties.Author        then yield Diff("Author", x, y, nullUpdateSql)
                //if x.Properties.EngineVersion <> y.Properties.EngineVersion then yield Diff("EngineVersion", x, y, nullUpdateSql)
                //if x.Properties.LangVersion   <> y.Properties.LangVersion   then yield Diff("LangVersion", x, y, nullUpdateSql)
                //if x.Properties.Description   <> y.Properties.Description   then yield Diff("Description", x, y, nullUpdateSql)
                //if criteria.DateTime && !! x.Properties.DateTime.IsEqualTime(y.Properties.DateTime) then
                //    yield Diff("DateTime", x, y, nullUpdateSql)
                if x.PropertiesJson <> y.PropertiesJson then yield Diff("Properties", x, y, (getUpdatePropertiesSql x, y))
            }
        member x.ComputeDiff(y) = x.ComputeDiff(y, Cc())


    type Flow with // ComputeDiff
        member x.ComputeDiff(y:Flow, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if (x.System |-> _.Guid) <> (y.System |-> _.Guid)   then yield Diff("OwnerSystem", x, y, nullUpdateSql)
                if x.PropertiesJson <> y.PropertiesJson then yield Diff("Properties", x, y, (getUpdatePropertiesSql x, y))
            }

    type Work with // ComputeDiff
        member x.ComputeDiff(y:Work, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)

                if (x.System |-> _.Guid) <> (y.System |-> _.Guid) then yield Diff("OwnerSystem", x, y, nullUpdateSql)

                let xp = x.FlowGuid
                let yp = y.FlowGuid
                if xp <> yp then
                    let updateSql = $"UPDATE {Tn.Work} SET flowId = {y.Flow.Value.Id.Value} WHERE id = {x.Id.Value};"
                    yield Diff("FlowId", x, y, (updateSql, null))

                if x.Motion        <> y.Motion        then yield Diff(nameof x.Motion,        x, y, nullUpdateSql)
                if x.Script        <> y.Script        then yield Diff(nameof x.Script,        x, y, nullUpdateSql)
                if x.ExternalStart <> y.ExternalStart then yield Diff(nameof x.ExternalStart, x, y, nullUpdateSql)
                if x.IsFinished    <> y.IsFinished    then yield Diff(nameof x.IsFinished,    x, y, nullUpdateSql)
                if x.NumRepeat     <> y.NumRepeat     then yield Diff(nameof x.NumRepeat,     x, y, nullUpdateSql)
                if x.Period        <> y.Period        then yield Diff(nameof x.Period,        x, y, nullUpdateSql)
                if x.Delay         <> y.Delay         then yield Diff(nameof x.Delay,         x, y, nullUpdateSql)
                if criteria.RuntimeStatus && x.Status4 <> y.Status4      then yield Diff("Status", x, y, nullUpdateSql)
                if x.PropertiesJson <> y.PropertiesJson then yield Diff("Properties", x, y, (getUpdatePropertiesSql x, y))

                yield! (x.Calls,  y.Calls,  criteria) |||> computeDiffRecursively
                yield! (x.Arrows, y.Arrows, criteria) |||> computeDiffRecursively
            }

    type Call with // ComputeDiff
        member x.ComputeDiff(y:Call, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)

                if (x.Work |-> _.Guid)  <> (y.Work |-> _.Guid)  then yield Diff(nameof x.Work, x, y, nullUpdateSql)
                // ApiCallValueSpecs 비교를 위해 JSON으로 변환하여 비교
                if x.AutoConditions.ToJson() <> y.AutoConditions.ToJson() then yield Diff(nameof x.AutoConditions, x, y, nullUpdateSql)
                if x.CommonConditions.ToJson() <> y.CommonConditions.ToJson() then yield Diff(nameof x.CommonConditions, x, y, nullUpdateSql)
                if x.CallType   <> y.CallType    then yield Diff(nameof x.CallType, x, y, nullUpdateSql)
                if x.IsDisabled <> y.IsDisabled  then yield Diff(nameof x.IsDisabled, x, y, nullUpdateSql)
                if x.Timeout    <> y.Timeout     then yield Diff(nameof x.Timeout, x, y, nullUpdateSql)
                if criteria.RuntimeStatus && x.Status4 <> y.Status4 then yield Diff("Status", x, y, nullUpdateSql)
                if x.PropertiesJson <> y.PropertiesJson then yield Diff("Properties", x, y, (getUpdatePropertiesSql x, y))

                let d1 = (x.ApiCallGuids, y.ApiCallGuids) ||> setEqual |> not
                if d1 then yield Diff("ApiCalls", x, y, nullUpdateSql)
            }

    type ApiDef with // ComputeDiff
        member x.ComputeDiff(y:ApiDef, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if x.IsPush <> y.IsPush then yield Diff(nameof x.IsPush, x, y, nullUpdateSql)
                if x.TxGuid <> y.TxGuid then yield Diff(nameof x.TxGuid, x, y, ($"UPDATE {Tn.ApiDef} SET txId={y.TX.Id.Value} WHERE id={y.Id.Value}", null))
                if x.RxGuid <> y.RxGuid then yield Diff(nameof x.RxGuid, x, y, ($"UPDATE {Tn.ApiDef} SET rxId={y.RX.Id.Value} WHERE id={y.Id.Value}", null))
                if x.PropertiesJson <> y.PropertiesJson then yield Diff("Properties", x, y, (getUpdatePropertiesSql x, y))
            }

    type ApiCall with // ComputeDiff
        member x.ComputeDiff(y:ApiCall, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if x.ApiDefGuid <> y.ApiDefGuid then yield Diff(nameof x.ApiDefGuid, x, y, nullUpdateSql)
                if x.InAddress  <> y.InAddress  then yield Diff(nameof x.InAddress, x, y, nullUpdateSql)
                if x.OutAddress <> y.OutAddress then yield Diff(nameof x.OutAddress, x, y, nullUpdateSql)
                if x.InSymbol   <> y.InSymbol   then yield Diff(nameof x.InSymbol, x, y, nullUpdateSql)
                if x.OutSymbol  <> y.OutSymbol  then yield Diff(nameof x.OutSymbol, x, y, nullUpdateSql)
                if x.ValueSpec  <> y.ValueSpec  then yield Diff(nameof x.ValueSpec, x, y, nullUpdateSql)
                if x.IOTagsJson <> y.IOTagsJson then yield Diff(nameof x.IOTagsJson, x, y, nullUpdateSql)
                if x.PropertiesJson <> y.PropertiesJson then yield Diff("Properties", x, y, (getUpdatePropertiesSql x, y))
            }

    type ArrowBetweenWorks with // ComputeDiff
        member x.ComputeDiff(y:ArrowBetweenWorks, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if x.XSourceGuid <> y.XSourceGuid then yield Diff(nameof x.XSourceGuid, x, y, ($"UPDATE {Tn.ArrowWork} SET source={y.Source.Id.Value} WHERE id={y.Id.Value}", null))
                if x.XTargetGuid <> y.XTargetGuid then yield Diff(nameof x.XTargetGuid, x, y, ($"UPDATE {Tn.ArrowWork} SET target={y.Target.Id.Value} WHERE id={y.Id.Value}", null))
                if x.XTypeId <> y.XTypeId then yield Diff(nameof x.XTypeId, x, y, nullUpdateSql)
            }

    type ArrowBetweenCalls with // ComputeDiff
        member x.ComputeDiff(y:ArrowBetweenCalls, criteria:Cc): Cr seq =
            seq {
                yield! x.ComputeDiffUnique(y, criteria)
                if x.XSourceGuid <> y.XSourceGuid then yield Diff(nameof x.XSourceGuid, x, y, ($"UPDATE {Tn.ArrowCall} SET source={y.Source.Id.Value} WHERE id={y.Id.Value}", null))
                if x.XTargetGuid <> y.XTargetGuid then yield Diff(nameof x.XTargetGuid, x, y, ($"UPDATE {Tn.ArrowCall} SET target={y.Target.Id.Value} WHERE id={y.Id.Value}", null))
                if x.XTypeId <> y.XTypeId then yield Diff(nameof x.XTypeId, x, y, nullUpdateSql)
            }

    type BLCABase with
        member x.ComputeDiff(y:BLCABase, criteria:Cc): Cr seq =
            seq {
                if x.ComputeDiffUnique(y, criteria).Any() || x.IOTagsJson <> y.IOTagsJson then
                    let systemId = y.RawParent >>= tryCast<DsSystem> |-> _.Id |?? (fun () -> fail()) |> Option.get
                    let obj:ORMJsonSystemEntity = { Id=y.Id; SystemId=systemId; Json=y.ToJson(); Type=y.GetType().Name; Guid=y.Guid }
                    //assert(y.IOTagsJson.NonNullAny())
                    let sql = $"UPDATE {Tn.SystemEntity} SET systemId = @SystemId, type = @Type, json = @Json WHERE guid = @Guid"
                    yield Diff(y.GetType().Name, x, y, (sql, obj))
            }

    type IRtUnique with // IsEqual
        member internal x.ComputeDiff(y:IRtUnique, criteria:Cc): Cr seq =
            seq {
                match x, y with
                | (:? Project  as u), (:? Project  as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? DsSystem as u), (:? DsSystem as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? Flow     as u), (:? Flow     as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? Work     as u), (:? Work     as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? Call     as u), (:? Call     as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? ApiDef   as u), (:? ApiDef   as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? ApiCall  as u), (:? ApiCall  as v)  -> yield! u.ComputeDiff(v, criteria)

                | (:? DsButton    as u), (:? DsButton    as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? Lamp        as u), (:? Lamp        as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? DsCondition as u), (:? DsCondition as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? DsAction    as u), (:? DsAction    as v)  -> yield! u.ComputeDiff(v, criteria)

                | (:? ArrowBetweenWorks as u), (:? ArrowBetweenWorks as v)  -> yield! u.ComputeDiff(v, criteria)
                | (:? ArrowBetweenCalls as u), (:? ArrowBetweenCalls as v)  -> yield! u.ComputeDiff(v, criteria)

                | _ -> fail()

                match getTypeFactory() with
                | Some factory -> yield! factory.ComputeExtensionDiff(x, y).Cast<Cr>()
                | _ -> ()
            }
        member x.IsEqual(y:Project, ?criteria:Cc) =
            let criteria = criteria |? Cc()
            x.ComputeDiff(y, criteria)
            |> forall (function Equal -> true | _-> false)      // _.IsEqual() : not working
