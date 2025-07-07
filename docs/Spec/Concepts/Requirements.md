# 1. Requirements
1. 3D mapping 시 고유한 system 이름으로 (project 이름 상관없이) 지정 가능할 것
   1. OPCUA 주소 지정 / 수정 용이성
2. 코드 생성시의 헷갈림?
3. 상태 update 헷갈림?
4. 
## 1.1. 저장 관련 requirements
 - [📁 링크 보기](./Storage.md)
# 2. Asset 개념 추가
```sql
CREATE TABLE asset(
    id: PK
    , guid    UUID NOT NULL
    , name    TEXT NOT NULL
    , iri     TEXT NOT NULL
    , isController BOOLEAN NOT NULL DEFAULT FALSE -- PC, PLC 자산인 경우 true, device 인 경우 false
    , controllingSystemId INT
    , FOREIGN KEY(controllingSystemId)   REFERENCES system(id) ON DELETE SET NULL
)

CREATE TABLE system(
    -- iri 는 asset 으로 이동
    -- ...
    , assetId INT NOT NULL
    , FOREIGN KEY(assetId)   REFERENCES asset(id) ON DELETE CASCADE -- SET NULL ??
)
```
1. Asset 은 실물 자산과 1:1 대응.  system 과는 다름 (work, flow 등을 가지지 않는 단순 자산 열거 정보)
   1. 제어 할당 되지 않은 자산인 경우, 1:0
2. Asset 을 실제 `제어하는` System(DsSystem or 자연법칙) 은 하나만 존재해야 함
   1. 자연법칙을 따르는 asset 을 `제어하는` DsSystem 도 하나만 존재해야 함
   2. `asset 을 비제어용으로 사용하는 DsSystem 은 복수개 존재할 수 있음`
      1. 이 경우, DsSystem 은 사본들로서 서로 독립적 (복사해서 사용할 수도 있고, 수정해서 사용할 수 도있고.. System 모양이 다를 수도 있음)
3. 제어기 자산(isController 값이 true)은 project 의 Active system 에 해당하는 자산
4. 비제어기 자산은 passive system 에 해당하는 자산.  (자연법칙에 따른 제어)
