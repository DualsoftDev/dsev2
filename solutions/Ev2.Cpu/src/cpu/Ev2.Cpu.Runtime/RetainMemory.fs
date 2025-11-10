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
// 바이너리 직렬화로 성능 최적화 (JSON 대비 ~3배 빠름)
// ═════════════════════════════════════════════════════════════════════════════

/// 리테인될 변수 하나의 데이터
[<Struct>]
type RetainVariable = {
    /// 변수 이름
    Name: string
    /// 메모리 영역 (Local, Internal, etc.)
    Area: string
    /// 데이터 타입 (typeof<int>, typeof<bool>, etc.)
    DataType: string
    /// 직렬화된 값 (바이너리)
    ValueBytes: byte[]
}

/// FB Static 변수 하나의 데이터
[<Struct>]
type FBStaticVariable = {
    /// 변수 이름
    Name: string
    /// 데이터 타입 (typeof<int>, typeof<bool>, etc.)
    DataType: string
    /// 직렬화된 값 (바이너리)
    ValueBytes: byte[]
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

// ═════════════════════════════════════════════════════════════════════════════
// 스키마/값 분리 저장 시스템 (고성능 아키텍처)
// ═════════════════════════════════════════════════════════════════════════════
// 메타데이터(스키마)와 값(Value)을 분리하여 저장
// - retain_schema.json: 변수 구조 (변경 시에만 업데이트)
// - retain_values.dat: 값만 순수 바이너리 (매번 초고속 저장)
// 기대 효과: 15-30배 빠른 저장 속도, 70% 파일 크기 감소
// ═════════════════════════════════════════════════════════════════════════════

/// 변수 스키마 정보 (메타데이터만, 값 제외)
[<Struct>]
type VariableSchema = {
    /// 변수 이름
    Name: string
    /// 메모리 영역 (Local, Internal, etc.)
    Area: string
    /// 데이터 타입 문자열
    DataType: string
    /// 값 파일 내 오프셋 (bytes)
    Offset: int
    /// 고정 크기 (bytes)
    Size: int
}

/// FB Static 변수 스키마
[<Struct>]
type FBStaticVariableSchema = {
    /// 변수 이름
    Name: string
    /// 데이터 타입 문자열
    DataType: string
    /// 값 파일 내 오프셋 (bytes)
    Offset: int
    /// 고정 크기 (bytes)
    Size: int
}

/// FB 인스턴스 스키마
type FBStaticInstanceSchema = {
    /// FB 인스턴스 이름
    InstanceName: string
    /// Static 변수 스키마 목록
    Variables: FBStaticVariableSchema list
}

/// 전체 리테인 스키마 정의
type RetainSchema = {
    /// 스키마 버전
    Version: int
    /// 스키마 해시 (빠른 검증용)
    SchemaHash: string
    /// 스키마 생성 시각
    Timestamp: DateTime
    /// 전체 값 파일 크기 (bytes)
    TotalSize: int
    /// 변수 스키마 목록
    Variables: VariableSchema list
    /// FB Static 스키마 목록
    FBStatic: FBStaticInstanceSchema list
}

/// 리테인 저장소 인터페이스
type IRetainStorage =
    /// 스냅샷을 저장소에 저장
    abstract Save: RetainSnapshot -> Result<unit, string>
    /// 저장소에서 스냅샷 로드 (없으면 None)
    abstract Load: unit -> Result<RetainSnapshot option, string>
    /// 저장소 삭제
    abstract Delete: unit -> Result<unit, string>

/// 바이너리 직렬화 헬퍼
module BinarySerializer =

    /// 문자열을 바이너리로 쓰기
    let writeString (writer: BinaryWriter) (value: string) =
        if isNull value then
            writer.Write(-1)
        else
            let bytes = Encoding.UTF8.GetBytes(value)
            writer.Write(bytes.Length)
            writer.Write(bytes)

    /// 바이너리에서 문자열 읽기
    let readString (reader: BinaryReader) : string =
        let length = reader.ReadInt32()
        if length = -1 then
            null
        else
            let bytes = reader.ReadBytes(length)
            Encoding.UTF8.GetString(bytes)

    /// 바이트 배열 쓰기
    let writeBytes (writer: BinaryWriter) (bytes: byte[]) =
        if isNull bytes then
            writer.Write(-1)
        else
            writer.Write(bytes.Length)
            writer.Write(bytes)

    /// 바이트 배열 읽기
    let readBytes (reader: BinaryReader) : byte[] =
        let length = reader.ReadInt32()
        if length = -1 then
            null
        else
            reader.ReadBytes(length)

/// 체크섬 계산 헬퍼 (SHA256)
module ChecksumHelper =
    /// <summary>바이트 데이터의 SHA256 체크섬 계산</summary>
    /// <param name="data">체크섬 계산 대상 바이트 배열</param>
    /// <returns>Base64 인코딩된 SHA256 해시</returns>
    let computeSHA256 (data: byte[]) : string =
        use sha256 = SHA256.Create()
        let hash = sha256.ComputeHash(data)
        Convert.ToBase64String(hash)

/// 타입별 고정 크기 슬롯 매니저
module TypeSizes =
    /// String 타입의 고정 길이 (bytes)
    [<Literal>]
    let StringFixedSize = 256

    /// String의 실제 데이터 영역 크기 (length prefix 4 bytes 제외)
    [<Literal>]
    let StringDataSize = 252

    /// 타입 이름으로부터 고정 크기 반환 (bytes)
    let getSizeFromTypeName (typeName: string) : int =
        match typeName.ToLowerInvariant() with
        | "bool" | "boolean" -> 1
        | "sbyte" | "byte" -> 1
        | "int16" | "short" -> 2
        | "uint16" | "ushort" -> 2
        | "int" | "int32" -> 4
        | "uint" | "uint32" -> 4
        | "int64" | "long" -> 8
        | "uint64" | "ulong" -> 8
        | "double" | "float64" -> 8
        | "single" | "float" | "float32" -> 4
        | "string" -> StringFixedSize
        | _ -> invalidArg "typeName" (sprintf "Unsupported type for fixed-size slot: %s" typeName)

    /// Type으로부터 고정 크기 반환 (bytes)
    let getSize (dataType: Type) : int =
        if dataType = typeof<bool> then 1
        elif dataType = typeof<sbyte> then 1
        elif dataType = typeof<byte> then 1
        elif dataType = typeof<int16> then 2
        elif dataType = typeof<uint16> then 2
        elif dataType = typeof<int> then 4
        elif dataType = typeof<uint32> then 4
        elif dataType = typeof<int64> then 8
        elif dataType = typeof<uint64> then 8
        elif dataType = typeof<double> then 8
        elif dataType = typeof<single> then 4
        elif dataType = typeof<string> then StringFixedSize
        else invalidArg "dataType" (sprintf "Unsupported type for fixed-size slot: %s" dataType.FullName)

    /// String 값을 고정 크기 슬롯에 쓰기 (length-prefix + data)
    let writeStringToSlot (writer: BinaryWriter) (value: string) =
        if isNull value then
            writer.Write(0) // length = 0
            // 나머지 패딩
            writer.Write(Array.zeroCreate<byte> StringDataSize)
        else
            let bytes = Encoding.UTF8.GetBytes(value)
            let length = min bytes.Length StringDataSize
            writer.Write(length)
            writer.Write(bytes, 0, length)
            // 패딩
            if length < StringDataSize then
                writer.Write(Array.zeroCreate<byte> (StringDataSize - length))

    /// 고정 크기 슬롯에서 String 값 읽기
    let readStringFromSlot (reader: BinaryReader) : string =
        let length = reader.ReadInt32()
        if length = 0 then
            // 패딩 스킵
            reader.ReadBytes(StringDataSize) |> ignore
            ""
        else
            let bytes = reader.ReadBytes(length)
            // 나머지 패딩 스킵
            if length < StringDataSize then
                reader.ReadBytes(StringDataSize - length) |> ignore
            Encoding.UTF8.GetString(bytes)

/// 바이너리 파일 기반 리테인 저장소
/// 순수 바이너리 직렬화로 성능 최적화 (JSON 대비 ~3배 빠름, 파일 크기 ~50% 감소)
type BinaryRetainStorage(filePath: string) =

    // 매직 넘버와 버전 (파일 포맷 식별용)
    let [<Literal>] MagicNumber = 0x52544E44 // "RTND" (Retain Data)
    let [<Literal>] FormatVersion = 2 // Binary format version

    /// RetainVariable을 바이너리로 쓰기
    let writeRetainVariable (writer: BinaryWriter) (v: RetainVariable) =
        BinarySerializer.writeString writer v.Name
        BinarySerializer.writeString writer v.Area
        BinarySerializer.writeString writer v.DataType
        BinarySerializer.writeBytes writer v.ValueBytes

    /// 바이너리에서 RetainVariable 읽기
    let readRetainVariable (reader: BinaryReader) : RetainVariable =
        {
            Name = BinarySerializer.readString reader
            Area = BinarySerializer.readString reader
            DataType = BinarySerializer.readString reader
            ValueBytes = BinarySerializer.readBytes reader
        }

    /// FBStaticVariable을 바이너리로 쓰기
    let writeFBStaticVariable (writer: BinaryWriter) (v: FBStaticVariable) =
        BinarySerializer.writeString writer v.Name
        BinarySerializer.writeString writer v.DataType
        BinarySerializer.writeBytes writer v.ValueBytes

    /// 바이너리에서 FBStaticVariable 읽기
    let readFBStaticVariable (reader: BinaryReader) : FBStaticVariable =
        {
            Name = BinarySerializer.readString reader
            DataType = BinarySerializer.readString reader
            ValueBytes = BinarySerializer.readBytes reader
        }

    /// FBStaticData를 바이너리로 쓰기
    let writeFBStaticData (writer: BinaryWriter) (fb: FBStaticData) =
        BinarySerializer.writeString writer fb.InstanceName
        writer.Write(fb.Variables.Length)
        for v in fb.Variables do
            writeFBStaticVariable writer v

    /// 바이너리에서 FBStaticData 읽기
    let readFBStaticData (reader: BinaryReader) : FBStaticData =
        let instanceName = BinarySerializer.readString reader
        let varCount = reader.ReadInt32()
        let variables = [ for _ in 1..varCount -> readFBStaticVariable reader ]
        { InstanceName = instanceName; Variables = variables }

    /// 스냅샷을 순수 바이너리로 직렬화하여 저장
    let saveToFile (snapshot: RetainSnapshot) : Result<unit, string> =
        try
            // 디렉토리가 없으면 생성
            let directory = Path.GetDirectoryName(filePath)
            if not (String.IsNullOrEmpty(directory)) && not (Directory.Exists(directory)) then
                Directory.CreateDirectory(directory) |> ignore

            use ms = new MemoryStream()
            use writer = new BinaryWriter(ms, Encoding.UTF8, true)

            // 헤더: 매직 넘버 + 포맷 버전
            writer.Write(MagicNumber)
            writer.Write(FormatVersion)

            // 메타데이터
            writer.Write(snapshot.Timestamp.ToBinary())
            writer.Write(snapshot.Version)

            // Variables
            writer.Write(snapshot.Variables.Length)
            for v in snapshot.Variables do
                writeRetainVariable writer v

            // FBStaticData
            writer.Write(snapshot.FBStaticData.Length)
            for fb in snapshot.FBStaticData do
                writeFBStaticData writer fb

            writer.Flush()

            // 체크섬 계산 (데이터 부분만)
            let dataBytes = ms.ToArray()
            let checksum = ChecksumHelper.computeSHA256 dataBytes

            // 체크섬을 파일 끝에 추가
            BinarySerializer.writeString writer checksum
            writer.Flush()

            let finalBytes = ms.ToArray()

            // 임시 파일에 먼저 저장 (원자적 쓰기)
            let tempPath = filePath + ".tmp"
            File.WriteAllBytes(tempPath, finalBytes)

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

    /// 파일에서 바이너리 읽고 역직렬화 (체크섬 검증 포함)
    let loadFromFile () : Result<RetainSnapshot option, string> =
        let tryLoadAndValidate (path: string) =
            try
                let allBytes = File.ReadAllBytes(path)
                use ms = new MemoryStream(allBytes)
                use reader = new BinaryReader(ms, Encoding.UTF8)

                // 헤더 검증
                let magic = reader.ReadInt32()
                if magic <> MagicNumber then
                    Error "Invalid file format (wrong magic number)"
                else
                    let version = reader.ReadInt32()
                    if version <> FormatVersion then
                        Error (sprintf "Unsupported format version: %d (expected %d)" version FormatVersion)
                    else
                        // 메타데이터 읽기
                        let timestampBinary = reader.ReadInt64()
                        let timestamp = DateTime.FromBinary(timestampBinary)
                        let snapshotVersion = reader.ReadInt32()

                        // Variables 읽기
                        let varCount = reader.ReadInt32()
                        let variables = [ for _ in 1..varCount -> readRetainVariable reader ]

                        // FBStaticData 읽기
                        let fbCount = reader.ReadInt32()
                        let fbStaticData = [ for _ in 1..fbCount -> readFBStaticData reader ]

                        // 체크섬 검증 (체크섬 읽기 전 위치 저장)
                        let dataLength = int ms.Position
                        let dataBytes = Array.sub allBytes 0 dataLength
                        let calculatedChecksum = ChecksumHelper.computeSHA256 dataBytes

                        // 체크섬 읽기 (파일 끝)
                        let storedChecksum = BinarySerializer.readString reader

                        if storedChecksum <> calculatedChecksum then
                            Error (sprintf "Checksum mismatch - data may be corrupted")
                        else
                            let snapshot = {
                                Timestamp = timestamp
                                Version = snapshotVersion
                                Variables = variables
                                FBStaticData = fbStaticData
                                Checksum = storedChecksum
                            }
                            Ok (Some snapshot)
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

/// 값 직렬화/역직렬화 헬퍼 (순수 바이너리)
module RetainValueSerializer =

    /// 객체를 바이너리 바이트 배열로 직렬화
    let serialize (value: obj) (dataType: Type) : byte[] =
        if isNull value then
            [||] // null은 빈 배열로
        else
            use ms = new MemoryStream()
            use writer = new BinaryWriter(ms)

            if dataType = typeof<bool> then writer.Write(unbox<bool> value)
            elif dataType = typeof<sbyte> then writer.Write(unbox<sbyte> value)
            elif dataType = typeof<byte> then writer.Write(unbox<byte> value)
            elif dataType = typeof<int16> then writer.Write(unbox<int16> value)
            elif dataType = typeof<uint16> then writer.Write(unbox<uint16> value)
            elif dataType = typeof<int> then writer.Write(unbox<int> value)
            elif dataType = typeof<uint32> then writer.Write(unbox<uint32> value)
            elif dataType = typeof<int64> then writer.Write(unbox<int64> value)
            elif dataType = typeof<uint64> then writer.Write(unbox<uint64> value)
            elif dataType = typeof<double> then writer.Write(unbox<double> value)
            elif dataType = typeof<string> then BinarySerializer.writeString writer (unbox<string> value)
            else invalidArg "dataType" (sprintf "Unsupported type: %s" dataType.FullName)

            writer.Flush()
            ms.ToArray()

    /// 바이트 배열을 객체로 역직렬화
    let deserialize (bytes: byte[]) (dataType: Type) : obj =
        if isNull bytes || bytes.Length = 0 then
            null
        else
            try
                use ms = new MemoryStream(bytes)
                use reader = new BinaryReader(ms)

                if dataType = typeof<bool> then box (reader.ReadBoolean())
                elif dataType = typeof<sbyte> then box (reader.ReadSByte())
                elif dataType = typeof<byte> then box (reader.ReadByte())
                elif dataType = typeof<int16> then box (reader.ReadInt16())
                elif dataType = typeof<uint16> then box (reader.ReadUInt16())
                elif dataType = typeof<int> then box (reader.ReadInt32())
                elif dataType = typeof<uint32> then box (reader.ReadUInt32())
                elif dataType = typeof<int64> then box (reader.ReadInt64())
                elif dataType = typeof<uint64> then box (reader.ReadUInt64())
                elif dataType = typeof<double> then box (reader.ReadDouble())
                elif dataType = typeof<string> then box (BinarySerializer.readString reader)
                else invalidArg "dataType" (sprintf "Unsupported type: %s" dataType.FullName)
            with
            | :? EndOfStreamException ->
                // If can't read full value, return default
                TypeHelpers.getDefaultValue dataType
            | ex ->
                // Other errors, re-throw
                reraise()

/// 기본 리테인 저장소 경로
module RetainDefaults =
    /// 기본 리테인 파일 경로
    let DefaultRetainFilePath = "retain.dat"

    /// 현재 버전 (호환성 체크용)
    let CurrentVersion = 1

    /// 기본 스키마 파일 경로
    let DefaultSchemaFilePath = "retain_schema.json"

    /// 기본 값 파일 경로
    let DefaultValuesFilePath = "retain_values.dat"

// ═════════════════════════════════════════════════════════════════════════════
// 스키마 기반 고성능 리테인 저장소
// ═════════════════════════════════════════════════════════════════════════════

/// 스키마 해시 계산 헬퍼
module SchemaHasher =
    /// 스키마로부터 결정론적 해시 생성
    let computeSchemaHash (schema: RetainSchema) : string =
        use ms = new MemoryStream()
        use writer = new StreamWriter(ms, Encoding.UTF8)

        // 변수 스키마 직렬화 (이름, 타입, 오프셋, 크기)
        for v in schema.Variables do
            writer.WriteLine(sprintf "%s|%s|%s|%d|%d" v.Name v.Area v.DataType v.Offset v.Size)

        // FB Static 스키마 직렬화
        for fb in schema.FBStatic do
            writer.WriteLine(sprintf "FB:%s" fb.InstanceName)
            for v in fb.Variables do
                writer.WriteLine(sprintf "%s|%s|%d|%d" v.Name v.DataType v.Offset v.Size)

        writer.Flush()
        let bytes = ms.ToArray()
        ChecksumHelper.computeSHA256 bytes

/// 스키마 기반 리테인 저장소 (고성능 아키텍처)
/// - retain_schema.json: 메타데이터 (변경 시에만 업데이트)
/// - retain_values.dat: 값만 순수 바이너리 (매번 초고속 저장)
/// 기대 효과: 15-30배 빠른 저장 속도, 70% 파일 크기 감소
type SchemaBasedRetainStorage(schemaPath: string, valuesPath: string) =

    // 매직 넘버와 버전
    let [<Literal>] ValuesMagicNumber = 0x52544E56 // "RTNV" (Retain Values)
    let [<Literal>] ValuesFormatVersion = 1
    let [<Literal>] FooterMagicNumber = 0x454E4456 // "ENDV" (End Values)

    /// JSON으로 스키마 저장
    let saveSchemaToJson (schema: RetainSchema) : Result<unit, string> =
        try
            let directory = Path.GetDirectoryName(schemaPath)
            if not (String.IsNullOrEmpty(directory)) && not (Directory.Exists(directory)) then
                Directory.CreateDirectory(directory) |> ignore

            // System.Text.Json 사용
            let options = JsonSerializerOptions()
            options.WriteIndented <- true
            let json = JsonSerializer.Serialize(schema, options)

            // 원자적 쓰기
            let tempPath = schemaPath + ".tmp"
            File.WriteAllText(tempPath, json)

            if File.Exists(schemaPath) then
                let backupPath = schemaPath + ".bak"
                if File.Exists(backupPath) then
                    File.Delete(backupPath)
                File.Move(schemaPath, backupPath)

            File.Move(tempPath, schemaPath)
            Ok ()
        with
        | ex -> Error (sprintf "Failed to save schema: %s" ex.Message)

    /// JSON에서 스키마 로드
    let loadSchemaFromJson () : Result<RetainSchema option, string> =
        try
            if not (File.Exists(schemaPath)) then
                Ok None
            else
                let json = File.ReadAllText(schemaPath)
                let schema = JsonSerializer.Deserialize<RetainSchema>(json)
                Ok (Some schema)
        with
        | ex -> Error (sprintf "Failed to load schema: %s" ex.Message)

    /// 값 파일에 헤더 쓰기
    let writeValuesHeader (writer: BinaryWriter) (schemaHash: string) =
        writer.Write(ValuesMagicNumber)
        writer.Write(ValuesFormatVersion)
        // 스키마 해시의 처음 8 bytes (빠른 검증용)
        let hashBytes = Convert.FromBase64String(schemaHash)
        writer.Write(hashBytes, 0, min 8 hashBytes.Length)
        if hashBytes.Length < 8 then
            writer.Write(Array.zeroCreate<byte> (8 - hashBytes.Length))

    /// 값 파일에서 헤더 읽기 및 검증
    let readAndValidateValuesHeader (reader: BinaryReader) (expectedSchemaHash: string) : Result<unit, string> =
        try
            let magic = reader.ReadInt32()
            if magic <> ValuesMagicNumber then
                Error "Invalid values file format (wrong magic number)"
            else
                let version = reader.ReadInt32()
                if version <> ValuesFormatVersion then
                    Error (sprintf "Unsupported values file version: %d (expected %d)" version ValuesFormatVersion)
                else
                    // 스키마 해시 빠른 검증
                    let storedHashPreview = reader.ReadBytes(8)
                    let expectedHashBytes = Convert.FromBase64String(expectedSchemaHash)
                    let expectedHashPreview = Array.sub expectedHashBytes 0 (min 8 expectedHashBytes.Length)

                    if storedHashPreview <> expectedHashPreview then
                        Error "Schema mismatch - values file does not match current schema"
                    else
                        Ok ()
        with
        | ex -> Error (sprintf "Failed to read values header: %s" ex.Message)

    /// 값 파일에 푸터 쓰기 (체크섬)
    let writeValuesFooter (writer: BinaryWriter) (checksum: string) =
        let checksumBytes = Convert.FromBase64String(checksum)
        writer.Write(checksumBytes.Length)
        writer.Write(checksumBytes)
        writer.Write(FooterMagicNumber)

    /// 타입에 따라 값을 writer에 쓰기 (헬퍼 함수)
    let writeValueByType (writer: BinaryWriter) (dataType: Type) (value: obj) =
        if dataType = typeof<bool> then writer.Write(unbox<bool> value)
        elif dataType = typeof<sbyte> then writer.Write(unbox<sbyte> value)
        elif dataType = typeof<byte> then writer.Write(unbox<byte> value)
        elif dataType = typeof<int16> then writer.Write(unbox<int16> value)
        elif dataType = typeof<uint16> then writer.Write(unbox<uint16> value)
        elif dataType = typeof<int> then writer.Write(unbox<int> value)
        elif dataType = typeof<uint32> then writer.Write(unbox<uint32> value)
        elif dataType = typeof<int64> then writer.Write(unbox<int64> value)
        elif dataType = typeof<uint64> then writer.Write(unbox<uint64> value)
        elif dataType = typeof<double> then writer.Write(unbox<double> value)
        elif dataType = typeof<single> then writer.Write(unbox<single> value)
        elif dataType = typeof<string> then TypeSizes.writeStringToSlot writer (unbox<string> value)
        else invalidOp (sprintf "Unsupported type: %s" dataType.FullName)

    /// 값 파일에서 푸터 읽기 및 검증
    let readAndValidateValuesFooter (reader: BinaryReader) (expectedChecksum: string) : Result<unit, string> =
        try
            let checksumLength = reader.ReadInt32()
            let storedChecksumBytes = reader.ReadBytes(checksumLength)
            let storedChecksum = Convert.ToBase64String(storedChecksumBytes)

            let footerMagic = reader.ReadInt32()
            if footerMagic <> FooterMagicNumber then
                Error "Invalid values file footer"
            elif storedChecksum <> expectedChecksum then
                Error "Checksum mismatch - values file may be corrupted"
            else
                Ok ()
        with
        | ex -> Error (sprintf "Failed to read values footer: %s" ex.Message)

    /// 스키마에 따라 값들을 바이너리로 저장 (초고속)
    let saveValues (schema: RetainSchema) (snapshot: RetainSnapshot) : Result<unit, string> =
        try
            let directory = Path.GetDirectoryName(valuesPath)
            if not (String.IsNullOrEmpty(directory)) && not (Directory.Exists(directory)) then
                Directory.CreateDirectory(directory) |> ignore

            use ms = new MemoryStream()
            use writer = new BinaryWriter(ms, Encoding.UTF8, true)

            // 헤더 쓰기
            writeValuesHeader writer schema.SchemaHash

            // 성능 최적화: Dictionary로 O(1) 조회
            let snapshotDict =
                snapshot.Variables
                |> List.map (fun v -> v.Name, v)
                |> dict

            // 값들을 오프셋 순서대로 쓰기
            // 변수 값 쓰기
            for varSchema in schema.Variables do
                let hasVar, var = snapshotDict.TryGetValue(varSchema.Name)
                if hasVar then
                    // 최적화: 이미 직렬화된 ValueBytes를 deserialize 없이 직접 쓰기
                    // 타입과 크기가 스키마에 정의되어 있으므로 안전
                    let dataType = TypeHelpers.parseTypeName varSchema.DataType

                    // 타입별로 고정 크기 직접 쓰기 (빠른 경로)
                    if var.DataType = varSchema.DataType && var.ValueBytes.Length > 0 then
                        // 같은 타입이고 유효한 데이터면 직접 쓰기
                        if dataType = typeof<bool> && var.ValueBytes.Length >= 1 then
                            writer.Write(var.ValueBytes, 0, 1)
                        elif dataType = typeof<sbyte> && var.ValueBytes.Length >= 1 then
                            writer.Write(var.ValueBytes, 0, 1)
                        elif dataType = typeof<byte> && var.ValueBytes.Length >= 1 then
                            writer.Write(var.ValueBytes, 0, 1)
                        elif dataType = typeof<int16> && var.ValueBytes.Length >= 2 then
                            writer.Write(var.ValueBytes, 0, 2)
                        elif dataType = typeof<uint16> && var.ValueBytes.Length >= 2 then
                            writer.Write(var.ValueBytes, 0, 2)
                        elif dataType = typeof<int> && var.ValueBytes.Length >= 4 then
                            writer.Write(var.ValueBytes, 0, 4)
                        elif dataType = typeof<uint32> && var.ValueBytes.Length >= 4 then
                            writer.Write(var.ValueBytes, 0, 4)
                        elif dataType = typeof<int64> && var.ValueBytes.Length >= 8 then
                            writer.Write(var.ValueBytes, 0, 8)
                        elif dataType = typeof<uint64> && var.ValueBytes.Length >= 8 then
                            writer.Write(var.ValueBytes, 0, 8)
                        elif dataType = typeof<double> && var.ValueBytes.Length >= 8 then
                            writer.Write(var.ValueBytes, 0, 8)
                        elif dataType = typeof<single> && var.ValueBytes.Length >= 4 then
                            writer.Write(var.ValueBytes, 0, 4)
                        elif dataType = typeof<string> then
                            // 문자열은 deserialize 필요 (슬롯 크기 맞추기 위해)
                            let varDataType = TypeHelpers.parseTypeName var.DataType
                            let value = RetainValueSerializer.deserialize var.ValueBytes varDataType
                            TypeSizes.writeStringToSlot writer (unbox<string> value)
                        else
                            // 타입 불일치 또는 잘못된 크기 - deserialize 후 재직렬화
                            let varDataType = TypeHelpers.parseTypeName var.DataType
                            let value = RetainValueSerializer.deserialize var.ValueBytes varDataType
                            writeValueByType writer dataType value
                    else
                        // 타입 변환 필요한 경우
                        let varDataType = TypeHelpers.parseTypeName var.DataType
                        let value = RetainValueSerializer.deserialize var.ValueBytes varDataType
                        writeValueByType writer dataType value
                else
                    // 변수가 스냅샷에 없으면 기본값 쓰기
                    let dataType = TypeHelpers.parseTypeName varSchema.DataType
                    let defaultValue = TypeHelpers.getDefaultValue dataType
                    writeValueByType writer dataType defaultValue

            // FB Static 값 쓰기
            // 성능 최적화: FB도 Dictionary로 변환
            let fbDict =
                snapshot.FBStaticData
                |> List.map (fun fb -> fb.InstanceName, fb)
                |> dict

            for fbSchema in schema.FBStatic do
                let hasFB, fb = fbDict.TryGetValue(fbSchema.InstanceName)
                if hasFB then
                    // FB 내부 변수도 Dictionary로 변환
                    let fbVarDict =
                        fb.Variables
                        |> List.map (fun v -> v.Name, v)
                        |> dict

                    for varSchema in fbSchema.Variables do
                        let hasVar, var = fbVarDict.TryGetValue(varSchema.Name)
                        if hasVar then
                            let dataType = TypeHelpers.parseTypeName varSchema.DataType

                            // 최적화: 직접 바이트 쓰기
                            if var.DataType = varSchema.DataType && var.ValueBytes.Length > 0 then
                                // 같은 타입이면 직접 쓰기
                                if dataType = typeof<bool> && var.ValueBytes.Length >= 1 then
                                    writer.Write(var.ValueBytes, 0, 1)
                                elif dataType = typeof<sbyte> && var.ValueBytes.Length >= 1 then
                                    writer.Write(var.ValueBytes, 0, 1)
                                elif dataType = typeof<byte> && var.ValueBytes.Length >= 1 then
                                    writer.Write(var.ValueBytes, 0, 1)
                                elif dataType = typeof<int16> && var.ValueBytes.Length >= 2 then
                                    writer.Write(var.ValueBytes, 0, 2)
                                elif dataType = typeof<uint16> && var.ValueBytes.Length >= 2 then
                                    writer.Write(var.ValueBytes, 0, 2)
                                elif dataType = typeof<int> && var.ValueBytes.Length >= 4 then
                                    writer.Write(var.ValueBytes, 0, 4)
                                elif dataType = typeof<uint32> && var.ValueBytes.Length >= 4 then
                                    writer.Write(var.ValueBytes, 0, 4)
                                elif dataType = typeof<int64> && var.ValueBytes.Length >= 8 then
                                    writer.Write(var.ValueBytes, 0, 8)
                                elif dataType = typeof<uint64> && var.ValueBytes.Length >= 8 then
                                    writer.Write(var.ValueBytes, 0, 8)
                                elif dataType = typeof<double> && var.ValueBytes.Length >= 8 then
                                    writer.Write(var.ValueBytes, 0, 8)
                                elif dataType = typeof<single> && var.ValueBytes.Length >= 4 then
                                    writer.Write(var.ValueBytes, 0, 4)
                                elif dataType = typeof<string> then
                                    let varDataType = TypeHelpers.parseTypeName var.DataType
                                    let value = RetainValueSerializer.deserialize var.ValueBytes varDataType
                                    TypeSizes.writeStringToSlot writer (unbox<string> value)
                                else
                                    // 타입 불일치 또는 잘못된 크기
                                    let varDataType = TypeHelpers.parseTypeName var.DataType
                                    let value = RetainValueSerializer.deserialize var.ValueBytes varDataType
                                    writeValueByType writer dataType value
                            else
                                // 타입 변환 필요
                                let varDataType = TypeHelpers.parseTypeName var.DataType
                                let value = RetainValueSerializer.deserialize var.ValueBytes varDataType
                                writeValueByType writer dataType value
                        else
                            // 기본값 쓰기
                            let dataType = TypeHelpers.parseTypeName varSchema.DataType
                            let defaultValue = TypeHelpers.getDefaultValue dataType
                            writeValueByType writer dataType defaultValue
                else
                    // FB 인스턴스 전체가 없으면 모든 변수를 기본값으로
                    for varSchema in fbSchema.Variables do
                        let dataType = TypeHelpers.parseTypeName varSchema.DataType
                        let defaultValue = TypeHelpers.getDefaultValue dataType
                        writeValueByType writer dataType defaultValue

            writer.Flush()

            // 체크섬 계산
            let dataBytes = ms.ToArray()
            let checksum = ChecksumHelper.computeSHA256 dataBytes

            // 푸터 쓰기
            writeValuesFooter writer checksum

            writer.Flush()
            let finalBytes = ms.ToArray()

            // 원자적 쓰기
            let tempPath = valuesPath + ".tmp"
            File.WriteAllBytes(tempPath, finalBytes)

            if File.Exists(valuesPath) then
                let backupPath = valuesPath + ".bak"
                if File.Exists(backupPath) then
                    File.Delete(backupPath)
                File.Move(valuesPath, backupPath)

            File.Move(tempPath, valuesPath)
            Ok ()
        with
        | ex -> Error (sprintf "Failed to save values: %s" ex.Message)

    /// 스키마에 따라 값들을 바이너리에서 로드 (초고속)
    let loadValues (schema: RetainSchema) : Result<RetainSnapshot, string> =
        try
            if not (File.Exists(valuesPath)) then
                Error "Values file does not exist"
            else
                let allBytes = File.ReadAllBytes(valuesPath)
                use ms = new MemoryStream(allBytes)
                use reader = new BinaryReader(ms, Encoding.UTF8)

                // 헤더 검증
                match readAndValidateValuesHeader reader schema.SchemaHash with
                | Error err -> Error err
                | Ok () ->
                    // 값들을 오프셋 순서대로 읽기
                    let mutable variables = []

                    // 변수 값 읽기
                    for varSchema in schema.Variables do
                        let dataType = TypeHelpers.parseTypeName varSchema.DataType

                        let value =
                            if dataType = typeof<bool> then box (reader.ReadBoolean())
                            elif dataType = typeof<sbyte> then box (reader.ReadSByte())
                            elif dataType = typeof<byte> then box (reader.ReadByte())
                            elif dataType = typeof<int16> then box (reader.ReadInt16())
                            elif dataType = typeof<uint16> then box (reader.ReadUInt16())
                            elif dataType = typeof<int> then box (reader.ReadInt32())
                            elif dataType = typeof<uint32> then box (reader.ReadUInt32())
                            elif dataType = typeof<int64> then box (reader.ReadInt64())
                            elif dataType = typeof<uint64> then box (reader.ReadUInt64())
                            elif dataType = typeof<double> then box (reader.ReadDouble())
                            elif dataType = typeof<single> then box (reader.ReadSingle())
                            elif dataType = typeof<string> then box (TypeSizes.readStringFromSlot reader)
                            else invalidOp (sprintf "Unsupported type: %s" dataType.FullName)

                        let valueBytes = RetainValueSerializer.serialize value dataType

                        variables <- {
                            RetainVariable.Name = varSchema.Name
                            Area = varSchema.Area
                            DataType = varSchema.DataType
                            ValueBytes = valueBytes
                        } :: variables

                    // FB Static 값 읽기
                    let mutable fbStaticData = []

                    for fbSchema in schema.FBStatic do
                        let mutable fbVariables = []

                        for varSchema in fbSchema.Variables do
                            let dataType = TypeHelpers.parseTypeName varSchema.DataType

                            let value =
                                if dataType = typeof<bool> then box (reader.ReadBoolean())
                                elif dataType = typeof<sbyte> then box (reader.ReadSByte())
                                elif dataType = typeof<byte> then box (reader.ReadByte())
                                elif dataType = typeof<int16> then box (reader.ReadInt16())
                                elif dataType = typeof<uint16> then box (reader.ReadUInt16())
                                elif dataType = typeof<int> then box (reader.ReadInt32())
                                elif dataType = typeof<uint32> then box (reader.ReadUInt32())
                                elif dataType = typeof<int64> then box (reader.ReadInt64())
                                elif dataType = typeof<uint64> then box (reader.ReadUInt64())
                                elif dataType = typeof<double> then box (reader.ReadDouble())
                                elif dataType = typeof<single> then box (reader.ReadSingle())
                                elif dataType = typeof<string> then box (TypeSizes.readStringFromSlot reader)
                                else invalidOp (sprintf "Unsupported type: %s" dataType.FullName)

                            let valueBytes = RetainValueSerializer.serialize value dataType

                            fbVariables <- {
                                FBStaticVariable.Name = varSchema.Name
                                DataType = varSchema.DataType
                                ValueBytes = valueBytes
                            } :: fbVariables

                        fbStaticData <- {
                            FBStaticData.InstanceName = fbSchema.InstanceName
                            Variables = List.rev fbVariables
                        } :: fbStaticData

                    // 체크섬 검증 위치 저장
                    let dataLength = int ms.Position
                    let dataBytes = Array.sub allBytes 0 dataLength
                    let calculatedChecksum = ChecksumHelper.computeSHA256 dataBytes

                    // 푸터 검증
                    match readAndValidateValuesFooter reader calculatedChecksum with
                    | Error err -> Error err
                    | Ok () ->
                        let snapshot = {
                            RetainSnapshot.Timestamp = schema.Timestamp
                            Version = schema.Version
                            Variables = List.rev variables
                            FBStaticData = List.rev fbStaticData
                            Checksum = calculatedChecksum
                        }
                        Ok snapshot
        with
        | ex -> Error (sprintf "Failed to load values: %s" ex.Message)

    // Public members
    member _.SaveSchema(schema: RetainSchema) = saveSchemaToJson schema
    member _.LoadSchema() = loadSchemaFromJson ()
    member _.SaveValues(schema, snapshot) = saveValues schema snapshot
    member _.LoadValues(schema) = loadValues schema
    member _.SchemaPath = schemaPath
    member _.ValuesPath = valuesPath
