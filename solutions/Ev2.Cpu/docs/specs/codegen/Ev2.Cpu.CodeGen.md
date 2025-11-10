# Ev2.Cpu.CodeGen 개발 사양

## 1. 개요
- **목표**: Core DSL을 Structured Text(ST) 기반 PLC 코드로 변환하는 코드 생성 파이프라인을 제공한다.
- **출력 아티팩트**: `Ev2.Cpu.CodeGen.dll`
- **테스트 프로젝트**: `src/Ev2.Cpu.CodeGen.Tests` (33 tests) – Work/Flow/System 패턴, 릴레이/타이머 DSL 검증.

## 2. 모듈 구조
| 영역 | 주요 파일 | 책임 |
| --- | --- | --- |
| 공통 DSL | `CodeGen/CodeCommon.fs` | `Relay` 타입, 래치/펄스/타이머 builder, `CodeGenUtils` 헬퍼, ST 출력 기본 규칙 정의.
| 유틸/도구 | `CodeGen/CodeUtil.fs` | 섹션 머지, 조건 최적화, 네임 컨벤션(`TagNaming`), 검증 함수.
| 시스템 패턴 | `CodeGen/CodeSystem.fs` | 시스템(모드/상태) 릴레이 자동 생성.
| 플로우 패턴 | `CodeGen/CodeFlow.fs` | FlowTag 정의, 버튼/램프/상태 자동 생성, 분기/병합 로직.
| 워크 패턴 | `CodeGen/CodeWork.fs` | WorkKind 정의, 기본 Work/Sequence 템플릿, Relay 빌더.

> (구형 `CodeApi*`, `CodeCall*` 는 현재 빌드 대상에서 제외됨. 필요 시 별도 복원.)

## 3. 의존성
- 프로젝트 참조: `Ev2.Cpu.Core`
- 외부 패키지 없음.
- 네임스페이스: 주로 `Ev2.Cpu.FS` (레거시 DSL) 사용. Core의 AST(`DsOperator`)가 필요할 경우 명시적으로 import.

## 4. 설계 규칙
1. **DSL stays pure**: `Relay`/`Expr` 빌더는 상태를 저장하지 않고 immutable 구조만 반환해야 함.
2. **플랫폼 독립성**: ST 타겟 전용 문자열은 `CodeGenUtils` 내부로 한정. 특정 PLC 방언 추가 시 여기 확장.
3. **네이밍 규칙**: 외부 이름은 `TagNaming` 모듈을 통해 변환. 규칙 변경 시 테스트 `System.Test.fs`, `WorkFlow.Test.fs` 갱신.
4. **패턴 추가**: Work/Flow/System 패턴을 추가하면 대응 테스트 파일 (예: `WorkFlow.Test.fs`)에 시나리오 작성 필수.

## 5. 빌드 & 테스트
```bash
# 단일 빌드
dotnet build src/Ev2.Cpu.CodeGen/Ev2.Cpu.CodeGen.fsproj
# 코드 생성 테스트
dotnet test src/Ev2.Cpu.CodeGen.Tests/Ev2.Cpu.CodeGen.Tests.fsproj
```

## 6. TODO / 향후 과제
- 플로우/워크 패턴을 YAML/JSON 기반 선언에서 생성하도록 DSL → 데이터 변환 계층 검토.
- 기존 `CodeApi*` 모듈을 현대화하여 REST/OPC-UA 연동 템플릿 제공.
- 생성 코드에 대한 포맷터/인덴터 추가 (현재는 수동 포맷).

## 7. 유지보수 로드맵

### 7.1 현황 진단 (AS-IS)
- `FCBuilder`, `FBBuilder` 가 자체 Validation을 구현하며 Core와 규칙이 다르게 적용된다.
- `callFB` 가 위치 기반 인자를 일부 허용해 사용자 코드가 쉽게 잘못 작성된다.
- 태그/스코프 생성을 문자열 결합으로 처리해 충돌 검출과 디버깅이 어렵다.
- 코드 변경이 문서/샘플에 즉시 반영되지 않아 사용자 혼란이 발생한다.
- 테스트·샘플·템플릿이 서로 다른 빌더 패턴을 사용하여 유지보수가 어렵다.

### 7.2 목표 상태 (TO-BE)
- 빌더·헬퍼가 Core Validation·Scope 서비스를 직접 사용해 단일 계약을 유지한다.
- `Result` 기반 API와 맵 기반 인자 입력을 표준화하여 호출 규약을 명확히 한다.
- ScopeService를 통해 태그 생성·충돌 감지·디버그 정보를 자동화한다.
- 문서·샘플·템플릿이 CI에서 자동 검증·생성되어 항상 최신 상태를 유지한다.
- 템플릿이 표준화되어 테스트로 보호되고 전체 에코시스템이 동일 규약을 따른다.

### 7.3 개선 과제
- **빌더 리팩터링**: Core Validation 모듈을 직접 호출하고 `Build` 를 `Result<'T, ValidationError>` 패턴으로 통일한다.
- **호출 헬퍼 정비**: `callFC/FB` 는 이름-값 맵을 필수로 요구하고 위치 인자는 호환 레이어와 Deprecation 경고로 제한한다.
- **스코프 통합**: ScopeService를 도입해 태그 생성·충돌 감지·디버그 출력 경로를 일원화한다.
- **문서/샘플 자동화**: 스니펫 컴파일 테스트를 CI에 추가하고 문서 빌더를 통해 최신 API를 즉시 반영한다.
- **템플릿 표준화**: 최신 API를 준수하는 템플릿을 제공하고 골든 AST 비교 테스트로 회귀를 방지한다.
