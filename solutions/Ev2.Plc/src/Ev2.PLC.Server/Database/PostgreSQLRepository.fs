namespace DSPLCServer.Database

open System
open System.Globalization
open System.Data
open Npgsql
open DSPLCServer.Common

module private PostgreSqlHelpers =
    let vendorToString = function
        | PlcVendor.LSElectric _ -> "LSElectric"
        | PlcVendor.Mitsubishi _ -> "Mitsubishi"
        | PlcVendor.Siemens _ -> "Siemens"
        | PlcVendor.AllenBradley _ -> "AllenBradley"
        | PlcVendor.Custom (name, _, _) -> name

    let vendorFromString (value: string) =
        match value with
        | null | "" -> PlcVendor.CreateLSElectric()
        | _ ->
            match value.ToLowerInvariant() with
            | "lselectric" -> PlcVendor.CreateLSElectric()
            | "mitsubishi" -> PlcVendor.CreateMitsubishi()
            | "siemens" -> PlcVendor.CreateSiemens()
            | "allenbradley" | "allen-bradley" -> PlcVendor.CreateAllenBradley()
            | _ -> PlcVendor.CreateLSElectric()

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
            PlcTagQuality.Bad(value.Substring(4))
        | _ -> PlcTagQuality.Uncertain

    let serializeScalarValue (value: ScalarValue) =
        match value with
        | ScalarValue.BoolValue b -> "Bool", if b then "1" else "0"
        | ScalarValue.IntValue i -> "Int", i.ToString(CultureInfo.InvariantCulture)
        | ScalarValue.FloatValue f -> "Float", f.ToString(CultureInfo.InvariantCulture)
        | ScalarValue.StringValue s -> "String", s
        | ScalarValue.BytesValue bytes -> "Bytes", Convert.ToBase64String(bytes)

    let deserializeScalarValue valueType valueText =
        match valueType with
        | null | "" -> ScalarValue.StringValue valueText
        | _ ->
            match valueType.ToLowerInvariant() with
            | "bool" -> ScalarValue.BoolValue (valueText = "1" || valueText.Equals("true", StringComparison.OrdinalIgnoreCase))
            | "int" -> ScalarValue.IntValue(Int64.Parse(valueText, CultureInfo.InvariantCulture))
            | "float" -> ScalarValue.FloatValue(Double.Parse(valueText, CultureInfo.InvariantCulture))
            | "bytes" -> ScalarValue.BytesValue(Convert.FromBase64String(valueText))
            | "string" -> ScalarValue.StringValue valueText
            | _ -> ScalarValue.StringValue valueText

/// PostgreSQL 저장소 구현
type PostgreSQLRepository(connectionString: string) =

    let createConnection() =
        let connection = new NpgsqlConnection(connectionString)
        connection.Open()
        connection

    let ensureTables (connection: NpgsqlConnection) =
        use command = connection.CreateCommand()
        command.CommandText <- """
        CREATE TABLE IF NOT EXISTS plc_configurations (
            plc_id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            vendor TEXT NOT NULL,
            connection_string TEXT NULL,
            scan_interval INTEGER NOT NULL,
            is_enabled BOOLEAN NOT NULL,
            created_at TIMESTAMPTZ NOT NULL,
            updated_at TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS tag_configurations (
            tag_id TEXT PRIMARY KEY,
            plc_id TEXT NOT NULL REFERENCES plc_configurations(plc_id) ON DELETE CASCADE,
            name TEXT NOT NULL,
            address TEXT NOT NULL,
            data_type TEXT NOT NULL,
            is_enabled BOOLEAN NOT NULL,
            description TEXT NULL,
            created_at TIMESTAMPTZ NOT NULL,
            updated_at TIMESTAMPTZ NOT NULL
        );

        CREATE TABLE IF NOT EXISTS plc_data_points (
            id BIGSERIAL PRIMARY KEY,
            tag_id TEXT NOT NULL REFERENCES tag_configurations(tag_id) ON DELETE CASCADE,
            plc_id TEXT NOT NULL,
            value_text TEXT NOT NULL,
            value_type TEXT NOT NULL,
            quality TEXT NOT NULL,
            timestamp TIMESTAMPTZ NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_plc_data_points_tag ON plc_data_points(tag_id);
        CREATE INDEX IF NOT EXISTS idx_plc_data_points_plc ON plc_data_points(plc_id);
        CREATE INDEX IF NOT EXISTS idx_plc_data_points_timestamp ON plc_data_points(timestamp);
        """
        command.ExecuteNonQuery() |> ignore

    let configureCommand (command: NpgsqlCommand) (parameters: (string * obj) list) =
        command.Parameters.Clear()
        for (name, value) in parameters do
            let parameter = command.CreateParameter()
            parameter.ParameterName <- name
            parameter.Value <-
                match value with
                | null -> box DBNull.Value
                | :? string as s when String.IsNullOrEmpty(s) -> box s
                | _ -> value
            command.Parameters.Add(parameter) |> ignore

    let mapConfiguration (reader: NpgsqlDataReader) : PLCConfiguration =
        {
            Id = reader.GetString(reader.GetOrdinal("plc_id"))
            Name = reader.GetString(reader.GetOrdinal("name"))
            Vendor = PostgreSqlHelpers.vendorFromString (reader.GetString(reader.GetOrdinal("vendor")))
            ConnectionString =
                let ordinal = reader.GetOrdinal("connection_string")
                if reader.IsDBNull(ordinal) then "" else reader.GetString(ordinal)
            ScanInterval = reader.GetInt32(reader.GetOrdinal("scan_interval"))
            IsEnabled = reader.GetBoolean(reader.GetOrdinal("is_enabled"))
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
        }

    let mapTag (reader: NpgsqlDataReader) : TagConfiguration =
        {
            Id = reader.GetString(reader.GetOrdinal("tag_id"))
            PlcId = reader.GetString(reader.GetOrdinal("plc_id"))
            Name = reader.GetString(reader.GetOrdinal("name"))
            Address = reader.GetString(reader.GetOrdinal("address"))
            DataType = PostgreSqlHelpers.tagDataTypeFromString (reader.GetString(reader.GetOrdinal("data_type")))
            IsEnabled = reader.GetBoolean(reader.GetOrdinal("is_enabled"))
            Description =
                let ordinal = reader.GetOrdinal("description")
                if reader.IsDBNull(ordinal) then None else Some (reader.GetString(ordinal))
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
        }

    let mapDataPoint (reader: NpgsqlDataReader) : PLCDataPoint =
        let valueType = reader.GetString(reader.GetOrdinal("value_type"))
        let valueText = reader.GetString(reader.GetOrdinal("value_text"))
        {
            Id = reader.GetInt64(reader.GetOrdinal("id"))
            TagId = reader.GetString(reader.GetOrdinal("tag_id"))
            PlcId = reader.GetString(reader.GetOrdinal("plc_id"))
            Value = PostgreSqlHelpers.deserializeScalarValue valueType valueText
            Quality = reader.GetString(reader.GetOrdinal("quality")) |> PostgreSqlHelpers.qualityFromString
            Timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp"))
        }

    member private this.ExecuteNonQuery(sql: string, parameters: (string * obj) list) =
        use connection = createConnection()
        use command = connection.CreateCommand()
        command.CommandText <- sql
        configureCommand command parameters
        command.ExecuteNonQuery() |> ignore

    member private this.ExecuteScalar<'a>(sql: string, parameters: (string * obj) list) : 'a option =
        use connection = createConnection()
        use command = connection.CreateCommand()
        command.CommandText <- sql
        configureCommand command parameters
        let result = command.ExecuteScalar()
        if isNull result || result = DBNull.Value then None else Some (result :?> 'a)

    member private this.QueryList<'a>(sql: string, parameters: (string * obj) list, projector: NpgsqlDataReader -> 'a) : 'a list =
        use connection = createConnection()
        use command = connection.CreateCommand()
        command.CommandText <- sql
        configureCommand command parameters
        use readerObj = command.ExecuteReader()
        let reader = readerObj :?> NpgsqlDataReader
        let results = ResizeArray<'a>()
        while reader.Read() do
            results.Add(projector reader)
        List.ofSeq results

    member private this.QuerySingle<'a>(sql: string, parameters: (string * obj) list, projector: NpgsqlDataReader -> 'a) : 'a option =
        use connection = createConnection()
        use command = connection.CreateCommand()
        command.CommandText <- sql
        configureCommand command parameters
        use readerObj = command.ExecuteReader()
        let reader = readerObj :?> NpgsqlDataReader
        if reader.Read() then Some (projector reader) else None

    interface IDataRepository with
        member this.Initialize() =
            use connection = createConnection()
            ensureTables connection

        member this.UpsertPLCConfiguration(config: PLCConfiguration) =
            let sql = """
                INSERT INTO plc_configurations (plc_id, name, vendor, connection_string, scan_interval, is_enabled, created_at, updated_at)
                VALUES (@plc_id, @name, @vendor, @connection_string, @scan_interval, @is_enabled, @created_at, @updated_at)
                ON CONFLICT (plc_id) DO UPDATE SET
                    name = EXCLUDED.name,
                    vendor = EXCLUDED.vendor,
                    connection_string = EXCLUDED.connection_string,
                    scan_interval = EXCLUDED.scan_interval,
                    is_enabled = EXCLUDED.is_enabled,
                    updated_at = EXCLUDED.updated_at;
            """
            this.ExecuteNonQuery(sql, [
                ("@plc_id", box config.Id);
                ("@name", box config.Name);
                ("@vendor", box (PostgreSqlHelpers.vendorToString config.Vendor));
                ("@connection_string", if String.IsNullOrWhiteSpace(config.ConnectionString) then box DBNull.Value else box config.ConnectionString);
                ("@scan_interval", box config.ScanInterval);
                ("@is_enabled", box config.IsEnabled);
                ("@created_at", box config.CreatedAt);
                ("@updated_at", box config.UpdatedAt)
            ])

        member this.GetPLCConfiguration(plcId: string) =
            let sql = "SELECT * FROM plc_configurations WHERE plc_id = @plc_id"
            this.QuerySingle(sql, [ ("@plc_id", box plcId) ], mapConfiguration)

        member this.GetAllPLCConfigurations() =
            let sql = "SELECT * FROM plc_configurations ORDER BY name"
            this.QueryList(sql, [], mapConfiguration)

        member this.DeletePLCConfiguration(plcId: string) =
            let sql = "DELETE FROM plc_configurations WHERE plc_id = @plc_id"
            this.ExecuteNonQuery(sql, [ ("@plc_id", box plcId) ])

        member this.UpsertTagConfiguration(config: TagConfiguration) =
            let sql = """
                INSERT INTO tag_configurations (tag_id, plc_id, name, address, data_type, is_enabled, description, created_at, updated_at)
                VALUES (@tag_id, @plc_id, @name, @address, @data_type, @is_enabled, @description, @created_at, @updated_at)
                ON CONFLICT (tag_id) DO UPDATE SET
                    plc_id = EXCLUDED.plc_id,
                    name = EXCLUDED.name,
                    address = EXCLUDED.address,
                    data_type = EXCLUDED.data_type,
                    is_enabled = EXCLUDED.is_enabled,
                    description = EXCLUDED.description,
                    updated_at = EXCLUDED.updated_at;
            """
            this.ExecuteNonQuery(sql, [
                ("@tag_id", box config.Id);
                ("@plc_id", box config.PlcId);
                ("@name", box config.Name);
                ("@address", box config.Address);
                ("@data_type", box (PostgreSqlHelpers.tagDataTypeToString config.DataType));
                ("@is_enabled", box config.IsEnabled);
                ("@description", match config.Description with | Some value -> box value | None -> box DBNull.Value);
                ("@created_at", box config.CreatedAt);
                ("@updated_at", box config.UpdatedAt)
            ])

        member this.GetTagConfiguration(tagId: string) =
            let sql = "SELECT * FROM tag_configurations WHERE tag_id = @tag_id"
            this.QuerySingle(sql, [ ("@tag_id", box tagId) ], mapTag)

        member this.GetTagConfigurationsByPlc(plcId: string) =
            let sql = "SELECT * FROM tag_configurations WHERE plc_id = @plc_id ORDER BY name"
            this.QueryList(sql, [ ("@plc_id", box plcId) ], mapTag)

        member this.GetAllTagConfigurations() =
            let sql = "SELECT * FROM tag_configurations ORDER BY plc_id, name"
            this.QueryList(sql, [], mapTag)

        member this.DeleteTagConfiguration(tagId: string) =
            let sql = "DELETE FROM tag_configurations WHERE tag_id = @tag_id"
            this.ExecuteNonQuery(sql, [ ("@tag_id", box tagId) ])

        member this.InsertDataPoints(dataPoints: PLCDataPoint list) =
            if List.isEmpty dataPoints then () else
                use connection = createConnection()
                use transaction = connection.BeginTransaction()
                use command = connection.CreateCommand()
                command.CommandText <- """
                    INSERT INTO plc_data_points (tag_id, plc_id, value_text, value_type, quality, timestamp)
                    VALUES (@tag_id, @plc_id, @value_text, @value_type, @quality, @timestamp)
                """
                command.Transaction <- transaction

                let tagParam = command.CreateParameter()
                tagParam.ParameterName <- "@tag_id"
                command.Parameters.Add(tagParam) |> ignore

                let plcParam = command.CreateParameter()
                plcParam.ParameterName <- "@plc_id"
                command.Parameters.Add(plcParam) |> ignore

                let valueTextParam = command.CreateParameter()
                valueTextParam.ParameterName <- "@value_text"
                command.Parameters.Add(valueTextParam) |> ignore

                let valueTypeParam = command.CreateParameter()
                valueTypeParam.ParameterName <- "@value_type"
                command.Parameters.Add(valueTypeParam) |> ignore

                let qualityParam = command.CreateParameter()
                qualityParam.ParameterName <- "@quality"
                command.Parameters.Add(qualityParam) |> ignore

                let timestampParam = command.CreateParameter()
                timestampParam.ParameterName <- "@timestamp"
                command.Parameters.Add(timestampParam) |> ignore

                for dataPoint in dataPoints do
                    let valueType, valueText = PostgreSqlHelpers.serializeScalarValue dataPoint.Value
                    tagParam.Value <- dataPoint.TagId
                    plcParam.Value <- dataPoint.PlcId
                    valueTextParam.Value <- valueText
                    valueTypeParam.Value <- valueType
                    qualityParam.Value <- PostgreSqlHelpers.qualityToString dataPoint.Quality
                    timestampParam.Value <- dataPoint.Timestamp
                    command.ExecuteNonQuery() |> ignore

                transaction.Commit()

        member this.QueryLatestByPlc(plcId: string) =
            let sql = """
                SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp
                FROM (
                    SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp,
                           ROW_NUMBER() OVER (PARTITION BY tag_id ORDER BY timestamp DESC) AS rn
                    FROM plc_data_points
                    WHERE plc_id = @plc_id
                ) AS ranked
                WHERE rn = 1
                ORDER BY timestamp DESC;
            """
            this.QueryList(sql, [ ("@plc_id", box plcId) ], mapDataPoint)

        member this.QueryLatestByTag(tagId: string) =
            let sql = """
                SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp
                FROM plc_data_points
                WHERE tag_id = @tag_id
                ORDER BY timestamp DESC
                LIMIT 1;
            """
            this.QuerySingle(sql, [ ("@tag_id", box tagId) ], mapDataPoint)

        member this.QueryRangeByTag(tagId: string, options: QueryOptions) =
            let builder = System.Text.StringBuilder()
            builder.Append("SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp FROM plc_data_points WHERE tag_id = @tag_id") |> ignore
            let parameters = System.Collections.Generic.List<string * obj>()
            parameters.Add(("@tag_id", box tagId))

            options.StartTime |> Option.iter (fun start ->
                builder.Append(" AND timestamp >= @start_time") |> ignore
                parameters.Add(("@start_time", box start))
            )
            options.EndTime |> Option.iter (fun finish ->
                builder.Append(" AND timestamp <= @end_time") |> ignore
                parameters.Add(("@end_time", box finish))
            )
            builder.Append(" ORDER BY timestamp ") |> ignore
            builder.Append(if options.OrderDesc then "DESC" else "ASC") |> ignore
            options.Limit |> Option.iter (fun limit ->
                builder.Append(" LIMIT @limit") |> ignore
                parameters.Add(("@limit", box limit))
            )

            this.QueryList(builder.ToString(), List.ofSeq parameters, mapDataPoint)

        member this.QueryRangeByPlc(plcId: string, options: QueryOptions) =
            let builder = System.Text.StringBuilder()
            builder.Append("SELECT id, tag_id, plc_id, value_text, value_type, quality, timestamp FROM plc_data_points WHERE plc_id = @plc_id") |> ignore
            let parameters = System.Collections.Generic.List<string * obj>()
            parameters.Add(("@plc_id", box plcId))

            options.StartTime |> Option.iter (fun start ->
                builder.Append(" AND timestamp >= @start_time") |> ignore
                parameters.Add(("@start_time", box start))
            )
            options.EndTime |> Option.iter (fun finish ->
                builder.Append(" AND timestamp <= @end_time") |> ignore
                parameters.Add(("@end_time", box finish))
            )
            builder.Append(" ORDER BY timestamp ") |> ignore
            builder.Append(if options.OrderDesc then "DESC" else "ASC") |> ignore
            options.Limit |> Option.iter (fun limit ->
                builder.Append(" LIMIT @limit") |> ignore
                parameters.Add(("@limit", box limit))
            )

            this.QueryList(builder.ToString(), List.ofSeq parameters, mapDataPoint)

        member this.PruneBefore(cutoff: DateTime) =
            let sql = "DELETE FROM plc_data_points WHERE timestamp < @cutoff"
            use connection = createConnection()
            use command = connection.CreateCommand()
            command.CommandText <- sql
            configureCommand command [ ("@cutoff", box cutoff) ]
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
            let sql = "SELECT COUNT(*) FROM plc_data_points WHERE plc_id = @plc_id"
            this.ExecuteScalar<int64>(sql, [ ("@plc_id", box plcId) ]) |> Option.defaultValue 0L
