namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS

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

        // { Flow 하부 정의 용
        let [<Literal>] Button       = "button"
        let [<Literal>] Lamp         = "lamp"
        let [<Literal>] Condition    = "condition"
        let [<Literal>] Action       = "action"
        // } Flow 하부 정의 용

        // { n : m 관계의 table mapping
        let [<Literal>] MapProject2System = "mapProject2System"
        let [<Literal>] MapCall2ApiCall   = "mapCall2ApiCall"
        // } n : m 관계의 table mapping

        // -- Work 가 가진 flowId 로 충분!!
        //let [<Literal>] MapFlow2Work      = "mapFlow2Work"

        let [<Literal>] Enum         = "enum"


        let [<Literal>] Meta         = "meta"
        let [<Literal>] Log          = "log"
        let [<Literal>] TableHistory = "tableHistory"
        let [<Literal>] EOT          = "endOfTable"

        let AllTableNames = [
            Project; System; Flow; Work; Call; ArrowWork; ArrowCall; ApiCall; ApiDef; ParamWork; ParamCall;
            Button; Lamp; Condition; Action; Enum;
            Meta; TableHistory; MapProject2System; MapCall2ApiCall; ]        // Log;

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
    , [dateTime]      DATETIME(7)"""

    let sqlUniqWithName() = sqlUniq() + $"""
    , [name]          NVARCHAR({NameLength}) NOT NULL"""

    let private getSqlCreateSchemaHelper(withTrigger:bool) =
        $"""
BEGIN TRANSACTION;


CREATE TABLE [{Tn.Project}]( {sqlUniqWithName()}
    , [author]       TEXT NOT NULL
    , [version]      TEXT NOT NULL
    , [description]  TEXT
    , CONSTRAINT {Tn.Project}_uniq UNIQUE (name)    -- Project 의 이름은 유일해야 함
);

CREATE TABLE [{Tn.System}]( {sqlUniqWithName()}
    , [prototype]     TINYINT NOT NULL DEFAULT 0  -- 프로토타입 시스템 여부.  0: 일반 시스템, 1: 프로토타입 시스템
    , [author]        TEXT NOT NULL
    , [langVersion]   TEXT NOT NULL
    , [engineVersion] TEXT NOT NULL
    , [originGuid]    TEXT      -- 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null.  FOREIGN KEY 설정 안함.  db 에 원본삭제시 null 할당 가능
    , [description]   TEXT
);


CREATE TABLE [{Tn.MapProject2System}]( {sqlUniq()}
    , [projectId]      {intKeyType} NOT NULL
    , [systemId]       {intKeyType} NOT NULL
    , [isActive]       TINYINT NOT NULL DEFAULT 0
    , FOREIGN KEY(projectId)   REFERENCES {Tn.Project}(id) ON DELETE CASCADE
    , FOREIGN KEY(systemId)    REFERENCES {Tn.System}(id) ON DELETE CASCADE
    , CONSTRAINT {Tn.MapProject2System}_uniq UNIQUE (projectId, systemId)
);

-- Call 은 여러개의 Api 를 동시에 호출할 수 있다.
CREATE TABLE [{Tn.MapCall2ApiCall}]( {sqlUniq()}
    , [callId]     {intKeyType} NOT NULL
    , [apiCallId]  {intKeyType} NOT NULL
    , FOREIGN KEY(callId)     REFERENCES {Tn.Call}(id) ON DELETE CASCADE
    , FOREIGN KEY(apiCallId)  REFERENCES {Tn.ApiCall}(id) -- DO *NOT* DELETE CASCADE
    , CONSTRAINT {Tn.MapCall2ApiCall}_uniq UNIQUE (callId, apiCallId)
);


CREATE TABLE [{Tn.Flow}]( {sqlUniqWithName()}
    , [systemId]      {intKeyType} NOT NULL
    , FOREIGN KEY(systemId)   REFERENCES {Tn.System}(id) ON DELETE CASCADE
);


    CREATE TABLE [{Tn.Button}]( {sqlUniqWithName()}
        , [flowId]        {intKeyType} NOT NULL
        , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE
    );

    CREATE TABLE [{Tn.Lamp}]( {sqlUniqWithName()}
        , [flowId]        {intKeyType} NOT NULL
        , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE
    );

    CREATE TABLE [{Tn.Condition}]( {sqlUniqWithName()}
        , [flowId]        {intKeyType} NOT NULL
        , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE
    );

    CREATE TABLE [{Tn.Action}]( {sqlUniqWithName()}
        , [flowId]        {intKeyType} NOT NULL
        , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE
    );

CREATE TABLE [{Tn.Enum}](
    [id]              {intKeyType} PRIMARY KEY AUTOINCREMENT NOT NULL
    , [category]      NVARCHAR({NameLength}) NOT NULL
    , [name]          NVARCHAR({NameLength}) NOT NULL
    , [value]         INT NOT NULL
    , CONSTRAINT {Tn.Enum}_uniq UNIQUE (name, category)
);


CREATE TABLE [{Tn.Work}]( {sqlUniqWithName()}
    , [systemId]      {intKeyType} NOT NULL
    , [flowId]      {intKeyType} DEFAULT NULL    -- NULL 허용 (work가 flow에 속하지 않을 수도 있음)
    , FOREIGN KEY(systemId) REFERENCES {Tn.System}(id) ON DELETE CASCADE
    , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE      -- Flow 삭제시 work 삭제, flowId 는 null 허용
);

CREATE TABLE [{Tn.Call}]( {sqlUniqWithName()}
    , [callTypeId]    {intKeyType} -- NOT NULL         -- 호출 유형: e.g "Normal", "Parallel", "Repeat"
    , [timeout]       INT   -- ms
    , [autoPre]       TEXT
    , [safety]        TEXT
    , [workId]        {intKeyType} NOT NULL
    -- , [apiCallId]     {intKeyType} NOT NULL
    , FOREIGN KEY(workId)    REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Call 도 삭제
    , FOREIGN KEY(callTypeId)   REFERENCES {Tn.Enum}(id)
    -- , FOREIGN KEY(apiCallId) REFERENCES {Tn.ApiCall}(id)
);

-- Work 간 연결.  System 에 속함
CREATE TABLE [{Tn.ArrowWork}]( {sqlUniq()}
    , [source]        {intKeyType} NOT NULL
    , [target]        {intKeyType} NOT NULL
    , [typeId]        {intKeyType} NOT NULL         -- arrow type : "Start", "Reset", ??

    , [systemId]      {intKeyType} NOT NULL
    , FOREIGN KEY(source)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(typeId)   REFERENCES {Tn.Enum}(id)
    , FOREIGN KEY(systemId) REFERENCES {Tn.System}(id) ON DELETE CASCADE    -- System 삭제시 Arrow 도 삭제
);

-- Call 간 연결.  Work 에 속함
CREATE TABLE [{Tn.ArrowCall}]( {sqlUniq()}
    , [source]        {intKeyType} NOT NULL
    , [target]        {intKeyType} NOT NULL
    , [typeId]        {intKeyType} NOT NULL         -- arrow type : "Start", "Reset", ??

    , [workId]        {intKeyType} NOT NULL
    , FOREIGN KEY(source)   REFERENCES {Tn.Call}(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES {Tn.Call}(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(typeId)   REFERENCES {Tn.Enum}(id)
    , FOREIGN KEY(workId)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
);

--
-- Work > Call > ApiCall > ApiDef
--

CREATE TABLE [{Tn.ApiCall}]( {sqlUniqWithName()}
    , [systemId]        {intKeyType} NOT NULL
    , [inAddress]       TEXT NOT NULL
    , [outAddress]      TEXT NOT NULL
    , [inSymbol]        TEXT NOT NULL
    , [outSymbol]       TEXT NOT NULL

    -- Value 에 대해서는 Database column 에 욱여넣기 힘듦.  문자열 규약이 필요.  e.g. "1.0", "(1, 10)", "(, 3.14)", "[5, 10)",
    , [value]           TEXT NOT NULL   -- 값 범위 또는 단일 값 조건 정의 (선택 사항).  ValueParam type
    , [valueTypeId]     {intKeyType} NOT NULL         -- (e.g. "string", "int", "float", "bool", "dateTime",
    , [apiDefId]        {intKeyType} NOT NULL
    , FOREIGN KEY(systemId)   REFERENCES {Tn.System}(id) ON DELETE CASCADE      -- Call 삭제시 ApiCall 도 삭제
    , FOREIGN KEY(valueTypeId)   REFERENCES {Tn.Enum}(id)
);

CREATE TABLE [{Tn.ApiDef}]( {sqlUniqWithName()}
    , [isPush]          TINYINT NOT NULL DEFAULT 0
    , [systemId]        {intKeyType} NOT NULL       -- API 가 정의된 target system
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

    open System.Data
    open Dapper

    /// enum 의 값을 DB 에 넣는다.  enum 의 이름과 값은 DB 에서 유일해야 함.  see tryFindEnumValueId also
    let insertEnumValues<'TEnum when 'TEnum : enum<int>> (conn: IDbConnection) =
        let enumType = typeof<'TEnum>
        let category = enumType.Name
        let names = Enum.GetNames(enumType)
        let values = Enum.GetValues(enumType) :?> 'TEnum[]

        for i in 0 .. names.Length - 1 do
            let name = names.[i]
            let value = Convert.ToInt32(values.[i])
            let sql = """
                INSERT OR IGNORE INTO enum (name, category, value)
                VALUES (@Name, @Category, @Value)
            """
            conn.Execute(sql, dict [
                "Name", box name
                "Category", box category
                "Value", box value
            ]) |> ignore



    /// SQL schema 생성.  trigger 도 함께 생성하려면 getSqlCreateSchemaWithTrigger() 사용
    let getSqlCreateSchema() = getSqlCreateSchemaHelper false
    let getSqlCreateSchemaWithTrigger() = getSqlCreateSchemaHelper true


