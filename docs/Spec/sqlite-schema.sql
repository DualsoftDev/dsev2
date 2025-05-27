
--
-- Auto-generated DS schema.  Do *NOT* Edit.
--

BEGIN TRANSACTION;


CREATE TABLE [project]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
    , [author]       TEXT NOT NULL
    , [version]      TEXT NOT NULL
    , [description]  TEXT
    , CONSTRAINT project_uniq UNIQUE (name)    -- Project 의 이름은 유일해야 함
);

CREATE TABLE [system]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
    , [author]        TEXT NOT NULL
    , [langVersion]   TEXT NOT NULL
    , [engineVersion] TEXT NOT NULL
    , [originGuid]    TEXT      -- 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null.  FOREIGN KEY 설정 안함.  db 에 원본삭제시 null 할당 가능
    , [description]   TEXT
);


CREATE TABLE [projectSystemMap]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [projectId]      INTEGER NOT NULL
    , [systemId]       INTEGER NOT NULL
    , [isActive]       TINYINT NOT NULL DEFAULT 0
    , FOREIGN KEY(projectId)   REFERENCES project(id) ON DELETE CASCADE
    , FOREIGN KEY(systemId)    REFERENCES system(id) ON DELETE CASCADE
    , CONSTRAINT projectSystemMap_uniq UNIQUE (projectId, systemId)
);

CREATE TABLE [flow]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
    , [systemId]      INTEGER NOT NULL
    , FOREIGN KEY(systemId)   REFERENCES system(id) ON DELETE CASCADE
);


    CREATE TABLE [button]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
        , [flowId]        INTEGER NOT NULL
        , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE
    );

    CREATE TABLE [lamp]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
        , [flowId]        INTEGER NOT NULL
        , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE
    );

    CREATE TABLE [condition]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
        , [flowId]        INTEGER NOT NULL
        , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE
    );

    CREATE TABLE [action]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
        , [flowId]        INTEGER NOT NULL
        , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE
    );

CREATE TABLE [enum](
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [name]          NVARCHAR(128) NOT NULL
    , [category]      NVARCHAR(128) NOT NULL
    , [value]         INT NOT NULL
    , CONSTRAINT enum_uniq UNIQUE (name, category)
);


CREATE TABLE [work]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
    , [systemId]      INTEGER NOT NULL
    , [flowId]      INTEGER DEFAULT NULL    -- NULL 허용 (work가 flow에 속하지 않을 수도 있음)
    , FOREIGN KEY(systemId) REFERENCES system(id) ON DELETE CASCADE
    , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE      -- Flow 삭제시 work 삭제, flowId 는 null 허용
);

CREATE TABLE [call]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
    , [callTypeId]    INTEGER -- NOT NULL         -- 호출 유형: e.g "Normal", "Parallel", "Repeat"
    , [timeOut]       INT   -- ms
    , [autoPre]       TEXT
    , [safety]        TEXT
    , [workId]        INTEGER NOT NULL
    -- , [apiCallId]     INTEGER NOT NULL
    , FOREIGN KEY(workId)    REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Call 도 삭제
    , FOREIGN KEY(callTypeId)   REFERENCES enum(id)
    -- , FOREIGN KEY(apiCallId) REFERENCES apiCall(id)
);

-- Work 간 연결.  System 에 속함
CREATE TABLE [arrowWork]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [source]        INTEGER NOT NULL
    , [target]        INTEGER NOT NULL
    , [systemId]      INTEGER NOT NULL
    , FOREIGN KEY(source)   REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(systemId) REFERENCES system(id) ON DELETE CASCADE    -- System 삭제시 Arrow 도 삭제
);

-- Call 간 연결.  Work 에 속함
CREATE TABLE [arrowCall]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [source]        INTEGER NOT NULL
    , [target]        INTEGER NOT NULL
    , [workId]        INTEGER NOT NULL
    , FOREIGN KEY(source)   REFERENCES call(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES call(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(workId)   REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
);

--
-- Work > Call > ApiCall > ApiDef
--

CREATE TABLE [apiCall]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [inAddress]       TEXT NOT NULL
    , [outAddress]      TEXT NOT NULL
    , [inSymbol]        TEXT NOT NULL
    , [outSymbol]       TEXT NOT NULL

    -- Value 에 대해서는 Database column 에 욱여넣기 힘듦.  문자열 규약이 필요.  e.g. "1.0", "(1, 10)", "(, 3.14)", "[5, 10)",
    , [value]           TEXT NOT NULL   -- 값 범위 또는 단일 값 조건 정의 (선택 사항).  ValueParam type
    , [valueTypeId]     INTEGER NOT NULL         -- (e.g. "string", "int", "float", "bool", "dateTime",
    , [apiDefId]        INTEGER NOT NULL
    , FOREIGN KEY(valueTypeId)   REFERENCES enum(id)
);

CREATE TABLE [apiDef]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
    , [name]          NVARCHAR(128) NOT NULL
    , [isPush]          TINYINT NOT NULL DEFAULT 0
    , [systemId]        INTEGER NOT NULL       -- API 가 정의된 target system
);

CREATE TABLE [paramWork] (  
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
);

CREATE TABLE [paramCall] (  
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)
);


CREATE TABLE [meta] (
    id INTEGER PRIMARY KEY NOT NULL,
    key TEXT NOT NULL,
    val TEXT NOT NULL
);


-- tableHistory 테이블
CREATE TABLE [tableHistory] (
    id INTEGER PRIMARY KEY NOT NULL,
    name TEXT NOT NULL,
    operation TEXT,
    oldId INTEGER,
    newId INTEGER,
    CONSTRAINT tableHistory_uniq UNIQUE (name, operation, oldId, newId)
);






INSERT INTO [meta] (key, val) VALUES ('Version', '1.0.0.0');
DELETE FROM tableHistory;

CREATE TABLE [endOfTable](
    id INTEGER PRIMARY KEY NOT NULL
);

COMMIT;
