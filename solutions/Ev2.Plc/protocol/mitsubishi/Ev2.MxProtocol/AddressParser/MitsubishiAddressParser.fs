namespace Ev2.MxProtocol

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions

module AddressParserModule =
    
    /// Mitsubishi 디바이스 타입별 특성
    type DeviceInfo = {
        DeviceCode: string
        IsBitDevice: bool
        IsWordDevice: bool
        DefaultDataSize: int
        MaxAddress: int option
    }
    
    /// Mitsubishi 지원 디바이스 타입
    let supportedDevices = [
        // 비트 디바이스
        { DeviceCode = "X"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 7777 }    // 입력
        { DeviceCode = "Y"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 7777 }    // 출력  
        { DeviceCode = "M"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 8191 }    // 내부 릴레이
        { DeviceCode = "L"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 8191 }    // 래치 릴레이
        { DeviceCode = "F"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 2047 }    // 엣지 릴레이
        { DeviceCode = "V"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 2047 }    // 엣지 릴레이
        { DeviceCode = "B"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 7777 }    // 링크 릴레이
        { DeviceCode = "S"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 4095 }    // 스텝 릴레이
        
        // 워드 디바이스
        { DeviceCode = "D"; IsBitDevice = false; IsWordDevice = true; DefaultDataSize = 16; MaxAddress = Some 12287 }  // 데이터 레지스터
        { DeviceCode = "W"; IsBitDevice = false; IsWordDevice = true; DefaultDataSize = 16; MaxAddress = Some 7777 }   // 링크 레지스터
        { DeviceCode = "R"; IsBitDevice = false; IsWordDevice = true; DefaultDataSize = 16; MaxAddress = Some 32767 }  // 파일 레지스터
        { DeviceCode = "Z"; IsBitDevice = false; IsWordDevice = true; DefaultDataSize = 16; MaxAddress = Some 19 }     // 인덱스 레지스터
        
        // 특수 디바이스 (비트와 워드 모두 지원)
        { DeviceCode = "SM"; IsBitDevice = true; IsWordDevice = false; DefaultDataSize = 1; MaxAddress = Some 2047 }   // 특수 릴레이
        { DeviceCode = "SD"; IsBitDevice = false; IsWordDevice = true; DefaultDataSize = 16; MaxAddress = Some 2047 }  // 특수 레지스터
        { DeviceCode = "T"; IsBitDevice = true; IsWordDevice = true; DefaultDataSize = 16; MaxAddress = Some 1023 }    // 타이머
        { DeviceCode = "C"; IsBitDevice = true; IsWordDevice = true; DefaultDataSize = 16; MaxAddress = Some 1023 }    // 카운터
    ]
    
    let private getDeviceInfo (deviceCode: string) : DeviceInfo option =
        supportedDevices |> List.tryFind (fun d -> d.DeviceCode.Equals(deviceCode, StringComparison.OrdinalIgnoreCase))
    
    let tryParseMitsubishiAddress (addressText: string) : PlcAddress option =
        if String.IsNullOrWhiteSpace(addressText) then None
        else
            let trimmed = addressText.Trim().ToUpperInvariant()
            
            // Mitsubishi 주소 패턴: 디바이스코드 + 숫자 + (옵션: .비트)
            // 예: D100, M0, X0, Y10, D100.5, T0, C10
            let pattern = @"^([A-Z]{1,2})(\d+)(?:\.(\d+))?$"
            let m = Regex.Match(trimmed, pattern, RegexOptions.IgnoreCase)
            
            if m.Success then
                let deviceCode = m.Groups.[1].Value.ToUpperInvariant()
                let addressNum = Int32.Parse(m.Groups.[2].Value)
                let bitPosition = 
                    if m.Groups.[3].Success then 
                        Some (Int32.Parse(m.Groups.[3].Value))
                    else None
                
                match getDeviceInfo deviceCode with
                | Some deviceInfo ->
                    // 주소 범위 검증
                    let isValidAddress = 
                        match deviceInfo.MaxAddress with
                        | Some maxAddr -> addressNum <= maxAddr
                        | None -> true
                    
                    if isValidAddress then
                        let dataSize = 
                            match bitPosition with
                            | Some _ -> 1  // 비트 액세스
                            | None -> deviceInfo.DefaultDataSize
                        
                        let totalBitOffset = 
                            match bitPosition with
                            | Some bit -> addressNum * deviceInfo.DefaultDataSize + bit
                            | None -> addressNum * deviceInfo.DefaultDataSize
                        
                        let address = {
                            DeviceType = deviceCode
                            Address = addressNum
                            BitPosition = bitPosition
                            DataSize = dataSize
                            TotalBitOffset = totalBitOffset
                        }
                        Some address
                    else None
                | None -> None
            else None
    
    let formatMitsubishiAddress (address: PlcAddress) : string =
        match address.BitPosition with
        | Some bit -> $"{address.DeviceType}{address.Address}.{bit}"
        | None -> $"{address.DeviceType}{address.Address}"
    
    let validateMitsubishiAddress (addressText: string) : bool =
        tryParseMitsubishiAddress addressText |> Option.isSome
    
    let inferDataType (address: PlcAddress) : PlcDataType =
        match address.DataSize with
        | 1 -> PlcDataType.Bool
        | 8 -> PlcDataType.UInt8
        | 16 -> PlcDataType.UInt16
        | 32 -> PlcDataType.UInt32
        | 64 -> PlcDataType.UInt64
        | _ -> PlcDataType.UInt16 // Mitsubishi 기본값
    
    /// 디바이스가 비트 디바이스인지 확인
    let isBitDevice (deviceCode: string) : bool =
        match getDeviceInfo deviceCode with
        | Some info -> info.IsBitDevice
        | None -> false
    
    /// 디바이스가 워드 디바이스인지 확인
    let isWordDevice (deviceCode: string) : bool =
        match getDeviceInfo deviceCode with
        | Some info -> info.IsWordDevice
        | None -> false

[<Extension>]
type MitsubishiAddressParser() =
    
    interface IAddressParser with
        member _.TryParseAddress(addressText: string) =
            match AddressParserModule.tryParseMitsubishiAddress addressText with
            | Some address ->
                Some {
                    Address = address
                    OriginalText = addressText
                    NormalizedText = addressText.Trim().ToUpperInvariant()
                }
            | None -> None
        
        member _.FormatAddress(address: PlcAddress) =
            AddressParserModule.formatMitsubishiAddress address
        
        member _.ValidateAddress(addressText: string) =
            AddressParserModule.validateMitsubishiAddress addressText
        
        member _.InferDataType(address: PlcAddress) =
            AddressParserModule.inferDataType address
        
        member _.SupportedDeviceTypes = 
            AddressParserModule.supportedDevices 
            |> List.map (fun d -> d.DeviceCode)

    /// Mitsubishi 디바이스가 비트 디바이스인지 확인
    [<Extension>]
    static member IsBitDevice(deviceCode: string) : bool =
        AddressParserModule.isBitDevice deviceCode
    
    /// Mitsubishi 디바이스가 워드 디바이스인지 확인
    [<Extension>]
    static member IsWordDevice(deviceCode: string) : bool =
        AddressParserModule.isWordDevice deviceCode
    
    /// Mitsubishi 주소가 비트 액세스인지 확인
    [<Extension>]
    static member IsBitAccess(addressText: string) : bool =
        not (String.IsNullOrWhiteSpace(addressText)) &&
        addressText.Contains(".")
    
    /// Mitsubishi 디바이스 정보 조회
    [<Extension>]
    static member GetDeviceInfo(deviceCode: string) =
        AddressParserModule.supportedDevices 
        |> List.tryFind (fun d -> d.DeviceCode.Equals(deviceCode, StringComparison.OrdinalIgnoreCase))