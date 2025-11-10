namespace Ev2.LsProtocol.Core

open System

/// LS Electric PLC 장치 타입
type LsDeviceType =
    | XGI of device: string  // XGI 장치 (예: %IW0, %QD1)
    | XGK of device: string  // XGK 장치 (예: D100, M0)

/// LS Electric 연결 타입
type LsConnectionType =
    | Ethernet
    | RS232
    | RS485

/// LS Electric PLC 모델
type LsPlcModel =
    | XGI
    | XGK
    | XGT

/// LS Electric 프로토콜 설정
type LsProtocolConfig = {
    PlcModel: LsPlcModel
    ConnectionType: LsConnectionType
    IpAddress: string option
    Port: int option
    LocalEthernet: bool
    ComPort: string option
    BaudRate: int option
    Timeout: TimeSpan
}

/// LS Electric 프로토콜 에러
type LsProtocolError =
    | NoError
    | ConnectionError of message: string
    | SessionError of message: string
    | XgtError of code: uint16 * message: string
    | DeviceError of message: string
    | TimeoutError
    | InvalidCommand of command: string
    | InvalidDevice of device: string
    | InvalidAddress of address: string
    | InvalidData of message: string
    | CommunicationError of message: string
    | NetworkError of error: System.Net.Sockets.SocketError
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
        | XgtError (code, msg) -> sprintf "XGT error 0x%04X: %s" code msg
        | DeviceError msg -> sprintf "Device error: %s" msg
        | TimeoutError -> "Operation timed out"
        | InvalidCommand cmd -> sprintf "Invalid command: %s" cmd
        | InvalidDevice dev -> sprintf "Invalid device: %s" dev
        | InvalidAddress addr -> sprintf "Invalid address: %s" addr
        | InvalidData msg -> sprintf "Invalid data: %s" msg
        | CommunicationError msg -> sprintf "Communication error: %s" msg
        | NetworkError err -> sprintf "Network error: %A" err
        | UnknownError msg -> sprintf "Unknown error: %s" msg

/// LS Electric 에러 코드 (호환성 유지)
type LsErrorCode =
    | Success = 0x00
    | InvalidCommand = 0x01
    | InvalidDevice = 0x02
    | InvalidAddress = 0x03
    | InvalidLength = 0x04
    | DeviceError = 0x05
    | CommunicationError = 0x06
    | TimeoutError = 0x07