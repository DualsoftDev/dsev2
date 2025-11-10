namespace Ev2.S7Protocol.Client

open System
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Protocol

/// <summary>
/// Siemens S7 프로토콜 클라이언트 with improved error handling and validation
/// </summary>
type S7Client(config: S7Config, ?packetLogger: PacketLogger) =
    let session = new SessionManager(config, ?packetLogger = packetLogger)
    let mutable packetsSent = 0L
    let mutable packetsReceived = 0L
    let mutable errorCount = 0L
    let mutable lastError : DateTime option = None
    let mutable lastErrorMessage : string option = None
    let mutable connectionStartTime : DateTime option = None
    
    let updateStatsSuccess () =
        packetsReceived <- packetsReceived + 1L
    
    let updateStatsError (msg: string) =
        errorCount <- errorCount + 1L
        lastError <- Some DateTime.UtcNow
        lastErrorMessage <- Some msg
    
    /// <summary>
    /// Parse M area bit address like "M0.0"
    /// Returns: (byteAddress, bitOffset) or Error
    /// </summary>
    let parseBitAddress (address: string) =
        if address.StartsWith("M") && address.Contains(".") then
            let parts = address.Substring(1).Split('.')
            if parts.Length = 2 then
                match Int32.TryParse(parts.[0]), Int32.TryParse(parts.[1]) with
                | (true, byteAddr), (true, bitAddr) when bitAddr >= 0 && bitAddr <= 7 ->
                    Ok (byteAddr, bitAddr)
                | (true, _), (true, bitAddr) ->
                    Error $"Bit offset must be 0-7, got {bitAddr}"
                | _ -> 
                    Error "Invalid address format"
            else
                Error "Address must be in format 'M<byte>.<bit>'"
        else
            Error "Only M area supported (format: M0.0)"
    
    /// <summary>
    /// Parse byte address like "MB1"
    /// Returns: byteAddress or Error
    /// </summary>
    let parseByteAddress (address: string) (prefix: string) =
        if address.StartsWith(prefix) then
            match Int32.TryParse(address.Substring(prefix.Length)) with
            | true, byteAddr when byteAddr >= 0 -> Ok byteAddr
            | true, byteAddr -> Error $"Address cannot be negative: {byteAddr}"
            | _ -> Error $"Invalid address format: {address}"
        else
            Error $"Only {prefix} area supported"
    
    member this.Connect() = 
        match session.Connect() with
        | Ok () ->
            connectionStartTime <- Some DateTime.UtcNow
            updateStatsSuccess ()
            Ok ()
        | Error err ->
            updateStatsError err
            Error err
    
    member this.Disconnect() = 
        session.Disconnect()
        connectionStartTime <- None
    
    member this.IsConnected = session.IsConnected
    
    /// Read bit from M area
    member this.ReadBit(address: string) =
        match parseBitAddress address with
        | Ok (byteAddr, bitAddr) ->
            let result = session.ReadBit(DataArea.Merker, 0, byteAddr, bitAddr)
            match result with
            | Ok _ -> updateStatsSuccess ()
            | Error err -> updateStatsError err
            result
        | Error err -> 
            updateStatsError err
            Error err
    
    /// Write bit to M area
    member this.WriteBit(address: string, value: bool) =
        match parseBitAddress address with
        | Ok (byteAddr, bitAddr) ->
            let result = session.WriteBit(DataArea.Merker, 0, byteAddr, bitAddr, value)
            match result with
            | Ok _ -> updateStatsSuccess ()
            | Error err -> updateStatsError err
            result
        | Error err ->
            updateStatsError err
            Error err
    
    /// Read byte from M area
    member this.ReadByte(address: string) =
        match parseByteAddress address "MB" with
        | Ok byteAddr ->
            match session.ReadBytes(DataArea.Merker, 0, byteAddr, 1) with
            | Ok data when data.Length > 0 -> 
                updateStatsSuccess ()
                Ok data.[0]
            | Ok _ -> 
                updateStatsError "No data received"
                Error "No data received"
            | Error err -> 
                updateStatsError err
                Error err
        | Error err ->
            updateStatsError err
            Error err
    
    /// Write byte to M area
    member this.WriteByte(address: string, value: byte) =
        match parseByteAddress address "MB" with
        | Ok byteAddr ->
            let result = session.WriteBytes(DataArea.Merker, 0, byteAddr, [| value |])
            match result with
            | Ok _ -> updateStatsSuccess ()
            | Error err -> updateStatsError err
            result
        | Error err ->
            updateStatsError err
            Error err
    
    /// Read word (Int16) from M area
    member this.ReadInt16(address: string) =
        match parseByteAddress address "MW" with
        | Ok byteAddr ->
            match session.ReadBytes(DataArea.Merker, 0, byteAddr, 2) with
            | Ok data when data.Length = 2 ->
                let value = (int16 data.[0] <<< 8) ||| int16 data.[1]
                updateStatsSuccess ()
                Ok value
            | Ok _ -> 
                updateStatsError "Invalid data length"
                Error "Invalid data length"
            | Error err -> 
                updateStatsError err
                Error err
        | Error err ->
            updateStatsError err
            Error err
    
    /// Write word (Int16) to M area
    member this.WriteInt16(address: string, value: int16) =
        match parseByteAddress address "MW" with
        | Ok byteAddr ->
            let data = [| byte (value >>> 8); byte value |]
            let result = session.WriteBytes(DataArea.Merker, 0, byteAddr, data)
            match result with
            | Ok _ -> updateStatsSuccess ()
            | Error err -> updateStatsError err
            result
        | Error err ->
            updateStatsError err
            Error err
    
    /// Read DWord (Int32) from M area
    member this.ReadInt32(address: string) =
        match parseByteAddress address "MD" with
        | Ok byteAddr ->
            match session.ReadBytes(DataArea.Merker, 0, byteAddr, 4) with
            | Ok data when data.Length = 4 ->
                let value = 
                    (int32 data.[0] <<< 24) ||| 
                    (int32 data.[1] <<< 16) ||| 
                    (int32 data.[2] <<< 8) ||| 
                    int32 data.[3]
                updateStatsSuccess ()
                Ok value
            | Ok _ -> 
                updateStatsError "Invalid data length"
                Error "Invalid data length"
            | Error err -> 
                updateStatsError err
                Error err
        | Error err ->
            updateStatsError err
            Error err
    
    /// Write DWord (Int32) to M area
    member this.WriteInt32(address: string, value: int32) =
        match parseByteAddress address "MD" with
        | Ok byteAddr ->
            let data = [| 
                byte (value >>> 24)
                byte (value >>> 16)
                byte (value >>> 8)
                byte value 
            |]
            let result = session.WriteBytes(DataArea.Merker, 0, byteAddr, data)
            match result with
            | Ok _ -> updateStatsSuccess ()
            | Error err -> updateStatsError err
            result
        | Error err ->
            updateStatsError err
            Error err
    
    /// Read Real (Float32) from M area
    member this.ReadReal(address: string) =
        match parseByteAddress address "MD" with
        | Ok byteAddr ->
            match session.ReadBytes(DataArea.Merker, 0, byteAddr, 4) with
            | Ok data when data.Length = 4 ->
                let bytes = [| data.[3]; data.[2]; data.[1]; data.[0] |]
                let value = BitConverter.ToSingle(bytes, 0)
                updateStatsSuccess ()
                Ok value
            | Ok _ -> 
                updateStatsError "Invalid data length"
                Error "Invalid data length"
            | Error err -> 
                updateStatsError err
                Error err
        | Error err ->
            updateStatsError err
            Error err
    
    /// Write Real (Float32) to M area
    member this.WriteReal(address: string, value: float32) =
        match parseByteAddress address "MD" with
        | Ok byteAddr ->
            let bytes = BitConverter.GetBytes(value)
            let data = [| bytes.[3]; bytes.[2]; bytes.[1]; bytes.[0] |]
            let result = session.WriteBytes(DataArea.Merker, 0, byteAddr, data)
            match result with
            | Ok _ -> updateStatsSuccess ()
            | Error err -> updateStatsError err
            result
        | Error err ->
            updateStatsError err
            Error err
    
    /// Read bytes from PLC with validation
    member this.ReadBytes(area: DataArea, db: int, start: int, count: int) =
        if count <= 0 then
            updateStatsError "Count must be positive"
            Error "Count must be positive"
        elif not (Constants.DataLimits.isValidAddress area db start count) then
            updateStatsError "Address out of range"
            Error "Address out of range"
        else
            let result = session.ReadBytes(area, db, start, count)
            match result with
            | Ok _ -> updateStatsSuccess ()
            | Error err -> updateStatsError err
            result
    
    /// Write bytes to PLC with validation
    member this.WriteBytes(area: DataArea, db: int, start: int, data: byte[]) =
        if data.Length = 0 then
            updateStatsError "Data cannot be empty"
            Error "Data cannot be empty"
        elif not (Constants.DataLimits.isValidAddress area db start data.Length) then
            updateStatsError "Address out of range"
            Error "Address out of range"
        else
            let result = session.WriteBytes(area, db, start, data)
            match result with
            | Ok _ -> updateStatsSuccess ()
            | Error err -> updateStatsError err
            result
    
    /// Read from DB area
    member this.ReadDB(db: int, start: int, count: int) =
        this.ReadBytes(DataArea.DataBlock, db, start, count)
    
    /// Write to DB area
    member this.WriteDB(db: int, start: int, data: byte[]) =
        this.WriteBytes(DataArea.DataBlock, db, start, data)
    
    /// Read from Merker area
    member this.ReadMerker(start: int, count: int) =
        this.ReadBytes(DataArea.Merker, 0, start, count)
    
    /// Read from Input area
    member this.ReadInput(start: int, count: int) =
        this.ReadBytes(DataArea.ProcessInput, 0, start, count)
    
    /// Read from Output area
    member this.ReadOutput(start: int, count: int) =
        this.ReadBytes(DataArea.ProcessOutput, 0, start, count)
    
    /// Get statistics
    member this.GetStatistics() : CommunicationStats = 
        let total = float (packetsReceived + errorCount)
        let successRate = if total > 0.0 then (float packetsReceived / total) * 100.0 else 100.0
        let uptime = 
            match connectionStartTime with
            | Some startTime -> DateTime.UtcNow - startTime
            | None -> TimeSpan.Zero
        
        {
            PacketsSent = packetsSent
            PacketsReceived = packetsReceived
            BytesSent = 0L
            BytesReceived = 0L
            ErrorCount = errorCount
            LastError = lastError
            LastErrorMessage = lastErrorMessage
            AverageResponseTime = 0.0
            MinResponseTime = 0.0
            MaxResponseTime = 0.0
            ConnectionUptime = uptime
            SuccessRate = successRate
        }
    
    /// Get negotiated PDU size
    member this.PDUSize = session.PDUSize
    
    interface IDisposable with
        member this.Dispose() = 
            this.Disconnect()
            (session :> IDisposable).Dispose()
