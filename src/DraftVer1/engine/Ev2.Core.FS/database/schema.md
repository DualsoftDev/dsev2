item :
	id
	itemType_id

itemType:
	id
	name: -- e.g "소나타", "그랜져", "아반떼"


work:
	guid
	name: -- e.g "F1.W1"
	item_id

- item 은 하나의 자동차 instanace 를 표현하는 entry 이고, work 는자동차를 생산하기 위한 공정을 나타내는 entry
- item 은 공정이 서로 연결된 곳에서는 동시에 여러 work에서 생산 중일 수 있고, 일반적으로는 하나의 공정에서 처리
- 하나의 work는 최대 1개의 item 을 생산할 수 있음

sqlite 용으로 data base schema 만들면?



```sql

CREATE TABLE itemType (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE
);

CREATE TABLE item (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    itemType_id INTEGER NOT NULL,
    FOREIGN KEY (itemType_id) REFERENCES itemType(id) ON DELETE CASCADE     -- 부모(itemType)가 사라지면 자식(item)도 의미가 없으므로 함께 삭제됨
);

CREATE TABLE work (
    guid TEXT PRIMARY KEY,  -- UUID 또는 문자열 기반 GUID 사용
    name TEXT NOT NULL UNIQUE,
    item_id INTEGER,
    FOREIGN KEY (item_id) REFERENCES item(id) ON DELETE SET NULL            -- 부모(item)가 삭제되어도 자식(work)이 계속 남아 있어야 할 때
);


```


## API's
- Work 내에 item 생성
    - 해당 work 에 item 이 이미 존재하면 에러
- Work 내에 item 삭제
    - 해당 work 에 item 이 존재하지 않으면 에러
- item [swork1; swork2; .., ; sworkn] 에서 [twork1; twork2, .., tworkn] 로 이동
    - branching / merging 고려하면 src, tgt 양쪽 모두 복수개 처리 가능해야 함.
    - src 나 tgt 이 empty 이면 에러
    - src work 에 하나라도 item 이 존재하지 않으면 에러
    - tgt work 에 하나라도 item 이 존재하면 에러