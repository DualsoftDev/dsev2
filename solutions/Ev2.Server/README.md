# Ev2.Server Solution

## Overview

Ev2.Server는 백엔드 서버 솔루션으로, REST/gRPC API, 실시간 데이터 스트림, PLC 통신 게이트웨이 등을 제공합니다.

## Planned Projects

### 1. Ev2.Server.Api
- ASP.NET Core REST API
- gRPC 서비스
- OpenAPI/Swagger 문서

### 2. Ev2.Server.Realtime
- SignalR Hub
- WebSocket 실시간 데이터 스트림
- 이벤트 브로드캐스팅

### 3. Ev2.Server.PlcGateway
- PLC 스캔 오케스트레이터
- Ev2.PLC 드라이버 통합
- 데이터 수집 및 캐싱

### 4. Ev2.Server.Core
- 비즈니스 로직
- 태그 관리, 알람, 이벤트
- 도메인 서비스

### 5. Ev2.Server.Infrastructure
- JWT 인증/인가
- Redis 캐시
- 데이터베이스 접근 (EF Core/Dapper)
- 로깅 (Serilog)

## Dependencies

```
Ev2.Server.Api/Realtime
    ↓
Ev2.Server.Core
    ↓
Ev2.Server.PlcGateway → Ev2.Plc.*
    ↓
Ev2.Server.Infrastructure → Ev2.Core.*
```

## Status

**현재 상태**: 기본 디렉토리 구조만 생성됨
**다음 단계**: 프로젝트 파일 생성 및 구현
