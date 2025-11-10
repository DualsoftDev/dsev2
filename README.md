# Ev2 Platform - 6-Solution Monorepo

## 프로젝트 개요

Ev2 플랫폼은 산업 자동화를 위한 통합 소프트웨어 플랫폼으로, 6개의 독립적인 솔루션으로 구성된 모노레포입니다.

```
dsev2cpucodex/
├── 📦 Ev2.Core/          공통 기반 라이브러리 (4개 프로젝트)
├── 🎯 Ev2.Cpu/           CPU 런타임 엔진 (5개 프로젝트)
├── 🏭 Ev2.Gen/           PLC 코드 생성 (5개 프로젝트)
├── 🔌 Ev2.Plc/           PLC 드라이버/프로토콜 (8개 프로젝트)
├── 🌐 Ev2.Server/        백엔드 서버 (5개 프로젝트 예정)
└── 💻 Ev2.UI/            사용자 인터페이스 (기술 스택 미정)
```

## 빠른 시작

### 필수 요구사항

- .NET SDK 8.0.400 이상
- F# 8.0 이상
- Visual Studio 2022, VS Code, 또는 Rider

### 전체 빌드

```bash
# 의존성 복원
dotnet restore

# 순서대로 빌드
dotnet build Ev2.Core/Ev2.Core.sln
dotnet build Ev2.Cpu/Ev2.Cpu.sln
dotnet build Ev2.Plc/Ev2.Plc.sln
dotnet build Ev2.Gen/Ev2.Gen.sln
```

### 개별 솔루션 빌드

```bash
# Core 빌드
cd Ev2.Core
dotnet build

# Cpu 빌드 및 테스트
cd Ev2.Cpu
dotnet build
dotnet test

# Gen 빌드
cd Ev2.Gen
dotnet build
```

## 솔루션별 개요

### 1. 🟦 Ev2.Core - 공통 기반 라이브러리

모든 솔루션이 참조하는 기반 라이브러리

| 프로젝트 | 설명 |
|---------|------|
| Ev2.Core.Contracts | 공통 인터페이스, 추상 타입 |
| Ev2.Core.Common | 공통 데이터 타입, 유틸리티 |
| Ev2.Core.Infrastructure | 로깅, 설정, DB, 직렬화 |
| Ev2.Core.Aas | AAS 3.0 통합 |

**상태**: ⚠️ 기본 스켈레톤 생성 완료, 마이그레이션 필요
**문서**: [Ev2.Core/README.md](Ev2.Core/README.md)

### 2. 🟩 Ev2.Cpu - CPU 런타임 엔진

IEC 61131-3 기반 수식 실행 엔진

| 프로젝트 | 설명 |
|---------|------|
| Ev2.Cpu.Core | AST, 파싱, 타입 시스템 |
| Ev2.Cpu.Runtime | 실행 엔진, 메모리 관리 |
| Ev2.Cpu.StandardLibrary | IEC 61131-3 표준 함수 블록 |
| Ev2.Cpu.Debug | 디버깅, 성능 분석 |
| Ev2.Cpu.Generation | PLC 코드 생성 (→ Ev2.Gen으로 이동 예정) |

**상태**: ✅ 기존 프로젝트 유지, 솔루션 파일 생성 완료
**문서**: [Ev2.Cpu/README.md](Ev2.Cpu/README.md)

### 3. 🟨 Ev2.Gen - PLC 코드 생성

IEC 61131-3 PLC 언어 코드 생성기

| 프로젝트 | 설명 |
|---------|------|
| Ev2.Gen.Core | 코드 생성 엔진 코어 |
| Ev2.Gen.LD | Ladder Diagram 생성 |
| Ev2.Gen.ST | Structured Text 생성 |
| Ev2.Gen.IL | Instruction List 생성 |
| Ev2.Gen.Templates | 템플릿 엔진 (Scriban) |

**상태**: ⚠️ 기본 구조 생성 완료, 구현 필요
**문서**: [Ev2.Gen/README.md](Ev2.Gen/README.md)

### 4. 🟪 Ev2.Plc - PLC 드라이버/프로토콜

다양한 PLC 벤더 통신 드라이버

| 카테고리 | 프로젝트 |
|---------|---------|
| **Core** | Common, Driver, Mapper, Server |
| **Protocols** | Allen-Bradley, Siemens S7, Mitsubishi, LS Electric |

**상태**: ✅ 기존 프로젝트 유지, 솔루션 파일 생성 완료
**문서**: 각 프로토콜별 README 참조

### 5. 🟥 Ev2.Server - 백엔드 서버

REST API, 실시간 데이터, PLC 게이트웨이

| 프로젝트 (예정) | 설명 |
|---------------|------|
| Ev2.Server.Api | REST/gRPC API |
| Ev2.Server.Realtime | SignalR, WebSocket |
| Ev2.Server.PlcGateway | PLC 통신 오케스트레이터 |
| Ev2.Server.Core | 비즈니스 로직 |
| Ev2.Server.Infrastructure | 인증, 로깅, DB |

**상태**: 📝 디렉토리 구조만 생성, 구현 예정
**문서**: [Ev2.Server/README.md](Ev2.Server/README.md)

### 6. 🟧 Ev2.UI - 사용자 인터페이스

웹/데스크톱 UI (기술 스택 미정)

**옵션**:
- Blazor (WebAssembly/Server)
- React/Vue/Angular
- WPF/WinForms

**상태**: 📝 디렉토리 구조만 생성, 기술 스택 결정 필요
**문서**: [Ev2.UI/README.md](Ev2.UI/README.md)

## 의존성 구조

```
계층 5: Ev2.UI → Ev2.Server (API만)
계층 4: Ev2.Server → Ev2.Plc, Ev2.Cpu, Ev2.Gen, Ev2.Core
계층 3: Ev2.Plc (독립), Ev2.Gen → Ev2.Cpu.Core
계층 2: Ev2.Cpu (독립)
계층 1: Ev2.Core (기반)
```

**원칙**:
- ✅ 상위 계층 → 하위 계층 참조 허용
- ❌ 하위 계층 → 상위 계층 역참조 금지
- ❌ 동일 계층 간 직접 참조 금지

## 공통 빌드 설정

### Directory.Build.props
- 타겟 프레임워크: .NET 8.0
- 경고를 에러로 처리: true
- 문서 자동 생성: true
- 공통 NuGet 패키지 (Serilog, xUnit 등)

### .editorconfig
- F#: 4칸 들여쓰기, 120자 제한
- C#: 4칸 들여쓰기, 120자 제한
- UTF-8 인코딩, LF 줄바꿈

### global.json
- SDK 버전: 8.0.400 이상

## 프로젝트 상태

| 솔루션 | 프로젝트 수 | 상태 | 진행률 |
|--------|-----------|------|-------|
| Ev2.Core | 4 | ⚠️ 스켈레톤 | 20% |
| Ev2.Cpu | 5 | ✅ 완료 | 100% |
| Ev2.Gen | 5 | ⚠️ 구조 | 30% |
| Ev2.Plc | 8 | ✅ 완료 | 100% |
| Ev2.Server | 5 (예정) | 📝 계획 | 0% |
| Ev2.UI | TBD | 📝 계획 | 0% |

## 다음 단계

### 단기 (1-2주)
- [ ] Ev2.Core: 기존 Ev2.Core.FS 파일 마이그레이션
- [ ] Ev2.Gen: Ev2.Cpu.Generation 이동
- [ ] 전체 솔루션 빌드 검증

### 중기 (1개월)
- [ ] Ev2.Server: 프로젝트 생성 및 구현
- [ ] Ev2.UI: 기술 스택 결정 및 프로젝트 생성
- [ ] CI/CD 파이프라인 구축

### 장기 (2-3개월)
- [ ] 전체 통합 테스트
- [ ] 성능 최적화
- [ ] NuGet 패키지 배포
- [ ] 문서 완성

## 문서

- [아키텍처 문서](docs/ARCHITECTURE.md) - 전체 아키텍처 설명
- [마이그레이션 가이드](Ev2.Core/README.md#migration-from-ev2corefs-dsev2-master) - 기존 코드 마이그레이션
- 각 솔루션별 README 참조

## 라이선스

© 2024-2025 Dualsoft. All rights reserved.

## 기여

이 프로젝트는 Dualsoft 내부 프로젝트입니다.

## 지원

문의사항은 사내 개발팀으로 연락해주세요.

---

**Last Updated**: 2025-11-10
**Architecture Version**: 1.0
