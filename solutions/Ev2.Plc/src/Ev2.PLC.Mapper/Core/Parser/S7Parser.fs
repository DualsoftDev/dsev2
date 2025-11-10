namespace Ev2.PLC.Mapper.Core.Parser

open System
open System.IO
open System.Xml.Linq
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// Siemens S7 TIA Portal 파일 파서
type S7Parser(logger: ILogger<S7Parser>) =

    /// XML Element에서 속성 값 추출
    let getAttributeValue (element: XElement) (name: string) =
        element.Attribute(XName.Get name)
        |> Option.ofObj
        |> Option.map (fun attr -> attr.Value)

    /// Network Element를 RawLogic으로 변환
    let parseNetworkElement (networkElement: XElement) : RawLogic option =
        try
            let networkNumber = getAttributeValue networkElement "Number" |> Option.defaultValue "0"
            let title = networkElement.Descendants(XName.Get "Title") |> Seq.tryHead
                        |> Option.bind (fun e -> e.Value |> Some)
            let comment = networkElement.Descendants(XName.Get "Comment") |> Seq.tryHead
                         |> Option.bind (fun e -> e.Value |> Some)

            // Get STL content
            let stlContent =
                networkElement.Descendants(XName.Get "STL")
                |> Seq.map (fun e -> e.Value)
                |> String.concat "\n"

            // Get LAD content
            let ladContent =
                networkElement.Descendants(XName.Get "LAD")
                |> Seq.map (fun e -> e.ToString())
                |> String.concat "\n"

            // Get SCL content
            let sclContent =
                networkElement.Descendants(XName.Get "SCL")
                |> Seq.map (fun e -> e.Value)
                |> String.concat "\n"

            // Combine all content
            let content =
                if not (String.IsNullOrWhiteSpace(stlContent)) then stlContent
                elif not (String.IsNullOrWhiteSpace(sclContent)) then sclContent
                elif not (String.IsNullOrWhiteSpace(ladContent)) then ladContent
                else networkElement.ToString()

            // Extract variables
            let variablePattern = @"\b([IQMDB][BWD]?\d+(?:\.\d+)?)\b"
            let variables =
                Regex.Matches(content, variablePattern)
                |> Seq.cast<Match>
                |> Seq.map (fun m -> m.Groups.[1].Value)
                |> Seq.distinct
                |> Seq.toList

            // Detect logic type
            let logicType =
                if not (String.IsNullOrWhiteSpace(stlContent)) then LogicType.STL
                elif not (String.IsNullOrWhiteSpace(sclContent)) then LogicType.SCL
                elif not (String.IsNullOrWhiteSpace(ladContent)) then LogicType.Ladder
                else LogicType.STL

            // Detect flow type
            let detectFlowType() =
                let contentLower = content.ToLower()
                if contentLower.Contains("safety") || contentLower.Contains("emergency") || contentLower.Contains("estop") then
                    Some LogicFlowType.Safety
                elif content.Contains("TON") || content.Contains("TOF") || content.Contains("TP") then
                    Some LogicFlowType.Timer
                elif content.Contains("CTU") || content.Contains("CTD") || content.Contains("CTUD") then
                    Some LogicFlowType.Counter
                elif content.Contains("ADD") || content.Contains("SUB") || content.Contains("MUL") || content.Contains("DIV") then
                    Some LogicFlowType.Math
                elif content.Contains("JMP") || content.Contains("JC") || content.Contains("CALL") then
                    Some LogicFlowType.Conditional
                elif content.Contains("IF") && content.Contains("THEN") then
                    Some LogicFlowType.Conditional
                elif content.Contains("P#") || content.Contains("FP") || content.Contains("FN") then
                    Some LogicFlowType.Interrupt
                else
                    Some LogicFlowType.Simple

            Some {
                Id = Some networkNumber
                Name = title |> Option.orElse comment
                Number = match Int32.TryParse(networkNumber) with | true, n -> n | _ -> 0
                Content = content
                RawContent = Some content
                LogicType = logicType
                Type = detectFlowType()
                Variables = variables
                Comments = [comment] |> List.choose id
                LineNumber = Some (match Int32.TryParse(networkNumber) with | true, n -> n | _ -> 0)
                Properties = Map.empty
                Comment = comment
            }
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse network element")
            None

    /// AWL/STL 파일 파싱
    let parseAwlFile (filePath: string) : RawLogic list =
        try
            if not (File.Exists(filePath)) then
                logger.LogError("AWL file not found: {FilePath}", filePath)
                []
            else
                let lines = File.ReadAllLines(filePath)
                let mutable networks = []
                let mutable currentNetwork = []
                let mutable networkNumber = 0
                let mutable networkTitle = ""
                let mutable networkComment = ""

                for line in lines do
                    let trimmedLine = line.Trim()

                    if trimmedLine.StartsWith("NETWORK") then
                        // Save previous network if exists
                        if currentNetwork.Length > 0 then
                            let content = String.concat "\n" (List.rev currentNetwork)
                            let rawLogic = {
                                Id = Some (networkNumber.ToString())
                                Name = if String.IsNullOrEmpty(networkTitle) then None else Some networkTitle
                                Number = networkNumber
                                Content = content
                                RawContent = Some content
                                LogicType = LogicType.STL
                                Type =
                                    let contentLower = content.ToLower()
                                    if contentLower.Contains("safety") || contentLower.Contains("emergency") then
                                        Some LogicFlowType.Safety
                                    elif content.Contains("T#") || content.Contains("TON") then
                                        Some LogicFlowType.Timer
                                    elif content.Contains("CTU") || content.Contains("CTD") then
                                        Some LogicFlowType.Counter
                                    else
                                        Some LogicFlowType.Simple
                                Variables =
                                    Regex.Matches(content, @"\b([IQMDB][BWD]?\d+(?:\.\d+)?)\b")
                                    |> Seq.cast<Match>
                                    |> Seq.map (fun m -> m.Groups.[1].Value)
                                    |> Seq.distinct
                                    |> Seq.toList
                                Comments = if String.IsNullOrEmpty(networkComment) then [] else [networkComment]
                                LineNumber = Some networkNumber
                                Properties = Map.empty
                                Comment = if String.IsNullOrEmpty(networkComment) then None else Some networkComment
                            }
                            networks <- rawLogic :: networks

                        // Start new network
                        networkNumber <- networkNumber + 1
                        currentNetwork <- []
                        networkTitle <- ""
                        networkComment <- ""

                    elif trimmedLine.StartsWith("TITLE") then
                        networkTitle <- trimmedLine.Substring(5).Trim().Trim('=', '"')

                    elif trimmedLine.StartsWith("//") then
                        if String.IsNullOrEmpty(networkComment) then
                            networkComment <- trimmedLine.Substring(2).Trim()
                        else
                            networkComment <- networkComment + " " + trimmedLine.Substring(2).Trim()

                    elif not (String.IsNullOrWhiteSpace(trimmedLine)) && not (trimmedLine.StartsWith("*")) then
                        currentNetwork <- trimmedLine :: currentNetwork

                // Add last network
                if currentNetwork.Length > 0 then
                    let content = String.concat "\n" (List.rev currentNetwork)
                    let rawLogic = {
                        Id = Some (networkNumber.ToString())
                        Name = if String.IsNullOrEmpty(networkTitle) then None else Some networkTitle
                        Number = networkNumber
                        Content = content
                        RawContent = Some content
                        LogicType = LogicType.STL
                        Type = Some LogicFlowType.Simple
                        Variables =
                            Regex.Matches(content, @"\b([IQMDB][BWD]?\d+(?:\.\d+)?)\b")
                            |> Seq.cast<Match>
                            |> Seq.map (fun m -> m.Groups.[1].Value)
                            |> Seq.distinct
                            |> Seq.toList
                        Comments = if String.IsNullOrEmpty(networkComment) then [] else [networkComment]
                        LineNumber = Some networkNumber
                        Properties = Map.empty
                        Comment = if String.IsNullOrEmpty(networkComment) then None else Some networkComment
                    }
                    networks <- rawLogic :: networks

                let result = List.rev networks
                logger.LogInformation("Parsed {Count} networks from AWL file: {FilePath}",
                                    result.Length, filePath)
                result
        with
        | ex ->
            logger.LogError(ex, "Error parsing AWL file: {FilePath}", filePath)
            []

    /// XML 파일 파싱 (TIA Portal export)
    let parseXmlFile (filePath: string) : RawLogic list =
        try
            if not (File.Exists(filePath)) then
                logger.LogError("XML file not found: {FilePath}", filePath)
                []
            else
                let doc = XDocument.Load(filePath)
                let root = doc.Root

                // Find all Network elements
                let networks =
                    root.Descendants(XName.Get "Network")
                    |> Seq.choose parseNetworkElement
                    |> Seq.toList

                // If no networks, try Segment elements
                let segments =
                    if networks.IsEmpty then
                        root.Descendants(XName.Get "Segment")
                        |> Seq.choose parseNetworkElement
                        |> Seq.toList
                    else
                        []

                let allNetworks = networks @ segments

                logger.LogInformation("Parsed {Count} networks from XML file: {FilePath}",
                                     allNetworks.Length, filePath)
                allNetworks
        with
        | ex ->
            logger.LogError(ex, "Error parsing XML file: {FilePath}", filePath)
            []

    /// SCL 파일 파싱
    let parseSclFile (filePath: string) : RawLogic list =
        try
            if not (File.Exists(filePath)) then
                logger.LogError("SCL file not found: {FilePath}", filePath)
                []
            else
                let content = File.ReadAllText(filePath)
                let mutable blocks = []
                let mutable blockNumber = 0

                // Split by function/function block definitions
                let functionPattern = @"(?:FUNCTION|FUNCTION_BLOCK|ORGANIZATION_BLOCK)\s+(\w+)"
                let matches = Regex.Matches(content, functionPattern)

                if matches.Count > 0 then
                    for i in 0 .. matches.Count - 1 do
                        let startIndex = matches.[i].Index
                        let endIndex =
                            if i < matches.Count - 1 then
                                matches.[i + 1].Index - 1
                            else
                                content.Length - 1

                        let blockContent = content.Substring(startIndex, endIndex - startIndex + 1)
                        blockNumber <- blockNumber + 1

                        let rawLogic = {
                            Id = Some (blockNumber.ToString())
                            Name = Some matches.[i].Groups.[1].Value
                            Number = blockNumber
                            Content = blockContent
                            RawContent = Some blockContent
                            LogicType = LogicType.SCL
                            Type =
                                if blockContent.ToLower().Contains("safety") then
                                    Some LogicFlowType.Safety
                                elif blockContent.Contains("TIME#") then
                                    Some LogicFlowType.Timer
                                else
                                    Some LogicFlowType.Sequential
                            Variables =
                                Regex.Matches(blockContent, @"#(\w+)")
                                |> Seq.cast<Match>
                                |> Seq.map (fun m -> m.Groups.[1].Value)
                                |> Seq.distinct
                                |> Seq.toList
                            Comments = []
                            LineNumber = Some blockNumber
                            Properties = Map.empty
                            Comment = None
                        }
                        blocks <- rawLogic :: blocks
                else
                    // Treat entire file as single block
                    let rawLogic = {
                        Id = Some "1"
                        Name = Some (Path.GetFileNameWithoutExtension(filePath))
                        Number = 1
                        Content = content
                        RawContent = Some content
                        LogicType = LogicType.SCL
                        Type = Some LogicFlowType.Sequential
                        Variables =
                            Regex.Matches(content, @"#(\w+)")
                            |> Seq.cast<Match>
                            |> Seq.map (fun m -> m.Groups.[1].Value)
                            |> Seq.distinct
                            |> Seq.toList
                        Comments = []
                        LineNumber = Some 1
                        Properties = Map.empty
                        Comment = None
                    }
                    blocks <- [rawLogic]

                let result = List.rev blocks
                logger.LogInformation("Parsed {Count} blocks from SCL file: {FilePath}",
                                    result.Length, filePath)
                result
        with
        | ex ->
            logger.LogError(ex, "Error parsing SCL file: {FilePath}", filePath)
            []

    /// Interface implementation
    interface IPlcParser with
        member this.ParseFileAsync(filePath: string) = async {
            let ext = Path.GetExtension(filePath).ToUpperInvariant()
            match ext with
            | ".AWL" | ".STL" -> return parseAwlFile filePath
            | ".SCL" -> return parseSclFile filePath
            | ".XML" -> return parseXmlFile filePath
            | _ ->
                logger.LogWarning("Unsupported file extension: {Extension}", ext)
                return []
        }

        member this.ParseDirectoryAsync(directoryPath: string) = async {
            if not (Directory.Exists(directoryPath)) then
                logger.LogError("Directory not found: {DirectoryPath}", directoryPath)
                return []
            else
                // Parse all supported files
                let awlFiles = Directory.GetFiles(directoryPath, "*.awl", SearchOption.AllDirectories)
                let stlFiles = Directory.GetFiles(directoryPath, "*.stl", SearchOption.AllDirectories)
                let sclFiles = Directory.GetFiles(directoryPath, "*.scl", SearchOption.AllDirectories)
                let xmlFiles = Directory.GetFiles(directoryPath, "*.xml", SearchOption.AllDirectories)

                let allFiles = Array.concat [awlFiles; stlFiles; sclFiles; xmlFiles]

                let allNetworks =
                    allFiles
                    |> Array.collect (fun file ->
                        logger.LogInformation("Parsing file: {FilePath}", file)
                        let ext = Path.GetExtension(file).ToUpperInvariant()
                        let networks =
                            match ext with
                            | ".AWL" | ".STL" -> parseAwlFile file
                            | ".SCL" -> parseSclFile file
                            | ".XML" -> parseXmlFile file
                            | _ -> []
                        networks |> List.toArray)
                    |> Array.toList

                logger.LogInformation("Parsed {Count} networks from {FileCount} files",
                                      allNetworks.Length, allFiles.Length)
                return allNetworks
        }

        member this.ParseContentAsync(content: string) = async {
            // Determine format by content inspection
            let ext =
                if content.Contains("<Network") || content.Contains("<Segment") then ".xml"
                elif content.Contains("FUNCTION") || content.Contains("IF") && content.Contains("THEN") then ".scl"
                else ".awl"

            let tempFile = Path.GetTempFileName() + ext
            try
                File.WriteAllText(tempFile, content)
                match ext with
                | ".xml" -> return parseXmlFile tempFile
                | ".scl" -> return parseSclFile tempFile
                | _ -> return parseAwlFile tempFile
            finally
                if File.Exists(tempFile) then
                    File.Delete(tempFile)
        }

        member this.SupportedFileExtensions = [".awl"; ".AWL"; ".stl"; ".STL"; ".scl"; ".SCL"; ".xml"; ".XML"]

        member this.CanParse(filePath: string) =
            let ext = Path.GetExtension(filePath).ToUpperInvariant()
            ext = ".AWL" || ext = ".STL" || ext = ".SCL" || ext = ".XML"

/// S7 Parser Factory
module S7ParserFactory =
    let create (logger: ILogger<S7Parser>) : IPlcParser =
        S7Parser(logger) :> IPlcParser