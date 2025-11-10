namespace DSPLCServer.Console

open System
open System.Threading
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open DSPLCServer.Common
open DSPLCServer.Core

/// 콘솔 인터페이스 - 사용자 명령을 처리하는 서비스 (동기 버전)
type ConsoleInterface(serviceProvider: IServiceProvider, logger: ILogger<ConsoleInterface>) =
    
    let mutable isRunning = false
    let mutable consoleThread = None
    
    /// 프롬프트 출력
    member private this.ShowPrompt() =
        Console.ForegroundColor <- ConsoleColor.Green
        Console.Write("DSPLCServer> ")
        Console.ResetColor()
    
    /// 도움말 출력
    member private this.ShowHelp() =
        Console.WriteLine("=== Available Commands ===")
        Console.WriteLine("help, h, ?           - Show this help message")
        Console.WriteLine("status, st           - Show server status")
        Console.WriteLine("list, ls             - List all configured PLCs")
        Console.WriteLine("scan [plc-id]        - Trigger immediate scan for specific PLC or all PLCs")
        Console.WriteLine("stats                - Show data logger statistics")
        Console.WriteLine("flush                - Force flush data logger buffer")
        Console.WriteLine("cleanup [days]       - Cleanup old data (default: 30 days)")
        Console.WriteLine("exit, quit, q        - Exit the application")
        Console.WriteLine("")
    
    /// 서버 상태 출력
    member private this.ShowStatus() =
        try
            let scanScheduler = serviceProvider.GetRequiredService<ScanScheduler>()
            let dataLogger: DataLogger = serviceProvider.GetRequiredService<DataLogger>()
            
            let activePLCs = scanScheduler.GetActivePLCs()
            let stats = dataLogger.GetStatistics()
            
            Console.WriteLine("=== Server Status ===")
            Console.WriteLine(sprintf "Active PLCs: %d" activePLCs.Length)
            Console.WriteLine(sprintf "Data Buffer Size: %d/%d" stats.BufferSize stats.MaxBufferSize)
            Console.WriteLine(sprintf "Buffer Utilization: %.1f%%" stats.BufferUtilization)
            Console.WriteLine(sprintf "Total Points Logged: %d" stats.TotalPointsLogged)
            Console.WriteLine(sprintf "Total Batches Processed: %d" stats.TotalBatchesProcessed)
            Console.WriteLine(sprintf "Last Flush Time: %s" (stats.LastFlushTime.ToString("yyyy-MM-dd HH:mm:ss")))
            
            if activePLCs.Length > 0 then
                Console.WriteLine("PLC Connection Status:")
                for plc in activePLCs do
                    let status = plc.ConnectionState
                    let statusText = 
                        match status.Status with
                        | Connected -> "Connected"
                        | Disconnected -> "Disconnected"
                        | Connecting -> "Connecting"
                        | Error _ -> "Error"
                    Console.WriteLine(sprintf "  %s (%A): %s" plc.PlcId plc.Vendor statusText)
            Console.WriteLine("")
        with
        | ex ->
            Console.WriteLine(sprintf "Error retrieving status: %s" ex.Message)
            logger.LogError(ex, "Error executing status command")
    
    /// PLC 목록 출력
    member private this.ListPLCs() =
        try
            let scanScheduler = serviceProvider.GetRequiredService<ScanScheduler>()
            let activePLCs = scanScheduler.GetActivePLCs()
            
            Console.WriteLine(sprintf "=== Configured PLCs (%d) ===" activePLCs.Length)
            for plc in activePLCs do
                let status = plc.ConnectionState
                let statusText = 
                    match status.Status with
                    | Connected -> "ㅇ Connected"
                    | Disconnected -> "✗ Disconnected"
                    | Connecting -> "⏳ Connecting"
                    | Error _ -> "❌ Error"
                
                Console.WriteLine(sprintf "  %s (%A): %s" plc.PlcId plc.Vendor statusText)
                match status.Status with
                | Error _ when status.LastErrorMessage.IsSome ->
                    Console.WriteLine(sprintf "    Error: %s" status.LastErrorMessage.Value)
                | _ -> ()
            Console.WriteLine("")
        with
        | ex ->
            Console.WriteLine(sprintf "Error listing PLCs: %s" ex.Message)
            logger.LogError(ex, "Error executing list PLCs command")
    
    /// PLC 스캔 트리거
    member private this.TriggerScan(plcIdOpt: string option) =
        try
            let scanScheduler = serviceProvider.GetRequiredService<ScanScheduler>()
            
            match plcIdOpt with
            | Some plcId ->
                Console.WriteLine(sprintf "Triggering scan for PLC: %s" plcId)
                scanScheduler.ScanPLCById(plcId)
                Console.WriteLine("Scan completed")
            | None ->
                Console.WriteLine("Triggering scan for all PLCs")
                scanScheduler.ScanAllPLCs()
                Console.WriteLine("All scans completed")
        with
        | ex ->
            Console.WriteLine(sprintf "Error during scan: %s" ex.Message)
            logger.LogError(ex, "Error executing scan command")
    
    /// 데이터 로거 통계 출력
    member private this.ShowDataLoggerStats() =
        try
            let dataLogger: DataLogger = serviceProvider.GetRequiredService<DataLogger>()
            let stats = dataLogger.GetStatistics()
            
            Console.WriteLine("=== Data Logger Statistics ===")
            Console.WriteLine(sprintf "Buffer Size: %d / %d" stats.BufferSize stats.MaxBufferSize)
            Console.WriteLine(sprintf "Buffer Utilization: %.1f%%" stats.BufferUtilization)
            Console.WriteLine(sprintf "Batch Size: %d" stats.BatchSize)
            Console.WriteLine(sprintf "Flush Interval: %.0f seconds" stats.FlushInterval.TotalSeconds)
            Console.WriteLine(sprintf "Total Points Logged: %d" stats.TotalPointsLogged)
            Console.WriteLine(sprintf "Total Batches Processed: %d" stats.TotalBatchesProcessed)
            Console.WriteLine(sprintf "Last Flush Time: %s UTC" (stats.LastFlushTime.ToString("yyyy-MM-dd HH:mm:ss")))
            Console.WriteLine("")
        with
        | ex ->
            Console.WriteLine(sprintf "Error retrieving statistics: %s" ex.Message)
            logger.LogError(ex, "Error executing stats command")
    
    /// 데이터 로거 강제 플러시
    member private this.ForceFlush() =
        try
            let dataLogger: DataLogger = serviceProvider.GetRequiredService<DataLogger>()
            
            Console.WriteLine("Forcing data logger buffer flush...")
            let flushedCount = dataLogger.FlushNow()
            
            if flushedCount > 0 then
                Console.ForegroundColor <- ConsoleColor.Green
                Console.WriteLine(sprintf "Successfully flushed %d data points to database" flushedCount)
                Console.ResetColor()
            else
                Console.WriteLine("No data points to flush")
        with
        | ex ->
            Console.ForegroundColor <- ConsoleColor.Red
            Console.WriteLine(sprintf "Error during flush: %s" ex.Message)
            Console.ResetColor()
            logger.LogError(ex, "Error executing flush command")
    
    /// 오래된 데이터 정리
    member private this.CleanupOldData(daysOpt: int option) =
        try
            let dataLogger: DataLogger = serviceProvider.GetRequiredService<DataLogger>()
            let days = daysOpt |> Option.defaultValue 30
            
            Console.WriteLine(sprintf "Cleaning up data older than %d days..." days)
            let deletedCount = dataLogger.CleanupOldData(days)
            
            if deletedCount > 0L then
                Console.ForegroundColor <- ConsoleColor.Green
                Console.WriteLine(sprintf "Successfully deleted %d old records" deletedCount)
                Console.ResetColor()
            else
                Console.WriteLine("No old records found to delete")
        with
        | ex ->
            Console.ForegroundColor <- ConsoleColor.Red
            Console.WriteLine(sprintf "Error during cleanup: %s" ex.Message)
            Console.ResetColor()
            logger.LogError(ex, "Error executing cleanup command")
    
    /// 명령 처리
    member private this.ProcessCommand(command: string) =
        let parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        if parts.Length = 0 then 
            ()
        else
            let cmd = parts.[0].ToLowerInvariant()
            let args = if parts.Length > 1 then Some parts.[1] else None
            
            match cmd with
            | "help" | "h" | "?" -> 
                this.ShowHelp()
                
            | "status" | "st" ->
                this.ShowStatus()
                
            | "list" | "ls" ->
                this.ListPLCs()
                
            | "scan" ->
                this.TriggerScan(args)
                
            | "stats" ->
                this.ShowDataLoggerStats()
                
            | "flush" ->
                this.ForceFlush()
                
            | "cleanup" ->
                let days = 
                    args |> Option.bind (fun s -> 
                        match Int32.TryParse(s) with 
                        | (true, d) -> Some d 
                        | _ -> None)
                this.CleanupOldData(days)
                
            | "exit" | "quit" | "q" ->
                Console.WriteLine("Shutting down server...")
                isRunning <- false
                
            | "" -> 
                () // 빈 명령 무시
                
            | _ ->
                Console.ForegroundColor <- ConsoleColor.Yellow
                Console.WriteLine(sprintf "Unknown command: %s" cmd)
                Console.ResetColor()
                Console.WriteLine("Type 'help' for available commands")
    
    /// 콘솔 루프 실행
    member private this.ConsoleLoop() =
        logger.LogInformation("Console interface started")
        
        // 시작 메시지 출력
        Console.WriteLine("=== DS PLC Server ===")
        Console.WriteLine("Type 'help' for available commands")
        Console.WriteLine("")
        
        try
            while isRunning do
                this.ShowPrompt()
                
                let input = Console.ReadLine()
                if input <> null then
                    let command = input.Trim()
                    if not (String.IsNullOrEmpty(command)) then
                        this.ProcessCommand(command)
                        
        with
        | ex ->
            logger.LogError(ex, "Error in console interface")
        
        Console.WriteLine("Console interface stopped")
        logger.LogInformation("Console interface stopped")
    
    /// 콘솔 인터페이스 시작
    member this.Start() =
        if not isRunning then
            isRunning <- true
            
            let thread = new Thread(this.ConsoleLoop)
            thread.IsBackground <- false  // 메인 스레드로 유지
            thread.Start()
            consoleThread <- Some thread
    
    /// 콘솔 인터페이스 중지
    member this.Stop() =
        if isRunning then
            isRunning <- false
            
            match consoleThread with
            | Some thread -> 
                if thread.IsAlive then
                    thread.Join(5000) |> ignore
            | None -> ()
    
    interface IDisposable with
        member this.Dispose() =
            this.Stop()