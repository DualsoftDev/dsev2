namespace Ev2.Cpu.StandardLibrary.Tests

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.StandardLibrary.Analog

module AnalogTests =

    [<Fact>]
    let ``SCALE - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match SCALE.create() with
        | Ok fc ->
            fc.Name |> should equal "SCALE"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``LIMIT - FC 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match LIMIT.create() with
        | Ok fc ->
            fc.Name |> should equal "LIMIT"
            match fc.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FC creation failed: {msg}"

    [<Fact>]
    let ``HYSTERESIS - FB 생성 및 Validation`` () =
        DsTagRegistry.clear()
        match HYSTERESIS.create() with
        | Ok fb ->
            fb.Name |> should equal "HYSTERESIS"
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"
