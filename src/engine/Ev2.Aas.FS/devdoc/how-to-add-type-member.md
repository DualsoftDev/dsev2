# DS 타입에 멤버 추가 가이드

DS 시스템의 타입(Project, DsSystem, Flow, Work, Call 등)에 새로운 멤버를 추가할 때 수행해야 할 작업 목록입니다.

## 필수 작업 목록

### 1. 런타임 타입에 속성 추가
- **파일**: `src/engine/Ev2.Core.FS/AbstractClasses.fs`
- **작업**: 해당 클래스에 `member val PropertyName = defaultValue with get, set` 추가
- **예시**: `member val ExternalStart = nullString with get, set`

### 2. ORM 타입에 속성 추가
- **파일**: `src/engine/Ev2.Core.FS/database/Database.ORM.fs`
- **작업**: 
  - ORM 클래스에 속성 추가
  - `Initialize` 메서드에서 런타임 객체로부터 값 복사 로직 추가
- **예시**: 
  ```fsharp
  member val ExternalStart = nullString with get, set
  // Initialize 메서드 내
  x.ExternalStart <- runtime.ExternalStart
  ```

### 3. 데이터베이스 스키마 업데이트
- **파일**: `src/engine/Ev2.Core.FS/database/Database.Schema.fs`
- **작업**: 해당 테이블에 새 컬럼 정의 추가
- **예시**: `, {k "externalStart"} TEXT`

### 4. JSON 타입에 속성 추가
- **파일**: `src/engine/Ev2.Core.FS/TypeConversion,Serialization/NewtonsoftJsonDsObjects.fs`
- **작업**: NjXXX 클래스에 속성 추가
- **예시**: `member val ExternalStart = nullString with get, set`

### 5. 타입 변환 로직 업데이트
- **파일**: `src/engine/Ev2.Core.FS/TypeConversion,Serialization/DsCopy.Properties.fs`
- **작업**: 
  - source 매칭 패턴에 새 속성 추가
  - destination 할당 로직에 새 속성 추가
- **예시**: 
  ```fsharp
  // source 매칭
  | :? Work as s -> {| ... ExternalStart=s.ExternalStart ... |}
  // destination 할당  
  | :? Work as d -> ... d.ExternalStart<-s.ExternalStart ...
  ```

### 6. 비교 로직 업데이트
- **파일**: `src/engine/Ev2.Core.FS/TypeConversion,Serialization/DsCompare.Objects.fs`
- **작업**: 해당 타입의 `ComputeDiff` 메서드에 새 속성 비교 로직 추가
- **예시**: `if x.ExternalStart <> y.ExternalStart then yield Diff(nameof x.ExternalStart, x, y, null)`

### 7. 데이터베이스 INSERT/UPDATE 로직 업데이트
- **파일**: `src/engine/Ev2.Core.FS/TypeConversion,Serialization/DB.Insert.fs`
- **작업**: INSERT SQL에 새 컬럼과 파라미터 추가
- **예시**: 
  ```fsharp
  // 컬럼 리스트에 추가
  (guid, parameter, name, ..., externalStart, ...)
  // VALUES에 파라미터 추가
  VALUES (@Guid, @Parameter, @Name, ..., @ExternalStart, ...)
  ```

### 8. AAS 직렬화 지원 (선택적)
만약 AAS 형식으로 내보내기가 필요한 경우:

#### 8a. AAS 변환 (To AAS)
- **파일**: `src/engine/Ev2.Aas.FS/Core.To.Aas.fs`
- **작업**: 해당 타입의 변환 로직에 새 속성 추가
- **예시**: `JObj().TrySetProperty(work.ExternalStart, nameof work.ExternalStart)`

#### 8b. AAS 역변환 (From AAS)
- **파일**: `src/engine/Ev2.Aas.FS/Core.From.Aas.fs`
- **작업**: AAS에서 객체 생성 시 새 속성 읽기 추가
- **예시**: 
  ```fsharp
  let externalStart = smc.TryGetPropValue "ExternalStart" |? null
  // NjXXX 생성 시 속성 설정
  ExternalStart=externalStart
  ```

#### 8c. AAS 시맨틱 매핑 (선택적)
- **파일**: `src/engine/Ev2.Aas.FS/Core.Aas.fs`
- **작업**: 필요시 새 속성을 위한 시맨틱 URL 매핑 추가
- **예시**: `("ExternalStart", "https://dualsoft.com/aas/work/externalStart")`

### 9. 테스트 데이터 업데이트 (선택적)
- **파일**: `src/engine/Ev2.Core.FS/MiniSample.fs`
- **작업**: 샘플 데이터에 새 속성 값 설정
- **예시**: `w.ExternalStart <- "StartCommand1"`

## 작업 순서 권장사항

1. **1단계**: 런타임 타입 → ORM 타입 → 데이터베이스 스키마 (핵심 구조)
2. **2단계**: JSON 타입 → 타입 변환 로직 (직렬화 지원)
3. **3단계**: 비교 로직 → DB INSERT/UPDATE (비즈니스 로직)
4. **4단계**: AAS 지원 (선택적)
5. **5단계**: 테스트 및 검증

## 검증 방법

### 빌드 테스트
```bash
dotnet build src/dsev2.sln --verbosity minimal
```

### 단위 테스트 실행
```bash
dotnet test src/unit-test/UnitTest.Core/UnitTest.Core.fsproj
dotnet test src/unit-test/UnitTest.Aas/UnitTest.Aas.fsproj  # AAS 지원 시
```

### 라운드 트립 테스트
새로운 속성이 다음 과정을 거쳐 올바르게 보존되는지 확인:
1. 런타임 객체 생성 → 속성 설정
2. 데이터베이스 저장
3. 데이터베이스에서 읽기
4. 속성 값 확인

## 주의사항

- **스키마 변경**: 데이터베이스 스키마 변경 시 기존 데이터 마이그레이션 고려
- **타입 안전성**: nullable/non-nullable 타입 일관성 유지
- **네이밍**: 기존 코드와 일관된 네이밍 규칙 사용 (PascalCase)
- **기본값**: `nullString`, `0`, `false` 등 적절한 기본값 설정
- **성능**: 인덱스가 필요한 속성의 경우 데이터베이스 인덱스 고려

## 예제: ExternalStart 속성 추가

이 가이드는 Work 타입에 ExternalStart 문자열 속성을 추가한 실제 사례를 바탕으로 작성되었습니다. 각 단계의 구체적인 구현은 해당 파일들에서 `ExternalStart`를 검색하여 참고할 수 있습니다.

## 관련 문서

- `Ev2.Core.FS.md`: 핵심 엔진 아키텍처
- `Ev2.Aas.FS.md`: AAS 통합 가이드
- `aas.md`: 전체 시스템 개요