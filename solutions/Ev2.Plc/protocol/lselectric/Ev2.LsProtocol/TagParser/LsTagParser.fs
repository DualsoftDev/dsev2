namespace Ev2.LsProtocol

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open Ev2.LsProtocol

[<AutoOpen>]
module LsTagParserModule =

    let private ensureNoEmpty (tags: string seq) =
        if tags |> Seq.exists String.IsNullOrWhiteSpace then
            invalidArg "tags" "Tag address cannot be empty."

    let dataTypeFromBitSize bitSize =
        match bitSize with
        | 1 -> PlcTagDataType.Bool
        | 8 -> PlcTagDataType.UInt8
        | 16 -> PlcTagDataType.UInt16
        | 32 -> PlcTagDataType.UInt32
        | 64 -> PlcTagDataType.UInt64
        | _ when bitSize % 8 = 0 -> PlcTagDataType.Bytes (bitSize / 8)
        | _ ->
            // round up to the nearest byte
            PlcTagDataType.Bytes ((bitSize + 7) / 8)

    let private bitSizeFromXgiTag (tag: string) =
        let normalized = tag.Trim().ToUpperInvariant()
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

    let private bitSizeFromXgkTag (tag: string) =
        let normalized = tag.Trim().ToUpperInvariant()
        let deviceCandidates =
            [ "ZR"; "SD"; "SW"; "SB"; "DX"; "DY"; "SM"; "SX"; "X"; "Y"; "M"; "L"; "F"; "B"; "V"; "D"; "R"; "W"; "K"; "P"; "N"; "T"; "C"; "Q"; "A" ]
        let device = deviceCandidates |> List.tryFind (normalized.StartsWith)
        match device with
        | Some dev when dev = "X" || dev = "Y" || dev = "M" || dev = "L" || dev = "F" || dev = "B" || dev = "SB" || dev = "SM" || dev = "DX" || dev = "DY" -> 1
        | Some dev when dev = "ZR" || dev = "SD" || dev = "SW" || dev = "SB" || dev = "D" || dev = "R" || dev = "W" || dev = "V" -> 16
        | Some dev when dev = "P" || dev = "K" || dev = "N" || dev = "U" || dev = "Q" || dev = "A" -> 16
        | _ ->
            if normalized.Contains(".") then 1 else 16

    let isXGI (tags: string seq) : bool =
        ensureNoEmpty tags

        let trimmed = tags |> Seq.filter (fun f -> not (String.IsNullOrWhiteSpace f)) |> Seq.map (fun t -> t.Trim()) |> Seq.toList
        let hasXGI = trimmed |> List.exists (fun t -> t.StartsWith("%"))
        let hasXGK = trimmed |> List.exists (fun t -> not (t.StartsWith("%")))

        if hasXGI && hasXGK then
            let xgiTags = trimmed |> List.filter (fun t -> t.StartsWith("%")) |> String.concat ", "
            let xgkTags = trimmed |> List.filter (fun t -> not (t.StartsWith("%"))) |> String.concat ", "
            invalidArg "tags" ($"XGI tags and XGK tags cannot be mixed.\nXGI: {xgiTags}\nXGK: {xgkTags}")

        hasXGI

    let getBitSize (tag: string) (isXgi: bool) =
        if isXgi then
            match LsXgiAddressParserModule.tryParseXgiAddress tag with
            | Some info -> info.DataSize
            | None -> bitSizeFromXgiTag tag
        else
            match LsXgkAddressParserModule.tryParseXgkAddress tag with
            | Some info -> info.DataSize
            | None -> bitSizeFromXgkTag tag

[<Extension>]
type LsTagParser =

    [<Extension>]
    static member IsXGI(tags: string seq) =
        LsTagParserModule.isXGI tags

    [<Extension>]
    static member GetDataType(tag: string, isXgi: bool) : PlcTagDataType =
        let bitSize = LsTagParserModule.getBitSize tag isXgi
        LsTagParserModule.dataTypeFromBitSize bitSize
