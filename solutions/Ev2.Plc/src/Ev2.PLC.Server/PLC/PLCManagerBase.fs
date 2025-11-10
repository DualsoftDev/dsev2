namespace DSPLCServer.PLC

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open DSPLCServer.Common
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Interfaces

/// Server-specific PLC Manager that wraps the universal IPlcDriver interface
type ServerPlcManager(config: PlcServerConfig, driver: IPlcDriver, logger: ILogger<ServerPlcManager>) =
    
    let mutable isDisposed = false
    let serverStartTime = DateTime.UtcNow
    
    // Events
    let connectionStateChangedEvent = Event<string * PlcConnectionStatus>()
    let scanCompletedEvent = Event<string * ServerScanStatistics>()
    let errorOccurredEvent = Event<string * string>()
    
    // Subscribe to driver events
    do
        driver.ConnectionStateChanged.Add(fun evt ->
            connectionStateChangedEvent.Trigger(config.PlcId, evt.NewStatus)
            logger.LogInformation("PLC {PlcId} connection state changed from {OldStatus} to {NewStatus}", 
                config.PlcId, evt.OldStatus, evt.NewStatus))
        
        driver.ScanCompleted.Add(fun batch ->
            let stats = ServerScanStatistics.FromScanBatch(batch)
            scanCompletedEvent.Trigger(config.PlcId, stats)
            logger.LogDebug("PLC {PlcId} scan completed: {SuccessfulTags}/{TotalTags} tags successful", 
                config.PlcId, stats.SuccessfulTags, stats.TotalTags))
        
        driver.ErrorOccurred.Add(fun (error, ex) ->
            errorOccurredEvent.Trigger(config.PlcId, error)
            logger.LogError("PLC {PlcId} error: {Error}", config.PlcId, error))
    
    /// PLC configuration
    member this.Config = config
    
    /// Underlying driver
    member this.Driver = driver
    
    /// Current connection status
    member this.PlcConnectionStatus = driver.PlcConnectionStatus
    
    /// Current connection info
    member this.ConnectionInfo = driver.ConnectionInfo
    
    /// Current diagnostics
    member this.Diagnostics = driver.Diagnostics
    
    /// Driver capabilities
    member this.Capabilities = driver.Capabilities
    
    /// Server uptime
    member this.Uptime = DateTime.UtcNow - serverStartTime
    
    /// Connect to PLC
    member this.ConnectAsync() = 
        task {
            try
                logger.LogInformation("Connecting to PLC {PlcId} ({Vendor})", config.PlcId, config.Vendor)
                let! result = driver.ConnectAsync()
                match result with
                | Result.Ok () -> 
                    logger.LogInformation("Successfully connected to PLC {PlcId}", config.PlcId)
                    return true
                | Result.Error error ->
                    logger.LogError("Failed to connect to PLC {PlcId}: {Error}", config.PlcId, error)
                    return false
            with
            | ex ->
                logger.LogError(ex, "Exception while connecting to PLC {PlcId}", config.PlcId)
                return false
        }
    
    /// Disconnect from PLC
    member this.DisconnectAsync() =
        task {
            try
                logger.LogInformation("Disconnecting from PLC {PlcId}", config.PlcId)
                do! driver.DisconnectAsync()
                logger.LogInformation("Successfully disconnected from PLC {PlcId}", config.PlcId)
            with
            | ex ->
                logger.LogError(ex, "Exception while disconnecting from PLC {PlcId}", config.PlcId)
        }
    
    /// Perform health check
    member this.HealthCheckAsync() =
        task {
            try
                logger.LogDebug("Performing health check for PLC {PlcId}", config.PlcId)
                let! isHealthy = driver.HealthCheckAsync()
                if isHealthy then
                    logger.LogDebug("PLC {PlcId} health check passed", config.PlcId)
                else
                    logger.LogWarning("PLC {PlcId} health check failed", config.PlcId)
                return isHealthy
            with
            | ex ->
                logger.LogError(ex, "Exception during health check for PLC {PlcId}", config.PlcId)
                return false
        }
    
    /// Read a single tag
    member this.ReadTagAsync(tag: TagConfiguration) =
        task {
            try
                logger.LogTrace("Reading tag {TagId} from PLC {PlcId}", tag.Id, config.PlcId)
                let! result = driver.ReadTagAsync(tag)
                match result with
                | Result.Ok scanResult ->
                    logger.LogTrace("Successfully read tag {TagId}: {Value} (Quality: {Quality})", 
                        tag.Id, scanResult.Value, scanResult.Quality)
                    return Some scanResult
                | Result.Error error ->
                    logger.LogWarning("Failed to read tag {TagId} from PLC {PlcId}: {Error}", 
                        tag.Id, config.PlcId, error)
                    return None
            with
            | ex ->
                logger.LogError(ex, "Exception while reading tag {TagId} from PLC {PlcId}", tag.Id, config.PlcId)
                return None
        }
    
    /// Write a single tag
    member this.WriteTagAsync(tag: TagConfiguration, value: PlcValue) =
        task {
            try
                logger.LogTrace("Writing tag {TagId} to PLC {PlcId}: {Value}", tag.Id, config.PlcId, value)
                let! result = driver.WriteTagAsync(tag, value)
                match result with
                | Result.Ok () ->
                    logger.LogTrace("Successfully wrote tag {TagId}", tag.Id)
                    return true
                | Result.Error error ->
                    logger.LogWarning("Failed to write tag {TagId} to PLC {PlcId}: {Error}", 
                        tag.Id, config.PlcId, error)
                    return false
            with
            | ex ->
                logger.LogError(ex, "Exception while writing tag {TagId} to PLC {PlcId}", tag.Id, config.PlcId)
                return false
        }
    
    /// Read multiple tags
    member this.ReadTagsAsync(tags: TagConfiguration list) =
        task {
            try
                logger.LogDebug("Reading {TagCount} tags from PLC {PlcId}", tags.Length, config.PlcId)
                let! result = driver.ReadTagsAsync(tags)
                match result with
                | Result.Ok batch ->
                    let stats = ServerScanStatistics.FromScanBatch(batch)
                    logger.LogDebug("Successfully read tags from PLC {PlcId}: {SuccessfulTags}/{TotalTags} successful", 
                        config.PlcId, stats.SuccessfulTags, stats.TotalTags)
                    return Some batch
                | Result.Error error ->
                    logger.LogError("Failed to read tags from PLC {PlcId}: {Error}", config.PlcId, error)
                    return None
            with
            | ex ->
                logger.LogError(ex, "Exception while reading tags from PLC {PlcId}", config.PlcId)
                return None
        }
    
    /// Execute a scan request
    member this.ExecuteScanAsync(request: ScanRequest) =
        task {
            try
                logger.LogDebug("Executing scan request {RequestId} for PLC {PlcId}", request.RequestId, config.PlcId)
                let! result = driver.ExecuteScanAsync(request)
                match result with
                | Result.Ok batch ->
                    let stats = ServerScanStatistics.FromScanBatch(batch)
                    logger.LogDebug("Scan request {RequestId} completed: {SuccessfulTags}/{TotalTags} successful", 
                        request.RequestId, stats.SuccessfulTags, stats.TotalTags)
                    return Some batch
                | Result.Error error ->
                    logger.LogError("Scan request {RequestId} failed for PLC {PlcId}: {Error}", 
                        request.RequestId, config.PlcId, error)
                    return None
            with
            | ex ->
                logger.LogError(ex, "Exception during scan request {RequestId} for PLC {PlcId}", 
                    request.RequestId, config.PlcId)
                return None
        }
    
    /// Update diagnostics
    member this.UpdateDiagnosticsAsync() =
        task {
            try
                logger.LogTrace("Updating diagnostics for PLC {PlcId}", config.PlcId)
                do! driver.UpdateDiagnosticsAsync()
                logger.LogTrace("Diagnostics updated for PLC {PlcId}", config.PlcId)
            with
            | ex ->
                logger.LogError(ex, "Exception while updating diagnostics for PLC {PlcId}", config.PlcId)
        }
    
    /// Get performance metrics
    member this.GetPerformanceMetrics() =
        try
            driver.GetPerformanceMetrics()
        with
        | ex ->
            logger.LogError(ex, "Exception while getting performance metrics for PLC {PlcId}", config.PlcId)
            PerformanceMetrics.Empty(config.PlcId)
    
    /// Get system information
    member this.GetSystemInfoAsync() =
        task {
            try
                logger.LogDebug("Getting system info for PLC {PlcId}", config.PlcId)
                let! result = driver.GetSystemInfoAsync()
                match result with
                | Result.Ok systemInfo ->
                    logger.LogDebug("Successfully retrieved system info for PLC {PlcId}", config.PlcId)
                    return Some systemInfo
                | Result.Error error ->
                    logger.LogWarning("Failed to get system info for PLC {PlcId}: {Error}", config.PlcId, error)
                    return None
            with
            | ex ->
                logger.LogError(ex, "Exception while getting system info for PLC {PlcId}", config.PlcId)
                return None
        }
    
    /// Validate tag configuration
    member this.ValidateTag(tag: TagConfiguration) =
        try
            let result = driver.ValidateTagConfiguration(tag)
            match result with
            | Result.Ok () -> true
            | Result.Error error ->
                logger.LogWarning("Tag {TagId} validation failed: {Error}", tag.Id, error)
                false
        with
        | ex ->
            logger.LogError(ex, "Exception while validating tag {TagId}", tag.Id)
            false
    
    /// Get supported data types
    member this.GetSupportedDataTypes() =
        try
            driver.GetSupportedDataTypes()
        with
        | ex ->
            logger.LogError(ex, "Exception while getting supported data types for PLC {PlcId}", config.PlcId)
            []
    
    // Events
    [<CLIEvent>]
    member this.ConnectionStateChanged = connectionStateChangedEvent.Publish
    
    [<CLIEvent>]
    member this.ScanCompleted = scanCompletedEvent.Publish
    
    [<CLIEvent>]
    member this.ErrorOccurred = errorOccurredEvent.Publish
    
    // IDisposable implementation
    interface IDisposable with
        member this.Dispose() =
            if not isDisposed then
                isDisposed <- true
                try
                    // Disconnect if connected
                    if driver.PlcConnectionStatus.IsOperational then
                        driver.DisconnectAsync() |> ignore
                    
                    // Dispose driver if it implements IDisposable
                    match driver with
                    | :? IDisposable as disposable -> disposable.Dispose()
                    | _ -> ()
                    
                    logger.LogInformation("PLC Manager for {PlcId} disposed", config.PlcId)
                with
                | ex ->
                    logger.LogError(ex, "Exception while disposing PLC Manager for {PlcId}", config.PlcId)

/// PLC Manager factory for creating managers with the appropriate drivers
type PlcManagerFactory(driverFactory: IPlcDriverFactory, loggerFactory: ILoggerFactory) =
    
    /// Create a PLC manager for the specified configuration
    member this.CreateManager(config: PlcServerConfig) =
        try
            let logger = loggerFactory.CreateLogger<PlcManagerFactory>()
            logger.LogInformation("Creating PLC manager for {PlcId} ({Vendor})", config.PlcId, config.Vendor)
            
            let vendorName = config.Vendor.ToString()
            
            if not (driverFactory.IsVendorSupported(vendorName)) then
                let supportedVendors = String.concat ", " driverFactory.SupportedVendors
                failwith $"Vendor {vendorName} is not supported. Supported vendors: {supportedVendors}"
            
            match driverFactory.CreateDriver(config.PlcId, vendorName, config.ConnectionConfig) with
            | Ok driver ->
                let managerLogger = loggerFactory.CreateLogger<ServerPlcManager>()
                let manager = new ServerPlcManager(config, driver, managerLogger)
                logger.LogInformation("Successfully created PLC manager for {PlcId}", config.PlcId)
                Ok manager
            | Result.Error error ->
                let errorMsg = $"Failed to create driver for PLC {config.PlcId}: {error}"
                logger.LogError(errorMsg)
                Result.Error errorMsg
        with
        | ex ->
            let logger = loggerFactory.CreateLogger<PlcManagerFactory>()
            let errorMsg = $"Exception while creating PLC manager for {config.PlcId}: {ex.Message}"
            logger.LogError(ex, errorMsg)
            Result.Error errorMsg
    
    /// Get capabilities for a vendor
    member this.GetVendorCapabilities(vendor: PlcVendor) =
        try
            let vendorName = vendor.ToString()
            driverFactory.GetCapabilities(vendorName)
        with
        | ex ->
            let logger = loggerFactory.CreateLogger<PlcManagerFactory>()
            logger.LogError(ex, "Exception while getting capabilities for vendor {Vendor}", vendor)
            Result.Error $"Failed to get capabilities: {ex.Message}"
    
    /// Get supported vendors
    member this.GetSupportedVendors() = driverFactory.SupportedVendors
    
    /// Validate configuration for a vendor
    member this.ValidateConfiguration(vendor: PlcVendor, connectionConfig: ConnectionConfig) =
        try
            let vendorName = vendor.ToString()
            driverFactory.ValidateConfiguration(vendorName, connectionConfig)
        with
        | ex ->
            let logger = loggerFactory.CreateLogger<PlcManagerFactory>()
            logger.LogError(ex, "Exception while validating configuration for vendor {Vendor}", vendor)
            Result.Error $"Configuration validation failed: {ex.Message}"