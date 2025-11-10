module Ev2.PLC.Server.Tests.DatabaseFactoryTests

open System
open System.IO
open Xunit
open DSPLCServer.Database

[<Fact>]
let ``DatabaseFactory detects SQLite by connection string`` () =
    let conn = "Data Source=/tmp/test.db;Journal Mode=WAL"
    let detected = DatabaseFactory.detectProvider conn
    Assert.Equal(Some DatabaseType.SQLite, detected)

[<Fact>]
let ``DatabaseFactory detects PostgreSQL by connection string`` () =
    let conn = "Host=localhost;Port=5432;Database=dsplc;Username=postgres"
    let detected = DatabaseFactory.detectProvider conn
    Assert.Equal(Some DatabaseType.PostgreSQL, detected)

[<Fact>]
let ``DatabaseFactory creates SQLite repository`` () =
    let path = Path.Combine(Path.GetTempPath(), $"factory-test-{Guid.NewGuid()}.db")
    let config = DatabaseFactory.createSQLiteConfig path
    let repo = DatabaseFactory.createRepository config
    Assert.IsType<SQLiteRepository>(repo)

[<Fact>]
let ``DatabaseFactory createRepositoryFromConnectionString handles SQLite`` () =
    let conn = "Data Source=:memory:"
    let repoOpt = DatabaseFactory.createRepositoryFromConnectionString conn
    Assert.True(repoOpt.IsSome)
    Assert.IsType<SQLiteRepository>(repoOpt.Value)

[<Fact>]
let ``DatabaseFactory createFromSettings returns SQLite config`` () =
    let sqliteSettings : DatabaseFactory.SQLiteSettings = { FilePath = "Data/test.db" }
    let settings : DatabaseFactory.DatabaseSettings = {
        Provider = "sqlite"
        SQLite = Some sqliteSettings
        PostgreSQL = None
        InitializeSchema = true
    }
    let configOpt = DatabaseFactory.createFromSettings settings
    Assert.True(configOpt.IsSome)
    Assert.Equal(DatabaseType.SQLite, configOpt.Value.DatabaseType)
