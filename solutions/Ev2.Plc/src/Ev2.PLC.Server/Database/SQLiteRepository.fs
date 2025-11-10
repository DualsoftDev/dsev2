namespace DSPLCServer.Database

open System
open System.Globalization
open System.Data
open Microsoft.Data.Sqlite
open DSPLCServer.Common

module private SQLiteHelpers =
    let boolToInt value = if value then 1 else 0
    let intToBool value = value <> 0

    let vendorToString = function
        | PlcVendor.LSElectric -> "LSElectric"
        | PlcVendor.Mitsubishi -> "Mitsubishi"
        | PlcVendor.AllenBradley -> "AllenBradley"

    let vendorFromString = function
        | null | "" -> PlcVendor.LSElectric
        | "LSElectric" -> PlcVendor.LSElectric
        | "Mitsubishi" -> PlcVendor.Mitsubishi
        | "AllenBradley" -> PlcVendor.AllenBradley
        | other ->
            match other.ToLowerInvariant() with
            | "lselectric" -> PlcVendor.LSElectric
            | "mitsubishi" -> PlcVendor.Mitsubishi
            | "allenbradley" | "allen-bradley" -> PlcVendor.AllenBradley
            | _ -> PlcVendor.LSElectric

    let tagDataTypeToString = function
        | TagDataType.Bool -> "Bool"
        | TagDataType.Int16 -> "Int16"
        | TagDataType.Int32 -> "Int32"
        | TagDataType.Int64 -> "Int64"
        | TagDataType.Float32 -> "Float32"
        | TagDataType.Float64 -> "Float64"
        | TagDataType.String -> "String"
        | TagDataType.Bytes -> "Bytes"

    let tagDataTypeFromString (value: string) =
        match value with
        | null -> TagDataType.String
        | _ ->
            match value.ToLowerInvariant() with
            | "bool" -> TagDataType.Bool
            | "int16" | "short" -> TagDataType.Int16
            | "int32" | "int" -> TagDataType.Int32
            | "int64" | "long" -> TagDataType.Int64
            | "float32" | "single" -> TagDataType.Float32
            | "float64" | "double" -> TagDataType.Float64
            | "bytes" -> TagDataType.Bytes
            | _ -> TagDataType.String

    let qualityToString = function
        | PlcTagQuality.Good -> "Good"
        | PlcTagQuality.Uncertain -> "Uncertain"
        | PlcTagQuality.Bad message -> $"Bad:{message}"

    let qualityFromString (value: string) =
        match value with
        | null | "" -> PlcTagQuality.Uncertain
        | _ when value.Equals("Good", StringComparison.OrdinalIgnoreCase) -> PlcTagQuality.Good
        | _ when value.Equals("Uncertain", StringComparison.OrdinalIgnoreCase) -> PlcTagQuality.Uncertain
        | _ when value.StartsWith("Bad:", StringComparison.OrdinalIgnoreCase) ->
            let message = value.Substring(4)
            PlcTagQuality.Bad message
        | _ -> PlcTagQuality.Uncertain

    let serializeScalarValue (value: ScalarValue) =
        match value with
        | ScalarValue.BoolValue b -> "Bool", if b then "1" else "0"
        | ScalarValue.IntValue i -> "Int", i.ToString(CultureInfo.InvariantCulture)
        | ScalarValue.FloatValue f -> "Float", f.ToString(CultureInfo.InvariantCulture)
        | ScalarValue.StringValue s -> "String", s
        | ScalarValue.BytesValue bytes -> "Bytes", Convert.ToBase64String(bytes)

    let deserializeScalarValue (valueType: string) (valueText: string) =
        match valueType with
        | null | "" -> ScalarValue.StringValue valueText
        | _ ->
            match valueType.ToLowerInvariant() with
            | "bool" -> ScalarValue.BoolValue (valueText = "1" || valueText.Equals("true", StringComparison.OrdinalIgnoreCase))
            | "int" ->
                let parsed = Int64.Parse(valueText, CultureInfo.InvariantCulture)
                ScalarValue.IntValue parsed
            | "float" ->
                let parsed = Double.Parse(valueText, CultureInfo.InvariantCulture)
                ScalarValue.FloatValue parsed
            | "bytes" ->
                let data = Convert.FromBase64String(valueText)
                ScalarValue.BytesValue data
            | "string" -> ScalarValue.StringValue valueText
            | _ -> ScalarValue.StringValue valueText

    let toIso (value: DateTime) = value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
    let fromIso (value: string) = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)

/// SQLite 데이터 저장소 구현
type SQLiteRepository(connectionString: string) =

    let createConnection() =
        let connection = new SqliteConnection(connectionString)
        connection.Open()
        // 활성화
        use pragma = connection.CreateCommand()
        pragma.CommandText <- "PRAGMA foreign_keys = ON;"
        pragma.ExecuteNonQuery() |> ignore
        connection

    let ensureTables (connection: SqliteConnection) =
        let command = connection.CreateCommand()
        command.CommandText <- """
        CREATE TABLE IF NOT EXISTS plc_configurations (
            plc_id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            vendor TEXT NOT NULL,
            connection_string TEXT NULL,
            scan_interval INTEGER NOT NULL,
            is_enabled INTEGER NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS tag_configurations (
            tag_id TEXT PRIMARY KEY,
            plc_id TEXT NOT NULL REFERENCES plc_configurations(plc_id) ON DELETE CASCADE,
            name TEXT NOT NULL,
            address TEXT NOT NULL,
            data_type TEXT NOT NULL,
            is_enabled INTEGER NOT NULL,
            description TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS plc_data_points (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            tag_id TEXT NOT NULL,
            plc_id TEXT NOT NULL,
            value_text TEXT NOT NULL,
            value_type TEXT NOT NULL,
            quality TEXT NOT NULL,
            timestamp TEXT NOT NULL,
            FOREIGN KEY(tag_id) REFERENCES tag_configurations(tag_id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS idx_plc_data_points_tag ON plc_data_points(tag_id);
        CREATE INDEX IF NOT EXISTS idx_plc_data_points_plc ON plc_data_points(plc_id);
        CREATE INDEX IF NOT EXISTS idx_plc_data_points_timestamp ON plc_data_points(timestamp);
        """
        command.ExecuteNonQuery() |> ignore

    let mapConfiguration (reader: SqliteDataReader) : PLCConfiguration =
        let id = reader.GetString(reader.GetOrdinal("plc_id"))
        let name = reader.GetString(reader.GetOrdinal("name"))
        let vendor = reader.GetString(reader.GetOrdinal("vendor")) |> SQLiteHelpers.vendorFromString
        let connectionString =
            let ordinal = reader.GetOrdinal("connection_string")
            if reader.IsDBNull(ordinal) then "" else reader.GetString(ordinal)
        {
            Id = id
            Name = name
            Vendor = vendor
            ConnectionString = connectionString
            ScanInterval = reader.GetInt32(reader.GetOrdinal("scan_interval"))
            IsEnabled = reader.GetInt32(reader.GetOrdinal("is_enabled")) |> SQLiteHelpers.intToBool
            CreatedAt = reader.GetString(reader.GetOrdinal("created_at")) |> SQLiteHelpers.fromIso
            UpdatedAt = reader.GetString(reader.GetOrdinal("updated_at")) |> SQLiteHelpers.fromIso
        }

    let mapTag (reader: SqliteDataReader) : TagConfiguration =
        let id = reader.GetString(reader.GetOrdinal("tag_id"))
        let plcId = reader.GetString(reader.GetOrdinal("plc_id"))
        let name = reader.GetString(reader.GetOrdinal("name"))
        let address = reader.GetString(reader.GetOrdinal("address"))
        let dataType = reader.GetString(reader.GetOrdinal("data_type")) |> SQLiteHelpers.tagDataTypeFromString
        let descriptionOrdinal = reader.GetOrdinal("description")
        let description = if reader.IsDBNull(descriptionOrdinal) then None else Some (reader.GetString(descriptionOrdinal))
        {
            Id = id
            PlcId = plcId
            Name = name
            Address = address
            DataType = dataType
            IsEnabled = reader.GetInt32(reader.GetOrdinal("is_enabled")) |> SQLiteHelpers.intToBool
            Description = description
            CreatedAt = reader.GetString(reader.GetOrdinal("created_at")) |> SQLiteHelpers.fromIso
            UpdatedAt = reader.GetString(reader.GetOrdinal("updated_at")) |> SQLiteHelpers.fromIso
        }

    let mapDataPoint (reader: SqliteDataReader) : PLCDataPoint =
        let id = reader.GetInt64(reader.GetOrdinal("id"))
        let tagId = reader.GetString(reader.GetOrdinal("tag_id"))
        let plcId = reader.GetString(reader.GetOrdinal("plc_id"))
        let valueType = reader.GetString(reader.GetOrdinal("value_type"))
        let valueText = reader.GetString(reader.GetOrdinal("value_text"))
        let quality = reader.GetString(reader.GetOrdinal("quality")) |> SQLiteHelpers.qualityFromString
        {
            Id = id
            TagId = tagId
            PlcId = plcId
            Value = SQLiteHelpers.deserializeScalarValue valueType valueText
            Quality = quality
            Timestamp = reader.GetString(reader.GetOrdinal("timestamp")) |> SQLiteHelpers.fromIso
        }

    member private _.ExecuteNonQuery(sql: string, parameters: (string * obj) list) =
        use connection = createConnection()
        use command = connection.CreateCommand()
        command.CommandText <- sql
        for (name, value) in parameters do
            command.Parameters.AddWithValue(name, value) |> ignore
        command.ExecuteNonQuery() |> ignore

    member private _.QueryList<'a>(sql: string, parameters: (string * obj) list, projector: SqliteDataReader -> 'a) : 'a list =
        use connection = createConnection()
        use command = connection.CreateCommand()
        command.CommandText <- sql
        for (name, value) in parameters do
            command.Parameters.AddWithValue(name, value) |> ignore
        use reader = command.ExecuteReader()
        let results = ResizeArray<'a>()
        while reader.Read() do
            results.Add(projector reader)
        List.ofSeq results

    member private _.QuerySingle<'a>(sql: string, parameters: (string * obj) list, projector: SqliteDataReader -> 'a) : 'a option =
        use connection = createConnection()
        use command = connection.CreateCommand()
        command.CommandText <- sql
        for (name, value) in parameters do
            command.Parameters.AddWithValue(name, value) |> ignore
        use reader = command.ExecuteReader()
        if reader.Read() then
            Some (projector reader)
        else
            None

    member private _.ExecuteScalar<'a>(sql: string, parameters: (string * obj) list) : 'a option =
        use connection = createConnection()
        use command = connection.CreateCommand()
        command.CommandText <- sql
        for (name, value) in parameters do
            command.Parameters.AddWithValue(name, value) |> ignore
        let result = command.ExecuteScalar()
        if isNull result || result = DBNull.Value then None else Some (result :?> 'a)

    interface IDataRepository with
        member this.Initialize() =
            use connection = createConnection()
            ensureTables connection

        member this.UpsertPLCConfiguration(config: PLCConfiguration) =
            let sql = """
                INSERT INTO plc_configurations (plc_id, name, vendor, connection_string, scan_interval, is_enabled, created_at, updated_at)
                VALUES ($plc_id, $name, $vendor, $connection_string, $scan_interval, $is_enabled, $created_at, $updated_at)
                ON CONFLICT(plc_id) DO UPDATE SET
                    name = excluded.name,
                    vendor = excluded.vendor,
                    connection_string = excluded.connection_string,
                    scan_interval = excluded.scan_interval,
                    is_enabled = excluded.is_enabled,
                    updated_at = excluded.updated_at;
                """
            this.ExecuteNonQuery(sql, [
                ("$plc_id", box config.Id);
                ("$name", box config.Name);
                ("$vendor", box (SQLiteHelpers.vendorToString config.Vendor));
                ("$connection_string", if String.IsNullOrWhiteSpace(config.ConnectionString) then box DBNull.Value else box config.ConnectionString);
                ("$scan_interval", box config.ScanInterval);
                ("$is_enabled", box (SQLiteHelpers.boolToInt config.IsEnabled));
                ("$created_at", box (SQLiteHelpers.toIso config.CreatedAt));
                ("$updated_at", box (SQLiteHelpers.toIso config.UpdatedAt))
            ])

        member this.GetPLCConfiguration(plcId: string) =
            let sql = "SELECT * FROM plc_configurations WHERE plc_id = $plc_id"
            this.QuerySingle(sql, [ ("$plc_id", box plcId) ], mapConfiguration)

        member this.GetAllPLCConfigurations() =
            let sql = "SELECT * FROM plc_configurations ORDER BY name"
            this.QueryList(sql, [], mapConfiguration)

        member this.DeletePLCConfiguration(plcId: string) =
            let sql = "DELETE FROM plc_configurations WHERE plc_id = $plc_id"
            this.ExecuteNonQuery(sql, [ ("$plc_id", box plcId) ])

        member this.UpsertTagConfiguration(config: TagConfiguration) =
            let sql = """
                INSERT INTO tag_configurations (tag_id, plc_id, name, address, data_type, is_enabled, description, created_at, updated_at)
                VALUES ($tag_id, $plc_id, $name, $address, $data_type, $is_enabled, $description, $created_at, $updated_at)
                ON CONFLICT(tag_id) DO UPDATE SET
                    plc_id = excluded.plc_id,
                    name = excluded.name,
                    address = excluded.address,
                    data_type = excluded.data_type,
                    is_enabled = excluded.is_enabled,
                    description = excluded.description,
                    updated_at = excluded.updated_at;
                """
            this.ExecuteNonQuery(sql, [
                ("$tag_id", box config.Id);
                ("$plc_id", box config.PlcId);
                ("$name", box config.Name);
                ("$address", box config.Address);
                ("$data_type", box (SQLiteHelpers.tagDataTypeToString config.DataType));
                ("$is_enabled", box (SQLiteHelpers.boolToInt config.IsEnabled));
                ("$description", match config.Description with | Some value -> box value | None -> box DBNull.Value);
                ("$created_at", box (SQLiteHelpers.toIso config.CreatedAt));
                ("$updated_at", box (SQLiteHelpers.toIso config.UpdatedAt))
            ])

        member this.GetTagConfiguration(tagId: string) =
            let sql = "SELECT * FROM tag_configurations WHERE tag_id = $tag_id"
            this.QuerySingle(sql, [ ("$tag_id", box tagId) ], mapTag)

        member this.GetTagConfigurationsByPlc(plcId: string) =
            let sql = "SELECT * FROM tag_configurations WHERE plc_id = $plc_id ORDER BY name"
            this.QueryList(sql, [ ("$plc_id", box plcId) ], mapTag)

        member this.GetAllTagConfigurations() =
            let sql = "SELECT * FROM tag_configurations ORDER BY plc_id, name"
            this.QueryList(sql, [], mapTag)

        member this.DeleteTagConfiguration(tagId: string) =
            let sql = "DELETE FROM tag_configurations WHERE tag_id = $tag_id"
            this.ExecuteNonQuery(sql, [ ("$tag_id", box tagId) ])

        member this.InsertDataPoints(dataPoints: PLCDataPoint list) =
            if List.isEmpty dataPoints then () else
                use connection = createConnection()
                use transaction = connection.BeginTransaction()
                use command = connection.CreateCommand()
                command.Transaction <- transaction
                command.CommandText <- """
                    INSERT INTO plc_data_points (tag_id, plc_id, value_text, value_type, quality, timestamp)
                    VALUES ($tag_id, $plc_id, $value_text, $value_type, $quality, $timestamp)
                """
                let tagParam = command.CreateParameter()
                tagParam.ParameterName <- "$tag_id"
                command.Parameters.Add(tagParam) |> ignore
                let plcParam = command.CreateParameter()
                plcParam.ParameterName <- "$plc_id"
                command.Parameters.Add(plcParam) |> ignore
                let valueTextParam = command.CreateParameter()
                valueTextParam.ParameterName <- "$value_text"
                command.Parameters.Add(valueTextParam) |> ignore
                let valueTypeParam = command.CreateParameter()
                valueTypeParam.ParameterName <- "$value_type"
                command.Parameters.Add(valueTypeParam) |> ignore
                let qualityParam = command.CreateParameter()
                qualityParam.ParameterName <- "$quality"
                command.Parameters.Add(qualityParam) |> ignore
                let timestampParam = command.CreateParameter()
                timestampParam.ParameterName <- "$timestamp"
                command.Parameters.Add(timestampParam) |> ignore

                for dataPoint in dataPoints do
                    let valueType, valueText = SQLiteHelpers.serializeScalarValue dataPoint.Value
                    tagParam.Value <- dataPoint.TagId
                    plcParam.Value <- dataPoint.PlcId
                    valueTextParam.Value <- valueText
                    valueTypeParam.Value <- valueType
                    qualityParam.Value <- SQLiteHelpers.qualityToString dataPoint.Quality
                    timestampParam.Value <- SQLiteHelpers.toIso dataPoint.Timestamp
                    command.ExecuteNonQuery() |> ignore

                transaction.Commit()

        member this.QueryLatestByPlc(plcId: string) =
            let sql = """
                SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp
                FROM (
                    SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp,
                           ROW_NUMBER() OVER (PARTITION BY tag_id ORDER BY timestamp DESC) AS row_num
                    FROM plc_data_points
                    WHERE plc_id = $plc_id
                )
                WHERE row_num = 1
                ORDER BY timestamp DESC;
            """
            this.QueryList(sql, [ ("$plc_id", box plcId) ], mapDataPoint)

        member this.QueryLatestByTag(tagId: string) =
            let sql = """
                SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp
                FROM plc_data_points
                WHERE tag_id = $tag_id
                ORDER BY timestamp DESC
                LIMIT 1;
            """
            this.QuerySingle(sql, [ ("$tag_id", box tagId) ], mapDataPoint)

        member this.QueryRangeByTag(tagId: string, options: QueryOptions) =
            let sql = System.Text.StringBuilder()
            sql.Append("SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp FROM plc_data_points WHERE tag_id = $tag_id") |> ignore
            let parameters = System.Collections.Generic.List<string * obj>()
            parameters.Add(("$tag_id", box tagId))

            options.StartTime |> Option.iter (fun start ->
                sql.Append(" AND timestamp >= $start_time") |> ignore
                parameters.Add(("$start_time", box (SQLiteHelpers.toIso start)))
            )
            options.EndTime |> Option.iter (fun finish ->
                sql.Append(" AND timestamp <= $end_time") |> ignore
                parameters.Add(("$end_time", box (SQLiteHelpers.toIso finish)))
            )
            sql.Append(" ORDER BY timestamp ") |> ignore
            sql.Append(if options.OrderDesc then "DESC" else "ASC") |> ignore
            options.Limit |> Option.iter (fun limit ->
                sql.Append(" LIMIT $limit") |> ignore
                parameters.Add(("$limit", box limit))
            )

            this.QueryList(sql.ToString(), List.ofSeq parameters, mapDataPoint)

        member this.QueryRangeByPlc(plcId: string, options: QueryOptions) =
            let sql = System.Text.StringBuilder()
            sql.Append("SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp FROM plc_data_points WHERE plc_id = $plc_id") |> ignore
            let parameters = System.Collections.Generic.List<string * obj>()
            parameters.Add(("$plc_id", box plcId))

            options.StartTime |> Option.iter (fun start ->
                sql.Append(" AND timestamp >= $start_time") |> ignore
                parameters.Add(("$start_time", box (SQLiteHelpers.toIso start)))
            )
            options.EndTime |> Option.iter (fun finish ->
                sql.Append(" AND timestamp <= $end_time") |> ignore
                parameters.Add(("$end_time", box (SQLiteHelpers.toIso finish)))
            )
            sql.Append(" ORDER BY timestamp ") |> ignore
            sql.Append(if options.OrderDesc then "DESC" else "ASC") |> ignore
            options.Limit |> Option.iter (fun limit ->
                sql.Append(" LIMIT $limit") |> ignore
                parameters.Add(("$limit", box limit))
            )

            this.QueryList(sql.ToString(), List.ofSeq parameters, mapDataPoint)

        member this.PruneBefore(cutoff: DateTime) =
            let sql = "DELETE FROM plc_data_points WHERE timestamp < $cutoff"
            use connection = createConnection()
            use command = connection.CreateCommand()
            command.CommandText <- sql
            command.Parameters.AddWithValue("$cutoff", SQLiteHelpers.toIso cutoff) |> ignore
            int64 (command.ExecuteNonQuery())

        member this.HealthCheck() =
            try
                let sql = "SELECT 1"
                match this.ExecuteScalar<int64>(sql, []) with
                | Some _ -> true
                | None -> false
            with
            | _ -> false

        member this.GetDataPointCount() =
            let sql = "SELECT COUNT(*) FROM plc_data_points"
            this.ExecuteScalar<int64>(sql, []) |> Option.defaultValue 0L

        member this.GetDataPointCountByPlc(plcId: string) =
            let sql = "SELECT COUNT(*) FROM plc_data_points WHERE plc_id = $plc_id"
            this.ExecuteScalar<int64>(sql, [ ("$plc_id", box plcId) ]) |> Option.defaultValue 0L
