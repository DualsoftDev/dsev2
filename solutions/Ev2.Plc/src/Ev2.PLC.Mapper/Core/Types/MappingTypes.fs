namespace Ev2.PLC.Mapper.Core.Types

open System
open Ev2.PLC.Common.Types

/// 검증 심각도
type ValidationSeverity =
    | ValidationInfo
    | ValidationWarning
    | ValidationError
    | ValidationCritical

/// 검증 결과
type ValidationResult = {
    IsValid: bool
    Severity: ValidationSeverity
    Message: string
    Variable: string option
    Suggestion: string option
} with
    static member Success = {
        IsValid = true
        Severity = ValidationSeverity.ValidationInfo
        Message = "Validation passed"
        Variable = None
        Suggestion = None
    }

    static member Error(message: string, ?variable: string, ?suggestion: string) = {
        IsValid = false
        Severity = ValidationSeverity.ValidationError
        Message = message
        Variable = variable
        Suggestion = suggestion
    }

    static member Warning(message: string, ?variable: string, ?suggestion: string) = {
        IsValid = true
        Severity = ValidationSeverity.ValidationWarning
        Message = message
        Variable = variable
        Suggestion = suggestion
    }

/// 검증 규칙
type ValidationRule = {
    Name: string
    Description: string
    Severity: ValidationSeverity
    Check: RawVariable -> ValidationResult
}

/// 명명 규칙
type NamingConvention = {
    Name: string
    Pattern: string
    Description: string
    DeviceTypeHints: Map<string, DeviceType>
    ApiTypeHints: Map<string, ApiType>
    Priority: int
} with
    static member GetDefaults() = [
        {
            Name = "Standard"
            Pattern = @"^(?<area>[A-Z0-9]+)_(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$"
            Description = "AREA_DEVICE_API format"
            DeviceTypeHints = Map.ofList [
                ("MOTOR", Motor); ("CYL", Cylinder); ("SENSOR", Sensor)
                ("VALVE", Valve); ("CONV", Conveyor); ("BTN", PushButton)
                ("LAMP", Lamp); ("CNT", DeviceType.Counter); ("TMR", DeviceType.Timer)
            ]
            ApiTypeHints = Map.ofList [
                ("FWD", Command); ("BACK", Command); ("START", Command); ("STOP", Command)
                ("UP", Command); ("DOWN", Command); ("OPEN", Command); ("CLOSE", Command)
                ("RUNNING", Status); ("ERROR", Status); ("DETECT", Status)
                ("SPEED", Parameter); ("POSITION", Parameter); ("VALUE", Feedback)
            ]
            Priority = 1
        }
        {
            Name = "Simple"
            Pattern = @"^(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$"
            Description = "DEVICE_API format"
            DeviceTypeHints = Map.empty
            ApiTypeHints = Map.empty
            Priority = 2
        }
    ]

/// 변수 패턴
type VariablePattern = {
    Name: string
    Regex: string
    DeviceType: DeviceType option
    ApiType: ApiType option
    Direction: IODirection option
    Description: string
}

/// 주소 범위
type AddressRange = {
    DeviceType: string
    StartAddress: int
    EndAddress: int
    DataTypes: PlcDataType list
    Description: string
    IsReserved: bool
}

/// 매핑 단계
type MappingPhase =
    | Parsing
    | Analysis
    | Mapping
    | Validation
    | Optimization
    | Completed
    | Failed of string

    member this.DisplayName =
        match this with
        | Parsing -> "Parsing PLC Program"
        | Analysis -> "Analyzing Variables"
        | Mapping -> "Creating Mappings"
        | Validation -> "Validating Results"
        | Optimization -> "Optimizing Layout"
        | Completed -> "Completed"
        | Failed msg -> $"Failed: {msg}"

/// 매핑 진행 상태
type MappingProgress = {
    Phase: MappingPhase
    CurrentStep: string
    CompletedSteps: int
    TotalSteps: int
    StartTime: DateTime
    ElapsedTime: TimeSpan
    EstimatedRemaining: TimeSpan option
} with
    static member Start(totalSteps: int) = {
        Phase = Parsing
        CurrentStep = "Starting"
        CompletedSteps = 0
        TotalSteps = totalSteps
        StartTime = DateTime.UtcNow
        ElapsedTime = TimeSpan.Zero
        EstimatedRemaining = None
    }

    member this.ProgressPercentage =
        if this.TotalSteps = 0 then 0.0
        else (float this.CompletedSteps) / (float this.TotalSteps) * 100.0

/// 매핑 옵션
type MappingOptions = {
    AnalyzeLogicFlow: bool
    GenerateApiDependencies: bool
    OptimizeAddressAllocation: bool
    ValidateNaming: bool
    GenerateDocumentation: bool
    IncludeStatistics: bool
    ParallelProcessing: bool
    MaxConcurrency: int
} with
    static member Default = {
        AnalyzeLogicFlow = true
        GenerateApiDependencies = true
        OptimizeAddressAllocation = true
        ValidateNaming = true
        GenerateDocumentation = false
        IncludeStatistics = true
        ParallelProcessing = true
        MaxConcurrency = Environment.ProcessorCount
    }

/// 매핑 설정
type MappingConfiguration = {
    Vendor: PlcVendor
    NamingConventions: NamingConvention list
    DeviceTypeMapping: Map<string, DeviceType>
    ApiTypeMapping: Map<string, ApiType>
    AddressRanges: Map<string, AddressRange>
    ValidationRules: ValidationRule list
    OptimizationEnabled: bool
    CustomPatterns: VariablePattern list
} with
    static member Default(vendor: PlcVendor) = {
        Vendor = vendor
        NamingConventions = NamingConvention.GetDefaults()
        DeviceTypeMapping = Map.empty
        ApiTypeMapping = Map.empty
        AddressRanges = Map.empty
        ValidationRules = []
        OptimizationEnabled = true
        CustomPatterns = []
    }

/// 매핑 컨텍스트
type MappingContext = {
    Configuration: MappingConfiguration
    Options: MappingOptions
    Progress: MappingProgress
    CurrentArea: string option
    CurrentDevice: string option
    Errors: string list
    Warnings: string list
} with
    static member Create(config: MappingConfiguration, options: MappingOptions) = {
        Configuration = config
        Options = options
        Progress = MappingProgress.Start(5)
        CurrentArea = None
        CurrentDevice = None
        Errors = []
        Warnings = []
    }

    member this.AddError(error: string) =
        { this with Errors = error :: this.Errors }

    member this.AddWarning(warning: string) =
        { this with Warnings = warning :: this.Warnings }

    member this.UpdateProgress(phase: MappingPhase, step: string) =
        let completedSteps =
            match phase with
            | Parsing -> 1
            | Analysis -> 2
            | Mapping -> 3
            | Validation -> 4
            | Optimization -> 5
            | Completed -> 5
            | Failed _ -> this.Progress.CompletedSteps

        let elapsed = DateTime.UtcNow - this.Progress.StartTime
        { this with
            Progress =
                { this.Progress with
                    Phase = phase
                    CurrentStep = step
                    CompletedSteps = completedSteps
                    ElapsedTime = elapsed
                }
        }
