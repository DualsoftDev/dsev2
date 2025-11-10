namespace DSPLCServer

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Serilog
open Serilog.Extensions.Hosting
open System
open System.IO
open System.Threading.Tasks
open DSPLCServer.Common
open DSPLCServer.Database
open DSPLCServer.PLC
open DSPLCServer.Core
// open DSPLCServer.Console
open Ev2.PLC.Common.Interfaces
open Ev2.PLC.Common.Types

/// Placeholder PLC driver factory for demonstration
type PlcDriverFactoryPlaceholder() =
    let supportedVendors = ["Allen-Bradley"; "Siemens"; "Mitsubishi"; "LS Electric"]
    
    interface IPlcDriverFactory with
        member this.SupportedVendors = supportedVendors
        
        member this.IsVendorSupported(vendor: string) = 
            supportedVendors |> List.contains vendor
        
        member this.CreateDriver(plcId: string, vendor: string, connectionConfig: ConnectionConfig) =
            // This is a placeholder - actual implementations would create real drivers
            Result.Error $"Driver creation not implemented for vendor: {vendor}"
        
        member this.CreateAdvancedDriver(plcId: string, vendor: string, connectionConfig: ConnectionConfig) =
            Result.Error $"Advanced driver creation not implemented for vendor: {vendor}"
        
        member this.GetCapabilities(vendor: string) =
            // Return basic capabilities for supported vendors
            if supportedVendors |> List.contains vendor then
                Ok {
                    DriverCapabilities.Default with
                        Name = $"{vendor} Driver"
                        Version = "1.0.0"
                        SupportedVendors = [vendor]
                }
            else
                Result.Error $"Vendor not supported: {vendor}"
        
        member this.CreateDriverFromConnectionString(plcId: string, vendor: string, connectionString: string) =
            Result.Error "Connection string parsing not implemented"
        
        member this.ValidateConfiguration(vendor: string, connectionConfig: ConnectionConfig) =
            if supportedVendors |> List.contains vendor then Ok ()
            else Result.Error $"Vendor not supported: {vendor}"

module Program =
    
    /// Configure Serilog logging
    let configureSerilog (configuration: IConfiguration) =
        Log.Logger <- 
            LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path = "Logs/dsplcserver-.log",
                    rollingInterval = RollingInterval.Day,
                    retainedFileCountLimit = 7,
                    outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger()
    
    /// Configure services in DI container
    let configureServices (services: IServiceCollection) (configuration: IConfiguration) =
        
        // Logging
        services.AddLogging(fun builder ->
            builder.ClearProviders() |> ignore
            builder.AddSerilog(dispose = true) |> ignore
        ) |> ignore
        
        // Configuration
        services.AddSingleton<IConfiguration>(configuration) |> ignore
        
        // Create Data directory for SQLite if it doesn't exist
        let dataDir = "Data"
        if not (Directory.Exists(dataDir)) then
            Directory.CreateDirectory(dataDir) |> ignore
        
        // Data repository (using mock for now)
        let repository = new MockRepository() :> IDataRepository
        services.AddSingleton<IDataRepository>(repository) |> ignore
        
        // PLC driver factory (placeholder - will need actual implementations)
        let driverFactory = new PlcDriverFactoryPlaceholder() :> IPlcDriverFactory
        services.AddSingleton<IPlcDriverFactory>(driverFactory) |> ignore
        
        // PLC manager factory
        services.AddTransient<PlcManagerFactory>() |> ignore
        
        // Core services
        services.AddSingleton<DataLogger>() |> ignore
        services.AddSingleton<ScanScheduler>() |> ignore
        // services.AddSingleton<MonitoringService>() |> ignore
        
        // Hosted services
        services.AddHostedService<DataLoggerService>() |> ignore
        services.AddHostedService<ScanSchedulerService>() |> ignore
        // services.AddHostedService<MonitoringHostedService>() |> ignore
        
        // Console interface
        // services.AddSingleton<ConsoleInterface>() |> ignore
        
        ()
    
    /// Initialize application
    let initializeApplicationAsync (serviceProvider: IServiceProvider) =
        task {
            let logger = serviceProvider.GetRequiredService<ILogger<obj>>()
            
            try
                // Initialize database
                let repository = serviceProvider.GetRequiredService<IDataRepository>()
                do! repository.InitializeAsync()
                logger.LogInformation("Database initialized successfully")
                
                // Load sample PLC configurations if database is empty
                let! configs = repository.GetAllPlcConfigurationsAsync()
                if configs.IsEmpty then
                    logger.LogInformation("No PLC configurations found, creating sample configurations")
                    
                    // Sample Allen-Bradley PLC
                    let abConfig = PlcServerConfig.Create(
                        "PLC001", 
                        PlcVendor.AllenBradley, 
                        "AB PLC 1", 
                        ConnectionConfig.ForTCP("192.168.1.100", 44818, 5000)
                    )
                    
                    // Sample Siemens PLC
                    let siemensConfig = PlcServerConfig.Create(
                        "PLC002",
                        PlcVendor.Siemens,
                        "Siemens PLC 1",
                        ConnectionConfig.ForTCP("192.168.1.101", 102, 5000)
                    )
                    
                    do! repository.SavePlcConfigurationAsync(abConfig)
                    do! repository.SavePlcConfigurationAsync(siemensConfig)
                    
                    logger.LogInformation("Sample PLC configurations created")
                
                logger.LogInformation("Application initialized successfully")
                return true
            with
            | ex ->
                logger.LogError(ex, "Application initialization failed")
                return false
        }
    
    /// Shutdown application
    let shutdownApplicationAsync (serviceProvider: IServiceProvider) =
        task {
            let logger = serviceProvider.GetRequiredService<ILogger<obj>>()
            
            try
                logger.LogInformation("Shutting down DSPLCServer...")
                
                // Services will be stopped automatically by hosted service framework
                logger.LogInformation("DSPLCServer shutdown completed")
            with
            | ex ->
                logger.LogError(ex, "Error during shutdown")
        }
    
    /// Build service provider
    let buildServiceProvider (configuration: IConfiguration) =
        let services = ServiceCollection()
        configureServices services configuration
        services.BuildServiceProvider()
    
    /// Show usage and exit
    let showUsageAndExit() =
        let usage = """
DS PLC Server - F# Implementation with Universal Driver Support

Usage:
  DSPLCServer.FS [options]

Options:
  --help, -h        Show this help message

Supported PLC Vendors:
  - Allen-Bradley (EtherNet/IP)
  - Siemens (S7)
  - Mitsubishi (MC Protocol)
  - LS Electric (XGT Protocol)

Environment Variables:
  DSPLC_DB_PROVIDER    Database provider (sqlite/postgresql, default: sqlite)
  DSPLC_DB_SQLITE_PATH SQLite database file path
  DSPLC_DB_HOST        Database host (for PostgreSQL)
  DSPLC_DB_NAME        Database name
  DSPLC_DB_USER        Database username
  DSPLC_DB_PASSWORD    Database password

Configuration Files:
  appsettings.json              - Main configuration
  appsettings.Development.json  - Development overrides

Features:
  - Universal PLC driver interface supporting 4 major vendors
  - Real-time data collection and logging
  - Health monitoring and diagnostics
  - Event-driven architecture
  - Configurable scan scheduling
  - Data quality tracking
  - Performance metrics

Examples:
  DSPLCServer.FS                        # Run server with console interface
  DSPLC_DB_PROVIDER=sqlite DSPLCServer.FS  # Use SQLite explicitly
"""
        Console.WriteLine(usage)
        0
    
    
    /// Application entry point
    [<EntryPoint>]
    let main args =
        try
            try
                // Check for help request
                if args |> Array.contains "--help" || args |> Array.contains "-h" then
                    showUsageAndExit()
                else
                    // Load configuration
                    let configuration = 
                        ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
                            .AddJsonFile("appsettings.Development.json", optional = true, reloadOnChange = true)
                            .AddEnvironmentVariables()
                            .AddCommandLine(args)
                            .Build()
                    
                    // Configure Serilog
                    configureSerilog configuration
                    
                    Log.Information("DSPLCServer starting up...")
                    Log.Information("Universal PLC Driver Server - Supporting Allen-Bradley, Siemens, Mitsubishi, LS Electric")
                    Log.Information("Arguments: {Args}", String.Join(" ", args))
                    
                    // Build and run application using hosted service model
                    let hostBuilder = 
                        Host.CreateDefaultBuilder(args)
                            .UseSerilog()
                            .ConfigureServices(fun context services ->
                                configureServices services context.Configuration)
                    
                    use host = hostBuilder.Build()
                    
                    // Initialize application
                    let initTask = initializeApplicationAsync (host.Services)
                    if initTask.Result then
                        Log.Information("DSPLCServer initialized successfully")
                        
                        // Register shutdown handler
                        Console.CancelKeyPress.Add(fun args ->
                            args.Cancel <- true
                            Log.Information("Shutdown signal received")
                        )
                        
                        // Start console interface in background
                        // let consoleInterface = host.Services.GetRequiredService<ConsoleInterface>()
                        let consoleTask = 
                            Task.Run(fun () -> 
                                () // consoleInterface.Start()
                            )
                        
                        // Run the host (this will start all hosted services)
                        let hostTask = host.RunAsync()
                        
                        // Wait for either console exit or host shutdown
                        Task.WaitAny([| hostTask; consoleTask |]) |> ignore
                        
                        // Shutdown
                        let shutdownTask = shutdownApplicationAsync (host.Services)
                        shutdownTask.Wait()
                        
                        0
                    else
                        Log.Error("DSPLCServer initialization failed")
                        1
            with
            | ex ->
                Log.Fatal(ex, "DSPLCServer terminated unexpectedly")
                1
        finally
            Log.CloseAndFlush()