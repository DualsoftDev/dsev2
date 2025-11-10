namespace Ev2.PLC.Mapper.Parsers.AllenBradley

open System
open System.IO
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// Allen-Bradley L5K 파서 구현
type AllenBradleyParser(logger: ILogger<AllenBradleyParser>) =
    
    let parseTagLine (line: string) : RawVariable option =
        // L5K TAG format: TAG <name> <controller/program> <datatype> <initialvalue> // <comment>
        let tagPattern = @"TAG\s+(\w+)\s+(Controller|Program:\w+)\s+(\w+(?:\[\d+\])?)\s*(.*)"
        let regexMatch = Regex.Match(line.Trim(), tagPattern)

        if regexMatch.Success then
            let name = regexMatch.Groups.[1].Value
            let scope = regexMatch.Groups.[2].Value
            let dataType = regexMatch.Groups.[3].Value
            let remainingData = regexMatch.Groups.[4].Value.Trim()
            
            // Parse initial value and comment from remaining data
            let (initialValue, comment) = 
                if remainingData.Contains("//") then
                    let parts = remainingData.Split([|"//"|], 2, StringSplitOptions.None)
                    let initVal = parts.[0].Trim()
                    let commentPart = if parts.Length > 1 then parts.[1].Trim() else ""
                    
                    let cleanInitVal = 
                        if initVal.StartsWith(":=") then initVal.Substring(2).Trim()
                        elif String.IsNullOrWhiteSpace(initVal) then ""
                        else initVal
                    
                    (if String.IsNullOrWhiteSpace(cleanInitVal) then None else Some cleanInitVal)
                    , (if String.IsNullOrWhiteSpace(commentPart) then None else Some commentPart)
                else
                    let cleanInitVal = 
                        if remainingData.StartsWith(":=") then remainingData.Substring(2).Trim()
                        elif String.IsNullOrWhiteSpace(remainingData) then ""
                        else remainingData
                    
                    ((if String.IsNullOrWhiteSpace(cleanInitVal) then None else Some cleanInitVal), None)
            
            Some {
                Name = name
                Address = "" // L5K uses tag names, not addresses
                DataType = dataType
                Comment = comment
                InitialValue = initialValue
                Scope = Some scope
                AccessLevel = None
                Properties = Map.empty
            }
        else
            None
    
    let parseRoutine (routineSection: string) : RawLogic =
        let lines = routineSection.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
        let routineName = 
            lines
            |> Array.tryFind (fun l -> l.StartsWith("ROUTINE "))
            |> Option.map (fun l -> l.Substring(8).Trim())
            |> Option.defaultValue "Unknown"
        
        // Extract ladder logic or structured text
        let content = String.Join("\n", lines)
        
        // Find variable references
        let variablePattern = @"\b[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*\b"
        let variables = 
            Regex.Matches(content, variablePattern)
            |> Seq.cast<Match>
            |> Seq.map (fun (m: Match) -> m.Value)
            |> Seq.distinct
            |> Seq.filter (fun v -> not (["ROUTINE"; "END_ROUTINE"; "RUNG"; "END_RUNG"] |> List.contains v))
            |> Seq.toList
        
        {
            Id = Some routineName
            Name = Some routineName
            Number = 0
            Content = content
            RawContent = Some content
            LogicType = if content.Contains("RUNG") then LogicType.LadderRung else LogicType.StructuredText
            Type = Some (if content.Contains("RUNG") then LogicFlowType.Simple else LogicFlowType.Sequential)
            Variables = variables
            Comments = []
            LineNumber = None
            Properties = Map.empty
            Comment = None
        }
    
    let extractProjectInfo (content: string) (filePath: string) : ProjectInfo =
        let lines = content.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
        
        let name = 
            lines
            |> Array.tryFind (fun l -> l.StartsWith("PROJECT "))
            |> Option.map (fun l -> l.Substring(8).Trim())
            |> Option.defaultValue (Path.GetFileNameWithoutExtension(filePath))
        
        let version = 
            lines
            |> Array.tryFind (fun l -> l.Contains("VERSION"))
            |> Option.bind (fun l -> 
                let versionMatch = Regex.Match(l, @"VERSION\s+(\d+\.\d+(?:\.\d+)?)")
                if versionMatch.Success then Some versionMatch.Groups.[1].Value else None)
            |> Option.defaultValue "1.0.0"
        
        {
            Name = name
            Version = version
            Vendor = PlcVendor.CreateAllenBradley()
            Format = AllenBradleyL5K filePath
            CreatedDate = DateTime.UtcNow
            ModifiedDate = DateTime.UtcNow
            Description = None
            Author = None
            FilePath = filePath
            FileSize = if File.Exists(filePath) then FileInfo(filePath).Length else 0L
        }
    
    let parseSections (content: string) : Map<string, string list> =
        let lines = content.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
        let mutable sections = Map.empty<string, string list>
        let mutable currentSection = ""
        let mutable currentLines = []
        
        for line in lines do
            let trimmedLine = line.Trim()
            if trimmedLine.StartsWith("TAG ") && currentSection <> "TAGS" then
                if currentSection <> "" then
                    sections <- sections.Add(currentSection, List.rev currentLines)
                currentSection <- "TAGS"
                currentLines <- [trimmedLine]
            elif trimmedLine.StartsWith("ROUTINE ") then
                if currentSection <> "" then
                    sections <- sections.Add(currentSection, List.rev currentLines)
                currentSection <- trimmedLine
                currentLines <- [trimmedLine]
            elif trimmedLine.StartsWith("END_ROUTINE") then
                if currentSection <> "" then
                    currentLines <- trimmedLine :: currentLines
                    sections <- sections.Add(currentSection, List.rev currentLines)
                currentSection <- ""
                currentLines <- []
            else
                currentLines <- trimmedLine :: currentLines
        
        if currentSection <> "" then
            sections <- sections.Add(currentSection, List.rev currentLines)
        
        sections
    
    interface IAllenBradleyParser with
        member this.SupportedVendor = PlcVendor.CreateAllenBradley()
        
        member this.SupportedFormats = [AllenBradleyL5K ""]
        
        member this.CanParse(format: PlcProgramFormat) =
            match format with
            | AllenBradleyL5K _ -> true
            | _ -> false
        
        member this.ParseAsync(filePath: string) : Task<RawPlcProgram> = task {
            try
                if not (File.Exists(filePath)) then
                    raise (FileNotFoundException($"File not found: {filePath}", filePath))

                let content = File.ReadAllText(filePath)
                let format = AllenBradleyL5K filePath
                return! (this :> IAllenBradleyParser).ParseContentAsync(content, format)
            with
            | ex ->
                logger.LogError(ex, "Error parsing Allen-Bradley file: {FilePath}", filePath)
                return raise ex
        }

        member this.ParseContentAsync(content: string, format: PlcProgramFormat) : Task<RawPlcProgram> = task {
            try
                let filePath = 
                    match format with
                    | AllenBradleyL5K path -> path
                    | _ -> "unknown.L5K"
                
                let projectInfo = extractProjectInfo content filePath
                let sections = parseSections content
                
                // Parse tags
                let variables = 
                    sections.TryFind("TAGS")
                    |> Option.defaultValue []
                    |> List.choose parseTagLine
                
                // Parse routines
                let logic = 
                    sections
                    |> Map.toList
                    |> List.filter (fun (key, _) -> key.StartsWith("ROUTINE "))
                    |> List.map (fun (_, lines) -> parseRoutine (String.Join("\n", lines)))
                
                let result = {
                    ProjectInfo = projectInfo
                    Variables = variables
                    Logic = logic
                    Comments = []
                    Metadata = Map.empty
                }
                
                logger.LogInformation("Successfully parsed Allen-Bradley file: {VariableCount} variables, {LogicCount} routines", 
                                    variables.Length, logic.Length)
                
                return result
            with
            | ex ->
                logger.LogError(ex, "Error parsing Allen-Bradley content")
                return raise ex
        }

        member this.ValidateFileAsync(filePath: string) : Task<ValidationResult> = 
            task {
                try
                    if not (File.Exists(filePath)) then
                        return ValidationResult.Error("File not found", filePath)
                    else 

                        let extension = Path.GetExtension(filePath).ToLower()

                        let validationResult =
                            if extension <> ".l5k" then
                                ValidationResult.Warning("Expected .L5K file extension", filePath)
                            else
                                let content = File.ReadAllText(filePath)

                                // Check for L5K specific markers
                                let hasProject = content.Contains("PROJECT ")
                                let hasTags = content.Contains("TAG ")

                                if not hasProject then
                                    ValidationResult.Warning("No PROJECT declaration found - may not be a valid L5K file", filePath)
                                elif not hasTags then
                                    ValidationResult.Warning("No TAG declarations found - empty project?", filePath)
                                else
                                    ValidationResult.Success

                        return validationResult
                with
                | ex ->
                    return ValidationResult.Error($"Validation failed: {ex.Message}", filePath)
        }
        
        member this.ParseL5KFileAsync(filePath: string) : Task<RawPlcProgram> =
            (this :> IAllenBradleyParser).ParseAsync(filePath)
        
        member this.ParseTagSectionAsync(content: string) = task {
            try
                let lines = content.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
                let variables = 
                    lines
                    |> Array.filter (fun l -> l.Trim().StartsWith("TAG "))
                    |> Array.choose parseTagLine
                    |> Array.toList
                
                return variables
            with
            | ex ->
                logger.LogError(ex, "Error parsing tag section")
                return []
        }
        
        member this.ParseRoutineSectionAsync(content: string) = task {
            try
                let sections = parseSections content
                let logic = 
                    sections
                    |> Map.toList
                    |> List.filter (fun (key, _) -> key.StartsWith("ROUTINE "))
                    |> List.map (fun (_, lines) -> parseRoutine (String.Join("\n", lines)))
                
                return logic
            with
            | ex ->
                logger.LogError(ex, "Error parsing routine section")
                return []
        }
        
        member this.AnalyzeProgramStructureAsync(content: string) = task {
            try
                let sections = parseSections content
                let analysis = 
                    sections
                    |> Map.map (fun key lines -> $"{lines.Length} lines")
                
                return analysis
            with
            | ex ->
                logger.LogError(ex, "Error analyzing program structure")
                return Map.empty
        }

/// Allen-Bradley 파서 생성을 위한 팩토리
module AllenBradleyParserFactory =
    let create (logger: ILogger<AllenBradleyParser>) : IAllenBradleyParser =
        AllenBradleyParser(logger) :> IAllenBradleyParser
