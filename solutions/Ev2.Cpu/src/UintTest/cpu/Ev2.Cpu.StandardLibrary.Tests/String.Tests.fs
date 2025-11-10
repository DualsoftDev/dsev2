namespace Ev2.Cpu.StandardLibrary.Tests

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.StandardLibrary.String

module StringTests =

    [<Fact>]
    let ``CONCAT - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match CONCAT.create() with
        | Ok fc ->
            fc.Name |> should equal "CONCAT"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``LEFT - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match LEFT.create() with
        | Ok fc ->
            fc.Name |> should equal "LEFT"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``RIGHT - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match RIGHT.create() with
        | Ok fc ->
            fc.Name |> should equal "RIGHT"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MID - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match MID.create() with
        | Ok fc ->
            fc.Name |> should equal "MID"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``FIND - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match FIND.create() with
        | Ok fc ->
            fc.Name |> should equal "FIND"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    // ═════════════════════════════════════════════════════════════════════
    // Boundary Value Tests (Phase 6)
    // ═════════════════════════════════════════════════════════════════════

    [<Fact>]
    let ``CONCAT - Has expected parameters`` () =
        DsTagRegistry.clear()
        match CONCAT.create() with
        | Ok fc ->
            fc.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fc.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            // Should return String type
            fc.ReturnType |> should equal DsDataType.TString

            // Should have String inputs
            fc.Inputs |> List.exists (fun p -> p.DataType = DsDataType.TString)
                      |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``LEFT - Has expected parameters`` () =
        DsTagRegistry.clear()
        match LEFT.create() with
        | Ok fc ->
            fc.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fc.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            fc.ReturnType |> should equal DsDataType.TString

            // Should have String input and Int length parameter
            fc.Inputs |> List.exists (fun p -> p.DataType = DsDataType.TString)
                      |> should equal true
            fc.Inputs |> List.exists (fun p -> p.DataType = DsDataType.TInt)
                      |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``RIGHT - Has expected parameters`` () =
        DsTagRegistry.clear()
        match RIGHT.create() with
        | Ok fc ->
            fc.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fc.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            fc.ReturnType |> should equal DsDataType.TString

            fc.Inputs |> List.exists (fun p -> p.DataType = DsDataType.TString)
                      |> should equal true
            fc.Inputs |> List.exists (fun p -> p.DataType = DsDataType.TInt)
                      |> should equal true
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MID - Has expected parameters`` () =
        DsTagRegistry.clear()
        match MID.create() with
        | Ok fc ->
            fc.Inputs.Length |> should be (greaterThanOrEqualTo 3)
            fc.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            fc.ReturnType |> should equal DsDataType.TString

            // Should have String input, Int start position, Int length
            fc.Inputs |> List.exists (fun p -> p.DataType = DsDataType.TString)
                      |> should equal true
            fc.Inputs |> List.filter (fun p -> p.DataType = DsDataType.TInt)
                      |> List.length |> should be (greaterThanOrEqualTo 2)
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``FIND - Has expected parameters`` () =
        DsTagRegistry.clear()
        match FIND.create() with
        | Ok fc ->
            fc.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fc.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            // FIND returns Int (position)
            fc.ReturnType |> should equal DsDataType.TInt

            // Should have String inputs (source and search string)
            fc.Inputs |> List.filter (fun p -> p.DataType = DsDataType.TString)
                      |> List.length |> should be (greaterThanOrEqualTo 2)
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``CONCAT - Body contains logic`` () =
        DsTagRegistry.clear()
        match CONCAT.create() with
        | Ok fc ->
            match fc.Body with
            | Terminal _ -> failwith "Expected non-trivial body"
            | _ -> ()
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``LEFT - Body contains logic`` () =
        DsTagRegistry.clear()
        match LEFT.create() with
        | Ok fc ->
            match fc.Body with
            | Terminal _ -> failwith "Expected non-trivial body"
            | _ -> ()
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``RIGHT - Body contains logic`` () =
        DsTagRegistry.clear()
        match RIGHT.create() with
        | Ok fc ->
            match fc.Body with
            | Terminal _ -> failwith "Expected non-trivial body"
            | _ -> ()
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``MID - Body contains logic`` () =
        DsTagRegistry.clear()
        match MID.create() with
        | Ok fc ->
            match fc.Body with
            | Terminal _ -> failwith "Expected non-trivial body"
            | _ -> ()
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``FIND - Body contains logic`` () =
        DsTagRegistry.clear()
        match FIND.create() with
        | Ok fc ->
            match fc.Body with
            | Terminal _ -> failwith "Expected non-trivial body"
            | _ -> ()
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``CONCAT - Function is reusable`` () =
        DsTagRegistry.clear()
        match CONCAT.create() with
        | Ok fc1 ->
            DsTagRegistry.clear()
            match CONCAT.create() with
            | Ok fc2 ->
                fc1.Name |> should equal fc2.Name
                fc1.Inputs.Length |> should equal fc2.Inputs.Length
            | Error msg -> failwith $"Second creation failed: {msg}"
        | Error msg ->
            failwith $"First creation failed: {msg}"

    [<Fact>]
    let ``LEFT - Function is reusable`` () =
        DsTagRegistry.clear()
        match LEFT.create() with
        | Ok fc1 ->
            DsTagRegistry.clear()
            match LEFT.create() with
            | Ok fc2 ->
                fc1.Name |> should equal fc2.Name
            | Error msg -> failwith $"Second creation failed: {msg}"
        | Error msg ->
            failwith $"First creation failed: {msg}"

    [<Fact>]
    let ``RIGHT - Function is reusable`` () =
        DsTagRegistry.clear()
        match RIGHT.create() with
        | Ok fc1 ->
            DsTagRegistry.clear()
            match RIGHT.create() with
            | Ok fc2 ->
                fc1.Name |> should equal fc2.Name
            | Error msg -> failwith $"Second creation failed: {msg}"
        | Error msg ->
            failwith $"First creation failed: {msg}"

    [<Fact>]
    let ``MID - Function is reusable`` () =
        DsTagRegistry.clear()
        match MID.create() with
        | Ok fc1 ->
            DsTagRegistry.clear()
            match MID.create() with
            | Ok fc2 ->
                fc1.Name |> should equal fc2.Name
            | Error msg -> failwith $"Second creation failed: {msg}"
        | Error msg ->
            failwith $"First creation failed: {msg}"

    [<Fact>]
    let ``FIND - Function is reusable`` () =
        DsTagRegistry.clear()
        match FIND.create() with
        | Ok fc1 ->
            DsTagRegistry.clear()
            match FIND.create() with
            | Ok fc2 ->
                fc1.Name |> should equal fc2.Name
            | Error msg -> failwith $"Second creation failed: {msg}"
        | Error msg ->
            failwith $"First creation failed: {msg}"
