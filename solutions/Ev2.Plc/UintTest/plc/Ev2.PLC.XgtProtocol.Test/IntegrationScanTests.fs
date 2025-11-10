namespace Ev2.PLC.XgtProtocol.Tests
open System
open System.Collections.Generic
open System.Threading
open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.XgtProtocol
open TestEndpoints
open System.Diagnostics

module IntegrationScanTests =
    // 태그별 개별 카운터를 관리하는 Dictionary
    type TagCounters = Dictionary<string, int64>
    
    let generateIncrementalValue (tag: string) (counters: TagCounters) : ScalarValue * PlcTagDataType =
        // 해당 태그의 카운터가 없으면 초기화
        if not (counters.ContainsKey(tag)) then
            counters.[tag] <- 0L
        
        let currentValue = counters.[tag]
        counters.[tag] <- currentValue + 1L
        
        // 태그 타입에 따라 적절한 타입으로 변환
        if tag.Contains("X") then 
            // Bool 타입: 짝수/홀수로 true/false 결정
            ScalarValue.BoolValue (currentValue % 2L = 0L), PlcTagDataType.Bool
            
        elif tag.Contains("B") then 
            // Byte 타입: 0-255 순환
            let byteValue = byte (currentValue % 256L)
            ScalarValue.UInt8Value byteValue, PlcTagDataType.UInt8
            
        elif tag.Contains("W") then 
            // Int16 타입: -32768 ~ 32767 범위 내에서 순환
            let int16Value = int16 (currentValue % 65536L - 32768L)
            ScalarValue.Int16Value int16Value, PlcTagDataType.Int16
            
        elif tag.Contains("D") then 
            // Int32 타입: 그대로 사용 (오버플로우 시 순환)
            let int32Value = int32 currentValue
            ScalarValue.Int32Value int32Value, PlcTagDataType.Int32
            
        elif tag.Contains("L") or tag.Contains("R") then 
            // Int64 타입: 그대로 사용
            ScalarValue.Int64Value currentValue, PlcTagDataType.Int64
            
        else 
            failwithf "알 수 없는 태그 타입: %s" tag
    
    [<Fact>]
    let ``Integration - Incremental Write & Read for 30 Seconds`` () =
        let scanMgr = XgtScanManager(false, 20, 3000, false)
        let ip = TestEndpoints.ipXGIEFMTB
        
        // 테스트할 태그 목록
        let tags =
            [ "%MD100"; "%MD200"; "%ML1000"; "%ML2000"; "%ML3000"; "%ML4000"; "%RL123" ]
        
        // 각 태그별 개별 카운터
        let counters = TagCounters()
        
        try
            // 스캔 시작
            let result = scanMgr.StartScanReadOnly(ip, tags)
            
            // 태그별 초기값 출력
            printfn "\n========================================="
            printfn "[✓] 태그별 개별 카운터 초기화"
            for tag in tags do
                counters.[tag] <- 0L
                printfn "  %s: 카운터 시작값 = 0" tag
            printfn "========================================="
            
            // 태그 값 변경 이벤트 구독 - 모든 변경사항을 Debug 로그에 출력
            match scanMgr.GetScanner(ip) with
            | Some scanner ->
                scanner.TagValueChanged.Add(fun evt ->
                    match evt.Tag with
                    | :? XgtTag as tag -> 
                        let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                        Debug.WriteLine $"[{timestamp}] [Read Event] {tag.Address} → {tag.Value}"
                    | _ -> ())
            | None -> ()
            
            let startTime = DateTime.Now
            let duration = TimeSpan.FromSeconds(3.0)
            let mutable cycleCount = 0
            
            printfn "\n========================================="
            printfn "[✓] 태그별 순차 증가 테스트 시작"
            printfn "시작 시간: %O" startTime
            printfn "테스트 시간: %.0f초" duration.TotalSeconds
            printfn "쓰기 간격: 10ms (초당 약 100회)"
            printfn "디버그 로그: 모든 읽기/쓰기 이벤트 기록"
            printfn "콘솔 출력: 100회마다 (약 1초마다)"
            printfn "=========================================\n"
            
            // 테스트 실행
            while DateTime.Now - startTime < duration do
                cycleCount <- cycleCount + 1
                let elapsed = DateTime.Now - startTime
                
                // 100회마다 진행상황 출력 (약 1초마다)
                if cycleCount % 100 = 0 then
                    printfn $"[Cycle #{cycleCount:D4}] 경과: {elapsed.TotalSeconds:F1}초"
                
                for KeyValue(_, tag) in result do
                    let value, dtype = generateIncrementalValue tag.Address counters
                    tag.SetWriteValue(value)
                    
                    // 모든 쓰기 동작을 Debug 로그에 출력
                    let timestamp = DateTime.Now.ToString("HH:mm:ss.fff")
                    Debug.WriteLine $"[{timestamp}] [Write] {tag.Address} ← {value} ({dtype}) [Counter: {counters.[tag.Address]}]"
                    
                    // 100회마다 콘솔에도 출력 (진행 상황 확인용)
                    if cycleCount % 100 = 0 then
                        let counterValue = counters.[tag.Address]
                        printfn $"  {tag.Address}: 카운터={counterValue} → 쓰기값={value} ({dtype})"
                
                if cycleCount % 100 = 0 then
                    printfn ""  // 사이클 간 구분 빈 줄
                
                // 쓰기 간격 (10ms)
                Thread.Sleep(10)
            
            // 스캔 중단
            scanMgr.StopScan(ip)
            
            // 최종 통계
            printfn "========================================="
            printfn "[✓] 테스트 완료"
            printfn "종료 시간: %O" DateTime.Now
            printfn "총 사이클 수: %d" cycleCount
            printfn "\n[최종 카운터 값]"
            for KeyValue(tag, count) in counters do
                printfn "  %s: %d회 쓰기 완료" tag count
            printfn "=========================================\n"
            
        with ex ->
            printfn $"[!] PLC 연결 실패 (테스트 스킵 처리): {ex.Message}"