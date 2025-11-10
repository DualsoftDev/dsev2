namespace Ev2.Cpu.StandardLibrary.Tests

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.StandardLibrary.Math

module MathTests =

    [<Fact>]
    let ``AVERAGE - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match AVERAGE.create() with
        | Ok fc ->
            fc.Name |> should equal "AVERAGE"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MIN - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match MIN.create() with
        | Ok fc ->
            fc.Name |> should equal "MIN"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MAX - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match MAX.create() with
        | Ok fc ->
            fc.Name |> should equal "MAX"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    // ═════════════════════════════════════════════════════════════════════
    // Boundary Value Tests (Phase 6)
    // ═════════════════════════════════════════════════════════════════════

    [<Fact>]
    let ``AVERAGE - Has expected parameters`` () =
        DsTagRegistry.clear()
        match AVERAGE.create() with
        | Ok fc ->
            fc.Inputs.Length |> should be (greaterThanOrEqualTo 1)
            fc.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            // Should return numeric type (Int or Double)
            let isNumeric = fc.ReturnType = typeof<int> || fc.ReturnType = typeof<double>
            isNumeric |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MIN - Has expected parameters`` () =
        DsTagRegistry.clear()
        match MIN.create() with
        | Ok fc ->
            fc.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fc.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            let isNumeric = fc.ReturnType = typeof<int> || fc.ReturnType = typeof<double>
            isNumeric |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MAX - Has expected parameters`` () =
        DsTagRegistry.clear()
        match MAX.create() with
        | Ok fc ->
            fc.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fc.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            let isNumeric = fc.ReturnType = typeof<int> || fc.ReturnType = typeof<double>
            isNumeric |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``AVERAGE - Body contains logic`` () =
        DsTagRegistry.clear()
        match AVERAGE.create() with
        | Ok fc ->
            // Body should not be empty terminal
            match fc.Body with
            | Terminal _ -> failwith "Expected non-trivial body"
            | _ -> ()
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MIN - Body contains logic`` () =
        DsTagRegistry.clear()
        match MIN.create() with
        | Ok fc ->
            match fc.Body with
            | Terminal _ -> failwith "Expected non-trivial body"
            | _ -> ()
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MAX - Body contains logic`` () =
        DsTagRegistry.clear()
        match MAX.create() with
        | Ok fc ->
            match fc.Body with
            | Terminal _ -> failwith "Expected non-trivial body"
            | _ -> ()
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``AVERAGE - Supports Int data type`` () =
        DsTagRegistry.clear()
        match AVERAGE.create() with
        | Ok fc ->
            // Should accept Int parameters
            let hasIntParam = fc.Inputs |> List.exists (fun p ->
                p.DataType = typeof<int> || p.DataType = typeof<double>)
            hasIntParam |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MIN - Supports Int data type`` () =
        DsTagRegistry.clear()
        match MIN.create() with
        | Ok fc ->
            let hasIntParam = fc.Inputs |> List.exists (fun p ->
                p.DataType = typeof<int> || p.DataType = typeof<double>)
            hasIntParam |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MAX - Supports Int data type`` () =
        DsTagRegistry.clear()
        match MAX.create() with
        | Ok fc ->
            let hasIntParam = fc.Inputs |> List.exists (fun p ->
                p.DataType = typeof<int> || p.DataType = typeof<double>)
            hasIntParam |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``AVERAGE - Function is reusable`` () =
        DsTagRegistry.clear()
        // Create multiple instances to verify reusability
        match AVERAGE.create() with
        | Ok fc1 ->
            DsTagRegistry.clear()
            match AVERAGE.create() with
            | Ok fc2 ->
                fc1.Name |> should equal fc2.Name
                fc1.Inputs.Length |> should equal fc2.Inputs.Length
            | Error msg -> failwith $"Second creation failed: {msg}"
        | Error msg ->
            failwith $"First creation failed: {msg}"

    [<Fact>]
    let ``MIN - Function is reusable`` () =
        DsTagRegistry.clear()
        match MIN.create() with
        | Ok fc1 ->
            DsTagRegistry.clear()
            match MIN.create() with
            | Ok fc2 ->
                fc1.Name |> should equal fc2.Name
                fc1.Inputs.Length |> should equal fc2.Inputs.Length
            | Error msg -> failwith $"Second creation failed: {msg}"
        | Error msg ->
            failwith $"First creation failed: {msg}"

    [<Fact>]
    let ``MAX - Function is reusable`` () =
        DsTagRegistry.clear()
        match MAX.create() with
        | Ok fc1 ->
            DsTagRegistry.clear()
            match MAX.create() with
            | Ok fc2 ->
                fc1.Name |> should equal fc2.Name
                fc1.Inputs.Length |> should equal fc2.Inputs.Length
            | Error msg -> failwith $"Second creation failed: {msg}"
        | Error msg ->
            failwith $"First creation failed: {msg}"

    [<Fact>]
    let ``AVERAGE - Metadata is valid`` () =
        DsTagRegistry.clear()
        match AVERAGE.create() with
        | Ok fc ->
            // Metadata should be present (even if empty)
            fc.Metadata |> should not' (equal null)
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MIN - Metadata is valid`` () =
        DsTagRegistry.clear()
        match MIN.create() with
        | Ok fc ->
            fc.Metadata |> should not' (equal null)
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MAX - Metadata is valid`` () =
        DsTagRegistry.clear()
        match MAX.create() with
        | Ok fc ->
            fc.Metadata |> should not' (equal null)
        | Error msg ->
            failwith $"FC creation failed: {msg}"
