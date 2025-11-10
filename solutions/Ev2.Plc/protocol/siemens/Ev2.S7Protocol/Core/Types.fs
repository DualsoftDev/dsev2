namespace Ev2.S7Protocol.Core

open System
open System.Net.Sockets

/// CPU 타입
type CpuType = 
    | S7200 = 0
    | S7300 = 10
    | S7400 = 20
    | S71200 = 30
    | S71500 = 40

/// 데이터 영역 타입
type DataArea =
    | ProcessInput = 0x81      // I 영역
    | ProcessOutput = 0x82     // Q 영역
    | Merker = 0x83           // M 영역
    | DataBlock = 0x84        // DB 영역
    | Counter = 0x1C          // C 영역
    | Timer = 0x1D            // T 영역

/// S7 데이터 타입
type S7DataType =
    | Bit           // 1 bit
    | Byte          // 8 bit
    | Word          // 16 bit
    | DWord         // 32 bit
    | Int           // 16 bit signed
    | DInt          // 32 bit signed
    | Real          // 32 bit float
    | LReal         // 64 bit double
    | String of int // 문자열 (최대 길이)
    | Array of S7DataType * int

/// 전송 크기
type TransportSize =
    | Bit = 0x01
    | Byte = 0x02
    | Char = 0x03
    | Word = 0x04
    | Int = 0x05
    | DWord = 0x06
    | DInt = 0x07
    | Real = 0x08

/// S7 함수 코드
type S7Function =
    | ReadVar = 0x04
    | WriteVar = 0x05
    | RequestDownload = 0x1A
    | DownloadBlock = 0x1B
    | DownloadEnded = 0x1C
    | StartUpload = 0x1D
    | Upload = 0x1E
    | EndUpload = 0x1F
    | PlcControl = 0x28
    | PlcStop = 0x29
    | SetupComm = 0xF0

/// PDU 타입
type PDUType =
    | Job = 0x01      // 작업 요청
    | Ack = 0x02      // 확인 (응답 데이터 없음)
    | AckData = 0x03  // 데이터 응답
    | UserData = 0x07 // 사용자 데이터

/// 연결 설정
type S7Config = {
    Name: string
    IpAddress: string
    CpuType: CpuType
    Rack: int
    Slot: int
    Port: int
    LocalTSAP: int
    RemoteTSAP: int
    Timeout: TimeSpan
    MaxPDUSize: int
    Password: string option  // S7 password for write access
}

/// PLC 정보
type PlcInfo = {
    Name: string
    ModuleTypeName: string
    SerialNumber: string
    ASName: string
    ModuleName: string
    Copyright: string
}

/// 읽기 요청
type ReadRequest = {
    Area: DataArea
    DBNumber: int
    StartByte: int
    BitOffset: int
    Amount: int
    DataType: S7DataType
}

/// 쓰기 요청
type WriteRequest = {
    Area: DataArea
    DBNumber: int
    StartByte: int
    BitOffset: int
    Data: byte[]
    DataType: S7DataType
}

/// 읽기 아이템
type S7DataItem = {
    Area: DataArea
    WordLen: TransportSize
    DBNumber: int
    Start: int
    Amount: int
    Data: byte[]
}

/// 프로토콜 에러
type S7ProtocolError =
    | NoError
    | ConnectionError of message: string
    | SessionError of message: string
    | TcpError of error: SocketError
    | IsoError of code: byte * message: string
    | S7Error of code: uint16 * message: string
    | TimeoutError
    | InvalidPDU
    | InvalidData
    | WrongPDUSize
    | DataError of message: string
    | CPUError of code: uint16
    | NetworkError of error: SocketError
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
        | TcpError err -> sprintf "TCP error: %A" err
        | IsoError (code, msg) -> sprintf "ISO error 0x%02X: %s" code msg
        | S7Error (code, msg) -> sprintf "S7 error 0x%04X: %s" code msg
        | TimeoutError -> "Operation timed out"
        | InvalidPDU -> "Invalid PDU"
        | InvalidData -> "Invalid data"
        | WrongPDUSize -> "Wrong PDU size"
        | DataError msg -> sprintf "Data error: %s" msg
        | CPUError code -> sprintf "CPU error 0x%04X" code
        | NetworkError err -> sprintf "Network error: %A" err
        | UnknownError msg -> sprintf "Unknown error: %s" msg

/// 에러 클래스
type ErrorClass =
    | NoError = 0x00
    | ApplicationRelationship = 0x81
    | ObjectDefinition = 0x82
    | NoResourcesAvailable = 0x83
    | ServiceProcessing = 0x84
    | Supplies = 0x85
    | AccessError = 0x87

/// 에러 코드
type ErrorCode =
    | NoError = 0x00
    | InvalidBlock = 0x05
    | ObjectNotFound = 0x0A
    | OutOfRange = 0x05
    | InvalidAddress = 0x06
    | DataTypeNotSupported = 0x07
    | DataTypeInconsistent = 0x08
    | ObjectDoesNotExist = 0x09
    | HardwareFault = 0x01

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

/// TPKT 헤더
type TPKTHeader = {
    Version: byte
    Reserved: byte
    Length: uint16
}

/// COTP 연결 요청
type COTPConnectionRequest = {
    DestinationReference: uint16
    SourceReference: uint16
    ClassOptions: byte
    ParameterCode: byte
    ParameterLength: byte
    SourceTSAP: uint16
    DestinationTSAP: uint16
    TPDUSize: byte
}

/// COTP 연결 확인
type COTPConnectionConfirm = {
    DestinationReference: uint16
    SourceReference: uint16
    ClassOptions: byte
    ParameterCode: byte
    ParameterLength: byte
    TPDUSize: byte
}

/// COTP 데이터
type COTPData = {
    LastDataUnit: bool
    Data: byte[]
}

/// S7 헤더
type S7Header = {
    ProtocolId: byte       // 항상 0x32
    PDUType: PDUType
    RedundancyId: uint16   // 보통 0
    ProtocolDataUnitRef: uint16
    ParameterLength: uint16
    DataLength: uint16
    ErrorClass: byte option
    ErrorCode: byte option
}

/// S7 작업 요청 헤더
type S7JobHeader = {
    Function: S7Function
    ItemCount: byte
}

/// S7 읽기/쓰기 파라미터
type S7RequestItem = {
    VariableSpec: byte    // 0x12
    Length: byte
    SyntaxId: byte       // 0x10 = Address data
    TransportSize: TransportSize
    ItemCount: uint16
    DBNumber: uint16
    Area: DataArea
    BitAddress: uint32   // 3 bytes for address + 1 byte for bit
}

/// S7 응답 데이터
type S7ResponseItem = {
    ReturnCode: byte
    TransportSize: TransportSize
    DataLength: uint16
    Data: byte[]
}

/// Helper 함수들
module Types =
    /// CPU 타입을 문자열로 변환
    let cpuTypeToString = function
        | CpuType.S7200 -> "S7200"
        | CpuType.S7300 -> "S7300"
        | CpuType.S7400 -> "S7400"
        | CpuType.S71200 -> "S71200"
        | CpuType.S71500 -> "S71500"
        | _ -> "Unknown"
    
    /// 문자열을 CPU 타입으로 변환
    let parseCpuType (str: string) =
        match str.ToUpper() with
        | "S7200" -> CpuType.S7200
        | "S7300" -> CpuType.S7300
        | "S7400" -> CpuType.S7400
        | "S71200" -> CpuType.S71200
        | "S71500" -> CpuType.S71500
        | _ -> CpuType.S7300  // 기본값
    
    /// 데이터 타입 크기 계산
    let getDataTypeSize = function
        | Bit -> 1
        | Byte -> 1
        | Word -> 2
        | DWord -> 4
        | Int -> 2
        | DInt -> 4
        | Real -> 4
        | LReal -> 8
        | String size -> size + 2  // 길이 정보 포함
        | Array (baseType, count) ->
            let baseSize = 
                match baseType with
                | Bit -> 1
                | Byte -> 1
                | Word -> 2
                | DWord -> 4
                | Int -> 2
                | DInt -> 4
                | Real -> 4
                | LReal -> 8
                | String s -> s + 2
                | Array _ -> 1  // 중첩 배열은 지원하지 않음
            baseSize * count
    
    /// TransportSize를 S7DataType으로 변환
    let transportSizeToDataType = function
        | TransportSize.Bit -> Bit
        | TransportSize.Byte -> Byte
        | TransportSize.Word -> Word
        | TransportSize.DWord -> DWord
        | TransportSize.Int -> Int
        | TransportSize.DInt -> DInt
        | TransportSize.Real -> Real
        | _ -> Byte
    
    /// S7DataType을 TransportSize로 변환
    let dataTypeToTransportSize = function
        | Bit -> TransportSize.Bit
        | Byte -> TransportSize.Byte
        | Word -> TransportSize.Word
        | DWord -> TransportSize.DWord
        | Int -> TransportSize.Int
        | DInt -> TransportSize.DInt
        | Real -> TransportSize.Real
        | LReal -> TransportSize.Real
        | String _ -> TransportSize.Char
        | Array (baseType, _) ->
            match baseType with
            | Bit -> TransportSize.Bit
            | Byte -> TransportSize.Byte
            | Word -> TransportSize.Word
            | DWord -> TransportSize.DWord
            | Int -> TransportSize.Int
            | DInt -> TransportSize.DInt
            | Real -> TransportSize.Real
            | LReal -> TransportSize.Real
            | String _ -> TransportSize.Char
            | Array _ -> TransportSize.Byte
    
    /// TSAP 계산 (Rack과 Slot으로부터)
    let calculateTSAP (connectionType: byte) (rack: int) (slot: int) =
        (int connectionType <<< 8) ||| (rack * 0x20 + slot)