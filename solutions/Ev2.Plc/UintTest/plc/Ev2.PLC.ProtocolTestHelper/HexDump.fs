namespace ProtocolTestHelper

open System
open System.Text

/// Pretty printers for byte-oriented protocols.
[<AutoOpen>]
module HexDump =

    /// Formats the supplied buffer as a canonical hex dump (16 bytes per line).
    let format (data: byte[]) =
        if obj.ReferenceEquals(data, null) || data.Length = 0 then
            "<empty>"
        else
            let builder = StringBuilder()
            let ascii = StringBuilder(16)

            let mutable offset = 0
            for value in data do
                if offset % 16 = 0 then
                    if offset > 0 then
                        builder.Append("  ").Append(ascii.ToString()).AppendLine() |> ignore
                        ascii.Clear() |> ignore
                    builder.AppendFormat("{0:X4}: ", offset) |> ignore

                builder.AppendFormat("{0:X2} ", value) |> ignore
                ascii.Append(if value >= 0x20uy && value <= 0x7Euy then char value else '.') |> ignore
                offset <- offset + 1

            // pad trailing spacing if final row has fewer than 16 bytes
            let remaining = offset % 16
            if remaining <> 0 then
                for _ in remaining .. 15 do
                    builder.Append("   ") |> ignore
            builder.Append("  ").Append(ascii.ToString()).ToString()

    /// Formats a comparison between two buffers, highlighting the first differing offset.
    let diff (expected: byte[]) (actual: byte[]) =
        if Object.ReferenceEquals(expected, actual) then
            "Buffers reference the same instance."
        else
            let minLength = min expected.Length actual.Length
            let mutable mismatchIndex = -1
            let mutable index = 0
            while mismatchIndex = -1 && index < minLength do
                if expected.[index] <> actual.[index] then
                    mismatchIndex <- index
                index <- index + 1

            let msg =
                if mismatchIndex = -1 then
                    if expected.Length = actual.Length then
                        "Buffers are identical."
                    else
                        sprintf "Buffer lengths differ. Expected %d bytes, actual %d bytes." expected.Length actual.Length
                else
                    sprintf "First difference at offset %d (expected 0x%02X, actual 0x%02X)." mismatchIndex expected.[mismatchIndex] actual.[mismatchIndex]

            StringBuilder()
                .AppendLine(msg)
                .AppendLine("[expected]")
                .AppendLine(format expected)
                .AppendLine()
                .AppendLine("[actual]")
                .AppendLine(format actual)
                .ToString()

    /// Writes the formatted dump to the console (useful inside tests where logging is unavailable).
    let print (label: string) (data: byte[]) =
        let timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff")
        Console.WriteLine("[{0}] {1}", timestamp, label)
        Console.WriteLine(format data)
