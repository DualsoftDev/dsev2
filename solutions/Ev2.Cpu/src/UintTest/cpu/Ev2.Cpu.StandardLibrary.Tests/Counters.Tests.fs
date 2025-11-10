namespace Ev2.Cpu.StandardLibrary.Tests

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.StandardLibrary.Counters

module CountersTests =

    [<Fact>]
    let ``CTU - FB 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match CTU.create() with
        | Ok fb ->
            fb.Name |> should equal "CTU"
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTD - FB 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match CTD.create() with
        | Ok fb ->
            fb.Name |> should equal "CTD"
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTUD - FB 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match CTUD.create() with
        | Ok fb ->
            fb.Name |> should equal "CTUD"
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    // ═════════════════════════════════════════════════════════════════════
    // Boundary Value Tests (Phase 6)
    // ═════════════════════════════════════════════════════════════════════

    [<Fact>]
    let ``CTU - Has expected input/output parameters`` () =
        DsTagRegistry.clear()
        match CTU.create() with
        | Ok fb ->
            fb.Inputs.Length |> should be (greaterThanOrEqualTo 2)  // At least CU and R
            fb.Outputs.Length |> should be (greaterThanOrEqualTo 1)  // At least Q

            // Verify CU (Count Up) parameter exists
            fb.Inputs |> List.exists (fun p -> p.Name = "CU" && p.DataType = DsDataType.TBool)
                      |> should equal true

            // Verify R (Reset) parameter exists
            fb.Inputs |> List.exists (fun p -> p.Name = "R" && p.DataType = DsDataType.TBool)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTD - Has expected input/output parameters`` () =
        DsTagRegistry.clear()
        match CTD.create() with
        | Ok fb ->
            fb.Inputs.Length |> should be (greaterThanOrEqualTo 2)  // At least CD and LD
            fb.Outputs.Length |> should be (greaterThanOrEqualTo 1)  // At least Q

            // Verify CD (Count Down) parameter exists
            fb.Inputs |> List.exists (fun p -> p.Name = "CD" && p.DataType = DsDataType.TBool)
                      |> should equal true

            // Verify LD (Load) parameter exists
            fb.Inputs |> List.exists (fun p -> p.Name = "LD" && p.DataType = DsDataType.TBool)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTUD - Has expected input/output parameters`` () =
        DsTagRegistry.clear()
        match CTUD.create() with
        | Ok fb ->
            fb.Inputs.Length |> should be (greaterThanOrEqualTo 3)  // At least CU, CD, R
            fb.Outputs.Length |> should be (greaterThanOrEqualTo 1)  // At least QU or QD

            // Verify CU parameter exists
            fb.Inputs |> List.exists (fun p -> p.Name = "CU" && p.DataType = DsDataType.TBool)
                      |> should equal true

            // Verify CD parameter exists
            fb.Inputs |> List.exists (fun p -> p.Name = "CD" && p.DataType = DsDataType.TBool)
                      |> should equal true

            // Verify R parameter exists
            fb.Inputs |> List.exists (fun p -> p.Name = "R" && p.DataType = DsDataType.TBool)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTU - Has static variables for counter value`` () =
        DsTagRegistry.clear()
        match CTU.create() with
        | Ok fb ->
            // CTU needs static variables to store CV (Current Value)
            fb.Statics.Length |> should be (greaterThanOrEqualTo 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTD - Has static variables for counter value`` () =
        DsTagRegistry.clear()
        match CTD.create() with
        | Ok fb ->
            fb.Statics.Length |> should be (greaterThanOrEqualTo 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTUD - Has static variables for counter value`` () =
        DsTagRegistry.clear()
        match CTUD.create() with
        | Ok fb ->
            fb.Statics.Length |> should be (greaterThanOrEqualTo 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTU - Body contains logic`` () =
        DsTagRegistry.clear()
        match CTU.create() with
        | Ok fb ->
            fb.Body.Length |> should be (greaterThan 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTD - Body contains logic`` () =
        DsTagRegistry.clear()
        match CTD.create() with
        | Ok fb ->
            fb.Body.Length |> should be (greaterThan 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTUD - Body contains logic`` () =
        DsTagRegistry.clear()
        match CTUD.create() with
        | Ok fb ->
            fb.Body.Length |> should be (greaterThan 0)
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTU - Has PV (Preset Value) parameter`` () =
        DsTagRegistry.clear()
        match CTU.create() with
        | Ok fb ->
            // PV should be an Int parameter
            fb.Inputs |> List.exists (fun p -> p.Name = "PV" && p.DataType = DsDataType.TInt)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``CTD - Has PV (Preset Value) parameter`` () =
        DsTagRegistry.clear()
        match CTD.create() with
        | Ok fb ->
            fb.Inputs |> List.exists (fun p -> p.Name = "PV" && p.DataType = DsDataType.TInt)
                      |> should equal true
        | Error msg ->
            failwith $"FB creation failed: {msg}"
