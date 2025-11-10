# Ev2.Cpu.Runtime 개발 사양

## 1. 개요
- **목표**: Core DSL로 작성된 프로그램을 PLC 스캔 루프 방식으로 실행하는 런타임을 제공한다.
- **출력 아티팩트**: `Ev2.Cpu.Runtime.dll` (Console entry: `CpuTest.fs`)
- **테스트 프로젝트**: `src/UintTest/cpu/Ev2.Cpu.Runtime.Tests` (165 tests) – 컨텍스트/스캔/평가/리테인/릴레이/타이밍 등 전체 동작 검증.

## 2. 모듈 구조
| 영역 | 주요 파일 | 책임 |
| --- | --- | --- |
| 메모리 | `Runtime/Memory.fs` | 입력/출력/로컬/글로벌 저장소, 타입 선언 및 히스토리/스냅샷 관리.
| 컨텍스트 | `Runtime/Context.fs` | `ExecutionContext`, 스캔 상태, 타이머/카운터 유틸리티(`updateTimerOn/Off` 등).
| 평가기 | `Runtime/ExprEvaluator.fs`, `Runtime/StmtEvaluator.fs`, `Runtime/ExpressionEvaluatorCore.fs` | `DsExpr` 평가, 단일 문장 실행, TON/CTU/CTL 등 빌트인 핸들링.
| 빌트인 함수 | `Runtime/BuiltinFunctions.fs` | 수치/문자열/논리 함수, `TypeConversion` 사용.
| 스캔 루프 | `CpuScan.fs`, `CpuTest.fs` | scanOnce / StartAsync 구현, 샘플 워크플로 시나리오.
| 레거시 서포트 | `Legacy/Cpu.Runtime.fs` | 이전 버전 런타임 (참고용).

## 3. 의존성
- 프로젝트 참조: `Ev2.Cpu.Core`
- 외부 패키지 없음.
- Runtime은 레거시 DSL(`Ev2.Cpu.FS.Expression`, `Statement`)을 주로 사용하므로 Core DSL을 확장할 경우 여기와 테스트를 함께 갱신해야 한다.

## 4. 설계 규칙
1. **스캔 원자성**: `StmtEvaluator.exec` 는 한 스캔 내에서 부작용을 반영하고, 실패 시 `Context.error` 로 기록 후 다음 스캔으로 넘어갈 수 있도록 예외를 잡아야 한다.
2. **타이머/카운터 단일 진실원천**: 타이머/카운터 상태는 반드시 `Context.fs` 의 helper를 통해 변경. 직접 필드 수정 금지.
3. **메모리 타입 선언**: `Memory.Declare*` 호출 후에만 `Set`/`Get` 사용. 테스트(`Runtime.Execution.Test.fs`)는 이 전제가 깨지지 않았는지 확인.
4. **빌트인 확장**: 새로운 함수 추가 시 `BuiltinFunctions.call`과 런타임 테스트를 동시에 갱신.

## 5. 빌드 & 테스트
```bash
# 단일 빌드
dotnet build src/Ev2.Cpu.Runtime/Ev2.Cpu.Runtime.fsproj
# 런타임 테스트
dotnet test src/Ev2.Cpu.Runtime.Tests/Ev2.Cpu.Runtime.Tests.fsproj
# 샘플 실행 (CpuTest)
dotnet run --project src/Ev2.Cpu.Runtime/Ev2.Cpu.Runtime.fsproj
```

## 6. TODO / 향후 과제
- `CpuScan` 의 비동기 루프를 CancellationToken/Task 기반에서 IHostedService 스타일로 확장.
- 플러그인 시스템(`Extensibility/PluginSystem.fs`) 재통합 시 런타임과의 경량 어댑터 설계 필요.
- 히스토리/트레이스 출력 포맷 (Json/CSV) 옵션 추가.

## 7. 유지보수 로드맵

### 7.1 현황 진단 (AS-IS)
- 평가기가 최신 `EUserFC`, `SUserFB` 노드를 완전 지원하지 못해 래퍼 코드가 남아 있다.
- FB 인스턴스 상태 저장소가 전역 Dictionary에 의존하여 초기화·스냅샷·삭제 시 일관성이 떨어진다.
- 타이머/카운터 매니저가 하드코딩되어 테스트/모킹/확장이 어렵다.
- 실행 중 예외 발생 시 전체 스캔이 중단되고 구조화된 오류가 제공되지 않는다.
- 스캔 시간, 상태 변화, 인스턴스 데이터 등 관측 정보가 충분히 수집되지 않는다.

### 7.2 목표 상태 (TO-BE)
- Core AST와 Validation 메타데이터를 그대로 활용하는 실행 파이프라인을 구축한다.
- FB 인스턴스를 서비스 레이어에서 관리하며 생성→리셋→스냅샷→삭제 라이프사이클을 명확히 한다.
- 시간·카운터 서비스를 추상화해 모킹·테스트·확장을 용이하게 한다.
- 구조화된 오류 객체를 정의하고 정책별(즉시 종료, 경고 후 지속 등) 처리 전략을 제공한다.
- 스캔 단계별 로그, 성능 카운터, 상태 덤프 등 운영 관점 데이터를 노출한다.

### 7.3 개선 과제
- **실행 파이프라인**: 입력→실행→출력 단계를 구분하고 Validation/로그를 각 단계에 배치한다.
- **인스턴스 관리**: `FBInstanceStore`(가칭)를 도입하여 안전한 상태 관리와 동시성 제어를 확보한다.
- **타이머/카운터 모듈화**: 인터페이스 기반 서비스로 재작성하고 Mock 시간 공급자를 통해 경계 조건 테스트를 강화한다.
- **오류 처리**: `RuntimeError`(위치, 인스턴스, 조건 포함) 타입을 정의하고 복구 전략을 명문화한다.
- **관측성 도구**: 스캔 시간/상태/메모리 메트릭을 측정하여 EventSource·ILogger 또는 CLI 스크립트로 제공한다.

<a id="runtime-test-roadmap"></a>
### 7.4 테스트 스위트 로드맵 (Ev2.Cpu.Runtime.Tests)
- **AS-IS**: FB 인스턴스 라이프사이클, 타이머 엣지 케이스, 오류 정책 시나리오가 부족하고 Mock 인프라 부재로 복잡한 테스트 재현이 어렵다. 로그·트레이스 검증이 없으며 Generation/Runtime 간 헬퍼 코드가 중복된다.
- **TO-BE**: 초기화→실행→리셋→스냅샷 전체 흐름을 자동화 테스트로 보장하고, 타이머/스토리지/로깅을 모듈화된 Mock으로 교체하여 경계 조건을 재현한다. 관측 데이터를 검증하고 공통 헬퍼를 공유한다.
- **주요 과제**
  - 표준 FC/FB, Mock Clock 등을 포함한 공통 Fixture와 도메인 헬퍼를 구축한다.
  - Generation 빌더를 활용해 AST 생성부터 스캔 실행까지 실제 흐름을 재현한다.
  - 타이머·카운터·오류 정책·관측성 관련 장기 실행 및 경계 조건 시나리오를 추가한다.
  - 로그/트레이스/메트릭 검증 유틸리티와 장기 실행 스크립트를 도입하고, 소요 시간 증가는 야간 플로우로 분리한다.

---

## 8. 관련 Quickstart & Reference

| 용도 | 문서 |
|------|------|
| 빠른 시작 | `docs/guides/quickstarts/PLC-Code-Generation-Guide.md` |
| 운영 가이드 | `docs/guides/operations/Retain-Memory-Guide.md` |
| 사용자 매뉴얼 | `docs/guides/manuals/runtime/Ev2.Cpu.Runtime-사용자매뉴얼.md` |
| 심화 스펙 | `docs/specs/runtime/RuntimeSpec.md` |
