namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS
open Dual.Common.Db.FS

[<AutoOpen>]
module DatabaseSchemaModule =

    /// {varchar NameLength} 는 TEXT 와 완전 동일하며, 혹시 다른 DBMS 를 사용할 경우의 호환성을 위한 것.
    let [<Literal>] NameLength = 128

    //#if DEBUG   // TODO : 제거 요망
    ///// UNIQUE indexing 여부 성능 고려해서 판단 필요
    //let [<Literal>] guidUniqSpec = "UNIQUE"
    //let [<Literal>] intKeyType = "INTEGER"      // or "INTEGER"
    //#else
    //let [<Literal>] guidUniqSpec = ""
    //#endif


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
        let [<Literal>] Temp         = "temp"
        let [<Literal>] Log          = "log"
        let [<Literal>] TableHistory = "tableHistory"
        let [<Literal>] EOT          = "endOfTable"

        let AllTableNames = [
            Project; System; Flow; Work; Call; ArrowWork; ArrowCall; ApiCall; ApiDef; ParamWork; ParamCall;
            Button; Lamp; Condition; Action; Enum;
            Meta; Temp; TableHistory; MapProject2System; MapCall2ApiCall; ]        // Log;

    // database view names
    module Vn =
        let [<Literal>] MapProject2System = "vwMapProject2System"
        let [<Literal>] MapCall2ApiCall   = "vwMapCall2ApiCall"
        let [<Literal>] System     = "vwSystem"
        let [<Literal>] Flow       = "vwFlow"
        let [<Literal>] Work       = "vwWork"
        let [<Literal>] Call       = "vwCall"
        let [<Literal>] ArrowCall  = "vwArrowCall"
        let [<Literal>] ArrowWork  = "vwArrowWork"
        let [<Literal>] ApiDef     = "vwApiDef"
        let [<Literal>] ApiCall    = "vwApiCall"
        let AllViewTableNames = [
                MapProject2System; MapCall2ApiCall
                System; Flow; Work; Call; ApiDef; ApiCall
                ArrowCall; ArrowWork; ]

    /// SQL schema 생성.  trigger 도 함께 생성하려면 getSqlCreateSchemaWithTrigger() 사용
    let getSqlCreateSchema (dbProvider: DbProvider) (withTrigger: bool) =

        let intKeyType = dbProvider.SqlIntKeyType

        let guidUniqSpec = "UNIQUE"

        let boolean = dbProvider.SqlBoolean

        let varchar (n: int) = $"{dbProvider.SqlVarChar}({n})"
        let autoincPrimaryKey = dbProvider.SqlAutoincPrimaryKey

        let datetime = $"{dbProvider.SqlDateTime}(7)"

        let triggerSql (dbProvider: DbProvider) =
            let createTrigger (op: string) (tableName: string) =
                let updateSql =
                    match op with
                    | "INSERT" ->
                        $"""
                        INSERT INTO {Tn.TableHistory} (name, operation, oldId, newId)
                        SELECT '{tableName}', '{op}', NULL, NEW.id
                        WHERE NOT EXISTS (
                            SELECT 1 FROM {Tn.TableHistory} WHERE newId = NEW.id AND name = '{tableName}' AND operation = '{op}'
                        );
                        """
                    | "UPDATE" ->
                        $"""
                        INSERT INTO {Tn.TableHistory} (name, operation, oldId, newId)
                        SELECT '{tableName}', '{op}', OLD.id, NEW.id
                        WHERE NOT EXISTS (
                            SELECT 1 FROM {Tn.TableHistory} WHERE name = '{tableName}' AND operation = '{op}' AND oldId = OLD.id AND newId = NEW.id
                        );
                        """
                    | "DELETE" ->
                        $"""
                        INSERT INTO {Tn.TableHistory} (name, operation, oldId, newId)
                        VALUES ('{tableName}', '{op}', OLD.id, NULL);
                        """
                    | _ -> failwith $"Invalid op: {op}"

                match dbProvider with
                | Sqlite _ ->
                    $"""
                    DROP TRIGGER IF EXISTS trigger_{tableName}_{op};
                    CREATE TRIGGER trigger_{tableName}_{op} AFTER {op} ON {tableName}
                    BEGIN
                        {updateSql}
                    END;
                    """
                | Postgres _ ->
                    $"""
                    DROP TRIGGER IF EXISTS trigger_{tableName}_{op} ON "{tableName}";
                    CREATE OR REPLACE FUNCTION trigger_fn_{tableName}_{op}() RETURNS trigger AS $$
                    BEGIN
                        {updateSql}
                        RETURN NEW;
                    END;
                    $$ LANGUAGE plpgsql;

                    CREATE TRIGGER trigger_{tableName}_{op}
                    AFTER {op} ON "{tableName}"
                    FOR EACH ROW EXECUTE FUNCTION trigger_fn_{tableName}_{op}();
                    """

            let historyTargetTables = Tn.AllTableNames |> except [Tn.TableHistory]
            [ for t in historyTargetTables do
                for op in ["INSERT"; "UPDATE"; "DELETE"] do
                    createTrigger op t ]
            |> String.concat "\n"

        let sqlUniq () = $"""
        [id]              {autoincPrimaryKey}
        , [guid]          TEXT NOT NULL {guidUniqSpec}   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
        , [dateTime]      {datetime}"""

        let sqlUniqWithName () = sqlUniq() + $"""
        , [name]          {varchar NameLength} NOT NULL"""

        $"""
BEGIN TRANSACTION;


CREATE TABLE [{Tn.Project}]( {sqlUniqWithName()}
    , [author]       TEXT NOT NULL
    , [version]      TEXT NOT NULL
    , [description]  TEXT
    , CONSTRAINT {Tn.Project}_uniq UNIQUE (name)    -- Project 의 이름은 유일해야 함
);

CREATE TABLE [{Tn.System}]( {sqlUniqWithName()}
    , [prototypeId]   {intKeyType}                  -- 프로토타입의 Guid.  prototype 으로 만든 instance 는 prototype 의 Guid 를 갖고, prototype 자체는 NULL 을 갖는다.
    , [author]        TEXT NOT NULL
    , [langVersion]   TEXT NOT NULL
    , [engineVersion] TEXT NOT NULL
    , [originGuid]    TEXT      -- 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null.  FOREIGN KEY 설정 안함.  db 에 원본삭제시 null 할당 가능
    , [description]   TEXT
    , FOREIGN KEY(prototypeId) REFERENCES {Tn.System}(id) ON DELETE SET NULL     -- prototype 삭제시, instance 의 prototype 참조만 삭제
);


CREATE TABLE [{Tn.MapProject2System}]( {sqlUniq()}
    , [projectId]      {intKeyType} NOT NULL
    , [systemId]       {intKeyType} NOT NULL
    , [isActive]       {boolean} NOT NULL DEFAULT 0
    , [loadedName]     TEXT
    , FOREIGN KEY(projectId)   REFERENCES {Tn.Project}(id) ON DELETE CASCADE
    , FOREIGN KEY(systemId)    REFERENCES {Tn.System}(id) ON DELETE CASCADE     -- NO ACTION       -- ON DELETE RESTRICT    -- RESTRICT: 부모 레코드가 삭제되기 전에 참조되고 있는 자식 레코드가 있는지 즉시 검사하고, 있으면 삭제를 막음.
    , CONSTRAINT {Tn.MapProject2System}_uniq UNIQUE (projectId, systemId)
);

-- TODO: MapProject2System row 하나 삭제시,
--    다른 project 에서 참조되고 있지 않은 systemId 에 해당하는 system 들을 삭제할 수 있도록 trigger 설정 필요

CREATE TRIGGER IF NOT EXISTS trigger_{Tn.Project}_beforeDelete_recordSystemIds
BEFORE DELETE ON {Tn.Project}
BEGIN
    DELETE FROM {Tn.Temp} WHERE key = 'trigger_temp_systemId';

    INSERT INTO {Tn.Temp} (key, val)
    SELECT 'trigger_temp_systemId', systemId
    FROM {Tn.MapProject2System}
    WHERE projectId = OLD.id;
END;

CREATE TRIGGER IF NOT EXISTS trigger_{Tn.Project}_afterDelete_dropSystems
AFTER DELETE ON {Tn.Project}
BEGIN
    DELETE FROM {Tn.System}
    WHERE id IN (
        SELECT CAST(val AS INTEGER) FROM {Tn.Temp} WHERE key = 'trigger_temp_systemId'
    )
    AND NOT EXISTS (
        SELECT 1 FROM {Tn.MapProject2System}
        WHERE systemId = {Tn.System}.id
    );

    DELETE FROM meta WHERE key = 'trigger_temp_systemId';
END;



--CREATE TRIGGER IF NOT EXISTS trigger_{Tn.MapProject2System}_afterDelete_dropSystems
--AFTER DELETE ON {Tn.MapProject2System}
--BEGIN
--    -- 디버깅용 로그 삽입
--    INSERT INTO {Tn.Temp}(key, val)
--    VALUES (
--        'trigger_{Tn.MapProject2System}_afterDelete_dropSystems',
--        '삭제된 systemId=' || OLD.systemId
--    );
--
--    -- system 삭제 시도
--    DELETE FROM {Tn.System}
--    WHERE id = OLD.systemId
--      AND NOT EXISTS (
--          SELECT 1 FROM {Tn.MapProject2System}
--          WHERE systemId = OLD.systemId
--      );
--END;



-- Call 은 여러개의 Api 를 동시에 호출할 수 있다.
CREATE TABLE [{Tn.MapCall2ApiCall}]( {sqlUniq()}
    , [callId]     {intKeyType} NOT NULL
    , [apiCallId]  {intKeyType} NOT NULL
    , FOREIGN KEY(callId)     REFERENCES {Tn.Call}(id) ON DELETE CASCADE
    , FOREIGN KEY(apiCallId)  REFERENCES {Tn.ApiCall}(id) ON DELETE CASCADE
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
    [id]              {autoincPrimaryKey}
    , [category]      {varchar NameLength} NOT NULL
    , [name]          {varchar NameLength} NOT NULL
    , [value]         INT NOT NULL
    , CONSTRAINT {Tn.Enum}_uniq UNIQUE (name, category)
);


CREATE TABLE [{Tn.Work}]( {sqlUniqWithName()}
    , [systemId]    {intKeyType} NOT NULL
    , [flowId]      {intKeyType} DEFAULT NULL    -- NULL 허용 (work가 flow에 속하지 않을 수도 있음)
    , [status4Id]   {intKeyType} DEFAULT NULL
    , FOREIGN KEY(systemId)  REFERENCES {Tn.System}(id) ON DELETE CASCADE
    , FOREIGN KEY(flowId)    REFERENCES {Tn.Flow}(id) ON DELETE CASCADE      -- Flow 삭제시 work 삭제, flowId 는 null 허용
    , FOREIGN KEY(status4Id) REFERENCES {Tn.Enum}(id) ON DELETE SET NULL
);

CREATE TABLE [{Tn.Call}]( {sqlUniqWithName()}
    , [callTypeId]    {intKeyType} -- NOT NULL         -- 호출 유형: e.g "Normal", "Parallel", "Repeat"
    , [status4Id]     {intKeyType} DEFAULT NULL
    , [timeout]       INT   -- ms
    , [autoPre]       TEXT
    , [safety]        TEXT
    , [isDisabled]    {boolean} NOT NULL DEFAULT 0   -- 0: 활성화, 1: 비활성화
    , [workId]        {intKeyType} NOT NULL
    , FOREIGN KEY(workId)     REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Call 도 삭제
    , FOREIGN KEY(callTypeId) REFERENCES {Tn.Enum}(id) ON DELETE RESTRICT
    , FOREIGN KEY(status4Id) REFERENCES {Tn.Enum}(id) ON DELETE SET NULL
    -- , [apiCallId]     {intKeyType} NOT NULL  -- call 이 복수개의 apiCall 을 가지므로, {Tn.MapCall2ApiCall} 에 저장
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
    , FOREIGN KEY(typeId)   REFERENCES {Tn.Enum}(id) ON DELETE RESTRICT
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
    , FOREIGN KEY(typeId)   REFERENCES {Tn.Enum}(id) ON DELETE RESTRICT
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
    , [value1]          TEXT -- 값
    , [value2]          TEXT -- 값
    , [valueTypeId]     {intKeyType} NOT NULL         -- (e.g. "string", "int", "float", "bool", "dateTime",
    , [rangeTypeId]     {intKeyType} NOT NULL         -- (e.g. "Single", "min_max", ...
    , [apiDefId]        {intKeyType} NOT NULL
    , FOREIGN KEY(systemId)    REFERENCES {Tn.System}(id) ON DELETE CASCADE      -- Call 삭제시 ApiCall 도 삭제
    , FOREIGN KEY(valueTypeId) REFERENCES {Tn.Enum}(id)
    , FOREIGN KEY(rangeTypeId) REFERENCES {Tn.Enum}(id)
);

CREATE TABLE [{Tn.ApiDef}]( {sqlUniqWithName()}
    , [isPush]          {boolean} NOT NULL DEFAULT 0
    , [systemId]        {intKeyType} NOT NULL       -- API 가 정의된 target system
    , FOREIGN KEY(systemId)   REFERENCES {Tn.System}(id) ON DELETE CASCADE
);


-- 삭제 ??
CREATE TABLE [{Tn.ParamWork}] (  {sqlUniq()}
);

-- 삭제 ??
CREATE TABLE [{Tn.ParamCall}] (  {sqlUniq()}
);


CREATE TABLE [{Tn.Meta}] (
    id {intKeyType} PRIMARY KEY NOT NULL,
    key TEXT NOT NULL,
    val TEXT NOT NULL
);

CREATE TABLE [{Tn.Temp}] (
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


CREATE VIEW [{Vn.MapProject2System}] AS
    SELECT
        m.[id]
        , p.[id]    AS projectId
        , p.[name]  AS projectName
        , s.[id]    AS systemId
        , s.[name]  AS systemName
        , s2.[id]   AS prototypeId
        , s2.[name] AS prototypeName
        , m.[loadedName]
        , m.[isActive]
    FROM [{Tn.MapProject2System}] m
    JOIN [{Tn.Project}] p ON p.id = m.projectId
    JOIN [{Tn.System}]  s ON s.id = m.systemId
    LEFT JOIN [{Tn.System}]  s2 ON s2.id = s.prototypeId
    ;


CREATE VIEW [{Vn.MapCall2ApiCall}] AS
    SELECT
        m.[id]
        , p.[id]    AS projectId
        , p.[name]  AS projectName
        , s.[id]    AS systemId
        , s.[name]  AS systemName
        , psm.[loadedName]
        , w.[id]    AS workId
        , w.[name]  AS workName
        , c.[id]    AS callId
        , c.[name]  AS callName
        , ac.[id]   AS apiCallId
        , ac.[name] AS apiCallName
        , ad.[id]   AS apiDefId
        , ad.[name] AS apiDefName
    FROM [{Tn.MapCall2ApiCall}] m
    JOIN [{Tn.Call}] c                ON c.Id         = m.callId
    JOIN [{Tn.ApiCall}] ac            ON ac.Id        = m.apiCallId
    JOIN [{Tn.ApiDef}] ad             ON ad.Id        = ac.apiDefId
    JOIN [{Tn.Work}] w                ON w.Id         = c.workId
    JOIN [{Tn.System}] s              ON s.id         = w.systemId
    JOIN [{Tn.MapProject2System}] psm ON psm.systemId = s.id
    JOIN [{Tn.Project}] p             ON p.id         = psm.projectId
    ;

CREATE VIEW [{Vn.System}] AS
    SELECT
        s.[id]
        , s.[name]  AS systemName
        , psm.[loadedName]
        , p.[id]    AS projectId
        , p.[name]  AS projectName
    FROM [{Tn.MapProject2System}] psm
    JOIN [{Tn.System}] s ON psm.systemId = s.id
    JOIN [{Tn.Project}] p ON p.id = psm.projectId
    ;

CREATE VIEW [{Vn.ApiDef}] AS
    SELECT
        x.[id]
        , x.[name]
        , x.[isPush]
        , s.[id]    AS systemId
        , s.[name]  AS systemName
    FROM [{Tn.ApiDef}] x
    JOIN [{Tn.System}] s  ON s.id = x.systemId
    ;

CREATE VIEW [{Vn.ApiCall}] AS
    SELECT
        x.[id]
        , x.[name]
        , x.[inAddress]
        , x.[outAddress]
        , x.[inSymbol]
        , x.[outSymbol]
        , x.[value1]
        , x.[value2]
        , enumV.[name] AS valueType
        , enumR.[name] AS rangeType
        , ad.[id]   AS apiDefId
        , ad.[name] AS apiDefName
        , s.[id]    AS systemId
        , s.[name]  AS systemName
    FROM [{Tn.ApiCall}] x
    JOIN [{Tn.ApiDef}] ad ON ad.id = x.apiDefId
    JOIN [{Tn.System}] s  ON s.id = ad.systemId
    JOIN [{Tn.Enum}] enumV ON enumV.id = x.valueTypeId
    JOIN [{Tn.Enum}] enumR ON enumR.id = x.rangeTypeId
    ;


CREATE VIEW [{Vn.Flow}] AS
    SELECT
        x.[id]
        , x.[name]  AS flowName
        , p.[id]    AS projectId
        , p.[name]  AS projectName
        , s.[id]    AS systemId
        , s.[name]  AS systemName
        , w.[id]    AS workId
        , w.[name]  AS workName
    FROM [{Tn.Work}] w
    LEFT JOIN [{Tn.Flow}] x           ON x.id         = w.flowId
    JOIN [{Tn.System}] s              ON s.id         = w.systemId
    JOIN [{Tn.MapProject2System}] psm ON psm.systemId = s.id
    JOIN [{Tn.Project}] p             ON p.id         = psm.projectId
    ;


CREATE VIEW [{Vn.Work}] AS
    SELECT
        x.[id]
        , x.[name]  AS workName
        , e.[name]  AS status4
        , p.[id]    AS projectId
        , p.[name]  AS projectName
        , s.[id]    AS systemId
        , s.[name]  AS systemName
        , f.[id]    AS flowId
        , f.[name]  AS flowName
    FROM [{Tn.Work}] x
    JOIN [{Tn.System}] s ON s.id = x.systemId
    JOIN [{Tn.MapProject2System}] psm ON psm.systemId = s.id
    JOIN [{Tn.Project}] p ON p.id = psm.projectId
    LEFT JOIN [{Tn.Flow}] f ON f.id = x.flowId
    LEFT JOIN [{Tn.Enum}] e ON e.id = x.status4Id
    ;

CREATE VIEW [{Vn.Call}] AS
    SELECT
        c.[id]
        , c.[name]  AS callName
        , e.[name]  AS status4
        , c.[timeout]
        , c.[autoPre]
        , c.[safety]
        , c.[isDisabled]
        , p.[id]      AS projectId
        , p.[name]  AS projectName
        , s.[id]    AS systemId
        , s.[name]  AS systemName
        , w.[id]    AS workId
        , w.[name]  AS workName
    FROM [{Tn.Call}] c
    JOIN [{Tn.Work}] w                ON w.Id         = c.workId
    JOIN [{Tn.System}] s              ON s.id         = w.systemId
    JOIN [{Tn.MapProject2System}] psm ON psm.systemId = s.id
    JOIN [{Tn.Project}] p             ON p.id         = psm.projectId
    LEFT JOIN [{Tn.Enum}] e           ON e.id         = c.status4Id
    ;



CREATE VIEW [{Vn.ArrowCall}] AS
    SELECT
        ac.[id]
        , ac.[source]
        , src.[name] AS sourceName
        , ac.[target]
        , tgt.[name] AS targetName
        , ac.[typeId]
        , enum.[name] AS enumName
        , ac.[workId]
        , w.[name] AS workName
        , p.[id]    AS projectId
        , p.[name]  AS projectName
        , s.[id]    AS systemId
        , s.[name]  AS systemName
    FROM [{Tn.ArrowCall}] ac
    JOIN [{Tn.Call}] src ON src.Id = ac.source
    JOIN [{Tn.Call}] tgt ON tgt.Id = ac.target
    JOIN [{Tn.Work}] w ON w.Id = ac.workId
    JOIN [{Tn.System}] s ON s.id = w.systemId
    JOIN [{Tn.MapProject2System}] psm ON psm.systemId = s.id
    JOIN [{Tn.Project}] p ON p.id = psm.projectId
    LEFT JOIN [{Tn.Enum}] enum ON ac.typeId = enum.id
    ;


CREATE VIEW [{Vn.ArrowWork}] AS
    SELECT
        aw.[id]
        , aw.[source]
        , src.[name]      AS sourceName
        , aw.[target]
        , tgt.[name]      AS targetName
        , aw.[typeId]
        , enum.[name]     AS enumName
        , aw.[systemId]
        , p.[id]          AS projectId
        , p.[name]        AS projectName
        , s.[id]          AS systemId
        , s.[name]        AS systemName
    FROM [{Tn.ArrowWork}] aw
    JOIN [{Tn.Work}] src ON src.Id = aw.source
    JOIN [{Tn.Work}] tgt ON tgt.Id = aw.target
    JOIN [{Tn.System}] s ON s.id = src.systemId
    JOIN [{Tn.MapProject2System}] psm ON psm.systemId = s.id
    JOIN [{Tn.Project}] p  ON p.id = psm.projectId
    LEFT JOIN [{Tn.Enum}] enum ON aw.typeId = enum.id
    ;



INSERT INTO [Meta] (key, val) VALUES ('Version', '1.0.0.0');
DELETE FROM TableHistory;
{ if withTrigger then triggerSql dbProvider else "" }

CREATE TABLE [{Tn.EOT}] (
    id {intKeyType} PRIMARY KEY NOT NULL
);

COMMIT; """



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





