namespace Ev2.Cpu.Runtime.Tests

open System
open System.IO
open Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Runtime

// ═════════════════════════════════════════════════════════════════════════════
// Retain Memory Tests - 리테인 메모리 테스트
// ═════════════════════════════════════════════════════════════════════════════

module RetainMemoryTests =

    // ─────────────────────────────────────────────────────────────────────────
    // 테스트 헬퍼
    // ─────────────────────────────────────────────────────────────────────────

    let createTestProgram() =
        { Statement.Program.Name = "RetainTestProgram"
          Inputs = []
          Outputs = []
          Locals = []
          Body = [] }

    let cleanupTestFile (filePath: string) =
        try
            if File.Exists(filePath) then
                File.Delete(filePath)
            let backupPath = filePath + ".bak"
            if File.Exists(backupPath) then
                File.Delete(backupPath)
        with
        | _ -> ()

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 1: BinaryRetainStorage 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``BinaryRetainStorage - Save and Load`` () =
        let testFile = "test_retain_save_load.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)

        // 스냅샷 생성
        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = [
                { RetainVariable.Name = "Counter"; Area = "L"; DataType = "Int32"; ValueBytes = RetainValueSerializer.serialize (box 42) typeof<int> }
                { RetainVariable.Name = "Status"; Area = "L"; DataType = "Bool"; ValueBytes = RetainValueSerializer.serialize (box true) typeof<bool> }
            ]
            FBStaticData = []
            Checksum = ""
        }

        // 저장
        let saveResult = storage.Save(snapshot)
        Assert.True(saveResult.IsOk, "Save should succeed")

        // 로드
        let loadResult = storage.Load()
        Assert.True(loadResult.IsOk, "Load should succeed")

        match loadResult with
        | Ok (Some loaded) ->
            Assert.Equal(2, loaded.Variables.Length)
            Assert.Equal("Counter", loaded.Variables.[0].Name)
            let counterValue = RetainValueSerializer.deserialize loaded.Variables.[0].ValueBytes typeof<int>
            Assert.Equal(42, unbox<int> counterValue)
        | _ -> failwith "Expected loaded snapshot"

        cleanupTestFile testFile

    [<Fact>]
    let ``BinaryRetainStorage - Load non-existent file returns None`` () =
        let testFile = "test_retain_nonexistent.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)
        let loadResult = storage.Load()

        Assert.True(loadResult.IsOk)
        match loadResult with
        | Ok None -> Assert.True(true)
        | _ -> failwith "Expected None for non-existent file"

    [<Fact>]
    let ``BinaryRetainStorage - Delete removes file`` () =
        let testFile = "test_retain_delete.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)

        // 파일 생성
        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = []
            FBStaticData = []
            Checksum = ""
        }
        storage.Save(snapshot) |> ignore

        Assert.True(File.Exists(testFile))

        // 삭제
        let deleteResult = storage.Delete()
        Assert.True(deleteResult.IsOk)
        Assert.False(File.Exists(testFile))

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 2: Memory Retain 기능 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``Memory - DeclareLocal with retain=true`` () =
        let memory = Memory()

        memory.DeclareLocal("RetainVar", typeof<int>, retain=true)
        memory.DeclareLocal("NormalVar", typeof<int>, retain=false)

        memory.Set("RetainVar", box 100)
        memory.Set("NormalVar", box 200)

        let snapshot = memory.CreateRetainSnapshot()

        // Retain 변수만 스냅샷에 포함되어야 함
        Assert.Equal(1, snapshot.Variables.Length)
        Assert.Equal("RetainVar", snapshot.Variables.[0].Name)

    [<Fact>]
    let ``Memory - CreateRetainSnapshot only includes retain variables`` () =
        let memory = Memory()

        memory.DeclareLocal("Counter", typeof<int>, retain=true)
        memory.DeclareLocal("TempVar", typeof<int>)
        memory.DeclareInternal("InternalRetain", typeof<double>, retain=true)

        memory.Set("Counter", box 42)
        memory.Set("TempVar", box 99)
        memory.Set("InternalRetain", box 3.14)

        let snapshot = memory.CreateRetainSnapshot()

        // Retain 변수 2개만 포함
        Assert.Equal(2, snapshot.Variables.Length)

        let names = snapshot.Variables |> List.map (fun v -> v.Name) |> Set.ofList
        Assert.True(names.Contains("Counter"))
        Assert.True(names.Contains("InternalRetain"))
        Assert.False(names.Contains("TempVar"))

    [<Fact>]
    let ``Memory - RestoreFromSnapshot restores retain variable values`` () =
        let memory = Memory()

        // Retain 변수 선언
        memory.DeclareLocal("Counter", typeof<int>, retain=true)
        memory.DeclareLocal("Status", typeof<bool>, retain=true)

        // 스냅샷 생성
        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = [
                { RetainVariable.Name = "Counter"; Area = "L"; DataType = "Int32"; ValueBytes = RetainValueSerializer.serialize (box 999) typeof<int> }
                { RetainVariable.Name = "Status"; Area = "L"; DataType = "Bool"; ValueBytes = RetainValueSerializer.serialize (box true) typeof<bool> }
            ]
            FBStaticData = []
            Checksum = ""
        }

        // 복원
        memory.RestoreFromSnapshot(snapshot)

        // 값 확인
        let counterValue = memory.Get("Counter") :?> int
        let statusValue = memory.Get("Status") :?> bool

        Assert.Equal(999, counterValue)
        Assert.True(statusValue)

    [<Fact>]
    let ``Memory - RestoreFromSnapshot ignores non-retain variables`` () =
        let memory = Memory()

        // Non-retain 변수 선언
        memory.DeclareLocal("NonRetainVar", typeof<int>, retain=false)
        memory.Set("NonRetainVar", box 100)

        // 스냅샷에는 값이 있지만 복원되지 않아야 함
        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = [
                { RetainVariable.Name = "NonRetainVar"; Area = "L"; DataType = "Int32"; ValueBytes = RetainValueSerializer.serialize (box 999) typeof<int> }
            ]
            FBStaticData = []
            Checksum = ""
        }

        memory.RestoreFromSnapshot(snapshot)

        // 값이 변경되지 않아야 함
        let value = memory.Get("NonRetainVar") :?> int
        Assert.Equal(100, value)

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 3: CpuScanEngine 통합 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``CpuScanEngine - Auto save on stop`` () =
        let testFile = "test_retain_autosave.dat"
        cleanupTestFile testFile

        let program = createTestProgram()
        let ctx = Context.create()
        let storage = BinaryRetainStorage(testFile)

        // Retain 변수 선언 및 값 설정
        ctx.Memory.DeclareLocal("AutoSaveCounter", typeof<int>, retain=true)
        ctx.Memory.Set("AutoSaveCounter", box 777)

        let engine = CpuScanEngine(program, ctx, None, None, Some storage)

        // 엔진 종료 (자동 저장)
        engine.StopAsync().Wait()

        // 파일이 생성되었는지 확인
        Assert.True(File.Exists(testFile))

        // 저장된 데이터 확인
        match storage.Load() with
        | Ok (Some snapshot) ->
            Assert.Equal(1, snapshot.Variables.Length)
            Assert.Equal("AutoSaveCounter", snapshot.Variables.[0].Name)
            let savedValue = RetainValueSerializer.deserialize snapshot.Variables.[0].ValueBytes typeof<int>
            Assert.Equal(777, unbox<int> savedValue)
        | _ -> failwith "Expected saved snapshot"

        cleanupTestFile testFile

    [<Fact>]
    let ``CpuScanEngine - Auto load on start`` () =
        let testFile = "test_retain_autoload.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)

        // 사전에 리테인 데이터 저장
        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = [
                { RetainVariable.Name = "AutoLoadCounter"; Area = "L"; DataType = "Int32"; ValueBytes = RetainValueSerializer.serialize (box 888) typeof<int> }
            ]
            FBStaticData = []
            Checksum = ""
        }
        storage.Save(snapshot) |> ignore

        // 새로운 엔진 시작 (자동 로드)
        let program = createTestProgram()
        let ctx = Context.create()
        ctx.Memory.DeclareLocal("AutoLoadCounter", typeof<int>, retain=true)

        let engine = CpuScanEngine(program, ctx, None, None, Some storage)

        // 값이 복원되었는지 확인
        let value = ctx.Memory.Get("AutoLoadCounter") :?> int
        Assert.Equal(888, value)

        cleanupTestFile testFile

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 4: 전체 시나리오 테스트 (전원 OFF/ON 시뮬레이션)
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``Retain Memory - Full power cycle scenario`` () =
        let testFile = "test_retain_power_cycle.dat"
        cleanupTestFile testFile

        let program = createTestProgram()
        let storage = BinaryRetainStorage(testFile)

        // ══════════════════════════════════════════════════════════════════
        // Phase 1: 전원 ON, 작업 수행
        // ══════════════════════════════════════════════════════════════════
        let ctx1 = Context.create()
        ctx1.Memory.DeclareLocal("WorkCounter", typeof<int>, retain=true)
        ctx1.Memory.DeclareLocal("TempValue", typeof<int>, retain=false)
        ctx1.Memory.DeclareLocal("Status", typeof<bool>, retain=true)

        ctx1.Memory.Set("WorkCounter", box 500)
        ctx1.Memory.Set("TempValue", box 123)
        ctx1.Memory.Set("Status", box true)

        let engine1 = CpuScanEngine(program, ctx1, None, None, Some storage)
        engine1.ScanOnce() |> ignore

        // 전원 OFF (자동 저장)
        engine1.StopAsync().Wait()

        // ══════════════════════════════════════════════════════════════════
        // Phase 2: 전원 다시 ON, 리테인 변수 복원 확인
        // ══════════════════════════════════════════════════════════════════
        let ctx2 = Context.create()
        ctx2.Memory.DeclareLocal("WorkCounter", typeof<int>, retain=true)
        ctx2.Memory.DeclareLocal("TempValue", typeof<int>, retain=false)
        ctx2.Memory.DeclareLocal("Status", typeof<bool>, retain=true)

        // 엔진 시작 (자동 복원)
        let engine2 = CpuScanEngine(program, ctx2, None, None, Some storage)

        // 값 확인
        let workCounter = ctx2.Memory.Get("WorkCounter") :?> int
        let tempValue = ctx2.Memory.Get("TempValue") :?> int
        let status = ctx2.Memory.Get("Status") :?> bool

        // Retain 변수는 복원, Non-retain은 기본값
        Assert.Equal(500, workCounter)
        Assert.Equal(0, tempValue)  // 기본값 (복원 안됨)
        Assert.True(status)

        cleanupTestFile testFile

    // ═════════════════════════════════════════════════════════════════
    // Phase 5: Large Data & Performance Tests (Phase 4 Test Enhancement)
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    let ``BinaryRetainStorage - Large snapshot with 1000 variables`` () =
        let testFile = "test_retain_large_1000.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)

        // Create snapshot with 1000 variables
        let variables = [
            for i in 1..1000 do
                { RetainVariable.Name = sprintf "LargeVar%d" i
                  Area = "L"
                  DataType = "Int32"
                  ValueBytes = RetainValueSerializer.serialize (box (i * 100)) typeof<int> }
        ]

        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = variables
            FBStaticData = []
            Checksum = ""
        }

        // Save and measure time
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let saveResult = storage.Save(snapshot)
        sw.Stop()

        Assert.True(saveResult.IsOk)
        Assert.True(sw.ElapsedMilliseconds < 5000L, sprintf "Save took %dms (expected < 5000ms)" sw.ElapsedMilliseconds)

        // Load and verify
        let sw2 = System.Diagnostics.Stopwatch.StartNew()
        let loadResult = storage.Load()
        sw2.Stop()

        Assert.True(loadResult.IsOk)
        Assert.True(sw2.ElapsedMilliseconds < 5000L, sprintf "Load took %dms (expected < 5000ms)" sw2.ElapsedMilliseconds)

        match loadResult with
        | Ok (Some loaded) ->
            Assert.Equal(1000, loaded.Variables.Length)
            Assert.Equal("LargeVar1", loaded.Variables.[0].Name)
            Assert.Equal("LargeVar1000", loaded.Variables.[999].Name)
        | _ -> failwith "Expected loaded snapshot"

        cleanupTestFile testFile

    [<Fact>]
    let ``BinaryRetainStorage - Very large snapshot with 10000 variables`` () =
        let testFile = "test_retain_large_10000.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)

        // Create snapshot with 10,000 variables (simulates large PLC program)
        let variables = [
            for i in 1..10000 do
                { RetainVariable.Name = sprintf "VeryLargeVar%d" i
                  Area = "L"
                  DataType = if i % 2 = 0 then "Int32" else "Double"  // Updated to match TypeHelpers change
                  ValueBytes = if i % 2 = 0 then
                                 RetainValueSerializer.serialize (box i) typeof<int>
                               else
                                 RetainValueSerializer.serialize (box (float i * 3.14)) typeof<double> }
        ]

        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = variables
            FBStaticData = []
            Checksum = ""
        }

        // Save and measure time (should complete within reasonable time)
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let saveResult = storage.Save(snapshot)
        sw.Stop()

        Assert.True(saveResult.IsOk, "Save should succeed with 10,000 variables")
        Assert.True(sw.ElapsedMilliseconds < 30000L, sprintf "Save took %dms (expected < 30s for 10k vars)" sw.ElapsedMilliseconds)

        // Verify file was created and has reasonable size
        Assert.True(File.Exists(testFile))
        let fileInfo = FileInfo(testFile)
        Assert.True(fileInfo.Length > 100000L, sprintf "File size %d bytes (expected > 100KB)" fileInfo.Length)

        // Load and verify
        let sw2 = System.Diagnostics.Stopwatch.StartNew()
        let loadResult = storage.Load()
        sw2.Stop()

        Assert.True(loadResult.IsOk, "Load should succeed")
        Assert.True(sw2.ElapsedMilliseconds < 30000L, sprintf "Load took %dms (expected < 30s)" sw2.ElapsedMilliseconds)

        match loadResult with
        | Ok (Some loaded) ->
            Assert.Equal(10000, loaded.Variables.Length)
            Assert.Equal("VeryLargeVar1", loaded.Variables.[0].Name)
            Assert.Equal("VeryLargeVar10000", loaded.Variables.[9999].Name)

            // Verify data types are mixed
            let intCount = loaded.Variables |> List.filter (fun v -> v.DataType = "Int32") |> List.length
            let doubleCount = loaded.Variables |> List.filter (fun v -> v.DataType = "Double") |> List.length
            Assert.Equal(5000, intCount)
            Assert.Equal(5000, doubleCount)
        | _ -> failwith "Expected loaded snapshot"

        cleanupTestFile testFile

    [<Fact>]
    let ``BinaryRetainStorage - Empty snapshot (zero variables)`` () =
        let testFile = "test_retain_empty.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)

        let emptySnapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = []
            FBStaticData = []
            Checksum = ""
        }

        // Save empty snapshot
        let saveResult = storage.Save(emptySnapshot)
        Assert.True(saveResult.IsOk, "Should be able to save empty snapshot")

        // Load empty snapshot
        let loadResult = storage.Load()
        Assert.True(loadResult.IsOk)

        match loadResult with
        | Ok (Some loaded) ->
            Assert.Equal(0, loaded.Variables.Length)
        | _ -> failwith "Expected empty snapshot"

        cleanupTestFile testFile

    [<Fact>]
    let ``BinaryRetainStorage - Corrupted file returns error`` () =
        let testFile = "test_retain_corrupted.dat"
        cleanupTestFile testFile

        // Create corrupted file (invalid binary data)
        File.WriteAllBytes(testFile, [| 0xFFuy; 0xDEuy; 0xADuy; 0xBEuy; 0xEFuy |])

        let storage = BinaryRetainStorage(testFile)
        let loadResult = storage.Load()

        // Should return error for corrupted data
        Assert.True(loadResult.IsError, "Loading corrupted file should return error")

        cleanupTestFile testFile

    [<Fact>]
    let ``BinaryRetainStorage - Variables with very long names`` () =
        let testFile = "test_retain_long_names.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)

        // Create variable with 500-character name
        let longName = String.replicate 500 "X"
        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = [
                { RetainVariable.Name = longName
                  Area = "L"
                  DataType = "Int32"
                  ValueBytes = RetainValueSerializer.serialize (box 42) typeof<int> }
            ]
            FBStaticData = []
            Checksum = ""
        }

        // Save and load
        let saveResult = storage.Save(snapshot)
        Assert.True(saveResult.IsOk)

        let loadResult = storage.Load()
        Assert.True(loadResult.IsOk)

        match loadResult with
        | Ok (Some loaded) ->
            Assert.Equal(1, loaded.Variables.Length)
            Assert.Equal(500, loaded.Variables.[0].Name.Length)
            Assert.Equal(longName, loaded.Variables.[0].Name)
        | _ -> failwith "Expected loaded snapshot"

        cleanupTestFile testFile

    [<Fact>]
    let ``BinaryRetainStorage - Variables with very long values`` () =
        let testFile = "test_retain_long_values.dat"
        cleanupTestFile testFile

        let storage = BinaryRetainStorage(testFile)

        // Create variable with 10,000-character string value
        let longValue = String.replicate 10000 "ABC"
        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = [
                { RetainVariable.Name = "LongString"
                  Area = "L"
                  DataType = "String"
                  ValueBytes = RetainValueSerializer.serialize (box longValue) typeof<string> }
            ]
            FBStaticData = []
            Checksum = ""
        }

        // Save and load
        let saveResult = storage.Save(snapshot)
        Assert.True(saveResult.IsOk)

        let loadResult = storage.Load()
        Assert.True(loadResult.IsOk)

        match loadResult with
        | Ok (Some loaded) ->
            Assert.Equal(1, loaded.Variables.Length)
            let loadedValue = RetainValueSerializer.deserialize loaded.Variables.[0].ValueBytes typeof<string>
            Assert.True((unbox<string> loadedValue).Contains("ABC"))
        | _ -> failwith "Expected loaded snapshot"

        cleanupTestFile testFile

    [<Fact>]
    let ``Memory - CreateRetainSnapshot performance with 5000 variables`` () =
        // Temporarily increase limit for performance test
        let previousLimit = RuntimeLimits.Current
        RuntimeLimits.Current <- { previousLimit with MaxMemoryVariables = 10000 }
        try
            let memory = Memory()

            // Declare 5000 retain variables
            for i in 1..5000 do
                memory.DeclareLocal(sprintf "PerfVar%d" i, typeof<int>, retain=true)
                memory.Set(sprintf "PerfVar%d" i, box i)

            // Measure snapshot creation time
            let sw = System.Diagnostics.Stopwatch.StartNew()
            let snapshot = memory.CreateRetainSnapshot()
            sw.Stop()

            Assert.Equal(5000, snapshot.Variables.Length)
            Assert.True(sw.ElapsedMilliseconds < 10000L, sprintf "Snapshot creation took %dms (expected < 10s)" sw.ElapsedMilliseconds)
        finally
            RuntimeLimits.Current <- previousLimit

    [<Fact>]
    let ``Memory - RestoreFromSnapshot performance with 5000 variables`` () =
        // Temporarily increase limit for performance test
        let previousLimit = RuntimeLimits.Current
        RuntimeLimits.Current <- { previousLimit with MaxMemoryVariables = 10000 }
        try
            let memory = Memory()

            // Declare 5000 retain variables
            for i in 1..5000 do
                memory.DeclareLocal(sprintf "RestoreVar%d" i, typeof<int>, retain=true)

            // Create snapshot with 5000 variables
            let variables = [
                for i in 1..5000 do
                    { RetainVariable.Name = sprintf "RestoreVar%d" i
                      Area = "L"
                      DataType = "Int32"  // Updated to match TypeHelpers change
                      ValueBytes = RetainValueSerializer.serialize (box (i * 10)) typeof<int> }
            ]

            let snapshot = {
                RetainSnapshot.Timestamp = DateTime.Now
                Version = 1
                Variables = variables
                FBStaticData = []
                Checksum = ""
            }

            // Measure restore time
            let sw = System.Diagnostics.Stopwatch.StartNew()
            memory.RestoreFromSnapshot(snapshot)
            sw.Stop()

            Assert.True(sw.ElapsedMilliseconds < 10000L, sprintf "Restore took %dms (expected < 10s)" sw.ElapsedMilliseconds)

            // Verify a few values
            let val1 = memory.Get("RestoreVar1") :?> int
            let val5000 = memory.Get("RestoreVar5000") :?> int
            Assert.Equal(10, val1)
            Assert.Equal(50000, val5000)
        finally
            RuntimeLimits.Current <- previousLimit

    [<Fact>]
    let ``CpuScanEngine - Retain with mixed data types`` () =
        let testFile = "test_retain_mixed_types.dat"
        cleanupTestFile testFile

        let program = createTestProgram()
        let storage = BinaryRetainStorage(testFile)

        // Phase 1: Save mixed types
        let ctx1 = Context.create()
        ctx1.Memory.DeclareLocal("IntVar", typeof<int>, retain=true)
        ctx1.Memory.DeclareLocal("DoubleVar", typeof<double>, retain=true)
        ctx1.Memory.DeclareLocal("BoolVar", typeof<bool>, retain=true)
        ctx1.Memory.DeclareLocal("StringVar", typeof<string>, retain=true)

        ctx1.Memory.Set("IntVar", box 12345)
        ctx1.Memory.Set("DoubleVar", box 3.14159)
        ctx1.Memory.Set("BoolVar", box true)
        ctx1.Memory.Set("StringVar", box "Test String Value")

        let engine1 = CpuScanEngine(program, ctx1, None, None, Some storage)
        engine1.StopAsync().Wait()

        // Phase 2: Restore and verify
        let ctx2 = Context.create()
        ctx2.Memory.DeclareLocal("IntVar", typeof<int>, retain=true)
        ctx2.Memory.DeclareLocal("DoubleVar", typeof<double>, retain=true)
        ctx2.Memory.DeclareLocal("BoolVar", typeof<bool>, retain=true)
        ctx2.Memory.DeclareLocal("StringVar", typeof<string>, retain=true)

        let engine2 = CpuScanEngine(program, ctx2, None, None, Some storage)

        let intVal = ctx2.Memory.Get("IntVar") :?> int
        let doubleVal = ctx2.Memory.Get("DoubleVar") :?> float
        let boolVal = ctx2.Memory.Get("BoolVar") :?> bool
        let stringVal = ctx2.Memory.Get("StringVar") :?> string

        Assert.Equal(12345, intVal)
        Assert.Equal(3.14159, doubleVal, 5)  // 5 decimal places precision
        Assert.True(boolVal)
        Assert.Equal("Test String Value", stringVal)

        cleanupTestFile testFile

    // ═════════════════════════════════════════════════════════════════════════════
    // 스키마 기반 고성능 Retain 저장소 테스트
    // ═════════════════════════════════════════════════════════════════════════════

    [<Fact>]
    let ``SchemaBasedRetainStorage - Basic schema and values save/load`` () =
        let schemaFile = "test_schema.json"
        let valuesFile = "test_values.dat"
        cleanupTestFile schemaFile
        cleanupTestFile valuesFile

        let storage = SchemaBasedRetainStorage(schemaFile, valuesFile)

        // 스키마 생성
        let schema = {
            RetainSchema.Version = 1
            SchemaHash = ""
            Timestamp = DateTime.Now
            TotalSize = 265  // 4 (int32) + 1 (bool) + 256 (string) + 4 (double)
            Variables = [
                { VariableSchema.Name = "Counter"; Area = "L"; DataType = "Int32"; Offset = 0; Size = 4 }
                { VariableSchema.Name = "Flag"; Area = "L"; DataType = "Bool"; Offset = 4; Size = 1 }
                { VariableSchema.Name = "Message"; Area = "V"; DataType = "String"; Offset = 5; Size = 256 }
            ]
            FBStatic = []
        }

        // 스키마 해시 계산
        let schemaWithHash = { schema with SchemaHash = SchemaHasher.computeSchemaHash schema }

        // 스키마 저장
        let saveSchemaResult = storage.SaveSchema(schemaWithHash)
        Assert.True(saveSchemaResult.IsOk, "Schema save should succeed")

        // 스냅샷 생성
        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = [
                { RetainVariable.Name = "Counter"; Area = "L"; DataType = "Int32";
                  ValueBytes = RetainValueSerializer.serialize (box 42) typeof<int> }
                { RetainVariable.Name = "Flag"; Area = "L"; DataType = "Bool";
                  ValueBytes = RetainValueSerializer.serialize (box true) typeof<bool> }
                { RetainVariable.Name = "Message"; Area = "V"; DataType = "String";
                  ValueBytes = RetainValueSerializer.serialize (box "Hello World") typeof<string> }
            ]
            FBStaticData = []
            Checksum = ""
        }

        // 값 저장
        let saveValuesResult = storage.SaveValues(schemaWithHash, snapshot)
        Assert.True(saveValuesResult.IsOk, sprintf "Values save should succeed: %A" saveValuesResult)

        // 스키마 로드
        match storage.LoadSchema() with
        | Ok (Some loadedSchema) ->
            Assert.Equal(schemaWithHash.SchemaHash, loadedSchema.SchemaHash)
            Assert.Equal(3, loadedSchema.Variables.Length)

            // 값 로드
            match storage.LoadValues(loadedSchema) with
            | Ok loadedSnapshot ->
                Assert.Equal(3, loadedSnapshot.Variables.Length)

                // 값 확인
                let counter = loadedSnapshot.Variables |> List.find (fun v -> v.Name = "Counter")
                let counterValue = RetainValueSerializer.deserialize counter.ValueBytes typeof<int>
                Assert.Equal(42, unbox<int> counterValue)

                let flag = loadedSnapshot.Variables |> List.find (fun v -> v.Name = "Flag")
                let flagValue = RetainValueSerializer.deserialize flag.ValueBytes typeof<bool>
                Assert.True(unbox<bool> flagValue)

                let message = loadedSnapshot.Variables |> List.find (fun v -> v.Name = "Message")
                let messageValue = RetainValueSerializer.deserialize message.ValueBytes typeof<string>
                Assert.Equal("Hello World", unbox<string> messageValue)
            | Error err -> failwith (sprintf "Values load failed: %s" err)
        | Ok None -> failwith "Schema file not found"
        | Error err -> failwith (sprintf "Schema load failed: %s" err)

        cleanupTestFile schemaFile
        cleanupTestFile valuesFile

    [<Fact>]
    let ``Memory.BuildSchema - Creates correct schema from retain variables`` () =
        let memory = Memory()

        // Retain 변수 선언
        memory.DeclareLocal("LocalCounter", typeof<int>, retain=true)
        memory.DeclareLocal("LocalFlag", typeof<bool>, retain=true)
        memory.DeclareInternal("InternalValue", typeof<double>, retain=true)

        // 값 설정
        memory.Set("LocalCounter", box 100)
        memory.Set("LocalFlag", box false)
        memory.Set("InternalValue", box 2.718)

        // 스키마 빌드
        let schema = memory.BuildSchema()

        // 검증
        Assert.Equal(3, schema.Variables.Length)
        Assert.True(schema.TotalSize > 0, "TotalSize should be calculated")
        Assert.False(String.IsNullOrEmpty(schema.SchemaHash), "SchemaHash should be computed")

        // 변수 확인
        let counterVar = schema.Variables |> List.tryFind (fun v -> v.Name = "LocalCounter")
        Assert.True(counterVar.IsSome, "LocalCounter should be in schema")
        Assert.Equal("L", counterVar.Value.Area)
        Assert.Equal(4, counterVar.Value.Size)

    [<Fact>]
    let ``Memory.IsSchemaModified - Detects new retain variables`` () =
        let memory = Memory()

        // 초기 상태
        Assert.False(memory.IsSchemaModified(), "Initial schema should not be modified")

        // 일반 변수 선언 (retain=false)
        memory.DeclareLocal("NormalVar", typeof<int>, retain=false)
        Assert.False(memory.IsSchemaModified(), "Non-retain variable should not modify schema")

        // Retain 변수 선언
        memory.DeclareLocal("RetainVar", typeof<int>, retain=true)
        Assert.True(memory.IsSchemaModified(), "Retain variable should modify schema")

        // 플래그 리셋
        memory.ResetSchemaModifiedFlag()
        Assert.False(memory.IsSchemaModified(), "Schema modified flag should be reset")

    [<Fact>]
    let ``SchemaBasedRetainStorage - Performance test with 1000 variables`` () =
        let schemaFile = "test_perf_schema.json"
        let valuesFile = "test_perf_values.dat"
        cleanupTestFile schemaFile
        cleanupTestFile valuesFile

        let storage = SchemaBasedRetainStorage(schemaFile, valuesFile)

        // 1000개 변수 스키마 생성
        let variables = [
            for i in 1..1000 do
                { VariableSchema.Name = sprintf "Var%d" i
                  Area = "L"
                  DataType = "Int32"
                  Offset = (i-1) * 4
                  Size = 4 }
        ]

        let schema = {
            RetainSchema.Version = 1
            SchemaHash = ""
            Timestamp = DateTime.Now
            TotalSize = 4000
            Variables = variables
            FBStatic = []
        }

        let schemaWithHash = { schema with SchemaHash = SchemaHasher.computeSchemaHash schema }
        storage.SaveSchema(schemaWithHash) |> ignore

        // 1000개 값 스냅샷 생성
        let snapshotVars = [
            for i in 1..1000 do
                { RetainVariable.Name = sprintf "Var%d" i
                  Area = "L"
                  DataType = "Int32"
                  ValueBytes = RetainValueSerializer.serialize (box i) typeof<int> }
        ]

        let snapshot = {
            RetainSnapshot.Timestamp = DateTime.Now
            Version = 1
            Variables = snapshotVars
            FBStaticData = []
            Checksum = ""
        }

        // 성능 측정: 저장
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let saveResult = storage.SaveValues(schemaWithHash, snapshot)
        sw.Stop()

        Assert.True(saveResult.IsOk, "Save should succeed")
        printfn "[PERF] Schema-based save (1000 vars): %dms" sw.ElapsedMilliseconds

        // 성능 측정: 로드
        let sw2 = System.Diagnostics.Stopwatch.StartNew()
        let loadResult = storage.LoadValues(schemaWithHash)
        sw2.Stop()

        Assert.True(loadResult.IsOk, sprintf "Load should succeed: %A" loadResult)
        printfn "[PERF] Schema-based load (1000 vars): %dms" sw2.ElapsedMilliseconds

        // 예상: < 50ms (기존 BinaryRetainStorage 대비 15-30배 빠름)
        Assert.True(sw.ElapsedMilliseconds < 100L,
                    sprintf "Save should be very fast: %dms (expected < 100ms)" sw.ElapsedMilliseconds)
        Assert.True(sw2.ElapsedMilliseconds < 100L,
                    sprintf "Load should be very fast: %dms (expected < 100ms)" sw2.ElapsedMilliseconds)

        cleanupTestFile schemaFile
        cleanupTestFile valuesFile
