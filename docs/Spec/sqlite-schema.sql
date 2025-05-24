-- 2025-05-24 version

CREATE TABLE [project]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)

    , [name]          NVARCHAR(128) NOT NULL

);

CREATE TABLE [system]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)

    , [name]          NVARCHAR(128) NOT NULL

);


CREATE TABLE [projectSystemMap]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)

    , [projectId]      INTEGER NOT NULL
    , [systemId]       INTEGER NOT NULL
    , [active]         TINYINT NOT NULL DEFAULT 0
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

    , [workId]        INTEGER NOT NULL
    , FOREIGN KEY(workId)   REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Call 도 삭제
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


CREATE TABLE [apiCall]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)

    , [name]          NVARCHAR(128) NOT NULL

);

CREATE TABLE [apiDef]( 
    [id]              INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
    , [guid]          TEXT NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , [dateTime]      DATETIME(7)

    , [name]          NVARCHAR(128) NOT NULL

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
