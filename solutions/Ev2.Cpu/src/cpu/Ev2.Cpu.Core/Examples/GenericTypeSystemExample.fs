namespace Ev2.Cpu.Core.Examples

open System
open Ev2.Cpu.Core

/// <summary>제네릭 타입 시스템 사용 예제</summary>
module GenericTypeSystemExample =

    // ═════════════════════════════════════════════════════════════════════
    // 1. Variable 사용 예제
    // ═════════════════════════════════════════════════════════════════════

    /// Variable 기본 사용법
    let variableBasics() =
        printfn "=== Variable Basics ==="

        // Variable 생성
        let counter = Variable<int>("Counter", 0)
        let enable = Variable<bool>("Enable", true)
        let message = Variable<string>("Message", "Hello World")

        printfn "Counter: %d" counter.Value
        printfn "Enable: %b" enable.Value
        printfn "Message: %s" message.Value

        // 값 변경
        counter.Value <- counter.Value + 1
        enable.Value <- not enable.Value

        printfn "Counter after increment: %d" counter.Value
        printfn "Enable after toggle: %b" enable.Value

    /// VariableBuilders 사용법
    let variableBuilders() =
        printfn "\n=== Variable Builders ==="

        // VariableBuilders 모듈 사용
        let counter = VariableBuilders.int "Counter"
        let temperature = VariableBuilders.doubleWith "Temperature" 25.5
        let status = VariableBuilders.boolWith "Status" true

        printfn "Counter (default): %d" counter.Value
        printfn "Temperature: %.1f" temperature.Value
        printfn "Status: %b" status.Value

    // ═════════════════════════════════════════════════════════════════════
    // 2. Literal 사용 예제
    // ═════════════════════════════════════════════════════════════════════

    let literalBasics() =
        printfn "\n=== Literal Basics ==="

        let maxValue = Literal<int>(100)
        let pi = Literal<double>(3.14159)
        let enabled = Literal<bool>(true)

        printfn "Max Value: %d" maxValue.Value
        printfn "PI: %.5f" pi.Value
        printfn "Enabled: %b" enabled.Value

        // DataType 확인
        printfn "Max Value Type: %s" maxValue.DataType.Name
        printfn "PI Type: %s" pi.DataType.Name

    // ═════════════════════════════════════════════════════════════════════
    // 3. Tag 사용 예제
    // ═════════════════════════════════════════════════════════════════════

    let tagBasics() =
        printfn "\n=== Tag Basics ==="

        // Tag 생성
        let counterTag = Tag<int>.Int("Counter")
        let enableTag = Tag<bool>.Bool("Enable")
        let tempTag = Tag<double>.Double("Temperature")

        printfn "Counter Tag: %O" counterTag
        printfn "Enable Tag: %O" enableTag
        printfn "Temperature Tag: %O" tempTag

        // TagBuilders 모듈 사용
        let speedTag = TagBuilders.double "Speed"
        let nameTag = TagBuilders.string "Name"

        printfn "Speed Tag Type: %s" speedTag.DataType.Name
        printfn "Name Tag Type: %s" nameTag.DataType.Name

    // ═════════════════════════════════════════════════════════════════════
    // 4. Storage 사용 예제
    // ═════════════════════════════════════════════════════════════════════

    let storageBasics() =
        printfn "\n=== Storage Basics ==="

        // Storage 생성
        let storage = Storage()

        // 변수 추가
        let counter = Variable<int>("Counter", 0)
        let enable = Variable<bool>("Enable", true)
        let temp = Variable<double>("Temperature", 25.0)

        storage.Add(counter.Name, counter)
        storage.Add(enable.Name, enable)
        storage.Add(temp.Name, temp)

        printfn "Storage Count: %d" storage.Count

        // 타입 안전 조회
        match storage.GetTyped<int>("Counter") with
        | Some var -> printfn "Counter found: %d" var.TValue
        | None -> printfn "Counter not found"

        // 모든 int 변수 조회
        let intVars = storage.GetAllTyped<int>()
        printfn "\nAll int variables:"
        for var in intVars do
            printfn "  %s = %d" var.Name var.TValue

    // ═════════════════════════════════════════════════════════════════════
    // 5. DsTag 상호 변환 예제
    // ═════════════════════════════════════════════════════════════════════

    let tagConversion() =
        printfn "\n=== Tag Conversion ==="

        // DsTag → Tag<'T>
        let dsTag = DsTag.Create("Counter", typeof<int>)
        printfn "DsTag: %O" dsTag

        match TagConversion.tryFromDsTag<int> dsTag with
        | Some tag -> printfn "Successfully converted to Tag<int>: %O" tag
        | None -> printfn "Conversion failed"

        // Tag<'T> → DsTag
        let genericTag = Tag<int>.Int("Speed")
        let convertedDsTag = TagConversion.toDsTag genericTag
        printfn "Tag<int> → DsTag: %O" convertedDsTag

    // ═════════════════════════════════════════════════════════════════════
    // 6. 타입 안전성 데모
    // ═════════════════════════════════════════════════════════════════════

    let typeSafety() =
        printfn "\n=== Type Safety Demo ==="

        // 타입 안전 변수
        let counter = Variable<int>("Counter", 0)

        // 타입 체크 - 컴파일 타임에 검증됨
        counter.Value <- 42        // OK
        counter.Value <- counter.Value + 1  // OK

        printfn "Counter: %d" counter.Value

        // 잘못된 타입 할당은 컴파일 에러
        // counter.Value <- "text"  // Compile Error!
        // counter.Value <- true    // Compile Error!

        // 런타임 타입 체크
        let var: IVariable = counter
        match var with
        | :? IVariable<int> as intVar ->
            printfn "Variable is int type with value: %d" intVar.TValue
        | _ ->
            printfn "Variable is not int type"

    // ═════════════════════════════════════════════════════════════════════
    // 메인 실행
    // ═════════════════════════════════════════════════════════════════════

    /// 모든 예제 실행
    let runAll() =
        printfn "╔════════════════════════════════════════════════╗"
        printfn "║  Ev2.Cpu Generic Type System Examples         ║"
        printfn "╚════════════════════════════════════════════════╝\n"

        variableBasics()
        variableBuilders()
        literalBasics()
        tagBasics()
        storageBasics()
        tagConversion()
        typeSafety()

        printfn "\n╔════════════════════════════════════════════════╗"
        printfn "║  All examples completed successfully!         ║"
        printfn "╚════════════════════════════════════════════════╝"
