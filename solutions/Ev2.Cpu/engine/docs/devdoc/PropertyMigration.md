# Runtime Member → Properties 마이그레이션 체크리스트

본 문서는 `type XXX`에서 개별 필드를 제거하고 `XXXProperties`에 수용할 때 필요한 작업을 정리합니다. 
가령 `Project`/`ProjectProperties` 마이그레이션 대상이 다음과 같이 주어질 수 있습니다.

Project 의 Database, AasxPath, Author, Version, Description, DateTime 속성을 
Project 의 Properties 속성(ProjectProperties type) 으로 이동한다면

1. NjProject 의 해당 속성들 처리 하는 부분을 삭제. (왜냐면, NjProject.Properties 에 해당 정보들을 관리할 것이므로)
1. ORMProject 도 마찬가지
1. Database project table 의 삭제된 속성 관련 column 들 함께 삭제 ==> 관련 CRUD update
1. AASX serailize/deserialize 시에도 해당 속성들 표출 없이.  (Properites 속성 하나만 남김)


## 1. 사전 준비
- 영향을 받는 타입(런타임, JSON/Nj, ORM, C#, 테스트 등)을 모두 파악한다.
- 해당 타입의 속성이 어디서 소비·직렬화·저장되는지 grep 으로 조사한다.
- 변경 전/후 JSON 샘플을 확보하여 차이 검증 기준을 만든다.

## 2. Runtime 타입 (`type XXX`)
- `member val Properties = XXXProperties.Create(this)` 형태가 존재하는지 확인하고, 없다면 추가한다.
- 기존 public 멤버를 제거하거나 주석 처리한 뒤 `Properties`를 통한 접근자로 교체한다. 외부 API 호환이 필요하면 C# 확장 타입 등에서 래퍼 프로퍼티를 제공한다.
- `IRtXXX` 인터페이스 구현부가 DateTime 등 이동된 속성에 접근하는 경우 `Properties`를 경유하도록 수정한다.

## 3. Properties 타입 (`XXXProperties`)
- 이동 대상 멤버를 정의하고 기본값/초기화를 지정한다.
- 필요한 경우 `Create` 팩토리에서 `RawParent`를 설정한다.
- `DeepClone`, JSON 직렬화 시 누락되지 않는지 확인한다.

## 4. NJ(JSON) 타입 (`NjXXX`)
- JSON 구조가 `Properties` 포함하도록 반영되어 있는지 확인한다.
- 기존 멤버를 제거하고 `Properties`에 위임한다. 필요 시 `ShouldSerialize*` 메서드는 `Properties` 내부 상태로 대체/제거한다.
- `OnSerializing`/`OnDeserialized` 훅에서 `Properties`를 복제하고 parent를 설정하는 로직을 보강한다.
- 구버전 JSON/AAS 샘플과의 호환이 필요하다면, `Core.From.Aas`/`NjXXX.FromJson` 등 역직렬화 경로에서 `Properties` 블록이 비어 있을 때 이전 필드를 `Properties`로 이주하는 폴백을 구현한다. (`Work` → `WorkProperties` 마이그레이션 사례 참고)

## 5. ORM 타입 & DB 접근
- `ORMXXX` 생성자 및 `Initialize` 메서드에서 `PropertiesJson`만 다루도록 정리한다.
- `AppDbApi.rt2Orm`, `ToORM`, `DB.Insert`, `DB.Select` 등에서 속성 복제가 `PropertiesJson` 기준으로만 이루어지도록 수정한다. (구 컬럼 제거 후 INSERT/UPDATE 문에 남은 파라미터가 없는지 반드시 확인)
- C# 확장 ORM 타입이 있는 경우(예: CustomProject) 새 구조를 래핑할 수 있도록 보강한다.

## 6. DB 스키마
- `Database.Schema.fs`의 테이블 정의에서 이동된 컬럼을 제거하고 `properties` 컬럼만 유지/추가한다.
- 스펙 문서(`docs/Spec/*.sql`)와 과거 스키마 샘플도 동일하게 업데이트한다.
- 필요하면 마이그레이션 스크립트를 별도로 작성한다.

## 7. 직렬화/복제 유틸리티
- `DsCopy.Properties.fs`, `DsCompare.Objects.fs` 등 속성을 비교/복제하는 모듈에서 `Properties` 중심으로 동작하도록 변경한다. 필요하면 `Diff("Properties", …)`로만 비교하도록 단순화한다.
- AAS 변환(`Core.To.Aas.fs`, `Core.From.Aas.fs`)이 `Properties`를 직렬화/역직렬화하도록 수정한다.
- AAS 변환 시, 구버전 필드를 임시로 읽어 `Properties`에 채운 뒤 JSON 생성 시 기존 필드를 더 이상 쓰지 않도록 한다.

## 8. C# 코드베이스 영향
- 확장 프로젝트(Hmc.*) 등 C# 코드에서 `Project.Author`처럼 직행 접근하던 부분을 `Properties` 경유로 변경한다.
- 필요한 경우 확장 타입에 래퍼 프로퍼티를 제공해 외부 코드의 수정 폭을 줄인다.

## 9. 테스트 & 샘플 데이터
- 유닛 테스트, 샘플 JSON, To/From AAS 테스트 등에서 기대값을 `Properties` 구조로 맞춘다.
- Json 비교 테스트는 `Properties` 내부 필드까지 검증하도록 업데이트한다. 기존 테스트가 개별 필드를 직접 비교했다면 `Diff("Properties", …)` 형태로 수정한다.
- 스크립트/도구에서 생성한 예제 파일이 있다면 재생성하거나 문서화한다.

## 10. 검증 절차
1. `dotnet build` (하위 프로젝트 포함) 실행.
2. 가능하면 관련 `dotnet test` 수행.
3. JSON/AAS Round-trip 테스트로 구조 변경이 반영되었는지 확인.
4. DB 스키마 차이를 검토하고 문서화한다.

## 11. 참고 팁
- `MiniSample.create()` 같은 샘플 생성 함수에서 기본 메타 데이터를 채워 두면 테스트가 한결 수월하다.
- F# Interactive를 사용할 경우 `ModuleInitializer` 의존성이 있는지 확인하고 초기화 코드를 함께 호출한다.
- 변경 전후 JSON을 `EmJson.IsJsonEquals` 등으로 비교해 regressions 를 빠르게 탐지한다.
- DB 스키마를 단일 `properties` 컬럼으로 축약할 때는 `docs/Spec/Data/*/sqlite-schema.sql` 등 버전별 샘플도 빠뜨리지 말고 갱신한다.
- `MiniSample.create()` 같은 샘플 생성 함수는 새 `Properties` 경로를 사용하도록 즉시 고친다. (샘플이 런타임·테스트 동시 검증 포인트이므로 누락 시 테스트에서 놓치기 쉽다.)

