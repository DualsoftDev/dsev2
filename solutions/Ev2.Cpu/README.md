# Ev2.Cpu Solution

## Overview

Ev2.Cpu는 CPU 런타임 엔진 솔루션으로, IEC 61131-3 표준에 기반한 수식 실행 엔진을 제공합니다.

## Projects

### Source Projects (src/cpu/)

#### 1. Ev2.Cpu.Core
**역할**: CPU 엔진의 핵심 - AST, 파싱, 타입 시스템, 연산자
**의존성**: 없음 (자체 완결형)

주요 컴포넌트:
- `Ast/`: Expression, Statement, Program AST 정의
- `Core/`: DataType, Operators, Tags
- `Parsing/`: Lexer, Parser, TokenTypes
- `Struct/`: 레거시 구조체 기반 타입
- `UserDefined/`: 사용자 정의 FC/FB 시스템

#### 2. Ev2.Cpu.Runtime
**역할**: 실행 엔진, 메모리 관리, 스캔 처리
**의존성**: Ev2.Cpu.Core

주요 컴포넌트:
- `Engine/`: 표현식/문장 평가기, 메모리 풀, 타임베이스
- `Engine/Functions/`: 빌트인 함수 (산술, 비교, 수학, 문자열, 시스템)
- `Engine/`: RelayLifecycle, RelayStateManager
- `CpuScan.fs`: 스캔 실행기

#### 3. Ev2.Cpu.Generation (⚠️ Ev2.Gen으로 이동 예정)
**역할**: PLC 코드 생성 (Ladder Diagram, Structured Text)
**의존성**: Ev2.Cpu.Core

주요 컴포넌트:
- `Codegen/`: PLC 코드 생성 엔진
- `Generation/`: Expression, Statement, Program 생성기
- `Loops/`: 배열, 시퀀스 패턴 처리
- `System/`: 시스템 릴레이, 패턴

**참고**: 이 프로젝트는 Ev2.Gen 솔루션으로 이동될 예정입니다.

#### 4. Ev2.Cpu.StandardLibrary
**역할**: IEC 61131-3 표준 함수 블록 라이브러리
**의존성**: Ev2.Cpu.Runtime

표준 함수 블록:
- **Timers**: TON, TOF, TP, TONR
- **Counters**: CTU, CTD, CTUD
- **Edge Detection**: R_TRIG, F_TRIG
- **Bistable**: RS, SR
- **Analog**: HYSTERESIS, LIMIT, SCALE
- **Math**: MIN, MAX, AVERAGE
- **String**: CONCAT, FIND, LEFT, MID, RIGHT

#### 5. Ev2.Cpu.Debug
**역할**: 디버깅, 성능 분석, 벤치마킹
**의존성**: Ev2.Cpu.Runtime, Ev2.Cpu.StandardLibrary

주요 컴포넌트:
- `Performance/Core/`: 성능 메트릭, 타입
- `Performance/Monitoring/`: CPU, 메모리, 스캔 모니터
- `Performance/Analysis/`: 분석기, 벤치마크
- `Performance/Reporting/`: 성능 리포터

### Test Projects (src/UintTest/cpu/)

- **Ev2.Cpu.Core.Tests**: Core 기능 테스트
- **Ev2.Cpu.Runtime.Tests**: Runtime 실행 테스트
- **Ev2.Cpu.Generation.Tests**: 코드 생성 테스트
- **Ev2.Cpu.StandardLibrary.Tests**: 표준 라이브러리 테스트

## Build

```bash
# 전체 솔루션 빌드
cd Ev2.Cpu
dotnet build Ev2.Cpu.sln

# 특정 프로젝트 빌드
dotnet build src/cpu/Ev2.Cpu.Core/Ev2.Cpu.Core.fsproj

# 테스트 실행
dotnet test Ev2.Cpu.sln
```

## Architecture

### Data Flow
```
수식 입력 (Formula String)
    ↓
Lexer → TokenTypes
    ↓
Parser → AST (Expression, Statement)
    ↓
ExprEvaluator/StmtEvaluator → 실행
    ↓
Memory/RelayStateManager → 상태 관리
    ↓
결과 반환
```

### Dependencies
```
Ev2.Cpu.Debug
    ↓
Ev2.Cpu.StandardLibrary
    ↓
Ev2.Cpu.Runtime
    ↓
Ev2.Cpu.Core (기반)

Ev2.Cpu.Generation (독립, Core만 참조)
```

## Key Features

- ✅ **IEC 61131-3 호환**: 표준 데이터 타입 및 연산자
- ✅ **사용자 정의 FB/FC**: 확장 가능한 함수 블록 시스템
- ✅ **고성능 실행**: 메모리 풀, 최적화된 평가기
- ✅ **풍부한 표준 라이브러리**: 타이머, 카운터, 수학, 문자열
- ✅ **디버깅 도구**: 성능 프로파일러, 벤치마크
- ✅ **코드 생성**: PLC LD/ST 코드 자동 생성

## Migration to Ev2.Gen

`Ev2.Cpu.Generation` 프로젝트는 다음 단계에서 `Ev2.Gen` 솔루션으로 이동됩니다:

1. Ev2.Gen/src/Ev2.Gen.Core로 Generation 이동
2. Ev2.Gen.LD, Ev2.Gen.ST, Ev2.Gen.IL 프로젝트 생성
3. Ev2.Cpu.sln에서 Generation 프로젝트 제거

## References

- IEC 61131-3 표준 문서
- docs/RuntimeSpec.md: 런타임 사양
- docs/LoopInfrastructure.md: 루프 인프라
