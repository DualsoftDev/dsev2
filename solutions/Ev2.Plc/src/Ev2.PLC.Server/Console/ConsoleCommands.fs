namespace DSPLCServer.Console

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open DSPLCServer.Common
open DSPLCServer.Database
open DSPLCServer.Core
open DSPLCServer.PLC

/// 콘솔 명령 - 파싱된 사용자 명령을 나타냄
type ConsoleCommand =
    | Help
    | Status
    | ListPLCs
    | ScanPLC of plcId: string option
    | ShowStats
    | FlushBuffer
    | CleanupData of days: int option
    | ConnectPLC of plcId: string
    | DisconnectPLC of plcId: string
    | ShowConfig of plcId: string option
    | Exit
    | Unknown of command: string

/// 콘솔 명령 핸들러 - 명령 파싱 및 실행을 담당
type ConsoleCommandHandler(serviceProvider: IServiceProvider, logger: ILogger<ConsoleCommandHandler>) =
    
    /// 명령 문자열을 ConsoleCommand로 파싱
    member this.ParseCommand(input: string) : ConsoleCommand =
        let parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        if parts.Length = 0 then Unknown("")
        else
            let cmd = parts.[0].ToLowerInvariant()
            let args = if parts.Length > 1 then parts |> Array.skip 1 else [||]
            
            match cmd with
            | "help" | "h" | "?" -> Help
            | "status" | "st" -> Status
            | "list" | "ls" | "plcs" -> ListPLCs
            | "scan" -> 
                let plcId = if args.Length > 0 then Some args.[0] else None
                ScanPLC plcId
            | "stats" -> ShowStats
            | "flush" -> FlushBuffer
            | "cleanup" ->
                let days = 
                    if args.Length > 0 then
                        match Int32.TryParse(args.[0]) with
                        | (true, d) when d > 0 -> Some d
                        | _ -> None
                    else None
                CleanupData days
            | "connect" ->
                if args.Length > 0 then ConnectPLC args.[0]
                else Unknown("connect requires PLC ID")
            | "disconnect" ->
                if args.Length > 0 then DisconnectPLC args.[0]
                else Unknown("disconnect requires PLC ID")
            | "config" ->
                let plcId = if args.Length > 0 then Some args.[0] else None
                ShowConfig plcId
            | "exit" | "quit" | "q" -> Exit
            | _ -> Unknown cmd
    
    /// Help 명령 실행
    member private this.ExecuteHelp() = task {
        let helpText = """
=== DS PLC Server Commands ===

General Commands:
  help, h, ?           - Show this help message
  status, st           - Show server status and active PLCs
  list, ls, plcs       - List all configured PLCs with connection status
  exit, quit, q        - Exit the application

PLC Management:
  scan [plc-id]        - Trigger immediate scan for specific PLC or all PLCs
  connect <plc-id>     - Connect to specific PLC
  disconnect <plc-id>  - Disconnect from specific PLC
  config [plc-id]      - Show PLC configuration

Data Management:
  stats               - Show data logger statistics
  flush               - Force flush data logger buffer to database
  cleanup [days]      - Cleanup old data (default: 30 days)

Examples:
  scan                - Scan all PLCs
  scan PLC001         - Scan specific PLC
  connect PLC001      - Connect to PLC001
  cleanup 7           - Delete data older than 7 days

Notes:
- Commands are case-insensitive
- Optional parameters are shown in [brackets]
- Required parameters are shown in <brackets>
"""
        Console.WriteLine(helpText)
        return true
    }
    
    /// 명령 실행
    member this.ExecuteCommand(command: ConsoleCommand) : Task<bool> = task {
        try
            match command with
            | Help -> return! this.ExecuteHelp()
            
            | Status -> return! this.ExecuteStatus()
            
            | ListPLCs -> return! this.ExecuteListPLCs()
            | ScanPLC plcId -> return! this.ExecuteScanPLC(plcId)
            | ShowStats -> return! this.ExecuteShowStats()
            | FlushBuffer -> return! this.ExecuteFlushBuffer()
            | CleanupData days -> return! this.ExecuteCleanupData(days)
            | ShowConfig plcId -> return! this.ExecuteShowConfig(plcId)
            | ConnectPLC plcId -> 
                Console.WriteLine($"Connect command not yet implemented for PLC: {plcId}")
                return true
            | DisconnectPLC plcId ->
                Console.WriteLine($"Disconnect command not yet implemented for PLC: {plcId}")
                return true
            | Exit -> 
                Console.WriteLine("Goodbye!")
                return false
            | Unknown cmd ->
                if not (String.IsNullOrWhiteSpace(cmd)) then
                    Console.ForegroundColor <- ConsoleColor.Yellow
                    Console.WriteLine($"Unknown command: {cmd}")
                    Console.ResetColor()
                    Console.WriteLine("Type 'help' for available commands")
                return true
                
        with
        | ex ->
            Console.ForegroundColor <- ConsoleColor.Red
            Console.WriteLine($"Unexpected error executing command: {ex.Message}")
            Console.ResetColor()
            logger.LogError(ex, "Unexpected error executing command: {Command}", command)
            return true
    }
    
    // 나머지 실행 메서드들은 ConsoleInterface.fs의 구현을 여기로 이동하여 구현할 수 있습니다
    member private this.ExecuteStatus() = task { return true } // 임시 구현
    member private this.ExecuteListPLCs() = task { return true } // 임시 구현
    member private this.ExecuteScanPLC(plcId: string option) = task { return true } // 임시 구현
    member private this.ExecuteShowStats() = task { return true } // 임시 구현
    member private this.ExecuteFlushBuffer() = task { return true } // 임시 구현
    member private this.ExecuteCleanupData(days: int option) = task { return true } // 임시 구현
    member private this.ExecuteShowConfig(plcId: string option) = task { return true } // 임시 구현