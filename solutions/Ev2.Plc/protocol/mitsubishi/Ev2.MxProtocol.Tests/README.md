# Mitsubishi Protocol Tests

이 프로젝트는 Mitsubishi MELSEC PLC와의 통신을 테스트합니다.

## PLC 설정

현재 두 대의 PLC가 설정되어 있습니다:

- **PLC1**: 192.168.9.120:7777
- **PLC2**: 192.168.9.121:5002

## 테스트 실행 방법

### 1. 기본 테스트 (PLC1 사용)
```bash
dotnet test
```

### 2. 특정 PLC 선택하여 테스트
```bash
# PLC1 사용
set MELSEC_TEST_PLC=PLC1
dotnet test

# PLC2 사용  
set MELSEC_TEST_PLC=PLC2
dotnet test
```

### 3. 수동으로 PLC 설정
```bash
set MELSEC_PLC_HOST=192.168.9.120
set MELSEC_PLC_PORT=7777
dotnet test
```

## 테스트 카테고리

- **Unit Tests**: PLC 연결 없이 실행되는 단위 테스트
- **Integration Tests**: 실제 PLC 연결이 필요한 통합 테스트
  - `[<RequiresMelsecPLC>]`: 기본 PLC 사용
  - `[<RequiresPLC1>]`: PLC1 전용 테스트
  - `[<RequiresPLC2>]`: PLC2 전용 테스트

## 환경 변수

- `MELSEC_TEST_PLC`: 사용할 PLC 선택 (PLC1, PLC2, 1, 2)
- `MELSEC_PLC_HOST`: PLC IP 주소 (수동 설정)
- `MELSEC_PLC_PORT`: PLC 포트 번호 (수동 설정)
- `MELSEC_PLC_VERIFY_DEVICE`: 실 PLC 연결 테스트에서 사용할 워드 디바이스 코드 (예: D, W, R)  
- `MELSEC_PLC_VERIFY_ADDRESS`: 연결 검증용 디바이스 시작 주소 (10진수 또는 0x 접두사의 16진수)
- `MELSEC_PLC_VERIFY_COUNT`: 읽어올 워드 개수 (기본값 1)
- `MELSEC_ACCESS_NETWORK` / `MELSEC_ACCESS_NETWORK_NUMBER`: 접근 경로의 네트워크 번호 (0~255, 0x 접두사 지원)
- `MELSEC_ACCESS_STATION` / `MELSEC_ACCESS_STATION_NUMBER`: 접근 경로의 스테이션 번호 (0~255, 0x 접두사 지원)
- `MELSEC_ACCESS_IO` / `MELSEC_ACCESS_IO_NUMBER`: 접근 경로의 I/O 번호 (0~65535, 기본값 0x03FF)
- `MELSEC_ACCESS_RELAY` / `MELSEC_ACCESS_RELAY_TYPE`: 접근 경로의 릴레이 타입 (0~255)
