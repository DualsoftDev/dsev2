namespace Ev2.PLC.Mapper.Core.Parser

open System
open System.IO
open System.Xml.Linq
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// LS Electric XG5000 XML 파일 파서
type LSElectricParser(logger: ILogger<LSElectricParser>) =

    /// XML Element에서 속성 값 추출
    let getAttributeValue (element: XElement) (name: string) =
        element.Attribute(XName.Get name)
        |> Option.ofObj
        |> Option.map (fun attr -> attr.Value)

    /// Rung Element를 RawLogic으로 변환
    let parseRungElement (rungElement: XElement) : RawLogic option =
        try
            let id = getAttributeValue rungElement "Number" |> Option.defaultValue (Guid.NewGuid().ToString())
            let content = rungElement.ToString()

            // Extract variables using regex
            let variablePattern = @"Variable=""([^""]+)"""
            let variables =
                Regex.Matches(content, variablePattern)
                |> Seq.cast<Match>
                |> Seq.map (fun m -> m.Groups.[1].Value)
                |> Seq.distinct
                |> Seq.toList

            // Extract comments
            let commentPattern = @"Comment=""([^""]+)"""
            let comments =
                Regex.Matches(content, commentPattern)
                |> Seq.cast<Match>
                |> Seq.map (fun m -> m.Groups.[1].Value)
                |> Seq.toList

            // Get rung name/description
            let name = getAttributeValue rungElement "Name"
            let desc = getAttributeValue rungElement "Description"

            // Detect logic type
            let logicType =
                if content.Contains("<Element") then LogicType.LadderRung
                elif content.Contains("IF") || content.Contains("THEN") then LogicType.StructuredText
                else LogicType.LadderRung

            // Detect flow type based on content
            let detectFlowType() =
                let contentLower = content.ToLower()
                if contentLower.Contains("safety") || contentLower.Contains("emergency") || contentLower.Contains("estop") then
                    Some LogicFlowType.Safety
                elif contentLower.Contains("timer") || content.Contains("TON") || content.Contains("TOF") || content.Contains("TP") then
                    Some LogicFlowType.Timer
                elif contentLower.Contains("counter") || content.Contains("CTU") || content.Contains("CTD") || content.Contains("CTUD") then
                    Some LogicFlowType.Counter
                elif content.Contains("JMP") || content.Contains("CALL") || content.Contains("SCALL") then
                    Some LogicFlowType.Conditional
                elif content.Contains("ADD") || content.Contains("SUB") || content.Contains("MUL") || content.Contains("DIV") then
                    Some LogicFlowType.Math
                elif content.Contains("SEQ") then
                    Some LogicFlowType.Sequence
                elif content.Contains("Type=\"72\"") || content.Contains("Type=\"73\"") then // Rising/Falling edge
                    Some LogicFlowType.Interrupt
                else
                    Some LogicFlowType.Simple

            Some {
                Id = Some id
                Name = name |> Option.orElse desc
                Number = match Int32.TryParse(id) with | true, n -> n | _ -> 0
                Content = content
                RawContent = Some content
                LogicType = logicType
                Type = detectFlowType()
                Variables = variables
                Comments = comments
                LineNumber = getAttributeValue rungElement "Line" |> Option.bind (fun v -> match Int32.TryParse(v) with | true, n -> Some n | _ -> None)
                Properties = Map.empty
                Comment = comments |> List.tryHead
            }
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse rung element")
            None

    /// Program Element 파싱
    let parseProgramElement (programElement: XElement) : RawLogic list =
        try
            let programName = getAttributeValue programElement "Name" |> Option.defaultValue "Unknown"
            logger.LogDebug("Parsing program: {ProgramName}", programName)

            // Find all Rung elements
            let rungs =
                programElement.Descendants(XName.Get "Rung")
                |> Seq.choose parseRungElement
                |> Seq.toList

            // If no Rung elements, try Network elements
            let networks =
                if rungs.IsEmpty then
                    programElement.Descendants(XName.Get "Network")
                    |> Seq.choose parseRungElement
                    |> Seq.toList
                else
                    []

            rungs @ networks
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse program element")
            []

    /// XML 파일 파싱
    let parseXmlFile (filePath: string) : RawLogic list =
        try
            if not (File.Exists(filePath)) then
                logger.LogError("XML file not found: {FilePath}", filePath)
                []
            else
                let doc = XDocument.Load(filePath)
                let root = doc.Root

                // Try to find Project root
                let projectRoot =
                    if root.Name.LocalName = "Project" then Some root
                    else root.Descendants(XName.Get "Project") |> Seq.tryHead

                match projectRoot with
                | Some project ->
                    // Find all Program elements
                    let programs = project.Descendants(XName.Get "Program")
                    let allRungs =
                        programs
                        |> Seq.collect parseProgramElement
                        |> Seq.toList

                    // If no programs found, try direct Rung elements
                    let directRungs =
                        if allRungs.IsEmpty then
                            project.Descendants(XName.Get "Rung")
                            |> Seq.choose parseRungElement
                            |> Seq.toList
                        else
                            []

                    let totalRungs = allRungs @ directRungs

                    logger.LogInformation("Parsed {Count} rungs from XML file: {FilePath}",
                                        totalRungs.Length, filePath)
                    totalRungs
                | None ->
                    // Try to parse direct Rung elements from root
                    let rungs =
                        root.Descendants(XName.Get "Rung")
                        |> Seq.choose parseRungElement
                        |> Seq.toList

                    if rungs.IsEmpty then
                        logger.LogWarning("No Project or Rung elements found in: {FilePath}", filePath)
                    else
                        logger.LogInformation("Parsed {Count} rungs from XML file: {FilePath}",
                                            rungs.Length, filePath)
                    rungs
        with
        | ex ->
            logger.LogError(ex, "Error parsing XML file: {FilePath}", filePath)
            []

    /// CSV 형식의 변수 파일 파싱
    let parseVariablesCsv (filePath: string) : Map<string, string> =
        try
            if not (File.Exists(filePath)) then
                Map.empty
            else
                let lines = File.ReadAllLines(filePath)
                // Check if first line is header
                let dataLines =
                    if lines.Length > 0 && lines.[0].Contains("Variable") then
                        lines |> Array.skip 1
                    else
                        lines

                dataLines
                |> Array.map (fun line ->
                    let parts = line.Split([|','; '\t'|])
                    if parts.Length >= 2 then
                        Some (parts.[0].Trim(), parts.[1].Trim())
                    else
                        None)
                |> Array.choose id
                |> Map.ofArray
        with
        | ex ->
            logger.LogError(ex, "Error parsing variables CSV: {FilePath}", filePath)
            Map.empty

    /// Interface implementation
    interface IPlcParser with
        member this.ParseFileAsync(filePath: string) = async {
            let ext = Path.GetExtension(filePath).ToUpperInvariant()
            if ext = ".XML" then
                return parseXmlFile filePath
            elif ext = ".CSV" then
                // CSV files usually contain variables, not logic
                logger.LogWarning("CSV file contains variables, not logic: {FilePath}", filePath)
                return []
            else
                logger.LogWarning("Unsupported file extension: {Extension}", ext)
                return []
        }

        member this.ParseDirectoryAsync(directoryPath: string) = async {
            if not (Directory.Exists(directoryPath)) then
                logger.LogError("Directory not found: {DirectoryPath}", directoryPath)
                return []
            else
                // Parse all XML files
                let xmlFiles = Directory.GetFiles(directoryPath, "*.xml", SearchOption.AllDirectories)
                let allRungs =
                    xmlFiles
                    |> Array.collect (fun file ->
                        logger.LogInformation("Parsing XML file: {FilePath}", file)
                        parseXmlFile file |> List.toArray)
                    |> Array.toList

                logger.LogInformation("Parsed {Count} rungs from {FileCount} XML files",
                                      allRungs.Length, xmlFiles.Length)
                return allRungs
        }

        member this.ParseContentAsync(content: string) = async {
            // Create temp file
            let tempFile = Path.GetTempFileName() + ".xml"
            try
                File.WriteAllText(tempFile, content)
                return parseXmlFile tempFile
            finally
                if File.Exists(tempFile) then
                    File.Delete(tempFile)
        }

        member this.SupportedFileExtensions = [".xml"; ".XML"; ".csv"; ".CSV"]

        member this.CanParse(filePath: string) =
            let ext = Path.GetExtension(filePath).ToUpperInvariant()
            ext = ".XML" || ext = ".CSV"

/// LS Electric Parser Factory
module LSElectricParserFactory =
    let create (logger: ILogger<LSElectricParser>) : IPlcParser =
        LSElectricParser(logger) :> IPlcParser