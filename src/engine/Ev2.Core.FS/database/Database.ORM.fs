namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Dual.Common.Base
open System.Collections.Generic

[<AutoOpen>]
module ORMTypesModule =

    type RowId = int
    type IORMProject    = inherit IORMRow
    type IORMSystem     = inherit IORMRow
    type IORMFlow       = inherit IORMRow
    type IORMWork       = inherit IORMRow
    type IORMCall       = inherit IORMRow

    type IORMApiCall    = inherit IORMRow
    type IORMApiDef     = inherit IORMRow
    type IORMParamWork  = inherit IORMRow
    type IORMParamCall  = inherit IORMRow
    type IORMMeta       = inherit IORMRow
    type IORMLog        = inherit IORMRow

    [<AbstractClass>]
    type ORMUniq(name, guid:Guid, id:Nullable<Id>, dateTime:DateTime) =
        interface IUnique
        interface IORMRow

        member val Id = id with get, set
        member val Pid = Nullable<Id>() with get, set
        member val Name = name with get, set

        member val Guid = guid.ToString("D") with get, set

        member val DateTime = dateTime with get, set
        member val RawParent = Option<ORMUniq>.None with get, set

        new() = ORMUniq(null, emptyGuid, nullableId, minDate)
        new(name, guid:Guid, id:Nullable<Id>) = ORMUniq(name, guid, id, minDate)

    [<AbstractClass>]
    type ORMArrowBase(srcId:int, tgtId:int, parentId:int, guid:Guid, id:Nullable<Id>, dateTime:DateTime) =
        inherit ORMUniq(null, guid, id, dateTime)
        new() = ORMArrowBase(-1, -1, -1, emptyGuid, nullableId, minDate)
        member val Source = srcId with get, set
        member val Target = tgtId with get, set

    /// Work 간 연결.  System 에 속함
    type ORMArrowWork(srcId:int, tgtId:int, systemId:int, guid:Guid, id:Nullable<Id>, dateTime:DateTime) =
        inherit ORMArrowBase(srcId, tgtId, systemId, guid, id, dateTime)
        new() = ORMArrowWork(-1, -1, -1, emptyGuid, nullableId, minDate)
        member val SystemId = id with get, set

    /// Call 간 연결.  Work 에 속함
    type ORMArrowCall(srcId:int, tgtId:int, workId:int, guid:Guid, id:Nullable<Id>, dateTime:DateTime) =
        inherit ORMArrowBase(srcId, tgtId, workId, guid, id, dateTime)
        new() = ORMArrowCall(-1, -1, -1, emptyGuid, nullableId, minDate)
        member val WorkId = id with get, set

    /// Object Releation Mapper for Asset
    type ORMProject(name, guid, id:Id, dateTime, author:string, version, (*langVersion, engineVersion,*) description) =
        inherit ORMUniq(name, guid, id, dateTime)
        interface IORMProject
        member val Author = author with get, set
        member val Version       = version     with get, set
        member val Description   = description with get, set


        new() = ORMProject(null, emptyGuid, -1, minDate, Environment.UserName, nullVersion, nullString)
        new(name, guid) = ORMProject(name, guid, -1, minDate, Environment.UserName, nullVersion, nullString)

    type ORMSystem(name, guid, id:Id, dateTime, originGuid:Nullable<Guid>, author:string, langVersion:Version, engineVersion:Version, description:string) =
        inherit ORMUniq(name, guid, id, dateTime)
        interface IORMSystem
        new() = ORMSystem(null, emptyGuid, -1, minDate, nullableGuid, nullString, nullVersion, nullVersion, nullString)
        new(name, guid) = ORMSystem(name, guid, -1, now(), nullableGuid, nullString, nullVersion, nullVersion, nullString)

        member val Author        = author        with get, set
        member val EngineVersion = engineVersion with get, set
        member val LangVersion   = langVersion   with get, set
        member val Description   = description   with get, set

        member val OriginGuid = originGuid with get, set

    type ORMFlow(name, guid, id:Id, systemId:Id, dateTime) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- systemId

        interface IORMFlow
        new() = ORMFlow(null, emptyGuid, -1, -1, minDate)
        member x.SystemId with get() = x.Pid and set v = x.Pid <- v

    type ORMWork(name, guid, id:Id, systemId:Id, dateTime, flowId:Nullable<Id>) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- systemId

        interface IORMWork
        new() = ORMWork(null, emptyGuid, -1, -1, minDate, nullableId)
        member val FlowId = flowId with get, set
        member x.SystemId with get() = x.Pid and set v = x.Pid <- v

    type ORMCall(name, guid, id:Id, workId:Id, dateTime) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- workId

        interface IORMCall
        new() = ORMCall(null, emptyGuid, -1, -1, minDate)
        member x.WorkId with get() = x.Pid and set v = x.Pid <- v



    type ORMProjectSystemMap(projectId:Id, systemId:Id, isActive:bool, name, guid, id:Id, dateTime) =
        inherit ORMUniq(name, guid, id, dateTime)
        new() = ORMProjectSystemMap(-1, -1, false, null, emptyGuid, -1, minDate)
        member val ProjectId = projectId with get, set
        member val SystemId = systemId with get, set
        member val IsActive = isActive with get, set

    type ORMApiCall(name, guid, id:Id, workId:Id, dateTime) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- workId

        interface IORMApiCall
        new() = ORMApiCall(null, emptyGuid, -1, -1, minDate)
        member x.WorkId with get() = x.Pid and set v = x.Pid <- v

    type ORMApiDef(name, guid, id:Id, workId:Id, dateTime) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- workId

        interface IORMApiDef
        new() = ORMApiDef(null, emptyGuid, -1, -1, minDate)
        member x.WorkId with get() = x.Pid and set v = x.Pid <- v


[<AutoOpen>]
module ORMTypeConversionModule =
    let o2n = Option.toNullable
    let private ds2Orm (guidDic:Dictionary<Guid, ORMUniq>) (x:IDsObject) =
            match x |> tryCast<Unique> with
            | Some uniq ->
                let id = uniq.Id |? -1
                let pid = (uniq.RawParent >>= _.Id) |? -1
                let guid, name = uniq.Guid, uniq.Name
                let pGuid, dateTime = uniq.PGuid, uniq.DateTime

                match uniq with
                | :? DsProject as z ->
                    ORMProject(name, guid, id, dateTime, z.Author, z.Version, z.Description) :> ORMUniq
                | :? DsSystem as z -> ORMSystem(name, guid, id, dateTime, z.OriginGuid |> Option.toNullable, z.Author, z.LangVersion, z.EngineVersion, z.Description)
                | :? DsFlow   as z -> ORMFlow  (name, guid, id, pid, dateTime)
                | :? DsWork   as z ->
                    let flowId = z.OptFlowGuid |-> (fun fguid -> guidDic[fguid].Id) |? Nullable<Id>()
                    ORMWork  (name, guid, id, pid, dateTime, flowId)
                | :? DsCall   as z -> ORMCall  (name, guid, id, pid, dateTime)

                | :? ArrowBetweenWorks as z ->  // arrow 삽입 전에 parent 및 양 끝점 node(call, work 등) 가 먼저 삽입되어 있어야 한다.
                    let id, src, tgt = o2n z.Id, z.Source.Id.Value, z.Target.Id.Value
                    let parentId = (z.RawParent >>= _.Id).Value
                    ORMArrowWork (src, tgt, parentId, z.Guid, id, z.DateTime)

                | :? ArrowBetweenCalls as z ->  // arrow 삽입 전에 parent 및 양 끝점 node(call, work 등) 가 먼저 삽입되어 있어야 한다.
                    let id, src, tgt = o2n z.Id, z.Source.Id.Value, z.Target.Id.Value
                    let parentId = (z.RawParent >>= _.Id).Value
                    ORMArrowCall (src, tgt, parentId, z.Guid, id, z.DateTime)

                | _ -> failwith $"Not yet for conversion into ORM.{x.GetType()}={x}"

                |> tee (fun ormUniq -> guidDic[guid] <- ormUniq )

            | _ -> failwithf "Cannot convert to ORM. %A" x


    type IDsObject with
        /// DS object 를 DB 에 기록하기 위한 ORM object 로 변환.  e.g DsProject -> ORMProject
        member x.ToORM(guidDic:Dictionary<Guid, ORMUniq>) = ds2Orm guidDic x

    type DsProject with
        /// DsProject 를 DB 에 기록하기 위한 ORMProject 로 변환.
        member x.ToORM(): Dictionary<Guid, ORMUniq> * ORMUniq =
            let guidDic = Dictionary<Guid, ORMUniq>()
            guidDic, ds2Orm guidDic x

    type DsSystem with
        /// DsSystem 를 DB 에 기록하기 위한 ORMSystem 로 변환.
        member x.ToORM(): Dictionary<Guid, ORMUniq> * ORMUniq =
            let guidDic = Dictionary<Guid, ORMUniq>()
            guidDic, ds2Orm guidDic x



