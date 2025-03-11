namespace Dual.Plc2DS

open System.IO
open System.Text

open Dual.Common.Core.FS
open Dual.Common.Core

[<AutoOpen>]
module Util =
    type File =
        /// CVS 파일에서 헤더가 나올 때까지 읽음.  Header 자체는 포함하지 않음
        static member TryReadUntilHeader(filePath: string, csvHeader: string, ?maxLine: int, ?encoding: Encoding): string[] option =
            let encoding = encoding |? FileEx.GetEncoding(filePath)
            let maxLine = maxLine |? 20 //Int32.MaxValue // 최대 라인이 없으면 무제한으로 설정
            let mutable found = false
            let headers =
                File.ReadLines(filePath, encoding)
                |> Seq.truncate maxLine
                |> Seq.takeWhile (fun line ->
                    if line = csvHeader then found <- true
                    not found
                )
                |> Seq.toArray

            if found then Some (headers @ [|csvHeader|]) else None

        static member PeekLines(filePath: string, ?startLine: int, ?lineCount: int, ?encoding:Encoding): string seq =
            let startLine = startLine |? 0
            let encoding = encoding |? FileEx.GetEncoding(filePath)
            File.ReadLines(filePath, encoding)
            |> Seq.skip (startLine)     // 시작 라인까지 건너뜀
            |> fun lines ->
                match lineCount with
                | Some lineCount -> lines |> Seq.truncate lineCount   // 지정된 개수만큼 읽음
                | None -> lines

    type Csv =
        //static member ParseLineLegacy (line: string, ?delimeter:char) =
        //    let delimeter = delimeter |? ','
        //    let pattern = $"""(?:^|{delimeter})(?:"([^"]*)"|([^{delimeter}]*))""" // 쉼표로 시작하는 경우도 감지
        //    Regex.Matches(line, pattern)
        //    |> Seq.cast<Match>
        //    |> Seq.map (fun m ->
        //        if m.Groups.[1].Success then m.Groups.[1].Value // 따옴표 안 값
        //        else if m.Groups.[2].Success then m.Groups.[2].Value // 일반 값
        //        else "" // 빈 값
        //    )
        //    |> Seq.toArray

        static member ParseLine (line: string, ?delimeter: char, ?trim:bool): string[] =
            let delimeter = delimeter |? ','
            let trim = trim |? true
            let mutable insideQuotes = false
            let mutable buffer:char list = []
            let mutable result:string list = []
            let unbuffer() =
                let col = buffer |> List.rev |> System.String.Concat
                buffer <- []
                if trim then col.Trim() else col

            for (i, ch) in line |> indexed do
                match ch, insideQuotes with
                | '"', _ ->
                    insideQuotes <- not insideQuotes  // 따옴표 안/밖 전환

                    // 맨 마지막에 ,"" 로 끝나는 경우, empty string column 추가
                    if i = line.Length-1 && buffer = [] && not insideQuotes then
                        result <- "" :: result

                | c, true -> buffer <- c :: buffer  // 따옴표 안에 있는 문자
                | d, false when d = delimeter ->
                    result <- unbuffer() :: result
                    buffer <- []
                | c, false -> buffer <- c :: buffer

            if buffer <> [] then
                result <- unbuffer() :: result

            result |> List.rev |> Array.ofList
