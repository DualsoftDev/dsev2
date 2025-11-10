# 통합 패킷 로깅 시스템 가이드

## 🎯 개요

`ProtocolTestHelper.PacketLogger`는 모든 PLC 프로토콜에서 공통으로 사용할 수 있는 통합 패킷 로깅 시스템입니다.

## 🚀 기본 사용법

### 환경 변수 설정
```bash
# 통합 패킷 로깅 활성화
export PROTOCOL_PACKET_LOGGING=true

# 로그 디렉토리 설정 (선택사항)
export PROTOCOL_LOG_DIR=/path/to/protocol_logs
```

### 프로토콜별 Frame Analyzer 구현
```fsharp
open ProtocolTestHelper.PacketLogger

type MyProtocolFrameAnalyzer() =
    interface IFrameAnalyzer with
        member _.ProtocolName = "MyProtocol"
        
        member _.AnalyzeFrame(bytes: byte[]) =
            // 프로토콜별 프레임 분석 로직
            $"Frame analysis for {bytes.Length} bytes"
```

### 기본 로깅
```fsharp
open ProtocolTestHelper.PacketLogger

// 요청 패킷 로깅
logRequest 
    "MyProtocol"           // 프로토콜 이름
    "192.168.1.100"        // 호스트
    502                    // 포트
    requestBytes           // 패킷 데이터
    "Read holding registers" // 설명
    Map.empty              // 추가 속성
    (Some analyzer)        // Frame analyzer

// 응답 패킷 로깅
logResponse 
    "MyProtocol"
    "192.168.1.100"
    502
    responseBytes
    "Read response"
    Map.empty
    (Some analyzer)

// 에러 로깅
logError
    "MyProtocol"
    "192.168.1.100"
    502
    "Connection timeout"
    "Register read operation"
    Map.empty
```

### Operation Tracker 사용
```fsharp
// 연속된 작업에 대한 추적
let tracker = OperationTracker("ModbusRTU", "192.168.1.100", 502)

// 자동으로 operation ID가 부여됨
let opDesc = tracker.LogRequest(requestBytes, "Read coils", props, analyzer)
// -> "Op1: Read coils"

tracker.LogResponse(responseBytes, opDesc, props, analyzer)
tracker.LogError("Timeout error", "Coil read", props)
```

## 📁 로그 파일 구조

### 디렉토리 구조
```
protocol_logs/
├── modbus/
│   ├── modbus_packet_2024-10-11_14-30-45-123_REQUEST.log
│   ├── modbus_packet_2024-10-11_14-30-45-156_RESPONSE.log
│   └── modbus_error_2024-10-11_14-30-46-789.log
├── melsec/
│   ├── melsec_packet_2024-10-11_14-30-45-123_REQUEST.log
│   └── melsec_error_2024-10-11_14-30-46-456.log
└── siemens/
    └── siemens_packet_2024-10-11_14-30-45-123_REQUEST.log
```

### 로그 파일 내용
```
=== MODBUS Packet Log ===
Timestamp: 2024-10-11 14:30:45.123
Direction: REQUEST
Host: 192.168.1.100:502
Description: Op1: Read holding registers
Length: 6 bytes

Properties:
  FunctionCode: 0x03
  StartAddress: 1000
  Quantity: 10

Frame Analysis:
=== MODBUS RTU Frame Analysis ===
Device Address: 0x01
Function Code: 0x03 (Read Holding Registers)
Start Address: 1000 (0x03E8)
Quantity: 10 (0x000A)
CRC: 0xC5CA
✓ CRC Valid

Enhanced Hex Dump:
00000000: 01 03 03 E8 00 0A C5 CA                         | ........

Raw Bytes:
01 03 03 E8 00 0A C5 CA

Standard Hex Dump:
0000: 01 03 03 E8 00 0A C5 CA                          ........
```

## 🔧 프로토콜 통합 예제

### 1. Modbus 프로토콜 통합

```fsharp
// ModbusFrameAnalyzer.fs
type ModbusFrameAnalyzer() =
    interface IFrameAnalyzer with
        member _.ProtocolName = "Modbus"
        
        member _.AnalyzeFrame(bytes: byte[]) =
            let sb = StringBuilder()
            sb.AppendLine("=== MODBUS RTU Frame Analysis ===") |> ignore
            
            if bytes.Length >= 2 then
                sb.AppendLine($"Device Address: 0x{bytes.[0]:X2}") |> ignore
                sb.AppendLine($"Function Code: 0x{bytes.[1]:X2}") |> ignore
                
                match bytes.[1] with
                | 0x03uy -> sb.AppendLine("  -> Read Holding Registers") |> ignore
                | 0x04uy -> sb.AppendLine("  -> Read Input Registers") |> ignore
                | 0x06uy -> sb.AppendLine("  -> Write Single Register") |> ignore
                | _ -> sb.AppendLine("  -> Unknown function") |> ignore
                
                if bytes.Length >= 4 then
                    let address = (uint16 bytes.[2] <<< 8) ||| uint16 bytes.[3]
                    sb.AppendLine($"Address: {address} (0x{address:X4})") |> ignore
            
            sb.ToString()

// ModbusClient with logging
type LoggingModbusClient(host: string, port: int) =
    let tracker = OperationTracker("Modbus", host, port)
    let analyzer = ModbusFrameAnalyzer() :> IFrameAnalyzer
    
    member _.ReadHoldingRegisters(address: int, count: int) =
        let request = [| 0x01uy; 0x03uy; byte (address >>> 8); byte address; byte (count >>> 8); byte count |]
        let props = Map.ofList [("FunctionCode", "0x03"); ("Address", address.ToString()); ("Count", count.ToString())]
        
        let opDesc = tracker.LogRequest(request, $"Read {count} holding registers from {address}", props, Some analyzer)
        
        // 실제 Modbus 통신 수행
        // let response = performModbusRead(...)
        
        // tracker.LogResponse(response, opDesc, props, Some analyzer)
        // response
        Ok [| 0x01uy; 0x03uy; 0x02uy; 0x12uy; 0x34uy |] // 예시 응답
```

### 2. Siemens S7 프로토콜 통합

```fsharp
// S7FrameAnalyzer.fs
type S7FrameAnalyzer() =
    interface IFrameAnalyzer with
        member _.ProtocolName = "S7"
        
        member _.AnalyzeFrame(bytes: byte[]) =
            let sb = StringBuilder()
            sb.AppendLine("=== SIEMENS S7 Frame Analysis ===") |> ignore
            
            if bytes.Length >= 4 then
                sb.AppendLine($"Protocol ID: 0x{bytes.[0]:X2}") |> ignore
                sb.AppendLine($"Message Type: 0x{bytes.[1]:X2}") |> ignore
                let length = (uint16 bytes.[2] <<< 8) ||| uint16 bytes.[3]
                sb.AppendLine($"Length: {length}") |> ignore
                
                if bytes.[1] = 0x01uy then
                    sb.AppendLine("  -> Job Request") |> ignore
                elif bytes.[1] = 0x03uy then
                    sb.AppendLine("  -> Ack Data") |> ignore
            
            sb.ToString()

// S7 클라이언트 사용 예
let s7Analyzer = S7FrameAnalyzer() :> IFrameAnalyzer
let s7Tracker = OperationTracker("S7", "192.168.1.200", 102)

// S7 read operation
let s7Request = [| 0x32uy; 0x01uy; 0x00uy; 0x00uy; (*...*) |]
let props = Map.ofList [("JobType", "Read"); ("DataBlock", "DB1")]
s7Tracker.LogRequest(s7Request, "Read DB1.DBW0", props, Some s7Analyzer) |> ignore
```

### 3. LS Electric XGT 프로토콜 통합

```fsharp
// XgtFrameAnalyzer.fs  
type XgtFrameAnalyzer() =
    interface IFrameAnalyzer with
        member _.ProtocolName = "XGT"
        
        member _.AnalyzeFrame(bytes: byte[]) =
            let sb = StringBuilder()
            sb.AppendLine("=== LS ELECTRIC XGT Frame Analysis ===") |> ignore
            
            if bytes.Length >= 20 then
                let companyId = System.Text.Encoding.ASCII.GetString(bytes.[0..7])
                sb.AppendLine($"Company ID: {companyId}") |> ignore
                
                if companyId.StartsWith("LSIS-XGT") then
                    sb.AppendLine("  -> Valid XGT frame") |> ignore
                    let command = (uint16 bytes.[16] <<< 8) ||| uint16 bytes.[17]
                    sb.AppendLine($"Command: 0x{command:X4}") |> ignore
                    
                    match command with
                    | 0x0054us -> sb.AppendLine("  -> Read request") |> ignore  
                    | 0x0055us -> sb.AppendLine("  -> Write request") |> ignore
                    | _ -> sb.AppendLine("  -> Unknown command") |> ignore
            
            sb.ToString()

// XGT 프로토콜 로깅 통합
let xgtAnalyzer = XgtFrameAnalyzer() :> IFrameAnalyzer
let xgtTracker = OperationTracker("XGT", "192.168.9.100", 2004)

xgtTracker.LogRequest(xgtFrame, "Read %MW100", Map.empty, Some xgtAnalyzer) |> ignore
```

## 🛠️ 고급 기능

### 로그 파일 관리
```fsharp
// 프로토콜별 로그 파일 조회
let modbusLogs = PacketLogger.getLogFiles "Modbus"
let melsecLogs = PacketLogger.getLogFiles "MELSEC"

// 오래된 로그 정리 (최근 20개만 유지)
PacketLogger.clearOldLogs "Modbus" 20
PacketLogger.clearOldLogs "MELSEC" 20

// 로그 요약 정보
let summary = PacketLogger.getLogSummary "Modbus"
printfn "%s" summary
// -> "Modbus Logs: 15 packets, 2 errors"
```

### 프로토콜별 설정
```fsharp
// 프로토콜별 로그 디렉토리
let modbusLogDir = PacketLogger.getLogDirectory "Modbus"
// -> protocol_logs/modbus/

let s7LogDir = PacketLogger.getLogDirectory "S7" 
// -> protocol_logs/s7/
```

### 확장된 속성 로깅
```fsharp
let extendedProps = Map.ofList [
    ("RequestId", "12345")
    ("ClientVersion", "1.2.3")
    ("Timeout", "5000ms")
    ("RetryCount", "3")
    ("DeviceModel", "FX5U-32MR")
    ("Protocol", "MODBUS RTU")
    ("Baud", "9600")
    ("Parity", "None")
]

tracker.LogRequest(frame, "Complex operation", extendedProps, analyzer) |> ignore
```

## 📊 통합 테스트 프레임워크

### 멀티 프로토콜 테스트
```fsharp
[<Fact>]
let ``Multi protocol packet logging test`` () =
    PacketLogger.LoggingEnabled <- true
    
    // 여러 프로토콜 동시 테스트
    let protocols = [
        ("Modbus", "192.168.1.100", 502, modbusAnalyzer)
        ("MELSEC", "192.168.1.120", 7777, melsecAnalyzer)
        ("S7", "192.168.1.200", 102, s7Analyzer)
    ]
    
    for (protocolName, host, port, analyzer) in protocols do
        let tracker = OperationTracker(protocolName, host, port)
        let testFrame = [| 0x01uy; 0x02uy; 0x03uy |]
        
        tracker.LogRequest(testFrame, "Multi-protocol test", Map.empty, Some analyzer) |> ignore
    
    // 전체 로그 요약
    for (protocolName, _, _, _) in protocols do
        printfn "%s" (PacketLogger.getLogSummary protocolName)
```

## 🎯 기존 프로토콜 마이그레이션

### 1. 기존 로깅 코드 교체
```fsharp
// Before (protocol-specific)
MyProtocolLogger.logPacket direction host port bytes description

// After (unified)
PacketLogger.logRequest protocolName host port bytes description Map.empty (Some analyzer)
```

### 2. 환경 변수 통합
```bash
# Before (protocol-specific)  
export MODBUS_PACKET_LOGGING=true
export MELSEC_PACKET_LOGGING=true
export S7_PACKET_LOGGING=true

# After (unified)
export PROTOCOL_PACKET_LOGGING=true
```

### 3. 로그 파일 위치 통합
```
# Before
logs/modbus/
logs/melsec/  
logs/s7/

# After  
protocol_logs/modbus/
protocol_logs/melsec/
protocol_logs/s7/
```

## 💡 베스트 프랙티스

1. **프로토콜 이름 일관성**: 대소문자 일관성 유지
2. **Frame Analyzer 구현**: 각 프로토콜별 상세 분석 제공
3. **Operation Tracking**: 연관된 요청/응답 추적
4. **속성 활용**: 컨텍스트 정보 풍부하게 제공
5. **로그 정리**: 정기적으로 오래된 로그 정리
6. **환경 변수**: 통합된 환경 변수 사용

통합 패킷 로깅 시스템으로 모든 PLC 프로토콜의 디버깅을 효율적으로 수행할 수 있습니다! 🎉