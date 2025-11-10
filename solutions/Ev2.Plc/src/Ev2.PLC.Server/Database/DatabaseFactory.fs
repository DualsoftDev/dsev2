namespace DSPLCServer.Database

open System
open System.IO

/// 지원하는 데이터베이스 제공자
type DatabaseType =
    | SQLite
    | PostgreSQL

/// 데이터베이스 설정
type DatabaseConfiguration = {
    DatabaseType: DatabaseType
    ConnectionString: string
    InitializeSchema: bool
}

/// 데이터베이스 팩토리
module DatabaseFactory =
    
    /// SQLite 연결 문자열 생성 (WAL 모드 포함)
    let private createSQLiteConnectionString (filePath: string) =
        $"Data Source={filePath};Journal Mode=WAL;Cache=Shared;Foreign Keys=True"
    
    /// PostgreSQL 연결 문자열 생성
    let private createPostgreSQLConnectionString (host: string) (port: int) (database: string) (username: string) (password: string) =
        $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Prefer"
    
    /// SQLite 설정 생성
    let createSQLiteConfig (filePath: string) = {
        DatabaseType = SQLite
        ConnectionString = createSQLiteConnectionString filePath
        InitializeSchema = true
    }
    
    /// PostgreSQL 설정 생성 
    let createPostgreSQLConfig (host: string) (port: int) (database: string) (username: string) (password: string) = {
        DatabaseType = PostgreSQL
        ConnectionString = createPostgreSQLConnectionString host port database username password
        InitializeSchema = true
    }
    
    /// 연결 문자열에서 제공자 감지
    let detectProvider (connectionString: string) : DatabaseType option =
        if connectionString.Contains("Data Source=") then
            Some SQLite
        elif connectionString.Contains("Host=") || connectionString.Contains("Server=") then
            Some PostgreSQL
        else
            None
    
    /// 설정에 따라 Repository 생성
    let createRepository (config: DatabaseConfiguration) : IDataRepository =
        match config.DatabaseType with
        | SQLite -> new SQLiteRepository(config.ConnectionString) :> IDataRepository
        | PostgreSQL -> new PostgreSQLRepository(config.ConnectionString) :> IDataRepository
    
    /// 연결 문자열에서 Repository 자동 생성
    let createRepositoryFromConnectionString (connectionString: string) : IDataRepository option =
        detectProvider connectionString
        |> Option.map (fun provider ->
            match provider with
            | SQLite -> new SQLiteRepository(connectionString) :> IDataRepository
            | PostgreSQL -> new PostgreSQLRepository(connectionString) :> IDataRepository)
    
    /// 기본 SQLite 설정 (로컬 앱 데이터 폴더 사용)
    let createDefaultSQLiteConfig() =
        let appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        let dbFolder = Path.Combine(appDataFolder, "DSPLCServer")
        
        if not (Directory.Exists(dbFolder)) then
            Directory.CreateDirectory(dbFolder) |> ignore
        
        let dbFilePath = Path.Combine(dbFolder, "dsplcserver.db")
        createSQLiteConfig dbFilePath
    
    /// appsettings.json 호환 설정 구조
    [<CLIMutable>]
    type DatabaseSettings = {
        Provider: string
        SQLite: SQLiteSettings option
        PostgreSQL: PostgreSQLSettings option
        InitializeSchema: bool
    }
    and [<CLIMutable>] SQLiteSettings = {
        FilePath: string
    }
    and [<CLIMutable>] PostgreSQLSettings = {
        Host: string
        Port: int
        Database: string
        Username: string
        Password: string
    }
    
    /// 설정 객체에서 DatabaseConfiguration 생성
    let createFromSettings (settings: DatabaseSettings) : DatabaseConfiguration option =
        match settings.Provider.ToLowerInvariant() with
        | "sqlite" ->
            settings.SQLite
            |> Option.map (fun s -> createSQLiteConfig s.FilePath)
        | "postgresql" ->
            settings.PostgreSQL
            |> Option.map (fun s -> createPostgreSQLConfig s.Host s.Port s.Database s.Username s.Password)
        | _ -> None
    
    /// 환경변수에서 데이터베이스 설정 생성
    let createFromEnvironment() : DatabaseConfiguration =
        let provider = Environment.GetEnvironmentVariable("DSPLC_DB_PROVIDER") |> Option.ofObj |> Option.defaultValue "sqlite"
        
        match provider.ToLowerInvariant() with
        | "sqlite" ->
            let filePath = Environment.GetEnvironmentVariable("DSPLC_DB_SQLITE_PATH") 
                          |> Option.ofObj 
                          |> Option.defaultValue (Path.Combine(Path.GetTempPath(), "dsplcserver.db"))
            createSQLiteConfig filePath
        | "postgresql" ->
            let host = Environment.GetEnvironmentVariable("DSPLC_DB_HOST") |> Option.ofObj |> Option.defaultValue "localhost"
            let portStr = Environment.GetEnvironmentVariable("DSPLC_DB_PORT") |> Option.ofObj |> Option.defaultValue "5432"
            let database = Environment.GetEnvironmentVariable("DSPLC_DB_NAME") |> Option.ofObj |> Option.defaultValue "dsplcserver"
            let username = Environment.GetEnvironmentVariable("DSPLC_DB_USER") |> Option.ofObj |> Option.defaultValue "postgres"
            let password = Environment.GetEnvironmentVariable("DSPLC_DB_PASSWORD") |> Option.ofObj |> Option.defaultValue ""
            
            match Int32.TryParse(portStr) with
            | (true, port) -> createPostgreSQLConfig host port database username password
            | _ -> createDefaultSQLiteConfig()
        | _ -> createDefaultSQLiteConfig()
    
    /// Repository 초기화 (스키마 생성)
    let initializeRepository (repository: IDataRepository) =
        repository.Initialize()
    
    /// Repository 연결 테스트
    let testRepository (repository: IDataRepository) =
        repository.HealthCheck()