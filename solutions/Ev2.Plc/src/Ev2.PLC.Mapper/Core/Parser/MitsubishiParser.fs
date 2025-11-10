namespace Ev2.PLC.Mapper.Core.Parser

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// CSV 레코드 타입
type CsvRecord = {
    Step: string option
    Instruction: string
    Device: string option
    Comment: string option
    RawLine: string
}

/// Mitsubishi GxWorks CSV 파일 파서
type MitsubishiParser(logger: ILogger<MitsubishiParser>) =

    /// CSV 라인 파싱
    let parseCsvLine (line: string) : CsvRecord option =
        try
            // Handle different CSV formats (comma or tab separated)
            let separator = if line.Contains("\t") then '\t' else ','
            let parts = line.Split(separator)

            if parts.Length >= 2 then
                let instruction = parts.[1].Trim().Trim('"')
                Some {
                    Step = if parts.Length > 0 then Some (parts.[0].Trim().Trim('"')) else None
                    Instruction = instruction
                    Device = if parts.Length > 2 then Some (parts.[2].Trim().Trim('"')) else None
                    Comment = if parts.Length > 3 then Some (parts.[3].Trim().Trim('"')) else None
                    RawLine = line
                }
            else
                None
        with
        | ex ->
            logger.LogDebug("Failed to parse CSV line: {Line}", line)
            None

    /// 명령어 그룹을 RawLogic으로 변환
    let convertToRawLogic (rungNumber: int) (records: CsvRecord list) : RawLogic =
        // Combine all instructions
        let content =
            records
            |> List.map (fun r -> sprintf "%s %s" r.Instruction (r.Device |> Option.defaultValue ""))
            |> String.concat " "

        // Extract variables
        let variablePattern = @"\b([XYMDRTCSKHFBWxymdrtkshfbw]\d+(?:\.\d+)?)\b"
        let variables =
            records
            |> List.collect (fun r ->
                match r.Device with
                | Some device ->
                    Regex.Matches(device, variablePattern)
                    |> Seq.cast<Match>
                    |> Seq.map (fun m -> m.Groups.[1].Value)
                    |> Seq.toList
                | None -> [])
            |> List.distinct

        // Extract comments
        let comments =
            records
            |> List.choose (fun r -> r.Comment)
            |> List.filter (fun c -> not (String.IsNullOrWhiteSpace(c)))

        // Detect logic type
        let logicType =
            if records |> List.exists (fun r ->
                r.Instruction.StartsWith("LD") || r.Instruction.StartsWith("AND") ||
                r.Instruction.StartsWith("OR") || r.Instruction = "OUT") then
                LogicType.InstructionList
            else
                LogicType.FunctionBlock

        // Detect flow type
        let detectFlowType() =
            let hasInstruction inst =
                records |> List.exists (fun r -> r.Instruction.Contains(inst))

            if comments |> List.exists (fun c -> c.ToLower().Contains("safety") || c.ToLower().Contains("emergency")) then
                Some LogicFlowType.Safety
            elif hasInstruction "TON" || hasInstruction "TOF" || hasInstruction "TP" then
                Some LogicFlowType.Timer
            elif hasInstruction "CTU" || hasInstruction "CTD" || hasInstruction "CTUD" then
                Some LogicFlowType.Counter
            elif hasInstruction "ADD" || hasInstruction "SUB" || hasInstruction "MUL" || hasInstruction "DIV" then
                Some LogicFlowType.Math
            elif hasInstruction "CJ" || hasInstruction "JMP" || hasInstruction "CALL" then
                Some LogicFlowType.Conditional
            elif hasInstruction "PLS" || hasInstruction "PLF" then
                Some LogicFlowType.Interrupt
            elif hasInstruction "MOV" || hasInstruction "BMOV" then
                Some LogicFlowType.Sequential
            else
                Some LogicFlowType.Simple

        {
            Id = Some (rungNumber.ToString())
            Name = comments |> List.tryHead
            Number = rungNumber
            Content = content
            RawContent = Some content
            LogicType = logicType
            Type = detectFlowType()
            Variables = variables
            Comments = comments
            LineNumber = Some rungNumber
            Properties = Map.empty
            Comment = comments |> List.tryHead
        }

    /// CSV 파일 파싱
    let parseCsvFile (filePath: string) : RawLogic list =
        try
            if not (File.Exists(filePath)) then
                logger.LogError("CSV file not found: {FilePath}", filePath)
                []
            else
                // Detect encoding
                let encoding =
                    try
                        use reader = new StreamReader(filePath, Encoding.GetEncoding("Shift_JIS"), true)
                        let _ = reader.Peek()
                        reader.CurrentEncoding
                    with
                    | _ -> Encoding.UTF8

                let lines = File.ReadAllLines(filePath, encoding)

                // Skip header if present
                let dataLines =
                    if lines.Length > 0 && (lines.[0].Contains("Step") || lines.[0].Contains("Instruction")) then
                        lines |> Array.skip 1
                    else
                        lines

                // Parse CSV records
                let records =
                    dataLines
                    |> Array.choose parseCsvLine
                    |> Array.toList

                // Group records into logical blocks (rungs)
                let mutable rungs = []
                let mutable currentRung = []
                let mutable rungNumber = 0

                for record in records do
                    match record.Instruction.ToUpper() with
                    | "LD" | "LDI" when currentRung.Length > 0 ->
                        // Start of new rung
                        if currentRung.Length > 0 then
                            rungNumber <- rungNumber + 1
                            let rawLogic = convertToRawLogic rungNumber (List.rev currentRung)
                            rungs <- rawLogic :: rungs
                            currentRung <- [record]
                        else
                            currentRung <- [record]
                    | "END" | "FEND" ->
                        // End of program or function
                        if currentRung.Length > 0 then
                            rungNumber <- rungNumber + 1
                            let rawLogic = convertToRawLogic rungNumber (List.rev currentRung)
                            rungs <- rawLogic :: rungs
                            currentRung <- []
                    | _ ->
                        currentRung <- record :: currentRung

                // Add last rung if exists
                if currentRung.Length > 0 then
                    rungNumber <- rungNumber + 1
                    let rawLogic = convertToRawLogic rungNumber (List.rev currentRung)
                    rungs <- rawLogic :: rungs

                let result = List.rev rungs
                logger.LogInformation("Parsed {Count} rungs from CSV file: {FilePath}",
                                    result.Length, filePath)
                result
        with
        | ex ->
            logger.LogError(ex, "Error parsing CSV file: {FilePath}", filePath)
            []

    /// AWL (Instruction List) 파일 파싱
    let parseAwlFile (filePath: string) : RawLogic list =
        try
            if not (File.Exists(filePath)) then
                logger.LogError("AWL file not found: {FilePath}", filePath)
                []
            else
                let lines = File.ReadAllLines(filePath)
                let mutable rungs = []
                let mutable currentInstructions = []
                let mutable rungNumber = 0
                let mutable currentComment = ""

                for line in lines do
                    let trimmedLine = line.Trim()

                    // Skip empty lines
                    if String.IsNullOrWhiteSpace(trimmedLine) then
                        ()
                    // Comment line
                    elif trimmedLine.StartsWith("//") || trimmedLine.StartsWith("(*") then
                        currentComment <- trimmedLine.Substring(2).Trim()
                    // Instruction line
                    elif not (trimmedLine.StartsWith("*")) then
                        let parts = trimmedLine.Split([|' '; '\t'|], StringSplitOptions.RemoveEmptyEntries)
                        if parts.Length > 0 then
                            let instruction = parts.[0]
                            let device = if parts.Length > 1 then Some parts.[1] else None
                            let comment = if not (String.IsNullOrEmpty(currentComment)) then Some currentComment else None

                            let record = {
                                Step = None
                                Instruction = instruction
                                Device = device
                                Comment = comment
                                RawLine = trimmedLine
                            }

                            // Check if this starts a new rung
                            if (instruction = "LD" || instruction = "LDI") && currentInstructions.Length > 0 then
                                // Save current rung
                                rungNumber <- rungNumber + 1
                                let rawLogic = convertToRawLogic rungNumber (List.rev currentInstructions)
                                rungs <- rawLogic :: rungs
                                currentInstructions <- [record]
                            else
                                currentInstructions <- record :: currentInstructions

                            currentComment <- ""

                // Add last rung
                if currentInstructions.Length > 0 then
                    rungNumber <- rungNumber + 1
                    let rawLogic = convertToRawLogic rungNumber (List.rev currentInstructions)
                    rungs <- rawLogic :: rungs

                let result = List.rev rungs
                logger.LogInformation("Parsed {Count} rungs from AWL file: {FilePath}",
                                    result.Length, filePath)
                result
        with
        | ex ->
            logger.LogError(ex, "Error parsing AWL file: {FilePath}", filePath)
            []

    /// Interface implementation
    interface IPlcParser with
        member this.ParseFileAsync(filePath: string) = async {
            let ext = Path.GetExtension(filePath).ToUpperInvariant()
            if ext = ".CSV" then
                return parseCsvFile filePath
            elif ext = ".AWL" || ext = ".IL" then
                return parseAwlFile filePath
            else
                logger.LogWarning("Unsupported file extension: {Extension}", ext)
                return []
        }

        member this.ParseDirectoryAsync(directoryPath: string) = async {
            if not (Directory.Exists(directoryPath)) then
                logger.LogError("Directory not found: {DirectoryPath}", directoryPath)
                return []
            else
                // Parse all CSV and AWL files
                let csvFiles = Directory.GetFiles(directoryPath, "*.csv", SearchOption.AllDirectories)
                let awlFiles = Directory.GetFiles(directoryPath, "*.awl", SearchOption.AllDirectories)
                let ilFiles = Directory.GetFiles(directoryPath, "*.il", SearchOption.AllDirectories)

                let allFiles = Array.concat [csvFiles; awlFiles; ilFiles]

                let allRungs =
                    allFiles
                    |> Array.collect (fun file ->
                        logger.LogInformation("Parsing file: {FilePath}", file)
                        let ext = Path.GetExtension(file).ToUpperInvariant()
                        let rungs =
                            if ext = ".CSV" then
                                parseCsvFile file
                            else
                                parseAwlFile file
                        rungs |> List.toArray)
                    |> Array.toList

                logger.LogInformation("Parsed {Count} rungs from {FileCount} files",
                                      allRungs.Length, allFiles.Length)
                return allRungs
        }

        member this.ParseContentAsync(content: string) = async {
            // Determine format by content inspection
            let isCsv = content.Contains(",") || content.Contains("\t")
            let ext = if isCsv then ".csv" else ".awl"

            let tempFile = Path.GetTempFileName() + ext
            try
                File.WriteAllText(tempFile, content)
                if isCsv then
                    return parseCsvFile tempFile
                else
                    return parseAwlFile tempFile
            finally
                if File.Exists(tempFile) then
                    File.Delete(tempFile)
        }

        member this.SupportedFileExtensions = [".csv"; ".CSV"; ".awl"; ".AWL"; ".il"; ".IL"]

        member this.CanParse(filePath: string) =
            let ext = Path.GetExtension(filePath).ToUpperInvariant()
            ext = ".CSV" || ext = ".AWL" || ext = ".IL"

/// Mitsubishi Parser Factory
module MitsubishiParserFactory =
    let create (logger: ILogger<MitsubishiParser>) : IPlcParser =
        MitsubishiParser(logger) :> IPlcParser