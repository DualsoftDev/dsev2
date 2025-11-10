namespace ProtocolTestHelper

open System
open System.Diagnostics
open System.Text.RegularExpressions
open ProtocolTestHelper.TestExecution

/// Shared helpers for running integration scenarios against protocol clients.
module IntegrationTestRunner =
    
    /// Exception used internally to shuttle protocol-specific errors through the exception pipeline.
    type private ClientConnectionFailed<'TError>(error: 'TError) =
        inherit Exception()
        member _.Error = error
    
    /// Describes how to construct, connect, and tear down a protocol client for integration testing.
    type ClientLifecycle<'TClient, 'TError, 'TConnectInfo> =
        { CreateClient: unit -> 'TClient
          Connect: 'TClient -> Result<'TConnectInfo, 'TError>
          Disconnect: 'TClient -> unit
          Dispose: 'TClient -> unit
          MapException: exn -> 'TError
          DumpLogs: unit -> string
          AugmentError: 'TError -> string -> 'TError }
    
    /// Runs an integration test action with a managed protocol client, capturing stdout/stderr.
    let runWithClient (lifecycle: ClientLifecycle<'TClient, 'TError, 'TConnectInfo>) (action: 'TClient -> 'T)
        : TestResult<'T, 'TError> =
        
        let appendLogs error =
            let logs = lifecycle.DumpLogs()
            if String.IsNullOrWhiteSpace logs then error
            else lifecycle.AugmentError error logs
        
        let mapExceptionWithLogs (ex: exn) =
            match ex with
            | :? ClientConnectionFailed<'TError> as failure -> failure.Error
            | _ -> lifecycle.MapException ex |> appendLogs

        TestExecution.captureOutput mapExceptionWithLogs (fun () ->
            let client = lifecycle.CreateClient()
            try
                match lifecycle.Connect client with
                | Ok _ ->
                    try
                        action client
                    finally
                        lifecycle.Disconnect client
                | Error error ->
                    let errorWithLogs = appendLogs error
                    lifecycle.Disconnect client
                    raise (ClientConnectionFailed errorWithLogs)
            finally
                lifecycle.Dispose client)
    
    /// Unwraps a `TestResult`, failing with logs when an error is present.
    let unwrapOrFail (failWithLogs: TestResult<'T, 'TError> -> string -> unit)
                     (messageBuilder: 'TError -> string)
                     (result: TestResult<'T, 'TError>) : 'T =
        match result.Result with
        | Ok value -> value
        | Error error ->
            failWithLogs result (messageBuilder error)
            raise (InvalidOperationException "failWithLogs is expected to throw before returning.")

/// 자동화된 프로토콜 테스트 실행 및 결과 분석
module AutomatedTestRunner =
    
    /// 테스트 실행 결과 정보
    type TestExecutionResult = {
        ProtocolName: string
        TestName: string
        Duration: TimeSpan
        Status: TestStatus
        StdOut: string
        ErrorMessage: string option
        StackTrace: string option
        HexDumps: string list
        PerformanceMetrics: (string * float) list
    }
    and TestStatus = 
        | Passed 
        | Failed 
        | Skipped

    /// 실패 패턴 분류
    type FailurePattern =
        | ConnectionTimeout
        | CipError of errorCode: string
        | WriteOperationFailed 
        | PayloadTooLarge
        | ProtocolSpecificError of message: string
        | Unknown of message: string

    /// 테스트 결과창 파싱 함수들
    module TestOutputParser =
        
        let parseTestDuration (output: string) =
            let durationRegex = Regex(@"기간:\s*(\d+)ms", RegexOptions.IgnoreCase)
            match durationRegex.Match(output) with
            | m when m.Success -> 
                match Int32.TryParse(m.Groups.[1].Value) with
                | true, ms -> TimeSpan.FromMilliseconds(float ms)
                | _ -> TimeSpan.Zero
            | _ -> TimeSpan.Zero

        let extractHexDumps (output: string) =
            let hexRegex = Regex(@"\[TX\]\s*\(\d+\s*bytes\)\s*([0-9A-F\s]+)", RegexOptions.IgnoreCase)
            let rxRegex = Regex(@"\[RX\]\s*\(\d+\s*bytes\)\s*([0-9A-F\s]+)", RegexOptions.IgnoreCase)
            
            let txMatches = hexRegex.Matches(output) |> Seq.cast<Match> |> Seq.map (fun m -> $"TX: {m.Groups.[1].Value.Trim()}")
            let rxMatches = rxRegex.Matches(output) |> Seq.cast<Match> |> Seq.map (fun m -> $"RX: {m.Groups.[1].Value.Trim()}")
            
            Seq.append txMatches rxMatches |> List.ofSeq

        let detectFailurePattern (output: string) =
            if output.Contains("CIP error 0xFF") then
                CipError "0xFF"
            elif output.Contains("Connection timeout") || output.Contains("연결이 성립하지 않았습니다") then
                ConnectionTimeout
            elif output.Contains("Write operation failed") then
                WriteOperationFailed
            elif output.Contains("Payload preview") && output.Contains("too large") then
                PayloadTooLarge
            elif output.Contains("✗") || output.Contains("실패") then
                let errorRegex = Regex(@"✗\s*([^:]+):\s*(.+)", RegexOptions.IgnoreCase)
                match errorRegex.Match(output) with
                | m when m.Success -> ProtocolSpecificError (m.Groups.[2].Value.Trim())
                | _ -> Unknown "Unspecified test failure"
            else
                Unknown "Could not classify failure pattern"

        let extractPerformanceMetrics (output: string) =
            let metrics = ResizeArray<string * float>()
            
            // Duration 추출
            let duration = parseTestDuration output
            if duration > TimeSpan.Zero then
                metrics.Add("Duration", duration.TotalMilliseconds)
            
            // Chunk 처리 성능 추출
            let chunkRegex = Regex(@"Chunk\s+(\d+):\s*[^(]+\((\d+)\s*elements\)", RegexOptions.IgnoreCase)
            let chunks = chunkRegex.Matches(output) |> Seq.cast<Match> |> Seq.length
            if chunks > 0 then
                metrics.Add("ChunksProcessed", float chunks)
            
            metrics |> List.ofSeq

    /// 프로토콜별 테스트 실행기
    let runProtocolTests (protocolName: string) (testProjectPath: string) =
        let processInfo = ProcessStartInfo()
        processInfo.FileName <- "/mnt/c/Program Files/dotnet/dotnet.exe"
        processInfo.Arguments <- $"test \"{testProjectPath}\" --verbosity normal"
        processInfo.RedirectStandardOutput <- true
        processInfo.RedirectStandardError <- true
        processInfo.UseShellExecute <- false
        processInfo.CreateNoWindow <- true

        match Process.Start(processInfo) with
        | null ->
            {
                ProtocolName = protocolName
                TestName = "Integration Tests"
                Duration = TimeSpan.Zero
                Status = Failed
                StdOut = ""
                ErrorMessage = Some "Failed to start process"
                StackTrace = None
                HexDumps = []
                PerformanceMetrics = []
            }
        | proc ->
            use _proc = proc
            let output = proc.StandardOutput.ReadToEnd()
            let error = proc.StandardError.ReadToEnd()
            proc.WaitForExit()

            let combinedOutput = output + "\n" + error

            {
                ProtocolName = protocolName
                TestName = "Integration Tests"
                Duration = TestOutputParser.parseTestDuration combinedOutput
                Status = if proc.ExitCode = 0 then Passed else Failed
                StdOut = combinedOutput
                ErrorMessage = if String.IsNullOrWhiteSpace(error) then None else Some error
                StackTrace = None
                HexDumps = TestOutputParser.extractHexDumps combinedOutput
                PerformanceMetrics = TestOutputParser.extractPerformanceMetrics combinedOutput
            }

    /// 실패 분석 및 해결책 제안
    let analyzeAndSuggestFix (protocolName: string) (pattern: FailurePattern) (result: TestExecutionResult) =
        match pattern with
        | CipError errorCode ->
            printfn $"  └─ CIP 에러 {errorCode} 감지"
            if result.HexDumps.Length > 0 then
                printfn $"  └─ 헥사 덤프 {result.HexDumps.Length}개 캐처됨"
            printfn $"  └─ 제안: 청크 크기를 줄이거나 재시도 로직 추가"
        
        | ConnectionTimeout ->
            printfn $"  └─ 연결 타임아웃 감지"
            printfn $"  └─ 제안: PLC 연결 상태 확인 및 네트워크 점검"
        
        | WriteOperationFailed ->
            printfn $"  └─ 쓰기 작업 실패"
            printfn $"  └─ 제안: 권한 확인 및 PLC 모드 상태 점검"
        
        | PayloadTooLarge ->
            printfn $"  └─ 페이로드 크기 초과"
            printfn $"  └─ 제안: 데이터를 더 작은 청크로 분할"
        
        | ProtocolSpecificError msg ->
            printfn $"  └─ 프로토콜 특화 에러: {msg}"
        
        | Unknown msg ->
            printfn $"  └─ 알 수 없는 오류: {msg}"

    /// 테스트 결과 요약 출력
    let printResults (results: TestExecutionResult list) =
        printfn "=========================================="
        printfn "프로토콜별 테스트 결과 요약"
        printfn "=========================================="
        printfn "%-15s %8s %10s %s" "프로토콜" "상태" "실행시간" "상세"
        printfn "------------------------------------------"
        
        let mutable totalTime = 0.0
        let mutable passedCount = 0
        let mutable failedCount = 0
        
        for result in results do
            let statusIcon = 
                match result.Status with
                | Passed -> 
                    passedCount <- passedCount + 1
                    "✓"
                | Failed -> 
                    failedCount <- failedCount + 1  
                    "✗"
                | Skipped -> "⊘"
            
            let timeStr = $"{result.Duration.TotalSeconds:F3}초"
            totalTime <- totalTime + result.Duration.TotalSeconds
            
            let detail = 
                match result.Status with
                | Failed when result.ErrorMessage.IsSome -> $"에러: {result.ErrorMessage.Value.Substring(0, min 30 result.ErrorMessage.Value.Length)}"
                | _ when result.HexDumps.Length > 0 -> $"헥사덤프 {result.HexDumps.Length}개"
                | _ -> ""
            
            printfn "%-15s %8s %10s %s" result.ProtocolName statusIcon timeStr detail
        
        printfn "------------------------------------------"
        printfn "%-15s %8s %10s" "총계" $"{passedCount}✓ {failedCount}✗" $"{totalTime:F3}초"
        printfn "=========================================="

    /// 모든 프로토콜 테스트 실행
    let runAllProtocolTests() =
        let protocolProjects = [
            ("LS Electric", "/mnt/c/ds/dsev2cpu/src/protocol/lselectric/Ev2.LsProtocol.Tests")
            ("Siemens S7", "/mnt/c/ds/dsev2cpu/src/protocol/siemens/Ev2.S7Protocol.Tests") 
            ("Mitsubishi MX", "/mnt/c/ds/dsev2cpu/src/protocol/mitsubishi/Ev2.MxProtocol.Tests")
            // AB는 마지막에 추가
        ]

        let results = ResizeArray<TestExecutionResult>()
        
        printfn "=== 자동화된 프로토콜 테스트 실행 시작 ==="
        printfn ""

        for (protocolName, projectPath) in protocolProjects do
            printfn $"[{protocolName}] 테스트 실행 중..."
            try
                let result = runProtocolTests protocolName projectPath
                results.Add(result)
                
                match result.Status with
                | Passed -> printfn $"[{protocolName}] ✓ 성공 - {result.Duration.TotalSeconds:F3}초"
                | Failed -> 
                    printfn $"[{protocolName}] ✗ 실패 - {result.Duration.TotalSeconds:F3}초"
                    let pattern = TestOutputParser.detectFailurePattern result.StdOut
                    analyzeAndSuggestFix protocolName pattern result
                | Skipped -> printfn $"[{protocolName}] ⊘ 건너뜀"
                
            with ex ->
                printfn $"[{protocolName}] ⚠ 예외 발생: {ex.Message}"
                let errorResult = {
                    ProtocolName = protocolName
                    TestName = "Integration Tests"
                    Duration = TimeSpan.Zero
                    Status = Failed
                    StdOut = ""
                    ErrorMessage = Some ex.Message
                    StackTrace = Some ex.StackTrace
                    HexDumps = []
                    PerformanceMetrics = []
                }
                results.Add(errorResult)
            
            printfn ""

        printfn "=== 테스트 실행 완료 ==="
        printfn ""
        printResults (List.ofSeq results)
        
        results |> List.ofSeq
