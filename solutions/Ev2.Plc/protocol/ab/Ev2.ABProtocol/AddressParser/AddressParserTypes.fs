namespace Ev2.AbProtocol

open System

/// PLC 주소 정보를 나타내는 기본 타입
type PlcAddress = {
    /// 디바이스 타입 (예: D, M, X, Y 등)
    DeviceType: string
    /// 주소 번호
    Address: int
    /// 비트 위치 (비트 단위 액세스인 경우)
    BitPosition: int option
    /// 데이터 크기 (비트 단위)
    DataSize: int
    /// 전체 비트 오프셋
    TotalBitOffset: int
}

/// PLC 데이터 타입
type PlcDataType =
    | Bool
    | Int8  | UInt8
    | Int16 | UInt16  
    | Int32 | UInt32
    | Int64 | UInt64
    | Float32 | Float64
    | String of maxLength: int
    | Bytes of maxLength: int
    | Array of elementType: PlcDataType * length: int
    | Struct of fields: (string * PlcDataType) list

    member this.Size =
        match this with
        | Bool -> 1
        | Int8 | UInt8 -> 1
        | Int16 | UInt16 -> 2
        | Int32 | UInt32 | Float32 -> 4
        | Int64 | UInt64 | Float64 -> 8
        | String maxLength -> maxLength
        | Bytes maxLength -> maxLength
        | Array (elementType, length) -> elementType.Size * length
        | Struct fields -> fields |> List.sumBy (fun (_, dataType) -> dataType.Size)

/// 주소 파싱 결과
type AddressParseResult = {
    /// 파싱된 주소 정보
    Address: PlcAddress
    /// 원래 주소 문자열
    OriginalText: string
    /// 정규화된 주소 문자열
    NormalizedText: string
}

/// 주소 파서 인터페이스
type IAddressParser =
    /// 주소 문자열을 파싱하여 주소 정보를 반환
    abstract member TryParseAddress: addressText: string -> AddressParseResult option
    
    /// 주소 정보로부터 주소 문자열을 생성
    abstract member FormatAddress: address: PlcAddress -> string
    
    /// 주소 문자열이 유효한지 검증
    abstract member ValidateAddress: addressText: string -> bool
    
    /// 데이터 타입을 추론
    abstract member InferDataType: address: PlcAddress -> PlcDataType
    
    /// 지원하는 디바이스 타입 목록
    abstract member SupportedDeviceTypes: string list