namespace Ev2.PLC.Mapper.Core.Engine

open System
open System.Text.RegularExpressions
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces
open Ev2.PLC.Mapper.Core.Configuration

/// 변수 분석 엔진 구현
type VariableAnalyzer(logger: ILogger<VariableAnalyzer>, configProvider: IConfigurationProvider) =
    
    let mutable currentConfig = configProvider.LoadConfiguration(None)
    let mutable deviceTypeHints = ConfigurationConverter.convertDeviceTypeHints currentConfig.MappingConfiguration.DeviceTypeHints
    let mutable apiTypeHints = ConfigurationConverter.convertApiTypeHints currentConfig.MappingConfiguration.ApiTypeHints
    
    let extractNameComponents (variableName: string) (pattern: string) : Map<string, string> option =
        try
            let regex = Regex(pattern, RegexOptions.IgnoreCase)
            let regexMatch = regex.Match(variableName)

            if regexMatch.Success then
                let mutable components = Map.empty
                for groupName in regex.GetGroupNames() do
                    if groupName <> "0" && regexMatch.Groups.[groupName].Success then
                        components <- components.Add(groupName, regexMatch.Groups.[groupName].Value)
                Some components
            else
                None
        with
        | ex ->
            logger.LogWarning(ex, "Error parsing variable name with pattern: {Pattern}", pattern)
            None
    
    let inferDeviceTypeFromName (deviceName: string) : DeviceType =
        let upperName = deviceName.ToUpper()
        
        deviceTypeHints
        |> Map.tryPick (fun hint deviceType ->
            if upperName.Contains(hint) then Some deviceType else None)
        |> Option.defaultWith (fun () ->
            // Fallback logic based on configuration patterns
            let fallbackPatterns = currentConfig.MappingConfiguration.DeviceInferencePatterns.FallbackPatterns
            
            let matchedPattern = 
                fallbackPatterns
                |> Array.tryFind (fun pattern ->
                    let regex = Regex(pattern.Pattern, RegexOptions.IgnoreCase)
                    regex.IsMatch(upperName))
            
            match matchedPattern with
            | Some pattern -> ConfigurationConverter.convertDeviceType pattern.DeviceType
            | None -> DeviceType.Custom deviceName)
    
    let inferApiTypeFromName (apiName: string) (deviceType: DeviceType) : ApiType =
        let upperApi = apiName.ToUpper()
        
        apiTypeHints
        |> Map.tryFind upperApi
        |> Option.defaultWith (fun () ->
            // Device-specific API type inference from configuration
            let deviceTypeName = deviceType.ToString()
            let deviceRules = 
                currentConfig.MappingConfiguration.ApiInferenceRules.DeviceSpecificRules
                |> Array.tryFind (fun rule -> rule.DeviceType = deviceTypeName)
            
            let deviceMatch = 
                match deviceRules with
                | Some rules ->
                    rules.Patterns
                    |> Array.tryFind (fun pattern ->
                        let regex = Regex(pattern.Pattern, RegexOptions.IgnoreCase)
                        regex.IsMatch(upperApi))
                    |> Option.map (fun pattern -> ConfigurationConverter.convertApiType pattern.DeviceType)
                | None -> None
            
            match deviceMatch with
            | Some apiType -> apiType
            | None ->
                // General rules from configuration
                let generalMatch = 
                    currentConfig.MappingConfiguration.ApiInferenceRules.GeneralRules
                    |> Array.tryFind (fun rule ->
                        let regex = Regex(rule.Pattern, RegexOptions.IgnoreCase)
                        regex.IsMatch(upperApi))
                    |> Option.map (fun rule -> ConfigurationConverter.convertApiType rule.DeviceType)
                
                match generalMatch with
                | Some apiType -> apiType
                | None -> Command) // Default to command
    
    let calculateConfidence (components: Map<string, string>) (conventions: NamingConvention list) : float =
        let hasArea = components.ContainsKey("area")
        let hasDevice = components.ContainsKey("device")
        let hasApi = components.ContainsKey("api")
        
        let confidenceLevels = currentConfig.MappingConfiguration.DefaultConfidenceLevels
        let confidenceBoosts = currentConfig.MappingConfiguration.ConfidenceBoosts
        
        let baseConfidence = 
            match hasArea, hasDevice, hasApi with
            | true, true, true -> confidenceLevels.FullMatch
            | false, true, true -> confidenceLevels.DeviceAndApi
            | true, true, false -> confidenceLevels.AreaAndDevice
            | false, true, false -> confidenceLevels.DeviceOnly
            | _ -> confidenceLevels.Fallback
        
        // Boost confidence if device/api names match known patterns
        let deviceBoost = 
            components.TryFind("device")
            |> Option.bind (fun device -> deviceTypeHints |> Map.tryPick (fun hint _ -> 
                if device.ToUpper().Contains(hint) then Some confidenceBoosts.DeviceHintBoost else None))
            |> Option.defaultValue 0.0
        
        let apiBoost = 
            components.TryFind("api")
            |> Option.bind (fun api -> apiTypeHints |> Map.tryFind (api.ToUpper()))
            |> Option.map (fun _ -> confidenceBoosts.ApiHintBoost)
            |> Option.defaultValue 0.0
        
        Math.Min(1.0, baseConfidence + deviceBoost + apiBoost)
    
    // 설정 재로드 메서드
    member this.ReloadConfiguration(?configPath: string) =
        currentConfig <- configProvider.LoadConfiguration(configPath)
        deviceTypeHints <- ConfigurationConverter.convertDeviceTypeHints currentConfig.MappingConfiguration.DeviceTypeHints
        apiTypeHints <- ConfigurationConverter.convertApiTypeHints currentConfig.MappingConfiguration.ApiTypeHints
        logger.LogInformation("Configuration reloaded with {DeviceHints} device hints and {ApiHints} API hints", 
                            deviceTypeHints.Count, apiTypeHints.Count)
    
    interface IVariableAnalyzer with
        member this.AnalyzeVariableNameAsync(variable: RawVariable, conventions: NamingConvention list) = task {
            try
                let mutable bestPattern = None
                let mutable bestConfidence = 0.0
                
                for convention in conventions do
                    match extractNameComponents variable.Name convention.Pattern with
                    | Some components ->
                        let confidence = calculateConfidence components conventions
                        if confidence > bestConfidence then
                            bestConfidence <- confidence
                            bestPattern <- Some {
                                OriginalName = variable.Name
                                Prefix = components.TryFind("area")
                                DeviceName = components.TryFind("device") |> Option.defaultValue variable.Name
                                ApiSuffix = components.TryFind("api")
                                IOType = if variable.Address.StartsWith("I") || variable.Address.StartsWith("X") then Input else Output
                                Confidence = confidence
                            }
                    | None -> ()
                
                return bestPattern
            with
            | ex ->
                logger.LogError(ex, "Error analyzing variable name: {VariableName}", variable.Name)
                return None
        }
        
        member this.InferDeviceTypeAsync(variableName: string) = task {
            return inferDeviceTypeFromName variableName
        }
        
        member this.InferApiTypeAsync(apiName: string, deviceType: DeviceType) = task {
            return inferApiTypeFromName apiName deviceType
        }
        
        member this.AnalyzeVariablesBatchAsync(variables: RawVariable list, config: MappingConfiguration) = task {
            let! results = 
                variables
                |> List.map (fun variable -> task {
                    let! pattern = (this :> IVariableAnalyzer).AnalyzeVariableNameAsync(variable, config.NamingConventions)

                    let result = VariableAnalysisResult.Create(variable)

                    match pattern with
                    | Some p ->
                        let deviceType = inferDeviceTypeFromName p.DeviceName
                        let apiType = 
                            p.ApiSuffix 
                            |> Option.map (fun api -> inferApiTypeFromName api deviceType)
                            |> Option.defaultValue Command

                        let device = Device.Create(p.DeviceName, deviceType, p.Prefix |> Option.defaultValue "")
                        let api = ApiDefinition.Create(p.ApiSuffix |> Option.defaultValue "DO", apiType, Bool, p.IOType)

                        return { result with 
                                    Pattern = Some p
                                    Device = Some device
                                    Api = Some api
                                    Confidence = p.Confidence }
                    | None ->
                        return { result with 
                                    Issues = ["Could not parse variable name pattern"]
                                    Confidence = 0.1 }
                })
                |> List.toArray
                |> Task.WhenAll

            return results |> Array.toList
        }

        member this.ExtractAreasAsync(variables: RawVariable list) = task {
            let areas = 
                variables
                |> List.choose (fun v ->
                    NamingConvention.GetDefaults()
                    |> List.tryPick (fun conv ->
                        extractNameComponents v.Name conv.Pattern
                        |> Option.bind (fun comp -> comp.TryFind("area"))))
                |> List.distinct
                |> List.map Area.Create

            return areas
        }

        member this.ExtractDevicesAsync(variables: RawVariable list, areas: Area list) = task {
            let defaultConfig = MappingConfiguration.Default(PlcVendor.CreateLSElectric())
            let! analysisResults = (this :> IVariableAnalyzer).AnalyzeVariablesBatchAsync(variables, defaultConfig)
            
            let devices = 
                analysisResults
                |> List.choose (fun result -> result.Device)
                |> List.distinctBy (fun device -> device.Name)
            
            return devices
        }
        
        member this.GenerateApiDefinitionsAsync(devices: Device list) = task {
            let apiDefs = 
                devices
                |> List.collect (fun device ->
                    device.Type.StandardApis
                    |> List.map (fun apiName ->
                        let apiType = inferApiTypeFromName apiName device.Type
                        let dataType = if apiType = Parameter then Int32 else Bool
                        let direction = if apiType = Status || apiType = Feedback then Input else Output
                        ApiDefinition.Create(apiName, apiType, dataType, direction)))
                |> List.distinctBy (fun api -> api.Name)
            
            return apiDefs
        }

/// 명명 규칙 분석기 구현
type NamingAnalyzer(logger: ILogger<NamingAnalyzer>) =
    
    interface INamingAnalyzer with
        member this.ParseVariableName(variableName: string, pattern: string) =
            try
                let regex = Regex(pattern, RegexOptions.IgnoreCase)
                let regexMatch = regex.Match(variableName)

                if regexMatch.Success then
                    let mutable components = Map.empty
                    for groupName in regex.GetGroupNames() do
                        if groupName <> "0" && regexMatch.Groups.[groupName].Success then
                            components <- components.Add(groupName, regexMatch.Groups.[groupName].Value)
                    Some components
                else
                    None
            with
            | ex ->
                logger.LogWarning(ex, "Error parsing variable name: {Name}", variableName)
                None
        
        member this.NormalizeDeviceName(deviceName: string) =
            deviceName.ToUpper().Trim()
        
        member this.NormalizeApiName(apiName: string) =
            apiName.ToUpper().Trim()
        
        member this.ExtractAreaName(variableName: string) =
            // Try common area patterns
            let patterns = [
                @"^([A-Z0-9]+)_.*"
                @"^AREA([0-9]+)_.*"
                @"^LINE([0-9]+)_.*"
                @"^STATION([A-Z0-9]+)_.*"
            ]
            
            patterns
            |> List.tryPick (fun pattern ->
                let regexMatch = Regex.Match(variableName, pattern)
                if regexMatch.Success then Some regexMatch.Groups.[1].Value else None)
        
        member this.ValidateVariableName(variableName: string, conventions: NamingConvention list) =
            if String.IsNullOrWhiteSpace(variableName) then
                ValidationResult.Error("Variable name cannot be empty")
            elif variableName.Length > 100 then
                ValidationResult.Warning("Variable name is too long", suggestion = "Keep variable names under 100 characters")
            elif conventions |> List.exists (fun conv -> Regex.IsMatch(variableName, conv.Pattern)) then
                ValidationResult.Success
            else
                ValidationResult.Warning($"Variable '{variableName}' does not match any naming convention")

/// 변수 분석기 팩토리
module VariableAnalyzerFactory =
    let create (logger: ILogger<VariableAnalyzer>) (configProvider: IConfigurationProvider) : IVariableAnalyzer =
        VariableAnalyzer(logger, configProvider) :> IVariableAnalyzer
    
    let createNamingAnalyzer (logger: ILogger<NamingAnalyzer>) : INamingAnalyzer =
        NamingAnalyzer(logger) :> INamingAnalyzer
