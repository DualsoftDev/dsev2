namespace Dual.Plc2DS.Common.FS

open System.IO
open Dual.Common.Core
open Dual.Common.Core.FS
open System.Text

[<AutoOpen>]
module Util =
    type File =
        static member PeekLines(filePath: string, ?startLine: int, ?lineCount: int, ?encoding:Encoding): string[] =
            let startLine = startLine |? 0
            let encoding = encoding |? FileEx.GetEncoding(filePath)
            File.ReadLines(filePath, encoding)
            |> Seq.skip (startLine)     // 시작 라인까지 건너뜀
            |> fun lines ->
                match lineCount with
                | Some lineCount -> lines |> Seq.truncate lineCount   // 지정된 개수만큼 읽음
                | None -> lines
            |> Seq.toArray
