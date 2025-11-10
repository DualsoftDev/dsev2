namespace Ev2.Cpu.StandardLibrary.Tests

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.UserDefined
open Ev2.Cpu.StandardLibrary

// Disable parallel test execution to avoid tag registry conflicts
[<assembly: CollectionBehavior(DisableTestParallelization = true)>]
do ()

module RegistryTests =

    [<Fact>]
    let ``StandardLibraryRegistry - 모든 FB 이름 목록 조회`` () =
        DsTagRegistry.clear()
        let names = StandardLibraryRegistry.getAllStandardFBNames()
        names |> List.length |> should equal 22

        // 주요 FB 존재 확인
        names |> should contain "R_TRIG"
        names |> should contain "TON"
        names |> should contain "CTU"
        names |> should contain "SCALE"
        names |> should contain "AVERAGE"
        names |> should contain "CONCAT"

    [<Fact>]
    let ``StandardLibraryRegistry - 모든 FB 생성 성공`` () =
        DsTagRegistry.clear()
        let allFBs = StandardLibraryRegistry.createAllStandardFBs()
        allFBs |> Map.count |> should equal 22

        // 모든 FB가 성공적으로 생성되어야 함
        allFBs
        |> Map.iter (fun name result ->
            match result with
            | Ok _ -> ()  // Success
            | Error msg -> failwith $"FB '{name}' creation failed: {msg}")

    [<Fact>]
    let ``StandardLibraryRegistry - 통계 조회`` () =
        DsTagRegistry.clear()
        let stats = StandardLibraryRegistry.getStatistics()

        stats.TotalFBs |> should equal 22
        stats.EdgeDetection |> should equal 2
        stats.Bistable |> should equal 2
        stats.Timers |> should equal 4
        stats.Counters |> should equal 3
        stats.Analog |> should equal 3
        stats.Math |> should equal 3
        stats.String |> should equal 5

    [<Fact>]
    let ``StandardLibraryRegistry - 모든 FB Validation 성공`` () =
        DsTagRegistry.clear()
        let validationResults = StandardLibraryRegistry.validateAll()

        validationResults |> List.length |> should equal 22

        // 모든 FB가 검증을 통과해야 함
        validationResults
        |> List.iter (fun (name, result) ->
            match result with
            | Ok () -> ()  // Success
            | Error msg -> failwith $"FB '{name}' validation failed: {msg}")

    [<Fact>]
    let ``StandardLibraryRegistry - UserLibrary 등록 테스트`` () =
        DsTagRegistry.clear()
        let library = UserLibrary()
        let results = StandardLibraryRegistry.registerAllTo library

        results |> List.length |> should equal 22

        // 모든 등록이 성공해야 함
        let successCount =
            results
            |> List.filter (fun (_, result) ->
                match result with Ok _ -> true | Error _ -> false)
            |> List.length

        successCount |> should equal 22

    [<Fact>]
    let ``StandardLibraryRegistry - 초기화 테스트`` () =
        DsTagRegistry.clear()
        let (successCount, failureCount) = StandardLibraryRegistry.initialize()

        successCount |> should equal 22
        failureCount |> should equal 0
