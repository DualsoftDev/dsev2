# Ev2 PLC Protocol Development Specification

본 문서는 Ev2 플랫폼에서 제공하는 4종 PLC 프로토콜(Allen‑Bradley AB, Mitsubishi MELSEC, LS Electric XGT, Siemens S7)에 대한 개발 기준과 유지보수 가이드를 통합 정리한 것이다. 신규 기능 설계, 버그 수정, 테스트 절차, 문서화 요구사항을 모두 포함한다.

---

## 1. 공통 아키텍처

| 계층 | 설명 | 공통 요구사항 |
| --- | --- | --- |
| Core | 프로토콜별 기본 타입, 열거형, 오류 모델, 연결 설정 | 불변 모델 사용, XML 요약 주석 필수 |
| Protocol | 세션 수명주기, 패킷 빌더/파서, 장치별 명령 세부 | 패킷 로깅 훅 제공, 재시도/타임아웃 명시 |
| Client | 고수준 API (Read/Write, 배치, 통계 등) | 공용 인터페이스와 naming 규약 준수 (`Read*`, `Write*`, `Get*`) |
| Tests | 단위 + 통합 테스트. 실 장비 또는 시뮬레이터 기반 | 실패 시 `failWithLogs` 계열 사용, 표준 환경 변수 활용 |

### 공용 헬퍼

- `ProtocolTestHelper` 라이브러리
  - `TestExecution.captureOutput` 및 `TestLogger` 로 STDOUT/STDERR, 패킷 로그 캡처
  - `IntegrationTestRunner`를 통해 클라이언트 연결과 에러 보강(로그 포함)을 통일
  - `IntegrationTestRunner.ClientLifecycle`은 다음을 반드시 구현:
    - `CreateClient`, `Connect`, `Disconnect`, `Dispose`
    - `MapException`: 예외 → 프로토콜 오류
    - `DumpLogs`: 현재까지 누적된 TX/RX 로그 문자열
    - `AugmentError`: 오류 메시지와 로그를 합쳐 반환

### 테스트 실패 처리

- **반드시** `failWithLogs` (`TestHelpers.failWithLogs`, `failWithLogsResult`) 사용
  - 직접 `Assert.True(false, …)` 등의 패턴 사용 금지
  - 예외 및 연결 실패는 `IntegrationTestRunner`가 TX/RX 로그를 합성하도록 유지
- 통합 테스트는 PLC별 `Requires*` 어트리뷰트(예: `[<RequiresMelsecPLC>]`)를 사용해 조건부 실행

### 환경 변수 규약

| 프로토콜 | 접두사 | 예시 |
| --- | --- | --- |
| AB | `AB_TEST_*` | `AB_TEST_IP`, `AB_TEST_PORT`, `AB_TEST_SLOT`, `AB_TEST_PLC_TYPE`, `AB_TEST_TIMEOUT_MS` |
| Mitsubishi | `MELSEC_TEST_*` | `MELSEC_TEST_HOST`, `MELSEC_TEST_PLC1_HOST`, `MELSEC_TEST_TIMEOUT_MS` |
| LS | `XGT_TEST_*` | `XGT_TEST_IP`, `XGT_TEST_PORT`, `XGT_TEST_TIMEOUT_MS`, `XGT_TEST_CPU_TYPE` |
| Siemens | `S7_TEST_*` (준비 중) | 필요 시 `S7_TEST_IP`, `S7_TEST_RACK`, `S7_TEST_SLOT` 등 추가 |

환경 변수는 테스트 시작 전에 `.ps1` 또는 `.sh` 스크립트로 설정하며, 각 TestHelpers 파일에서 기본값과 덮어쓰기 로직을 유지한다.

---

## 2. Allen‑Bradley (Ev2.ABProtocol)

참고 문서: `docs/Ev2.ABProtocol.init.md`

### 주요 모듈
- `Core/Types.fs` – `ConnectionConfig`, `AbProtocolError`, `DataType`
- `Protocol/Session.fs` – Register/Unregister Session, KeepAlive, 재시도
- `Protocol/PacketBuilder.fs` / `PacketParser.fs` – CIP Service 생성/파싱
- `Protocol/TagEnumerator.fs` – `GetAttributeList` 기반 태그 메타데이터
- `Client/ABClient.fs` – `ReadTag`, `WriteTag`, `BatchRead`, `CommunicationStats`

### 개발 지침
- 새 CIP 서비스 구현 시 `PacketBuilder`/`PacketParser`에 추가 후 `ABClient`에서 래핑
- 비트/배열 접근 시 `ABClientUtil`의 고수준 도우미 사용
- 통계(`CommunicationStats`) 업데이트 및 패킷 로깅 유지

### 테스트
- `Ev2.AbProtocol.Test` 내 `ClientHarness` + `IntegrationTestRunner` 사용
- `TagFixtures.fs`에 테스트용 태그를 중앙 관리
- 실패 시 자동으로 TX/RX 로그가 메시지에 포함되어야 함

---

## 3. Mitsubishi MELSEC (Ev2.MxProtocol)

### 주요 모듈
- `Ev2.MxProtocol.Core` – `MelsecConfig`, `DeviceCode`, `ValuePattern`
- `Ev2.MxProtocol.Protocol.Frame`/`PacketBuilder`/`PacketParser` – QnA 3E Binary 프레임 구성, EndCode 파싱
- `Ev2.MxProtocol.Client.MxClient` – 연결/재연결, `ReadBits/WriteBits`, `ReadWords/WriteWords`, `ReadBuffer/WriteBuffer`

### 개발 지침
- 모든 연결 진입점은 `MelsecClient.Connect()`에 집중. 타임아웃/예외 메시지 명확화
- 패킷 전송 전후 `logPacket "[TX]"`, `"[RX]"` 로깅 유지
- Chunk 기반 읽기/쓰기 구현 시 EndCode 검사 및 의미있는 메시지로 변환 (예: "Broadcast data count error")
- 새 기능 추가 시 통합 테스트(`Integration/...`)에 케이스 추가, `failWithLogs` 호출 보장
- `ClientHelpers`는 `IntegrationTestRunner` 사용. 오류 메시지에 로그가 **반드시** 포함되도록 `AugmentError` 구현 유지

### 테스트 & 환경
- 환경 변수 `MELSEC_TEST_*`
- 실 PLC 또는 Mock (`MockPlcServer`) 활용 가능
- 모든 `withConnectedClient` 호출은 `runWithClient`를 통해 실패 로그가 자동 첨부되어야 한다

---

## 4. LS Electric XGT (Ev2.LsProtocol)

### 주요 모듈
- `Ev2.LsProtocol/Protocol/XgtComm.fs` – TCP 클라이언트 베이스, `createMultiRead/Write` 프레임 처리
- `Ev2.LsProtocol/Protocol/XgtPacketParser.fs` – EFMTB/LocalEthernet 응답 파싱 및 오류 코드 변환
- `Ev2.LsProtocol.Tests` – 다양한 하드웨어(PLC1/PLC2) 시나리오

### 개발 지침
- `XgtTcpClientBase`에서 Connect/Disconnect, 재시도, 패킷 로깅 구현을 유지
- `LsClient`는 주소 파싱, 다중 읽기/쓰기, 버퍼 쓰기(U16 기반)를 담당 → 신규 명령 추가 시 `ScalarValue` 모델과 호환성 검토
- 테스트는 `failWithLogs` 사용. 기존 `TestHelpers`에 `dumpLogs` 사용 준비되어 있음
- 통합 시 `createPacketLogger`를 통해 TX/RX 로그가 `TestLogging`에 전달되도록 한다

### 환경
- 환경 변수 `XGT_TEST_*`
- CPU 타입(XGK/XGI)별 태그 세트 분리 유지

---

## 5. Siemens S7 (Ev2.S7Protocol)

### 주요 모듈
- `Ev2.S7Protocol.Core` – `S7Config`, `DataArea`, 에러 모델 등
- `Ev2.S7Protocol.Protocol.SessionManager` – ISO-on-TCP, COTP, S7 Job/Response 관리
- `Ev2.S7Protocol.Client.S7Client` – `ReadBit/WriteBit`, `ReadBytes`, `ReadInt16`, `ReadInt32` 등

### 개발 지침
- 세션 연결시 ISO-TP(COTP) 핸드셰이크, 파라미터 협상 성공 여부를 명확히 기록
- 모든 읽기/쓰기 함수는 `updateStatsSuccess`, `updateStatsError` 호출로 통계 유지
- 에러 메시지는 PLC 오류 코드를 사람이 읽기 쉽게 변환 (예: "Address must be in format 'M0.0'")
- 테스트 프로젝트(`Ev2.S7Protocol.Tests`)는 아직 간소. 통합 시나리오 추가 시 `failWithLogsWithResult` 사용

### 환경
- 환경 변수 `S7_TEST_IP`, `S7_TEST_RACK`, `S7_TEST_SLOT`, `S7_TEST_TIMEOUT_MS` 등 확장 가능
- 실제 S7 장비 또는 시뮬레이터(Snap7, S7NetPlus)와의 호환성 체크 필요

---

## 6. 공통 QA & 배포 체크리스트

1. **코드 스타일**
   - F# 파일은 UTF‑8, 공백 기반 들여쓰기
   - 공개 API는 XML 요약 주석 포함
2. **패킷 로깅**
   - TX/RX 로그는 모든 프로토콜에서 `TestLogging.forwardPacket` 호출
   - 실패 메시지는 `IntegrationTestRunner`에 의해 자동으로 로그 첨부되는지 확인
3. **테스트**
   - `dotnet build src/dsev2plc.sln`
   - 프로토콜별 `dotnet test` (실 장비 필요 시 조건부 실행)
   - 필요 시 `TotalProtocolTest`로 교차 검증
4. **문서화**
   - 변경 사항을 본 문서 또는 프로토콜별 세부 문서(`docs/`)에 기록
   - 환경 변수/테스트 태그 업데이트 시 README 반영

---

## 7. 향후 확장 가이드

- **새 프로토콜 추가 시**
  1. `src/protocol/<vendor>/` 구조를 동일하게 복제
  2. Core/Protocol/Client/Tests 레이어 정의
  3. `ProtocolTestHelper`를 통해 공통 로그 및 실패 처리 적용
  4. `docs/specs/protocols/`에 신규 프로토콜 세부 스펙 문서를 생성

- **로그/에러 메시지 표준화**
  - 오류 문자열은 `"<Operation> failed: <Detail>"` 패턴을 유지
  - 로그 첨부 시 두 줄 이상의 공백 줄을 삽입하여 가독성을 높임

- **테스트 자동화**
  - CI 환경에서는 실제 PLC 환경을 조건부로 구성 (환경 변수 기반 skip)
  - 향후 `ProtocolTestHelper.AutomatedTestRunner`를 활용한 통합 리포트 생성 계획

---

### 부록: 참조 디렉터리

```text
src/protocol/
├── ab
│   ├── Ev2.AbProtocol/...
│   └── Ev2.AbProtocol.Test/...
├── mitsubishi
│   ├── Ev2.MxProtocol/...
│   └── Ev2.MxProtocol.Tests/...
├── lselectric
│   ├── Ev2.LsProtocol/...
│   └── Ev2.LsProtocol.Tests/...
└── siemens
    ├── Ev2.S7Protocol/...
    └── Ev2.S7Protocol.Tests/...
```

각 디렉터리의 `TestHelpers.fs` / `ClientHelpers.fs` 에 공통 테스트 인프라가 정의되어 있으므로, 변경 시 상호 일관성을 유지한다.

---

본 스펙은 지속적으로 업데이트되어야 하며, 프로토콜별 개선 사항이 확인될 때마다 해당 섹션을 확장하거나 전용 문서를 추가한다.
