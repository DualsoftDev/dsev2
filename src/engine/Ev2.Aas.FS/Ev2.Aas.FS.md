# Ev2.Aas.FS

## 프로젝트 개요
- **프로젝트명**: Ev2.Aas.FS
- **타입**: F# 라이브러리
- **프레임워크**: .NET 9.0
- **언어 버전**: F# 9.0 (프로젝트 파일에서 LangVersion 9.0 설정)
- **네임스페이스**: Dual.Ev2.Aas
- **역할**: Asset Administration Shell (AAS) 3.0 표준 통합 및 AASX 파일 처리

## 설명
Ev2.Core.FS 프로젝트의 AAS (Asset Administration Shell) 패키지 탐색기 형식 파일에 대한 직렬화/역직렬화 기능을 담당합니다. AasCore.Aas3_0 패키지를 사용하여 Runtime 타입 ↔ NJ타입 ↔ AASX 간의 변환을 처리합니다.

### 주요 기능
- AAS 3.0 표준 준수 XML/JSON 직렬화
- AASX 패키지 파일 읽기/쓰기
- Runtime 객체와 AAS 형식 간 변환
- 확장 속성 지원을 위한 semantic URL 처리
- C#/F# 상호운용성을 위한 확장 메서드

## 의존성 정보

### TargetFramework
- `net9.0`: .NET 9.0 (AasCore.Aas3_0 호환성 요구사항)

### 프로젝트 참조
- `Ev2.Core.FS`: 핵심 도메인 모델 및 비즈니스 로직

### 패키지 참조
- `AasCore.Aas3_0`: AAS 3.0 표준 구현
  - 조건부 참조: 로컬 프로젝트 우선, 없으면 NuGet 패키지 (v1.0.0) 사용
  - 로컬 경로: `..\..\..\submodules\aas-core3.0-csharp\src\AasCore.Aas3_0\`
  - 주석 처리된 패키지: `System.IO.Packaging` (v9.0.6)

## 파일 구조

```
Ev2.Aas.FS/
├── Ev2.Aas.FS.fsproj          # 프로젝트 파일
├── CLAUDE.md                  # 프로젝트 지침
├── Ev2.Aas.FS.md             # 이 문서
│
├── 핵심 소스 파일/
│   ├── Prelude.fs             # 공통 유틸리티 및 상수
│   ├── Core.Aas.fs            # AAS 핵심 타입 및 변환 로직
│   ├── AAS.Extensions.fs      # AAS 타입 확장 메서드
│   ├── Core.To.Aas.fs         # Core → AAS 변환
│   ├── Core.From.Aas.fs       # AAS → Core 변환
│   ├── AasX.fs                # AASX 패키지 처리
│   └── Ev2AasExtensionForCSharp.fs  # C# 상호운용성 확장
│
├── 참조 파일/
│   ├── AasSyntax.md           # AAS 구문 가이드
│   ├── maximal.json           # AAS 최대 구성 예제
│   └── minimal.json           # AAS 최소 구성 예제
│
├── 개발 문서/
│   └── devdoc/
│       ├── AAS type 정의.md   # AAS 타입 정의
│       ├── AAS 검증.md        # AAS 검증 가이드
│       ├── Project.AasXml.md  # 프로젝트 AAS XML 매핑
│       └── SampleAASFolder/   # AAS 샘플 폴더 구조
│           ├── _rels/         # 관계 파일
│           ├── aasx/          # AASX 내부 구조
│           └── [Content_Types].xml
│
└── 빌드 출력/
    ├── bin/
    └── obj/
```

## 주요 컴포넌트

### 1. 핵심 모듈

#### Prelude.fs
- 공통 유틸리티 함수 (문자열 처리, 따옴표 처리)
- AAS 관련 상수 정의 (`SubmodelIdShort`)
- 내부 헬퍼 함수들

#### Core.Aas.fs
- **AasSemantics**: Semantic URL 매핑 테이블
  - Project, System, Flow, Work, Call 등 도메인 객체 매핑
  - Unique 식별자 (Name, Guid, Id, Parameter) 매핑
  - 확장 속성을 위한 `/extension/` 패턴 지원
- **JSON 타입 별칭**: `JNode`, `JObj`, `JArr` (System.Text.Json 축약)

#### AAS.Extensions.fs
- **IHasSemantics 확장**: SemanticId 키 매칭 유틸리티
- **UniqueInfo 타입**: AAS 고유 정보 구조체
- AAS 타입에 대한 F# 확장 메서드들

### 2. 변환 모듈

#### Core.To.Aas.fs
- **NjUnique → AAS 변환**: 
  - `tryCollectPropertiesNjUnique()`: 기본 속성 수집
  - 확장 속성 처리 (주석 처리된 기능)
- Runtime 객체를 AAS JSON/XML 형식으로 변환

#### Core.From.Aas.fs
- **AAS → Core 변환**:
  - 확장 속성 semantic URL 파싱 (주석 처리된 기능)
  - AAS 형식에서 Runtime 객체 생성

#### AasX.fs
- **AASX 패키지 처리**:
  - `ToSjENV()`: Submodel JSON 환경 생성
  - `ExportToAasxFile()`: AASX 파일 내보내기
  - `InjectToExistingAasxFile()`: 기존 AASX에 Submodel 주입
- ZIP 압축/해제 기능
- AAS XML/JSON 직렬화

### 3. 상호운용성

#### Ev2AasExtensionForCSharp.fs
- **C# 전용 확장 메서드**:
  - `CsExportToAasxFile()`: AASX 파일 내보내기
  - `CsInjectToExistingAasxFile()`: 기존 AASX 주입
  - `CsUpdateDbAasXml()`: 데이터베이스 AAS XML 업데이트
  - `CsToAasJsonString()`: AAS JSON 문자열 변환
  - `CsToENV()`: AAS 환경 생성
- `[<Extension>]` 속성을 통한 C# 접근성 보장

## 아키텍처 특징

### 타입 변환 체계
```
Runtime Type (Project, DsSystem, etc.)
    ↕
NJ Type (NjProject, NjSystem, etc.)
    ↕
AAS JSON/XML
    ↕
AASX Package File
```

### Semantic URL 체계
- **기본 객체**: `https://dualsoft.com/aas/{type}`
- **확장 속성**: `https://dualsoft.com/aas/extension/{type}/{property}`
- **고유 식별자**: `https://dualsoft.com/aas/unique/{identifier}`
- **복수 컬렉션**: `https://dualsoft.com/aas/plural/{collection}`

### 확장 메커니즘
- 확장 타입의 속성을 AAS semantic URL로 매핑
- 런타임에 동적 타입 검색 및 속성 수집
- JSON $type 필드를 통한 타입 식별

## 빌드 명령어

### 개발 환경 빌드
```bash
# 프로젝트 빌드
dotnet build Ev2.Aas.FS.fsproj

# Release 모드 빌드
dotnet build Ev2.Aas.FS.fsproj --configuration Release

# 의존성 복원
dotnet restore Ev2.Aas.FS.fsproj
```

### 솔루션 레벨 빌드
```bash
# 전체 솔루션 빌드 (상위 디렉토리에서)
cd ../../
dotnet build dsev2.sln

# 테스트 실행 (AAS 기능 테스트)
dotnet test ../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj
```

### 패키지 참조 확인
```bash
# 패키지 종속성 확인
dotnet list package

# 프로젝트 참조 확인
dotnet list reference
```

## 테스트

### 단위 테스트 프로젝트
- **위치**: `../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj`
- **테스트 범위**: 
  - AASX 변환 테스트
  - AAS 표준 호환성 검증
  - 확장 속성 직렬화/역직렬화
  - JSON/XML 변환 정확성

### 테스트 실행
```bash
# AAS 테스트 실행
dotnet test ../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj

# 상세 출력으로 테스트
dotnet test ../unit-test/UnitTest.Aas/UnitTest.Aas.fsproj --verbosity normal
```

## 사용 예시

### F# 사용법
```fsharp
open Dual.Ev2.Aas
open Ev2.Core.FS

// Project를 AASX로 내보내기
let project = (* 생성된 Project 객체 *)
project.ExportToAasxFile("output.aasx")

// AAS JSON 문자열 생성
let aasJson = project.ToAasJsonString()
```

### C# 사용법
```csharp
using Dual.Ev2.Aas;
using Ev2.Core.FS;

// C# 확장 메서드 사용
var project = /* 생성된 Project 객체 */;
project.CsExportToAasxFile("output.aasx");

// AAS JSON 문자열 생성
var aasJson = project.CsToAasJsonString();
```

## 관련 표준

### AAS (Asset Administration Shell) 3.0
- **표준 기관**: Industrial Digital Twin Association (IDTA)
- **목적**: Industry 4.0 디지털 트윈 표준화
- **파일 형식**: AASX (ZIP 기반 패키지), JSON, XML

### 지원 형식
- **AASX**: 압축된 AAS 패키지 (권장)
- **JSON**: 웹 API 및 클라우드 통합용
- **XML**: 엔터프라이즈 시스템 통합용

## 문제 해결

### 일반적인 문제
1. **AasCore.Aas3_0 패키지 오류**: .NET 9.0 프레임워크 확인
2. **Semantic URL 충돌**: AasSemantics.map 테이블 확인
3. **확장 속성 누락**: 타입 등록 및 TypeFactory 확인

### 디버깅 팁
- AAS JSON 출력을 통한 변환 결과 확인
- 단위 테스트를 통한 개별 기능 검증
- 로그 출력을 통한 변환 과정 추적

## 향후 개발 계획
- 확장 속성 처리 기능 완성 (현재 주석 처리됨)
- AAS 4.0 표준 대응 준비
- 성능 최적화 (대용량 AASX 파일 처리)
- 추가 AAS 검증 규칙 구현