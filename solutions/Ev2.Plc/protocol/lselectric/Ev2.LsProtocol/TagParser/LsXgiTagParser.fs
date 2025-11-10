namespace Ev2.LsProtocol

open System
open System.Globalization
open System.Runtime.CompilerServices
open System.Text.RegularExpressions


module internal LsXgiTagParserHelpers =
    let wordSize = 16
    let bitSize = 1

    let dataSizeMap =
        Map [
            "X", 1
            "B", 8
            "W", 16
            "D", 32
            "L", 64
        ]

    let dataRangeMap =
        Map [
            "X", 64 / 1
            "B", 64 / 8
            "W", 64 / 16
            "D", 64 / 32
            "L", 64 / 64
        ]

    let dataUTypeRangeMap =
        Map [
            "X", 512 / 1
            "B", 512 / 8
            "W", 512 / 16
            "D", 512 / 32
            "L", 512 / 64
        ]

    let tryParseInt (value: string) =
        match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
        | true, v -> Some v
        | _ -> None

    let tryParseParts (text: string) =
        let parts = text.Split([|'.'|], StringSplitOptions.RemoveEmptyEntries)
        let mutable ok = true
        let values = ResizeArray<int>()
        for part in parts do
            match tryParseInt part with
            | Some v -> values.Add v
            | None -> ok <- false
        if ok then Some (List.ofSeq values) else None

    let parseStandardTag (device: string) (dataType: string) (digits: int list) : XgtParserTag option =
        match digits with
        | [d1] ->
            if dataType = "X" then
                Some { DeviceType = device; DataSize = bitSize; TotalBitOffset = d1 }
            else
                match Map.tryFind dataType dataSizeMap with
                | Some size -> Some { DeviceType = device; DataSize = size; TotalBitOffset = size * d1 }
                | None -> None
        | [d1; d2] ->
            match Map.tryFind dataType dataSizeMap with
            | Some size when d2 >= 0 && d2 < size ->
                Some { DeviceType = device; DataSize = bitSize; TotalBitOffset = size * d1 + d2 }
            | _ -> None
        | _ -> None

    let parseIoTag (device: string) (safety: string) (dataType: string) (digits: int list) : XgtParserTag option =
        match digits, Map.tryFind dataType dataSizeMap, Map.tryFind dataType dataRangeMap with
        | [d1; d2; d3], Some size, Some range when d3 >= 0 && d3 < range ->
            let total = d1 * 1024 + d2 * 64 + d3 * size
            Some { DeviceType = device + safety; DataSize = size; TotalBitOffset = total }
        | _ -> None

    let parseUTypeTag (device: string) (dataType: string) (digits: int list) : XgtParserTag option =
        match digits, Map.tryFind dataType dataSizeMap, Map.tryFind dataType dataUTypeRangeMap with
        | [d1; d2; d3], Some size, Some range when d1 >= 0 && d1 <= 7 && d2 >= 0 && d2 <= 15 && d3 >= 0 && d3 < range ->
            let total = d1 * (512 * 16) + d2 * 512 + d3 * size
            Some { DeviceType = device; DataSize = size; TotalBitOffset = total }
        | _ -> None

module LsXgiAddressParserModule =
    open LsXgiTagParserHelpers

    let tryParseXgiAddress (address: string) : XgtParserTag option =
        if String.IsNullOrWhiteSpace address then None
        else
            let normalized = address.Trim().ToUpperInvariant()
            let m = Regex.Match(normalized, @"^%([IQMLKFNRAWUT])(S)?([XBWDL])([\d\.]+)$", RegexOptions.IgnoreCase)
            if m.Success then
                let device = m.Groups.[1].Value
                let safetyGroup = m.Groups.[2].Value
                let dataType = m.Groups.[3].Value
                let remaining = m.Groups.[4].Value
                match tryParseParts remaining with
                | Some digits ->
                    let safety = if String.IsNullOrEmpty safetyGroup then "" else safetyGroup

                    if safety = "" && remaining.Split('.').Length = 2 && List.contains device ["I";"Q";"M";"L";"N";"K";"R";"A";"W";"F"] && List.contains dataType ["X";"B";"W";"D";"L"] then
                        parseStandardTag device dataType digits
                    elif (safety = "" || safety = "S") && (device = "I" || device = "Q") && List.contains dataType ["X";"B";"W";"D";"L"] then
                        parseIoTag device safety dataType digits
                    elif safety = "" && device = "U" && List.contains dataType ["X";"B";"W";"D";"L"] then
                        parseUTypeTag device dataType digits
                    else None
                | None -> None
            else None

    let parseAddressIO (device: string) (bitSize: int) (offset: int) (slot: int) (sumBit: int) =
        match bitSize with
        | 1 -> sprintf "%%%sX0.%d.%d" device slot ((offset - sumBit) % 64)
        | 8 when offset % 8 = 0 -> sprintf "%%%sB%d" device (offset / 8)
        | 16 when offset % 16 = 0 -> sprintf "%%%sW%d" device (offset / 16)
        | 32 when offset % 32 = 0 -> sprintf "%%%sD%d" device (offset / 32)
        | 64 when offset % 64 = 0 -> sprintf "%%%sL%d" device (offset / 64)
        | _ -> invalidArg "bitSize" (sprintf "Invalid bit size %d for offset %d" bitSize offset)

    let parseAddressMemory (tag: XgtParserTag) =
        match tag.DataSize with
        | 1 -> sprintf "%%%sX%d" tag.DeviceType tag.TotalBitOffset
        | 8 when tag.TotalBitOffset % 8 = 0 -> sprintf "%%%sB%d" tag.DeviceType (tag.TotalBitOffset / 8)
        | 16 when tag.TotalBitOffset % 16 = 0 -> sprintf "%%%sW%d" tag.DeviceType (tag.TotalBitOffset / 16)
        | 32 when tag.TotalBitOffset % 32 = 0 -> sprintf "%%%sD%d" tag.DeviceType (tag.TotalBitOffset / 32)
        | 64 when tag.TotalBitOffset % 64 = 0 -> sprintf "%%%sL%d" tag.DeviceType (tag.TotalBitOffset / 64)
        | _ -> invalidArg "tag" (sprintf "Invalid offset %d for data size %d" tag.TotalBitOffset tag.DataSize)

[<Extension>]
type LsXgiTagParser =

    [<Extension>]
    static member Parse(tag: string) : string * int * int =
        match LsXgiAddressParserModule.tryParseXgiAddress tag with
        | Some info -> (info.DeviceType, info.DataSize, info.TotalBitOffset)
        | None -> Unchecked.defaultof<_>

    [<Extension>]
    static member TryParse(tag: string) : (string * int * int) option =
        LsXgiAddressParserModule.tryParseXgiAddress tag
        |> Option.map (fun info -> (info.DeviceType, info.DataSize, info.TotalBitOffset))

    [<Extension>]
    static member ParseAddressIO(device: string, bitSize: int, offset: int, slot: int, sumBit: int) : string =
        LsXgiAddressParserModule.parseAddressIO device bitSize offset slot sumBit

    [<Extension>]
    static member ParseAddressMemory(tag: XgtParserTag) : string =
        LsXgiAddressParserModule.parseAddressMemory tag
