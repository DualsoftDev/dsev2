namespace Ev2.MxProtocol.Core

open System

/// Device codes as defined in MELSEC Protocol
[<RequireQualifiedAccess>]
type DeviceCode =
    // Bit devices
    | SM | SD | X | Y | M | L | F | V | B
    | SB | DX | DY | S | TS | TC | SS | SC
    | CS | CC | TN | CN | LCN | LSTS | LSTC | LSTN
    // Word devices  
    | D | W | R | ZR | T | C | Z | LZ
    | RD | LCS | GX | GY | SN | ST | LTS | LTC
    | A | U | K | SW
    
    member this.ToByte() =
        match this with
        // Bit devices (1 byte codes)
        | SM -> 0x91uy | SD -> 0xA9uy | X -> 0x9Cuy | Y -> 0x9Duy
        | M -> 0x90uy | L -> 0x92uy | F -> 0x93uy | V -> 0x94uy
        | B -> 0xA0uy | SB -> 0xA1uy | DX -> 0xA2uy | DY -> 0xA3uy
        | S -> 0x98uy | TS -> 0xC1uy | TC -> 0xC0uy | SS -> 0xC7uy
        | SC -> 0xC6uy | CS -> 0xC4uy | CC -> 0xC3uy | TN -> 0xC2uy
        | CN -> 0xC5uy | LCN -> 0x56uy | LSTS -> 0x59uy | LSTC -> 0x58uy
        | LSTN -> 0x5Auy
        // Word devices (2 byte codes - first byte)
        | D -> 0xA8uy | W -> 0xB4uy | R -> 0xAFuy | ZR -> 0xB0uy
        | T -> 0xC2uy | C -> 0xC5uy | Z -> 0xCCuy | LZ -> 0x62uy
        | RD -> 0x2Cuy | LCS -> 0x54uy | GX -> 0xC8uy | GY -> 0xC9uy
        | SN -> 0xC8uy | ST -> 0xC9uy | LTS -> 0x51uy | LTC -> 0x50uy
        | A -> 0xB5uy | U -> 0xABuy | K -> 0xABuy | SW -> 0xB5uy
        
    member this.IsWordDevice() =
        match this with
        | D | W | R | ZR | T | C | Z | LZ | RD | LCS
        | GX | GY | SN | ST | LTS | LTC | A | U | K | SW -> true
        | _ -> false

/// Frame types supported by MELSEC protocol
[<RequireQualifiedAccess>]
type FrameType =
    | QnA_3E_Binary      // Binary code, standard
    | QnA_3E_Ascii       // ASCII code, standard  
    | QnA_3C_Binary      // Binary code, serial
    | QnA_3C_Ascii       // ASCII code, serial
    | QnA_4C_Binary      // Binary code with sequence
    | QnA_4C_Ascii       // ASCII code with sequence
    | QnA_2C_Ascii       // QnA compatible 2C frame
    | A_1E_Binary        // A compatible 1E frame (binary)
    | A_1E_Ascii         // A compatible 1E frame (ASCII)
    | A_1C_Ascii         // A compatible 1C frame

/// Access route information for network routing
type AccessRoute = {
    NetworkNumber: byte
    StationNumber: byte
    IoNumber: uint16
    RelayType: byte
}

/// Configuration for MELSEC communication
type MelsecConfig = {
    Name: string
    Host: string
    Port: int
    Timeout: TimeSpan
    FrameType: FrameType
    AccessRoute: AccessRoute
    MonitoringTimer: uint16
}
with
    member this.TimeoutMilliseconds =
        int this.Timeout.TotalMilliseconds |> max 1000

/// Command codes for device memory operations
[<RequireQualifiedAccess>]
type CommandCode =
    // Device memory read/write
    | BatchRead = 0x0401us          // Batch read
    | BatchWrite = 0x1401us         // Batch write  
    | RandomRead = 0x0403us         // Random read
    | RandomWrite = 0x1402us        // Random write (Test)
    | MultiBlockRead = 0x0406us     // Multiple block batch read
    | MultiBlockWrite = 0x1406us    // Multiple block batch write
    | MonitorRegister = 0x0801us    // Monitor data registration
    | Monitor = 0x0802us            // Monitor
    
    // Buffer memory operations
    | BufferRead = 0x0613us         // Buffer memory read
    | BufferWrite = 0x1613us        // Buffer memory write
    
    // Intelligent module buffer memory
    | IntelligentRead = 0x0601us    // Intelligent module buffer read
    | IntelligentWrite = 0x1601us   // Intelligent module buffer write
    
    // PLC CPU control
    | RemoteRun = 0x1001us          // Remote RUN
    | RemoteStop = 0x1002us         // Remote STOP
    | RemotePause = 0x1003us        // Remote PAUSE
    | RemoteLatchClear = 0x1005us   // Remote latch clear
    | RemoteReset = 0x1006us        // Remote RESET
    | ReadCpuType = 0x0101us        // Read CPU type/name
    
    // File operations  
    | FileInfoRead = 0x0201us       // Read file information
    | FileSearch = 0x0203us         // File search
    | FileCreate = 0x1202us         // Create new file
    | FileWrite = 0x1203us          // Write to file
    | FileModify = 0x1204us         // Modify file information
    | FileDelete = 0x1205us         // Delete file
    | FileCopy = 0x1206us           // Copy file
    | FileRead = 0x0206us           // Read file
    | FileLock = 0x0808us           // File lock/unlock
    | MemoryDefrag = 0x1207us       // Memory defragmentation
    
    // QCPU specific file operations
    | DirectoryRead = 0x1810us      // Read directory/file information (QCPU)
    | DirectorySearch = 0x1811us    // Search directory/file (QCPU)
    | FileCreateQ = 0x1820us        // Create file (QCPU)
    | FileDeleteQ = 0x1822us        // Delete file (QCPU)
    | FileCopyQ = 0x1824us          // Copy file (QCPU)
    | FileAttributeChange = 0x1825us // Change file attributes (QCPU)
    | FileDateChange = 0x1826us     // Change date of file creation (QCPU)
    | FileOpen = 0x1827us           // Open file (QCPU)
    | FileReadQ = 0x1828us          // Read file (QCPU)
    | FileWriteQ = 0x1829us         // Write file (QCPU)
    | FileClose = 0x182Aus          // Close file (QCPU)
    
    // Extended functions
    | LoopbackTest = 0x0619us       // Loopback test
    | OnDemand = 0x2101us           // On-demand function
    | Global = 0x1618us             // Global function

/// Subcommand codes
[<RequireQualifiedAccess>]
type SubcommandCode =
    | BitUnits = 0x0001us           // Bit units (1 bit)
    | WordUnits = 0x0000us          // Word units
    | FileModifyDateTime = 0x0000us // Modify date/time of last update
    | FileModifyNameSize = 0x0001us // Modify filename/size
    | FileModifyBatch = 0x0002us    // Batch modification
    | FileWriteData = 0x0000us      // Write arbitrary data
    | FileWriteFill = 0x0001us      // Write identical data (FILL)
    | FileLockRegister = 0x0000us   // Register file lock
    | FileLockCancel = 0x0001us     // Cancel file lock

/// Mitsubishi 프로토콜 에러
type MxProtocolError =
    | NoError
    | ConnectionError of message: string
    | SessionError of message: string
    | MelsecError of code: uint16 * networkError: byte * stationError: byte * message: string
    | DeviceError of message: string
    | TimeoutError
    | InvalidCommand of command: string
    | InvalidDevice of device: string
    | InvalidAddress of address: string
    | InvalidData of message: string
    | FrameError of message: string
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
        | MelsecError (code, networkErr, stationErr, msg) -> 
            sprintf "MELSEC error 0x%04X (Network: 0x%02X, Station: 0x%02X): %s" code networkErr stationErr msg
        | DeviceError msg -> sprintf "Device error: %s" msg
        | TimeoutError -> "Operation timed out"
        | InvalidCommand cmd -> sprintf "Invalid command: %s" cmd
        | InvalidDevice dev -> sprintf "Invalid device: %s" dev
        | InvalidAddress addr -> sprintf "Invalid address: %s" addr
        | InvalidData msg -> sprintf "Invalid data: %s" msg
        | FrameError msg -> sprintf "Frame error: %s" msg
        | NetworkError err -> sprintf "Network error: %A" err
        | UnknownError msg -> sprintf "Unknown error: %s" msg

/// End code for response messages (호환성 유지)
type EndCode =
    | EndCodeSuccess of code: uint16
    | EndCodeError of code: uint16 * networkError: byte * stationError: byte
    
    member this.IsSuccess =
        match this with
        | EndCodeSuccess _ -> true
        | EndCodeError _ -> false
        
    member this.Code =
        match this with
        | EndCodeSuccess code -> code
        | EndCodeError (code, _, _) -> code

/// Device access specification
type DeviceAccess = {
    DeviceCode: DeviceCode
    HeadNumber: int
    Count: uint16
}

/// Random access device specification  
type RandomDeviceAccess = {
    DeviceCode: DeviceCode
    DeviceNumber: int
    AccessSize: byte // 0=word, 1=dword for word devices; 0=16bit, 1=32bit for bit devices
}

/// Block specification for multiple block operations
type BlockAccess = {
    DeviceCode: DeviceCode
    HeadNumber: int
    PointCount: uint16
}

/// Request message structure
type MelsecRequest = {
    Command: CommandCode
    Subcommand: SubcommandCode
    Payload: byte array
}

/// Response message structure  
type MelsecResponse = {
    EndCode: EndCode
    Data: byte array
}
