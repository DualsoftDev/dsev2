namespace Ev2.Cpu.Tests.Infrastructure

open System
open Ev2.Cpu.Core

// ═══════════════════════════════════════════════════════════════════════
// Data Generators Module - 테스트 데이터 생성기
// ═══════════════════════════════════════════════════════════════════════
// Phase 1: 기반 인프라
// 경계값, 랜덤 값, 특수 케이스 등 다양한 테스트 데이터 생성
// ═══════════════════════════════════════════════════════════════════════

module DataGenerators =

    // ───────────────────────────────────────────────────────────────────
    // Integer Boundary Values
    // ───────────────────────────────────────────────────────────────────

    /// <summary>All integer boundary values for testing</summary>
    let intBoundaryValues = [
        Int32.MinValue          // -2,147,483,648
        Int32.MinValue + 1      // -2,147,483,647
        -1000000
        -1000
        -100
        -10
        -1
        0
        1
        10
        100
        1000
        1000000
        Int32.MaxValue - 1      // 2,147,483,646
        Int32.MaxValue          // 2,147,483,647
    ]

    /// <summary>Integer values that may cause overflow</summary>
    let intOverflowValues = [
        (Int32.MaxValue, 1)           // Would overflow
        (Int32.MaxValue, Int32.MaxValue)
        (Int32.MinValue, -1)          // Would underflow
        (Int32.MinValue, Int32.MinValue)
    ]

    /// <summary>Generate random integer in range</summary>
    let randomInt min max =
        let rnd = Random()
        rnd.Next(min, max + 1)

    /// <summary>Generate sequence of random integers</summary>
    let randomInts count min max =
        Seq.init count (fun _ -> randomInt min max)

    // ───────────────────────────────────────────────────────────────────
    // Double/Float Boundary Values
    // ───────────────────────────────────────────────────────────────────

    /// <summary>All double boundary values for testing</summary>
    let doubleBoundaryValues = [
        Double.NegativeInfinity
        Double.MinValue
        -1.7976931348623157E+308  // Near min
        -1.0e100
        -1000000.0
        -1000.0
        -100.0
        -10.0
        -1.0
        -0.1
        -Double.Epsilon           // Smallest negative
        -0.0                      // Negative zero
        0.0
        Double.Epsilon            // Smallest positive
        0.1
        1.0
        10.0
        100.0
        1000.0
        1000000.0
        1.0e100
        1.7976931348623157E+308   // Near max
        Double.MaxValue
        Double.PositiveInfinity
        Double.NaN
    ]

    /// <summary>Special double values (NaN, Infinity)</summary>
    let doubleSpecialValues = [
        Double.NaN
        Double.PositiveInfinity
        Double.NegativeInfinity
        -0.0
        Double.Epsilon
        -Double.Epsilon
    ]

    /// <summary>Subnormal (denormalized) double values</summary>
    let doubleSubnormalValues = [
        Double.Epsilon * 0.5
        Double.Epsilon * 0.1
        -Double.Epsilon * 0.5
    ]

    /// <summary>Generate random double in range</summary>
    let randomDouble min max =
        let rnd = Random()
        min + (rnd.NextDouble() * (max - min))

    /// <summary>Generate sequence of random doubles</summary>
    let randomDoubles count min max =
        Seq.init count (fun _ -> randomDouble min max)

    // ───────────────────────────────────────────────────────────────────
    // Boolean Values
    // ───────────────────────────────────────────────────────────────────

    /// <summary>All boolean values</summary>
    let boolValues = [true; false]

    /// <summary>Generate random boolean</summary>
    let randomBool() =
        Random().Next(2) = 0

    /// <summary>Generate sequence of random booleans</summary>
    let randomBools count =
        Seq.init count (fun _ -> randomBool())

    // ───────────────────────────────────────────────────────────────────
    // String Boundary Values
    // ───────────────────────────────────────────────────────────────────

    /// <summary>String boundary and special values</summary>
    let stringBoundaryValues = [
        null                  // Null string
        ""                    // Empty string
        " "                   // Single space
        "   "                 // Multiple spaces
        "\t"                  // Tab
        "\n"                  // Newline
        "\r\n"                // CRLF
        "a"                   // Single char
        "abc"                 // Short string
        "Hello, World!"       // Normal string
        String('x', 100)      // Long string (100 chars)
        String('x', 1000)     // Very long (1000 chars)
        String('x', 10000)    // Extremely long (10k chars)
    ]

    /// <summary>Special characters that might cause issues</summary>
    let specialCharStrings = [
        "\0"                  // Null character
        "\\"                  // Backslash
        "\""                  // Quote
        "'"                   // Single quote
        "'"                   // Smart quote
        "'"
        """                   // Smart double quote
        """
        "—"                   // Em dash
        "🔥"                  // Emoji
        "中文"                // Chinese characters
        "한글"                // Korean characters
        "العربية"             // Arabic
        "😀😁😂"              // Multiple emojis
    ]

    /// <summary>Strings that might cause parsing issues</summary>
    let problematicStrings = [
        "true"                // Looks like boolean
        "false"
        "null"                // Looks like null
        "NaN"                 // Looks like number
        "Infinity"
        "-Infinity"
        "123"                 // Looks like int
        "123.456"             // Looks like double
        "1e10"                // Scientific notation
        "+123"                // Leading plus
        "-0"                  // Negative zero
        "0x1A"                // Hex notation
        "0b1010"              // Binary notation
    ]

    /// <summary>Generate random string of given length</summary>
    let randomString length =
        let chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
        let rnd = Random()
        String(Array.init length (fun _ -> chars.[rnd.Next(chars.Length)]))

    /// <summary>Generate very long string (MB scale)</summary>
    let veryLongString (sizeInMB: float) =
        let totalChars = int (sizeInMB * 1024.0 * 1024.0 / 2.0) // UTF-16: 2 bytes per char
        String('x', totalChars)

    // ───────────────────────────────────────────────────────────────────
    // Type Specific Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>All Type values</summary>
    let allTypes = [
        typeof<int>
        typeof<double>
        typeof<bool>
        typeof<string>
    ]

    /// <summary>Generate boundary values for given Type</summary>
    let boundaryValuesForType (dtype: Type) =
        if dtype = typeof<int> then intBoundaryValues |> List.map box
        elif dtype = typeof<double> then doubleBoundaryValues |> List.map box
        elif dtype = typeof<bool> then boolValues |> List.map box
        elif dtype = typeof<string> then stringBoundaryValues |> List.map box
        else []

    /// <summary>Generate default value for Type</summary>
    let defaultValueForType (dtype: Type) =
        TypeHelpers.getDefaultValue dtype

    /// <summary>Generate random value for Type</summary>
    let randomValueForType (dtype: Type) =
        if dtype = typeof<int> then box (randomInt Int32.MinValue Int32.MaxValue)
        elif dtype = typeof<double> then box (randomDouble -1.0e100 1.0e100)
        elif dtype = typeof<bool> then box (randomBool())
        elif dtype = typeof<string> then box (randomString 20)
        else box null

    // ───────────────────────────────────────────────────────────────────
    // Tag Name Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Valid variable names</summary>
    let validVariableNames = [
        "x"
        "y"
        "z"
        "counter"
        "Counter"
        "COUNTER"
        "myVariable"
        "my_variable"
        "MyVariable"
        "variable123"
        "_private"
        "___triple"
        "a1b2c3"
    ]

    /// <summary>Invalid variable names (should fail validation)</summary>
    let invalidVariableNames = [
        null
        ""
        " "
        "123abc"               // Starts with number
        "my-variable"          // Contains hyphen
        "my.variable"          // Contains dot
        "my variable"          // Contains space
        "my@variable"          // Contains special char
        String('x', 1000)      // Too long
    ]

    /// <summary>Variable names with special meaning</summary>
    let reservedOrSpecialNames = [
        "true"
        "false"
        "null"
        "IF"
        "THEN"
        "ELSE"
        "END_IF"
        "WHILE"
        "RETURN"
    ]

    // ───────────────────────────────────────────────────────────────────
    // Collection Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Generate list of given size with generator function</summary>
    let generateList size generator =
        List.init size (fun _ -> generator())

    /// <summary>Generate empty, small, medium, large lists</summary>
    let listsOfVariousSizes generator = [
        []                              // Empty
        generateList 1 generator        // Single
        generateList 10 generator       // Small
        generateList 100 generator      // Medium
        generateList 1000 generator     // Large
    ]

    /// <summary>Generate deeply nested structure</summary>
    let rec generateNestedList depth leafGenerator =
        if depth <= 0 then
            [leafGenerator()]
        else
            List.init 3 (fun _ ->
                generateNestedList (depth - 1) leafGenerator
                |> List.head)

    // ───────────────────────────────────────────────────────────────────
    // Combinatorial Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Generate all pairs from a list</summary>
    let allPairs list =
        List.allPairs list list

    /// <summary>Generate all combinations of N items from list</summary>
    let rec combinations n list =
        match n, list with
        | 0, _ -> [[]]
        | _, [] -> []
        | k, x::xs ->
            List.map (fun l -> x::l) (combinations (k-1) xs)
            @ combinations k xs

    /// <summary>Generate cartesian product of multiple lists</summary>
    let rec cartesianProduct lists =
        match lists with
        | [] -> [[]]
        | head::tail ->
            List.collect (fun h ->
                List.map (fun t -> h::t) (cartesianProduct tail)) head

    // ───────────────────────────────────────────────────────────────────
    // Time-based Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>DateTime boundary values</summary>
    let dateTimeBoundaryValues = [
        DateTime.MinValue
        DateTime.MinValue.AddDays(1.0)
        DateTime(2000, 1, 1)
        DateTime(2024, 1, 1)
        DateTime.UtcNow
        DateTime.MaxValue.AddDays(-1.0)
        DateTime.MaxValue
    ]

    /// <summary>TimeSpan boundary values</summary>
    let timeSpanBoundaryValues = [
        TimeSpan.Zero
        TimeSpan.FromTicks(1L)
        TimeSpan.FromMilliseconds(1.0)
        TimeSpan.FromSeconds(1.0)
        TimeSpan.FromMinutes(1.0)
        TimeSpan.FromHours(1.0)
        TimeSpan.FromDays(1.0)
        TimeSpan.MaxValue
    ]

    // ───────────────────────────────────────────────────────────────────
    // Seed-based Random Generators (Reproducible)
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Create seeded random generator</summary>
    let createSeededRandom seed =
        Random(seed)

    /// <summary>Generate reproducible random sequence</summary>
    let seededRandomInts seed count min max =
        let rnd = createSeededRandom seed
        Seq.init count (fun _ -> rnd.Next(min, max + 1))

    /// <summary>Generate reproducible random doubles</summary>
    let seededRandomDoubles seed count min max =
        let rnd = createSeededRandom seed
        Seq.init count (fun _ -> min + (rnd.NextDouble() * (max - min)))
