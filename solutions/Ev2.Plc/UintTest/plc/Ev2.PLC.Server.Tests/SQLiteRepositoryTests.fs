module Ev2.PLC.Server.Tests.SQLiteRepositoryTests

open System
open System.IO
open Xunit
open Microsoft.Data.Sqlite
open DSPLCServer.Common
open DSPLCServer.Database

let private createTempSqliteConnection () =
    let dbPath = Path.Combine(Path.GetTempPath(), $"dsplcserver-test-{Guid.NewGuid()}.db")
    let connectionString = $"Data Source={dbPath}"
    connectionString, dbPath

let private createRepository () =
    let connectionString, dbPath = createTempSqliteConnection()
    let repository = new SQLiteRepository(connectionString) :> IDataRepository
    repository.Initialize()
    repository, dbPath

let private samplePlcConfig plcId name vendor =
    {
        PLCConfiguration.Id = plcId
        Name = name
        Vendor = vendor
        ConnectionString = "Host=127.0.0.1;Port=2004;Protocol=XGT"
        ScanInterval = 1000
        IsEnabled = true
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

let private sampleTagConfig tagId plcId name address dataType =
    {
        TagConfiguration.Id = tagId
        PlcId = plcId
        Name = name
        Address = address
        DataType = dataType
        IsEnabled = true
        Description = None
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

let private sampleDataPoint tagId plcId value quality timestamp =
    {
        PLCDataPoint.Id = 0L
        TagId = tagId
        PlcId = plcId
        Value = value
        Quality = quality
        Timestamp = timestamp
    }

[<Fact>]
let ``SQLiteRepository upsert and retrieve PLC configuration`` () =
    let repository, dbPath = createRepository()
    try
        let plc = samplePlcConfig "PLC1" "Test PLC" PlcVendor.LSElectric
        repository.UpsertPLCConfiguration plc
        let result = repository.GetPLCConfiguration "PLC1"
        Assert.True(result.IsSome)
        let plcRetrieved = result.Value
        Assert.Equal(plc.Name, plcRetrieved.Name)
        Assert.Equal(plc.Vendor, plcRetrieved.Vendor)
        Assert.True(repository.GetAllPLCConfigurations() |> List.exists (fun x -> x.Id = plc.Id))
    finally
        SqliteConnection.ClearAllPools()
        if File.Exists(dbPath) then File.Delete(dbPath)

[<Fact>]
let ``SQLiteRepository upsert, query and delete tag configuration`` () =
    let repository, dbPath = createRepository()
    try
        let plc = samplePlcConfig "PLC1" "Test PLC" PlcVendor.LSElectric
        repository.UpsertPLCConfiguration plc
        let tag = sampleTagConfig "TAG1" plc.Id "MotorSpeed" "D100" TagDataType.Int32
        repository.UpsertTagConfiguration tag
        let fetched = repository.GetTagConfiguration "TAG1"
        Assert.True(fetched.IsSome)
        Assert.Equal("MotorSpeed", fetched.Value.Name)
        Assert.Equal(TagDataType.Int32, fetched.Value.DataType)
        Assert.True(repository.GetTagConfigurationsByPlc(plc.Id) |> List.exists (fun x -> x.Id = tag.Id))
        repository.DeleteTagConfiguration tag.Id
        Assert.True(repository.GetTagConfiguration tag.Id |> Option.isNone)
    finally
        SqliteConnection.ClearAllPools()
        if File.Exists(dbPath) then File.Delete(dbPath)

[<Fact>]
let ``SQLiteRepository inserts datapoints and performs queries`` () =
    let repository, dbPath = createRepository()
    try
        let plc = samplePlcConfig "PLC1" "Test PLC" PlcVendor.LSElectric
        repository.UpsertPLCConfiguration plc
        let tag = sampleTagConfig "TAG1" plc.Id "MotorSpeed" "D100" TagDataType.Int32
        repository.UpsertTagConfiguration tag

        let now = DateTime.UtcNow
        let points =
            [ sampleDataPoint tag.Id plc.Id (ScalarValue.IntValue 10L) PlcTagQuality.Good now
              sampleDataPoint tag.Id plc.Id (ScalarValue.IntValue 20L) PlcTagQuality.Good (now.AddSeconds(1.0)) ]

        repository.InsertDataPoints points

        let latest = repository.QueryLatestByTag tag.Id
        Assert.True(latest.IsSome)
        match latest.Value.Value with
        | ScalarValue.IntValue v -> Assert.Equal(20L, v)
        | _ -> Assert.True(false, "Expected IntValue")

        let range = repository.QueryRangeByTag(tag.Id, { StartTime = None; EndTime = None; Limit = Some 10; OrderDesc = false })
        Assert.Equal(2, range.Length)

        let prunedCount = repository.PruneBefore(now.AddMilliseconds(500.0))
        Assert.Equal(1L, prunedCount)
    finally
        SqliteConnection.ClearAllPools()
        if File.Exists(dbPath) then File.Delete(dbPath)

[<Fact>]
let ``SQLiteRepository statistics and health check`` () =
    let repository, dbPath = createRepository()
    try
        let healthy = repository.HealthCheck()
        Assert.True(healthy)
        Assert.Equal(0L, repository.GetDataPointCount())
        Assert.Equal(0L, repository.GetDataPointCountByPlc "PLC1")
    finally
        SqliteConnection.ClearAllPools()
        if File.Exists(dbPath) then File.Delete(dbPath)
