namespace Ev2.AbProtocol.Client

open System
open System.Collections.Generic
open Ev2.AbProtocol.Core
open Ev2.AbProtocol.Protocol
open Ev2.AbProtocol

/// <summary>
///     High-level Allen-Bradley EtherNet/IP client that builds on the low-level session manager.
///     Provides rich read/write helpers, statistics and tag metadata caching.
/// </summary>
type PacketLogger = string -> byte[] -> int -> unit

type ABClient(config: ConnectionConfig, ?packetLogger: PacketLogger) =
    let session = new SessionManager(config, ?packetLogger = packetLogger)
    let mutable plcInfo: PlcInfo option = None
    let tagCache = Dictionary<string, TagInfo>()
    let stats = ref CommunicationStats.Empty
    
    let responseTimes = Queue<float>()
    let maxResponseSamples = 100
    let mutable totalRequests = 0L
    let mutable successfulRequests = 0L
    
    // Connected messaging state (reserved for future enhancements)
    let mutable useConnectedMessaging = false
    let mutable connectionId = 0u
    let mutable sequenceNumber = 0us
    
    // ========================================
    // Statistics helpers
    // ========================================
    
    let recordResponseTime (time: float) =
        responseTimes.Enqueue(time)
        if responseTimes.Count > maxResponseSamples then
            responseTimes.Dequeue() |> ignore
    
        let times = responseTimes.ToArray()
        stats := 
            { !stats with 
                AverageResponseTime = if times.Length > 0 then Array.average times else 0.0
                MinResponseTime = if times.Length > 0 then Array.min times else Double.MaxValue
                MaxResponseTime = if times.Length > 0 then Array.max times else 0.0 }

    let recordSuccess() =
        totalRequests <- totalRequests + 1L
        successfulRequests <- successfulRequests + 1L
        let successRate = 
            if totalRequests > 0L then
                float successfulRequests / float totalRequests * 100.0
            else 100.0
        stats := { !stats with SuccessRate = successRate }

    let recordError (error: AbProtocolError) =
        totalRequests <- totalRequests + 1L
        let successRate = 
            if totalRequests > 0L then
                float successfulRequests / float totalRequests * 100.0
            else 0.0
        stats := 
            { !stats with
                ErrorCount = (!stats).ErrorCount + 1L
                SuccessRate = successRate
                LastError = Some DateTime.UtcNow
                LastErrorMessage = Some error.Message }

    let recordPacketSent (bytes: int) =
        stats := 
            { !stats with 
                PacketsSent = (!stats).PacketsSent + 1L
                BytesSent = (!stats).BytesSent + int64 bytes }

    let recordPacketReceived (bytes: int) =
        stats := 
            { !stats with 
                PacketsReceived = (!stats).PacketsReceived + 1L
                BytesReceived = (!stats).BytesReceived + int64 bytes }
    
    // ========================================
    // Utility helpers
    // ========================================
    
    /// Treats path errors as recoverable by attempting a fallback read.
    let mapPathError err fallback =
        if ABClientUtil.isPathError err then fallback()
        else (err, None)
    
    // ========================================
    // Low-level read/write operations
    // ========================================
    
    /// Issues a single read request (internal helper).
    member private this.ReadTagRaw(tagName: string, count: int) =
        let startTime = DateTime.UtcNow
        let service = PacketBuilder.buildReadTagService tagName count
        recordPacketSent service.Length
        
        let sendResult = session.SendUnconnected(service)
        
        match sendResult with
        | (NoError, Some (buffer, length)) ->
            let responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds
            recordResponseTime responseTime
            recordPacketReceived length
            
            let (error, dataOpt) = PacketParser.parseReadTagResponse buffer length count
            match error with
            | NoError ->
                match dataOpt with
                | Some data ->
                    recordSuccess()
                    (NoError, Some data)
                | None ->
                    let err = UnknownError "No data returned"
                    recordError err
                    (err, None)
            | _ ->
                recordError error
                (error, dataOpt)
        | (error, _) ->
            recordError error
            (error, None)

    /// Issues a single write request (internal helper).
    member private this.WriteTagRaw(tagName: string, dataType: DataType, values: byte[]) =
        let startTime = DateTime.UtcNow
        let service = PacketBuilder.buildWriteTagService tagName dataType values
        recordPacketSent service.Length
        
        let sendResult = session.SendUnconnected(service)
        
        match sendResult with
        | (NoError, Some (buffer, length)) ->
            let responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds
            recordResponseTime responseTime
            recordPacketReceived length
            
            let error = PacketParser.parseWriteTagResponse buffer length
            match error with
            | NoError ->
                recordSuccess()
                NoError
            | _ ->
                recordError error
                error
        | (error, _) ->
            recordError error
            error
    
    /// Reads large arrays using chunked Multiple Service packets for CompactLogix controllers.
    member private this.ReadTagMultiple(tagName: string, dataType: DataType, elementCount: int) =
        let elementSize = dataType.ByteSize
        let maxBytesPerChunk = 480
        let maxElementsPerChunk = maxBytesPerChunk / elementSize
        
        let chunkValues = ResizeArray<obj>()
        let mutable error = NoError
        let mutable totalCollected = 0
        
        let elementCountOfValue (value: obj) =
            match value with
            | :? Array as arr -> arr.Length
            | _ -> 1
        
        while error = NoError && totalCollected < elementCount do
            let remaining = elementCount - totalCollected
            let chunkSize = min remaining maxElementsPerChunk
            let chunkTag = sprintf "%s[%d]" tagName totalCollected
            
            let (err, valueOpt) = this.ReadTagRaw(chunkTag, chunkSize)
            
            match err with
            | NoError ->
                match valueOpt with
                | Some value ->
                    let count = elementCountOfValue value
                    if count = 0 then
                        error <- UnknownError "Chunk returned no data"
                    else
                        chunkValues.Add(value)
                        totalCollected <- totalCollected + count
                | None ->
                    error <- UnknownError "No value in chunk"
            | _ ->
                error <- err
            
        
        match error with
        | NoError when totalCollected = elementCount ->
            let finalValue =
                match chunkValues.[0] with
                | :? (bool[]) -> chunkValues |> Seq.cast<bool[]> |> Array.concat |> box
                | :? (sbyte[]) -> chunkValues |> Seq.cast<sbyte[]> |> Array.concat |> box
                | :? (byte[]) -> chunkValues |> Seq.cast<byte[]> |> Array.concat |> box
                | :? (int16[]) -> chunkValues |> Seq.cast<int16[]> |> Array.concat |> box
                | :? (uint16[]) -> chunkValues |> Seq.cast<uint16[]> |> Array.concat |> box
                | :? (int32[]) -> chunkValues |> Seq.cast<int32[]> |> Array.concat |> box
                | :? (uint32[]) -> chunkValues |> Seq.cast<uint32[]> |> Array.concat |> box
                | :? (int64[]) -> chunkValues |> Seq.cast<int64[]> |> Array.concat |> box
                | :? (uint64[]) -> chunkValues |> Seq.cast<uint64[]> |> Array.concat |> box
                | :? (single[]) -> chunkValues |> Seq.cast<single[]> |> Array.concat |> box
                | :? (double[]) -> chunkValues |> Seq.cast<double[]> |> Array.concat |> box
                | value -> value
            
            recordSuccess()
            (NoError, Some finalValue)
        | NoError ->
            (UnknownError (sprintf "Incomplete: %d/%d elements" totalCollected elementCount), None)
        | _ ->
            (error, None)
    
    /// Writes large arrays using chunked Multiple Service packets.
    member private this.WriteTagMultiple(tagName: string, dataType: DataType, values: byte[]) =
        let elementSize = dataType.ByteSize
        let maxBytesPerChunk = 480
        let maxElementsPerChunk = maxBytesPerChunk / elementSize
    
        let totalElements = values.Length / elementSize
        let mutable error = NoError
        let mutable totalWritten = 0
        let mutable chunkIndex = 0
    
        printfn "[Multiple Write] %d elements, chunk size: %d (element: %d bytes)" 
            totalElements maxElementsPerChunk elementSize
    
        while error = NoError && totalWritten < totalElements do
            let remaining = totalElements - totalWritten
            let chunkSize = min remaining maxElementsPerChunk
            let byteOffset = totalWritten * elementSize
            let chunkBytes = chunkSize * elementSize
        
            let chunkTag = sprintf "%s[%d]" tagName totalWritten
            let chunkData = values.[byteOffset .. byteOffset + chunkBytes - 1]
        
            printfn "  Chunk %d: %s (%d elements)" (chunkIndex + 1) chunkTag chunkSize
        
            let err = this.WriteTagRaw(chunkTag, dataType, chunkData)
        
            match err with
            | NoError ->
                totalWritten <- totalWritten + chunkSize
                printfn "    OK Wrote %d elements" chunkSize
            | _ ->
                error <- err
        
            chunkIndex <- chunkIndex + 1
    
        error
    
    // ========================================
    // Public API surface
    // ========================================
    
    /// Establishes a CIP session with the PLC.
    member this.Connect() =
        let (error, handleOpt) = session.Connect()
        match error with
        | NoError ->
            match handleOpt with
            | Some handle ->
                this.GetPlcInfo() |> ignore
                (NoError, Some handle)
            | None ->
                let err = UnknownError "Session handle not returned"
                recordError err
                (err, None)
        | _ ->
            recordError error
            (error, None)
    
    /// Tears down the active PLC session.
    member this.Disconnect() = session.Disconnect()
    
    /// Queries identity information for the connected PLC.
    member this.GetPlcInfo() =
        let packet = PacketBuilder.buildListIdentity()
        recordPacketSent packet.Length
        
        let (error, resultOpt) = session.SendPacket(Constants.EIP.ListIdentity, [||])
        match error with
        | NoError ->
            match resultOpt with
            | Some (buffer, length) ->
                recordPacketReceived length
                
                let (error, identityOpt) = PacketParser.parseListIdentity buffer length
                match error with
                | NoError ->
                    match identityOpt with
                    | Some identity ->
                        let info = {
                            Vendor = "Rockwell Automation"
                            ProductType = sprintf "0x%04X" identity.DeviceType
                            ProductCode = int identity.ProductCode
                            ProductName = identity.ProductName
                            Revision = Version(int identity.MajorRevision, int identity.MinorRevision)
                            SerialNumber = identity.SerialNumber.ToString()
                            Status = 
                                match identity.State with
                                | 0x00uy -> Running
                                | 0x01uy -> ProgramMode
                                | 0x02uy -> Faulted
                                | _ -> Unknown
                            Identity = Some identity
                        }
                        plcInfo <- Some info
                        recordSuccess()
                        (NoError, Some info)
                    | None ->
                        let err = UnknownError "No identity data"
                        recordError err
                        (err, None)
                | _ ->
                    recordError error
                    (error, None)
            | None ->
                let err = UnknownError "No response"
                recordError err
                (err, None)
        | _ ->
            recordError error
            (error, None)
    
    /// <summary>
    ///     Retrieves the latest PLC tag catalog and refreshes the in-memory metadata cache.
    ///     The optional <paramref name="forceRefresh"/> flag is ignored-each call enumerates
    ///     the controller to avoid stale state persisting between test runs.
    /// </summary>
    member this.ListTags(?forceRefresh: bool) =
        ignore forceRefresh
        let (error, recordsOpt) = TagEnumerator.enumerateTags session
        match error with
        | NoError ->
            match recordsOpt with
            | Some records ->
                tagCache.Clear()
                let infos =
                    records
                    |> Array.map (fun record ->
                        let info = TagEnumerator.toTagInfo record
                        tagCache.[info.Name] <- info
                        info)
                (NoError, Some infos)
            | None ->
                (UnknownError "No tags returned", None)
        | _ ->
            (error, None)
    
    /// <summary>
    ///     Reads a PLC tag with an explicit <see cref="DataType"/> hint.  This overload keeps the
    ///     low-level parsing logic simple and removes the need for additional round trips to
    ///     discover element sizes when the caller already knows the expected type.
    /// </summary>
    member this.ReadTag(tagName: string, dataType: DataType, ?elementCount: int) =
        let count = defaultArg elementCount 1
        let elementSize = dataType.ByteSize

        // Bit selector handling (Tag.3 or Tag[0].3)
        match ABClientUtil.tryParseBitSelector tagName, count with
        | Some (baseTag, indexOpt, bit), 1 ->
            let elementTag = 
                match indexOpt with
                | Some index -> sprintf "%s[%d]" baseTag index
                | None -> baseTag
        
            let handleRaw raw =
                match ABClientUtil.tryExtractBitValue raw bit with
                | Some result -> (NoError, Some (box result))
                | None -> 
                    (UnknownError (sprintf "Unable to extract bit %d from %s (value type: %s)" 
                        bit elementTag (raw.GetType().FullName)), None)
        
            let (error, rawOpt) = this.ReadTagRaw(elementTag, 1)
            match error with
            | NoError ->
                match rawOpt with
                | Some raw -> handleRaw raw
                | None -> (UnknownError "No data", None)
            | _ ->
                mapPathError error (fun () ->
                    match indexOpt with
                    | Some index ->
                        let (err2, raw2Opt) = this.ReadTagRaw(baseTag, index + 1)
                        match err2 with
                        | NoError ->
                            match raw2Opt with
                            | Some raw2 ->
                                match ABClientUtil.sliceElement raw2 index with
                                | Some element -> handleRaw element
                                | None -> (UnknownError (sprintf "Unable to read element %d" index), None)
                            | None -> (UnknownError "No data", None)
                        | _ -> (err2, None)
                    | None -> (error, None))

        // Single array element access (Tag[0])
        | _ when count = 1 ->
            match ABClientUtil.tryParseIndexer tagName with
            | Some (baseTag, index) when tagName <> baseTag ->
                let (error, dataOpt) = this.ReadTagRaw(tagName, 1)
                match error with
                | NoError -> (NoError, dataOpt)
                | _ ->
                    mapPathError error (fun () ->
                        let isBoolArray () = dataType = DataType.BOOL
                    
                        let readCount =
                            if isBoolArray () then ((index / 32) + 1) * 32
                            else index + 1
                    
                        let (err2, raw2Opt) = this.ReadTagRaw(baseTag, readCount)
                        match err2 with
                        | NoError ->
                            match raw2Opt with
                            | Some raw2 ->
                                match ABClientUtil.sliceElement raw2 index with
                                | Some element -> (NoError, Some element)
                                | None -> (UnknownError (sprintf "Unable to read element %d" index), None)
                            | None -> (UnknownError "No data", None)
                        | _ -> (err2, None))
            | _ ->
                this.ReadTagRaw(tagName, count)

        // Large array read path
        | _ ->
            let estimatedBytes = int64 count * int64 elementSize
        
            if estimatedBytes > 480L then
                // Use Multiple Read optimisations
                this.ReadTagMultiple(tagName, dataType, count)
            else
                // Fallback to a simple read
                let (error, dataOpt) = this.ReadTagRaw(tagName, count)
                match error with
                | _PartialTransfer ->
                    // Retry with Multiple Read when partial transfers occur
                    printfn "[ReadTag] Partial transfer detected, retrying with multiple read"
                    this.ReadTagMultiple(tagName, dataType, count)
                | _ ->
                    (error, dataOpt)
    
    /// Writes a tag, including bit addressing support.
    member this.WriteTag(tagName: string, dataType: DataType, values: byte[]) =
        let inline boolOfBytes (bytes: byte[]) = bytes.Length > 0 && bytes.[0] <> 0uy

        // Bit selector handling
        match ABClientUtil.tryParseBitSelector tagName with
        | Some (baseTag, indexOpt, bit) when dataType = DataType.BOOL ->
            if values.Length = 0 then
                UnknownError "Invalid BOOL value"
            else
                let desired = boolOfBytes values
                let elementTag = 
                    match indexOpt with
                    | Some index -> sprintf "%s[%d]" baseTag index
                    | None -> baseTag
            
                let attemptWrite raw =
                    match ABClientUtil.tryPrepareBitWrite raw bit desired with
                    | Some (actualType, payload) when actualType <> DataType.BOOL ->
                        this.WriteTagRaw(elementTag, actualType, payload)
                    | Some _ | None ->
                        let index = defaultArg indexOpt 0
                        let readCount = if dataType = DataType.BOOL then (index + 1) * 32 else index + 1
                        let (err, fullRawOpt) = this.ReadTagRaw(baseTag, readCount)
                        match err with
                        | NoError ->
                            match fullRawOpt with
                            | Some fullRaw ->
                                match ABClientUtil.tryPrepareBitWriteFull fullRaw index bit desired with
                                | Some (actualType, payload) ->
                                    match fullRaw with
                                    | :? (bool[]) -> this.WriteTagRaw(elementTag, actualType, payload)
                                    | _ -> this.WriteTagRaw(baseTag, actualType, payload)
                                | None -> UnknownError "Unable to prepare bit write"
                            | None -> UnknownError "No data"
                        | _ -> err
            
                let (error, rawOpt) = this.ReadTagRaw(elementTag, 1)
                match error with
                | NoError ->
                    match rawOpt with
                    | Some raw -> attemptWrite raw
                    | None -> UnknownError "No data"
                | _ ->
                    match mapPathError error (fun () ->
                        match indexOpt with
                        | Some index ->
                            let (err2, raw2Opt) = this.ReadTagRaw(baseTag, index + 1)
                            match err2 with
                            | NoError ->
                                match raw2Opt with
                                | Some fullRaw ->
                                    match ABClientUtil.tryPrepareBitWriteFull fullRaw index bit desired with
                                    | Some (actualType, payload) ->
                                        match fullRaw with
                                        | :? (bool[]) -> (this.WriteTagRaw(elementTag, actualType, payload), Some ())
                                        | _ -> (this.WriteTagRaw(baseTag, actualType, payload), Some ())
                                    | None -> (UnknownError "Unable to prepare bit write", None)
                                | None -> (UnknownError "No data", None)
                            | _ -> (err2, None)
                        | None -> (error, None)) with
                    | (err, Some ()) -> err
                    | (err, None) -> err
        | _ ->
            // Index selector handling
            match ABClientUtil.tryParseIndexer tagName with
            | Some (baseTag, index) when tagName <> baseTag ->
                if dataType = DataType.BOOL then
                    if values.Length = 0 then
                        UnknownError "Invalid BOOL value"
                    else
                        let arrayLengthOpt =
                            match tagCache.TryGetValue(baseTag) with
                            | true, info when info.ArrayDimensions.Length > 0 ->
                                Some info.ArrayDimensions.[0]
                            | _ -> None
                    
                        let maxWritable =
                            match arrayLengthOpt with
                            | Some length when index >= length -> 0
                            | Some length -> min values.Length (length - index)
                            | None -> values.Length
                    
                        if maxWritable <= 0 then
                            UnknownError "Invalid index"
                        else
                            let payload = Array.init maxWritable (fun i -> 
                                if values.[i] <> 0uy then 1uy else 0uy)
                            this.WriteTagRaw(tagName, DataType.BOOL, payload)
                else
                    let error = this.WriteTagRaw(tagName, dataType, values)
                    match error with
                    | NoError -> NoError
                    | _ ->
                        match mapPathError error (fun () ->
                            let (err, rawOpt) = this.ReadTagRaw(baseTag, index + 1)
                            match err with
                            | NoError ->
                                match rawOpt with
                                | Some raw ->
                                    match ABClientUtil.decodeValue dataType values with
                                    | Some elementValue ->
                                        match ABClientUtil.packElement raw index elementValue with
                                        | Some (actualType, payload) -> 
                                            (this.WriteTagRaw(baseTag, actualType, payload), Some ())
                                        | None -> (UnknownError "Unable to prepare element write", None)
                                    | None -> (UnknownError "Unable to decode value", None)
                                | None -> (UnknownError "No data", None)
                            | _ -> (err, None)) with
                        | (err, Some ()) -> err
                        | (err, None) -> err
            | _ ->
                // Large array write path
                let totalBytes = int64 values.Length
                
                if totalBytes > 480L then
                    // Use Multiple Write optimisation
                    this.WriteTagMultiple(tagName, dataType, values)
                else
                    // Fallback to a simple write
                    this.WriteTagRaw(tagName, dataType, values)

    /// Executes a batch read (Multiple Service).
    member this.BatchRead(requests: ReadRequest[]) =
        let startTime = DateTime.UtcNow
        let results = Dictionary<string, AbProtocolError * obj option>()
        let makeKey (req: ReadRequest) = sprintf "%s[%d]" req.TagName req.StartIndex
        
        let chunks = PacketBuilder.buildBatchReadRequest session.SessionHandle 0UL requests
        
        let mutable chunkIndex = 0
        for chunk in chunks do
            let sendResult = session.SendUnconnected(chunk)
            
            let startIdx = chunkIndex * Constants.Performance.MaxServicesPerMultiPacket
            let endIdx = min (startIdx + Constants.Performance.MaxServicesPerMultiPacket) requests.Length
            
            match sendResult with
            | (NoError, Some (buffer, length)) ->
                let expectedCounts =
                    [| for i in startIdx .. endIdx - 1 ->
                        max 1 requests.[i].ElementCount |]
                
                let (error, responses) = PacketParser.parseMultipleServiceResponse buffer length expectedCounts
                match error with
                | NoError ->
                    for i in startIdx .. endIdx - 1 do
                        let req = requests.[i]
                        let respIdx = i - startIdx
                        if respIdx < responses.Length then
                            results.[makeKey req] <- responses.[respIdx]
                        else
                            results.[makeKey req] <- (UnknownError "No response", None)
                | _ ->
                    for i in startIdx .. endIdx - 1 do
                        results.[makeKey requests.[i]] <- (error, None)
            | (error, _) ->
                for i in startIdx .. endIdx - 1 do
                    results.[makeKey requests.[i]] <- (error, None)
            
            chunkIndex <- chunkIndex + 1
        
        recordResponseTime ((DateTime.UtcNow - startTime).TotalMilliseconds)
        (NoError, Some results)

    /// Returns current communication statistics.
    member this.GetStatistics() = !stats
    
    /// Keep-alive
    member this.KeepAlive() = session.KeepAlive()
    
    /// Indicates whether the client currently holds an active session.
    member this.IsConnected = session.IsConnected
    
    /// Returns cached PLC identity information when available.
    member this.PlcInfo = plcInfo
    
    interface IDisposable with
        member this.Dispose() =
            this.Disconnect()
            (session :> IDisposable).Dispose() |> ignore
