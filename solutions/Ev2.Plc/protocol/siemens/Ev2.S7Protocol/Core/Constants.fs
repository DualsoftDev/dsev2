namespace Ev2.S7Protocol.Core

[<RequireQualifiedAccess>]
module Constants =
    
    /// TPKT 프로토콜 상수
    module TPKT =
        let Version = 0x03uy
        let Reserved = 0x00uy
        let HeaderSize = 4
        let MinimumLength = 7  // TPKT(4) + COTP(3)
    
    /// COTP 프로토콜 상수
    module COTP =
        // PDU 타입
        let ConnectionRequest = 0xE0uy
        let ConnectionConfirm = 0xD0uy
        let DisconnectRequest = 0x80uy
        let DisconnectConfirm = 0xC0uy
        let DataTransfer = 0xF0uy
        let DataAcknowledge = 0x70uy
        let ExpeditedData = 0x10uy
        let RejectData = 0x50uy
        
        // 파라미터 코드
        let ParameterSrcTSAP = 0xC1uy
        let ParameterDstTSAP = 0xC2uy
        let ParameterTPDUSize = 0xC0uy
        
        // TPDU 크기
        let DefaultTPDUSize = 0x0Auy  // 1024 bytes
        
        // EOT (End of Transmission)
        let LastDataUnit = 0x80uy
        
        // 헤더 크기
        let DataHeaderSize = 3  // Length + PDU Type + EOT
    
    /// S7 프로토콜 상수
    module S7 =
        // 프로토콜 ID
        let ProtocolId = 0x32uy
        
        // PDU 타입
        let PDUTypeJob = 0x01uy
        let PDUTypeAck = 0x02uy
        let PDUTypeAckData = 0x03uy
        let PDUTypeUserData = 0x07uy
        
        // 에러 클래스
        let ErrorClassNoError = 0x00uy
        let ErrorClassApplicationRelationship = 0x81uy
        let ErrorClassObjectDefinition = 0x82uy
        let ErrorClassNoResourcesAvailable = 0x83uy
        let ErrorClassServiceProcessing = 0x84uy
        let ErrorClassSupplies = 0x85uy
        let ErrorClassAccessError = 0x87uy
        
        // 함수 코드
        let FunctionReadVar = 0x04uy
        let FunctionWriteVar = 0x05uy
        let FunctionSetupComm = 0xF0uy
        let FunctionReadSZL = 0x44uy
        
        // 서브함수 (SetupComm)
        let SubFunctionReadSZL = 0x01uy
        
        // Variable Specification
        let VariableSpecification = 0x12uy
        let VariableSpecLength = 0x0Auy
        
        // Syntax ID
        let SyntaxIdS7Any = 0x10uy      // Address data S7-Any
        let SyntaxIdDBRead = 0xA2uy     // DB read
        let SyntaxIdNCK = 0x82uy        // NCK address
        
        // Transport Size
        let TransportSizeBit = 0x01uy
        let TransportSizeByte = 0x02uy
        let TransportSizeChar = 0x03uy
        let TransportSizeWord = 0x04uy
        let TransportSizeInt = 0x05uy
        let TransportSizeDWord = 0x06uy
        let TransportSizeDInt = 0x07uy
        let TransportSizeReal = 0x08uy
        
        // 반환 코드
        let ReturnCodeSuccess = 0xFFuy
        let ReturnCodeHardwareFault = 0x01uy
        let ReturnCodeAccessDenied = 0x03uy
        let ReturnCodeInvalidAddress = 0x04uy
        let ReturnCodeDataError = 0x05uy
        let ReturnCodeDataTypeNotSupported = 0x06uy
        let ReturnCodeDataTypeInconsistent = 0x07uy
        let ReturnCodeObjectNotExist = 0x0Auy
        
        // Item 구조
        let ItemStructSize = 12  // S7 Any pointer size
        
        // 헤더 크기
        let JobHeaderSize = 10   // Protocol ID ~ Data Length
        let MinResponseSize = 12 // Minimum valid response
    
    /// 네트워크 상수
    module Network =
        let DefaultPort = 102
        let DefaultTimeout = 5000  // 5초
        
        /// PDU 크기 제한
        /// Note: Siemens recommends max 512 bytes for consistent data blocks
        let MaxPDUSize = 960       // 최대 PDU 크기
        let DefaultPDUSize = 480   // 기본 PDU 크기
        let MinPDUSize = 240       // 최소 PDU 크기
        let ConsistentDataLimit = 512  // 일관된 데이터 전송 제한
        
        // 버퍼 크기
        let ReceiveBufferSize = 8192
        let SendBufferSize = 8192
        
        // 프로토콜 오버헤드
        let TPKTOverhead = 4      // TPKT header
        let COTPOverhead = 3      // COTP data header
        let S7HeaderOverhead = 10 // S7 job header
        let ReadItemOverhead = 12 // Read variable spec
        let WriteItemOverhead = 12 // Write variable spec
        let WriteDataOverhead = 4  // Write data header
        
        // 총 오버헤드 계산
        let ReadRequestOverhead = 
            TPKTOverhead + COTPOverhead + S7HeaderOverhead + ReadItemOverhead
        
        let WriteRequestOverhead = 
            TPKTOverhead + COTPOverhead + S7HeaderOverhead + WriteItemOverhead + WriteDataOverhead
    
    /// 성능 관련 상수
    module Performance =
        let MaxItemsPerRequest = 20    // 한 요청당 최대 아이템 수
        let MaxBytesPerRequest = 512   // 한 요청당 최대 바이트 (Siemens 권장)
        let MaxRetries = 3
        let RetryDelay = 100           // 밀리초
        
        // 배치 처리
        let BatchReadChunkSize = 10    // 배치 읽기 청크 크기
        let BatchWriteChunkSize = 10   // 배치 쓰기 청크 크기
        
        // PDU 크기별 최대 데이터 계산 헬퍼
        let calculateMaxReadData pduSize =
            min (pduSize - Network.ReadRequestOverhead) MaxBytesPerRequest
        
        let calculateMaxWriteData pduSize =
            min (pduSize - Network.WriteRequestOverhead) MaxBytesPerRequest
    
    /// 디버그 설정
    module Debug =
        let EnablePacketLogging = false
        let LogPacketDetails = false
        let LogPerformanceMetrics = false
    
    /// TSAP 주소
    module TSAP =
        // 연결 타입
        let ConnectionTypePG = 0x01uy  // Programming Console
        let ConnectionTypeOP = 0x02uy  // Operator Panel
        let ConnectionTypeBasic = 0x03uy
        
        // 기본 TSAP 값
        let DefaultLocalTSAP = 0x0001  // 로컬 TSAP (S7NetPlus uses 0x0001)
        let DefaultRemoteTSAP = 0x0200  // 원격 TSAP for S7-300 (0x02 << 8)
        
        /// <summary>
        /// CPU별 원격 TSAP 계산 (S7NetPlus compatible)
        /// S7-300/400: ConnectionType(0x02) << 8 | (rack << 5 | slot)
        /// S7-1200/1500: ConnectionType(0x03) << 8 | rack
        /// </summary>
        let getRemoteTSAP (cpuType: CpuType) (rack: int) (slot: int) =
            match cpuType with
            | CpuType.S7200 -> 0x1000 ||| (rack * 0x100 + slot)
            | CpuType.S7300 | CpuType.S7400 -> 
                // S7-300/400: ConnectionType(0x02) << 8 | (rack << 5 | slot)
                0x0200 ||| ((rack <<< 5) ||| slot)
            | CpuType.S71200 | CpuType.S71500 -> 
                // S7-1200/1500: ConnectionType(0x03) << 8 | rack
                0x0300 ||| rack
            | _ -> DefaultRemoteTSAP
    
    /// 데이터 영역별 최대 크기
    module DataLimits =
        let MaxDBNumber = 65535
        let MaxDBSize = 65536
        let MaxMerkerSize = 8192
        let MaxInputSize = 8192
        let MaxOutputSize = 8192
        let MaxCounterNumber = 512
        let MaxTimerNumber = 512
        
        // 주소 범위 검증
        let isValidAddress (area: DataArea) (db: int) (startByte: int) (count: int) =
            match area with
            | DataArea.DataBlock when db < 0 || db > MaxDBNumber -> false
            | DataArea.DataBlock when startByte < 0 || startByte + count > MaxDBSize -> false
            | DataArea.Merker when startByte < 0 || startByte + count > MaxMerkerSize -> false
            | DataArea.ProcessInput when startByte < 0 || startByte + count > MaxInputSize -> false
            | DataArea.ProcessOutput when startByte < 0 || startByte + count > MaxOutputSize -> false
            | _ -> true