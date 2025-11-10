namespace Ev2.PLC.Mapper.Core.Configuration

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Configuration

/// JSON 설정 로더
type ConfigurationLoader(logger: ILogger<ConfigurationLoader>) =
    
    let jsonOptions = JsonSerializerOptions()
    do
        jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        jsonOptions.ReadCommentHandling <- JsonCommentHandling.Skip
        jsonOptions.AllowTrailingCommas <- true
        jsonOptions.Converters.Add(JsonStringEnumConverter())
    
    /// JSON 파일에서 설정 로드
    member this.LoadFromFile(configFilePath: string) : Result<MapperConfigRoot, string> =
        try
            if not (File.Exists(configFilePath)) then
                Result.Error $"Configuration file not found: {configFilePath}"
            else
                let jsonContent = File.ReadAllText(configFilePath)
                this.LoadFromJson(jsonContent)
        with
        | ex ->
            logger.LogError(ex, "Error loading configuration from file: {FilePath}", configFilePath)
            Result.Error $"Failed to load configuration: {ex.Message}"
    
    /// JSON 문자열에서 설정 로드
    member this.LoadFromJson(jsonContent: string) : Result<MapperConfigRoot, string> =
        try
            let config = JsonSerializer.Deserialize<MapperConfigRoot>(jsonContent, jsonOptions)
            logger.LogInformation("Configuration loaded successfully")
            Ok config
        with
        | ex ->
            logger.LogError(ex, "Error parsing configuration JSON")
            Result.Error $"Failed to parse configuration JSON: {ex.Message}"
    
    /// 기본 설정 경로에서 설정 로드 (설정 파일이 없으면 기본값 사용)
    member this.LoadOrDefault(configFilePath: string option) : MapperConfigRoot =
        match configFilePath with
        | Some path when File.Exists(path) ->
            match this.LoadFromFile(path) with
            | Ok config -> 
                logger.LogInformation("Configuration loaded from: {Path}", path)
                config
            | Result.Error msg ->
                logger.LogWarning("Failed to load configuration, using defaults: {Error}", msg)
                this.GetDefaultConfiguration()
        | _ ->
            logger.LogInformation("No configuration file specified, using defaults")
            this.GetDefaultConfiguration()
    
    /// 기본 설정 생성
    member this.GetDefaultConfiguration() : MapperConfigRoot =
        {
            MappingConfiguration = {
                DeviceTypeHints = 
                    [
                        ("MOTOR", "Motor"); ("MTR", "Motor"); ("M", "Motor")
                        ("CYLINDER", "Cylinder"); ("CYL", "Cylinder"); ("CY", "Cylinder")
                        ("SENSOR", "Sensor"); ("SEN", "Sensor"); ("S", "Sensor")
                        ("VALVE", "Valve"); ("VLV", "Valve"); ("V", "Valve")
                        ("CONVEYOR", "Conveyor"); ("CONV", "Conveyor"); ("CV", "Conveyor")
                        ("BUTTON", "PushButton"); ("BTN", "PushButton"); ("B", "PushButton")
                        ("LAMP", "Lamp"); ("LED", "Lamp"); ("L", "Lamp")
                        ("COUNTER", "Counter"); ("CNT", "Counter"); ("CT", "Counter")
                        ("TIMER", "Timer"); ("TMR", "Timer"); ("T", "Timer")
                        ("HMI", "HMI"); ("DISPLAY", "HMI"); ("SCREEN", "HMI")
                    ]
                    |> dict
                    |> System.Collections.Generic.Dictionary
                ApiTypeHints = {
                    Commands = 
                        [
                            ("FWD", "Command"); ("FORWARD", "Command"); ("FOR", "Command")
                            ("BACK", "Command"); ("BACKWARD", "Command"); ("BCK", "Command"); ("REV", "Command")
                            ("UP", "Command"); ("RAISE", "Command"); ("EXTEND", "Command"); ("EXT", "Command")
                            ("DOWN", "Command"); ("LOWER", "Command"); ("RETRACT", "Command"); ("RET", "Command")
                            ("START", "Command"); ("RUN", "Command"); ("ON", "Command")
                            ("STOP", "Command"); ("OFF", "Command"); ("HALT", "Command")
                            ("OPEN", "Command"); ("CLOSE", "Command")
                            ("RESET", "Command"); ("RST", "Command"); ("CLEAR", "Command")
                        ]
                        |> dict
                        |> System.Collections.Generic.Dictionary
                    Status = 
                        [
                            ("RUNNING", "Status"); ("RUN", "Status"); ("ACTIVE", "Status")
                            ("STOPPED", "Status"); ("IDLE", "Status"); ("READY", "Status")
                            ("ERROR", "Status"); ("ERR", "Status"); ("FAULT", "Status"); ("FLT", "Status")
                            ("ALARM", "Status"); ("ALM", "Status"); ("WARNING", "Status"); ("WARN", "Status")
                            ("DETECT", "Status"); ("DETECTED", "Status"); ("DET", "Status")
                            ("POSITION", "Status"); ("POS", "Status"); ("PRESENT", "Status")
                        ]
                        |> dict
                        |> System.Collections.Generic.Dictionary
                    Parameters = 
                        [
                            ("SPEED", "Parameter"); ("SPD", "Parameter"); ("VELOCITY", "Parameter"); ("VEL", "Parameter")
                            ("POSITION", "Parameter"); ("POS", "Parameter"); ("SETPOINT", "Parameter"); ("SP", "Parameter")
                            ("TIMEOUT", "Parameter"); ("TIME", "Parameter"); ("DELAY", "Parameter"); ("DLY", "Parameter")
                            ("PRESSURE", "Parameter"); ("PRESS", "Parameter"); ("TEMP", "Parameter"); ("TEMPERATURE", "Parameter")
                        ]
                        |> dict
                        |> System.Collections.Generic.Dictionary
                    Feedback = 
                        [
                            ("VALUE", "Feedback"); ("VAL", "Feedback"); ("ACTUAL", "Feedback"); ("ACT", "Feedback")
                            ("CURRENT", "Feedback"); ("CUR", "Feedback"); ("FEEDBACK", "Feedback"); ("FB", "Feedback")
                            ("MEASURED", "Feedback"); ("MEAS", "Feedback"); ("SIGNAL", "Feedback"); ("SIG", "Feedback")
                        ]
                        |> dict
                        |> System.Collections.Generic.Dictionary
                }
                NamingConventions = [|
                    {
                        Name = "Standard"
                        Pattern = @"^(?<area>[A-Z0-9]+)_(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$"
                        Description = "AREA_DEVICE_API format"
                        DeviceTypeHints = 
                            [
                                ("MOTOR", "Motor"); ("CYL", "Cylinder"); ("SENSOR", "Sensor")
                                ("VALVE", "Valve"); ("CONV", "Conveyor"); ("BTN", "PushButton")
                                ("LAMP", "Lamp"); ("CNT", "Counter"); ("TMR", "Timer")
                            ]
                            |> dict
                            |> System.Collections.Generic.Dictionary
                        ApiTypeHints = 
                            [
                                ("FWD", "Command"); ("BACK", "Command"); ("START", "Command"); ("STOP", "Command")
                                ("UP", "Command"); ("DOWN", "Command"); ("OPEN", "Command"); ("CLOSE", "Command")
                                ("RUNNING", "Status"); ("ERROR", "Status"); ("DETECT", "Status")
                                ("SPEED", "Parameter"); ("POSITION", "Parameter"); ("VALUE", "Feedback")
                            ]
                            |> dict
                            |> System.Collections.Generic.Dictionary
                        Priority = 1
                    }
                    {
                        Name = "Simple"
                        Pattern = @"^(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$"
                        Description = "DEVICE_API format"
                        DeviceTypeHints = System.Collections.Generic.Dictionary<string, string>()
                        ApiTypeHints = System.Collections.Generic.Dictionary<string, string>()
                        Priority = 2
                    }
                |]
                DeviceInferencePatterns = {
                    FallbackPatterns = [|
                        { Pattern = "MOTOR|DRIVE"; DeviceType = "Motor" }
                        { Pattern = "CYL|PISTON"; DeviceType = "Cylinder" }
                        { Pattern = "SENSOR|DETECT"; DeviceType = "Sensor" }
                        { Pattern = "VALVE|ACTUATOR"; DeviceType = "Valve" }
                        { Pattern = "CONV|BELT"; DeviceType = "Conveyor" }
                        { Pattern = "BUTTON|SWITCH"; DeviceType = "PushButton" }
                        { Pattern = "LAMP|LED|LIGHT"; DeviceType = "Lamp" }
                    |]
                }
                ApiInferenceRules = {
                    DeviceSpecificRules = [|
                        {
                            DeviceType = "Motor"
                            Patterns = [|
                                { Pattern = "SPEED|RPM"; DeviceType = "Parameter" }
                                { Pattern = "RUN|STOP"; DeviceType = "Command" }
                            |]
                        }
                        {
                            DeviceType = "Sensor"
                            Patterns = [|
                                { Pattern = "VALUE|READ"; DeviceType = "Feedback" }
                                { Pattern = "DETECT|PRESENT"; DeviceType = "Status" }
                            |]
                        }
                        {
                            DeviceType = "Cylinder"
                            Patterns = [|
                                { Pattern = "UP|DOWN"; DeviceType = "Command" }
                                { Pattern = "SENSOR|LIMIT"; DeviceType = "Status" }
                            |]
                        }
                    |]
                    GeneralRules = [|
                        { Pattern = "CMD|COMMAND$"; DeviceType = "Command" }
                        { Pattern = "STS|STATUS$"; DeviceType = "Status" }
                        { Pattern = "VAL|VALUE$"; DeviceType = "Feedback" }
                        { Pattern = "SET|CONFIG"; DeviceType = "Parameter" }
                    |]
                }
                ConfidenceBoosts = {
                    DeviceHintBoost = 0.1
                    ApiHintBoost = 0.1
                    PatternMatchBoost = 0.05
                }
                DefaultConfidenceLevels = {
                    FullMatch = 0.9
                    DeviceAndApi = 0.8
                    AreaAndDevice = 0.6
                    DeviceOnly = 0.5
                    Fallback = 0.3
                }
            }
            MappingOptions = {
                AnalyzeLogicFlow = true
                GenerateApiDependencies = true
                OptimizeAddressAllocation = true
                ValidateNaming = true
                GenerateDocumentation = false
                IncludeStatistics = true
                ParallelProcessing = true
                MaxConcurrency = Environment.ProcessorCount
            }
            ValidationRules = {
                VariableNaming = {
                    MaxLength = 100
                    AllowedCharacters = @"^[A-Za-z0-9_]+$"
                    ReservedWords = Some [| "IF"; "THEN"; "ELSE"; "END"; "AND"; "OR"; "NOT" |]
                }
                DeviceNaming = {
                    MaxLength = 50
                    AllowedCharacters = @"^[A-Z0-9_]+$"
                    ReservedWords = None
                }
                ApiNaming = {
                    MaxLength = 30
                    AllowedCharacters = @"^[A-Z0-9_]+$"
                    ReservedWords = None
                }
            }
            AddressRanges = {
                LSElectric = {|
                    InputAreas = [|
                        {
                            DeviceType = "X"
                            StartAddress = 0
                            EndAddress = 1023
                            DataTypes = [| "BOOL" |]
                            Description = "Digital Input"
                            IsReserved = false
                        }
                    |]
                    OutputAreas = [|
                        {
                            DeviceType = "Y"
                            StartAddress = 0
                            EndAddress = 1023
                            DataTypes = [| "BOOL" |]
                            Description = "Digital Output"
                            IsReserved = false
                        }
                    |]
                    MemoryAreas = [|
                        {
                            DeviceType = "M"
                            StartAddress = 0
                            EndAddress = 8191
                            DataTypes = [| "BOOL" |]
                            Description = "Internal Memory"
                            IsReserved = false
                        }
                        {
                            DeviceType = "D"
                            StartAddress = 0
                            EndAddress = 32767
                            DataTypes = [| "INT"; "DINT"; "REAL" |]
                            Description = "Data Register"
                            IsReserved = false
                        }
                    |]
                |}
            }
            CustomPatterns = [|
                {
                    Name = "KoreanConvention"
                    Regex = @"^(?<area>[A-Z0-9]+)_(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$"
                    DeviceType = None
                    ApiType = None
                    Direction = None
                    Description = "Korean industrial naming convention"
                }
            |]
        }

/// 외부 설정 인터페이스
type IConfigurationProvider =
    abstract member LoadConfiguration: configPath: string option -> MapperConfigRoot
    abstract member SaveConfiguration: config: MapperConfigRoot * filePath: string -> Result<unit, string>
    abstract member GetDefaultConfigPath: unit -> string

/// 설정 제공자 구현
type ConfigurationProvider(logger: ILogger<ConfigurationProvider>) =
    let loaderLogger = NullLogger<ConfigurationLoader>.Instance :> ILogger<ConfigurationLoader>
    let loader = ConfigurationLoader(loaderLogger)
    
    interface IConfigurationProvider with
        member this.LoadConfiguration(configPath: string option) =
            loader.LoadOrDefault(configPath)
        
        member this.SaveConfiguration(config: MapperConfigRoot, filePath: string) =
            try
                let jsonOptions = JsonSerializerOptions()
                jsonOptions.WriteIndented <- true
                jsonOptions.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
                jsonOptions.Converters.Add(JsonStringEnumConverter())
                
                let jsonContent = JsonSerializer.Serialize(config, jsonOptions)
                File.WriteAllText(filePath, jsonContent)
                logger.LogInformation("Configuration saved to: {FilePath}", filePath)
                Ok()
            with
            | ex ->
                logger.LogError(ex, "Error saving configuration to: {FilePath}", filePath)
                Result.Error $"Failed to save configuration: {ex.Message}"
        
        member this.GetDefaultConfigPath() =
            Path.Combine(AppContext.BaseDirectory, "Config", "mapper-config.json")

/// 설정 팩토리
module ConfigurationFactory =
    let createProvider (logger: ILogger<ConfigurationProvider>) : IConfigurationProvider =
        ConfigurationProvider(logger) :> IConfigurationProvider
    
    let createLoader (logger: ILogger<ConfigurationLoader>) : ConfigurationLoader =
        ConfigurationLoader(logger)