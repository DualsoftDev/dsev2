namespace Ev2.PLC.Mapper.Core.Configuration

open System
open System.Collections.Generic
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types

/// JSON 설정에서 로드되는 API 타입 힌트
type ApiTypeHintsConfig = {
    Commands: Dictionary<string, string>
    Status: Dictionary<string, string>
    Parameters: Dictionary<string, string>
    Feedback: Dictionary<string, string>
}

/// JSON 설정에서 로드되는 명명 규칙
type NamingConventionConfig = {
    Name: string
    Pattern: string
    Description: string
    DeviceTypeHints: Dictionary<string, string>
    ApiTypeHints: Dictionary<string, string>
    Priority: int
}

/// 장치 추론 패턴 설정
type DeviceInferencePatternConfig = {
    Pattern: string
    DeviceType: string
}

/// 장치별 API 추론 규칙 설정
type DeviceSpecificRuleConfig = {
    DeviceType: string
    Patterns: DeviceInferencePatternConfig[]
}

/// API 추론 규칙 설정
type ApiInferenceRulesConfig = {
    DeviceSpecificRules: DeviceSpecificRuleConfig[]
    GeneralRules: DeviceInferencePatternConfig[]
}

/// 신뢰도 부스트 설정
type ConfidenceBoostsConfig = {
    DeviceHintBoost: float
    ApiHintBoost: float
    PatternMatchBoost: float
}

/// 기본 신뢰도 레벨 설정
type DefaultConfidenceLevelsConfig = {
    FullMatch: float
    DeviceAndApi: float
    AreaAndDevice: float
    DeviceOnly: float
    Fallback: float
}

/// 장치 추론 패턴 설정
type DeviceInferencePatternsConfig = {
    FallbackPatterns: DeviceInferencePatternConfig[]
}

/// 주소 범위 설정
type AddressAreaConfig = {
    DeviceType: string
    StartAddress: int
    EndAddress: int
    DataTypes: string[]
    Description: string
    IsReserved: bool
}

/// PLC 벤더별 주소 범위 설정
type AddressRangeConfig = {
    LSElectric: {|
        InputAreas: AddressAreaConfig[]
        OutputAreas: AddressAreaConfig[]
        MemoryAreas: AddressAreaConfig[]
    |}
}

/// 검증 규칙 설정
type ValidationRuleConfig = {
    MaxLength: int
    AllowedCharacters: string
    ReservedWords: string[] option
}

/// 검증 규칙들 설정
type ValidationRulesConfig = {
    VariableNaming: ValidationRuleConfig
    DeviceNaming: ValidationRuleConfig
    ApiNaming: ValidationRuleConfig
}

/// 매핑 옵션 설정
type MappingOptionsConfig = {
    AnalyzeLogicFlow: bool
    GenerateApiDependencies: bool
    OptimizeAddressAllocation: bool
    ValidateNaming: bool
    GenerateDocumentation: bool
    IncludeStatistics: bool
    ParallelProcessing: bool
    MaxConcurrency: int
}

/// 커스텀 패턴 설정
type CustomPatternConfig = {
    Name: string
    Regex: string
    DeviceType: string option
    ApiType: string option
    Direction: string option
    Description: string
}

/// 매핑 설정
type MappingConfigurationConfig = {
    DeviceTypeHints: Dictionary<string, string>
    ApiTypeHints: ApiTypeHintsConfig
    NamingConventions: NamingConventionConfig[]
    DeviceInferencePatterns: DeviceInferencePatternsConfig
    ApiInferenceRules: ApiInferenceRulesConfig
    ConfidenceBoosts: ConfidenceBoostsConfig
    DefaultConfidenceLevels: DefaultConfidenceLevelsConfig
}

/// 전체 설정 루트
type MapperConfigRoot = {
    MappingConfiguration: MappingConfigurationConfig
    MappingOptions: MappingOptionsConfig
    ValidationRules: ValidationRulesConfig
    AddressRanges: AddressRangeConfig
    CustomPatterns: CustomPatternConfig[]
}

/// 설정 변환 도우미
module ConfigurationConverter =
    
    let convertDeviceType (deviceTypeStr: string) : DeviceType =
        match deviceTypeStr with
        | "Motor" -> Motor
        | "Cylinder" -> Cylinder
        | "Sensor" -> Sensor
        | "Valve" -> Valve
        | "Conveyor" -> Conveyor
        | "PushButton" -> PushButton
        | "Lamp" -> Lamp
        | "Counter" -> Counter
        | "Timer" -> Timer
        | "HMI" -> HMI
        | custom -> DeviceType.Custom custom
    
    let convertApiType (apiTypeStr: string) : ApiType =
        match apiTypeStr with
        | "Command" -> Command
        | "Status" -> Status
        | "Parameter" -> Parameter
        | "Feedback" -> Feedback
        | _ -> Command
    
    let convertIODirection (directionStr: string option) : IODirection option =
        match directionStr with
        | Some "Input" -> Some Input
        | Some "Output" -> Some Output
        | _ -> None
    
    let convertDeviceTypeHints (hints: Dictionary<string, string>) : Map<string, DeviceType> =
        hints
        |> Seq.map (fun kvp -> kvp.Key, convertDeviceType kvp.Value)
        |> Map.ofSeq
    
    let convertApiTypeHints (hints: ApiTypeHintsConfig) : Map<string, ApiType> =
        let convertDict (dict: Dictionary<string, string>) =
            dict |> Seq.map (fun kvp -> kvp.Key, convertApiType kvp.Value)
        
        [
            convertDict hints.Commands
            convertDict hints.Status
            convertDict hints.Parameters
            convertDict hints.Feedback
        ]
        |> Seq.concat
        |> Map.ofSeq
    
    let convertNamingConventions (conventions: NamingConventionConfig[]) : NamingConvention list =
        conventions
        |> Array.map (fun conv -> 
            let deviceHints = 
                conv.DeviceTypeHints
                |> Seq.map (fun kvp -> kvp.Key, convertDeviceType kvp.Value)
                |> Map.ofSeq
                
            let apiHints = 
                conv.ApiTypeHints
                |> Seq.map (fun kvp -> kvp.Key, convertApiType kvp.Value)
                |> Map.ofSeq
                
            ({
                Name = conv.Name
                Pattern = conv.Pattern
                Description = conv.Description
                DeviceTypeHints = deviceHints
                ApiTypeHints = apiHints
                Priority = conv.Priority
            } : NamingConvention))
        |> Array.toList
    
    let convertMappingOptions (options: MappingOptionsConfig) : MappingOptions =
        {
            AnalyzeLogicFlow = options.AnalyzeLogicFlow
            GenerateApiDependencies = options.GenerateApiDependencies
            OptimizeAddressAllocation = options.OptimizeAddressAllocation
            ValidateNaming = options.ValidateNaming
            GenerateDocumentation = options.GenerateDocumentation
            IncludeStatistics = options.IncludeStatistics
            ParallelProcessing = options.ParallelProcessing
            MaxConcurrency = if options.MaxConcurrency = 0 then Environment.ProcessorCount else options.MaxConcurrency
        }
    
    let convertCustomPatterns (patterns: CustomPatternConfig[]) : VariablePattern list =
        patterns
        |> Array.map (fun pattern -> 
            let deviceType = pattern.DeviceType |> Option.map convertDeviceType
            let apiType = pattern.ApiType |> Option.map convertApiType
            let direction = convertIODirection pattern.Direction
            
            ({
                Name = pattern.Name
                Regex = pattern.Regex
                DeviceType = deviceType
                ApiType = apiType
                Direction = direction
                Description = pattern.Description
            } : VariablePattern))
        |> Array.toList