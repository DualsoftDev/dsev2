namespace Ev2.Cpu.StandardLibrary.Tests

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.StandardLibrary.EdgeDetection

module EdgeDetectionTests =

    [<Fact>]
    let ``R_TRIG - FB 생성 성공`` () =
        DsTagRegistry.clear()
        match R_TRIG.create() with
        | Ok fb ->
            fb.Name |> should equal "R_TRIG"
            fb.Inputs |> List.length |> should equal 1
            fb.Outputs |> List.length |> should equal 1
            fb.Statics |> List.length |> should greaterThan 0
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``R_TRIG - 입력 파라미터 검증`` () =
        DsTagRegistry.clear()
        match R_TRIG.create() with
        | Ok fb ->
            let clkInput = fb.Inputs |> List.find (fun p -> p.Name = "CLK")
            clkInput.DataType |> should equal DsDataType.TBool
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``R_TRIG - 출력 파라미터 검증`` () =
        DsTagRegistry.clear()
        match R_TRIG.create() with
        | Ok fb ->
            let qOutput = fb.Outputs |> List.find (fun p -> p.Name = "Q")
            qOutput.DataType |> should equal DsDataType.TBool
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``F_TRIG - FB 생성 성공`` () =
        DsTagRegistry.clear()
        match F_TRIG.create() with
        | Ok fb ->
            fb.Name |> should equal "F_TRIG"
            fb.Inputs |> List.length |> should equal 1
            fb.Outputs |> List.length |> should equal 1
            fb.Statics |> List.length |> should greaterThan 0
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``F_TRIG - 입력 파라미터 검증`` () =
        DsTagRegistry.clear()
        match F_TRIG.create() with
        | Ok fb ->
            let clkInput = fb.Inputs |> List.find (fun p -> p.Name = "CLK")
            clkInput.DataType |> should equal DsDataType.TBool
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``F_TRIG - 출력 파라미터 검증`` () =
        DsTagRegistry.clear()
        match F_TRIG.create() with
        | Ok fb ->
            let qOutput = fb.Outputs |> List.find (fun p -> p.Name = "Q")
            qOutput.DataType |> should equal DsDataType.TBool
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``R_TRIG - Validation 성공`` () =
        DsTagRegistry.clear()
        match R_TRIG.create() with
        | Ok fb ->
            match fb.Validate() with
            | Ok () -> ()  // Success
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``F_TRIG - Validation 성공`` () =
        DsTagRegistry.clear()
        match F_TRIG.create() with
        | Ok fb ->
            match fb.Validate() with
            | Ok () -> ()  // Success
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"
