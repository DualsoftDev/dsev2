namespace Ev2.PLC.Mapper

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces
open Ev2.PLC.Mapper.Core.Engine
open Ev2.PLC.Mapper.Core.Configuration
open Ev2.PLC.Mapper.Parsers.LSElectric
open Ev2.PLC.Mapper.Parsers.AllenBradley

/// PLC Mapper 메인 팩토리
type MapperFactory(loggerFactory: ILoggerFactory, ?configProvider: IConfigurationProvider) =
    
    let logger = loggerFactory.CreateLogger<MapperFactory>()
    let configProvider = 
        configProvider |> Option.defaultWith (fun () ->
            let configLogger = loggerFactory.CreateLogger<ConfigurationProvider>()
            ConfigurationFactory.createProvider configLogger)
    
    /// 제조사별 파서 생성
    member this.CreateParser(vendor: PlcVendor) : IPlcProgramParser option =
        try
            match vendor with
            | PlcVendor.LSElectric _ ->
                let parserLogger = loggerFactory.CreateLogger<LSElectricParser>()
                Some (LSElectricParserFactory.create parserLogger :> IPlcProgramParser)
            
            | PlcVendor.AllenBradley _ ->
                let parserLogger = loggerFactory.CreateLogger<AllenBradleyParser>()
                Some (AllenBradleyParserFactory.create parserLogger :> IPlcProgramParser)
            
            | PlcVendor.Mitsubishi _ ->
                // TODO: Implement Mitsubishi parser
                logger.LogWarning("Mitsubishi parser not implemented yet")
                None
            
            | PlcVendor.Siemens _ ->
                // TODO: Implement Siemens parser
                logger.LogWarning("Siemens parser not implemented yet")
                None
            
            | PlcVendor.Custom _ ->
                logger.LogWarning($"Custom parser not supported: {vendor}")
                None
        with
        | ex ->
            logger.LogError(ex, "Error creating parser for vendor: {Vendor}", vendor)
            None
    
    /// 변수 분석기 생성
    member this.CreateVariableAnalyzer() : IVariableAnalyzer =
        let analyzerLogger = loggerFactory.CreateLogger<VariableAnalyzer>()
        VariableAnalyzerFactory.create analyzerLogger configProvider
    
    /// 명명 규칙 분석기 생성
    member this.CreateNamingAnalyzer() : INamingAnalyzer =
        let namingLogger = loggerFactory.CreateLogger<NamingAnalyzer>()
        VariableAnalyzerFactory.createNamingAnalyzer namingLogger

    /// 로직 분석기 생성
    member this.CreateLogicAnalyzer(vendor: PlcVendor) : ILogicAnalyzer option =
        try
            match vendor with
            | PlcVendor.LSElectric _ ->
                let analyzerLogger = loggerFactory.CreateLogger<LSLogicAnalyzer>()
                Some (LSLogicAnalyzerFactory.create analyzerLogger :> ILogicAnalyzer)

            | PlcVendor.AllenBradley _ ->
                let analyzerLogger = loggerFactory.CreateLogger<ABLogicAnalyzer>()
                Some (ABLogicAnalyzerFactory.create analyzerLogger :> ILogicAnalyzer)

            | PlcVendor.Mitsubishi _ ->
                let analyzerLogger = loggerFactory.CreateLogger<MxLogicAnalyzer>()
                Some (MxLogicAnalyzerFactory.create analyzerLogger :> ILogicAnalyzer)

            | PlcVendor.Siemens _ ->
                let analyzerLogger = loggerFactory.CreateLogger<S7LogicAnalyzer>()
                Some (S7LogicAnalyzerFactory.create analyzerLogger :> ILogicAnalyzer)

            | PlcVendor.Custom _ ->
                logger.LogWarning($"Custom logic analyzer not supported: {vendor}")
                None
        with
        | ex ->
            logger.LogError(ex, "Error creating logic analyzer for vendor: {Vendor}", vendor)
            None
    
    /// 파일 경로에서 제조사 추론
    member this.InferVendorFromFile(filePath: string) : PlcVendor option =
        let extension = System.IO.Path.GetExtension(filePath).ToLower()
        let fileName = System.IO.Path.GetFileName(filePath).ToLower()
        
        match extension with
        | ".l5k" -> Some (PlcVendor.CreateAllenBradley())
        | ".xml" when fileName.Contains("xg") || fileName.Contains("ls") -> Some (PlcVendor.CreateLSElectric())
        | ".xml" when fileName.Contains("tia") || fileName.Contains("siemens") -> Some (PlcVendor.CreateSiemens())
        | ".csv" when fileName.Contains("gx") || fileName.Contains("mitsubishi") -> Some (PlcVendor.CreateMitsubishi())
        | _ -> None
    
    /// 전체 매핑 파이프라인 실행
    member this.ProcessPlcProgramAsync(filePath: string, ?config: MappingConfiguration, ?options: MappingOptions) : Task<MappingResult> = task {
        let vendor = 
            config 
            |> Option.map (fun program -> program.Vendor)
            |> Option.orElse (this.InferVendorFromFile(filePath))
            |> Option.defaultValue (PlcVendor.CreateLSElectric())

        try
            logger.LogInformation("Starting PLC program processing: {FilePath}", filePath)
            
            match this.CreateParser(vendor) with
            | None -> 
                return MappingResult.CreateError(
                    ProjectInfo.Create("Unknown", vendor, CustomFormat(filePath, "Unknown"), filePath),
                    [$"No parser available for vendor: {vendor}"])
            
            | Some parser ->
                try
                    // 2. 파일 파싱
                    let! rawProgram = parser.ParseAsync(filePath)

                    logger.LogInformation("Successfully parsed {VariableCount} variables from {FilePath}", 
                                         rawProgram.Variables.Length, filePath)
                    
                    // 3. 변수 분석
                    let variableAnalyzer = this.CreateVariableAnalyzer()
                    let mappingConfig = config |> Option.defaultValue (MappingConfiguration.Default(vendor))
                    
                    let! (analysisResults: VariableAnalysisResult list) =
                        variableAnalyzer.AnalyzeVariablesBatchAsync(rawProgram.Variables, mappingConfig)
                    
                    // 4. 영역 및 디바이스 추출
                    let! areas = variableAnalyzer.ExtractAreasAsync(rawProgram.Variables)
                    let! devices = variableAnalyzer.ExtractDevicesAsync(rawProgram.Variables, areas)
                    
                    // 5. API 정의 생성
                    let! apiDefinitions = variableAnalyzer.GenerateApiDefinitionsAsync(devices)

                    // 5.5. 로직 흐름 분석 (옵션에 따라)
                    let! logicFlow =
                        if options.IsSome && options.Value.AnalyzeLogicFlow then
                            match this.CreateLogicAnalyzer(vendor) with
                            | Some logicAnalyzer ->
                                task {
                                    try
                                        let! flows = logicAnalyzer.AnalyzeRungsBatchAsync(rawProgram.Logic)
                                        logger.LogInformation("Logic flow analysis complete: {FlowCount} flows analyzed", flows.Length)
                                        return flows
                                    with
                                    | ex ->
                                        logger.LogWarning(ex, "Logic flow analysis failed, continuing without it")
                                        return []
                                }
                            | None ->
                                logger.LogDebug("No logic analyzer available for {Vendor}", vendor)
                                Task.FromResult([])
                        else
                            logger.LogDebug("Logic flow analysis disabled")
                            Task.FromResult([])

                    // 6. I/O 매핑 생성
                    let ioMapping =
                        analysisResults
                        |> List.choose (fun result ->
                            match result.Device, result.Api with
                            | Some device, Some api ->
                                let address = PlcAddress.Create("D100", deviceArea = "D", index = 100) // TODO: Implement proper address allocation
                                let ioVar = IOVariable.Create(result.Variable.Name, address, Bool, Input)
                                Some ioVar
                            | _ -> None)
                        |> List.fold (fun (mapping: IOMapping) var -> mapping.AddVariable(var)) IOMapping.Empty
                    
                    // 7. 통계 생성
                    let statistics = {
                        TotalVariables = rawProgram.Variables.Length
                        MappedVariables = analysisResults |> List.filter (fun r -> r.IsValid) |> List.length
                        TotalAreas = areas.Length
                        TotalDevices = devices.Length
                        TotalApis = apiDefinitions.Length
                        ProcessingTime = TimeSpan.FromSeconds(1.0) // TODO: Implement actual timing
                        FileSize = rawProgram.ProjectInfo.FileSize
                        ParsingTime = TimeSpan.FromMilliseconds(500.0)
                        AnalysisTime = TimeSpan.FromMilliseconds(500.0)
                    }
                    
                    // 8. 경고 및 오류 수집
                    let warnings = 
                        analysisResults
                        |> List.collect (fun r -> r.Issues)
                    
                    let errors = 
                        analysisResults
                        |> List.filter (fun r -> not r.IsValid)
                        |> List.map (fun r -> $"Failed to analyze variable: {r.Variable.Name}")
                    
                    let result = {
                        Success = errors.IsEmpty
                        ProjectInfo = rawProgram.ProjectInfo
                        Areas = areas
                        Devices = devices
                        ApiDefinitions = apiDefinitions
                        IOMapping = ioMapping
                        LogicFlow = logicFlow
                        Statistics = statistics
                        Warnings = warnings
                        Errors = errors
                    }
                    
                    logger.LogInformation("Mapping completed: {MappedCount}/{TotalCount} variables mapped successfully", 
                                         statistics.MappedVariables, statistics.TotalVariables)
                    
                    return result
                with
                | ex ->
                    logger.LogError(ex, "Error processing PLC program: {FilePath}", filePath)
                    return MappingResult.CreateError(
                        ProjectInfo.Create("Unknown", vendor, CustomFormat(filePath, "Unknown"), filePath),
                        [$"Processing error: {ex.Message}"])
        with
        | ex ->
            logger.LogError(ex, "Error processing PLC program: {FilePath}", filePath)
            return MappingResult.CreateError(
                ProjectInfo.Create("Error", vendor, CustomFormat(filePath, "Error"), filePath),
                [$"Processing error: {ex.Message}"])
    }
    
    /// 지원되는 파일 형식 확인
    member this.GetSupportedFormats() : (PlcVendor * string list) list = [
        (PlcVendor.CreateLSElectric(), [".xml"])
        (PlcVendor.CreateAllenBradley(), [".L5K"])
        (PlcVendor.CreateMitsubishi(), [".csv"])
        (PlcVendor.CreateSiemens(), [".xml"])
    ]
    
    /// 파일 유효성 검사
    member this.ValidateFileAsync(filePath: string) : Task<ValidationResult> = task {
        try
            if not (System.IO.File.Exists(filePath)) then
                return ValidationResult.Error("File not found", filePath)
            else 
                match this.InferVendorFromFile(filePath) with
                | None ->
                    return ValidationResult.Warning("Cannot determine PLC vendor from file", filePath, 
                        "Supported formats: .xml (LS Electric/Siemens), .L5K (Allen-Bradley), .csv (Mitsubishi)")
            
                | Some vendor ->
                    match this.CreateParser(vendor) with
                    | None ->
                        return ValidationResult.Error($"No parser available for {vendor}", filePath)
                    | Some parser ->
                        let! validationResult = parser.ValidateFileAsync(filePath)
                        return validationResult
        with
        | ex ->
            return ValidationResult.Error($"Validation error: {ex.Message}", filePath)
    }

/// 간편한 API 함수들
module MapperApi =
    
    /// 기본 로거로 MapperFactory 생성
    let createFactory(configPath: string option) =
        let loggerFactory = (NullLoggerFactory.Instance :> ILoggerFactory)
        let configLogger = loggerFactory.CreateLogger<ConfigurationProvider>()
        let configProvider = ConfigurationFactory.createProvider configLogger
        
        // 설정 경로가 제공되면 해당 설정 로드
        match configPath with
        | Some path -> 
            let _ = configProvider.LoadConfiguration(Some path)
            ()
        | None -> ()
        
        MapperFactory(loggerFactory, configProvider)
    
    /// 단순 파일 처리
    let processFileAsync (filePath: string) = task {
        let factory = createFactory(None)
        return! factory.ProcessPlcProgramAsync(filePath)
    }
    
    /// 설정과 함께 파일 처리
    let processFileWithConfigAsync (filePath: string) (config: MappingConfiguration) = task {
        let factory = createFactory(None)
        return! factory.ProcessPlcProgramAsync(filePath, config)
    }
    
    /// 외부 설정 파일과 함께 파일 처리
    let processFileWithConfigFileAsync (filePath: string) (configPath: string) = task {
        let factory = createFactory(Some configPath)
        return! factory.ProcessPlcProgramAsync(filePath)
    }
