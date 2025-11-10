# LS Electric XGT Protocol Tests

이 프로젝트는 LS Electric의 XGT PLC 프로토콜 구현에 대한 포괄적인 테스트 스위트입니다.

## 테스트 구조

### Unit Tests (단위 테스트)
- **XgtTypesTests.fs**: 기본 타입과 상수, 변환 함수 테스트
- **XgtFrameBuilderTests.fs**: 프레임 생성 로직 테스트
- **XgtResponseTests.fs**: 응답 파싱 및 처리 테스트  
- **XgtTagTests.fs**: 태그 주소 파싱 및 검증 테스트

### Integration Tests (통합 테스트)
- **IntegrationConnectionTests.fs**: PLC 연결 및 연결 상태 관리 테스트
- **IntegrationReadTests.fs**: 실제 PLC에서 데이터 읽기 테스트
- **IntegrationWriteTests.fs**: 실제 PLC에 데이터 쓰기 테스트
- **PerformanceTests.fs**: 성능 및 처리량 테스트

## 실행 방법

### 전체 테스트 실행
```bash
dotnet test
```

### 단위 테스트만 실행
```bash
dotnet test --filter Category!=Integration
```

### 통합 테스트만 실행 (실제 PLC 필요)
```bash
dotnet test --filter Category=Integration
```

### 특정 테스트 클래스 실행
```bash
dotnet test --filter ClassName=XgtFrameBuilderTests
```

## 환경 설정

통합 테스트를 실행하려면 다음 환경 변수를 설정하세요:

| 환경 변수 | 기본값 | 설명 |
|-----------|--------|------|
| `XGT_TEST_IP` | 192.168.1.100 | XGT PLC의 IP 주소 |
| `XGT_TEST_PORT` | 2004 | XGT 프로토콜 포트 |
| `XGT_TEST_TIMEOUT_MS` | 5000 | 통신 타임아웃 (밀리초) |
| `XGT_TEST_CPU_TYPE` | XGK | CPU 타입 (XGK 또는 XGI) |
| `XGT_SKIP_INTEGRATION` | false | 통합 테스트 건너뛰기 |

### 환경 변수 설정 예시

#### Windows (PowerShell)
```powershell
$env:XGT_TEST_IP = "192.168.1.50"
$env:XGT_TEST_PORT = "2004"
dotnet test --filter Category=Integration
```

#### Linux/macOS
```bash
export XGT_TEST_IP=192.168.1.50
export XGT_TEST_PORT=2004
dotnet test --filter Category=Integration
```

#### 통합 테스트 비활성화
```bash
export XGT_SKIP_INTEGRATION=true
dotnet test
```

## 지원하는 XGT 기능

### 프로토콜 지원
- ✅ XGT 표준 프로토콜 (포트 2004)
- ✅ EFMTB 프로토콜 
- ✅ 다중 변수 읽기/쓰기 (최대 16개)
- ✅ 비트 단위 주소 지정
- ✅ 상태 읽기

### 데이터 타입 지원
- ✅ BOOL (비트)
- ✅ BYTE (8비트)
- ✅ WORD (16비트)
- ✅ DWORD (32비트)
- ✅ LWORD (64비트)

### 메모리 영역 지원
#### XGK CPU
- ✅ P (입출력)
- ✅ M (내부 릴레이)
- ✅ K (킵 릴레이)
- ✅ T (타이머)
- ✅ C (카운터)
- ✅ D (데이터 레지스터)

#### XGI CPU  
- ✅ I (입력)
- ✅ Q (출력)
- ✅ M (내부 릴레이)
- ✅ F (에지 릴레이)
- ✅ L (링크 릴레이)

## 테스트 작성 가이드

### 새로운 단위 테스트 추가
```fsharp
[<Fact>]
let ``새로운 기능 테스트`` () =
    // Arrange
    let input = "test input"
    
    // Act  
    let result = YourFunction.process input
    
    // Assert
    Assert.Equal("expected", result)
```

### 새로운 통합 테스트 추가
```fsharp
[<Fact>]
let ``새로운 통합 테스트`` () =
    skipIfIntegrationDisabled "테스트 이름"
    
    withConnection (fun client -> async {
        // 테스트 로직
        let! result = client.SomeOperation() |> Async.AwaitTask
        Assert.NotNull(result)
    }) |> Async.RunSynchronously
```

## 성능 기준

통합 테스트는 다음 성능 기준을 검증합니다:

- **단일 읽기**: 평균 100ms 이하
- **단일 쓰기**: 평균 150ms 이하  
- **처리량**: 읽기 10회/초 이상, 쓰기 5회/초 이상
- **연결 시간**: 평균 5초 이하
- **메모리 사용량**: 반복 작업 후 1MB 이하 증가

## 문제 해결

### 통합 테스트 실패
1. PLC 네트워크 연결 확인
2. PLC IP 주소와 포트 확인
3. 방화벽 설정 확인
4. PLC CPU 타입 확인 (XGK/XGI)

### 성능 테스트 실패
1. 네트워크 지연 시간 확인
2. PLC 부하 상태 확인
3. 테스트 환경의 시스템 리소스 확인

## 기여 방법

1. 새로운 기능에 대한 단위 테스트 작성
2. 통합 테스트 추가 시 환경 변수 활용
3. 성능에 영향을 주는 변경 시 성능 테스트 업데이트
4. 모든 테스트가 통과하는지 확인 후 커밋