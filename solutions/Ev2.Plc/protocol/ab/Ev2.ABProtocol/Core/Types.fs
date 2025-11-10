namespace Ev2.AbProtocol.Core

open System
open System.Collections.Generic

/// PLC 유형 정의
type PlcType = 
    | CompactLogix
    | ControlLogix
    | MicroLogix
    | PLC5
    | SLC500
    | MicroLogix800
    | MicroLogix1000
    | MicroLogix1100
    | MicroLogix1200
    | MicroLogix1400
    | MicroLogix1500
    
    member this.IsLogixFamily =
        match this with
        | CompactLogix | ControlLogix -> true
        | _ -> false
    
    member this.IsMicroLogixFamily =
        match this with
        | MicroLogix | MicroLogix800 | MicroLogix1000 
        | MicroLogix1100 | MicroLogix1200 | MicroLogix1400 | MicroLogix1500 -> true
        | _ -> false
  

/// 데이터 타입 정의
type DataType =
    | BOOL
    | SINT
    | INT
    | DINT
    | LINT
    | USINT
    | UINT
    | UDINT
    | ULINT
    | REAL
    | LREAL
    | STRING of maxLength: int
    | ARRAY of elementType: DataType * count: int
    | STRUCT of fields: Map<string, DataType>
    | TIMER
    | COUNTER
    
    /// 데이터 타입의 바이트 크기 계산
    member this.ByteSize =
        match this with
        | BOOL -> 1
        | SINT | USINT -> 1
        | INT | UINT -> 2
        | DINT | UDINT | REAL -> 4
        | LINT | ULINT | LREAL -> 8
        | STRING length -> 88 + length  // 4-byte header + 4-byte length + 80 default + length
        | ARRAY (elementType, count) -> elementType.ByteSize * count
        | STRUCT fields -> 
            fields 
            |> Map.toSeq 
            |> Seq.sumBy (snd >> fun dt -> dt.ByteSize)
        | TIMER -> 12    // PRE (DINT) + ACC (DINT) + EN/TT/DN bits (DINT)
        | COUNTER -> 12  // PRE (DINT) + ACC (DINT) + CU/CD/DN/OV/UN bits (DINT)
    
    /// CIP 타입 코드 반환
    member this.CIPCode =
        match this with
        | BOOL -> 0xC1uy
        | SINT -> 0xC2uy
        | INT -> 0xC3uy
        | DINT -> 0xC4uy
        | LINT -> 0xC5uy
        | USINT -> 0xC6uy
        | UINT -> 0xC7uy
        | UDINT -> 0xC8uy
        | ULINT -> 0xC9uy
        | REAL -> 0xCAuy
        | LREAL -> 0xCBuy
        | STRING _ -> 0xD0uy
        | TIMER -> 0xD3uy      // Structure type code for TIMER
        | COUNTER -> 0xD4uy    // Structure type code for COUNTER
        | ARRAY _ | STRUCT _ -> 0x02uy

/// TIMER 구조체 (Allen-Bradley 표준)
type TimerStructure = {
    PRE: int32      // Preset value
    ACC: int32      // Accumulated value
    EN: bool        // Enable bit
    TT: bool        // Timer timing bit
    DN: bool        // Done bit
}
    with
    static member Empty = 
        { PRE = 0; ACC = 0; EN = false; TT = false; DN = false }
    
    /// TIMER를 12바이트 배열로 직렬화
    member this.ToBytes() =
        let bytes = Array.zeroCreate<byte> 12
        Array.Copy(BitConverter.GetBytes(this.PRE), 0, bytes, 0, 4)
        Array.Copy(BitConverter.GetBytes(this.ACC), 0, bytes, 4, 4)
        
        // Status bits (EN=bit29, TT=bit30, DN=bit31)
        let mutable status = 0
        if this.EN then status <- status ||| (1 <<< 29)
        if this.TT then status <- status ||| (1 <<< 30)
        if this.DN then status <- status ||| (1 <<< 31)
        Array.Copy(BitConverter.GetBytes(status), 0, bytes, 8, 4)
        
        bytes
    
    /// 12바이트 배열에서 TIMER 역직렬화
    static member FromBytes(bytes: byte[]) =
        if bytes.Length < 12 then
            TimerStructure.Empty
        else
            let pre = BitConverter.ToInt32(bytes, 0)
            let acc = BitConverter.ToInt32(bytes, 4)
            let status = BitConverter.ToInt32(bytes, 8)
            
            { PRE = pre
              ACC = acc
              EN = (status &&& (1 <<< 29)) <> 0
              TT = (status &&& (1 <<< 30)) <> 0
              DN = (status &&& (1 <<< 31)) <> 0 }

/// COUNTER 구조체 (Allen-Bradley 표준)
type CounterStructure = {
    PRE: int32      // Preset value
    ACC: int32      // Accumulated value
    CU: bool        // Count up enable bit
    CD: bool        // Count down enable bit
    DN: bool        // Done bit
    OV: bool        // Overflow bit
    UN: bool        // Underflow bit
}
    with
    static member Empty = 
        { PRE = 0; ACC = 0; CU = false; CD = false; DN = false; OV = false; UN = false }
    
    /// COUNTER를 12바이트 배열로 직렬화
    member this.ToBytes() =
        let bytes = Array.zeroCreate<byte> 12
        Array.Copy(BitConverter.GetBytes(this.PRE), 0, bytes, 0, 4)
        Array.Copy(BitConverter.GetBytes(this.ACC), 0, bytes, 4, 4)
        
        // Status bits (CU=bit27, CD=bit28, DN=bit29, OV=bit30, UN=bit31)
        let mutable status = 0
        if this.CU then status <- status ||| (1 <<< 27)
        if this.CD then status <- status ||| (1 <<< 28)
        if this.DN then status <- status ||| (1 <<< 29)
        if this.OV then status <- status ||| (1 <<< 30)
        if this.UN then status <- status ||| (1 <<< 31)
        Array.Copy(BitConverter.GetBytes(status), 0, bytes, 8, 4)
        
        bytes
    
    /// 12바이트 배열에서 COUNTER 역직렬화
    static member FromBytes(bytes: byte[]) =
        if bytes.Length < 12 then
            CounterStructure.Empty
        else
            let pre = BitConverter.ToInt32(bytes, 0)
            let acc = BitConverter.ToInt32(bytes, 4)
            let status = BitConverter.ToInt32(bytes, 8)
            
            { PRE = pre
              ACC = acc
              CU = (status &&& (1 <<< 27)) <> 0
              CD = (status &&& (1 <<< 28)) <> 0
              DN = (status &&& (1 <<< 29)) <> 0
              OV = (status &&& (1 <<< 30)) <> 0
              UN = (status &&& (1 <<< 31)) <> 0 }

/// 접근 레벨
type AccessLevel =
    | ReadOnly
    | WriteOnly
    | ReadWrite
    | Internal

/// 태그 정보
type TagInfo = {
    Name: string
    DataType: DataType
    ArrayDimensions: int[]
    AccessLevel: AccessLevel
    Description: string option
    Alias: string option
}

/// PLC 상태
type PlcStatus =
    | Running
    | ProgramMode
    | Faulted
    | Unknown

/// 디바이스 식별 정보
type DeviceIdentity = {
    VendorId: uint16
    DeviceType: uint16
    ProductCode: uint16
    MajorRevision: byte
    MinorRevision: byte
    Status: uint16
    SerialNumber: uint32
    ProductNameLength: byte
    ProductName: string
    State: byte
}

/// PLC 정보
type PlcInfo = {
    Vendor: string
    ProductType: string
    ProductCode: int
    ProductName: string
    Revision: Version
    SerialNumber: string
    Status: PlcStatus
    Identity: DeviceIdentity option
}

/// 연결 설정
type ConnectionConfig = {
    IpAddress: string
    Port: int
    PlcType: PlcType
    Slot: byte
    Timeout: TimeSpan
    MaxRetries: int
    RetryDelay: TimeSpan
    ConnectionPath: byte[] option
    UseConnectedMessaging: bool
    MaxConcurrentRequests: int
}
    with
    /// 기본 설정 생성
    static member Create(ipAddress: string, ?port: int, ?plcType: PlcType, ?slot: byte) =
        {
            IpAddress = ipAddress
            Port = defaultArg port 44818
            PlcType = defaultArg plcType PlcType.CompactLogix
            Slot = defaultArg slot 0uy
            Timeout = TimeSpan.FromSeconds(5.0)
            MaxRetries = 3
            RetryDelay = TimeSpan.FromMilliseconds(100.0)
            ConnectionPath = None
            UseConnectedMessaging = false
            MaxConcurrentRequests = 10
        }

/// 읽기 요청
type ReadRequest = {
    TagName: string
    StartIndex: int
    ElementCount: int
}
    with
    /// 단일 태그 읽기 요청 생성
    static member Single(tagName: string, ?elementCount: int) =
        {
            TagName = tagName
            StartIndex = 0
            ElementCount = defaultArg elementCount 1
        }

/// 쓰기 요청
type WriteRequest = {
    TagName: string
    StartIndex: int
    Values: obj[]
}
    with
    /// 단일 태그 쓰기 요청 생성
    static member Single(tagName: string, value: obj) =
        {
            TagName = tagName
            StartIndex = 0
            Values = [| value |]
        }

/// 배치 작업 요청
type BatchRequest =
    | BatchRead of ReadRequest[]
    | BatchWrite of WriteRequest[]
    | Mixed of (Choice<ReadRequest, WriteRequest>)[]

/// 통신 통계
type CommunicationStats = {
    PacketsSent: int64
    PacketsReceived: int64
    BytesSent: int64
    BytesReceived: int64
    ErrorCount: int64
    LastError: DateTime option
    LastErrorMessage: string option
    AverageResponseTime: float
    MinResponseTime: float
    MaxResponseTime: float
    ConnectionUptime: TimeSpan
    SuccessRate: float
}
    with
    /// 초기 통계 생성
    static member Empty =
        {
            PacketsSent = 0L
            PacketsReceived = 0L
            BytesSent = 0L
            BytesReceived = 0L
            ErrorCount = 0L
            LastError = None
            LastErrorMessage = None
            AverageResponseTime = 0.0
            MinResponseTime = Double.MaxValue
            MaxResponseTime = 0.0
            ConnectionUptime = TimeSpan.Zero
            SuccessRate = 100.0
        }

/// 데이터 품질
type DataQuality =
    | Good
    | Bad
    | Uncertain
    | Stale

/// 스트리밍 설정
type StreamingConfig = {
    Tags: string[]
    SampleRate: TimeSpan
    BufferSize: int
    EnableCompression: bool
    EnableTimestamp: bool
}

/// 스트리밍 데이터
type StreamingData = {
    TagName: string
    Timestamp: DateTime
    Values: obj[]
    Quality: DataQuality
}

/// 프로토콜 에러
type AbProtocolError =
    | NoError
    | ConnectionError of message: string
    | SessionError of message: string
    | CIPError of code: byte * message: string
    | PCCCError of code: byte * message: string
    | TimeoutError
    | InvalidTag of tagName: string
    | InvalidDataType of expected: DataType * actual: DataType
    | BufferOverflow
    | NetworkError of System.Net.Sockets.SocketError
    | UnknownError of message: string
    
    /// 에러가 없는지 확인
    member this.IsSuccess = 
        match this with
        | NoError -> true
        | _ -> false
    
    /// 에러가 있는지 확인
    member this.IsError = not this.IsSuccess
    
    /// 에러 메시지 포매팅
    member this.Message =
        match this with
        | NoError -> "No error"
        | ConnectionError msg -> sprintf "Connection error: %s" msg
        | SessionError msg -> sprintf "Session error: %s" msg
        | CIPError (code, msg) -> sprintf "CIP error 0x%02X: %s" code msg
        | PCCCError (code, msg) -> sprintf "PCCC error 0x%02X: %s" code msg
        | TimeoutError -> "Operation timed out"
        | InvalidTag tag -> sprintf "Invalid tag: %s" tag
        | InvalidDataType (expected, actual) -> 
            sprintf "Invalid data type (expected %A, actual %A)" expected actual
        | BufferOverflow -> "Buffer overflow"
        | NetworkError err -> sprintf "Network error: %A" err
        | UnknownError msg -> sprintf "Unknown error: %s" msg

/// 이벤트 인자
type DataReceivedEventArgs(tagName: string, values: obj[], timestamp: DateTime) =
    inherit EventArgs()
    member _.TagName = tagName
    member _.Values = values
    member _.Timestamp = timestamp

type ErrorOccurredEventArgs(error: AbProtocolError, timestamp: DateTime) =
    inherit EventArgs()
    member _.Error = error
    member _.Timestamp = timestamp

type ConnectionStateChangedEventArgs(connected: bool, timestamp: DateTime) =
    inherit EventArgs()
    member _.Connected = connected
    member _.Timestamp = timestamp

/// 타입 관련 유틸리티
module TypeHelpers =
    
    /// 데이터 타입의 바이트 크기를 재귀적으로 계산
    let rec getDataTypeSize = function
        | BOOL -> 1
        | SINT | USINT -> 1
        | INT | UINT -> 2
        | DINT | UDINT | REAL -> 4
        | LINT | ULINT | LREAL -> 8
        | STRING size -> size + 4
        | ARRAY (baseType, count) -> getDataTypeSize baseType * count
        | STRUCT fields ->
            fields 
            |> Map.fold (fun acc _ fieldType -> acc + getDataTypeSize fieldType) 0
        | TIMER -> 12
        | COUNTER -> 12
    
    /// 데이터 타입을 평탄화 (배열 차원 추출)
    let rec flattenDataType = function
        | ARRAY (inner, len) ->
            let baseType, dims = flattenDataType inner
            baseType, dims @ [len]
        | other -> other, []
    
    /// CIP 상태 코드를 에러 메시지로 변환
    let cipStatusToMessage (status: byte) =
        match status with
        | 0x00uy -> "Success"
        | 0x01uy -> "Connection failure"
        | 0x02uy -> "Resource unavailable"
        | 0x03uy -> "Invalid parameter"
        | 0x04uy -> "Path segment error"
        | 0x05uy -> "Path destination unknown"
        | 0x06uy -> "Partial transfer"
        | 0x07uy -> "Connection lost"
        | 0x08uy -> "Service not supported"
        | 0x09uy -> "Invalid attribute"
        | 0x16uy -> "Object does not exist"
        | _ -> sprintf "CIP error 0x%02X" status
    
    /// TIMER 배열을 바이트 배열로 변환
    let timersToBytes (timers: TimerStructure[]) =
        timers 
        |> Array.collect (fun timer -> timer.ToBytes())
    
    /// 바이트 배열을 TIMER 배열로 변환
    let bytesToTimers (bytes: byte[]) =
        let timerSize = 12
        let count = bytes.Length / timerSize
        Array.init count (fun i ->
            let offset = i * timerSize
            let timerBytes = Array.sub bytes offset timerSize
            TimerStructure.FromBytes(timerBytes))
    
    /// COUNTER 배열을 바이트 배열로 변환
    let countersToBytes (counters: CounterStructure[]) =
        counters 
        |> Array.collect (fun counter -> counter.ToBytes())
    
    /// 바이트 배열을 COUNTER 배열로 변환
    let bytesToCounters (bytes: byte[]) =
        let counterSize = 12
        let count = bytes.Length / counterSize
        Array.init count (fun i ->
            let offset = i * counterSize
            let counterBytes = Array.sub bytes offset counterSize
            CounterStructure.FromBytes(counterBytes))

/// Result 타입 별칭 (F# 스타일)
module Result =
    /// AbProtocolError를 사용하는 Result 타입
    type ProtocolResult<'T> = Result<'T, AbProtocolError>
    
    /// 성공 결과 생성
    let ok value : ProtocolResult<'T> = Ok value
    
    /// 에러 결과 생성
    let error err : ProtocolResult<'T> = Error err
    
    /// NoError를 Ok로 변환
    let fromError = function
        | NoError -> Ok ()
        | err -> Error err
    
    /// 에러 메시지로 에러 생성
    let errorMsg msg = Error (UnknownError msg)
    
    /// Option을 Result로 변환
    let ofOption errorValue = function
        | Some value -> Ok value
        | None -> Error errorValue