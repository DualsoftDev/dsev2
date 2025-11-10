namespace Ev2.PLC.Mapper.Parsers.LSElectric

open System
open System.IO
open System.Xml.Linq
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// LS Electric XML 파서 구현
type LSElectricParser(logger: ILogger<LSElectricParser>) =

    let getAttributeValue (element: XElement) (name: string) =
        element.Attribute(XName.Get name)
        |> Option.ofObj
        |> Option.map (fun attr -> attr.Value)

    let parseSymbol (symbolElement: XElement) : RawVariable =
        let name = getAttributeValue symbolElement "Name" |> Option.defaultValue ""
        let address = getAttributeValue symbolElement "Address" |> Option.defaultValue ""
        let dataType = getAttributeValue symbolElement "DataType" |> Option.defaultValue "BOOL"
        let comment = getAttributeValue symbolElement "Comment"
        let initialValue = getAttributeValue symbolElement "InitialValue"
        let scope = getAttributeValue symbolElement "Scope"

        {
            Name = name
            Address = address
            DataType = dataType
            Comment = comment
            InitialValue = initialValue
            Scope = scope
            AccessLevel = None
            Properties = Map.empty
        }

    let parseRung (rungElement: XElement) : RawLogic =
        let id = getAttributeValue rungElement "Number" |> Option.defaultValue (Guid.NewGuid().ToString())
        let content = rungElement.ToString()
        let lineNumber = 
            getAttributeValue rungElement "LineNumber"
            |> Option.bind (fun value -> match Int32.TryParse(value) with | true, n -> Some n | _ -> None)

        // Extract variable references from ladder logic
        let variablePattern = @"\b[A-Za-z_][A-Za-z0-9_]*\b"
        let variables = 
            Regex.Matches(content, variablePattern)
            |> Seq.cast<Match>
            |> Seq.map (fun (m: Match) -> m.Value)
            |> Seq.distinct
            |> Seq.toList
        
        {
            Id = id
            Type = LadderRung
            Content = content
            Variables = variables
            Comments = []
            LineNumber = lineNumber
            Properties = Map.empty
        }
    
    let extractProjectInfo (xmlDoc: XDocument) (filePath: string) : ProjectInfo =
        let root = xmlDoc.Root
        let projectElement = root.Descendants(XName.Get "Project") |> Seq.tryHead

        let name = 
            projectElement
            |> Option.bind (fun p -> getAttributeValue p "Name")
            |> Option.defaultValue (Path.GetFileNameWithoutExtension(filePath))

        let version = 
            projectElement
            |> Option.bind (fun p -> getAttributeValue p "Version")
            |> Option.defaultValue "1.0.0"

        let description = 
            projectElement
            |> Option.bind (fun p -> getAttributeValue p "Description")

        let format = LSElectricXML filePath

        {
            Name = name
            Version = version
            Vendor = PlcVendor.CreateLSElectric()
            Format = format
            CreatedDate = DateTime.UtcNow
            ModifiedDate = DateTime.UtcNow
            Description = description
            Author = None
            FilePath = filePath
            FileSize = if File.Exists(filePath) then FileInfo(filePath).Length else 0L
        }
    
    interface ILSElectricParser with
        member this.SupportedVendor = PlcVendor.CreateLSElectric()
        
        member this.SupportedFormats = [LSElectricXML ""]
        
        member this.CanParse(format: PlcProgramFormat) =
            match format with
            | LSElectricXML _ -> true
            | _ -> false
        
        member this.ParseAsync(filePath: string) : Task<RawPlcProgram> = task {
            try
                if not (File.Exists(filePath)) then
                    raise (FileNotFoundException($"File not found: {filePath}", filePath))

                let content = File.ReadAllText(filePath)
                let format = LSElectricXML filePath
                return! (this :> ILSElectricParser).ParseContentAsync(content, format)
            with
            | ex ->
                logger.LogError(ex, "Error parsing LS Electric file: {FilePath}", filePath)
                return raise ex
        }

        member this.ParseContentAsync(content: string, format: PlcProgramFormat) : Task<RawPlcProgram> = task {
            try
                let xmlDoc = XDocument.Parse(content)
                let filePath = 
                    match format with
                    | LSElectricXML path -> path
                    | _ -> "unknown.xml"

                let projectInfo = extractProjectInfo xmlDoc filePath

                // Extract symbols
                let variables = 
                    xmlDoc.Descendants(XName.Get "Symbol")
                    |> Seq.map parseSymbol
                    |> Seq.toList

                // Extract ladder rungs
                let logic = 
                    xmlDoc.Descendants(XName.Get "Rung")
                    |> Seq.map parseRung
                    |> Seq.toList

                // Extract comments
                let comments = 
                    xmlDoc.Descendants(XName.Get "Comment")
                    |> Seq.map (fun c -> {
                        Target = getAttributeValue c "Target" |> Option.defaultValue ""
                        Content = c.Value
                        Language = getAttributeValue c "Language"
                        Author = getAttributeValue c "Author"
                        CreatedDate = 
                            getAttributeValue c "Created"
                            |> Option.bind (fun dateStr -> match DateTime.TryParse(dateStr) with | true, date -> Some date | _ -> None)
                    })
                    |> Seq.toList
                
                let result = {
                    ProjectInfo = projectInfo
                    Variables = variables
                    Logic = logic
                    Comments = comments
                    Metadata = Map.empty
                }

                logger.LogInformation("Successfully parsed LS Electric file: {VariableCount} variables, {LogicCount} rungs", 
                                    variables.Length, logic.Length)

                return result
            with
            | ex ->
                logger.LogError(ex, "Error parsing LS Electric content")
                return raise ex
        }

        member this.ValidateFileAsync(filePath: string) : Task<ValidationResult> =
            task {
                try
                    if not (File.Exists(filePath)) then
                        return ValidationResult.Error($"File not found", filePath)
                    else
                        let extension = Path.GetExtension(filePath).ToLower()
                        if extension <> ".xml" then
                            return ValidationResult.Warning("Expected .xml file extension", filePath)
                        else
                            let content = File.ReadAllText(filePath)
                            let xmlDoc = XDocument.Parse(content)
                            // Check for LS Electric specific elements
                            let hasProject = xmlDoc.Descendants(XName.Get "Project") |> Seq.isEmpty |> not
                            let hasSymbols = xmlDoc.Descendants(XName.Get "Symbol") |> Seq.isEmpty |> not
                            if not hasProject then
                                return ValidationResult.Warning("No Project element found - may not be a valid LS Electric file", filePath)
                            elif not hasSymbols then
                                return ValidationResult.Warning("No Symbol elements found - empty project?", filePath)
                            else
                                return ValidationResult.Success
                with
                | ex ->
                    return ValidationResult.Error($"Validation failed: {ex.Message}", filePath)
            }
        
        member this.ParseXG5000ProjectAsync(projectPath: string) : Task<RawPlcProgram> = task {
            // XG5000 projects are typically directories with multiple XML files
            try
                if not (Directory.Exists(projectPath)) then
                    raise (DirectoryNotFoundException($"Project directory not found: {projectPath}"))

                let xmlFiles = Directory.GetFiles(projectPath, "*.xml", SearchOption.AllDirectories)
                if xmlFiles.Length = 0 then
                    raise (InvalidOperationException "No XML files found in project directory")

                // Find the main project file (usually the largest or has specific name patterns)
                let mainFile = 
                    xmlFiles 
                    |> Array.sortByDescending (fun f -> FileInfo(f).Length)
                    |> Array.head

                return! (this :> ILSElectricParser).ParseAsync(mainFile)
            with
            | ex ->
                logger.LogError(ex, "Error parsing XG5000 project: {ProjectPath}", projectPath)
                return raise ex
        }
        
        member this.ExtractSymbolTableAsync(xmlContent: string) = task {
            try
                let xmlDoc = XDocument.Parse(xmlContent)
                let variables = 
                    xmlDoc.Descendants("Symbol")
                    |> Seq.map parseSymbol
                    |> Seq.toList
                
                return variables
            with
            | ex ->
                logger.LogError(ex, "Error extracting symbol table")
                return []
        }
        
        member this.ExtractLadderLogicAsync(xmlContent: string) = task {
            try
                let xmlDoc = XDocument.Parse(xmlContent)
                let logic = 
                    xmlDoc.Descendants("Rung")
                    |> Seq.map parseRung
                    |> Seq.toList
                
                return logic
            with
            | ex ->
                logger.LogError(ex, "Error extracting ladder logic")
                return []
        }
        
        member this.ExtractProjectInfoAsync(xmlContent: string) = task {
            try
                let xmlDoc = XDocument.Parse(xmlContent)
                let projectInfo = extractProjectInfo xmlDoc "TestProject.xml"
                return projectInfo
            with
            | ex ->
                logger.LogError(ex, "Error extracting project info")
                return ProjectInfo.Create("Unknown", PlcVendor.CreateLSElectric(), LSElectricXML "", "TestProject.xml")
        }

/// LS Electric 파서 생성을 위한 팩토리
module LSElectricParserFactory =
    let create (logger: ILogger<LSElectricParser>) : ILSElectricParser =
        LSElectricParser(logger) :> ILSElectricParser
