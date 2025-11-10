namespace Ev2.Cpu.Runtime

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Retain Memory System - 전원 OFF/ON 시 변수 값 보존
// ═════════════════════════════════════════════════════════════════════════════
// PLC 전원이 꺼져도 값을 유지해야 하는 변수(Retain 변수)를 파일에 저장/복원
// 지원 대상: Local, Internal, FB Static 변수
// ═════════════════════════════════════════════════════════════════════════════

/// JSON converter for F# Map<string, string>
type MapStringStringConverter() =
    inherit JsonConverter<Map<string, string>>()

    override _.Read(reader: byref<Utf8JsonReader>, typeToConvert: Type, options: JsonSerializerOptions) =
        if reader.TokenType <> JsonTokenType.StartObject then
            raise (JsonException("Expected StartObject token"))

        let mutable map = Map.empty<string, string>
        let mutable continueReading = true
        while continueReading && reader.Read() do
            match reader.TokenType with
            | JsonTokenType.EndObject ->
                continueReading <- false
            | JsonTokenType.PropertyName ->
                let key = reader.GetString()
                if not (reader.Read()) then
                    raise (JsonException("Expected property value"))
                let value = reader.GetString()
                map <- Map.add key value map
            | _ ->
                raise (JsonException("Unexpected token in Map"))
        map

    override _.Write(writer: Utf8JsonWriter, value: Map<string, string>, options: JsonSerializerOptions) =
        writer.WriteStartObject()
        for KeyValue(k, v) in value do
            writer.WriteString(k, v)
        writer.WriteEndObject()

/// 리테인될 변수 하나의 데이터
[<Struct>]
type RetainVariable = {
    /// 변수 이름
    Name: string
    /// 메모리 영역 (Local, Internal, etc.)
    Area: string
    /// 데이터 타입 (TInt, TBool, etc.)
    DataType: string
    /// 직렬화된 값 (JSON 문자열)
    ValueJson: string
}

/// FB Static 변수 하나의 데이터 (MEDIUM FIX: 타입 메타데이터 추가)
[<Struct>]
type FBStaticVariable = {
    /// 변수 이름
    Name: string
    /// 데이터 타입 (TInt, TBool, etc.)
    DataType: string
    /// 직렬화된 값 (JSON 문자열)
    ValueJson: string
}

/// FB 인스턴스의 Static 변수 데이터
type FBStaticData = {
    /// FB 인스턴스 이름
    InstanceName: string
    /// Static 변수 목록 (MEDIUM FIX: 타입 정보 포함)
    Variables: FBStaticVariable list
}

/// 전체 리테인 데이터 스냅샷
type RetainSnapshot = {
    /// 스냅샷 생성 시각
    Timestamp: DateTime
    /// 버전 정보 (호환성 체크용)
    Version: int
    /// 리테인 변수 목록
    Variables: RetainVariable list
    /// FB Static 변수 데이터
    FBStaticData: FBStaticData list
    /// 데이터 무결성 검증용 체크섬 (SHA256, base64 인코딩)
    Checksum: string
}

/// 리테인 저장소 인터페이스
type IRetainStorage =
    /// 스냅샷을 저장소에 저장
    abstract Save: RetainSnapshot -> Result<unit, string>
    /// 저장소에서 스냅샷 로드 (없으면 None)
    abstract Load: unit -> Result<RetainSnapshot option, string>
    /// 저장소 삭제
    abstract Delete: unit -> Result<unit, string>

/// 체크섬 계산 헬퍼 (SHA256)
module ChecksumHelper =
    /// <summary>데이터의 SHA256 체크섬 계산</summary>
    /// <param name="data">체크섬 계산 대상 데이터</param>
    /// <returns>Base64 인코딩된 SHA256 해시</returns>
    let computeSHA256 (data: string) : string =
        use sha256 = SHA256.Create()
        let bytes = Encoding.UTF8.GetBytes(data)
        let hash = sha256.ComputeHash(bytes)
        Convert.ToBase64String(hash)

    /// <summary>RetainSnapshot의 데이터 부분만 직렬화 (체크섬 계산용)</summary>
    /// <param name="variables">변수 목록</param>
    /// <param name="fbStaticData">FB Static 데이터</param>
    /// <returns>JSON 문자열 (정렬된 키로 결정론적 출력)</returns>
    let serializeDataForChecksum (variables: RetainVariable list) (fbStaticData: FBStaticData list) : string =
        let opts = JsonSerializerOptions()
        opts.IncludeFields <- true
        opts.WriteIndented <- false
        // 결정론적 출력을 위해 변수를 이름순으로 정렬
        let sortedVars = variables |> List.sortBy (fun v -> v.Name)
        let sortedFBs = fbStaticData |> List.sortBy (fun fb -> fb.InstanceName)
        let data = {| Variables = sortedVars; FBStaticData = sortedFBs |}
        JsonSerializer.Serialize(data, opts)

/// 바이너리 파일 기반 리테인 저장소
/// JSON을 바이트로 직렬화하여 바이너리 파일에 저장
type BinaryRetainStorage(filePath: string) =

    let jsonOptions = JsonSerializerOptions()
    do
        jsonOptions.WriteIndented <- false
        jsonOptions.IncludeFields <- true
        jsonOptions.Converters.Add(MapStringStringConverter())

    /// 스냅샷을 JSON으로 직렬화 후 바이트로 변환하여 저장
    let saveToFile (snapshot: RetainSnapshot) : Result<unit, string> =
        try
            // 디렉토리가 없으면 생성
            let directory = Path.GetDirectoryName(filePath)
            if not (String.IsNullOrEmpty(directory)) && not (Directory.Exists(directory)) then
                Directory.CreateDirectory(directory) |> ignore

            // 체크섬 계산 (데이터 부분만 사용)
            let dataJson = ChecksumHelper.serializeDataForChecksum snapshot.Variables snapshot.FBStaticData
            let checksum = ChecksumHelper.computeSHA256 dataJson

            // 체크섬을 포함한 최종 스냅샷 생성
            let snapshotWithChecksum = { snapshot with Checksum = checksum }

            // JSON 직렬화
            let json = JsonSerializer.Serialize(snapshotWithChecksum, jsonOptions)
            let bytes = System.Text.Encoding.UTF8.GetBytes(json)

            // 임시 파일에 먼저 저장 (원자적 쓰기)
            let tempPath = filePath + ".tmp"
            File.WriteAllBytes(tempPath, bytes)

            // 기존 파일이 있으면 백업
            if File.Exists(filePath) then
                let backupPath = filePath + ".bak"
                if File.Exists(backupPath) then
                    File.Delete(backupPath)
                File.Move(filePath, backupPath)

            // 임시 파일을 실제 파일로 이동
            File.Move(tempPath, filePath)

            Ok ()
        with
        | ex -> Error (sprintf "Failed to save retain data: %s" ex.Message)

    /// 파일에서 바이트 읽고 JSON 역직렬화 (체크섬 검증 포함)
    let loadFromFile () : Result<RetainSnapshot option, string> =
        let tryLoadAndValidate (path: string) =
            try
                let bytes = File.ReadAllBytes(path)
                let json = System.Text.Encoding.UTF8.GetString(bytes)
                let snapshot = JsonSerializer.Deserialize<RetainSnapshot>(json, jsonOptions)

                // 체크섬 검증 (빈 문자열이면 레거시 데이터로 간주하고 경고만)
                if String.IsNullOrEmpty(snapshot.Checksum) then
                    eprintfn "[RETAIN] Warning: No checksum found (legacy data or corrupted file)"
                    Ok (Some snapshot)
                else
                    // 데이터의 체크섬 재계산
                    let dataJson = ChecksumHelper.serializeDataForChecksum snapshot.Variables snapshot.FBStaticData
                    let expectedChecksum = ChecksumHelper.computeSHA256 dataJson

                    if snapshot.Checksum = expectedChecksum then
                        Ok (Some snapshot)
                    else
                        Error (sprintf "Checksum mismatch (expected: %s, actual: %s) - data may be corrupted"
                                expectedChecksum snapshot.Checksum)
            with
            | ex -> Error (sprintf "Failed to load: %s" ex.Message)

        try
            if not (File.Exists(filePath)) then
                Ok None
            else
                match tryLoadAndValidate filePath with
                | Ok result -> Ok result
                | Error mainError ->
                    // 로드 실패 시 백업 파일 시도
                    let backupPath = filePath + ".bak"
                    if File.Exists(backupPath) then
                        match tryLoadAndValidate backupPath with
                        | Ok result ->
                            eprintfn "[RETAIN] Loaded from backup file (main file corrupted: %s)" mainError
                            Ok result
                        | Error backupError ->
                            Error (sprintf "Main file error: %s; Backup file error: %s" mainError backupError)
                    else
                        Error mainError
        with
        | ex -> Error (sprintf "Unexpected error: %s" ex.Message)

    // Public members for direct access
    member _.Save(snapshot) = saveToFile snapshot
    member _.Load() = loadFromFile ()
    member _.Delete() =
        try
            if File.Exists(filePath) then
                File.Delete(filePath)
            let backupPath = filePath + ".bak"
            if File.Exists(backupPath) then
                File.Delete(backupPath)
            Ok ()
        with
        | ex -> Error (sprintf "Failed to delete retain data: %s" ex.Message)

    // Interface implementation
    interface IRetainStorage with
        member this.Save(snapshot) = this.Save(snapshot)
        member this.Load() = this.Load()
        member this.Delete() = this.Delete()

/// 값 직렬화/역직렬화 헬퍼
module RetainValueSerializer =

    // JSON options that allow NaN/Infinity values
    let private jsonOptions =
        let opts = JsonSerializerOptions()
        opts.NumberHandling <- JsonNumberHandling.AllowNamedFloatingPointLiterals
        opts

    /// 객체를 JSON 문자열로 직렬화
    let serialize (value: obj) (dataType: DsDataType) : string =
        if isNull value then
            "null"
        else
            match dataType with
            | DsDataType.TBool -> JsonSerializer.Serialize(unbox<bool> value, jsonOptions)
            | DsDataType.TInt -> JsonSerializer.Serialize(unbox<int> value, jsonOptions)
            | DsDataType.TDouble -> JsonSerializer.Serialize(unbox<double> value, jsonOptions)
            | DsDataType.TString -> JsonSerializer.Serialize(unbox<string> value, jsonOptions)

    /// JSON 문자열을 객체로 역직렬화
    let deserialize (json: string) (dataType: DsDataType) : obj =
        if json = "null" then
            null
        else
            try
                match dataType with
                | DsDataType.TBool -> box (JsonSerializer.Deserialize<bool>(json, jsonOptions))
                | DsDataType.TInt -> box (JsonSerializer.Deserialize<int>(json, jsonOptions))
                | DsDataType.TDouble -> box (JsonSerializer.Deserialize<double>(json, jsonOptions))
                | DsDataType.TString -> box (JsonSerializer.Deserialize<string>(json, jsonOptions))
            with
            | ex ->
                // 역직렬화 실패 시 경고 로깅 후 기본값 반환
                eprintfn "WARNING: Failed to deserialize retain value (type=%A, json='%s'): %s. Using default value."
                    dataType json ex.Message
                dataType.DefaultValue

/// 기본 리테인 저장소 경로
module RetainDefaults =
    /// 기본 리테인 파일 경로
    let DefaultRetainFilePath = "retain.dat"

    /// 현재 버전 (호환성 체크용)
    let CurrentVersion = 1
