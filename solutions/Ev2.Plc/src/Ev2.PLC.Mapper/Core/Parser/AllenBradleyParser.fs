namespace Ev2.PLC.Mapper.Core.Parser

open System
open System.IO
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// L5K 파일 섹션
type L5KSection =
    | Controller
    | DataTypes
    | Modules
    | TagsSection
    | Programs
    | Routines
    | Rungs
    | Unknown of string

/// Allen-Bradley L5K 파일 파서
type AllenBradleyParser(logger: ILogger<AllenBradleyParser>) =

    /// 현재 파싱 중인 섹션 감지
    let detectSection (line: string) : L5KSection option =
        if line.StartsWith("CONTROLLER") then Some Controller
        elif line.StartsWith("DATATYPE") then Some DataTypes
        elif line.StartsWith("MODULE") then Some Modules
        elif line.StartsWith("TAG") then Some TagsSection
        elif line.StartsWith("PROGRAM") then Some Programs
        elif line.StartsWith("ROUTINE") then Some Routines
        elif line.Contains("RUNG") then Some Rungs
        elif line.StartsWith("END_") then None
        else None

    /// Rung 텍스트에서 RawLogic 추출
    let parseRung (rungNumber: int) (rungText: string) : RawLogic =
        // Extract variables using regex
        let variablePattern = @"\b([A-Za-z_]\w*(?:\[\d+\])?(?:\.\w+)*)\b"
        let variables =
            Regex.Matches(rungText, variablePattern)
            |> Seq.cast<Match>
            |> Seq.map (fun m -> m.Groups.[1].Value)
            |> Seq.filter (fun v ->
                // Filter out keywords
                not (v.StartsWith("XIC") || v.StartsWith("XIO") ||
                     v.StartsWith("OTE") || v.StartsWith("OTL") ||
                     v.StartsWith("OTU") || v.StartsWith("MOV") ||
                     v.StartsWith("ADD") || v.StartsWith("SUB") ||
                     v.StartsWith("MUL") || v.StartsWith("DIV") ||
                     v.StartsWith("EQU") || v.StartsWith("NEQ") ||
                     v.StartsWith("GRT") || v.StartsWith("LES") ||
                     v.StartsWith("GEQ") || v.StartsWith("LEQ") ||
                     v.StartsWith("TON") || v.StartsWith("TOF") ||
                     v.StartsWith("CTU") || v.StartsWith("CTD") ||
                     v.StartsWith("JMP") || v.StartsWith("LBL") ||
                     v.StartsWith("JSR") || v.StartsWith("RET")))
            |> Seq.distinct
            |> Seq.toList

        // Extract comments
        let commentPattern = @"(?:\/\/|\/\*|\(\*)(.+?)(?:\n|\*\/|\*\))"
        let comments =
            Regex.Matches(rungText, commentPattern)
            |> Seq.cast<Match>
            |> Seq.map (fun m -> m.Groups.[1].Value.Trim())
            |> Seq.toList

        // Detect logic type
        let logicType =
            if rungText.Contains("XIC") || rungText.Contains("XIO") || rungText.Contains("OTE") then
                LogicType.LadderRung
            elif rungText.Contains("IF") || rungText.Contains("THEN") || rungText.Contains("ELSE") then
                LogicType.StructuredText
            elif rungText.Contains("MOV") || rungText.Contains("ADD") || rungText.Contains("SUB") then
                LogicType.FunctionBlock
            else
                LogicType.LadderRung

        // Detect flow type
        let detectFlowType() =
            if Regex.IsMatch(rungText, @"\b(ESTOP|SAFETY|EMERGENCY|ALARM)\b", RegexOptions.IgnoreCase) then
                Some LogicFlowType.Safety
            elif rungText.Contains("TON") || rungText.Contains("TOF") || rungText.Contains("RTO") then
                Some LogicFlowType.Timer
            elif rungText.Contains("CTU") || rungText.Contains("CTD") || rungText.Contains("RES") then
                Some LogicFlowType.Counter
            elif rungText.Contains("ADD") || rungText.Contains("SUB") || rungText.Contains("MUL") || rungText.Contains("DIV") then
                Some LogicFlowType.Math
            elif rungText.Contains("JMP") || rungText.Contains("LBL") || rungText.Contains("JSR") then
                Some LogicFlowType.Conditional
            elif rungText.Contains("SEQ") || rungText.Contains("SQO") || rungText.Contains("SQI") then
                Some LogicFlowType.Sequence
            elif rungText.Contains("ONS") || rungText.Contains("OSR") || rungText.Contains("OSF") then
                Some LogicFlowType.Interrupt
            else
                Some LogicFlowType.Simple

        {
            Id = Some (rungNumber.ToString())
            Name = comments |> List.tryHead
            Number = rungNumber
            Content = rungText
            RawContent = Some rungText
            LogicType = logicType
            Type = detectFlowType()
            Variables = variables
            Comments = comments
            LineNumber = Some rungNumber
            Properties = Map.empty
            Comment = comments |> List.tryHead
        }

    /// L5K 파일 파싱
    let parseL5KFile (filePath: string) : RawLogic list =
        try
            if not (File.Exists(filePath)) then
                logger.LogError("L5K file not found: {FilePath}", filePath)
                []
            else
                let lines = File.ReadAllLines(filePath)
                let mutable currentSection = None
                let mutable currentRoutine = ""
                let mutable rungNumber = 0
                let mutable rungContent = ""
                let mutable inRung = false
                let mutable rungs = []

                for line in lines do
                    let trimmedLine = line.Trim()

                    // Section detection
                    match detectSection trimmedLine with
                    | Some section ->
                        currentSection <- Some section
                        if section = Routines then
                            let routineMatch = Regex.Match(trimmedLine, @"ROUTINE\s+(\w+)")
                            if routineMatch.Success then
                                currentRoutine <- routineMatch.Groups.[1].Value
                    | None -> ()

                    // Rung parsing
                    if currentSection = Some Routines || currentSection = Some Rungs then
                        if trimmedLine.StartsWith("RUNG") then
                            // Save previous rung if exists
                            if inRung && not (String.IsNullOrWhiteSpace(rungContent)) then
                                let rawLogic = parseRung rungNumber rungContent
                                rungs <- rawLogic :: rungs

                            // Start new rung
                            inRung <- true
                            rungNumber <- rungNumber + 1
                            rungContent <- ""
                        elif inRung then
                            if trimmedLine.StartsWith("END_RUNG") then
                                // Save current rung
                                if not (String.IsNullOrWhiteSpace(rungContent)) then
                                    let rawLogic = parseRung rungNumber rungContent
                                    rungs <- rawLogic :: rungs
                                inRung <- false
                                rungContent <- ""
                            else
                                // Accumulate rung content
                                rungContent <- rungContent + " " + trimmedLine

                // Return rungs in correct order
                List.rev rungs
        with
        | ex ->
            logger.LogError(ex, "Error parsing L5K file: {FilePath}", filePath)
            []

    /// CSV 형식의 태그 파일 파싱
    let parseTagsCsv (filePath: string) : Map<string, string> =
        try
            if not (File.Exists(filePath)) then
                Map.empty
            else
                let lines = File.ReadAllLines(filePath) |> Array.skip 1 // Skip header
                lines
                |> Array.map (fun line ->
                    let parts = line.Split(',')
                    if parts.Length >= 2 then
                        Some (parts.[0].Trim(), parts.[1].Trim())
                    else
                        None)
                |> Array.choose id
                |> Map.ofArray
        with
        | ex ->
            logger.LogError(ex, "Error parsing tags CSV: {FilePath}", filePath)
            Map.empty

    /// Interface implementation
    interface IPlcParser with
        member this.ParseFileAsync(filePath: string) = async {
            return parseL5KFile filePath
        }

        member this.ParseDirectoryAsync(directoryPath: string) = async {
            if not (Directory.Exists(directoryPath)) then
                logger.LogError("Directory not found: {DirectoryPath}", directoryPath)
                return []
            else
                let l5kFiles = Directory.GetFiles(directoryPath, "*.L5K", SearchOption.AllDirectories)
                let allRungs =
                    l5kFiles
                    |> Array.collect (fun file ->
                        logger.LogInformation("Parsing L5K file: {FilePath}", file)
                        parseL5KFile file |> List.toArray)
                    |> Array.toList

                logger.LogInformation("Parsed {Count} rungs from {FileCount} L5K files",
                                      allRungs.Length, l5kFiles.Length)
                return allRungs
        }

        member this.ParseContentAsync(content: string) = async {
            // Create temp file
            let tempFile = Path.GetTempFileName()
            try
                File.WriteAllText(tempFile, content)
                return parseL5KFile tempFile
            finally
                if File.Exists(tempFile) then
                    File.Delete(tempFile)
        }

        member this.SupportedFileExtensions = [".L5K"; ".l5k"; ".L5X"; ".l5x"]

        member this.CanParse(filePath: string) =
            let ext = Path.GetExtension(filePath).ToUpperInvariant()
            ext = ".L5K" || ext = ".L5X"

/// Allen-Bradley Parser Factory
module AllenBradleyParserFactory =
    let create (logger: ILogger<AllenBradleyParser>) : IPlcParser =
        AllenBradleyParser(logger) :> IPlcParser