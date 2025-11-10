namespace Ev2.AbProtocol

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions

module AddressParserModule =
    
    /// Allen-Bradley 지원 데이터 타입
    let supportedDataTypes = [
        "BOOL"; "SINT"; "INT"; "DINT"; "LINT"; "USINT"; "UINT"; "UDINT"; "ULINT"
        "REAL"; "LREAL"; "STRING"; "BYTE"; "WORD"; "DWORD"; "LWORD"
    ]
    
    /// Allen-Bradley 태그 주소 정규식 패턴
    let private tagPatterns = [
        // 로컬 태그: Program:MyProgram.LocalTag
        @"^Program:(\w+)\.(\w+)(?:\[(\d+)\])?(?:\.(\d+))?$"
        
        // 글로벌 태그: GlobalTag[0].Member  
        @"^(\w+)(?:\[(\d+)\])?(?:\.(\w+))?(?:\.(\d+))?$"
        
        // I/O 모듈 태그: Local:1:I.Data[0] 
        @"^Local:(\d+):(I|O)\.Data(?:\[(\d+)\])?(?:\.(\d+))?$"
        
        // 내부 시스템 태그: _System.Tag
        @"^_(\w+)\.(\w+)(?:\[(\d+)\])?(?:\.(\d+))?$"
    ]
    
    let tryParseTagAddress (addressText: string) : PlcAddress option =
        if String.IsNullOrWhiteSpace(addressText) then None
        else
            let trimmed = addressText.Trim()
            
            // 로컬 프로그램 태그 파싱
            let localMatch = Regex.Match(trimmed, tagPatterns.[0], RegexOptions.IgnoreCase)
            if localMatch.Success then
                let programName = localMatch.Groups.[1].Value
                let tagName = localMatch.Groups.[2].Value
                let arrayIndex = 
                    if localMatch.Groups.[3].Success then 
                        Some (Int32.Parse(localMatch.Groups.[3].Value))
                    else None
                let bitPosition = 
                    if localMatch.Groups.[4].Success then 
                        Some (Int32.Parse(localMatch.Groups.[4].Value))
                    else None
                
                let address = {
                    DeviceType = $"Program:{programName}"
                    Address = 0 // AB는 심볼릭 주소 사용
                    BitPosition = bitPosition
                    DataSize = if bitPosition.IsSome then 1 else 32
                    TotalBitOffset = 0
                }
                Some address
            else
                // 글로벌 태그 파싱
                let globalMatch = Regex.Match(trimmed, tagPatterns.[1], RegexOptions.IgnoreCase)
                if globalMatch.Success then
                    let tagName = globalMatch.Groups.[1].Value
                    let arrayIndex = 
                        if globalMatch.Groups.[2].Success then 
                            Some (Int32.Parse(globalMatch.Groups.[2].Value))
                        else None
                    let memberName = 
                        if globalMatch.Groups.[3].Success then 
                            Some globalMatch.Groups.[3].Value
                        else None
                    let bitPosition = 
                        if globalMatch.Groups.[4].Success then 
                            Some (Int32.Parse(globalMatch.Groups.[4].Value))
                        else None
                    
                    let address = {
                        DeviceType = "Global"
                        Address = 0 // AB는 심볼릭 주소 사용
                        BitPosition = bitPosition
                        DataSize = if bitPosition.IsSome then 1 else 32
                        TotalBitOffset = 0
                    }
                    Some address
                else
                    // I/O 모듈 태그 파싱
                    let ioMatch = Regex.Match(trimmed, tagPatterns.[2], RegexOptions.IgnoreCase)
                    if ioMatch.Success then
                        let slot = Int32.Parse(ioMatch.Groups.[1].Value)
                        let ioType = ioMatch.Groups.[2].Value
                        let dataIndex = 
                            if ioMatch.Groups.[3].Success then 
                                Int32.Parse(ioMatch.Groups.[3].Value)
                            else 0
                        let bitPosition = 
                            if ioMatch.Groups.[4].Success then 
                                Some (Int32.Parse(ioMatch.Groups.[4].Value))
                            else None
                        
                        let address = {
                            DeviceType = $"Local:{slot}:{ioType}"
                            Address = dataIndex
                            BitPosition = bitPosition
                            DataSize = if bitPosition.IsSome then 1 else 32
                            TotalBitOffset = dataIndex * 32 + (bitPosition |> Option.defaultValue 0)
                        }
                        Some address
                    else None
    
    let formatTagAddress (address: PlcAddress) : string =
        match address.DeviceType with
        | dt when dt.StartsWith("Program:") ->
            let programName = dt.Substring(8)
            match address.BitPosition with
            | Some bit -> $"Program:{programName}.Tag.{bit}"
            | None -> $"Program:{programName}.Tag"
            
        | "Global" ->
            match address.BitPosition with
            | Some bit -> $"GlobalTag.{bit}"
            | None -> "GlobalTag"
            
        | dt when dt.StartsWith("Local:") && dt.Contains(":") ->
            let parts = dt.Split(':')
            let slot = parts.[1]
            let ioType = parts.[2]
            match address.BitPosition with
            | Some bit -> $"Local:{slot}:{ioType}.Data[{address.Address}].{bit}"
            | None -> $"Local:{slot}:{ioType}.Data[{address.Address}]"
            
        | _ -> address.DeviceType
    
    let validateTagAddress (addressText: string) : bool =
        tryParseTagAddress addressText |> Option.isSome
    
    let inferDataType (address: PlcAddress) : PlcDataType =
        match address.DataSize with
        | 1 -> PlcDataType.Bool
        | 8 -> PlcDataType.UInt8
        | 16 -> PlcDataType.UInt16
        | 32 -> PlcDataType.UInt32
        | 64 -> PlcDataType.UInt64
        | _ -> PlcDataType.UInt32 // 기본값

[<Extension>]
type AbAddressParser() =
    
    interface IAddressParser with
        member _.TryParseAddress(addressText: string) =
            match AddressParserModule.tryParseTagAddress addressText with
            | Some address ->
                Some {
                    Address = address
                    OriginalText = addressText
                    NormalizedText = addressText.Trim()
                }
            | None -> None
        
        member _.FormatAddress(address: PlcAddress) =
            AddressParserModule.formatTagAddress address
        
        member _.ValidateAddress(addressText: string) =
            AddressParserModule.validateTagAddress addressText
        
        member _.InferDataType(address: PlcAddress) =
            AddressParserModule.inferDataType address
        
        member _.SupportedDeviceTypes = 
            [
                "Global"                    // 글로벌 태그
                "Program"                   // 프로그램 로컬 태그
                "Local"                     // I/O 모듈 태그
                "_System"                   // 시스템 태그
                "Controller"                // 컨트롤러 태그
                "Task"                      // 태스크 태그
            ]

    /// AB 태그가 배열 타입인지 확인
    [<Extension>]
    static member IsArrayTag(addressText: string) : bool =
        not (String.IsNullOrWhiteSpace(addressText)) &&
        Regex.IsMatch(addressText, @"\[\d+\]")
    
    /// AB 태그가 구조체 멤버인지 확인  
    [<Extension>]
    static member IsStructMember(addressText: string) : bool =
        not (String.IsNullOrWhiteSpace(addressText)) &&
        addressText.Contains(".") &&
        not (addressText.StartsWith("Program:")) &&
        not (addressText.StartsWith("Local:"))
    
    /// AB 태그가 비트 액세스인지 확인
    [<Extension>]
    static member IsBitAccess(addressText: string) : bool =
        not (String.IsNullOrWhiteSpace(addressText)) &&
        Regex.IsMatch(addressText, @"\.\d+$")