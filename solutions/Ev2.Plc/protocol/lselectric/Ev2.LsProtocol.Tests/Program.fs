namespace Ev2.LsProtocol.Tests

module Program =
    [<EntryPoint>]
    let main argv =
        // Print usage information for manual test execution
        printfn "LS Electric XGT Protocol Test Suite"
        printfn "======================================"
        printfn ""
        printfn "Unit Tests:"
        printfn "  dotnet test --filter Category!=Integration"
        printfn ""
        printfn "Integration Tests (requires XGT PLC):"
        printfn "  dotnet test --filter Category=Integration"
        printfn ""
        printfn "All Tests:"
        printfn "  dotnet test"
        printfn ""
        printfn "Environment Variables:"
        printfn "  XGT_TEST_IP=<PLC_IP>           (default: 192.168.1.100)"
        printfn "  XGT_TEST_PORT=<PORT>           (default: 2004)"
        printfn "  XGT_TEST_TIMEOUT_MS=<TIMEOUT>  (default: 5000)"
        printfn "  XGT_SKIP_INTEGRATION=true      (skip integration tests)"
        printfn ""
        0
