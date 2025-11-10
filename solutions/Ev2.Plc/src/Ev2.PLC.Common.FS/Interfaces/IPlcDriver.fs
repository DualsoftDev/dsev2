namespace Ev2.PLC.Common.Interfaces

open System
open System.Threading.Tasks
open Ev2.PLC.Common.Types

// ===================================
// Universal PLC Driver Interfaces - Vendor-agnostic interfaces for PLC operations
// ===================================

/// Core PLC driver interface - must be implemented by all PLC drivers
type IPlcDriver =
    /// Unique identifier for this PLC instance
    abstract member PlcId: string
    
    /// Current connection status
    abstract member PlcConnectionStatus: PlcConnectionStatus
    
    /// Connection configuration and metrics
    abstract member ConnectionInfo: ConnectionInfo
    
    /// Current diagnostics information
    abstract member Diagnostics: PlcDiagnostics
    
    /// Driver capabilities
    abstract member Capabilities: DriverCapabilities
    
    // === Connection Management ===
    
    /// Establish connection to the PLC
    abstract member ConnectAsync: unit -> Task<Result<unit, string>>
    
    /// Disconnect from the PLC
    abstract member DisconnectAsync: unit -> Task<unit>
    
    /// Test connection health
    abstract member HealthCheckAsync: unit -> Task<bool>
    
    /// Reset connection (disconnect and reconnect)
    abstract member ResetConnectionAsync: unit -> Task<Result<unit, string>>
    
    // === Single Tag Operations ===
    
    /// Read a single tag value
    abstract member ReadTagAsync: tagConfig: TagConfiguration -> Task<Result<ScanResult, string>>
    
    /// Write a single tag value
    abstract member WriteTagAsync: tagConfig: TagConfiguration * value: PlcValue -> Task<Result<unit, string>>
    
    /// Read multiple tags in a single operation
    abstract member ReadTagsAsync: tagConfigs: TagConfiguration list -> Task<Result<ScanBatch, string>>
    
    /// Write multiple tags in a single operation
    abstract member WriteTagsAsync: tagValues: (TagConfiguration * PlcValue) list -> Task<Result<unit, string>>
    
    // === Batch Operations ===
    
    /// Execute a scan request
    abstract member ExecuteScanAsync: request: ScanRequest -> Task<Result<ScanBatch, string>>
    
    /// Execute multiple scan requests
    abstract member ExecuteBatchScanAsync: requests: ScanRequest list -> Task<Result<ScanBatch list, string>>
    
    // === Tag Management ===
    
    /// Validate tag configuration for this driver
    abstract member ValidateTagConfiguration: tagConfig: TagConfiguration -> Result<unit, string>
    
    /// Get supported data types for this driver
    abstract member GetSupportedDataTypes: unit -> PlcDataType list
    
    /// Parse address string to PlcAddress for this vendor
    abstract member ParseAddress: addressString: string -> Result<PlcAddress, string>
    
    /// Validate address for this vendor
    abstract member ValidateAddress: address: PlcAddress -> Result<unit, string>
    
    // === Subscription Management ===
    
    /// Subscribe to tag value changes
    abstract member SubscribeAsync: subscription: TagSubscription -> Task<Result<unit, string>>
    
    /// Unsubscribe from tag value changes
    abstract member UnsubscribeAsync: subscriptionId: string -> Task<Result<unit, string>>
    
    /// Get all active subscriptions
    abstract member GetSubscriptions: unit -> TagSubscription list
    
    // === Diagnostics and Monitoring ===
    
    /// Update diagnostic information
    abstract member UpdateDiagnosticsAsync: unit -> Task<unit>
    
    /// Get current performance metrics
    abstract member GetPerformanceMetrics: unit -> PerformanceMetrics
    
    /// Get system information from PLC
    abstract member GetSystemInfoAsync: unit -> Task<Result<PlcSystemInfo, string>>
    
    // === Events ===
    
    /// Raised when connection status changes
    [<CLIEvent>]
    abstract member ConnectionStateChanged: IEvent<ConnectionStateChangedEvent>
    
    /// Raised when tag values change (for subscriptions)
    [<CLIEvent>]
    abstract member TagValueChanged: IEvent<ScanResult>
    
    /// Raised when scan operations complete
    [<CLIEvent>]
    abstract member ScanCompleted: IEvent<ScanBatch>
    
    /// Raised when diagnostic events occur
    [<CLIEvent>]
    abstract member DiagnosticEvent: IEvent<DiagnosticEvent>
    
    /// Raised when errors occur
    [<CLIEvent>]
    abstract member ErrorOccurred: IEvent<string * exn option>

/// Advanced PLC driver interface for drivers with extended capabilities
type IAdvancedPlcDriver =
    inherit IPlcDriver
    
    // === Advanced Diagnostics ===
    
    /// Get detailed diagnostic information
    abstract member GetDetailedDiagnosticsAsync: unit -> Task<Result<Map<string, PlcValue>, string>>
    
    /// Get CPU and system status
    abstract member GetCpuStatusAsync: unit -> Task<Result<Map<string, PlcValue>, string>>
    
    /// Get memory usage information
    abstract member GetMemoryUsageAsync: unit -> Task<Result<Map<string, PlcValue>, string>>
    
    /// Get I/O module status
    abstract member GetIOStatusAsync: unit -> Task<Result<Map<string, PlcValue>, string>>
    
    // === Advanced Data Operations ===
    
    /// Read structure/UDT tags with field access
    abstract member ReadStructureAsync: tagConfig: TagConfiguration * fieldPaths: string list -> Task<Result<Map<string, PlcValue>, string>>
    
    /// Write structure/UDT tags with field access
    abstract member WriteStructureAsync: tagConfig: TagConfiguration * fieldValues: Map<string, PlcValue> -> Task<Result<unit, string>>
    
    /// Read array elements with indexing
    abstract member ReadArrayAsync: tagConfig: TagConfiguration * startIndex: int * count: int -> Task<Result<PlcValue[], string>>
    
    /// Write array elements with indexing
    abstract member WriteArrayAsync: tagConfig: TagConfiguration * startIndex: int * values: PlcValue[] -> Task<Result<unit, string>>
    
    /// Read bit-level data
    abstract member ReadBitsAsync: tagConfig: TagConfiguration * bitOffset: int * bitCount: int -> Task<Result<bool[], string>>
    
    /// Write bit-level data
    abstract member WriteBitsAsync: tagConfig: TagConfiguration * bitOffset: int * values: bool[] -> Task<Result<unit, string>>
    
    // === Program Control ===
    
    /// Start PLC program execution
    abstract member StartProgramAsync: unit -> Task<Result<unit, string>>
    
    /// Stop PLC program execution
    abstract member StopProgramAsync: unit -> Task<Result<unit, string>>
    
    /// Get PLC operating mode
    abstract member GetOperatingModeAsync: unit -> Task<Result<string, string>>
    
    /// Set PLC operating mode
    abstract member SetOperatingModeAsync: mode: string -> Task<Result<unit, string>>
    
    // === File Operations ===
    
    /// Upload file to PLC
    abstract member UploadFileAsync: remotePath: string * data: byte[] -> Task<Result<unit, string>>
    
    /// Download file from PLC
    abstract member DownloadFileAsync: remotePath: string -> Task<Result<byte[], string>>
    
    /// List files on PLC
    abstract member ListFilesAsync: remotePath: string -> Task<Result<string list, string>>
    
    /// Delete file on PLC
    abstract member DeleteFileAsync: remotePath: string -> Task<Result<unit, string>>
    
    // === Security and Authentication ===
    
    /// Authenticate with PLC
    abstract member AuthenticateAsync: credentials: Map<string, string> -> Task<Result<unit, string>>
    
    /// Check current authentication status
    abstract member GetAuthenticationStatusAsync: unit -> Task<Result<bool, string>>


/// PLC driver factory interface
type IPlcDriverFactory =
    /// Get list of supported vendors
    abstract member SupportedVendors: string list
    
    /// Check if vendor is supported
    abstract member IsVendorSupported: vendor: string -> bool
    
    /// Create driver instance for specified vendor and configuration
    abstract member CreateDriver: plcId: string * vendor: string * connectionConfig: ConnectionConfig -> Result<IPlcDriver, string>
    
    /// Create advanced driver instance (if supported)
    abstract member CreateAdvancedDriver: plcId: string * vendor: string * connectionConfig: ConnectionConfig -> Result<IAdvancedPlcDriver, string>
    
    /// Get driver capabilities for vendor
    abstract member GetCapabilities: vendor: string -> Result<DriverCapabilities, string>
    
    /// Parse connection string and create driver
    abstract member CreateDriverFromConnectionString: plcId: string * vendor: string * connectionString: string -> Result<IPlcDriver, string>
    
    /// Validate connection configuration for vendor
    abstract member ValidateConfiguration: vendor: string * connectionConfig: ConnectionConfig -> Result<unit, string>

/// Connection management interface
type IConnectionManager =
    /// Current connection status
    abstract member Status: PlcConnectionStatus
    
    /// Connection configuration
    abstract member Configuration: ConnectionConfig
    
    /// Connection metrics
    abstract member Metrics: ConnectionMetrics
    
    /// Establish connection
    abstract member ConnectAsync: unit -> Task<Result<unit, string>>
    
    /// Disconnect
    abstract member DisconnectAsync: unit -> Task<unit>
    
    /// Test connection
    abstract member TestConnectionAsync: unit -> Task<bool>
    
    /// Reset connection
    abstract member ResetAsync: unit -> Task<Result<unit, string>>
    
    /// Update connection metrics
    abstract member UpdateMetrics: updateFn: (ConnectionMetrics -> ConnectionMetrics) -> unit
    
    /// Connection state changed event
    [<CLIEvent>]
    abstract member StateChanged: IEvent<ConnectionStateChangedEvent>

/// Protocol handler interface for vendor-specific protocol implementations
type IProtocolHandler =
    /// Protocol name and version
    abstract member ProtocolName: string
    abstract member ProtocolVersion: string
    
    /// Encode request message
    abstract member EncodeRequest: operation: ScanOperation * address: PlcAddress * dataType: PlcDataType -> Result<byte[], string>
    
    /// Decode response message
    abstract member DecodeResponse: responseData: byte[] * expectedDataType: PlcDataType -> Result<PlcValue, string>
    
    /// Validate message format
    abstract member ValidateMessage: messageData: byte[] -> Result<unit, string>
    
    /// Calculate message checksum/CRC
    abstract member CalculateChecksum: messageData: byte[] -> byte[]
    
    /// Get protocol-specific timeout
    abstract member GetTimeout: operation: ScanOperation -> TimeSpan
    
    /// Handle protocol-specific errors
    abstract member HandleProtocolError: errorData: byte[] -> Result<string, string>

/// Tag processor interface for tag-related operations
type ITagProcessor =
    /// Process scan results and apply transformations
    abstract member ProcessScanResult: result: ScanResult * tagConfig: TagConfiguration -> ScanResult
    
    /// Validate tag value against configuration
    abstract member ValidateTagValue: value: PlcValue * tagConfig: TagConfiguration -> Result<PlcValue, string>
    
    /// Apply scaling and transformation
    abstract member ApplyTransformation: value: PlcValue * tagConfig: TagConfiguration -> PlcValue
    
    /// Check value against deadband
    abstract member CheckDeadband: currentValue: PlcValue * newValue: PlcValue * deadband: float option -> bool
    
    /// Process subscription notification
    abstract member ProcessSubscriptionUpdate: subscription: TagSubscription * newValue: PlcValue * quality: DataQuality -> bool

/// Diagnostic provider interface
type IDiagnosticProvider =
    /// Get current diagnostics
    abstract member GetDiagnostics: unit -> PlcDiagnostics
    
    /// Update diagnostics
    abstract member UpdateDiagnosticsAsync: unit -> Task<unit>
    
    /// Add diagnostic message
    abstract member AddMessage: message: DiagnosticMessage -> unit
    
    /// Resolve diagnostic message
    abstract member ResolveMessage: messageId: string * resolvedBy: string option -> unit
    
    /// Get performance metrics
    abstract member GetPerformanceMetrics: unit -> PerformanceMetrics
    
    /// Update performance metrics
    abstract member UpdatePerformanceMetrics: updateFn: (PerformanceMetrics -> PerformanceMetrics) -> unit
    
    /// Diagnostic event
    [<CLIEvent>]
    abstract member DiagnosticEvent: IEvent<DiagnosticEvent>

/// Module for working with driver interfaces
module DriverInterface =
    
    /// Create a basic driver capabilities descriptor
    let createBasicCapabilities (name: string) (version: string) (vendors: string list) = {
        DriverCapabilities.Default with
            Name = name
            Version = version
            SupportedVendors = vendors
            SupportsRead = true
            SupportsWrite = true
            SupportsTCP = true
    }
    
    /// Check if driver supports operation
    let supportsOperation (capabilities: DriverCapabilities) (operation: ScanOperation) =
        match operation with
        | ScanOperation.Read -> capabilities.SupportsRead
        | ScanOperation.Write _ -> capabilities.SupportsWrite
        | ScanOperation.ReadWrite _ -> capabilities.SupportsRead && capabilities.SupportsWrite
    
    /// Check if driver supports data type
    let supportsDataType (capabilities: DriverCapabilities) (dataType: PlcDataType) =
        capabilities.SupportedDataTypes |> List.contains dataType || capabilities.SupportedDataTypes.IsEmpty
    
    /// Validate scan request against driver capabilities
    let validateScanRequest (capabilities: DriverCapabilities) (request: ScanRequest) : Result<unit, string> =
        let errors = [
            if not (supportsOperation capabilities request.Operation) then
                "Operation not supported by driver"
            if request.TagIds.Length > capabilities.MaxTagsPerRequest then
                $"Too many tags in request (max: {capabilities.MaxTagsPerRequest})"
        ]
        
        if errors.IsEmpty then Ok ()
        else Result.Error (String.concat "; " errors)
    
    /// Create driver result wrapper
    let wrapResult<'T> (operation: string) (result: Result<'T, string>) : Result<'T, string> =
        match result with
        | Ok value -> Ok value
        | Result.Error error -> Result.Error $"{operation} failed: {error}"
    
    /// Create async result wrapper
    let wrapAsyncResult<'T> (operation: string) (taskResult: Task<Result<'T, string>>) =
        task {
            let! result = taskResult
            return wrapResult operation result
        }