# Ev2.Cpu.Core 개발 사양

## 1. 개요
- **목표**: PLC 스타일 DSL을 위한 공통 타입, AST, 파서, 연산자 정의를 제공하여 모든 상위 계층(Runtime, CodeGen)이 동일한 도메인 모델을 공유하도록 한다.
- **출력 아티팩트**: `Ev2.Cpu.Core.dll`
- **테스트 프로젝트**: `src/Ev2.Cpu.Core.Tests` (121 tests) – 타입/연산/AST DSL 회귀 검증.

## 2. 모듈 구조
| 영역 | 주요 파일 | 책임 |
| --- | --- | --- |
| 데이터 타입 | `Core/Types.fs`, `Core/TypeConversion.fs`, `Core/TypeValidation.fs` | `DataType`/`DsType` 정의, 기본값/검증/변환 규칙, 스코프 검증(`validateScopePath`). |
| 연산자 | `Core/Operators.fs`, `Core/OperatorDefinitions.fs`, `Core/OperatorParsing.fs`, `Core/OperatorValidation.fs`, `Core/OperatorCatalog.fs` | `DsOp`/`DsOperator` 메타데이터, 파서 에일리어스, 피연산자 검증 로직. |
| AST (신형) | `Ast/Expression.fs`, `Ast/Statement.fs`, `Ast/Program.fs` | `DsExpr`, `DsStatement`, 프로그램 모델 (`DsFormula`) 과 타입 추론/검증/포매팅.
| AST (구조적 DSL) | `Struct/*.fs` | 레거시 래더 DSL (`DsTag`, `DsExpr`, `DsStmt`) 및 빌더/포매터. CodeGen/Runtime와의 호환성을 유지. |
| 파서 | `Parsing/Parser.fs` | 토큰화/구문 분석, 연산자 우선순위 파싱, 에러 메시지. |
| 레거시 아카이브 | `Legacy/Cpu.Core.fs` | 구형 통합 모듈(참고용). 신규 코드는 사용하지 않되 기능 파악에 참고.

## 3. 외부 노출 & 의존성
- 공개 네임스페이스: `DsRuntime.Cpu.Core`, `DsRuntime.Cpu.Ast`, `Ev2.Cpu.FS` (레거시 DSL)
- NuGet 의존성 없음.
- 하위 프로젝트에서 참조할 때는 DSL helper가 필요한 경우 `open Ev2.Cpu.FS.Expression` / `Statement` 를 사용.

## 4. 설계 규칙
1. **타입 일관성**: `DataType` ↔ `DsType` 맵핑을 수정할 경우 `TypeConversion`/`TypeValidation`, AST 타입 추론, CodeGen/Runtime 테스트를 동시에 수정.
2. **연산자 추가**: `DsOp`/`DsOperator` 를 모두 확장하고, 파서(alias)와 검증(`OperatorValidation.validateUnary/Binary`)을 반드시 업데이트. 테스트: `Core.Operators.Test.fs`.
3. **AST 확장**: 새로운 `DsExpr`/`DsStatement` 케이스 추가 시 `Expression.fs`/`Statement.fs` 의 `InferType`, `Validate`, 작동 테스트(`Ast.Expression.Test.fs`, `Statement.Test.fs`) 갱신.
4. **레거시 DSL 유지**: `Struct/*.fs` 엔트리가 CodeGen/Runtime에서 직접 사용되므로 시그니처 변경 시 해당 프로젝트와 테스트 동시 수정.

## 5. 빌드 & 테스트
```bash
# 단일 빌드
dotnet build src/Ev2.Cpu.Core/Ev2.Cpu.Core.fsproj
# 전체 테스트 (권장)
dotnet test src/dsev2cpu.sln --filter FullyQualifiedName~Ev2.Cpu.Core.Tests
```

## 6. TODO / 향후 과제
- `DsFormula` (Program.fs)의 버전 관리/메타데이터를 런타임에서 활용하도록 인터페이스 정리.
- 레거시 DSL/신형 AST 간 중복을 줄이기 위한 migration 계획 수립.
- Parser 오류 리포트에 소스 위치(행/열)를 포함하도록 확장.

## 7. 유지보수 로드맵

### 7.1 현황 진단 (AS-IS)
- 검증 로직이 Core/Generation/Runtime에 중복 구현되어 일관성이 깨진다.
- `DsOp` 메타데이터가 여러 위치에서 따로 관리되어 우선순위·호환 규칙이 쉽게 어긋난다.
- 스코프/태그 처리 과정이 수동 문자열 조합에 의존해 충돌 분석이 어렵다.
- 전역 싱글턴 `UserLibrary` 때문에 스레드 안전성과 테스트 독립성이 보장되지 않는다.
- AST 생성/검증 단계에 대한 관측 데이터가 부족하여 유지보수 비용이 높다.

### 7.2 목표 상태 (TO-BE)
- Core가 UserFC/FB, 스코프, 태그, 연산자 규칙을 단일 API로 제공하고 상위 레이어는 이를 호출만 한다.
- 명시적인 오류 타입과 상세 메시지를 통해 빌드 단계에서 조기 식별이 가능하다.
- 스코프 서비스화로 생성·충돌 감지·디버그 정보를 통합 제공한다.
- 불변 혹은 DI 기반 구조로 `UserLibrary`를 재구성해 멀티스레드와 테스트 환경을 보호한다.
- AST/태그 상태를 시각화·로깅하여 운영 및 디버그 가시성을 확보한다.

### 7.3 개선 과제
- **빌더/검증 통합**: Core Validation 모듈을 Generation/Runtime 빌더가 직접 사용하도록 API를 노출하고 호환 레이어를 정리한다.
- **스코프 서비스**: Scope/Tag 생성을 전담하는 서비스 객체를 도입하고 충돌 시 상세 오류와 디버그 정보를 제공한다.
- **연산자 메타데이터 일원화**: `DsOp` 정의, 파서 alias, 검증 규칙을 한 위치에서 선언하고 스니펫 테스트로 보호한다.
- **가시성 강화**: AST 생성/검증 파이프라인에서 로깅 훅을 제공하고 FSI/CLI 스크립트로 상태를 출력한다.
- **UserLibrary 재구성**: 불변 컬렉션 기반 또는 명시적 Locking 구조로 재작성하고 DI를 통한 인스턴스 주입을 지원한다.

<a id="core-test-roadmap"></a>
### 7.4 테스트 스위트 로드맵 (Ev2.Cpu.Core.Tests)
- **AS-IS**: Validation·Scope·Operator 규칙이 오래된 시나리오에 머물러 있고, 다양한 모듈에서 AST/태그 생성 헬퍼를 중복 사용하며 실패 메시지가 단순하다.
- **TO-BE**: 최신 ValidationResult 및 ScopeService를 직접 활용하고 공통 헬퍼를 모듈화하여 가독성과 유지보수를 높인다. 실패 시 입력, 기대/실제 AST, 스코프 정보를 구조화하여 제공하고 Edge-case 및 Property 기반 테스트로 커버리지를 확장한다.
- **주요 과제**
  - 테스트 파일 구조를 재편해 공통 헬퍼 모듈을 도입하고 수작업 스니펫을 제거한다.
  - 스냅샷·Property 테스트를 추가해 태그 충돌, 연산자 우선순위, 스코프 누락 등 경계 조건을 자동 검증한다.
  - 실패 메시지에 입력식, 기대/실제 AST, Validation 로그를 포함하는 진단 유틸리티를 도입한다.
