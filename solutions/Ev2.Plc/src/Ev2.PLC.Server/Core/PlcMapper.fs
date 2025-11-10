namespace DSPLCServer.Core

open System
open System.Collections.Concurrent
open System.Text.Json
open Microsoft.Extensions.Logging
open DSPLCServer.Common
open Ev2.PLC.Common.Types

/// Tag mapping configuration for address translation
type TagMapping = {
    /// Logical tag ID (used by client applications)
    LogicalTagId: string
    /// Physical PLC address
    PhysicalAddress: PlcAddress
    /// Data type for the tag
    DataType: PlcDataType
    /// Scaling factor for numeric values (optional)
    ScaleFactor: float option
    /// Offset for numeric values (optional)
    Offset: float option
    /// Description of the tag
    Description: string option
    /// Whether the tag is read-only
    IsReadOnly: bool
    /// Tag group or category
    Group: string option
}

/// Address mapping rule for automatic address translation
type AddressMapping = {
    /// Pattern to match logical addresses (supports wildcards)
    LogicalPattern: string
    /// Physical address template with placeholders
    PhysicalTemplate: string
    /// Data type mapping rules
    DataTypeMapping: Map<string, PlcDataType>
    /// Default data type if not specified
    DefaultDataType: PlcDataType
    /// Scaling configuration
    ScaleFactor: float option
    Offset: float option
}

/// PLC vendor-specific address format configuration
type VendorAddressFormat = {
    /// PLC vendor
    Vendor: PlcVendor
    /// Address format patterns (e.g., "D{0}", "DB{0}.DBW{1}")
    AddressPatterns: Map<PlcDataType, string>
    /// Address validation rules
    ValidationRules: Map<PlcDataType, string * int * int> // Pattern, Min, Max
    /// Default address format
    DefaultPattern: string
}

/// Mapping configuration for a PLC
type PlcMappingConfig = {
    /// PLC identifier
    PlcId: string
    /// PLC vendor
    Vendor: PlcVendor
    /// Tag mappings
    TagMappings: TagMapping list
    /// Address mapping rules
    AddressMappings: AddressMapping list
    /// Vendor-specific address format
    AddressFormat: VendorAddressFormat
    /// Enable automatic address mapping
    EnableAutoMapping: bool
    /// Default data type for unmapped tags
    DefaultDataType: PlcDataType
}

/// Result of tag mapping operation
type MappingResult = {
    /// Original logical tag configuration
    LogicalTag: TagConfiguration
    /// Mapped physical tag configuration
    PhysicalTag: TagConfiguration
    /// Applied mapping configuration
    Mapping: TagMapping option
    /// Whether automatic mapping was used
    IsAutoMapped: bool
    /// Scaling factor applied
    ScaleFactor: float option
    /// Offset applied
    Offset: float option
}

/// Value conversion result
type ValueConversionResult = {
    /// Original value
    OriginalValue: PlcValue
    /// Converted value
    ConvertedValue: PlcValue
    /// Scaling factor applied
    ScaleFactor: float option
    /// Offset applied
    Offset: float option
    /// Whether conversion was applied
    IsConverted: bool
}

/// PLC Mapper - Handles address mapping and data type conversion
type PlcMapper(config: PlcMappingConfig, logger: ILogger<PlcMapper>) =
    
    let mappingCache = ConcurrentDictionary<string, MappingResult>()
    let addressPatternCache = ConcurrentDictionary<string, string>()
    
    // Helper function to check if pattern matches address
    let matchesPattern (pattern: string) (address: string) =
        if pattern.Contains("*") then
            let regexPattern = pattern.Replace("*", ".*")
            Text.RegularExpressions.Regex.IsMatch(address, regexPattern)
        else
            address = pattern
    
    // Helper function to apply template with placeholders
    let applyTemplate (template: string) (logicalAddress: string) =
        // Simple placeholder replacement (can be enhanced)
        template.Replace("{0}", logicalAddress)
    
    // Helper function to get vendor-specific address format
    let getAddressFormat (dataType: PlcDataType) =
        match config.AddressFormat.AddressPatterns.TryGetValue(dataType) with
        | (true, pattern) -> pattern
        | _ -> config.AddressFormat.DefaultPattern
    
    // Helper function to validate address format
    let validateAddress (address: string) (dataType: PlcDataType) =
        match config.AddressFormat.ValidationRules.TryGetValue(dataType) with
        | (true, (pattern, minVal, maxVal)) ->
            let regex = Text.RegularExpressions.Regex(pattern)
            if regex.IsMatch(address) then
                // Extract numeric part and validate range
                let matches = regex.Matches(address)
                if matches.Count > 0 && matches.[0].Groups.Count > 1 then
                    match Int32.TryParse(matches.[0].Groups.[1].Value) with
                    | (true, value) when value >= minVal && value <= maxVal -> Ok ()
                    | (true, value) -> Result.Error $"Address value {value} out of range [{minVal}-{maxVal}]"
                    | _ -> Result.Error "Invalid address format"
                else
                    Ok ()
            else
                Result.Error $"Address does not match pattern {pattern}"
        | _ -> Ok () // No validation rules defined
    
    // Apply scaling and offset to numeric values
    let applyScaling (value: PlcValue) (scaleFactor: float option) (offset: float option) =
        match value, scaleFactor, offset with
        | PlcValue.Int16Value(v), Some scale, Some off -> PlcValue.Int16Value(int16 (float v * scale + off))
        | PlcValue.Int32Value(v), Some scale, Some off -> PlcValue.Int32Value(int32 (float v * scale + off))
        | PlcValue.Float32Value(v), Some scale, Some off -> PlcValue.Float32Value(float32 (float v * scale + off))
        | PlcValue.Float64Value(v), Some scale, Some off -> PlcValue.Float64Value(v * scale + off)
        | PlcValue.Int16Value(v), Some scale, None -> PlcValue.Int16Value(int16 (float v * scale))
        | PlcValue.Int32Value(v), Some scale, None -> PlcValue.Int32Value(int32 (float v * scale))
        | PlcValue.Float32Value(v), Some scale, None -> PlcValue.Float32Value(float32 (float v * scale))
        | PlcValue.Float64Value(v), Some scale, None -> PlcValue.Float64Value(v * scale)
        | PlcValue.Int16Value(v), None, Some off -> PlcValue.Int16Value(int16 (float v + off))
        | PlcValue.Int32Value(v), None, Some off -> PlcValue.Int32Value(int32 (float v + off))
        | PlcValue.Float32Value(v), None, Some off -> PlcValue.Float32Value(float32 (float v + off))
        | PlcValue.Float64Value(v), None, Some off -> PlcValue.Float64Value(v + off)
        | _ -> value
    
    // Reverse scaling and offset for write operations
    let reverseScaling (value: PlcValue) (scaleFactor: float option) (offset: float option) =
        match value, scaleFactor, offset with
        | PlcValue.Int16Value(v), Some scale, Some off -> PlcValue.Int16Value(int16 ((float v - off) / scale))
        | PlcValue.Int32Value(v), Some scale, Some off -> PlcValue.Int32Value(int32 ((float v - off) / scale))
        | PlcValue.Float32Value(v), Some scale, Some off -> PlcValue.Float32Value(float32 ((float v - off) / scale))
        | PlcValue.Float64Value(v), Some scale, Some off -> PlcValue.Float64Value((v - off) / scale)
        | PlcValue.Int16Value(v), Some scale, None -> PlcValue.Int16Value(int16 (float v / scale))
        | PlcValue.Int32Value(v), Some scale, None -> PlcValue.Int32Value(int32 (float v / scale))
        | PlcValue.Float32Value(v), Some scale, None -> PlcValue.Float32Value(float32 (float v / scale))
        | PlcValue.Float64Value(v), Some scale, None -> PlcValue.Float64Value(v / scale)
        | PlcValue.Int16Value(v), None, Some off -> PlcValue.Int16Value(int16 (float v - off))
        | PlcValue.Int32Value(v), None, Some off -> PlcValue.Int32Value(int32 (float v - off))
        | PlcValue.Float32Value(v), None, Some off -> PlcValue.Float32Value(float32 (float v - off))
        | PlcValue.Float64Value(v), None, Some off -> PlcValue.Float64Value(v - off)
        | _ -> value
    
    /// Map logical tag to physical tag
    member this.MapLogicalToPhysical(logicalTag: TagConfiguration) : Result<MappingResult, string> =
        try
            // Check cache first
            match mappingCache.TryGetValue(logicalTag.Id) with
            | (true, cached) -> Ok cached
            | _ ->
                // Look for explicit tag mapping
                let explicitMapping = 
                    config.TagMappings 
                    |> List.tryFind (fun m -> m.LogicalTagId = logicalTag.Id)
                
                match explicitMapping with
                | Some mapping ->
                    // Use explicit mapping
                    let physicalTag = TagConfiguration.Create(
                        logicalTag.Id,
                        logicalTag.PlcId,
                        logicalTag.Name,
                        mapping.PhysicalAddress,
                        mapping.DataType
                    )
                    
                    let result = {
                        LogicalTag = logicalTag
                        PhysicalTag = physicalTag
                        Mapping = Some mapping
                        IsAutoMapped = false
                        ScaleFactor = mapping.ScaleFactor
                        Offset = mapping.Offset
                    }
                    
                    mappingCache.TryAdd(logicalTag.Id, result) |> ignore
                    logger.LogDebug("Mapped logical tag {LogicalId} to physical address {PhysicalAddress} using explicit mapping", 
                        logicalTag.Id, mapping.PhysicalAddress.ToString())
                    Ok result
                
                | None when config.EnableAutoMapping ->
                    // Try automatic mapping using rules
                    let autoMapping = 
                        config.AddressMappings
                        |> List.tryFind (fun rule -> matchesPattern rule.LogicalPattern (logicalTag.Address.ToString()))
                    
                    match autoMapping with
                    | Some rule ->
                        let physicalAddressStr = applyTemplate rule.PhysicalTemplate (logicalTag.Address.ToString())
                        let physicalAddress = PlcAddress.Create(physicalAddressStr)
                        
                        // Determine data type
                        let dataType = 
                            match rule.DataTypeMapping.TryGetValue(logicalTag.DataType.ToString()) with
                            | (true, mappedType) -> mappedType
                            | _ -> rule.DefaultDataType
                        
                        // Validate address format
                        match validateAddress physicalAddressStr dataType with
                        | Ok () ->
                            let physicalTag = TagConfiguration.Create(
                                logicalTag.Id,
                                logicalTag.PlcId,
                                logicalTag.Name,
                                physicalAddress,
                                dataType
                            )
                            
                            let result = {
                                LogicalTag = logicalTag
                                PhysicalTag = physicalTag
                                Mapping = None
                                IsAutoMapped = true
                                ScaleFactor = rule.ScaleFactor
                                Offset = rule.Offset
                            }
                            
                            mappingCache.TryAdd(logicalTag.Id, result) |> ignore
                            logger.LogDebug("Auto-mapped logical tag {LogicalId} to physical address {PhysicalAddress}", 
                                logicalTag.Id, physicalAddressStr)
                            Ok result
                        
                        | Result.Error error ->
                            Result.Error $"Address validation failed for auto-mapped tag {logicalTag.Id}: {error}"
                    
                    | None ->
                        // No mapping rule found, use direct mapping
                        let result = {
                            LogicalTag = logicalTag
                            PhysicalTag = logicalTag
                            Mapping = None
                            IsAutoMapped = false
                            ScaleFactor = None
                            Offset = None
                        }
                        
                        mappingCache.TryAdd(logicalTag.Id, result) |> ignore
                        logger.LogTrace("No mapping found for tag {TagId}, using direct mapping", logicalTag.Id)
                        Ok result
                
                | None ->
                    Result.Error $"No mapping configured for tag {logicalTag.Id} and auto-mapping is disabled"
        
        with
        | ex ->
            logger.LogError(ex, "Exception while mapping logical tag {TagId}", logicalTag.Id)
            Result.Error $"Mapping failed: {ex.Message}"
    
    /// Get tag mapping by logical tag ID
    member this.GetTagMapping(logicalTagId: string) : TagMapping option =
        config.TagMappings |> List.tryFind (fun m -> m.LogicalTagId = logicalTagId)
    
    /// Convert value from logical to physical (for write operations)
    member this.ConvertLogicalToPhysical(value: PlcValue, mapping: MappingResult) : ValueConversionResult =
        try
            let convertedValue = reverseScaling value mapping.ScaleFactor mapping.Offset
            let isConverted = convertedValue <> value
            
            {
                OriginalValue = value
                ConvertedValue = convertedValue
                ScaleFactor = mapping.ScaleFactor
                Offset = mapping.Offset
                IsConverted = isConverted
            }
        with
        | ex ->
            logger.LogError(ex, "Exception while converting logical value to physical")
            {
                OriginalValue = value
                ConvertedValue = value
                ScaleFactor = None
                Offset = None
                IsConverted = false
            }
    
    /// Convert value from physical to logical (for read operations)
    member this.ConvertPhysicalToLogical(value: PlcValue, mapping: MappingResult) : ValueConversionResult =
        try
            let convertedValue = applyScaling value mapping.ScaleFactor mapping.Offset
            let isConverted = convertedValue <> value
            
            {
                OriginalValue = value
                ConvertedValue = convertedValue
                ScaleFactor = mapping.ScaleFactor
                Offset = mapping.Offset
                IsConverted = isConverted
            }
        with
        | ex ->
            logger.LogError(ex, "Exception while converting physical value to logical")
            {
                OriginalValue = value
                ConvertedValue = value
                ScaleFactor = None
                Offset = None
                IsConverted = false
            }

/// Factory for creating PLC mappers
type PlcMapperFactory(loggerFactory: ILoggerFactory) =
    
    /// Create mapper from configuration
    member this.CreateMapper(config: PlcMappingConfig) =
        try
            let logger = loggerFactory.CreateLogger<PlcMapper>()
            let mapper = new PlcMapper(config, logger)
            
            logger.LogInformation("Created PLC mapper for {PlcId} with {TagCount} tag mappings", 
                config.PlcId, config.TagMappings.Length)
            Ok mapper
        with
        | ex ->
            let logger = loggerFactory.CreateLogger<PlcMapperFactory>()
            logger.LogError(ex, "Exception while creating PLC mapper for {PlcId}", config.PlcId)
            Result.Error $"Failed to create mapper: {ex.Message}"
    
    /// Create default vendor address format
    member this.CreateDefaultAddressFormat(vendor: PlcVendor) =
        match vendor with
        | PlcVendor.Siemens -> 
            {
                Vendor = vendor
                AddressPatterns = Map [
                    (PlcDataType.Bool, "M{0}")
                    (PlcDataType.Int16, "MW{0}")
                    (PlcDataType.Int32, "MD{0}")
                    (PlcDataType.Float32, "MD{0}")
                    (PlcDataType.String(255), "DB{0}.DBB{1}")
                ]
                ValidationRules = Map [
                    (PlcDataType.Bool, (@"M(\d+)", 0, 65535))
                    (PlcDataType.Int16, (@"MW(\d+)", 0, 65534))
                    (PlcDataType.Int32, (@"MD(\d+)", 0, 65532))
                    (PlcDataType.Float32, (@"MD(\d+)", 0, 65532))
                ]
                DefaultPattern = "MW{0}"
            }
        | PlcVendor.Mitsubishi -> 
            {
                Vendor = vendor
                AddressPatterns = Map [
                    (PlcDataType.Bool, "M{0}")
                    (PlcDataType.Int16, "D{0}")
                    (PlcDataType.Int32, "D{0}")
                    (PlcDataType.Float32, "D{0}")
                    (PlcDataType.String(255), "D{0}")
                ]
                ValidationRules = Map [
                    (PlcDataType.Bool, (@"M(\d+)", 0, 8191))
                    (PlcDataType.Int16, (@"D(\d+)", 0, 12287))
                    (PlcDataType.Int32, (@"D(\d+)", 0, 12286))
                    (PlcDataType.Float32, (@"D(\d+)", 0, 12286))
                ]
                DefaultPattern = "D{0}"
            }
        | PlcVendor.AllenBradley -> 
            {
                Vendor = vendor
                AddressPatterns = Map [
                    (PlcDataType.Bool, "Program:MainProgram.{0}")
                    (PlcDataType.Int16, "Program:MainProgram.{0}")
                    (PlcDataType.Int32, "Program:MainProgram.{0}")
                    (PlcDataType.Float32, "Program:MainProgram.{0}")
                    (PlcDataType.String(255), "Program:MainProgram.{0}")
                ]
                ValidationRules = Map.empty
                DefaultPattern = "Program:MainProgram.{0}"
            }
        | PlcVendor.LSElectric -> 
            {
                Vendor = vendor
                AddressPatterns = Map [
                    (PlcDataType.Bool, "M{0}")
                    (PlcDataType.Int16, "D{0}")
                    (PlcDataType.Int32, "D{0}")
                    (PlcDataType.Float32, "D{0}")
                    (PlcDataType.String(255), "D{0}")
                ]
                ValidationRules = Map [
                    (PlcDataType.Bool, (@"M(\d+)", 0, 65535))
                    (PlcDataType.Int16, (@"D(\d+)", 0, 65535))
                    (PlcDataType.Int32, (@"D(\d+)", 0, 65534))
                    (PlcDataType.Float32, (@"D(\d+)", 0, 65534))
                ]
                DefaultPattern = "D{0}"
            }
        | _ -> 
            {
                Vendor = vendor
                AddressPatterns = Map [
                    (PlcDataType.Bool, "{0}")
                    (PlcDataType.Int16, "{0}")
                    (PlcDataType.Int32, "{0}")
                    (PlcDataType.Float32, "{0}")
                    (PlcDataType.String(255), "{0}")
                ]
                ValidationRules = Map.empty
                DefaultPattern = "{0}"
            }