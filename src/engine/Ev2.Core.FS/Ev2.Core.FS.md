# Ev2.Core.FS

## 프로젝트 개요

Ev2.Core.FS는 DS Engine Version 2의 핵심 구현 프로젝트입니다. 산업 자동화 시스템을 위한 도메인 모델과 비즈니스 로직을 F# 함수형 프로그래밍 패러다임으로 구현합니다.

## 의존성 정보

### Target Framework
- **TargetFramework**: netstandard2.0
- **LangVersion**: F# 9.0
- **OutputType**: Library

### NuGet 패키지 참조
- **System.Reactive** (6.0.1) - 반응형 프로그래밍
- **log4net** (2.0.17) - 로깅 프레임워크
- **Newtonsoft.Json** (13.0.3) - JSON 직렬화
- **Dapper** (2.1.35) - Micro ORM

### 프로젝트 참조
- Dual.Common.Base.FS - 공통 기반 라이브러리
- Dual.Common.Core.FS - 공통 핵심 라이브러리  
- Dual.Common.Db.FS - 공통 데이터베이스 라이브러리

## 파일 구조

```
Ev2.Core.FS/
├── Ev2.Core.FS.fsproj          # 프로젝트 파일
├── AppSettings.fs              # 애플리케이션 설정
├── ConstEnums.fs              # 상수 및 열거형
├── Interfaces.fs              # 핵심 인터페이스 정의
├── Unique.fs                  # 고유 식별자 관리
├── TypeFactory.fs             # 타입 팩토리 (확장 시스템)
├── AbstractClasses.fs         # 추상 클래스
├── DsObjectUtils.fs          # 도메인 객체 유틸리티
├── Ev2CoreExtensionForCSharp.fs # C# 호환성 확장
├── MiniSample.fs             # 샘플 데이터 생성
├── EntryPoint.fs             # 모듈 초기화 진입점
├── AssemblyInfo.fs           # 어셈블리 정보
├── appSettings.json          # 설정 파일
├── appSettings.json.template # 설정 템플릿
├── database/                 # 데이터베이스 레이어
│   ├── Database.Schema.fs    # 스키마 정의
│   ├── Database.ORM.fs       # ORM 매핑
│   └── AppDbApi.fs          # 데이터베이스 API
├── TypeConversion,Serialization/ # 타입 변환 및 직렬화
│   ├── NewtonsoftJsonDsObjects.fs # JSON 직렬화
│   ├── DsCopy.Object.fs      # 객체 복사
│   ├── DsCopy.Properties.fs  # 속성 복사
│   ├── DsCompare.Objects.fs  # 객체 비교
│   ├── DB.Select.fs          # 데이터베이스 조회
│   ├── DB.Insert.fs          # 데이터베이스 삽입
│   ├── DB.Update.fs          # 데이터베이스 업데이트
│   ├── DB.API.fs             # 데이터베이스 API
│   └── Ds2Aas.fs            # AAS 변환
├── Extension/                # 확장 시스템 폴더
└── DevDoc/                   # 개발 문서
    ├── DbConstraint.md
    ├── ErrorMessages.md
    ├── 속성 추가 절차.md
    ├── 저장 포맷.md
    └── 확장.md
```

## 주요 컴포넌트

### 핵심 도메인 모델 (`Interfaces.fs`)
산업 자동화 시스템의 계층적 구조를 정의합니다:

- **IDsProject** - 프로젝트 (최상위 컨테이너)
- **IDsSystem** - 시스템 (자동화 시스템)
- **IDsFlow** - 플로우 (UI 요소들)
- **IDsWork** - 작업 (실행 가능한 단위)
- **IDsCall** - 호출 (개별 실행 단계)
- **IDsApiCall/IDsApiDef** - API 정의 및 호출

### 타입 시스템 아키텍처
세 가지 타입 표현을 지원합니다:

1. **Runtime Types (IRt\*)** - 런타임 비즈니스 객체
2. **JSON Types (INj\*)** - Newtonsoft.Json 직렬화용
3. **ORM Types (IORM\*)** - 데이터베이스 매핑용

### 확장 시스템 (`TypeFactory.fs`)
Third Party 확장을 위한 인터페이스를 제공합니다:

- **ITypeFactory** - 런타임/JSON 타입 생성
- **ISchemaExtension** - SQL 스키마 확장
- C# 호환성을 위한 인터페이스 설계

### 데이터베이스 레이어
- **Database.Schema.fs** - SQL 스키마 정의
- **Database.ORM.fs** - Dapper 기반 ORM 매핑  
- **AppDbApi.fs** - 고수준 데이터베이스 API

### 타입 변환 시스템
다양한 타입 간 변환을 담당합니다:
- Runtime ↔ JSON 변환
- Runtime ↔ ORM 변환  
- 객체 복사 및 비교
- AAS 형식 변환

### 모듈 초기화 (`EntryPoint.fs`)
시스템 초기화를 담당합니다:
- `ModuleInitializer.Initialize()` - 전역 초기화
- 함수 포인터 설정
- 설정 파일 로딩

## 설계 원칙

### 이중성(Duality) 원리
객체가 맥락에 따라 다른 역할을 수행할 수 있는 개념:
- 구조적 이중성: 시스템 ⊕ 디바이스
- 실행적 이중성: 원인 ⊕ 결과

### 함수형 프로그래밍
- 불변 데이터 구조
- 타입 안전성
- 순수 함수와 부작용 분리

### 확장성
- Third Party 타입 확장 지원
- C# 상호 운용성
- 플러그인 아키텍처

## 빌드 명령어

### 로컬 빌드
```bash
cd F:\Git\aas\submodules\dsev2\src
dotnet build Ev2.Core.FS/Ev2.Core.FS.fsproj
```

### 전체 솔루션 빌드
```bash
cd F:\Git\aas\submodules\dsev2\src  
dotnet build dsev2.sln
```

### 릴리스 빌드
```bash
dotnet build Ev2.Core.FS/Ev2.Core.FS.fsproj --configuration Release
```

### 문서 생성
```bash
dotnet build Ev2.Core.FS/Ev2.Core.FS.fsproj --verbosity detailed
```

## 테스트

관련 테스트 프로젝트:
- `../unit-test/UnitTest.Core/UnitTest.Core.fsproj`

```bash
# 핵심 기능 테스트 실행
cd F:\Git\aas\submodules\dsev2\src
dotnet test unit-test/UnitTest.Core/UnitTest.Core.fsproj
```

## 주요 특징

- **확장 시스템**: Third Party 타입 확장 지원
- **다중 직렬화**: JSON, Database, AAS 형식 지원
- **C# 호환성**: C# 프로젝트와의 상호 운용성
- **타입 안전성**: F# 타입 시스템 활용
- **모듈화**: 계층별 명확한 분리

## 관련 프로젝트

- **Ev2.Aas.FS** - AAS 직렬화/역직렬화
- **Hmc.Aas** - Third Party 확장 예시
- **HmcConsoleApp** - 콘솔 테스트 애플리케이션