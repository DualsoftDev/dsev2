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
        let [<Literal>] TypeTest     = "typeTest"
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

        let int32 = "INT"
        let boolean    = dbProvider.SqlBoolean
        let now        = dbProvider.SqlNow
        let guid       = dbProvider.SqlGuidKeyType
        let jsonb      = dbProvider.SqlJsonBType
        let sqlConcat  = dbProvider.SqlConcat

        let falseValue = dbProvider.SqlFalse
        let k          = dbProvider.SqlWrapName

        let varchar (n: int) = $"{dbProvider.SqlVarChar}({n})"
        let autoincPrimaryKey = dbProvider.SqlAutoincPrimaryKey

        let datetime = $"{dbProvider.SqlDateTime}(6)"

        //let dropTriggerSql (dbProvider: DbProvider) (triggerName:string) (tableName:string) =
        //    match dbProvider with
        //    | Sqlite _ -> $"DROP TRIGGER IF EXISTS {triggerName};"
        //    | Postgres _ -> $"DROP TRIGGER IF EXISTS {triggerName} ON {tableName};";

        let triggerSql (dbProvider: DbProvider) =
            let allTables = Tn.AllTableNames |> except [Tn.TableHistory]

            [ for t in allTables do
                for op in ["INSERT"; "UPDATE"; "DELETE"] do
                    dbProvider.SqlCreateTrigger(t, op) ]
            |> String.concat "\n"


        let sysIdKeyExpr =
            let sysIdKey = "trigger_temp_systemId"
            match dbProvider with
            | Sqlite _ -> $"'{sysIdKey}_' || OLD.id"
            | Postgres _ -> $"concat('{sysIdKey}_', OLD.id)"


        (* Project 는 개념적으로는 System 을 child 로 가지지만, DB 구조에서는 독립 구성에 mapping table 에 의존하므로,
           ON DELETE CASCADE 를 적용할 수 없다.  따라서 Project 삭제시, mapping table 관계를 고려해서 system 을 삭제한다.
           - 삭제 전에 systemId 를 temp table 에 저장하고, 삭제 후에 temp table 에서 systemId 를 읽어와서
             해당 system 을 삭제한다.
         *)
        /// Project 삭제시, 시스템을 제거하기 위한 정보 저장용 trigger 생성
        let projectTriggerBeforeDelete =
            let name = "trigger_project_beforeDelete_recordSystemIds"
            let body = $"""
        INSERT INTO {Tn.Log} (projectId, message)
        SELECT
            OLD.id,
            'beforeDelete: projectId=' || OLD.id || ', systemIds=' || {sqlConcat "systemId"}
        FROM {Tn.MapProject2System}
        WHERE projectId = OLD.id;

        DELETE FROM {Tn.Temp} WHERE key = {sysIdKeyExpr};

        INSERT INTO {Tn.Temp} (key, val)
        SELECT {sysIdKeyExpr}, systemId
        FROM {Tn.MapProject2System}
        WHERE projectId = OLD.id;
        """
            dbProvider.SqlCreateCustomTrigger(name, "BEFORE", "DELETE", Tn.Project, body)



        /// Project 삭제 후, 저장된 정보를 이용해서 시스템을 제거하는 trigger 생성
        let projectTriggerAfterDelete =
            let name = "trigger_project_afterDelete_dropSystems"
            let body = $"""
        -- 로그 기록
        INSERT INTO {Tn.Log} (projectId, message)
        SELECT
            OLD.id,
            'afterDelete: projectId=' || OLD.id || ', systemIds=' || {sqlConcat "val"}
        FROM {Tn.Temp}
        WHERE key = {sysIdKeyExpr};

        -- 시스템 제거
        DELETE FROM {Tn.System}
        WHERE id IN (
            SELECT CAST(val AS INTEGER) FROM {Tn.Temp} WHERE key = {sysIdKeyExpr}
        )
        AND NOT EXISTS (
            SELECT 1 FROM {Tn.MapProject2System}
            WHERE systemId = {Tn.System}.id
        );

        -- temp 정리
        DELETE FROM {Tn.Temp} WHERE key = {sysIdKeyExpr};
        """
            dbProvider.SqlCreateCustomTrigger(name, "AFTER", "DELETE", Tn.Project, body)


        let sqlUniq () = $"""
    {k "id"}              {autoincPrimaryKey}
    , {k "guid"}          {guid} NOT NULL {guidUniqSpec}   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , {k "parameter"}     {jsonb}
    , {k "dateTime"}      {datetime}"""

        let sqlUniqWithName () = sqlUniq() + $"""
    , {k "name"}          {varchar NameLength} NOT NULL"""




        let sqlTables = $"""
CREATE TABLE {k Tn.Project}( {sqlUniqWithName()}
    , {k "author"}       TEXT NOT NULL
    , {k "version"}      TEXT NOT NULL
    , {k "description"}  TEXT
    , CONSTRAINT {Tn.Project}_uniq UNIQUE (name)    -- Project 의 이름은 유일해야 함
);

CREATE TABLE {k Tn.System}( {sqlUniqWithName()}
    , {k "prototypeId"}   {intKeyType}    -- 프로토타입의 Guid.  prototype 으로 만든 instance 는 prototype 의 Guid 를 갖고, prototype 자체는 NULL 을 갖는다.
    , {k "iri"}           TEXT            -- Internationalized Resource Identifier.  e.g. "http://example.com/system/12345"  -- System 의 이름은 유일해야 함
    , {k "author"}        TEXT NOT NULL
    , {k "langVersion"}   TEXT NOT NULL   -- System.Version 형식의 문자열.  e.g. "1.0.0"  -- System 의 언어 버전
    , {k "engineVersion"} TEXT NOT NULL
    , {k "originGuid"}    TEXT            -- 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null.  FOREIGN KEY 설정 안함.  db 에 원본삭제시 null 할당 가능
    , {k "description"}   TEXT
    , FOREIGN KEY(prototypeId) REFERENCES {Tn.System}(id) ON DELETE SET NULL     -- prototype 삭제시, instance 의 prototype 참조만 삭제
    , CONSTRAINT {Tn.System}_uniq UNIQUE (iri)
);


CREATE TABLE {k Tn.MapProject2System}( {sqlUniq()}
    , {k "projectId"}      {intKeyType} NOT NULL
    , {k "systemId"}       {intKeyType} NOT NULL
    , {k "isActive"}       {boolean} NOT NULL DEFAULT {falseValue}
    , {k "loadedName"}     TEXT
    , FOREIGN KEY(projectId)   REFERENCES {Tn.Project}(id) ON DELETE CASCADE
    , FOREIGN KEY(systemId)    REFERENCES {Tn.System}(id) ON DELETE CASCADE     -- NO ACTION       -- ON DELETE RESTRICT    -- RESTRICT: 부모 레코드가 삭제되기 전에 참조되고 있는 자식 레코드가 있는지 즉시 검사하고, 있으면 삭제를 막음.
    , CONSTRAINT {Tn.MapProject2System}_uniq UNIQUE (projectId, systemId)
);

-- TODO: MapProject2System row 하나 삭제시,
--    다른 project 에서 참조되고 있지 않은 systemId 에 해당하는 system 들을 삭제할 수 있도록 trigger 설정 필요

{projectTriggerBeforeDelete}

{projectTriggerAfterDelete}

{if withTrigger then triggerSql dbProvider else ""}





--CREATE TRIGGER trigger_{Tn.MapProject2System}_afterDelete_dropSystems
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



CREATE TABLE {k Tn.Flow}( {sqlUniqWithName()}
    , {k "systemId"}      {intKeyType} NOT NULL
    , FOREIGN KEY(systemId)   REFERENCES {Tn.System}(id) ON DELETE CASCADE
);


CREATE TABLE {k Tn.Button}( {sqlUniqWithName()}
    , {k "flowId"}        {intKeyType} NOT NULL
    , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE
);

CREATE TABLE {k Tn.Lamp}( {sqlUniqWithName()}
    , {k "flowId"}        {intKeyType} NOT NULL
    , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE
);

CREATE TABLE {k Tn.Condition}( {sqlUniqWithName()}
    , {k "flowId"}        {intKeyType} NOT NULL
    , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE
);

CREATE TABLE {k Tn.Action}( {sqlUniqWithName()}
    , {k "flowId"}        {intKeyType} NOT NULL
    , FOREIGN KEY(flowId)   REFERENCES {Tn.Flow}(id) ON DELETE CASCADE
);

CREATE TABLE {k Tn.Enum}(
    {k "id"}              {autoincPrimaryKey}
    , {k "category"}      {varchar NameLength} NOT NULL
    , {k "name"}          {varchar NameLength} NOT NULL
    , {k "value"}         INT NOT NULL
    , CONSTRAINT {Tn.Enum}_uniq UNIQUE (name, category)
);


CREATE TABLE {k Tn.Work}( {sqlUniqWithName()}
    , {k "systemId"}    {intKeyType} NOT NULL
    , {k "flowId"}      {intKeyType} DEFAULT NULL    -- NULL 허용 (work가 flow에 속하지 않을 수도 있음)
    , {k "motion"}      TEXT
    , {k "script"}      TEXT
    , {k "isFinished"}  {boolean} NOT NULL DEFAULT {falseValue}
    , {k "numRepeat"}   {int32} NOT NULL DEFAULT 0  -- 반복 횟수
    , {k "period"}      {int32} NOT NULL DEFAULT 0  -- 주기
    , {k "delay"}       {int32} NOT NULL DEFAULT 0  -- 지연
    , {k "status4Id"}   {intKeyType} DEFAULT NULL
    , FOREIGN KEY(systemId)  REFERENCES {Tn.System}(id) ON DELETE CASCADE
    , FOREIGN KEY(flowId)    REFERENCES {Tn.Flow}(id) ON DELETE CASCADE      -- Flow 삭제시 work 삭제, flowId 는 null 허용
    , FOREIGN KEY(status4Id) REFERENCES {Tn.Enum}(id) ON DELETE SET NULL
);


--
-- Work > Call > ApiCall > ApiDef
--

CREATE TABLE {k Tn.ApiCall}( {sqlUniqWithName()}
    , {k "systemId"}        {intKeyType} NOT NULL
    , {k "inAddress"}       TEXT NOT NULL
    , {k "outAddress"}      TEXT NOT NULL
    , {k "inSymbol"}        TEXT NOT NULL
    , {k "outSymbol"}       TEXT NOT NULL

    -- Value 에 대해서는 Database column 에 욱여넣기 힘듦.  문자열 규약이 필요.  e.g. "1.0", "(1, 10)", "(, 3.14)", "[5, 10)",
    , {k "value1"}          TEXT -- 값
    , {k "value2"}          TEXT -- 값
    , {k "valueTypeId"}     {intKeyType} NOT NULL         -- (e.g. "string", "int", "float", "bool", "dateTime",
    , {k "rangeTypeId"}     {intKeyType} NOT NULL         -- (e.g. "Single", "min_max", ...
    , {k "apiDefId"}        {intKeyType} NOT NULL
    , FOREIGN KEY(systemId)    REFERENCES {Tn.System}(id) ON DELETE CASCADE      -- Call 삭제시 ApiCall 도 삭제
    , FOREIGN KEY(valueTypeId) REFERENCES {Tn.Enum}(id)
    , FOREIGN KEY(rangeTypeId) REFERENCES {Tn.Enum}(id)
);

CREATE TABLE {k Tn.ApiDef}( {sqlUniqWithName()}
    , {k "isPush"}          {boolean} NOT NULL DEFAULT {falseValue}
    , {k "systemId"}        {intKeyType} NOT NULL       -- API 가 정의된 target system
    , FOREIGN KEY(systemId)   REFERENCES {Tn.System}(id) ON DELETE CASCADE
);


CREATE TABLE {k Tn.Call}( {sqlUniqWithName()}
    , {k "callTypeId"}    {intKeyType} -- NOT NULL         -- 호출 유형: e.g "Normal", "Parallel", "Repeat"
    , {k "status4Id"}     {intKeyType} DEFAULT NULL
    , {k "timeout"}       INT   -- ms
    , {k "autoPre"}       TEXT
    , {k "safety"}        TEXT
    , {k "isDisabled"}    {boolean} NOT NULL DEFAULT {falseValue}   -- 0: 활성화, 1: 비활성화
    , {k "workId"}        {intKeyType} NOT NULL
    , FOREIGN KEY(workId)     REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Call 도 삭제
    , FOREIGN KEY(callTypeId) REFERENCES {Tn.Enum}(id) ON DELETE RESTRICT
    , FOREIGN KEY(status4Id) REFERENCES {Tn.Enum}(id) ON DELETE SET NULL
    -- , {k "apiCallId"}     {intKeyType} NOT NULL  -- call 이 복수개의 apiCall 을 가지므로, {Tn.MapCall2ApiCall} 에 저장
    -- , FOREIGN KEY(apiCallId) REFERENCES {Tn.ApiCall}(id)
);


-- Call 은 여러개의 Api 를 동시에 호출할 수 있다.
CREATE TABLE {k Tn.MapCall2ApiCall}( {sqlUniq()}
    , {k "callId"}     {intKeyType} NOT NULL
    , {k "apiCallId"}  {intKeyType} NOT NULL
    , FOREIGN KEY(callId)     REFERENCES {Tn.Call}(id) ON DELETE CASCADE
    , FOREIGN KEY(apiCallId)  REFERENCES {Tn.ApiCall}(id) ON DELETE CASCADE
    , CONSTRAINT {Tn.MapCall2ApiCall}_uniq UNIQUE (callId, apiCallId)
);




-- Work 간 연결.  System 에 속함
CREATE TABLE {k Tn.ArrowWork}( {sqlUniq()}
    , {k "source"}        {intKeyType} NOT NULL
    , {k "target"}        {intKeyType} NOT NULL
    , {k "typeId"}        {intKeyType} NOT NULL         -- arrow type : "Start", "Reset", ??

    , {k "systemId"}      {intKeyType} NOT NULL
    , FOREIGN KEY(source)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(typeId)   REFERENCES {Tn.Enum}(id) ON DELETE RESTRICT
    , FOREIGN KEY(systemId) REFERENCES {Tn.System}(id) ON DELETE CASCADE    -- System 삭제시 Arrow 도 삭제
);

-- Call 간 연결.  Work 에 속함
CREATE TABLE {k Tn.ArrowCall}( {sqlUniq()}
    , {k "source"}        {intKeyType} NOT NULL
    , {k "target"}        {intKeyType} NOT NULL
    , {k "typeId"}        {intKeyType} NOT NULL         -- arrow type : "Start", "Reset", ??

    , {k "workId"}        {intKeyType} NOT NULL
    , FOREIGN KEY(source)   REFERENCES {Tn.Call}(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES {Tn.Call}(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(typeId)   REFERENCES {Tn.Enum}(id) ON DELETE RESTRICT
    , FOREIGN KEY(workId)   REFERENCES {Tn.Work}(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
);



-- 삭제 ??
CREATE TABLE {k Tn.ParamWork} (  {sqlUniq()}
);

-- 삭제 ??
CREATE TABLE {k Tn.ParamCall} (  {sqlUniq()}
);


CREATE TABLE {k Tn.Meta} (
    {k "id"}  {autoincPrimaryKey},
    {k "key"} TEXT NOT NULL,
    {k "val"} TEXT NOT NULL
);

CREATE TABLE {k Tn.Log} (
    {k "id"}  {autoincPrimaryKey}
    , {k "projectId"}   {intKeyType}
    , {k "dateTime"}    {datetime} NOT NULL DEFAULT {now}
    , {k "message"}     TEXT NOT NULL
    -- , FOREIGN KEY(projectId)   REFERENCES {Tn.Project}(id) ON DELETE CASCADE     -- "log" 테이블에서 자료 추가, 갱신 작업이 "log_projectid_fkey" 참조키(foreign key) 제약 조건을 위배했습니다
    -- FK cascade 는 동작 어려움... Project 삭제시 현재 log table 에 기록하기 때문에 입력과 동시에 삭제 일어나야 하는 상황 발생함
);

CREATE TABLE {k Tn.Temp} (
    {k "id"}  {autoincPrimaryKey}
    , {k "key"} TEXT NOT NULL
    , {k "val"} TEXT NOT NULL
);


-- tableHistory 테이블
CREATE TABLE {k Tn.TableHistory} (
    {k "id"}  {autoincPrimaryKey}
    , {k "name"} TEXT NOT NULL
    , {k "operation"} TEXT
    , {k "oldId"} {intKeyType}
    , {k "newId"} {intKeyType}
    , CONSTRAINT {Tn.TableHistory}_uniq UNIQUE (name, operation, oldId, newId)
);

CREATE TABLE {k Tn.TypeTest} (
    {k "id"}  {autoincPrimaryKey}
    , {k "guid"}          {guid}
    , {k "optionGuid"}    {guid}
    , {k "nullableGuid"}  {guid}
    , {k "optionInt"}     {intKeyType}
    , {k "nullableInt"}   {intKeyType}
    , {k "jsonb"}         {jsonb}
    , {k "dateTime"}      {datetime}
);

"""

        let sqlViews = $"""
CREATE VIEW {k Vn.MapProject2System} AS
    SELECT
        m.{k "id"}
        , p.{k "id"}    AS projectId
        , p.{k "name"}  AS projectName
        , s.{k "id"}    AS systemId
        , s.{k "name"}  AS systemName
        , s2.{k "id"}   AS prototypeId
        , s2.{k "name"} AS prototypeName
        , m.{k "loadedName"}
        , m.{k "isActive"}
    FROM {k Tn.MapProject2System} m
    JOIN {k Tn.Project} p ON p.id = m.projectId
    JOIN {k Tn.System}  s ON s.id = m.systemId
    LEFT JOIN {k Tn.System}  s2 ON s2.id = s.prototypeId
    ;


CREATE VIEW {k Vn.MapCall2ApiCall} AS
    SELECT
        m.{k "id"}
        , p.{k "id"}    AS projectId
        , p.{k "name"}  AS projectName
        , s.{k "id"}    AS systemId
        , s.{k "name"}  AS systemName
        , psm.{k "loadedName"}
        , w.{k "id"}    AS workId
        , w.{k "name"}  AS workName
        , c.{k "id"}    AS callId
        , c.{k "name"}  AS callName
        , ac.{k "id"}   AS apiCallId
        , ac.{k "name"} AS apiCallName
        , ad.{k "id"}   AS apiDefId
        , ad.{k "name"} AS apiDefName
    FROM {k Tn.MapCall2ApiCall} m
    JOIN {k Tn.Call} c                ON c.Id         = m.callId
    JOIN {k Tn.ApiCall} ac            ON ac.Id        = m.apiCallId
    JOIN {k Tn.ApiDef} ad             ON ad.Id        = ac.apiDefId
    JOIN {k Tn.Work} w                ON w.Id         = c.workId
    JOIN {k Tn.System} s              ON s.id         = w.systemId
    JOIN {k Tn.MapProject2System} psm ON psm.systemId = s.id
    JOIN {k Tn.Project} p             ON p.id         = psm.projectId
    ;

CREATE VIEW {k Vn.System} AS
    SELECT
        s.{k "id"}
        , s.{k "name"}  AS systemName
        , s.{k "parameter"}
        , s.{k "iri"}
        , psm.{k "loadedName"}
        , p.{k "id"}    AS projectId
        , p.{k "name"}  AS projectName
    FROM {k Tn.MapProject2System} psm
    JOIN {k Tn.System} s ON psm.systemId = s.id
    JOIN {k Tn.Project} p ON p.id = psm.projectId
    ;

CREATE VIEW {k Vn.ApiDef} AS
    SELECT
        x.{k "id"}
        , x.{k "name"}
        , x.{k "parameter"}
        , x.{k "isPush"}
        , s.{k "id"}    AS systemId
        , s.{k "name"}  AS systemName
    FROM {k Tn.ApiDef} x
    JOIN {k Tn.System} s  ON s.id = x.systemId
    ;

CREATE VIEW {k Vn.ApiCall} AS
    SELECT
        x.{k "id"}
        , x.{k "name"}
        , x.{k "parameter"}
        , x.{k "inAddress"}
        , x.{k "outAddress"}
        , x.{k "inSymbol"}
        , x.{k "outSymbol"}
        , x.{k "value1"}
        , x.{k "value2"}
        , enumV.{k "name"} AS valueType
        , enumR.{k "name"} AS rangeType
        , ad.{k "id"}   AS apiDefId
        , ad.{k "name"} AS apiDefName
        , s.{k "id"}    AS systemId
        , s.{k "name"}  AS systemName
    FROM {k Tn.ApiCall} x
    JOIN {k Tn.ApiDef} ad ON ad.id = x.apiDefId
    JOIN {k Tn.System} s  ON s.id = ad.systemId
    JOIN {k Tn.Enum} enumV ON enumV.id = x.valueTypeId
    JOIN {k Tn.Enum} enumR ON enumR.id = x.rangeTypeId
    ;


CREATE VIEW {k Vn.Flow} AS
    SELECT
        x.{k "id"}
        , x.{k "name"}  AS flowName
        , x.{k "parameter"}
        , p.{k "id"}    AS projectId
        , p.{k "name"}  AS projectName
        , s.{k "id"}    AS systemId
        , s.{k "name"}  AS systemName
        , w.{k "id"}    AS workId
        , w.{k "name"}  AS workName
    FROM {k Tn.Work} w
    LEFT JOIN {k Tn.Flow} x           ON x.id         = w.flowId
    JOIN {k Tn.System} s              ON s.id         = w.systemId
    JOIN {k Tn.MapProject2System} psm ON psm.systemId = s.id
    JOIN {k Tn.Project} p             ON p.id         = psm.projectId
    ;


CREATE VIEW {k Vn.Work} AS
    SELECT
        x.{k "id"}
        , x.{k "name"}  AS workName
        , x.{k "parameter"}
        , x.{k "motion"}
        , x.{k "script"}
        , x.{k "isFinished"}
        , x.{k "numRepeat"}
        , x.{k "period"}
        , x.{k "delay"}

        , e.{k "name"}  AS status4
        , p.{k "id"}    AS projectId
        , p.{k "name"}  AS projectName
        , s.{k "id"}    AS systemId
        , s.{k "name"}  AS systemName
        , f.{k "id"}    AS flowId
        , f.{k "name"}  AS flowName
    FROM {k Tn.Work} x
    JOIN {k Tn.System} s ON s.id = x.systemId
    JOIN {k Tn.MapProject2System} psm ON psm.systemId = s.id
    JOIN {k Tn.Project} p ON p.id = psm.projectId
    LEFT JOIN {k Tn.Flow} f ON f.id = x.flowId
    LEFT JOIN {k Tn.Enum} e ON e.id = x.status4Id
    ;

CREATE VIEW {k Vn.Call} AS
    SELECT
        c.{k "id"}
        , c.{k "name"}  AS callName
        , e.{k "name"}  AS status4
        , c.{k "parameter"}
        , c.{k "timeout"}
        , c.{k "autoPre"}
        , c.{k "safety"}
        , c.{k "isDisabled"}
        , p.{k "id"}      AS projectId
        , p.{k "name"}  AS projectName
        , s.{k "id"}    AS systemId
        , s.{k "name"}  AS systemName
        , w.{k "id"}    AS workId
        , w.{k "name"}  AS workName
    FROM {k Tn.Call} c
    JOIN {k Tn.Work} w                ON w.Id         = c.workId
    JOIN {k Tn.System} s              ON s.id         = w.systemId
    JOIN {k Tn.MapProject2System} psm ON psm.systemId = s.id
    JOIN {k Tn.Project} p             ON p.id         = psm.projectId
    LEFT JOIN {k Tn.Enum} e           ON e.id         = c.status4Id
    ;



CREATE VIEW {k Vn.ArrowCall} AS
    SELECT
        ac.{k "id"}
        , ac.{k "parameter"}
        , ac.{k "source"}
        , src.{k "name"} AS sourceName
        , ac.{k "target"}
        , tgt.{k "name"} AS targetName
        , ac.{k "typeId"}
        , enum.{k "name"} AS enumName
        , ac.{k "workId"}
        , w.{k "name"} AS workName
        , p.{k "id"}    AS projectId
        , p.{k "name"}  AS projectName
        , s.{k "id"}    AS systemId
        , s.{k "name"}  AS systemName
    FROM {k Tn.ArrowCall} ac
    JOIN {k Tn.Call} src ON src.Id = ac.source
    JOIN {k Tn.Call} tgt ON tgt.Id = ac.target
    JOIN {k Tn.Work} w ON w.Id = ac.workId
    JOIN {k Tn.System} s ON s.id = w.systemId
    JOIN {k Tn.MapProject2System} psm ON psm.systemId = s.id
    JOIN {k Tn.Project} p ON p.id = psm.projectId
    LEFT JOIN {k Tn.Enum} enum ON ac.typeId = enum.id
    ;


CREATE VIEW {k Vn.ArrowWork} AS
    SELECT
        aw.{k "id"}
        , aw.{k "parameter"}
        , aw.{k "source"}
        , src.{k "name"}      AS sourceName
        , aw.{k "target"}
        , tgt.{k "name"}      AS targetName
        , aw.{k "typeId"}
        , enum.{k "name"}     AS enumName
        , p.{k "id"}          AS projectId
        , p.{k "name"}        AS projectName
        , s.{k "id"}          AS systemId
        , s.{k "name"}        AS systemName
    FROM {k Tn.ArrowWork} aw
    JOIN {k Tn.Work} src ON src.Id = aw.source
    JOIN {k Tn.Work} tgt ON tgt.Id = aw.target
    JOIN {k Tn.System} s ON s.id = src.systemId
    JOIN {k Tn.MapProject2System} psm ON psm.systemId = s.id
    JOIN {k Tn.Project} p  ON p.id = psm.projectId
    LEFT JOIN {k Tn.Enum} enum ON aw.typeId = enum.id
    ;



INSERT INTO {k Tn.Meta} (key, val) VALUES ('Version', '1.0.0.0');
DELETE FROM {k Tn.TableHistory};
{ if withTrigger then triggerSql dbProvider else "" }

CREATE TABLE {k Tn.EOT} (
    {k "id"}  {autoincPrimaryKey}
);

--
-- End of database schema
--
"""

        sqlTables + "\n" + sqlViews






