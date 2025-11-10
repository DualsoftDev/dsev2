namespace PLC.Convert.MX

open System
open System.Diagnostics
open System.IO
open System.Linq
open System.Collections.Generic
open Microsoft.VisualBasic.FileIO
open System.Text.RegularExpressions
open Dual.Common.Core.FS
open Dual.Common.Base.FS

[<AutoOpen>]
module CSVParser =


    type Column = { ColumnName: string; Index: int }
    type CsvData = { Columns: Column[]; Rows: Dictionary<string, string>[] }

    let rowProcessedEvent = Event<int>() // 행 처리 이벤트
    let RowProcessed = rowProcessedEvent.Publish

    let totalLinesEvent = Event<int>() // 전체 행 수 이벤트
    let TotalLines = totalLinesEvent.Publish

    let parseCsv (filePath: string, skipRows: int) =
        use parser = new TextFieldParser(filePath)
        parser.TextFieldType <- FieldType.Delimited
        parser.SetDelimiters [| ","; "\t"; "|"; ";" |]

        // 전체 행 수 계산
        let totalLines =
            File.ReadLines(filePath) // 효율적인 스트림 기반 파일 읽기
            |> Seq.length

        // 전체 라인 수 이벤트 발생
        totalLinesEvent.Trigger(totalLines)

        let mutable rowCount = 1
        let mutable columns = [||]
        let rows = ResizeArray<Dictionary<string, string>>()

        while not parser.EndOfData do
            let fields = parser.ReadFields()
            match rowCount with
            | n when n = skipRows ->
                // 열 이름과 인덱스를 초기화
                columns <- Array.mapi (fun i colName -> { ColumnName = colName; Index = i }) fields
            | n when n > skipRows ->
                // 행 데이터를 딕셔너리로 변환하여 추가
                let row = Dictionary<string, string>(fields.Length) // 초기 크기 지정
                for col in columns do
                    if col.Index < fields.Length then
                        row.[col.ColumnName] <- fields.[col.Index]
                rows.Add(row)
            | _ -> ()
            rowCount <- rowCount + 1
            if rowCount % 100 = 0 || rowCount = totalLines then
                rowProcessedEvent.Trigger(rowCount) // 주기적으로 이벤트 발생

        // 결과 반환
        { Columns = columns; Rows = rows.ToArray() }



    // MxIO 파일에서 데이터를 읽어와 DeviceIO 타입으로 변환
    let readMxIO (files: string seq) =
        let readMxIO (file: string) =
            let data = parseCsv(file, 3)
            seq {
                for row in data.Rows do
                    let slot = row.["Slot"]
                    let typ = row.["Type"]
                    let points = 
                        match row.["Points"] with
                        | null | "" -> -1
                        | value -> int value 
                    let startXY = 
                        match row.["Start XY"] with
                        | null | "" -> -1
                        | value -> int value 

                    yield { Slot = slot; Type = typ; Points = points; StartXY = startXY }
            }
        files |> Seq.collect readMxIO

    // MxRemoteIO 파일에서 데이터를 읽어와 DeviceRemoteIO 타입으로 변환
    let readMxRemoteIO (files: string seq) =
        let readMxRemoteIO (file: string) =
            let data = parseCsv(file, 2)
            seq {
                for row in data.Rows do
                    let remoteType = row.["'Type'"]
                    let startX = row.["'Network Assignment X'"]
                    let startY = row.["'Network Assignment Y'"]

                    yield { RemoteType = remoteType; StartX = startX; StartY = startY }
            }
        files |> Seq.collect readMxRemoteIO

    // 주석 CSV 파일에서 데이터를 읽어 CommentDictionary로 변환
    let readCommentCSV (files: string seq) =
        let readCommentCSV (file: string) =
            let data = parseCsv(file, 2)
            seq {
                for row in data.Rows do
                    let name = row.Values.First() 
                    let comment = row.Values.Last()  
                    yield name, comment 
            }
        files |> Seq.collect readCommentCSV |> dict |> Dictionary 

    let readGlobalLabelCSV (files: string seq) =
        let readCSV (file: string) =
            let data = parseCsv(file, 2)
            seq {
                for row in data.Rows do
                    let name = row.Values.Skip(1).First() 
                    let dataType = row.Values.Skip(2).First() 
                    yield name, dataType 
            }
        files |> Seq.collect readCSV |> dict |> Dictionary 


    // POU 파일에서 읽어올 열의 종류를 정의
    [<Flags>]
    type PouColumn =
        | StepNo        = 0
        | LineStatement = 1
        | Instruction   = 2
        | IO_Device     = 3
        | Blank         = 4
        | PI_Statement  = 5
        | Note          = 6

    // POU CSV 파일을 읽어 ProgramCSVLine 시퀀스로 변환
    let readProgramCSV (file: string) (commentDic: CommentDictionary) =
        let data = parseCsv(file, 3)
        let dicCol = data.Columns |> Seq.mapi(fun i f -> i, f.ColumnName) |> dict

        let mergeLines (lines: ProgramCSVLine seq) =
            let linesArray = lines |> Seq.toArray
            let arguments = linesArray |> Array.collect (fun line -> line.Arguments)
            { linesArray.[0] with Arguments = arguments }

        seq {
            for row in data.Rows do
                let dev = row[dicCol[(int)PouColumn.IO_Device]]
                let comment = if commentDic.ContainsKey dev then commentDic.[dev] else ""

                let arguments =
                    if dev = "" && comment = "" then Array.empty
                    else [| Contact({ Name = dev; Comment = comment }) |]

                let stepNo =
                    match System.Int32.TryParse(row.[dicCol[(int)PouColumn.StepNo]]) with
                    | true, n -> Some n
                    | false, _ -> None

                yield {
                    StepNo = stepNo
                    LineStatement = row.[dicCol[(int)PouColumn.LineStatement]]
                    Instruction = row.[dicCol[(int)PouColumn.Instruction]]
                    Arguments = arguments
                }
        }
        |> Seq.groupWhen (fun line -> line.Instruction.any() || line.LineStatement.any())
        |> Seq.map mergeLines

    // POU 파일에서 Rung를 추출하는 함수
    let extractRungsFromPOU pouCsv (commentDic: CommentDictionary) =
        let rungs =
            let isSplitPosition (s1: ProgramCSVLine) (s2: ProgramCSVLine) =
                (not (ListNotFinishs.Contains(s1.Instruction)) && s2.Instruction.Contains("LD"))
                || ListExCMDSingleLine.Contains(s2.Instruction)
                || Regex.IsMatch(s2.Instruction, @"P(\d+)") //P 레이블 사용시 새Rung 시작
            let lines = readProgramCSV pouCsv commentDic
            lines |> Seq.splitOn isSplitPosition |> Seq.map Array.ofSeq |> Array.ofSeq

        let name = Path.GetFileNameWithoutExtension(pouCsv)
        { Name = name; Rungs = rungs }

    // Branch 명령어들을 병합하는 함수
    let mergeBranchRungs (pou: POUParseResult) =
        let lstRung = ResizeArray<Rung>()
        let updateRung (rung: Rung) =
            let newRung = lstRung |> Seq.collect id |> Array.ofSeq
            lstRung.Clear()
            newRung

        let isMergeBranch (rung: Rung) =
            lstRung.Add(rung)
            let mps = lstRung |> Seq.collect (Seq.filter (fun f -> f.Instruction = "MPS")) |> Array.ofSeq
            let mpp = lstRung |> Seq.collect (Seq.filter (fun f -> f.Instruction = "MPP")) |> Array.ofSeq
            mps.Length = mpp.Length
            
        let mergeRungs =
            pou.Rungs |> Seq.filter isMergeBranch |> Seq.map updateRung |> Array.ofSeq
        { Name = pou.Name; Rungs = mergeRungs }

    // Load 명령어들을 병합하는 함수
    let mergeLoadRungs (pou: POUParseResult) =
        let lstRung = ResizeArray<Rung>()
        let updateRung (rung: Rung) =
            let newRung = lstRung |> Seq.rev |> Seq.collect id |> Array.ofSeq
            lstRung.Clear()
            newRung

        let isMergeLoad (rung: Rung) =
            lstRung.Add(rung)
            let firstIns = rung |> Seq.head |> fun f -> f.Instruction

            if ListExCMD.Contains(firstIns) || firstIns.StartsWith("P") then true
            else
                let load = lstRung |> Seq.collect (Seq.filter (fun f -> f.Instruction.Contains("LD"))) |> Array.ofSeq
                let loadOper = lstRung |> Seq.collect (Seq.filter (fun f -> f.Instruction = "ANB" || f.Instruction = "ORB")) |> Array.ofSeq
                load.Length = loadOper.Length + 1   

        let reverseRungs = pou.Rungs |> Seq.rev
        let mergeRungs = reverseRungs |> Seq.filter isMergeLoad |> Seq.map updateRung |> Seq.rev |> Array.ofSeq
        { Name = pou.Name; Rungs = mergeRungs }

    // 여러 CSV 파일을 구문 분석하여 주석과 POU 데이터로 반환
    let parseCSVs csvs =
        let isCommentCSV file =
            let lines = File.ReadAllLines(file)  
            lines.Length > 2  // 3번째 Row   POU Column header
            && (
                (lines[2].Contains("\t") && lines[2].Split('\t').Length <= 3)
             || (lines[2].Contains(",")  && lines[2].Split(',').Length <= 3 )
             )
        let isGlobalLabelCSV file =
            let lines = File.ReadAllLines(file)  
            lines.Length > 2 
            && (
                (lines[2].Contains("\t") && lines[2].Split('\t').Length > 7)
             || (lines[2].Contains(",")  && lines[2].Split(',').Length > 7)
             )

        let commentCSVs = csvs |> Seq.filter isCommentCSV
        let globalLabelCSVs = csvs |> Seq.filter isGlobalLabelCSV
        let pouCSVs = csvs |> Seq.except commentCSVs |> Seq.except globalLabelCSVs  
        let commentDic = readCommentCSV commentCSVs
        let globalLabelDic = readGlobalLabelCSV globalLabelCSVs
        let pous = pouCSVs |> Seq.map (fun csv -> extractRungsFromPOU csv commentDic |> mergeBranchRungs |> mergeLoadRungs) |> Array.ofSeq
        pous, commentDic, globalLabelDic

    [<AutoOpen>]
    module Print =
        // ProgramCSVLine의 인수를 문자열로 변환
        let ArgumentOfLine (line: ProgramCSVLine) = line.Arguments |> Seq.map (fun a -> a.ToText())
        
        // ProgramCSVLine의 인수 정보를 출력
        let printArgumentOfLine (line: ProgramCSVLine) = ArgumentOfLine line |> String.concat ";"
        
        // ProgramCSVLine의 명령어와 인수를 튜플로 출력
        let printArgumentNCmdOfLine (line: ProgramCSVLine) = (ArgumentOfLine line, line.Instruction)
        
        // ProgramCSVLine의 단계 번호와 명령어 출력
        let printLine (line: ProgramCSVLine) =
            match line.StepNo with
            | Some stepNo -> sprintf "IL:%d %s %s" stepNo line.Instruction (printArgumentOfLine line)
            | None -> sprintf "IL:%s %s" line.Instruction (printArgumentOfLine line)

        // Rung 전체를 문자열로 출력
        let printRung (r: Rung) = r |> Seq.map printLine |> String.concat "\r\n"
