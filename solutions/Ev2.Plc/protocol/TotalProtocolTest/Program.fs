open System.Diagnostics
open TotalProtocolTest.Common
open TotalProtocolTest.Common.Env

[<EntryPoint>]
let main _ =
    let config = Env.runtimeConfig()

    let results = ResizeArray<string * float>()

    let runWithTiming name action =
        let sw = Stopwatch.StartNew()
        try
            action()
            sw.Stop()
            results.Add(name, sw.Elapsed.TotalSeconds)
            printfn "[%s] 완료 - %.3f초" name sw.Elapsed.TotalSeconds
        with ex ->
            sw.Stop()
            results.Add(name, sw.Elapsed.TotalSeconds)
            printfn "[%s] 실패 - %.3f초 (%s)" name sw.Elapsed.TotalSeconds ex.Message
            reraise()

    try
        runWithTiming "mitsubishiEthernet" (fun () -> TotalProtocolTest.MitsubishiTest.run config mxEthernet)
        runWithTiming "mitsubishiLocalEthernet" (fun () -> TotalProtocolTest.MitsubishiTest.run config mxLocalEthernet)
        runWithTiming "lsEfmtb" (fun () -> TotalProtocolTest.LsTest.run config lsEfmtb)
        runWithTiming "lsLocalEthernet" (fun () -> TotalProtocolTest.LsTest.run config lsLocalEthernet)
        runWithTiming "siemensCp" (fun () -> TotalProtocolTest.SiemensTest.run config siemensCp 0 2)
        runWithTiming "siemensLocalEthernet" (fun () -> TotalProtocolTest.SiemensTest.run config siemensLocalEthernet 0 2)
        runWithTiming "allenBradley" (fun () -> TotalProtocolTest.AbTest.run config abEndpoint)

        printfn "\n=========================================="
        printfn "모든 PLC 타입별 토글 테스트 완료"
        printfn "=========================================="
        printfn "%-25s %10s" "PLC" "실행시간"
        printfn "------------------------------------------"

        let mutable totalTime = 0.0
        for (name, time) in results do
            printfn "%-25s %8.3f초" name time
            totalTime <- totalTime + time

        printfn "------------------------------------------"
        printfn "%-25s %8.3f초" "총 실행시간" totalTime
        printfn "=========================================="

        0
    with ex ->
        printfn "\n=========================================="
        printfn "[오류] %s" ex.Message
        printfn "=========================================="

        if results.Count > 0 then
            printfn "\n부분 실행 결과:"
            printfn "%-25s %10s" "PLC" "실행시간"
            printfn "------------------------------------------"
            for (name, time) in results do
                printfn "%-25s %8.3f초" name time

        1
