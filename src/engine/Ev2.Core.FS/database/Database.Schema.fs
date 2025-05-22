namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Dual.Common.Base
open System.Collections.Generic

[<AutoOpen>]
module DatabaseSchemaModule =

    /// NVARCHAR({NameLength}) 는 TEXT 와 완전 동일하며, 혹시 다른 DBMS 를 사용할 경우의 호환성을 위한 것.
    let [<Literal>] NameLength = 128

    #if DEBUG   // TODO : 제거 요망
    /// UNIQUE indexing 여부 성능 고려해서 판단 필요
    let [<Literal>] guidUniqSpec = "UNIQUE"
    let [<Literal>] intKeyType = "INTEGER"      // or "INTEGER"
    #else
    let [<Literal>] guidUniqSpec = ""
    #endif


    module Tn =
        let [<Literal>] Project      = "project"
        let [<Literal>] System       = "system"
        let [<Literal>] Flow         = "flow"
        let [<Literal>] Work         = "work"
        let [<Literal>] Call         = "call"
        let [<Literal>] ArrowWork    = "arrowWork"
        let [<Literal>] ArrowCall    = "arrowCall"
        let [<Literal>] ApiCall      = "apiCall"
        let [<Literal>] ApiDef       = "apiDef"

        let [<Literal>] ParamWork    = "paramWork"
        let [<Literal>] ParamCall    = "paramCall"

        let [<Literal>] Meta         = "meta"
        let [<Literal>] Log          = "log"
        let [<Literal>] TableHistory = "tableHistory"
        let [<Literal>] ProjectSystemMap      = "projectSystemMap"
        let [<Literal>] EOT          = "endOfTable"

        let AllTableNames = [ Project; System; Flow; Work; Call; ArrowWork; ArrowCall; ApiCall; ApiDef; ParamWork; ParamCall; Meta; TableHistory; ProjectSystemMap; ]        // Log;

    // database view names
    module Vn =
        let Log     = "vwLog"
        let Storage = "vwStorage"


    let triggerSql() =
        // op : {INSERT, UPDATE, DELETE}
        let createTrigger (op:string) (tableName:string) =
            let update =
                match op with
                | "INSERT" ->
                    $"""
                    INSERT INTO {Tn.TableHistory} (name, operation, oldId, newId)
                    SELECT '{tableName}', '{op}', NULL, NEW.id
                    WHERE NOT EXISTS (
                        SELECT 1 FROM tableHistory WHERE newId = NEW.id AND name = '{tableName}' AND operation = '{op}'
                    );
                    """
                | "UPDATE" ->
                    $"""
                    INSERT INTO {Tn.TableHistory} (name, operation, oldId, newId)
                    SELECT '{tableName}', '{op}', OLD.id, NEW.id
                    WHERE NOT EXISTS (
                        SELECT 1 FROM tableHistory WHERE name = '{tableName}' AND operation = '{op}' AND oldId = OLD.id AND newId = NEW.id
                    );
                    """
                | "DELETE" ->
                    $"""
                    INSERT INTO {Tn.TableHistory} (name, operation, oldId, newId)
                    VALUES ('{tableName}', '{op}', OLD.id, NULL);
                    """
                | _ -> failwith "invalid op"
            $"""
            DROP TRIGGER IF EXISTS trigger_{tableName}_{op};
            CREATE TRIGGER trigger_{tableName}_{op} AFTER {op} ON {tableName}
            BEGIN {update} END;
            """
        let historyTargetTables = Tn.AllTableNames |> except [Tn.TableHistory]

        [ for t in historyTargetTables do
            for op in ["INSERT"; "UPDATE"; "DELETE"] do
                createTrigger op t ]
        |> String.concat ""


    let sqlUniq() = $"""
    [id]              {intKeyType} PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL {guidUniqSpec}   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
"""

    let sqlUniqWithName() = sqlUniq() + $"""
    , [name]          NVARCHAR({NameLength}) NOT NULL
"""

    let private getSqlCreateSchemaHelper(withTrigger:bool) =
        $"""
BEGIN TRANSACTION;


CREATE TABLE [{Tn.Project}]( {sqlUniqWithName()}
);

CREATE TABLE [{Tn.System}]( {sqlUniqWithName()}
);


CREATE TABLE [{Tn.ProjectSystemMap}]( {sqlUniq()}
    , [projectId]      {intKeyType} NOT NULL
    , [systemId]       {intKeyType} NOT NULL
    , [active]         TINYINT NOT NULL DEFAULT 0
    , FOREIGN KEY(projectId)   REFERENCES {Tn.Project}(id) ON DELETE CASCADE
    , FOREIGN KEY(systemId)    REFERENCES {Tn.System}(id) ON DELETE CASCADE
    , CONSTRAINT {Tn.ProjectSystemMap}_uniq UNIQUE (projectId, systemId)
);

CREATE TABLE [{Tn.Flow}]( {sqlUniqWithName()}
    , [systemId]      {intKeyType} NOT NULL
    , FOREIGN KEY(systemId)   REFERENCES {Tn.System}(id) ON DELETE CASCADE
);

CREATE TABLE [{Tn.Work}]( {sqlUniqWithName()}

    , [systemId]      {intKeyType} NOT NULL
    , [flowId]      {intKeyType} DEFAULT NULL    -- NULL 허용 (work가 flow에 속하지 않을 수도 있음)

    , FOREIGN KEY(systemId) REFERENCES {Tn.System}(id) ON DELETE CASCADE
    , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE      -- Flow 삭제시 work 삭제, flowId 는 null 허용
);

CREATE TABLE [{Tn.Call}]( {sqlUniqWithName()}
    , [workId]        {intKeyType} NOT NULL
    , FOREIGN KEY(workId)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Call 도 삭제
);

-- Work 간 연결.  System 에 속함
CREATE TABLE [{Tn.ArrowWork}]( {sqlUniq()}
    , [source]        {intKeyType} NOT NULL
    , [target]        {intKeyType} NOT NULL
    , [systemId]      {intKeyType} NOT NULL
    , FOREIGN KEY(source)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(systemId) REFERENCES {Tn.System}(id) ON DELETE CASCADE    -- System 삭제시 Arrow 도 삭제
);

-- Call 간 연결.  Work 에 속함
CREATE TABLE [{Tn.ArrowCall}]( {sqlUniq()}
    , [source]        {intKeyType} NOT NULL
    , [target]        {intKeyType} NOT NULL
    , [workId]        {intKeyType} NOT NULL
    , FOREIGN KEY(source)   REFERENCES {Tn.Call}(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES {Tn.Call}(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(workId)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
);


CREATE TABLE [{Tn.ApiCall}]( {sqlUniqWithName()}
);

CREATE TABLE [{Tn.ApiDef}]( {sqlUniqWithName()}
);

CREATE TABLE [{Tn.ParamWork}] (  {sqlUniq()}
);

CREATE TABLE [{Tn.ParamCall}] (  {sqlUniq()}
);


CREATE TABLE [{Tn.Meta}] (
    id {intKeyType} PRIMARY KEY NOT NULL,
    key TEXT NOT NULL,
    val TEXT NOT NULL
);


-- tableHistory 테이블
CREATE TABLE [{Tn.TableHistory}] (
    id {intKeyType} PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    operation TEXT,
    oldId {intKeyType},
    newId {intKeyType},
    CONSTRAINT {Tn.TableHistory}_uniq UNIQUE (name, operation, oldId, newId)
);



{ if withTrigger then triggerSql() else "" }


INSERT INTO [{Tn.Meta}] (key, val) VALUES ('Version', '1.0.0.0');
DELETE FROM {Tn.TableHistory};

CREATE TABLE [{Tn.EOT}](
    id {intKeyType} PRIMARY KEY NOT NULL
);

COMMIT;
"""

    /// SQL schema 생성.  trigger 도 함께 생성하려면 getSqlCreateSchemaWithTrigger() 사용
    let getSqlCreateSchema() = getSqlCreateSchemaHelper false
    let getSqlCreateSchemaWithTrigger() = getSqlCreateSchemaHelper true



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

        new() = ORMUniq(null, nullGuid, Nullable(), nullDate)
        new(name, guid:Guid, id:Nullable<Id>) = ORMUniq(name, guid, id, nullDate)

    [<AbstractClass>]
    type ORMArrowBase(srcId:int, tgtId:int, parentId:int, guid:Guid, id:Nullable<Id>, dateTime:DateTime) =
        inherit ORMUniq(null, guid, id, dateTime)
        member x.Source = srcId
        member x.Target = tgtId

    /// Work 간 연결.  System 에 속함
    type ORMArrowWork(srcId:int, tgtId:int, systemId:int, guid:Guid, id:Nullable<Id>, dateTime:DateTime) =
        inherit ORMArrowBase(srcId, tgtId, systemId, guid, id, dateTime)
        member val SystemId = id with get, set

    /// Call 간 연결.  Work 에 속함
    type ORMArrowCall(srcId:int, tgtId:int, workId:int, guid:Guid, id:Nullable<Id>, dateTime:DateTime) =
        inherit ORMArrowBase(srcId, tgtId, workId, guid, id, dateTime)
        member val WorkId = id with get, set

    /// Object Releation Mapper for Asset
    type ORMProject(name, guid, id:Id, dateTime) =
        inherit ORMUniq(name, guid, id, dateTime)
        interface IORMProject
        new() = ORMProject(null, nullGuid, -1, nullDate)
        new(name, guid) = ORMProject(name, guid, -1, nullDate)

    type ORMSystem(name, guid, id:Id, dateTime) =
        inherit ORMUniq(name, guid, id, dateTime)
        interface IORMSystem
        new() = ORMSystem(null, nullGuid, -1, nullDate)
        new(name, guid) = ORMSystem(name, guid, -1, nullDate)

    type ORMFlow(name, guid, id:Id, systemId:Id, dateTime) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- systemId

        interface IORMFlow
        new() = ORMFlow(null, nullGuid, -1, -1, nullDate)
        member x.SystemId with get() = x.Pid and set v = x.Pid <- v

    type ORMWork(name, guid, id:Id, systemId:Id, dateTime, flowId:Nullable<Id>) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- systemId

        interface IORMWork
        new() = ORMWork(null, nullGuid, -1, -1, nullDate, nullId)
        member val FlowId = flowId with get, set
        member x.SystemId with get() = x.Pid and set v = x.Pid <- v

    type ORMCall(name, guid, id:Id, workId:Id, dateTime) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- workId

        interface IORMCall
        new() = ORMCall(null, nullGuid, -1, -1, nullDate)
        member x.WorkId with get() = x.Pid and set v = x.Pid <- v



    type ORMProjectSystemMap(projectId:Id, systemId:Id, isActive:bool, name, guid, id:Id, dateTime) =
        inherit ORMUniq(name, guid, id, dateTime)
        member val ProjectId = projectId with get, set
        member val SystemId = systemId with get, set
        member val IsActive = isActive with get, set

    type ORMApiCall(name, guid, id:Id, workId:Id, dateTime) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- workId

        interface IORMApiCall
        new() = ORMApiCall(null, nullGuid, -1, -1, nullDate)
        member x.WorkId with get() = x.Pid and set v = x.Pid <- v

    type ORMApiDef(name, guid, id:Id, workId:Id, dateTime) as this =
        inherit ORMUniq(name, guid, id, dateTime)
        do
            this.Pid <- workId

        interface IORMApiDef
        new() = ORMApiDef(null, nullGuid, -1, -1, nullDate)
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
                | :? DsProject as z -> ORMProject(name, guid, id, dateTime) :> ORMUniq
                | :? DsSystem as z -> ORMSystem(name, guid, id, dateTime)
                | :? DsFlow   as z -> ORMFlow  (name, guid, id, pid, dateTime)
                | :? DsWork   as z ->
                    let flowId = z.OptFlowGuid |-> (fun flowGuid -> guidDic[flowGuid].Id) |? Nullable<Id>()
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

