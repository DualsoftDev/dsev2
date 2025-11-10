# Flow/Work/Call Properties Refactor Notes

## 배경
- commit `68c9973ebb49df0147becd4baef23081e88b536c` 이후 Flow, Work, Call에 `Properties`를 도입하면서 JSON/DB/AAS 왕복이 끊어짐.
- 반복되는 JSON 직렬화/역직렬화 패턴과 DB/ORM 매핑 코드가 각 타입마다 중복되어 유지보수 비용이 컸음.

## 핵심 변경 요약
1. **Runtime 타입 (`AbstractClasses.fs`)**
   - Flow/Work/Call/ApiCall/ApiDef에 `Properties`/`PropertiesJson` 멤버 추가.
   - Setter는 공통 helper(`DsPropertiesHelper.assignFromJson`)를 사용해 JSON 문자열 → 속성 인스턴스 변환.

2. **Newtonsoft JSON 타입 (`NewtonsoftJsonDsObjects.fs`)**
   - Flow/Work/Call/ApiCall/ApiDef 및 대응 Nj 타입에 `[<JsonProperty(Order = 99)>] Properties` 추가.
   - 직렬화/역직렬화 시 helper(`cloneProperties`) 사용해 parent 설정/DeepClone 반복 제거.

3. **복제/비교 (`DsCopy.*`, `DsCompare.Objects.fs`)**
   - Flow/Work/Call/ApiCall/ApiDef properties JSON을 복제/복사/비교 루틴에 통합.
   - `DsCopy.Properties.fs`를 한줄 할당 형태로 정리해 가독성 확보.

4. **DB & ORM (`Database.Schema.fs`, `Database.ORM.fs`, `DB.Insert.fs`, `DB.Select.fs`, `AppDbApi.fs`)**
   - Flow/Work/Call/ApiCall/ApiDef 테이블에 `properties` JSON 컬럼 추가.
   - 관련 ORM 타입에 `PropertiesJson` 멤버 추가, Insert/Select 경로에서 값 이동.

5. **AAS 라운드트립 (`Core.To.Aas.fs`, `Core.From.Aas.fs`)**
   - Flow/Work/Call/ApiCall/ApiDef의 Properties를 SubmodelElementCollection에 기록/복원.

6. **공통 Helper (`AbstractBaseClasses.fs`)**
   - `DsPropertiesHelper` 모듈에 `assignFromJson`/`cloneProperties` 추가.
   - owner 타입 제약을 일반화해 Flow/Work/Call/NjFlow/...에서 재사용.

## 마이그레이션/테스트 유의 사항
- DB 스키마 변경: `flow.properties`, `work.properties`, `call.properties` 컬럼 추가 필요.
- HmcConsoleApp 기준 round-trip 테스트 필수 (JSON ↔ DB ↔ AASX).
- 확장 타입(CustomProject 등)이 Flow/Work/Call properties를 사용하는 경우 DeepClone 및 parent 설정이 올바른지 확인.

## Follow-up
- DB 마이그레이션 스크립트 준비.
- `DsPropertiesHelper` 확대 적용 가능성 검토 (다른 Properties 계열에도 동일 패턴 존재 여부 확인).
