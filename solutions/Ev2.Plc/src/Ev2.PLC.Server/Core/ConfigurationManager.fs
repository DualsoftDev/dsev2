namespace DSPLCServer.FS.Core

open System
open System.IO
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open System.Text.Json
open DSPLCServer.FS.Database
open DSPLCServer.FS.PLC

/// 애플리케이션 설정 관리자
type ConfigurationManager(configuration: IConfiguration, repository: IDataRepository, logger: ILogger<ConfigurationManager>) =
    
    let mutable disposed = false
    
    /// PLC 설정을 데이터베이스에서 로드
    member this.LoadPLCConfigurationsAsync() = task {
        try
            let! plcs = repository.GetAllPLCsAsync()
            logger.LogInformation("Loaded {Count} PLC configurations from database", plcs.Length)
            return plcs
        with
        | ex ->
            logger.LogError(ex, "Failed to load PLC configurations")
            return [||]
    }
    
    /// PLC 설정을 JSON 파일로 내보내기
    member this.ExportPLCConfigurationsAsync(filePath: string) = task {
        try
            let! plcs = repository.GetAllPLCsAsync()
            let! tags = repository.GetAllTagsAsync()
            
            let plcConfigs = 
                plcs |> Array.map (fun plc ->
                    let plcTags = tags |> Array.filter (fun t -> t.PlcId = plc.Id)
                    {|
                        PLC = plc
                        Tags = plcTags
                    |}
                )
            
            let json = JsonSerializer.Serialize(plcConfigs, JsonSerializerOptions(WriteIndented = true))
            do! File.WriteAllTextAsync(filePath, json)
            
            logger.LogInformation("Exported {PLCCount} PLC configurations with {TagCount} tags to {FilePath}", 
                                plcs.Length, tags.Length, filePath)
            return true
        with
        | ex ->
            logger.LogError(ex, "Failed to export PLC configurations to {FilePath}", filePath)
            return false
    }
    
    /// JSON 파일에서 PLC 설정 가져오기
    member this.ImportPLCConfigurationsAsync(filePath: string) = task {
        try
            if not (File.Exists(filePath)) then
                logger.LogWarning("Import file not found: {FilePath}", filePath)
                return false
            
            let! json = File.ReadAllTextAsync(filePath)
            let importData = JsonSerializer.Deserialize<{| PLC: PLCConfiguration; Tags: TagConfiguration[] |}[]>(json)
            
            let mutable plcCount = 0
            let mutable tagCount = 0
            
            for item in importData do
                // PLC 설정 가져오기
                let plcConfig = { item.PLC with Id = 0; CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow }
                let! plcId = repository.CreatePLCAsync(plcConfig)
                
                if plcId > 0 then
                    plcCount <- plcCount + 1
                    
                    // 태그 설정 가져오기
                    for tag in item.Tags do
                        let tagConfig = { tag with Id = 0; PlcId = plcId; CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow }
                        let! tagId = repository.CreateTagAsync(tagConfig)
                        if tagId > 0 then
                            tagCount <- tagCount + 1
            
            logger.LogInformation("Imported {PLCCount} PLC configurations with {TagCount} tags from {FilePath}", 
                                plcCount, tagCount, filePath)
            return true
        with
        | ex ->
            logger.LogError(ex, "Failed to import PLC configurations from {FilePath}", filePath)
            return false
    }
    
    /// 기본 PLC 설정 생성
    member this.CreateDefaultPLCConfigurationAsync(manufacturerName: string, plcIP: string, plcName: string) = task {
        try
            match PLCManagerFactory.ParseManufacturer(manufacturerName) with
            | Some manufacturer ->
                let defaultConfig = PLCManagerFactory.GetDefaultConfiguration(manufacturer)
                match defaultConfig with
                | Some config ->
                    let plcConfig = {
                        Id = 0
                        PlcIP = plcIP
                        PlcType = config.Manufacturer
                        PlcName = plcName
                        ScanInterval = config.RecommendedScanInterval
                        IsActive = true
                        ConnectionString = None
                        CreatedAt = DateTime.UtcNow
                        UpdatedAt = DateTime.UtcNow
                    }
                    
                    let! plcId = repository.CreatePLCAsync(plcConfig)
                    
                    if plcId > 0 then
                        // 샘플 태그 생성
                        let sampleAddresses = PLCManagerFactory.GetSampleAddresses(manufacturer)
                        
                        for address in sampleAddresses do
                            let dataType = PLCManagerFactory.GetRecommendedDataType(manufacturer, address) |> Option.defaultValue Dual.PLC.Common.FS.PlcDataSizeType.Int16
                            let tagConfig = {
                                Id = 0
                                PlcId = plcId
                                TagName = sprintf "Tag_%s" address
                                Address = address
                                DataType = dataType
                                ScanGroup = "Default"
                                IsActive = true
                                Comment = sprintf "Sample tag for %s" address
                                CreatedAt = DateTime.UtcNow
                                UpdatedAt = DateTime.UtcNow
                            }
                            
                            let! _ = repository.CreateTagAsync(tagConfig)
                            ()
                        
                        logger.LogInformation("Created default PLC configuration for {Manufacturer} at {IP} with {TagCount} sample tags", 
                                            manufacturerName, plcIP, sampleAddresses.Length)
                        return Some plcId
                    else
                        return None
                | None -> return None
            | None -> 
                logger.LogWarning("Unknown manufacturer: {Manufacturer}", manufacturerName)
                return None
        with
        | ex ->
            logger.LogError(ex, "Failed to create default PLC configuration")
            return None
    }
    
    /// 애플리케이션 설정 가져오기
    member this.GetApplicationSettings() = {|
        Database = {|
            Type = configuration.["Database:Type"]
            InitializeDatabase = configuration.GetValue<bool>("Database:InitializeDatabase", true)
        |}
        PLC = {|
            DefaultScanInterval = configuration.GetValue<int>("PLC:DefaultScanInterval", 1000)
            MaxTagsPerScan = configuration.GetValue<int>("PLC:MaxTagsPerScan", 100)
            ConnectionTimeout = configuration.GetValue<int>("PLC:ConnectionTimeout", 5000)
            RetryAttempts = configuration.GetValue<int>("PLC:RetryAttempts", 3)
            RetryDelay = configuration.GetValue<int>("PLC:RetryDelay", 1000)
            SupportedManufacturers = configuration.GetSection("PLC:SupportedManufacturers").Get<string[]>()
        |}
        DataLogging = {|
            EnableDataLogging = configuration.GetValue<bool>("DataLogging:EnableDataLogging", true)
            BatchSize = configuration.GetValue<int>("DataLogging:BatchSize", 1000)
            FlushInterval = configuration.GetValue<int>("DataLogging:FlushInterval", 5000)
            MaxRetentionDays = configuration.GetValue<int>("DataLogging:MaxRetentionDays", 365)
            CompressionEnabled = configuration.GetValue<bool>("DataLogging:CompressionEnabled", true)
        |}
        Performance = {|
            EnableCaching = configuration.GetValue<bool>("Performance:EnableCaching", true)
            CacheExpiration = configuration.GetValue<int>("Performance:CacheExpiration", 300)
            MaxConcurrentScans = configuration.GetValue<int>("Performance:MaxConcurrentScans", 10)
            ThreadPoolMinWorkerThreads = configuration.GetValue<int>("Performance:ThreadPoolMinWorkerThreads", 10)
            ThreadPoolMaxWorkerThreads = configuration.GetValue<int>("Performance:ThreadPoolMaxWorkerThreads", 100)
        |}
        Monitoring = {|
            EnableHealthChecks = configuration.GetValue<bool>("Monitoring:EnableHealthChecks", true)
            EnableMetrics = configuration.GetValue<bool>("Monitoring:EnableMetrics", true)
            StatisticsInterval = configuration.GetValue<int>("Monitoring:StatisticsInterval", 60000)
            AlertThresholds = {|
                MaxScanTime = configuration.GetValue<int>("Monitoring:AlertThresholds:MaxScanTime", 5000)
                MaxErrorRate = configuration.GetValue<double>("Monitoring:AlertThresholds:MaxErrorRate", 10.0)
                MaxMemoryUsage = configuration.GetValue<int>("Monitoring:AlertThresholds:MaxMemoryUsage", 512)
            |}
        |}
    |}
    
    /// 설정 값 업데이트 (런타임)
    member this.UpdateSetting(key: string, value: string) =
        try
            configuration.[key] <- value
            logger.LogInformation("Updated configuration: {Key} = {Value}", key, value)
            true
        with
        | ex ->
            logger.LogError(ex, "Failed to update configuration: {Key}", key)
            false
    
    /// 연결 문자열 검증
    member this.ValidateConnectionStringAsync(connectionString: string) = task {
        try
            match DatabaseFactory.createRepositoryFromConnectionString(connectionString) with
            | Some repository ->
                let! isValid = repository.TestConnectionAsync()
                return isValid
            | None -> return false
        with
        | ex ->
            logger.LogError(ex, "Connection string validation failed")
            return false
    }
    
    /// PLC 연결 검증
    member this.ValidatePLCConnectionAsync(manufacturerName: string, plcIP: string) = task {
        try
            match PLCManagerFactory.ParseManufacturer(manufacturerName) with
            | Some manufacturer ->
                let factory = new PLCManagerFactory()
                let manager = factory.CreateManager(manufacturer, plcIP)
                
                let! isConnected = manager.TestConnectionAsync()
                manager.Dispose()
                
                return isConnected
            | None -> return false
        with
        | ex ->
            logger.LogError(ex, "PLC connection validation failed for {Manufacturer} at {IP}", manufacturerName, plcIP)
            return false
    }
    
    /// 시스템 진단 정보
    member this.GetSystemDiagnosticsAsync() = task {
        try
            let! dbInfo = repository.GetDatabaseInfoAsync()
            let appSettings = this.GetApplicationSettings()
            
            let systemInfo = {|
                Application = {|
                    Name = "DSPLCServer.FS"
                    Version = "1.0.0"
                    StartTime = DateTime.Now // 실제로는 시작 시간 추적 필요
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") |> Option.ofObj |> Option.defaultValue "Production"
                |}
                System = {|
                    MachineName = Environment.MachineName
                    OSVersion = Environment.OSVersion.ToString()
                    ProcessorCount = Environment.ProcessorCount
                    WorkingSet = Environment.WorkingSet
                    TotalPhysicalMemory = GC.GetTotalMemory(false)
                |}
                Database = dbInfo
                Configuration = appSettings
                SupportedPLCManufacturers = PLCManagerFactory.GetSupportedManufacturers()
            |}
            
            return systemInfo
        with
        | ex ->
            logger.LogError(ex, "Failed to get system diagnostics")
            return {|
                Error = ex.Message
                Timestamp = DateTime.Now
            |}
    }
    
    /// 설정 백업
    member this.BackupConfigurationAsync(backupPath: string) = task {
        try
            let! plcs = repository.GetAllPLCsAsync()
            let! tags = repository.GetAllTagsAsync()
            let appSettings = this.GetApplicationSettings()
            let! systemDiagnostics = this.GetSystemDiagnosticsAsync()
            
            let backup = {|
                BackupDate = DateTime.UtcNow
                PLCConfigurations = plcs
                TagConfigurations = tags
                ApplicationSettings = appSettings
                SystemDiagnostics = systemDiagnostics
            |}
            
            let json = JsonSerializer.Serialize(backup, JsonSerializerOptions(WriteIndented = true))
            
            let directory = Path.GetDirectoryName(backupPath)
            if not (String.IsNullOrEmpty(directory)) && not (Directory.Exists(directory)) then
                Directory.CreateDirectory(directory) |> ignore
            
            do! File.WriteAllTextAsync(backupPath, json)
            
            logger.LogInformation("Configuration backup created: {BackupPath}", backupPath)
            return true
        with
        | ex ->
            logger.LogError(ex, "Failed to create configuration backup")
            return false
    }
    
    /// 설정 복원
    member this.RestoreConfigurationAsync(backupPath: string) = task {
        try
            if not (File.Exists(backupPath)) then
                logger.LogWarning("Backup file not found: {BackupPath}", backupPath)
                return false
            
            let! json = File.ReadAllTextAsync(backupPath)
            let backup = JsonSerializer.Deserialize<{| BackupDate: DateTime; PLCConfigurations: PLCConfiguration[]; TagConfigurations: TagConfiguration[] |}>(json)
            
            // 기존 설정 삭제 후 복원 (주의: 데이터 손실 위험)
            logger.LogWarning("Starting configuration restore - this will delete existing configurations")
            
            // PLC 설정 복원
            for plc in backup.PLCConfigurations do
                let! plcId = repository.CreatePLCAsync({ plc with Id = 0; CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow })
                
                // 해당 PLC의 태그 복원
                let plcTags = backup.TagConfigurations |> Array.filter (fun t -> t.PlcId = plc.Id)
                for tag in plcTags do
                    let! _ = repository.CreateTagAsync({ tag with Id = 0; PlcId = plcId; CreatedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow })
                    ()
            
            logger.LogInformation("Configuration restored from backup: {BackupPath} (Date: {BackupDate})", 
                                backupPath, backup.BackupDate)
            return true
        with
        | ex ->
            logger.LogError(ex, "Failed to restore configuration from backup")
            return false
    }
    
    /// 리소스 정리
    member this.Dispose() =
        if not disposed then
            disposed <- true
    
    interface IDisposable with
        member this.Dispose() = this.Dispose()