namespace Ev2.LsProtocol

open System
open System.Globalization
open System.Runtime.CompilerServices
open System.Text.RegularExpressions


module internal LsXgkTagParserHelpers =
    let wordSize = 16
    let bitSize = 1

    let fiveDigitDevices = [ "N"; "D"; "R" ]
    let fourDigitDevices = [ "P"; "M"; "K"; "F"; "T"; "C" ]
    let uDevice = "U"
    let lDevice = "L"
    let zrDevice = "ZR"

    let tryParseInt (value: string) =
        match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
        | true, v -> Some v
        | _ -> None

    let tryParseHex (value: string) =
        match Int32.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) with
        | true, v -> Some v
        | _ -> None

    let parseHexaBitString (input: string) : int * int =
        if String.IsNullOrEmpty(input) then wordSize, 0
        else
            let clean = if input.StartsWith(".") then input.Substring(1) else input
            match tryParseHex clean with
            | Some bit when bit >= 0 && bit < 16 -> bitSize, bit
            | _ -> wordSize, 0

    let parsePMKF device remaining =
        let m = Regex.Match(remaining, @"^(\d{4})([\da-fA-F]?)$", RegexOptions.IgnoreCase)
        if m.Success then
            let wordStr = m.Groups.[1].Value
            let bitStr = m.Groups.[2].Value
            match tryParseInt wordStr with
            | Some word ->
                let size, bit = parseHexaBitString bitStr
                Some { DeviceType = device; DataSize = size; TotalBitOffset = word * 16 + bit }
            | None -> None
        else None

    let parseLDevice device remaining =
        let m = Regex.Match(remaining, @"^(\d{5})([\da-fA-F]?)$", RegexOptions.IgnoreCase)
        if m.Success then
            let wordStr = m.Groups.[1].Value
            let bitStr = m.Groups.[2].Value
            match tryParseInt wordStr with
            | Some word ->
                let size, bit = parseHexaBitString bitStr
                Some { DeviceType = device; DataSize = size; TotalBitOffset = word * 16 + bit }
            | None -> None
        else None

    let parseTCNDR device remaining =
        let m = Regex.Match(remaining, @"^(\d+)(\.[\da-fA-F]?)?$", RegexOptions.IgnoreCase)
        if m.Success then
            let wordStr = m.Groups.[1].Value
            let bitStr = m.Groups.[2].Value
            match tryParseInt wordStr with
            | Some word ->
                let size, bit = parseHexaBitString bitStr
                Some { DeviceType = device; DataSize = size; TotalBitOffset = word * 16 + bit }
            | None -> None
        else None

    let parseZR device remaining =
        let m = Regex.Match(remaining, @"^(\d+)$", RegexOptions.IgnoreCase)
        if m.Success then
            let wordStr = m.Groups.[1].Value
            match tryParseInt wordStr with
            | Some word -> Some { DeviceType = device; DataSize = wordSize; TotalBitOffset = word * 16 }
            | None -> None
        else None

    let parseZRFile devicePrefix remaining =
        let m = Regex.Match(remaining, @"^(\d+)(\.[\da-fA-F]?)?$", RegexOptions.IgnoreCase)
        if m.Success then
            let wordStr = m.Groups.[1].Value
            let bitStr = m.Groups.[2].Value
            match tryParseInt wordStr with
            | Some word ->
                let size, bit = parseHexaBitString bitStr
                Some { DeviceType = devicePrefix; DataSize = size; TotalBitOffset = word * 16 + bit }
            | None -> None
        else None

    let parseUDevice device remaining =
        let m = Regex.Match(remaining, @"^([\da-fA-F]+)\.(\d+)(?:\.(\w+))?$", RegexOptions.IgnoreCase)
        if m.Success then
            let fileStr = m.Groups.[1].Value
            let subStr = m.Groups.[2].Value
            let bitStr = m.Groups.[3].Value
            match tryParseHex fileStr, tryParseInt subStr with
            | Some fileIndex, Some subIndex when subIndex >= 0 && subIndex < 16 ->
                let bitOffset =
                    if String.IsNullOrEmpty bitStr then 0
                    else
                        let _, bit = parseHexaBitString bitStr
                        bit
                let size = if String.IsNullOrEmpty bitStr then wordSize else bitSize
                let total = (fileIndex * 32 * 16) + (subIndex * 16) + bitOffset
                Some { DeviceType = device; DataSize = size; TotalBitOffset = total }
            | _ -> None
        else None

    let generateBitText (device: string) (offset: int) =
        let word = offset / 16
        let bit = offset % 16
        let upper = device.ToUpperInvariant()
        match upper with
        | d when List.contains d fiveDigitDevices -> sprintf "%s%05d.%X" upper word bit
        | d when List.contains d fourDigitDevices -> sprintf "%s%04d%X" upper word bit
        | d when d = uDevice ->
            let file = offset / (32 * 16)
            let rem = offset % (32 * 16)
            let sub = rem / 16
            let bitPart = rem % 16
            sprintf "%s%X.%d.%02X" upper file sub bitPart
        | d when d = lDevice -> sprintf "%s%05d%X" upper word bit
        | _ -> invalidArg "device" (sprintf "Unsupported XGK device %s" device)

    let generateWordText (device: string) (offset: int) =
        let upper = device.ToUpperInvariant()
        let wordIndex = offset / 16
        match upper with
        | d when List.contains d fiveDigitDevices -> sprintf "%s%05d" upper wordIndex
        | d when List.contains d fourDigitDevices -> sprintf "%s%04d" upper wordIndex
        | d when d = uDevice ->
            let file = offset / (32 * 16)
            let rem = offset % (32 * 16)
            let sub = rem / 16
            sprintf "%s%X.%d" upper file sub
        | _ -> invalidArg "device" (sprintf "Unsupported XGK device %s" device)

    let createAddress (device: string) (offset: int) (isBit: bool) =
        if isBit then generateBitText device offset
        else generateWordText device offset

    let normalizeXgkTag (tag: string) (isBit: bool) =
        if String.IsNullOrWhiteSpace tag || tag.Length < 2 then tag
        else
            let normalized = tag.ToUpperInvariant()
            match normalized.[0] with
            | 'P' | 'M' | 'K' | 'F' ->
                let padCount = if isBit then 5 else 4
                let rest = normalized.Substring(1).PadLeft(padCount, '0')
                normalized.[0].ToString() + rest
            | 'L' ->
                let rest = normalized.Substring(1).PadLeft(6, '0')
                normalized.[0].ToString() + rest
            | _ -> normalized

module LsXgkAddressParserModule =
    open LsXgkTagParserHelpers

    let tryParseXgkAddress (tag: string) : XgtParserTag option =
        if String.IsNullOrWhiteSpace tag || tag.Length < 2 then None
        else
            let normalized = tag.ToUpperInvariant()
            let device = normalized.Substring(0, 1)
            let remaining = normalized.Substring(1)
            let devicePrefix = if normalized.Length >= 2 then normalized.Substring(0, 2) else normalized
            match device.[0], devicePrefix with
            | ('P' | 'M' | 'K' | 'F'), _ -> parsePMKF device remaining
            | 'L', _ -> parseLDevice device remaining
            | ('T' | 'C' | 'N' | 'D' | 'R'), _ when devicePrefix <> zrDevice -> parseTCNDR device remaining
            | ('Z' | 'R'), _ when devicePrefix <> zrDevice -> parseZR device remaining
            | _, prefix when prefix = zrDevice ->
                let rest = if normalized.Length > 2 then normalized.Substring(2) else String.Empty
                parseZRFile prefix rest
            | 'U', _ -> parseUDevice device remaining
            | 'S', _ -> None
            | _ -> None

    let tryParseXgkValidText (tag: string) (isBit: bool) =
        if String.IsNullOrWhiteSpace tag then None
        else
            let normalized = normalizeXgkTag tag isBit
            match tryParseXgkAddress normalized with
            | Some info when isBit && info.DataSize = bitSize -> Some (generateBitText info.DeviceType info.TotalBitOffset)
            | Some info when not isBit && info.DataSize = wordSize -> Some (generateWordText info.DeviceType info.TotalBitOffset)
            | _ -> None

    let parseAddress device offsetBit isBit = createAddress device offsetBit isBit

[<Extension>]
type LsXgkTagParser =

    [<Extension>]
    static member Parse(tag: string) : string * int * int =
        match LsXgkAddressParserModule.tryParseXgkAddress tag with
        | Some info -> (info.DeviceType, info.DataSize, info.TotalBitOffset)
        | None -> Unchecked.defaultof<_>

    [<Extension>]
    static member TryParse(tag: string) : (string * int * int) option =
        LsXgkAddressParserModule.tryParseXgkAddress tag
        |> Option.map (fun info -> (info.DeviceType, info.DataSize, info.TotalBitOffset))

    [<Extension>]
    static member TryParseWithIsBoolType(tag: string, isBit: bool) : (string * int * int) option =
        match LsXgkAddressParserModule.tryParseXgkValidText tag isBit with
        | Some text ->
            LsXgkAddressParserModule.tryParseXgkAddress text
            |> Option.map (fun info -> (info.DeviceType, info.DataSize, info.TotalBitOffset))
        | None -> None

    [<Extension>]
    static member Parse(tag: string, isBit: bool) : string * int * int =
        match LsXgkAddressParserModule.tryParseXgkValidText tag isBit with
        | Some text ->
            match LsXgkAddressParserModule.tryParseXgkAddress text with
            | Some info -> (info.DeviceType, info.DataSize, info.TotalBitOffset)
            | None -> Unchecked.defaultof<_>
        | None -> Unchecked.defaultof<_>

    [<Extension>]
    static member ParseAddress(device: string, offsetBit: int, isBit: bool) : string =
        LsXgkAddressParserModule.parseAddress device offsetBit isBit

    [<Extension>]
    static member ParseValidText(tag: string, isBit: bool) : string =
        match LsXgkAddressParserModule.tryParseXgkValidText tag isBit with
        | Some text -> text
        | None -> Unchecked.defaultof<_>
