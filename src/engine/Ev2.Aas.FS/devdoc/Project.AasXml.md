# Project, NjProject, ORMProject의 AasXml Member 처리

## 구현 완료 (2025-07-26)

### AasXml의 올바른 의미 파악
- **기존 오해**: Environment 전체를 직렬화한 XML로 생각
- **실제 의미**: AASX 파일 내부의 실제 AAS XML 파일(`aasx/_rels/aasx-origin.rels`에서 정의된 Target XML) 내용 자체

### 구현된 기능

#### 1. **readEnvironmentFromAasx 함수 수정** (`AasX.fs:70`)
```fsharp
let readEnvironmentFromAasx (aasxPath: string): {| FilePath: string; Version: string; Environment: Aas.Environment; OriginalXml: string |}
```
- 반환 타입에 `OriginalXml: string` 필드 추가
- AASX ZIP 내부에서 읽은 원본 XML 문자열을 반환값에 포함

#### 2. **NjProject.FromAasxFile 수정** (`Core.From.Aas.fs:35`)
```fsharp
// AASX 파일에서 읽은 원본 XML을 AasXml 멤버에 저장
project.AasXml <- aasFileInfo.OriginalXml
```
- Environment를 다시 직렬화하는 대신 원본 XML 사용
- AASX 파일의 실제 XML 내용을 그대로 보존

#### 3. **ToAasXmlString 메서드 개선** (`AasX.fs:239`)
```fsharp
member x.ToAasXmlString(): string =
    // 기존 AasXml이 있으면 우선 반환
    if not (String.IsNullOrEmpty(x.AasXml)) then
        x.AasXml
    else
        // 없으면 현재 상태에서 XML 생성
        let env = x.ToENV()
        // ... XML 직렬화 로직
```
- **NjProject**: 기존 AasXml 우선 반환, 없으면 현재 상태에서 생성
- **Project**: AasXml을 NjProject로 전달하여 일관성 유지

#### 4. **Project 타입 간 AasXml 전달** (`AasX.fs:435-440`)
```fsharp
static member FromAasxFile(aasxPath: string): Project =
    let njProj = NjProject.FromAasxFile(aasxPath)
    let project = njProj.ToJson() |> Project.FromJson
    // NjProject의 AasXml을 Project로 전달
    project.AasXml <- njProj.AasXml
    project
```

### 동작 방식

1. **AASX 로드** (`FromAasxFile`)
   - AASX ZIP 파일에서 `aasx/_rels/aasx-origin.rels` 파싱
   - Target XML 파일 경로 추출 (`findAasXmlFilePath`)
   - 실제 XML 파일 내용을 문자열로 읽어서 AasXml에 저장

2. **XML 조회** (`ToAasXmlString`)
   - 기존 AasXml이 있으면 원본 XML 반환 (데이터 보존)
   - 없으면 현재 Project 상태에서 새로 생성

3. **AASX 내보내기** (`ExportToAasxFile`)
   - 현재 Project 상태에서 Environment 생성
   - 새로 직렬화된 XML을 AasXml에 저장

4. **AASX 업데이트** (`InjectToExistingAasxFile`)
   - 기존 Environment + 새 Project Submodel로 Environment 업데이트
   - 업데이트된 Environment를 직렬화하여 AasXml에 저장

### 구현하지 않은 항목
- **ORMProject.ToAasXmlString**: 데이터베이스 ORM 전용 클래스로 AAS 변환 기능 없음
- **수동 설정/조회 메서드**: 기본적인 AasXml 멤버 접근으로 충분

### 핵심 개선점
- **원본 XML 보존**: AASX에서 로드한 원본 XML 형식과 구조 유지
- **데이터 일관성**: Project ↔ NjProject 변환 시 AasXml 정보 유지
- **스마트 XML 반환**: 기존 데이터 우선, 필요시 재생성

이제 AasXml 멤버는 AASX 파일의 실제 XML 내용을 올바르게 저장하고 관리합니다.

---

## 이전 계획 (참고용)

### 현재 상황 분석
- Project, NjProject, ORMProject 모두 AasXml 멤버를 보유
- 현재 모든 클래스에서 nullString으로 초기화되어 사용되지 않음
- DsCopy.Properties.fs에서 복사 작업 시 포함되어 있음
- DsCompare.Objects.fs에서 비교 대상으로 포함되어 있음

### 구현 우선순위 (완료)
1. ✅ High: NjProject.FromAasxFile에서 AasXml 설정
2. ✅ High: Export 메서드들에서 AasXml 업데이트  
3. ✅ Medium: ToAasXmlString 메서드 추가
4. ❌ Low: 수동 설정/조회 메서드 추가 (불필요하여 생략)