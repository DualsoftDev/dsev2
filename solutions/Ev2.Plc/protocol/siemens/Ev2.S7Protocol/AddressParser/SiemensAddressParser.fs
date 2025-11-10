namespace Ev2.S7Protocol

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions

module AddressParserModule =
    
    /// Siemens S7 디바이스 타입별 특성
    type S7DeviceInfo = {
        DeviceCode: string
        IsBitAddressing: bool
        IsWordAddressing: bool
        DefaultDataSize: int
        MinAddress: int
        MaxAddress: int option
        Description: string
    }
    
    /// Siemens S7 지원 디바이스 타입
    let supportedDevices = [
        // 입출력 이미지
        { DeviceCode = "I"; IsBitAddressing = true; IsWordAddressing = true; DefaultDataSize = 1; MinAddress = 0; MaxAddress = Some 65535; Description = "Input Image" }
        { DeviceCode = "Q"; IsBitAddressing = true; IsWordAddressing = true; DefaultDataSize = 1; MinAddress = 0; MaxAddress = Some 65535; Description = "Output Image" }
        
        // 메모리 영역
        { DeviceCode = "M"; IsBitAddressing = true; IsWordAddressing = true; DefaultDataSize = 1; MinAddress = 0; MaxAddress = Some 65535; Description = "Memory Bit" }
        
        // 데이터 블록
        { DeviceCode = "DB"; IsBitAddressing = true; IsWordAddressing = true; DefaultDataSize = 16; MinAddress = 1; MaxAddress = Some 65535; Description = "Data Block" }
        
        // 타이머와 카운터
        { DeviceCode = "T"; IsBitAddressing = false; IsWordAddressing = true; DefaultDataSize = 16; MinAddress = 0; MaxAddress = Some 65535; Description = "Timer" }
        { DeviceCode = "C"; IsBitAddressing = false; IsWordAddressing = true; DefaultDataSize = 16; MinAddress = 0; MaxAddress = Some 65535; Description = "Counter" }
        
        // 로컬 데이터
        { DeviceCode = "L"; IsBitAddressing = true; IsWordAddressing = true; DefaultDataSize = 1; MinAddress = 0; MaxAddress = Some 65535; Description = "Local Data" }
        
        // 주변장치 영역
        { DeviceCode = "P"; IsBitAddressing = true; IsWordAddressing = true; DefaultDataSize = 1; MinAddress = 0; MaxAddress = Some 65535; Description = "Peripheral" }
    ]
    
    let private getS7DeviceInfo (deviceCode: string) : S7DeviceInfo option =
        supportedDevices |> List.tryFind (fun d -> d.DeviceCode.Equals(deviceCode, StringComparison.OrdinalIgnoreCase))
    
    let tryParseS7Address (addressText: string) : PlcAddress option =
        if String.IsNullOrWhiteSpace(addressText) then None
        else
            let trimmed = addressText.Trim().ToUpperInvariant()
            
            // S7 주소 패턴들
            // 1. 비트 주소: I0.0, Q1.7, M10.3, DB1.DBX0.0
            // 2. 바이트 주소: IB0, QB1, MB10, DB1.DBB0
            // 3. 워드 주소: IW0, QW2, MW10, DB1.DBW0
            // 4. 더블워드 주소: ID0, QD4, MD10, DB1.DBD0
            // 5. 타이머/카운터: T0, C1
            
            let patterns = [
                // DB 블록 주소: DB1.DBX0.0, DB1.DBB0, DB1.DBW0, DB1.DBD0
                @"^DB(\d+)\.DB([XBWD])(\d+)(?:\.(\d+))?$"
                
                // 일반 비트 주소: I0.0, Q1.7, M10.3, L0.0, P0.0  
                @"^([IQMLP])(\d+)\.(\d+)$"
                
                // 일반 바이트/워드/더블워드: IB0, QW2, MD10, LW0, PD0
                @"^([IQMLP])([BWD])(\d+)$"
                
                // 타이머/카운터: T0, C1
                @"^([TC])(\d+)$"
            ]
            
            // DB 블록 주소 파싱
            let dbMatch = Regex.Match(trimmed, patterns.[0], RegexOptions.IgnoreCase)
            if dbMatch.Success then
                let dbNumber = Int32.Parse(dbMatch.Groups.[1].Value)
                let dataType = dbMatch.Groups.[2].Value
                let address = Int32.Parse(dbMatch.Groups.[3].Value)
                let bitPosition = 
                    if dbMatch.Groups.[4].Success then 
                        Some (Int32.Parse(dbMatch.Groups.[4].Value))
                    else None
                
                let dataSize = 
                    match dataType, bitPosition with
                    | "X", Some _ -> 1      // 비트 액세스
                    | "X", None -> 1        // 비트
                    | "B", _ -> 8           // 바이트
                    | "W", _ -> 16          // 워드
                    | "D", _ -> 32          // 더블워드
                    | _ -> 1
                
                let totalBitOffset = 
                    match dataType, bitPosition with
                    | "X", Some bit -> address * 8 + bit
                    | "B", _ -> address * 8
                    | "W", _ -> address * 16
                    | "D", _ -> address * 32
                    | _ -> address
                
                let plcAddress = {
                    DeviceType = $"DB{dbNumber}"
                    Address = address
                    BitPosition = bitPosition
                    DataSize = dataSize
                    TotalBitOffset = totalBitOffset
                }
                Some plcAddress
            else
                // 일반 비트 주소 파싱 (I0.0, Q1.7 등)
                let bitMatch = Regex.Match(trimmed, patterns.[1], RegexOptions.IgnoreCase)
                if bitMatch.Success then
                    let deviceCode = bitMatch.Groups.[1].Value
                    let byteAddr = Int32.Parse(bitMatch.Groups.[2].Value)
                    let bitPos = Int32.Parse(bitMatch.Groups.[3].Value)
                    
                    if bitPos >= 0 && bitPos <= 7 then
                        match getS7DeviceInfo deviceCode with
                        | Some deviceInfo when deviceInfo.IsBitAddressing ->
                            let plcAddress = {
                                DeviceType = deviceCode
                                Address = byteAddr
                                BitPosition = Some bitPos
                                DataSize = 1
                                TotalBitOffset = byteAddr * 8 + bitPos
                            }
                            Some plcAddress
                        | _ -> None
                    else None
                else
                    // 바이트/워드/더블워드 주소 파싱 (IB0, QW2 등)
                    let wordMatch = Regex.Match(trimmed, patterns.[2], RegexOptions.IgnoreCase)
                    if wordMatch.Success then
                        let deviceCode = bitMatch.Groups.[1].Value
                        let dataType = wordMatch.Groups.[2].Value
                        let address = Int32.Parse(wordMatch.Groups.[3].Value)
                        
                        let dataSize = 
                            match dataType with
                            | "B" -> 8     // 바이트
                            | "W" -> 16    // 워드
                            | "D" -> 32    // 더블워드
                            | _ -> 8
                        
                        match getS7DeviceInfo deviceCode with
                        | Some deviceInfo when deviceInfo.IsWordAddressing ->
                            let plcAddress = {
                                DeviceType = deviceCode
                                Address = address
                                BitPosition = None
                                DataSize = dataSize
                                TotalBitOffset = address * dataSize
                            }
                            Some plcAddress
                        | _ -> None
                    else
                        // 타이머/카운터 주소 파싱 (T0, C1 등)
                        let tcMatch = Regex.Match(trimmed, patterns.[3], RegexOptions.IgnoreCase)
                        if tcMatch.Success then
                            let deviceCode = tcMatch.Groups.[1].Value
                            let address = Int32.Parse(tcMatch.Groups.[2].Value)
                            
                            match getS7DeviceInfo deviceCode with
                            | Some deviceInfo ->
                                let plcAddress = {
                                    DeviceType = deviceCode
                                    Address = address
                                    BitPosition = None
                                    DataSize = deviceInfo.DefaultDataSize
                                    TotalBitOffset = address * deviceInfo.DefaultDataSize
                                }
                                Some plcAddress
                            | _ -> None
                        else None
    
    let formatS7Address (address: PlcAddress) : string =
        match address.DeviceType with
        | dt when dt.StartsWith("DB") ->
            let dbNum = dt.Substring(2)
            match address.DataSize, address.BitPosition with
            | 1, Some bit -> $"DB{dbNum}.DBX{address.Address}.{bit}"
            | 1, None -> $"DB{dbNum}.DBX{address.Address}"
            | 8, _ -> $"DB{dbNum}.DBB{address.Address}"
            | 16, _ -> $"DB{dbNum}.DBW{address.Address}"
            | 32, _ -> $"DB{dbNum}.DBD{address.Address}"
            | _ -> $"DB{dbNum}.DBX{address.Address}"
        | _ ->
            match address.DataSize, address.BitPosition with
            | 1, Some bit -> $"{address.DeviceType}{address.Address}.{bit}"
            | 8, _ -> $"{address.DeviceType}B{address.Address}"
            | 16, _ -> $"{address.DeviceType}W{address.Address}"
            | 32, _ -> $"{address.DeviceType}D{address.Address}"
            | _ -> $"{address.DeviceType}{address.Address}"
    
    let validateS7Address (addressText: string) : bool =
        tryParseS7Address addressText |> Option.isSome
    
    let inferDataType (address: PlcAddress) : PlcDataType =
        match address.DataSize with
        | 1 -> PlcDataType.Bool
        | 8 -> PlcDataType.UInt8
        | 16 -> PlcDataType.UInt16
        | 32 -> PlcDataType.UInt32
        | 64 -> PlcDataType.UInt64
        | _ -> PlcDataType.Bool // S7 기본값

[<Extension>]
type SiemensAddressParser() =
    
    interface IAddressParser with
        member _.TryParseAddress(addressText: string) =
            match AddressParserModule.tryParseS7Address addressText with
            | Some address ->
                Some {
                    Address = address
                    OriginalText = addressText
                    NormalizedText = addressText.Trim().ToUpperInvariant()
                }
            | None -> None
        
        member _.FormatAddress(address: PlcAddress) =
            AddressParserModule.formatS7Address address
        
        member _.ValidateAddress(addressText: string) =
            AddressParserModule.validateS7Address addressText
        
        member _.InferDataType(address: PlcAddress) =
            AddressParserModule.inferDataType address
        
        member _.SupportedDeviceTypes = 
            AddressParserModule.supportedDevices 
            |> List.map (fun d -> d.DeviceCode)

    /// S7 주소가 비트 액세스인지 확인
    [<Extension>]
    static member IsBitAccess(addressText: string) : bool =
        not (String.IsNullOrWhiteSpace(addressText)) &&
        (addressText.Contains(".") || 
         Regex.IsMatch(addressText.ToUpperInvariant(), @"[IQMLP]\d+\.\d+$") ||
         Regex.IsMatch(addressText.ToUpperInvariant(), @"DB\d+\.DBX\d+\.\d+$"))
    
    /// S7 주소가 DB 블록 주소인지 확인
    [<Extension>]
    static member IsDataBlockAddress(addressText: string) : bool =
        not (String.IsNullOrWhiteSpace(addressText)) &&
        addressText.ToUpperInvariant().StartsWith("DB")
    
    /// S7 주소가 타이머/카운터인지 확인
    [<Extension>]
    static member IsTimerOrCounter(addressText: string) : bool =
        not (String.IsNullOrWhiteSpace(addressText)) &&
        Regex.IsMatch(addressText.ToUpperInvariant(), @"^[TC]\d+$")
    
    /// S7 디바이스 정보 조회
    [<Extension>]
    static member GetDeviceInfo(deviceCode: string) =
        AddressParserModule.supportedDevices 
        |> List.tryFind (fun d -> d.DeviceCode.Equals(deviceCode, StringComparison.OrdinalIgnoreCase))