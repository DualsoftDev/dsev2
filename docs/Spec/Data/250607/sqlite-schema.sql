
--
-- Auto-generated DS schema for Postgres.  Do *NOT* Edit.
--

CREATE TABLE project( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , author       TEXT NOT NULL
    , version      TEXT NOT NULL
    , description  TEXT
    , CONSTRAINT project_uniq UNIQUE (name)    -- Project 의 이름은 유일해야 함
);

CREATE TABLE system( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , prototypeId   BIGINT    -- 프로토타입의 Guid.  prototype 으로 만든 instance 는 prototype 의 Guid 를 갖고, prototype 자체는 NULL 을 갖는다.
    , iri           TEXT            -- Internationalized Resource Identifier.  e.g. "http://example.com/system/12345"  -- System 의 이름은 유일해야 함
    , author        TEXT NOT NULL
    , langVersion   TEXT NOT NULL   -- System.Version 형식의 문자열.  e.g. "1.0.0"  -- System 의 언어 버전
    , engineVersion TEXT NOT NULL
    , originGuid    TEXT            -- 복사 생성시 원본의 Guid.  최초 생성시에는 복사원본이 없으므로 null.  FOREIGN KEY 설정 안함.  db 에 원본삭제시 null 할당 가능
    , description   TEXT
    , FOREIGN KEY(prototypeId) REFERENCES system(id) ON DELETE SET NULL     -- prototype 삭제시, instance 의 prototype 참조만 삭제
    , CONSTRAINT system_uniq UNIQUE (iri)
);


CREATE TABLE mapProject2System( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , projectId      BIGINT NOT NULL
    , systemId       BIGINT NOT NULL
    , isActive       BOOLEAN NOT NULL DEFAULT FALSE
    , loadedName     TEXT
    , FOREIGN KEY(projectId)   REFERENCES project(id) ON DELETE CASCADE
    , FOREIGN KEY(systemId)    REFERENCES system(id) ON DELETE CASCADE     -- NO ACTION       -- ON DELETE RESTRICT    -- RESTRICT: 부모 레코드가 삭제되기 전에 참조되고 있는 자식 레코드가 있는지 즉시 검사하고, 있으면 삭제를 막음.
    , CONSTRAINT mapProject2System_uniq UNIQUE (projectId, systemId)
);

-- TODO: MapProject2System row 하나 삭제시,
--    다른 project 에서 참조되고 있지 않은 systemId 에 해당하는 system 들을 삭제할 수 있도록 trigger 설정 필요


DROP TRIGGER IF EXISTS trigger_project_beforeDelete_recordSystemIds ON project;

CREATE OR REPLACE FUNCTION trigger_project_beforeDelete_recordSystemIds_fn() RETURNS trigger AS $$
BEGIN
    
        INSERT INTO log (projectId, message)
        SELECT
            OLD.id,
            'beforeDelete: projectId=' || OLD.id || ', systemIds=' ||
                COALESCE((
                    SELECT STRING_AGG(systemId::TEXT, ',')
                    FROM mapProject2System
                    WHERE projectId = OLD.id
                ), '없음');

        DELETE FROM temp WHERE key = concat('trigger_temp_systemId_', OLD.id);

        INSERT INTO temp (key, val)
        SELECT concat('trigger_temp_systemId_', OLD.id), systemId
        FROM mapProject2System
        WHERE projectId = OLD.id;
        
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_project_beforeDelete_recordSystemIds
BEFORE DELETE ON project
FOR EACH ROW EXECUTE FUNCTION trigger_project_beforeDelete_recordSystemIds_fn();
                    


DROP TRIGGER IF EXISTS trigger_project_afterDelete_dropSystems ON project;

CREATE OR REPLACE FUNCTION trigger_project_afterDelete_dropSystems_fn() RETURNS trigger AS $$
BEGIN
    
        -- 로그 기록
        INSERT INTO log (projectId, message)
        SELECT
            OLD.id,
            'afterDelete: projectId=' || OLD.id || ', systemIds=' ||
                COALESCE((
                    SELECT STRING_AGG(val::TEXT, ',')
                    FROM temp
                    WHERE key = concat('trigger_temp_systemId_', OLD.id)
                ), '없음');




        -- 시스템 제거
        DELETE FROM system
        WHERE id IN (
            SELECT CAST(val AS INTEGER) FROM temp WHERE key = concat('trigger_temp_systemId_', OLD.id)
        )
        AND NOT EXISTS (
            SELECT 1 FROM mapProject2System
            WHERE systemId = system.id
        );

        -- temp 정리
        DELETE FROM temp WHERE key = concat('trigger_temp_systemId_', OLD.id);
        
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_project_afterDelete_dropSystems
AFTER DELETE ON project
FOR EACH ROW EXECUTE FUNCTION trigger_project_afterDelete_dropSystems_fn();
                    







--CREATE TRIGGER trigger_mapProject2System_afterDelete_dropSystems
--AFTER DELETE ON mapProject2System
--BEGIN
--    -- 디버깅용 로그 삽입
--    INSERT INTO temp(key, val)
--    VALUES (
--        'trigger_mapProject2System_afterDelete_dropSystems',
--        '삭제된 systemId=' || OLD.systemId
--    );
--
--    -- system 삭제 시도
--    DELETE FROM system
--    WHERE id = OLD.systemId
--      AND NOT EXISTS (
--          SELECT 1 FROM mapProject2System
--          WHERE systemId = OLD.systemId
--      );
--END;



CREATE TABLE flow( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , systemId      BIGINT NOT NULL
    , FOREIGN KEY(systemId)   REFERENCES system(id) ON DELETE CASCADE
);


CREATE TABLE button( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , flowId        BIGINT NOT NULL
    , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE
);

CREATE TABLE lamp( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , flowId        BIGINT NOT NULL
    , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE
);

CREATE TABLE condition( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , flowId        BIGINT NOT NULL
    , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE
);

CREATE TABLE action( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , flowId        BIGINT NOT NULL
    , FOREIGN KEY(flowId)   REFERENCES flow(id) ON DELETE CASCADE
);

CREATE TABLE enum(
    id              SERIAL PRIMARY KEY
    , category      VARCHAR(128) NOT NULL
    , name          VARCHAR(128) NOT NULL
    , value         INT NOT NULL
    , CONSTRAINT enum_uniq UNIQUE (name, category)
);


CREATE TABLE work( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , systemId    BIGINT NOT NULL
    , flowId      BIGINT DEFAULT NULL    -- NULL 허용 (work가 flow에 속하지 않을 수도 있음)
    , motion      TEXT
    , script      TEXT
    , isFinished  BOOLEAN NOT NULL DEFAULT FALSE
    , numRepeat   INT NOT NULL DEFAULT 0  -- 반복 횟수
    , period      INT NOT NULL DEFAULT 0  -- 주기
    , delay       INT NOT NULL DEFAULT 0  -- 지연
    , status4Id   BIGINT DEFAULT NULL
    , FOREIGN KEY(systemId)  REFERENCES system(id) ON DELETE CASCADE
    , FOREIGN KEY(flowId)    REFERENCES flow(id) ON DELETE CASCADE      -- Flow 삭제시 work 삭제, flowId 는 null 허용
    , FOREIGN KEY(status4Id) REFERENCES enum(id) ON DELETE SET NULL
);


--
-- Work > Call > ApiCall > ApiDef
--

CREATE TABLE apiCall( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , systemId        BIGINT NOT NULL
    , inAddress       TEXT NOT NULL
    , outAddress      TEXT NOT NULL
    , inSymbol        TEXT NOT NULL
    , outSymbol       TEXT NOT NULL

    -- Value 에 대해서는 Database column 에 욱여넣기 힘듦.  문자열 규약이 필요.  e.g. "1.0", "(1, 10)", "(, 3.14)", "[5, 10)",
    , valueSpec       JSONB
    , apiDefId        BIGINT NOT NULL
    , FOREIGN KEY(systemId) REFERENCES system(id) ON DELETE CASCADE      -- Call 삭제시 ApiCall 도 삭제
);

CREATE TABLE apiDef( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , isPush          BOOLEAN NOT NULL DEFAULT FALSE
    , systemId        BIGINT NOT NULL       -- API 가 정의된 target system
    , FOREIGN KEY(systemId) REFERENCES system(id) ON DELETE CASCADE
);


CREATE TABLE call( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , name          VARCHAR(128) NOT NULL
    , callTypeId    BIGINT -- NOT NULL         -- 호출 유형: e.g "Normal", "Parallel", "Repeat"
    , status4Id     BIGINT DEFAULT NULL
    , timeout       INT   -- ms
    , autoPre       TEXT
    , safety        TEXT
    , isDisabled    BOOLEAN NOT NULL DEFAULT FALSE   -- 0: 활성화, 1: 비활성화
    , workId        BIGINT NOT NULL
    , FOREIGN KEY(workId)     REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Call 도 삭제
    , FOREIGN KEY(callTypeId) REFERENCES enum(id) ON DELETE RESTRICT
    , FOREIGN KEY(status4Id) REFERENCES enum(id) ON DELETE SET NULL
    -- , apiCallId     BIGINT NOT NULL  -- call 이 복수개의 apiCall 을 가지므로, mapCall2ApiCall 에 저장
    -- , FOREIGN KEY(apiCallId) REFERENCES apiCall(id)
);


-- Call 은 여러개의 Api 를 동시에 호출할 수 있다.
CREATE TABLE mapCall2ApiCall( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , callId     BIGINT NOT NULL
    , apiCallId  BIGINT NOT NULL
    , FOREIGN KEY(callId)     REFERENCES call(id) ON DELETE CASCADE
    , FOREIGN KEY(apiCallId)  REFERENCES apiCall(id) ON DELETE CASCADE
    , CONSTRAINT mapCall2ApiCall_uniq UNIQUE (callId, apiCallId)
);




-- Work 간 연결.  System 에 속함
CREATE TABLE arrowWork( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , source        BIGINT NOT NULL
    , target        BIGINT NOT NULL
    , typeId        BIGINT NOT NULL         -- arrow type : "Start", "Reset", ??

    , systemId      BIGINT NOT NULL
    , FOREIGN KEY(source)   REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
    , FOREIGN KEY(typeId)   REFERENCES enum(id) ON DELETE RESTRICT
    , FOREIGN KEY(systemId) REFERENCES system(id) ON DELETE CASCADE    -- System 삭제시 Arrow 도 삭제
);

-- Call 간 연결.  Work 에 속함
CREATE TABLE arrowCall( 
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
    , source        BIGINT NOT NULL
    , target        BIGINT NOT NULL
    , typeId        BIGINT NOT NULL         -- arrow type : "Start", "Reset", ??

    , workId        BIGINT NOT NULL
    , FOREIGN KEY(source)   REFERENCES call(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(target)   REFERENCES call(id) ON DELETE CASCADE      -- Call 삭제시 Arrow 도 삭제
    , FOREIGN KEY(typeId)   REFERENCES enum(id) ON DELETE RESTRICT
    , FOREIGN KEY(workId)   REFERENCES work(id) ON DELETE CASCADE      -- Work 삭제시 Arrow 도 삭제
);



-- 삭제 ??
CREATE TABLE paramWork (  
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
);

-- 삭제 ??
CREATE TABLE paramCall (  
    id              SERIAL PRIMARY KEY
    , guid          UUID NOT NULL UNIQUE   -- 32 byte char (for hex) string,  *********** UNIQUE indexing 여부 성능 고려해서 판단 필요 **********
    , parameter     JSONB
    , dateTime      TIMESTAMP(6)
);


CREATE TABLE meta (
    id  SERIAL PRIMARY KEY,
    key TEXT NOT NULL,
    val TEXT NOT NULL
);

CREATE TABLE log (
    id  SERIAL PRIMARY KEY
    , projectId   BIGINT
    , dateTime    TIMESTAMP(6) NOT NULL DEFAULT CURRENT_TIMESTAMP
    , message     TEXT NOT NULL
    -- , FOREIGN KEY(projectId)   REFERENCES project(id) ON DELETE CASCADE     -- "log" 테이블에서 자료 추가, 갱신 작업이 "log_projectid_fkey" 참조키(foreign key) 제약 조건을 위배했습니다
    -- FK cascade 는 동작 어려움... Project 삭제시 현재 log table 에 기록하기 때문에 입력과 동시에 삭제 일어나야 하는 상황 발생함
);

CREATE TABLE temp (
    id  SERIAL PRIMARY KEY
    , key TEXT NOT NULL
    , val TEXT NOT NULL
);


-- tableHistory 테이블
CREATE TABLE tableHistory (
    id  SERIAL PRIMARY KEY
    , name TEXT NOT NULL
    , operation TEXT
    , oldId BIGINT
    , newId BIGINT
    , CONSTRAINT tableHistory_uniq UNIQUE (name, operation, oldId, newId)
);

CREATE TABLE typeTest (
    id  SERIAL PRIMARY KEY
    , guid          UUID
    , optionGuid    UUID
    , nullableGuid  UUID
    , optionInt     BIGINT
    , nullableInt   BIGINT
    , jsonb         JSONB
    , dateTime      TIMESTAMP(6)
);



CREATE VIEW vwMapProject2System AS
    SELECT
        m.id
        , p.id    AS projectId
        , p.name  AS projectName
        , s.id    AS systemId
        , s.name  AS systemName
        , s2.id   AS prototypeId
        , s2.name AS prototypeName
        , m.loadedName
        , m.isActive
    FROM mapProject2System m
    JOIN project p ON p.id = m.projectId
    JOIN system  s ON s.id = m.systemId
    LEFT JOIN system  s2 ON s2.id = s.prototypeId
    ;


CREATE VIEW vwMapCall2ApiCall AS
    SELECT
        m.id
        , p.id    AS projectId
        , p.name  AS projectName
        , s.id    AS systemId
        , s.name  AS systemName
        , psm.loadedName
        , w.id    AS workId
        , w.name  AS workName
        , c.id    AS callId
        , c.name  AS callName
        , ac.id   AS apiCallId
        , ac.name AS apiCallName
        , ad.id   AS apiDefId
        , ad.name AS apiDefName
    FROM mapCall2ApiCall m
    JOIN call c                ON c.Id         = m.callId
    JOIN apiCall ac            ON ac.Id        = m.apiCallId
    JOIN apiDef ad             ON ad.Id        = ac.apiDefId
    JOIN work w                ON w.Id         = c.workId
    JOIN system s              ON s.id         = w.systemId
    JOIN mapProject2System psm ON psm.systemId = s.id
    JOIN project p             ON p.id         = psm.projectId
    ;

CREATE VIEW vwSystem AS
    SELECT
        s.id
        , s.name  AS systemName
        , s.parameter
        , s.iri
        , psm.loadedName
        , p.id    AS projectId
        , p.name  AS projectName
    FROM mapProject2System psm
    JOIN system s ON psm.systemId = s.id
    JOIN project p ON p.id = psm.projectId
    ;

CREATE VIEW vwApiDef AS
    SELECT
        x.id
        , x.name
        , x.parameter
        , x.isPush
        , s.id    AS systemId
        , s.name  AS systemName
    FROM apiDef x
    JOIN system s  ON s.id = x.systemId
    ;


CREATE VIEW vwApiCall AS
    SELECT
        x.id
        , x.name
        , x.parameter
        , x.inAddress
        , x.outAddress
        , x.inSymbol
        , x.outSymbol
        , x.valueSpec
        
        -- '->' 는 json/jsonb 객체를 그대로 유지
        -- '->>' 는 문자열을 추출
        , x.valueSpec->>'valueType' AS valueType
        , x.valueSpec->'value'->>'Case' AS case
        , CASE
            WHEN x.valueSpec->'value'->>'Case' = 'Single'
            THEN x.valueSpec->'value'->'Fields'->>0
            ELSE NULL
          END AS singleValue
        , ad.id   AS apiDefId
        , ad.name AS apiDefName
        , s.id    AS systemId
        , s.name  AS systemName
    FROM apiCall x
    JOIN apiDef ad ON ad.id = x.apiDefId
    JOIN system s  ON s.id = ad.systemId
    ;


CREATE VIEW vwFlow AS
    SELECT
        x.id
        , x.name  AS flowName
        , x.parameter
        , p.id    AS projectId
        , p.name  AS projectName
        , s.id    AS systemId
        , s.name  AS systemName
        , w.id    AS workId
        , w.name  AS workName
    FROM work w
    LEFT JOIN flow x           ON x.id         = w.flowId
    JOIN system s              ON s.id         = w.systemId
    JOIN mapProject2System psm ON psm.systemId = s.id
    JOIN project p             ON p.id         = psm.projectId
    ;


CREATE VIEW vwWork AS
    SELECT
        x.id
        , x.name  AS workName
        , x.parameter
        , x.motion
        , x.script
        , x.isFinished
        , x.numRepeat
        , x.period
        , x.delay
        , e.name  AS status4
        , p.id    AS projectId
        , p.name  AS projectName
        , s.id    AS systemId
        , s.name  AS systemName
        , f.id    AS flowId
        , f.name  AS flowName
    FROM work x
    JOIN system s ON s.id = x.systemId
    JOIN mapProject2System psm ON psm.systemId = s.id
    JOIN project p ON p.id = psm.projectId
    LEFT JOIN flow f ON f.id = x.flowId
    LEFT JOIN enum e ON e.id = x.status4Id
    ;

CREATE VIEW vwCall AS
    SELECT
        c.id
        , c.name  AS callName
        , e.name  AS status4
        , c.parameter
        , c.timeout
        , c.autoPre
        , c.safety
        , c.isDisabled
        , p.id      AS projectId
        , p.name  AS projectName
        , s.id    AS systemId
        , s.name  AS systemName
        , w.id    AS workId
        , w.name  AS workName
    FROM call c
    JOIN work w                ON w.Id         = c.workId
    JOIN system s              ON s.id         = w.systemId
    JOIN mapProject2System psm ON psm.systemId = s.id
    JOIN project p             ON p.id         = psm.projectId
    LEFT JOIN enum e           ON e.id         = c.status4Id
    ;



CREATE VIEW vwArrowCall AS
    SELECT
        ac.id
        , ac.parameter
        , ac.source
        , src.name AS sourceName
        , ac.target
        , tgt.name AS targetName
        , ac.typeId
        , enum.name AS enumName
        , ac.workId
        , w.name AS workName
        , p.id    AS projectId
        , p.name  AS projectName
        , s.id    AS systemId
        , s.name  AS systemName
    FROM arrowCall ac
    JOIN call src ON src.Id = ac.source
    JOIN call tgt ON tgt.Id = ac.target
    JOIN work w ON w.Id = ac.workId
    JOIN system s ON s.id = w.systemId
    JOIN mapProject2System psm ON psm.systemId = s.id
    JOIN project p ON p.id = psm.projectId
    LEFT JOIN enum enum ON ac.typeId = enum.id
    ;


CREATE VIEW vwArrowWork AS
    SELECT
        aw.id
        , aw.parameter
        , aw.source
        , src.name      AS sourceName
        , aw.target
        , tgt.name      AS targetName
        , aw.typeId
        , enum.name     AS enumName
        , p.id          AS projectId
        , p.name        AS projectName
        , s.id          AS systemId
        , s.name        AS systemName
    FROM arrowWork aw
    JOIN work src ON src.Id = aw.source
    JOIN work tgt ON tgt.Id = aw.target
    JOIN system s ON s.id = src.systemId
    JOIN mapProject2System psm ON psm.systemId = s.id
    JOIN project p  ON p.id = psm.projectId
    LEFT JOIN enum enum ON aw.typeId = enum.id
    ;



INSERT INTO meta (key, val) VALUES ('Version', '1.0.0.0');
DELETE FROM tableHistory;


CREATE TABLE endOfTable (
    id  SERIAL PRIMARY KEY
);

--
-- End of database schema
--
