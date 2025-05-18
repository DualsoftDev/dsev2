namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS
open Dual.Common.Db.FS

[<AutoOpen>]
module DatabaseSchemaModule =

    /// NVARCHAR({NameLength}) 는 TEXT 와 완전 동일하며, 혹시 다른 DBMS 를 사용할 경우의 호환성을 위한 것.
    let [<Literal>] NameLength = 128

    #if DEBUG   // TODO : 제거 요망
    /// UNIQUE indexing 여부 성능 고려해서 판단 필요
    let [<Literal>] guidUniqSpec = "UNIQUE"
    #else
    let [<Literal>] guidUniqSpec = ""
    #endif


    module Tn =
        let [<Literal>] System       = "system"
        let [<Literal>] Flow         = "flow"
        let [<Literal>] Work         = "work"
        let [<Literal>] Call         = "call"
        let [<Literal>] ApiCall      = "apiCall"
        let [<Literal>] ApiDef       = "apiDef"

        let [<Literal>] ParamWork    = "paramWork"
        let [<Literal>] ParamCall    = "paramCall"

        let [<Literal>] Meta         = "meta"
        let [<Literal>] Log          = "log"
        let [<Literal>] TableHistory = "tableHistory"

        let AllTableNames = [ System; Flow; Work; Call; ApiCall; ApiDef; ParamWork; ParamCall; Meta; TableHistory; ]        // Log;

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
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL {guidUniqSpec}   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
"""

    let sqlUniqWithName() = sqlUniq() + $"""
    , [name]          NVARCHAR({NameLength}) NOT NULL
"""

    let sqlCreateSchema =
        $"""
BEGIN TRANSACTION;


CREATE TABLE [{Tn.System}]( {sqlUniqWithName()}
);

CREATE TABLE [{Tn.Flow}]( {sqlUniqWithName()}
    , [systemId]      INTEGER NOT NULL
    , FOREIGN KEY(systemId)   REFERENCES {Tn.System}(id) ON DELETE CASCADE
);

CREATE TABLE [{Tn.Work}]( {sqlUniqWithName()}

    , [systemId]      INTEGER NOT NULL
    , [flowId]      INTEGER DEFAULT NULL    -- NULL 허용 (work가 flow에 속하지 않을 수도 있음)

    , FOREIGN KEY(systemId) REFERENCES {Tn.System}(id) ON DELETE CASCADE
    , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE      -- Flow 삭제시 work 삭제, flowId 는 null 허용
);

CREATE TABLE [{Tn.Call}]( {sqlUniqWithName()}
    , [workId]        INTEGER NOT NULL
    , FOREIGN KEY(workId)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Call 도 삭제
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
    id INTEGER PRIMARY KEY NOT NULL,
    key TEXT NOT NULL,
    val TEXT NOT NULL
);


-- tableHistory 테이블
CREATE TABLE [{Tn.TableHistory}] (
    id INTEGER PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    operation TEXT,
    oldId INTEGER,
    newId INTEGER,
    CONSTRAINT {Tn.TableHistory}_uniq UNIQUE (name, operation, oldId, newId)
);



{triggerSql()}


INSERT INTO [{Tn.Meta}] (key, val) VALUES ('Version', '1.0.0.0');
DELETE FROM {Tn.TableHistory};

COMMIT;
"""


    type RowId = int
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
    type ORMUniq(id:int, name:string, guid:Nullable<Guid>) =
        interface IORMRow
        new() = ORMUniq(-1, null, Nullable())
        member val Id = id with get, set
        member val Name = name with get, set
        member val Guid = guid with get, set

    /// Object Releation Mapper for Asset
    type ORMSystem(id, name, guid) =
        inherit ORMUniq(id, name, guid)
        interface IORMSystem
        new() = ORMSystem(-1, null, Nullable())

    type ORMFlow(id, name, guid, systemId:int) =
        inherit ORMUniq(id, name, guid)
        interface IORMFlow
        new() = ORMFlow(-1, null, Nullable(), -1)
        member val SystemId = systemId with get, set

    type ORMWork(id, name, guid, flowId:Nullable<int>) =
        inherit ORMUniq(id, name, guid)
        interface IORMWork
        new() = ORMWork(-1, null, Nullable(), Nullable())
        member val FlowId = flowId with get, set

    type ORMCall(id, name, guid, workId:int) =
        inherit ORMUniq(id, name, guid)
        interface IORMCall
        new() = ORMCall(-1, null, Nullable(), -1)
        member val WorkId = workId with get, set

    type ORMApiCall(id, name, guid, workId:int) =
        inherit ORMUniq(id, name, guid)
        interface IORMApiCall
        new() = ORMApiCall(-1, null, Nullable(), -1)
        member val WorkId = workId with get, set

    type ORMApiDef(id, name, guid, workId:int) =
        inherit ORMUniq(id, name, guid)
        interface IORMApiDef
        new() = ORMApiDef(-1, null, Nullable(), -1)
        member val WorkId = workId with get, set




