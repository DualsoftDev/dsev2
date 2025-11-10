namespace Ev2.Cpu.StandardLibrary.Tests

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.StandardLibrary.Bistable

module BistableTests =

    [<Fact>]
    let ``SR - FB 생성 성공`` () =
        DsTagRegistry.clear()
        match SR.create() with
        | Ok fb ->
            fb.Name |> should equal "SR"
            fb.Inputs |> List.length |> should equal 2
            fb.Outputs |> List.length |> should equal 1
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``SR - Validation 성공`` () =
        DsTagRegistry.clear()
        match SR.create() with
        | Ok fb ->
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``RS - FB 생성 성공`` () =
        DsTagRegistry.clear()
        match RS.create() with
        | Ok fb ->
            fb.Name |> should equal "RS"
            fb.Inputs |> List.length |> should equal 2
            fb.Outputs |> List.length |> should equal 1
        | Error msg ->
            failwith $"FB creation failed: {msg}"

    [<Fact>]
    let ``RS - Validation 성공`` () =
        DsTagRegistry.clear()
        match RS.create() with
        | Ok fb ->
            match fb.Validate() with
            | Ok () -> ()
            | Error msg -> failwith $"Validation failed: {msg}"
        | Error msg ->
            failwith $"FB creation failed: {msg}"
