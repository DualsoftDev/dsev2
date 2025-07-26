# Project, NjProject, ORMProject의 AasXml Member 처리

## 최종 구현 완료 (2025-07-26)

### 🔄 구현 방향 변경: AasXml 멤버 완전 제거

**기존 접근법의 문제점**:
- Project 객체에 큰 XML 문자열을 저장하여 메모리 사용량 증가
- 객체 복사/비교 시 불필요한 오버헤드
- 데이터 일관성 관리의 복잡성

**새로운 접근법**:
- **AasXml 멤버 완전 제거**: 메모리 사용량 최적화
- **별도 메서드로 XML 관리**: 필요시에만 데이터베이스 업데이트
- **명확한 책임 분리**: XML 저장과 Project 객체 분리

---

## ✅ 최종 구현 사항

### 1. **Project.UpdateDbAasXml 정적 메서드 구현** (`AasX.fs:190`)

```fsharp
static member UpdateDbAasXml(project: Project, aasxPath: string, dbApi: AppDbApi): unit =
    // 1. AASX 파일에서 원본 XML 읽기
    let aasFileInfo = readEnvironmentFromAasx aasxPath
    let originalXml = aasFileInfo.OriginalXml
    
    // 2. 프로젝트 ID 확인 
    let projectId = project.Id |? failwith "Project Id is not set"
    
    // 3. 데이터베이스에서 aasXml 컬럼만 업데이트
    dbApi.With(fun (conn, tr) ->
        let affectedRows = conn.Execute($"UPDATE {Tn.Project} SET aasXml = @AasXml WHERE id = @Id", 
            {| AasXml = originalXml; Id = projectId |}, tr)
        if affectedRows = 0 then
            failwith $"Project with Id {projectId} not found for AasXml update"
    )
```

### 2. **AasXml 멤버 완전 제거**

- **Project** (`Interfaces.fs`): `member val AasXml = nullString` 제거
- **NjProject** (`NewtonsoftJsonDsObjects.fs`): `member val AasXml = nullString` 제거  
- **ORMProject** (`Database.ORM.fs`): `member val AasXml = nullString` 제거

### 3. **기존 코드 수정**

#### ToAasXmlString 메서드 (`AasX.fs:21`)
```fsharp
member x.ToAasXmlString(): string =
    // 항상 현재 상태에서 XML 생성 (AasXml 멤버 제거됨)
    let env = x.ToENV()
    serializeEnvironmentToXml env
```

#### Export 메서드들
- **ExportToAasxFile**: AasXml 설정 코드 제거
- **InjectToExistingAasxFile**: AasXml 설정 코드 제거
- **FromAasxFile**: AasXml 전달 코드 제거

#### 데이터베이스 관련
- **DB.Insert.fs**: INSERT 쿼리에서 `aasXml` 필드 제거
- **AppDbApi.fs**: Project → ORMProject 변환에서 AasXml 처리 제거

#### 객체 처리
- **DsCopy.Properties.fs**: 복사 작업에서 AasXml 처리 제거
- **DsCompare.Objects.fs**: 비교 기준에서 AasXml 제거

---

## 💡 개선 효과

### 1. **메모리 사용량 대폭 감소**
- Project 객체에서 큰 XML 문자열 제거
- 객체 복사/이동 시 성능 향상

### 2. **명확한 책임 분리**
- **Project 객체**: 프로젝트 논리적 데이터만 포함
- **UpdateDbAasXml**: AAS XML 저장 전용 메서드
- **ToAasXmlString**: 현재 상태에서 XML 생성

### 3. **데이터 일관성 향상**
- XML 저장이 명시적으로 관리됨
- 불필요한 XML 동기화 문제 해결

### 4. **성능 최적화**
- 객체 비교/복사에서 XML 처리 제거
- 메모리 캐싱 오버헤드 제거

---

## 🔧 사용 방법

### 기본 사용법
```fsharp
// 1. Project 객체 생성 (가벼운 객체)
let project = Project.FromAasxFile("input.aasx")

// 2. 현재 상태에서 AAS XML 생성
let xmlString = project.ToAasXmlString()

// 3. 필요시 데이터베이스에 AAS XML 저장
Project.UpdateDbAasXml(project, "source.aasx", dbApi)
```

### 데이터베이스 스키마
- **Tn.Project 테이블**: `aasXml TEXT` 컬럼 유지
- **별도 업데이트**: `UpdateDbAasXml` 메서드로만 관리

---

## 📋 변경 사항 요약

| 구분 | 이전 구현 | 현재 구현 |
|------|-----------|-----------|
| **AasXml 멤버** | 모든 Project 타입에 존재 | 완전 제거 |
| **XML 저장** | 객체 생성/수정 시 자동 | 명시적 메서드 호출 |
| **메모리 사용** | XML 문자열 상시 보관 | 필요시에만 생성 |
| **데이터베이스** | INSERT/UPDATE 시 포함 | 별도 UPDATE 전용 |
| **객체 복사** | AasXml 포함 복사 | AasXml 제외 |

---

## 🗂️ 이전 구현 기록 (참고용)

### 1차 구현 (AasXml 멤버 활용)
- AASX 파일에서 원본 XML을 AasXml 멤버에 저장
- ToAasXmlString에서 캐싱된 XML 우선 반환
- Export 시 새로 생성된 XML을 AasXml에 저장

### 문제점 및 개선 동기
- 메모리 사용량 과다
- 객체 복사/비교 시 성능 저하
- 데이터 동기화 복잡성

### 최종 해결책
- **AasXml 멤버 완전 제거**
- **별도 메서드로 XML 관리**
- **성능 및 메모리 최적화**

---

**결론**: AasXml 멤버를 제거하고 별도 메서드로 관리함으로써 Project 객체의 성능과 메모리 효율성을 크게 향상시켰습니다.