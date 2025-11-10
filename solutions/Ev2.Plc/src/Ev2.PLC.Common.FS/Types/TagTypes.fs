namespace Ev2.PLC.Common.Types

open System

// ===================================
// Tag Management Types - Universal tag configuration and management for PLC operations
// ===================================

/// PLC address representation - vendor agnostic
type PlcAddress = {
    Raw: string
    DeviceArea: string option
    Index: int option
    BitIndex: int option
    ArraySize: int option
    DataSize: int
    VendorSpecific: Map<string, string>
} with
    static member Create(raw: string, ?deviceArea: string, ?index: int, ?bitIndex: int, ?arraySize: int, ?dataSize: int) = {
        Raw = raw.Trim()
        DeviceArea = deviceArea
        Index = index
        BitIndex = bitIndex
        ArraySize = arraySize
        DataSize = defaultArg dataSize 1
        VendorSpecific = Map.empty
    }

    member this.FullAddress =
        match this.DeviceArea, this.Index, this.BitIndex, this.ArraySize with
        | Some area, Some idx, Some bit, None -> $"{area}{idx}.{bit}"
        | Some area, Some idx, None, Some size -> $"{area}{idx}[{size}]"
        | Some area, Some idx, Some bit, Some size -> $"{area}{idx}.{bit}[{size}]"
        | Some area, Some idx, None, None -> $"{area}{idx}"
        | _ -> this.Raw

    member this.IsBitAddress = this.BitIndex.IsSome
    member this.IsArrayAddress = this.ArraySize.IsSome
    member this.IsValid = not (String.IsNullOrWhiteSpace(this.Raw))

    member this.WithVendorSpecific(key: string, value: string) =
        { this with VendorSpecific = this.VendorSpecific |> Map.add key value }

/// Tag access rights
type TagAccessRights =
    | ReadOnly
    | WriteOnly
    | ReadWrite
    | NoAccess

    member this.CanRead = 
        match this with
        | ReadOnly | ReadWrite -> true
        | WriteOnly | NoAccess -> false

    member this.CanWrite = 
        match this with
        | WriteOnly | ReadWrite -> true
        | ReadOnly | NoAccess -> false

/// Tag update mode
type TagUpdateMode =
    | OnScan          // Updated during regular scans
    | OnChange        // Updated only when value changes
    | OnDemand        // Updated only when explicitly requested
    | Continuous      // Updated as fast as possible
    | Scheduled of interval: TimeSpan // Updated at specific intervals

    member this.RequiresScheduling =
        match this with
        | Scheduled _ -> true
        | _ -> false

    member this.UpdateInterval =
        match this with
        | Scheduled interval -> Some interval
        | _ -> None

/// Tag validation rules
type TagValidationRule =
    | Range of min: PlcValue * max: PlcValue
    | EnumValues of allowedValues: PlcValue list
    | Pattern of regex: string
    | Custom of validatorName: string * parameters: Map<string, string>

    member this.ValidatorType =
        match this with
        | Range _ -> "Range"
        | EnumValues _ -> "Enum"
        | Pattern _ -> "Pattern"
        | Custom (name, _) -> name

/// Tag configuration
type TagConfiguration = {
    Id: string
    PlcId: string
    Name: string
    Description: string option
    Address: PlcAddress
    DataType: PlcDataType
    AccessRights: TagAccessRights
    UpdateMode: TagUpdateMode
    IsEnabled: bool
    
    // Value processing
    ScaleFactor: float option
    Offset: float option
    Unit: string option
    
    // Quality and validation
    ValidationRules: TagValidationRule list
    QualityThreshold: int // Minimum quality score (0-100)
    
    // Grouping and organization
    Group: string option
    Category: string option
    Priority: ScanPriority
    
    // Advanced settings
    DeadBand: float option
    MaxAge: TimeSpan option
    RetentionPeriod: TimeSpan option
    
    // Metadata
    Tags: string list // User-defined tags for filtering
    CustomProperties: Map<string, string>
    
    // Timestamps
    CreatedAt: DateTime
    UpdatedAt: DateTime
    CreatedBy: string option
    UpdatedBy: string option
} with
    static member Create(id: string, plcId: string, name: string, address: PlcAddress, dataType: PlcDataType) = {
        Id = id
        PlcId = plcId
        Name = name
        Description = None
        Address = address
        DataType = dataType
        AccessRights = ReadWrite
        UpdateMode = OnScan
        IsEnabled = true
        ScaleFactor = None
        Offset = None
        Unit = None
        ValidationRules = []
        QualityThreshold = 70
        Group = None
        Category = None
        Priority = Normal
        DeadBand = None
        MaxAge = None
        RetentionPeriod = None
        Tags = []
        CustomProperties = Map.empty
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
        CreatedBy = None
        UpdatedBy = None
    }

    member this.IsReadable = this.AccessRights.CanRead && this.IsEnabled
    member this.IsWritable = this.AccessRights.CanWrite && this.IsEnabled
    member this.RequiresValidation = not this.ValidationRules.IsEmpty

    member this.ApplyScaling(value: PlcValue) =
        PlcValue.applyScaling this.ScaleFactor this.Offset value

    member this.ValidateValue(value: PlcValue) : Result<PlcValue, string> =
        if not this.RequiresValidation then
            Ok value
        else
            let results = this.ValidationRules |> List.map (fun rule -> this.ValidateWithRule(rule, value))
            let failures = results |> List.choose (function Result.Error e -> Some e | Ok _ -> None)
            
            if failures.IsEmpty then Ok value
            else Result.Error (String.concat "; " failures)

    member private this.ValidateWithRule(rule: TagValidationRule, value: PlcValue) : Result<PlcValue, string> =
        match rule with
        | Range (minVal, maxVal) ->
            if PlcValue.isInRange (Some minVal) (Some maxVal) value then
                Ok value
            else
                Result.Error $"Value {value} is outside range [{minVal}..{maxVal}]"
        
        | EnumValues allowedValues ->
            if allowedValues |> List.contains value then
                Ok value
            else
                Result.Error $"Value {value} is not in allowed values: {allowedValues}"
        
        | Pattern regex ->
            try
                let pattern = System.Text.RegularExpressions.Regex(regex)
                if pattern.IsMatch(value.ToString()) then
                    Ok value
                else
                    Result.Error $"Value {value} does not match pattern {regex}"
            with
            | ex -> Result.Error $"Pattern validation failed: {ex.Message}"
        
        | Custom (validatorName, parameters) ->
            Result.Error $"Custom validator '{validatorName}' not implemented"

    member this.WithGroup(group: string) = { this with Group = Some group; UpdatedAt = DateTime.UtcNow }
    member this.WithCategory(category: string) = { this with Category = Some category; UpdatedAt = DateTime.UtcNow }
    member this.WithDescription(description: string) = { this with Description = Some description; UpdatedAt = DateTime.UtcNow }
    member this.WithUnit(unit: string) = { this with Unit = Some unit; UpdatedAt = DateTime.UtcNow }
    member this.WithScaling(scale: float, offset: float) = { this with ScaleFactor = Some scale; Offset = Some offset; UpdatedAt = DateTime.UtcNow }

    member this.AddTag(tag: string) = 
        if this.Tags |> List.contains tag then this
        else { this with Tags = tag :: this.Tags; UpdatedAt = DateTime.UtcNow }

    member this.RemoveTag(tag: string) =
        { this with Tags = this.Tags |> List.filter ((<>) tag); UpdatedAt = DateTime.UtcNow }

    member this.SetCustomProperty(key: string, value: string) =
        { this with CustomProperties = this.CustomProperties |> Map.add key value; UpdatedAt = DateTime.UtcNow }

    member this.Enable() = { this with IsEnabled = true; UpdatedAt = DateTime.UtcNow }
    member this.Disable() = { this with IsEnabled = false; UpdatedAt = DateTime.UtcNow }

/// Tag group for organizing related tags
type TagGroup = {
    Name: string
    PlcId: string
    Description: string option
    Tags: string list // Tag IDs
    UpdateMode: TagUpdateMode
    Priority: ScanPriority
    IsEnabled: bool
    MaxConcurrentOperations: int
    Metadata: Map<string, string>
    CreatedAt: DateTime
    UpdatedAt: DateTime
} with
    static member Create(name: string, plcId: string, ?description: string) = {
        Name = name
        PlcId = plcId
        Description = description
        Tags = []
        UpdateMode = OnScan
        Priority = Normal
        IsEnabled = true
        MaxConcurrentOperations = 10
        Metadata = Map.empty
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

    member this.TagCount = this.Tags.Length
    member this.IsEmpty = this.Tags.IsEmpty

    member this.AddTag(tagId: string) =
        if this.Tags |> List.contains tagId then this
        else { this with Tags = tagId :: this.Tags; UpdatedAt = DateTime.UtcNow }

    member this.RemoveTag(tagId: string) =
        { this with Tags = this.Tags |> List.filter ((<>) tagId); UpdatedAt = DateTime.UtcNow }

    member this.SetMetadata(key: string, value: string) =
        { this with Metadata = this.Metadata |> Map.add key value; UpdatedAt = DateTime.UtcNow }

/// Tag subscription for real-time updates
type TagSubscription = {
    SubscriptionId: string
    TagId: string
    SubscriberId: string
    UpdateMode: TagUpdateMode
    QualityFilter: DataQuality option
    ValueFilter: PlcValue option // Only notify if value differs
    DeadBand: float option
    MaxUpdateRate: TimeSpan option
    IsActive: bool
    LastNotification: DateTime option
    NotificationCount: int64
    CreatedAt: DateTime
} with
    static member Create(subscriptionId: string, tagId: string, subscriberId: string) = {
        SubscriptionId = subscriptionId
        TagId = tagId
        SubscriberId = subscriberId
        UpdateMode = OnChange
        QualityFilter = None
        ValueFilter = None
        DeadBand = None
        MaxUpdateRate = None
        IsActive = true
        LastNotification = None
        NotificationCount = 0L
        CreatedAt = DateTime.UtcNow
    }

    member this.ShouldNotify(newValue: PlcValue, newQuality: DataQuality) =
        if not this.IsActive then false
        else
            let qualityOk = 
                match this.QualityFilter with
                | Some requiredQuality -> newQuality = requiredQuality
                | None -> true

            let valueChanged = 
                match this.ValueFilter with
                | Some lastValue -> 
                    match this.DeadBand with
                    | Some deadBand -> not (PlcValue.areEqual (Some deadBand) lastValue newValue)
                    | None -> lastValue <> newValue
                | None -> true

            let rateLimited = 
                match this.MaxUpdateRate, this.LastNotification with
                | Some maxRate, Some lastTime -> 
                    DateTime.UtcNow < lastTime.Add(maxRate)
                | _ -> false

            qualityOk && valueChanged && not rateLimited

    member this.RecordNotification(value: PlcValue) =
        { this with 
            LastNotification = Some DateTime.UtcNow
            ValueFilter = Some value
            NotificationCount = this.NotificationCount + 1L }

    member this.Activate() = { this with IsActive = true }
    member this.Deactivate() = { this with IsActive = false }

/// Tag template for creating similar tags
type TagTemplate = {
    Name: string
    Description: string option
    DataType: PlcDataType
    AccessRights: TagAccessRights
    UpdateMode: TagUpdateMode
    ValidationRules: TagValidationRule list
    DefaultScaleFactor: float option
    DefaultOffset: float option
    DefaultUnit: string option
    DefaultCategory: string option
    DefaultPriority: ScanPriority
    Parameters: Map<string, string> // Template parameters
} with
    static member Create(name: string, dataType: PlcDataType) = {
        Name = name
        Description = None
        DataType = dataType
        AccessRights = ReadWrite
        UpdateMode = OnScan
        ValidationRules = []
        DefaultScaleFactor = None
        DefaultOffset = None
        DefaultUnit = None
        DefaultCategory = None
        DefaultPriority = Normal
        Parameters = Map.empty
    }

    member this.CreateTag(id: string, plcId: string, tagName: string, address: PlcAddress, ?parameters: Map<string, string>) =
        let tagParams = defaultArg parameters Map.empty
        let resolvedName = this.ResolveParameterPlaceholders(tagName, tagParams)
        
        let tag = TagConfiguration.Create(id, plcId, resolvedName, address, this.DataType)
        
        { tag with
            Description = this.Description
            AccessRights = this.AccessRights
            UpdateMode = this.UpdateMode
            ValidationRules = this.ValidationRules
            ScaleFactor = this.DefaultScaleFactor
            Offset = this.DefaultOffset
            Unit = this.DefaultUnit
            Category = this.DefaultCategory
            Priority = this.DefaultPriority }

    member private this.ResolveParameterPlaceholders(text: string, parameters: Map<string, string>) =
        parameters |> Map.fold (fun acc key value -> 
            acc.Replace($"{{{key}}}", value)) text

/// Tag import/export format
type TagExportFormat =
    | CSV
    | JSON
    | XML
    | Excel

/// Tag bulk operation
type TagBulkOperation =
    | Create of tags: TagConfiguration list
    | Update of updates: (string * Map<string, obj>) list // TagId * property updates
    | Delete of tagIds: string list
    | Enable of tagIds: string list
    | Disable of tagIds: string list
    | ChangeGroup of tagIds: string list * newGroup: string
    | ApplyTemplate of tagIds: string list * template: TagTemplate

/// Tag query filters
type TagQuery = {
    PlcIds: string list option
    Groups: string list option
    Categories: string list option
    DataTypes: PlcDataType list option
    AccessRights: TagAccessRights list option
    UpdateModes: TagUpdateMode list option
    IsEnabled: bool option
    Tags: string list option // User-defined tags
    NamePattern: string option
    CreatedAfter: DateTime option
    CreatedBefore: DateTime option
    UpdatedAfter: DateTime option
    UpdatedBefore: DateTime option
    CustomPropertyFilters: Map<string, string>
} with
    static member Empty = {
        PlcIds = None
        Groups = None
        Categories = None
        DataTypes = None
        AccessRights = None
        UpdateModes = None
        IsEnabled = None
        Tags = None
        NamePattern = None
        CreatedAfter = None
        CreatedBefore = None
        UpdatedAfter = None
        UpdatedBefore = None
        CustomPropertyFilters = Map.empty
    }

    static member ForPlc(plcId: string) = 
        { TagQuery.Empty with PlcIds = Some [plcId] }

    static member ForGroup(group: string) = 
        { TagQuery.Empty with Groups = Some [group] }

    static member EnabledOnly() = 
        { TagQuery.Empty with IsEnabled = Some true }

/// Module for working with tags
module Tag =

    /// Generate unique tag ID
    let generateId (plcId: string) (name: string) =
        let cleanName = name.Replace(" ", "_").Replace(".", "_")
        let guid = System.Guid.NewGuid().ToString("N").[..7]
        $"{plcId}_{cleanName}_{guid}"

    /// Parse address string to PlcAddress
    let parseAddress (addressString: string) =
        // Basic parsing - can be extended for vendor-specific formats
        let cleaned = addressString.Trim()
        
        // Try to extract device area and index
        let deviceAreaMatch = System.Text.RegularExpressions.Regex.Match(cleaned, @"^([A-Za-z]+)(\d+)(?:\.(\d+))?(?:\[(\d+)\])?$")
        
        if deviceAreaMatch.Success then
            let deviceArea = deviceAreaMatch.Groups.[1].Value
            let index = Int32.Parse(deviceAreaMatch.Groups.[2].Value)
            let bitIndex = 
                if deviceAreaMatch.Groups.[3].Success then Some (Int32.Parse(deviceAreaMatch.Groups.[3].Value))
                else None
            let arraySize = 
                if deviceAreaMatch.Groups.[4].Success then Some (Int32.Parse(deviceAreaMatch.Groups.[4].Value))
                else None
            
            PlcAddress.Create(cleaned, ?deviceArea = Some deviceArea, ?index = Some index, ?bitIndex = bitIndex, ?arraySize = arraySize)
        else
            PlcAddress.Create(cleaned)

    /// Validate tag configuration
    let validateConfiguration (tag: TagConfiguration) : Result<unit, string> =
        let errors = [
            if String.IsNullOrWhiteSpace(tag.Id) then "Tag ID cannot be empty"
            if String.IsNullOrWhiteSpace(tag.PlcId) then "PLC ID cannot be empty"
            if String.IsNullOrWhiteSpace(tag.Name) then "Tag name cannot be empty"
            if not tag.Address.IsValid then "Tag address is invalid"
            if tag.QualityThreshold < 0 || tag.QualityThreshold > 100 then "Quality threshold must be between 0 and 100"
        ]
        
        if errors.IsEmpty then Ok ()
        else Result.Error (String.concat "; " errors)

    /// Apply tag query filters
    let applyQuery (query: TagQuery) (tags: TagConfiguration list) =
        tags |> List.filter (fun tag ->
            let plcMatches = 
                match query.PlcIds with
                | Some plcIds -> plcIds |> List.contains tag.PlcId
                | None -> true

            let groupMatches = 
                match query.Groups, tag.Group with
                | Some groups, Some group -> groups |> List.contains group
                | Some _, None -> false
                | None, _ -> true

            let categoryMatches = 
                match query.Categories, tag.Category with
                | Some categories, Some category -> categories |> List.contains category
                | Some _, None -> false
                | None, _ -> true

            let dataTypeMatches = 
                match query.DataTypes with
                | Some dataTypes -> dataTypes |> List.contains tag.DataType
                | None -> true

            let accessMatches = 
                match query.AccessRights with
                | Some accessRights -> accessRights |> List.contains tag.AccessRights
                | None -> true

            let updateModeMatches = 
                match query.UpdateModes with
                | Some updateModes -> updateModes |> List.contains tag.UpdateMode
                | None -> true

            let enabledMatches = 
                match query.IsEnabled with
                | Some enabled -> tag.IsEnabled = enabled
                | None -> true

            let tagMatches = 
                match query.Tags with
                | Some queryTags -> queryTags |> List.exists (fun t -> tag.Tags |> List.contains t)
                | None -> true

            let nameMatches = 
                match query.NamePattern with
                | Some pattern -> 
                    try
                        System.Text.RegularExpressions.Regex.IsMatch(tag.Name, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    with
                    | _ -> false
                | None -> true

            let createdAfterMatches = 
                match query.CreatedAfter with
                | Some after -> tag.CreatedAt >= after
                | None -> true

            let createdBeforeMatches = 
                match query.CreatedBefore with
                | Some before -> tag.CreatedAt <= before
                | None -> true

            plcMatches && groupMatches && categoryMatches && dataTypeMatches && 
            accessMatches && updateModeMatches && enabledMatches && tagMatches && 
            nameMatches && createdAfterMatches && createdBeforeMatches)

    /// Group tags by specified property
    let groupBy (groupFunc: TagConfiguration -> string) (tags: TagConfiguration list) =
        tags |> List.groupBy groupFunc

    /// Calculate tag statistics
    let calculateStatistics (tags: TagConfiguration list) =
        let totalCount = tags.Length
        let enabledCount = tags |> List.filter (_.IsEnabled) |> List.length
        let disabledCount = totalCount - enabledCount
        
        let groupStats = tags |> List.groupBy (fun t -> t.Group |> Option.defaultValue "Ungrouped")
        let categoryStats = tags |> List.groupBy (fun t -> t.Category |> Option.defaultValue "Uncategorized")
        let dataTypeStats = tags |> List.groupBy (_.DataType)
        
        {|
            TotalCount = totalCount
            EnabledCount = enabledCount
            DisabledCount = disabledCount
            GroupCounts = groupStats |> List.map (fun (g, ts) -> (g, ts.Length))
            CategoryCounts = categoryStats |> List.map (fun (c, ts) -> (c, ts.Length))
            DataTypeCounts = dataTypeStats |> List.map (fun (dt, ts) -> (dt.ToString(), ts.Length))
        |}

/// Module for tag templates
module TagTemplate =

    /// Create template from existing tag
    let fromTag (tag: TagConfiguration) (templateName: string) = {
        Name = templateName
        Description = tag.Description
        DataType = tag.DataType
        AccessRights = tag.AccessRights
        UpdateMode = tag.UpdateMode
        ValidationRules = tag.ValidationRules
        DefaultScaleFactor = tag.ScaleFactor
        DefaultOffset = tag.Offset
        DefaultUnit = tag.Unit
        DefaultCategory = tag.Category
        DefaultPriority = tag.Priority
        Parameters = Map.empty
    }

    /// Create common templates
    let analogInput = TagTemplate.Create("AnalogInput", PlcDataType.Float32) |> fun t ->
        { t with AccessRights = ReadOnly; DefaultUnit = Some "V"; DefaultCategory = Some "Analog" }

    let analogOutput = TagTemplate.Create("AnalogOutput", PlcDataType.Float32) |> fun t ->
        { t with AccessRights = ReadWrite; DefaultUnit = Some "V"; DefaultCategory = Some "Analog" }

    let digitalInput = TagTemplate.Create("DigitalInput", PlcDataType.Bool) |> fun t ->
        { t with AccessRights = ReadOnly; DefaultCategory = Some "Digital" }

    let digitalOutput = TagTemplate.Create("DigitalOutput", PlcDataType.Bool) |> fun t ->
        { t with AccessRights = ReadWrite; DefaultCategory = Some "Digital" }

    let counter = TagTemplate.Create("Counter", PlcDataType.Int32) |> fun t ->
        { t with AccessRights = ReadOnly; DefaultCategory = Some "Counter" }

    let timer = TagTemplate.Create("Timer", PlcDataType.Int32) |> fun t ->
        { t with AccessRights = ReadWrite; DefaultUnit = Some "ms"; DefaultCategory = Some "Timer" }