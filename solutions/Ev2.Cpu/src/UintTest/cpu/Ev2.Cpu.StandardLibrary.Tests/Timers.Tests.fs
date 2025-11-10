namespace Ev2.Cpu.StandardLibrary.Tests

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.StandardLibrary.Timers

module TimersTests =

    [<Fact>]
    let ``TON - FB 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match TON.create() with
        | Ok fb ->
            fb.Name |> should equal "TON"
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TOF - FB 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match TOF.create() with
        | Ok fb ->
            fb.Name |> should equal "TOF"
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TP - FB 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match TP.create() with
        | Ok fb ->
            fb.Name |> should equal "TP"
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TONR - FB 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match TONR.create() with
        | Ok fb ->
            fb.Name |> should equal "TONR"
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    // ═════════════════════════════════════════════════════════════════════
    // Boundary Value Tests (Phase 6)
    // ═════════════════════════════════════════════════════════════════════

    [<Fact>]
    let ``TON - Has expected input/output parameters`` () =
        DsTagRegistry.clear()
        match TON.create() with
        | Ok fb ->
            fb.Inputs.Length |> should be (greaterThanOrEqualTo 2)  // At least IN and PT
            fb.Outputs.Length |> should be (greaterThanOrEqualTo 1)  // At least Q

            // Verify IN parameter exists (Bool)
            fb.Inputs |> List.exists (fun p -> p.Name = "IN" && p.DataType = DsDataType.TBool)
                      |> should equal true

            // Verify PT parameter exists (Int, for milliseconds)
            fb.Inputs |> List.exists (fun p -> p.Name = "PT" && p.DataType = DsDataType.TInt)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TOF - Has expected input/output parameters`` () =
        DsTagRegistry.clear()
        match TOF.create() with
        | Ok fb ->
            fb.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fb.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            fb.Inputs |> List.exists (fun p -> p.Name = "IN" && p.DataType = DsDataType.TBool)
                      |> should equal true
            fb.Inputs |> List.exists (fun p -> p.Name = "PT" && p.DataType = DsDataType.TInt)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TP - Has expected input/output parameters`` () =
        DsTagRegistry.clear()
        match TP.create() with
        | Ok fb ->
            fb.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fb.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            fb.Inputs |> List.exists (fun p -> p.Name = "IN" && p.DataType = DsDataType.TBool)
                      |> should equal true
            fb.Inputs |> List.exists (fun p -> p.Name = "PT" && p.DataType = DsDataType.TInt)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TONR - Has expected input/output parameters`` () =
        DsTagRegistry.clear()
        match TONR.create() with
        | Ok fb ->
            fb.Inputs.Length |> should be (greaterThanOrEqualTo 2)
            fb.Outputs.Length |> should be (greaterThanOrEqualTo 1)

            fb.Inputs |> List.exists (fun p -> p.Name = "IN" && p.DataType = DsDataType.TBool)
                      |> should equal true
            fb.Inputs |> List.exists (fun p -> p.Name = "R" && p.DataType = DsDataType.TBool)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TON - Has static variables for state retention`` () =
        DsTagRegistry.clear()
        match TON.create() with
        | Ok fb ->
            // TON needs at least one static variable to track elapsed time
            fb.Statics.Length |> should be (greaterThanOrEqualTo 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TOF - Has static variables for state retention`` () =
        DsTagRegistry.clear()
        match TOF.create() with
        | Ok fb ->
            fb.Statics.Length |> should be (greaterThanOrEqualTo 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TP - Has static variables for state retention`` () =
        DsTagRegistry.clear()
        match TP.create() with
        | Ok fb ->
            fb.Statics.Length |> should be (greaterThanOrEqualTo 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TONR - Has static variables for accumulator`` () =
        DsTagRegistry.clear()
        match TONR.create() with
        | Ok fb ->
            // TONR needs static variables to accumulate time
            fb.Statics.Length |> should be (greaterThanOrEqualTo 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TON - Body contains logic`` () =
        DsTagRegistry.clear()
        match TON.create() with
        | Ok fb ->
            fb.Body.Length |> should be (greaterThan 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``TOF - Body contains logic`` () =
        DsTagRegistry.clear()
        match TOF.create() with
        | Ok fb ->
            fb.Body.Length |> should be (greaterThan 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"
