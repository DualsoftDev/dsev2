namespace Ev2.AbProtocol.Core

/// 프로토콜 상수 정의
[<RequireQualifiedAccess>]
module Constants =
    
    /// EtherNet/IP 상수
    [<RequireQualifiedAccess>]
    module EIP =
        // 기본 설정
        let DefaultPort = 44818
        let Version = 0x0001us
        let DefaultTimeout = 2000 // ms
        let HeaderSize = 24 // bytes
        
        // EIP 명령 코드
        let RegisterSession = 0x0065us
        let UnregisterSession = 0x0066us
        let ListServices = 0x0004us
        let ListIdentity = 0x0063us
        let ListInterfaces = 0x0064us
        let UnconnectedSend = 0x006Fus
        let ConnectedSend = 0x0070us
        
        // CPF (Common Packet Format) 아이템 타입
        let ItemNull = 0x0000us
        let ItemListIdentity = 0x000Cus
        let ItemConnectionAddress = 0x00A1us
        let ItemConnectedData = 0x00B1us
        let ItemUnconnectedData = 0x00B2us
        let ItemListServices = 0x0100us
        let ItemSockaddrO2T = 0x8000us
        let ItemSockaddrT2O = 0x8001us
        let ItemSequencedAddress = 0x8002us
        
        // EIP 상태 코드
        let StatusSuccess = 0x00u
        let StatusInvalidCommand = 0x01u
        let StatusMemoryError = 0x02u
        let StatusIncorrectData = 0x03u
        let StatusInvalidSession = 0x64u
        let StatusInvalidLength = 0x65u
        let StatusUnsupportedProtocol = 0x69u
        
        /// EIP 상태 코드를 메시지로 변환
        let statusToMessage = function
            | s when s = StatusSuccess -> "Success"
            | s when s = StatusInvalidCommand -> "Invalid command"
            | s when s = StatusMemoryError -> "Memory error"
            | s when s = StatusIncorrectData -> "Incorrect data"
            | s when s = StatusInvalidSession -> "Invalid session"
            | s when s = StatusInvalidLength -> "Invalid length"
            | s when s = StatusUnsupportedProtocol -> "Unsupported protocol"
            | s -> sprintf "EIP error 0x%08X" s
    
    /// CIP (Common Industrial Protocol) 상수
    [<RequireQualifiedAccess>]
    module CIP =
        // CIP 서비스 코드
        let GetAttributesAll = 0x01uy
        let SetAttributesAll = 0x02uy
        let GetAttributeList = 0x03uy
        let SetAttributeList = 0x04uy
        let Reset = 0x05uy
        let Start = 0x06uy
        let Stop = 0x07uy
        let Create = 0x08uy
        let Delete = 0x09uy
        let MultipleServicePacket = 0x0Auy
        let ApplyAttributes = 0x0Duy
        let GetAttributeSingle = 0x0Euy
        let SetAttributeSingle = 0x10uy
        let FindNextObject = 0x11uy
        
        // 태그 관련 서비스
        let ReadTag = 0x4Cuy
        let WriteTag = 0x4Duy
        let ReadModifyWrite = 0x4Euy
        let ReadTagFragmented = 0x52uy
        let WriteTagFragmented = 0x53uy
        let GetInstanceAttributeList = 0x55uy
        let LargeForwardOpen = 0x5Buy
        
        // 응답 플래그
        let ReplyMask = 0x80uy
        
        /// 서비스 코드가 응답인지 확인
        let isReply (service: byte) = (service &&& ReplyMask) <> 0uy
        
        /// 요청 서비스 코드를 응답 코드로 변환
        let toReply (service: byte) = service ||| ReplyMask
        
        /// 응답 서비스 코드를 요청 코드로 변환
        let toRequest (service: byte) = service &&& ~~~ReplyMask
        
        // CIP 상태 코드
        let StatusSuccess = 0x00uy
        let StatusConnectionFailure = 0x01uy
        let StatusResourceUnavailable = 0x02uy
        let StatusInvalidParameterValue = 0x03uy
        let StatusPathSegmentError = 0x04uy
        let StatusPathDestinationUnknown = 0x05uy
        let StatusPartialTransfer = 0x06uy
        let StatusConnectionLost = 0x07uy
        let StatusServiceNotSupported = 0x08uy
        let StatusInvalidAttributeValue = 0x09uy
        let StatusAttributeListError = 0x0Auy
        let StatusAlreadyInRequestedState = 0x0Buy
        let StatusObjectStateConflict = 0x0Cuy
        let StatusObjectAlreadyExists = 0x0Duy
        let StatusAttributeNotSettable = 0x0Euy
        let StatusPrivilegeViolation = 0x0Fuy
        let StatusDeviceStateConflict = 0x10uy
        let StatusReplyDataTooLarge = 0x11uy
        let StatusFragmentationInProgress = 0x12uy
        let StatusNotEnoughData = 0x13uy
        let StatusAttributeNotSupported = 0x14uy
        let StatusTooMuchData = 0x15uy
        let StatusObjectDoesNotExist = 0x16uy
        let StatusFragmentationSequenceError = 0x17uy
        let StatusNoStoredAttributeData = 0x18uy
        let StatusStoreOperationFailure = 0x19uy
        let StatusRoutingFailure = 0x1Auy
        let StatusInvalidReplyReceived = 0x1Buy
        let StatusBufferOverflow = 0x1Cuy
        let StatusInvalidMessageFormat = 0x1Duy
        let StatusPartialError = 0x1Euy
        let StatusConnectionRelatedFailure = 0x1Fuy
        let StatusInvalidParameterLength = 0x20uy
        let StatusInvalidMessage = 0x21uy
        let StatusMemberNotFound = 0x22uy
        let StatusMemberNotSettable = 0x23uy
        let StatusGroupTwoServerGeneralFailure = 0x24uy
        let StatusUnknownCIPError = 0x25uy
        let StatusAttributeNotGettable = 0x26uy
        let StatusInstanceNotDeletable = 0x27uy
        let StatusServiceNotSupported2 = 0x28uy
        
        /// CIP 상태 코드를 메시지로 변환
        let statusToMessage = function
            | s when s = StatusSuccess -> "Success"
            | s when s = StatusConnectionFailure -> "Connection failure"
            | s when s = StatusResourceUnavailable -> "Resource unavailable"
            | s when s = StatusInvalidParameterValue -> "Invalid parameter"
            | s when s = StatusPathSegmentError -> "Path segment error"
            | s when s = StatusPathDestinationUnknown -> "Path destination unknown"
            | s when s = StatusPartialTransfer -> "Partial transfer"
            | s when s = StatusConnectionLost -> "Connection lost"
            | s when s = StatusServiceNotSupported -> "Service not supported"
            | s when s = StatusInvalidAttributeValue -> "Invalid attribute"
            | s when s = StatusAttributeListError -> "Attribute list error"
            | s when s = StatusAlreadyInRequestedState -> "Already in state"
            | s when s = StatusObjectStateConflict -> "State conflict"
            | s when s = StatusObjectAlreadyExists -> "Object exists"
            | s when s = StatusAttributeNotSettable -> "Not settable"
            | s when s = StatusPrivilegeViolation -> "Privilege violation"
            | s when s = StatusDeviceStateConflict -> "Device state conflict"
            | s when s = StatusReplyDataTooLarge -> "Data too large"
            | s when s = StatusFragmentationInProgress -> "Fragmentation"
            | s when s = StatusNotEnoughData -> "Not enough data"
            | s when s = StatusAttributeNotSupported -> "Attribute not supported"
            | s when s = StatusTooMuchData -> "Too much data"
            | s when s = StatusObjectDoesNotExist -> "Object not found"
            | s when s = StatusMemberNotFound -> "Member not found"
            | s -> sprintf "CIP error 0x%02X" s
    
    
    /// 성능 관련 상수
    [<RequireQualifiedAccess>]
    module Performance =
        // 패킷 크기 제한
        let MaxServicesPerMultiPacket = 20
        let MaxUnconnectedPacketSize = 504  // CIP Unconnected 실제 제한
        let MaxConnectedPacketSize = 4000   // Connected 경우
        let MaxElementsPerRead = 120        // DINT 기준 (120 * 4 = 480 bytes)
        // 타이밍 설정
        let DefaultStreamingInterval = 50  // ms
        let DefaultBufferSize = 1000
        let ConnectionTimeout = 5000  // ms
        let ReceiveTimeout = 2000  // ms
        let SendTimeout = 2000  // ms
        let KeepAliveInterval = 30000  // ms
        
        // 재시도 설정
        let MaxRetries = 3
        let RetryDelay = 100  // ms
        let MaxConcurrentRequests = 10
        
        /// 데이터 타입별 최대 요소 수 계산
        let maxElementsForDataType (bytesPerElement: int) =
            if bytesPerElement <= 0 then 1
            else MaxUnconnectedPacketSize / bytesPerElement
    
    /// 경로 세그먼트 타입
    [<RequireQualifiedAccess>]
    module PathSegment =
        // 세그먼트 타입
        let Port = 0x00uy
        let LogicalClass = 0x20uy
        let LogicalInstance = 0x24uy
        let LogicalAttribute = 0x30uy
        let LogicalConnection = 0x2Cuy
        let LogicalSpecial = 0x34uy
        let Electronic = 0x40uy
        let Network = 0x43uy
        let Symbolic = 0x91uy
        let Data = 0x80uy
        let DataType = 0x90uy
        let Reserved = 0xE0uy
        
        // 확장 세그먼트
        let ExtendedLogical16Bit = 0x25uy
        let ExtendedLogical32Bit = 0x26uy
        
        /// 세그먼트가 논리 세그먼트인지 확인
        let isLogical (segment: byte) = 
            (segment &&& 0xE0uy) = 0x20uy
        
        /// 세그먼트가 심볼릭 세그먼트인지 확인
        let isSymbolic (segment: byte) = 
            segment = Symbolic
    
    /// 데이터 타입 코드 (CIP 타입 코드)
    [<RequireQualifiedAccess>]
    module DataTypeCodes =
        let Bool = 0x00C1us
        let Sint = 0x00C2us
        let Int = 0x00C3us
        let Dint = 0x00C4us
        let Lint = 0x00C5us
        let Usint = 0x00C6us
        let Uint = 0x00C7us
        let Udint = 0x00C8us
        let Ulint = 0x00C9us
        let Real = 0x00CAus
        let Lreal = 0x00CBus
        let String = 0x00D0us
        let String2 = 0x00FCEus
        
        // 비트 스트링 타입
        let BitString8 = 0x00D1us
        let BitString16 = 0x00D2us
        let BitString32 = 0x00D3us
        let BitString64 = 0x00D4us
        
        // 특수 타입
        let Timer = 0x0F83us
        let Struct = 0x0002us
        
        /// 타입 코드에서 베이스 타입 추출 (하위 12비트)
        let getBaseType (typeCode: uint16) = typeCode &&& 0x0FFFus
        
        /// 배열 플래그 확인 (비트 13)
        let isArray (typeCode: uint16) = (typeCode &&& 0x2000us) <> 0us
        
        /// 구조체 플래그 확인 (비트 15)
        let isStruct (typeCode: uint16) = (typeCode &&& 0x8000us) <> 0us
    
    /// 클래스 ID
    [<RequireQualifiedAccess>]
    module ClassId =
        let Identity = 0x01uy
        let MessageRouter = 0x02uy
        let DeviceNet = 0x03uy
        let Assembly = 0x04uy
        let Connection = 0x05uy
        let ConnectionManager = 0x06uy
        let Register = 0x07uy
        let SymbolObject = 0x6Buy
        let Template = 0x6Cuy
        
    /// 속성 ID
    [<RequireQualifiedAccess>]
    module AttributeId =
        // 공통 속성
        let Revision = 0x0001us
        let MaxInstance = 0x0002us
        let NumInstances = 0x0003us
        
        // Symbol 클래스 속성
        let SymbolName = 0x0001us
        let SymbolType = 0x0002us
        let SymbolAddress = 0x0007us
        let SymbolDimensions = 0x0008us