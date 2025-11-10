namespace Ev2.LsProtocol

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions

[<AutoOpen>]
module AddressParserModule =

    let private ensureNoEmpty (addresses: string seq) =
        if addresses |> Seq.exists String.IsNullOrWhiteSpace then
            invalidArg "addresses" "Address string cannot be empty."

    let dataTypeFromBitSize bitSize =
        match bitSize with
        | 1 -> PlcDataType.Bool
        | 8 -> PlcDataType.UInt8
        | 16 -> PlcDataType.UInt16
        | 32 -> PlcDataType.UInt32
        | 64 -> PlcDataType.UInt64
        | _ when bitSize % 8 = 0 -> PlcDataType.Bytes (bitSize / 8)
        | _ ->
            // round up to the nearest byte
            PlcDataType.Bytes ((bitSize + 7) / 8)

    let private bitSizeFromXgiAddress (address: string) =
        let normalized = address.Trim().ToUpperInvariant()
        let m = Regex.Match(normalized, @"^%[A-Z]+([XBWDL])", RegexOptions.IgnoreCase)
        if m.Success then
            match m.Groups.[1].Value with
            | "X" -> 1
            | "B" -> 8
            | "W" -> 16
            | "D" -> 32
            | "L" -> 64
            | _ -> 16
        else 16

    let private bitSizeFromXgkAddress (address: string) =
        let normalized = address.Trim().ToUpperInvariant()
        let deviceCandidates =
            [ "ZR"; "SD"; "SW"; "SB"; "DX"; "DY"; "SM"; "SX"; "X"; "Y"; "M"; "L"; "F"; "B"; "V"; "D"; "R"; "W"; "K"; "P"; "N"; "T"; "C"; "Q"; "A" ]
        let device = deviceCandidates |> List.tryFind (normalized.StartsWith)
        match device with
        | Some dev when dev = "X" || dev = "Y" || dev = "M" || dev = "L" || dev = "F" || dev = "B" || dev = "SB" || dev = "SM" || dev = "DX" || dev = "DY" -> 1
        | Some dev when dev = "ZR" || dev = "SD" || dev = "SW" || dev = "SB" || dev = "D" || dev = "R" || dev = "W" || dev = "V" -> 16
        | Some dev when dev = "P" || dev = "K" || dev = "N" || dev = "U" || dev = "Q" || dev = "A" -> 16
        | _ ->
            if normalized.Contains(".") then 1 else 16

    let isXGI (addresses: string seq) : bool =
        ensureNoEmpty addresses

        let trimmed = addresses |> Seq.filter (fun f -> not (String.IsNullOrWhiteSpace f)) |> Seq.map (fun t -> t.Trim()) |> Seq.toList
        let hasXGI = trimmed |> List.exists (fun t -> t.StartsWith("%"))
        let hasXGK = trimmed |> List.exists (fun t -> not (t.StartsWith("%")))

        if hasXGI && hasXGK then
            let xgiAddresses = trimmed |> List.filter (fun t -> t.StartsWith("%")) |> String.concat ", "
            let xgkAddresses = trimmed |> List.filter (fun t -> not (t.StartsWith("%"))) |> String.concat ", "
            invalidArg "addresses" ($"XGI addresses and XGK addresses cannot be mixed.\nXGI: {xgiAddresses}\nXGK: {xgkAddresses}")

        hasXGI

    let getBitSize (address: string) (isXgi: bool) =
        if isXgi then
            match LsXgiAddressParserModule.tryParseXgiAddress address with
            | Some info -> info.DataSize
            | None -> bitSizeFromXgiAddress address
        else
            match LsXgkAddressParserModule.tryParseXgkAddress address with
            | Some info -> info.DataSize
            | None -> bitSizeFromXgkAddress address

    let convertXgtParserTagToPlcAddress (xgtTag: XgtParserTag) (originalText: string) : AddressParseResult =
        let bitPos = 
            if xgtTag.DataSize = 1 then 
                Some (xgtTag.TotalBitOffset % 16)
            else None
        
        let address = xgtTag.TotalBitOffset / (max xgtTag.DataSize 16)
        
        let plcAddress = {
            DeviceType = xgtTag.DeviceType
            Address = address
            BitPosition = bitPos
            DataSize = xgtTag.DataSize
            TotalBitOffset = xgtTag.TotalBitOffset
        }
        
        {
            Address = plcAddress
            OriginalText = originalText
            NormalizedText = originalText.Trim().ToUpperInvariant()
        }

[<Extension>]
type LsAddressParser() =
    
    interface IAddressParser with
        member _.TryParseAddress(addressText: string) =
            if String.IsNullOrWhiteSpace(addressText) then None
            else
                let isXgi = AddressParserModule.isXGI [addressText]
                if isXgi then
                    match LsXgiAddressParserModule.tryParseXgiAddress addressText with
                    | Some xgtTag -> Some (AddressParserModule.convertXgtParserTagToPlcAddress xgtTag addressText)
                    | None -> None
                else
                    match LsXgkAddressParserModule.tryParseXgkAddress addressText with
                    | Some xgtTag -> Some (AddressParserModule.convertXgtParserTagToPlcAddress xgtTag addressText)
                    | None -> None
        
        member _.FormatAddress(address: PlcAddress) =
            let xgtTag = {
                DeviceType = address.DeviceType
                DataSize = address.DataSize
                TotalBitOffset = address.TotalBitOffset
            }
            
            // XGI 또는 XGK 포맷으로 변환
            if address.DeviceType.StartsWith("I") || address.DeviceType.StartsWith("Q") then
                // XGI 포맷
                LsXgiAddressParserModule.parseAddressMemory xgtTag
            else
                // XGK 포맷
                let isBit = address.DataSize = 1
                LsXgkAddressParserModule.parseAddress address.DeviceType address.TotalBitOffset isBit
        
        member _.ValidateAddress(addressText: string) =
            if String.IsNullOrWhiteSpace(addressText) then false
            else
                let isXgi = AddressParserModule.isXGI [addressText]
                if isXgi then
                    LsXgiAddressParserModule.tryParseXgiAddress addressText |> Option.isSome
                else
                    LsXgkAddressParserModule.tryParseXgkAddress addressText |> Option.isSome
        
        member _.InferDataType(address: PlcAddress) =
            AddressParserModule.dataTypeFromBitSize address.DataSize
        
        member _.SupportedDeviceTypes = 
            [
                // XGI devices
                "I"; "Q"; "M"; "L"; "N"; "K"; "R"; "A"; "W"; "F"; "U"
                
                // XGK devices  
                "P"; "M"; "K"; "F"; "T"; "C"; "N"; "D"; "R"; "L"; "ZR"; "U"
                
                // Bit devices
                "X"; "Y"; "B"; "SB"; "SM"; "DX"; "DY"; "SX"
            ]

    [<Extension>]
    static member IsXGI(addresses: string seq) =
        AddressParserModule.isXGI addresses

    [<Extension>]
    static member GetDataType(address: string, isXgi: bool) : PlcDataType =
        let bitSize = AddressParserModule.getBitSize address isXgi
        AddressParserModule.dataTypeFromBitSize bitSize