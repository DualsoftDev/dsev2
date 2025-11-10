namespace ProtocolTestHelper

open System
open System.IO
open System.Text

/// Generic packet logging system for protocol debugging
module PacketLogger =

    /// Packet direction enumeration
    type PacketDirection = 
        | Request = 0
        | Response = 1

    /// Packet log entry
    type PacketLogEntry = {
        Timestamp: DateTime
        Direction: PacketDirection
        ProtocolName: string
        Host: string
        Port: int
        Description: string
        Data: byte[]
        Properties: Map<string, string>
    }

    /// Protocol-specific frame analyzer interface
    type IFrameAnalyzer =
        abstract member AnalyzeFrame: byte[] -> string
        abstract member ProtocolName: string

    /// Enable/disable packet logging globally
    let mutable LoggingEnabled = 
        Environment.GetEnvironmentVariable("PROTOCOL_PACKET_LOGGING") = "true"
    
    /// Base log directory
    let BaseLogDirectory = 
        let baseDir = Environment.GetEnvironmentVariable("PROTOCOL_LOG_DIR") 
        if String.IsNullOrEmpty(baseDir) then
            Path.Combine(Environment.CurrentDirectory, "protocol_logs")
        else baseDir
    
    /// Get protocol-specific log directory
    let getLogDirectory (protocolName: string) =
        Path.Combine(BaseLogDirectory, protocolName.ToLowerInvariant())
    
    /// Ensure log directory exists
    let ensureLogDirectory (protocolName: string) =
        let logDir = getLogDirectory protocolName
        if not (Directory.Exists(logDir)) then
            Directory.CreateDirectory(logDir) |> ignore
        logDir
    
    /// Enhanced hex dump with 8-byte address alignment
    let createEnhancedHexDump (bytes: byte[]) =
        let sb = StringBuilder()
        let mutable offset = 0
        while offset < bytes.Length do
            // Address (8 hex digits)
            sb.AppendFormat("{0:X8}: ", offset) |> ignore
            
            // Hex bytes (16 per line, grouped by 8)
            let endIndex = min (offset + 15) (bytes.Length - 1)
            for i in offset .. endIndex do
                sb.AppendFormat("{0:X2}", bytes.[i]) |> ignore
                if (i - offset + 1) % 8 = 0 then
                    sb.Append("  ") |> ignore  // Double space every 8 bytes
                else
                    sb.Append(" ") |> ignore   // Single space between bytes
            
            // Padding for partial lines
            let bytesInLine = endIndex - offset + 1
            if bytesInLine < 16 then
                let padding = 16 - bytesInLine
                for i in 1 .. padding do
                    sb.Append("   ") |> ignore
                    if (bytesInLine + i) % 8 = 0 then
                        sb.Append(" ") |> ignore
            
            // ASCII representation
            sb.Append("| ") |> ignore
            for i in offset .. endIndex do
                let c = char bytes.[i]
                if c >= ' ' && c <= '~' then
                    sb.Append(c) |> ignore
                else
                    sb.Append('.') |> ignore
            
            sb.AppendLine() |> ignore
            offset <- offset + 16
        
        sb.ToString()
    
    /// Log packet with optional frame analysis
    let logPacket (entry: PacketLogEntry) (analyzer: IFrameAnalyzer option) =
        if LoggingEnabled then
            try
                let logDir = ensureLogDirectory entry.ProtocolName
                let timestamp = entry.Timestamp.ToString("yyyy-MM-dd_HH-mm-ss-fff")
                let direction = entry.Direction.ToString().ToUpper()
                let protocolLower = entry.ProtocolName.ToLower()
                let filename = sprintf "%s_packet_%s_%s.log" protocolLower timestamp direction
                let filepath = Path.Combine(logDir, filename)
                
                use writer = new StreamWriter(filepath)
                writer.WriteLine(sprintf "=== %s Packet Log ===" (entry.ProtocolName.ToUpper()))
                writer.WriteLine(sprintf "Timestamp: %s" (entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")))
                writer.WriteLine(sprintf "Direction: %s" direction)
                writer.WriteLine(sprintf "Host: %s:%d" entry.Host entry.Port)
                writer.WriteLine(sprintf "Description: %s" entry.Description)
                writer.WriteLine(sprintf "Length: %d bytes" entry.Data.Length)
                writer.WriteLine()
                
                // Write properties if any
                if not entry.Properties.IsEmpty then
                    writer.WriteLine("Properties:")
                    for kvp in entry.Properties do
                        writer.WriteLine(sprintf "  %s: %s" kvp.Key kvp.Value)
                    writer.WriteLine()
                
                // Protocol-specific frame analysis
                match analyzer with
                | Some frameAnalyzer ->
                    writer.WriteLine("Frame Analysis:")
                    writer.WriteLine(frameAnalyzer.AnalyzeFrame entry.Data)
                    writer.WriteLine()
                | None -> ()
                
                // Enhanced hex dump
                writer.WriteLine("Enhanced Hex Dump:")
                writer.WriteLine(createEnhancedHexDump entry.Data)
                writer.WriteLine()
                
                // Raw bytes
                writer.WriteLine("Raw Bytes:")
                writer.WriteLine(String.Join(" ", entry.Data |> Array.map (fun b -> sprintf "%02X" b)))
                writer.WriteLine()
                
                // Simple hex dump (for compatibility)
                writer.WriteLine("Standard Hex Dump:")
                writer.WriteLine(HexDump.format entry.Data)
                
                printfn "[PacketLogger] Logged: %s" filename
            with ex ->
                printfn "[PacketLogger] Failed to log packet: %s" ex.Message
    
    /// Log request packet
    let logRequest (protocolName: string) (host: string) (port: int) (data: byte[]) (description: string) (properties: Map<string, string>) (analyzer: IFrameAnalyzer option) =
        let entry = {
            Timestamp = DateTime.Now
            Direction = PacketDirection.Request
            ProtocolName = protocolName
            Host = host
            Port = port
            Description = description
            Data = data
            Properties = properties
        }
        logPacket entry analyzer
    
    /// Log response packet
    let logResponse (protocolName: string) (host: string) (port: int) (data: byte[]) (description: string) (properties: Map<string, string>) (analyzer: IFrameAnalyzer option) =
        let entry = {
            Timestamp = DateTime.Now
            Direction = PacketDirection.Response
            ProtocolName = protocolName
            Host = host
            Port = port
            Description = description
            Data = data
            Properties = properties
        }
        logPacket entry analyzer
    
    /// Log error with context
    let logError (protocolName: string) (host: string) (port: int) (error: string) (context: string) (properties: Map<string, string>) =
        if LoggingEnabled then
            try
                let logDir = ensureLogDirectory protocolName
                let timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")
                let protocolLower = protocolName.ToLower()
                let filename = sprintf "%s_error_%s.log" protocolLower timestamp
                let filepath = Path.Combine(logDir, filename)
                
                use writer = new StreamWriter(filepath)
                writer.WriteLine(sprintf "=== %s Error Log ===" (protocolName.ToUpper()))
                writer.WriteLine(sprintf "Timestamp: %s" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")))
                writer.WriteLine(sprintf "Host: %s:%d" host port)
                writer.WriteLine(sprintf "Context: %s" context)
                writer.WriteLine(sprintf "Error: %s" error)
                writer.WriteLine()
                
                if not properties.IsEmpty then
                    writer.WriteLine("Properties:")
                    for kvp in properties do
                        writer.WriteLine(sprintf "  %s: %s" kvp.Key kvp.Value)
                    writer.WriteLine()
                
                printfn "[PacketLogger] Error logged: %s" filename
            with ex ->
                printfn "[PacketLogger] Failed to log error: %s" ex.Message
    
    /// Create operation tracker for sequential logging
    type OperationTracker(protocolName: string, host: string, port: int) =
        let mutable operationCounter = 0
        
        member _.NextOperation(description: string) =
            operationCounter <- operationCounter + 1
            sprintf "Op%d: %s" operationCounter description
        
        member this.LogRequest(data: byte[], description: string, ?properties: Map<string, string>, ?analyzer: IFrameAnalyzer) =
            let props = defaultArg properties Map.empty
            let fullDesc = this.NextOperation(description)
            logRequest protocolName host port data fullDesc props analyzer
            fullDesc
        
        member this.LogResponse(data: byte[], description: string, ?properties: Map<string, string>, ?analyzer: IFrameAnalyzer) =
            let props = defaultArg properties Map.empty
            logResponse protocolName host port data description props analyzer
        
        member this.LogError(error: string, context: string, ?properties: Map<string, string>) =
            let props = defaultArg properties Map.empty
            logError protocolName host port error context props
    
    /// Get all log files for a protocol
    let getLogFiles (protocolName: string) =
        try
            let logDir = getLogDirectory protocolName
            if Directory.Exists(logDir) then
                let protocolLower = protocolName.ToLower()
                Directory.GetFiles(logDir, sprintf "%s_*" protocolLower)
                |> Array.sortByDescending (fun f -> FileInfo(f).CreationTime)
            else
                [||]
        with ex ->
            printfn "[PacketLogger] Failed to get log files: %s" ex.Message
            [||]
    
    /// Clear old log files (keep only recent N files)
    let clearOldLogs (protocolName: string) (keepCount: int) =
        try
            let logFiles = getLogFiles protocolName
            if logFiles.Length > keepCount then
                let filesToDelete = logFiles |> Array.skip keepCount
                for file in filesToDelete do
                    File.Delete(file)
                printfn "[PacketLogger] Cleared %d old log files for %s" filesToDelete.Length protocolName
        with ex ->
            printfn "[PacketLogger] Failed to clear old logs: %s" ex.Message
    
    /// Summary of logged packets
    let getLogSummary (protocolName: string) =
        try
            let logFiles = getLogFiles protocolName
            let packetLogs = logFiles |> Array.filter (fun f -> f.Contains("_packet_"))
            let errorLogs = logFiles |> Array.filter (fun f -> f.Contains("_error_"))
            
            sprintf "%s Logs: %d packets, %d errors" protocolName packetLogs.Length errorLogs.Length
        with ex ->
            sprintf "Failed to get log summary: %s" ex.Message