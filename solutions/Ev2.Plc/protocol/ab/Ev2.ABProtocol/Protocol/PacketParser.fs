namespace Ev2.AbProtocol.Protocol

open System
open System.Text
open Ev2.AbProtocol.Core

/// <summary>
///     Parses EtherNet/IP and CIP responses into strongly typed results.  Each helper returns
///     a <see cref="AbProtocolError"/> to keep error handling explicit.
/// </summary>
[<AutoOpen>]
module PacketParser =
    
    // ========================================
    // Helper functions
    // ========================================
    
    /// Ensures the requested range fits within the buffer.
    let private validateBufferRange (buffer: byte[]) (offset: int) (length: int) =
        offset >= 0 && 
        length >= 0 && 
        offset + length <= buffer.Length
    
    /// Safely slices a portion of the buffer if the range is valid.
    let private safeSlice (buffer: byte[]) (offset: int) (length: int) =
        if validateBufferRange buffer offset length then
            Some buffer.[offset..offset + length - 1]
        else
            None
    
    /// Reads an unsigned byte when the buffer contains enough data.
    let private tryReadByte (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 1 then
            Some buffer.[offset]
        else
            None
    
    /// Reads an unsigned 16-bit little-endian integer.
    let private tryReadUInt16 (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 2 then
            Some (BitConverter.ToUInt16(buffer, offset))
        else
            None
    
    /// Reads a signed 16-bit little-endian integer.
    let private tryReadInt16 (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 2 then
            Some (BitConverter.ToInt16(buffer, offset))
        else
            None
    
    /// Reads an unsigned 32-bit little-endian integer.
    let private tryReadUInt32 (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 4 then
            Some (BitConverter.ToUInt32(buffer, offset))
        else
            None
    
    /// Reads a signed 32-bit little-endian integer.
    let private tryReadInt32 (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 4 then
            Some (BitConverter.ToInt32(buffer, offset))
        else
            None
    
    /// Reads an unsigned 64-bit little-endian integer.
    let private tryReadUInt64 (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 8 then
            Some (BitConverter.ToUInt64(buffer, offset))
        else
            None
    
    /// Reads a signed 64-bit little-endian integer.
    let private tryReadInt64 (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 8 then
            Some (BitConverter.ToInt64(buffer, offset))
        else
            None
    
    /// Reads a single-precision floating point value.
    let private tryReadSingle (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 4 then
            Some (BitConverter.ToSingle(buffer, offset))
        else
            None
    
    /// Reads a double-precision floating point value.
    let private tryReadDouble (buffer: byte[]) (offset: int) =
        if validateBufferRange buffer offset 8 then
            Some (BitConverter.ToDouble(buffer, offset))
        else
            None
    
    // ========================================
    // EtherNet/IP header parser
    // ========================================
    
    /// Parses the EtherNet/IP header and returns a protocol error on failure.
    let parseEIPHeader (buffer: byte[]) =
        if buffer.Length < Constants.EIP.HeaderSize then
            UnknownError "Buffer too small for EIP header"
        else
            let command = BitConverter.ToUInt16(buffer, 0)
            let length = BitConverter.ToUInt16(buffer, 2)
            let sessionHandle = BitConverter.ToUInt32(buffer, 4)
            let status = BitConverter.ToUInt32(buffer, 8)
            let context = BitConverter.ToUInt64(buffer, 12)
            let options = BitConverter.ToUInt32(buffer, 20)
            
            if status <> Constants.EIP.StatusSuccess then
                SessionError (Constants.EIP.statusToMessage status)
            else
                NoError
    
    /// Extracts values from the EtherNet/IP header.
    let parseEIPHeaderData (buffer: byte[]) =
        if buffer.Length < Constants.EIP.HeaderSize then
            None
        else
            let command = BitConverter.ToUInt16(buffer, 0)
            let length = BitConverter.ToUInt16(buffer, 2)
            let sessionHandle = BitConverter.ToUInt32(buffer, 4)
            let context = BitConverter.ToUInt64(buffer, 12)
            let options = BitConverter.ToUInt32(buffer, 20)
            Some (command, length, sessionHandle, context, options)
    
    /// Returns the data offset that follows the EtherNet/IP header.
    let getEIPDataOffset() = Constants.EIP.HeaderSize
    
    // ========================================
    // Session management responses
    // ========================================
    
    /// Parses the response for the Register Session command.
    let parseRegisterSession (buffer: byte[]) =
        match parseEIPHeader buffer with
        | NoError ->
            match parseEIPHeaderData buffer with
            | Some (_, _, sessionHandle, _, _) -> (NoError, Some sessionHandle)
            | None -> (UnknownError "Cannot extract session handle", None)
        | err -> (err, None)
    
    // ========================================
    // CPF (Common Packet Format) parser
    // ========================================
    
    /// Parses the items contained within a CPF structure.
    let parseCPFItems (buffer: byte[]) (offset: int) =
        if not (validateBufferRange buffer offset 8) then
            (UnknownError "Buffer too small for CPF header", [||])
        else
            let items = ResizeArray<(uint16 * byte[])>()
            let mutable pos = offset
            let mutable error = NoError
        
            // Interface handle (4 bytes) + Timeout (2 bytes)
            pos <- pos + 6
        
            // Item count (2 bytes)
            match tryReadUInt16 buffer pos with
            | None -> 
                error <- UnknownError "Cannot read CPF item count"
            | Some itemCount ->
                pos <- pos + 2
            
                let rec parseItems remaining =
                    if remaining = 0us || error <> NoError then
                        ()
                    else
                        if not (validateBufferRange buffer pos 4) then
                            error <- UnknownError (sprintf "Buffer too small for CPF item header at offset %d" pos)
                        else
                            match tryReadUInt16 buffer pos, tryReadUInt16 buffer (pos + 2) with
                            | Some itemType, Some itemLength ->
                                pos <- pos + 4
                            
                                let itemDataLength = int itemLength
                                if not (validateBufferRange buffer pos itemDataLength) then
                                    error <- UnknownError (sprintf "Buffer too small for CPF item data at offset %d (need %d bytes)" pos itemDataLength)
                                else
                                    match safeSlice buffer pos itemDataLength with
                                    | None -> 
                                        error <- UnknownError (sprintf "Cannot extract CPF item data at offset %d" pos)
                                    | Some itemData ->
                                        items.Add((itemType, itemData))
                                        pos <- pos + itemDataLength
                                        parseItems (remaining - 1us)
                            | _ -> 
                                error <- UnknownError (sprintf "Cannot read CPF item type/length at offset %d" pos)
            
                parseItems itemCount
            
            (error, items.ToArray())
    
    /// Extracts application data from a UCMM message.
    let extractUCMMData (items: (uint16 * byte[])[]) =
        items 
        |> Array.tryFind (fun (itemType, _) -> itemType = Constants.EIP.ItemUnconnectedData)
        |> Option.map snd
    
    /// Extracts application data from a connected message.
    let extractConnectedData (items: (uint16 * byte[])[]) =
        items 
        |> Array.tryFind (fun (itemType, _) -> itemType = Constants.EIP.ItemConnectedData)
        |> Option.map snd
    
    // ========================================
    // CIP response parsing
    // ========================================
    
    /// Parses the header portion of a CIP response.
    let parseCIPResponseHeader (data: byte[]) (offset: int) =
        if not (validateBufferRange data offset 4) then
            (UnknownError "Buffer too small for CIP response", 0uy, 0)
        else
            let replyService = data.[offset]
            let reserved = data.[offset + 1]
            let generalStatus = data.[offset + 2]
            let additionalStatusSize = data.[offset + 3]  // Additional status byte count
        
            let dataOffset = offset + 4 + (int additionalStatusSize * 2)
        
            //printfn "[CIP] Reply: 0x%02X, Status: 0x%02X, AddStatusSize: %d, DataOffset: %d" 
            //    replyService generalStatus additionalStatusSize dataOffset
        
            if generalStatus <> Constants.CIP.StatusSuccess then
                (CIPError (generalStatus, Constants.CIP.statusToMessage generalStatus), replyService, dataOffset)
            else
                (NoError, replyService, dataOffset)
    
    /// Parses an entire CIP response payload.
    let parseCIPResponse (buffer: byte[]) (offset: int) =
        parseCIPResponseHeader buffer offset
    
    // ========================================
    // Identity response parsing
    // ========================================
    
    /// Parses a ListIdentity response into device information.
    let parseListIdentity (buffer: byte[]) (length: int) =
        if length < 50 then
            (UnknownError "Response too short for identity", None)
        else
            match parseEIPHeader buffer with
            | NoError ->
                try
                    let mutable pos = 24
                    let itemCount = BitConverter.ToUInt16(buffer, pos)
                    pos <- pos + 2
                
                    if itemCount = 0us then
                        (UnknownError "No identity items in response", None)
                    else
                        let itemType = BitConverter.ToUInt16(buffer, pos)
                        pos <- pos + 2
                        let itemLength = BitConverter.ToUInt16(buffer, pos)
                        pos <- pos + 2
                    
                        if itemType <> Constants.EIP.ItemListIdentity then
                            (UnknownError (sprintf "Expected ItemListIdentity (0x%04X), got 0x%04X" Constants.EIP.ItemListIdentity itemType), None)
                        elif buffer.Length < pos + int itemLength then
                            (UnknownError "Buffer too small for identity data", None)
                        else
                            let data = buffer.[pos .. pos + int itemLength - 1]
                        
                            if data.Length < 33 then
                                (UnknownError (sprintf "Identity data too short: %d bytes" data.Length), None)
                            else
                                let vendorId = BitConverter.ToUInt16(data, 18)
                                let deviceType = BitConverter.ToUInt16(data, 20)
                                let productCode = BitConverter.ToUInt16(data, 22)
                                let majorRev = data.[24]
                                let minorRev = data.[25]
                                let status = BitConverter.ToUInt16(data, 26)
                                let serialNumber = BitConverter.ToUInt32(data, 28)
                                let nameLength = int data.[32]
                            
                                if data.Length < 33 + nameLength then
                                    (UnknownError "Identity data truncated", None)
                                else
                                    let productName = 
                                        if nameLength > 0 then
                                            Encoding.ASCII.GetString(data, 33, nameLength)
                                        else
                                            ""
                                
                                    let state = 
                                        if data.Length > 33 + nameLength then
                                            data.[33 + nameLength]
                                        else
                                            0uy
                                
                                    let identity = {
                                        VendorId = vendorId
                                        DeviceType = deviceType
                                        ProductCode = productCode
                                        MajorRevision = majorRev
                                        MinorRevision = minorRev
                                        Status = status
                                        SerialNumber = serialNumber
                                        ProductNameLength = byte nameLength
                                        ProductName = productName
                                        State = state
                                    }
                                    (NoError, Some identity)
                with ex ->
                    (UnknownError (sprintf "Parse exception: %s" ex.Message), None)
            | err -> (err, None)
    
    // ========================================
    // Data parsing helpers by type
    // ========================================
    
    /// Parses a packed BOOL array.
    let private parseBoolArray (data: byte[]) (offset: int) (elementCount: int) =
        try
            let byteCount = (elementCount + 7) / 8
            
            if not (validateBufferRange data offset byteCount) then
                (UnknownError (sprintf "Buffer too small for BOOL array (need %d bytes)" byteCount), None, offset)
            else
                let values = [| 
                    for i in 0 .. elementCount - 1 ->
                        let byteIdx = i / 8
                        let bitIdx = i % 8
                        (data.[offset + byteIdx] &&& (1uy <<< bitIdx)) <> 0uy
                |]
                (NoError, Some (box values), offset + byteCount)
        with ex ->
            (UnknownError (sprintf "BOOL array parse error: %s" ex.Message), None, offset)
    
    /// Parses a Logix STRING value.
    let private parseString (data: byte[]) (offset: int) =
        try
            if not (validateBufferRange data offset 4) then
                (UnknownError "Buffer too small for STRING length", None, offset)
            else
                match tryReadUInt32 data offset with
                | None -> (UnknownError "Cannot read STRING length", None, offset)
                | Some strLength ->
                    let length = int strLength
                    let dataOffset = offset + 4
                    
                    if not (validateBufferRange data dataOffset length) then
                        (UnknownError (sprintf "Buffer too small for STRING data (need %d bytes)" length), None, offset)
                    else
                        let str = 
                            if length > 0 then
                                Encoding.ASCII.GetString(data, dataOffset, length)
                            else
                                ""
                        
                        let totalSize = 4 + length
                        let paddedSize = if totalSize % 2 = 0 then totalSize else totalSize + 1
                        
                        (NoError, Some (box str), offset + paddedSize)
        with ex ->
            (UnknownError (sprintf "STRING parse error: %s" ex.Message), None, offset)
    
    /// Parses a TIMER structure.
    let private parseTimer (data: byte[]) (offset: int) =
        try
            if not (validateBufferRange data offset 12) then
                (UnknownError "Buffer too small for TIMER", None, offset)
            else
                match tryReadInt32 data offset, 
                      tryReadInt32 data (offset + 4), 
                      tryReadUInt32 data (offset + 8) with
                | Some pre, Some acc, Some status ->
                    let en = (status &&& 0x80000000u) <> 0u
                    let tt = (status &&& 0x40000000u) <> 0u
                    let dn = (status &&& 0x20000000u) <> 0u
                    
                    let timer = {| 
                        PRE = pre
                        ACC = acc
                        EN = en
                        TT = tt
                        DN = dn
                    |}
                    (NoError, Some (box timer), offset + 12)
                | _ -> (UnknownError "Cannot read TIMER fields", None, offset)
        with ex ->
            (UnknownError (sprintf "TIMER parse error: %s" ex.Message), None, offset)
    
    /// Parses a COUNTER structure.
    let private parseCounter (data: byte[]) (offset: int) =
        try
            if not (validateBufferRange data offset 12) then
                (UnknownError "Buffer too small for COUNTER", None, offset)
            else
                match tryReadInt32 data offset, 
                      tryReadInt32 data (offset + 4), 
                      tryReadUInt32 data (offset + 8) with
                | Some pre, Some acc, Some status ->
                    let cu = (status &&& 0x80000000u) <> 0u
                    let cd = (status &&& 0x40000000u) <> 0u
                    let dn = (status &&& 0x20000000u) <> 0u
                    let ov = (status &&& 0x10000000u) <> 0u
                    let un = (status &&& 0x08000000u) <> 0u
                    
                    let counter = {| 
                        PRE = pre
                        ACC = acc
                        CU = cu
                        CD = cd
                        DN = dn
                        OV = ov
                        UN = un
                    |}
                    (NoError, Some (box counter), offset + 12)
                | _ -> (UnknownError "Cannot read COUNTER fields", None, offset)
        with ex ->
            (UnknownError (sprintf "COUNTER parse error: %s" ex.Message), None, offset)
    
    /// Parses a single scalar value.
    let private parseSingleValue (baseType: uint16) (data: byte[]) (offset: int) =
        match baseType with
        | t when t = Constants.DataTypeCodes.Bool ->
            match tryReadByte data offset with
            | Some b -> (NoError, Some (box (b <> 0uy)), offset + 1)
            | None -> (UnknownError "Cannot read BOOL", None, offset)
            
        | t when t = Constants.DataTypeCodes.Sint ->
            match tryReadByte data offset with
            | Some b -> (NoError, Some (box (sbyte b)), offset + 1)
            | None -> (UnknownError "Cannot read SINT", None, offset)
            
        | t when t = Constants.DataTypeCodes.Int ->
            match tryReadInt16 data offset with
            | Some v -> (NoError, Some (box v), offset + 2)
            | None -> (UnknownError "Cannot read INT", None, offset)
            
        | t when t = Constants.DataTypeCodes.Dint ->
            match tryReadInt32 data offset with
            | Some v -> (NoError, Some (box v), offset + 4)
            | None -> (UnknownError "Cannot read DINT", None, offset)
            
        | t when t = Constants.DataTypeCodes.Lint ->
            match tryReadInt64 data offset with
            | Some v -> (NoError, Some (box v), offset + 8)
            | None -> (UnknownError "Cannot read LINT", None, offset)
            
        | t when t = Constants.DataTypeCodes.Usint ->
            match tryReadByte data offset with
            | Some v -> (NoError, Some (box v), offset + 1)
            | None -> (UnknownError "Cannot read USINT", None, offset)
            
        | t when t = Constants.DataTypeCodes.Uint ->
            match tryReadUInt16 data offset with
            | Some v -> (NoError, Some (box v), offset + 2)
            | None -> (UnknownError "Cannot read UINT", None, offset)
            
        | t when t = Constants.DataTypeCodes.Udint ->
            match tryReadUInt32 data offset with
            | Some v -> (NoError, Some (box v), offset + 4)
            | None -> (UnknownError "Cannot read UDINT", None, offset)
            
        | t when t = Constants.DataTypeCodes.Ulint ->
            match tryReadUInt64 data offset with
            | Some v -> (NoError, Some (box v), offset + 8)
            | None -> (UnknownError "Cannot read ULINT", None, offset)
            
        | t when t = Constants.DataTypeCodes.Real ->
            match tryReadSingle data offset with
            | Some v -> (NoError, Some (box v), offset + 4)
            | None -> (UnknownError "Cannot read REAL", None, offset)
            
        | t when t = Constants.DataTypeCodes.Lreal ->
            match tryReadDouble data offset with
            | Some v -> (NoError, Some (box v), offset + 8)
            | None -> (UnknownError "Cannot read LREAL", None, offset)
            
        | t when t = Constants.DataTypeCodes.String ->
            parseString data offset
            
        | t when t = Constants.DataTypeCodes.Timer ->
            parseTimer data offset
            
        | _ -> (InvalidDataType(DINT, BOOL), None, offset)
    
    /// Parses an array of values.
    let private parseArrayValues (baseType: uint16) (data: byte[]) (offset: int) (elementCount: int) =
        try
            match baseType with
            | t when t = Constants.DataTypeCodes.Bool ->
                parseBoolArray data offset elementCount
                
            | t when t = Constants.DataTypeCodes.Sint ->
                if not (validateBufferRange data offset elementCount) then
                    (UnknownError "Buffer too small for SINT array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> sbyte data.[offset + i] |]
                    (NoError, Some (box values), offset + elementCount)
                
            | t when t = Constants.DataTypeCodes.Int ->
                let byteCount = elementCount * 2
                if not (validateBufferRange data offset byteCount) then
                    (UnknownError "Buffer too small for INT array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> BitConverter.ToInt16(data, offset + i * 2) |]
                    (NoError, Some (box values), offset + byteCount)
                
            | t when t = Constants.DataTypeCodes.Dint ->
                let byteCount = elementCount * 4
                if not (validateBufferRange data offset byteCount) then
                    (UnknownError "Buffer too small for DINT array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> BitConverter.ToInt32(data, offset + i * 4) |]
                    (NoError, Some (box values), offset + byteCount)
                
            | t when t = Constants.DataTypeCodes.Lint ->
                let byteCount = elementCount * 8
                if not (validateBufferRange data offset byteCount) then
                    (UnknownError "Buffer too small for LINT array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> BitConverter.ToInt64(data, offset + i * 8) |]
                    (NoError, Some (box values), offset + byteCount)
                
            | t when t = Constants.DataTypeCodes.Usint ->
                if not (validateBufferRange data offset elementCount) then
                    (UnknownError "Buffer too small for USINT array", None, offset)
                else
                    let values = data.[offset .. offset + elementCount - 1]
                    (NoError, Some (box values), offset + elementCount)
                
            | t when t = Constants.DataTypeCodes.Uint ->
                let byteCount = elementCount * 2
                if not (validateBufferRange data offset byteCount) then
                    (UnknownError "Buffer too small for UINT array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> BitConverter.ToUInt16(data, offset + i * 2) |]
                    (NoError, Some (box values), offset + byteCount)
                
            | t when t = Constants.DataTypeCodes.Udint ->
                let byteCount = elementCount * 4
                if not (validateBufferRange data offset byteCount) then
                    (UnknownError "Buffer too small for UDINT array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> BitConverter.ToUInt32(data, offset + i * 4) |]
                    (NoError, Some (box values), offset + byteCount)
                
            | t when t = Constants.DataTypeCodes.Ulint ->
                let byteCount = elementCount * 8
                if not (validateBufferRange data offset byteCount) then
                    (UnknownError "Buffer too small for ULINT array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> BitConverter.ToUInt64(data, offset + i * 8) |]
                    (NoError, Some (box values), offset + byteCount)
                
            | t when t = Constants.DataTypeCodes.Real ->
                let byteCount = elementCount * 4
                if not (validateBufferRange data offset byteCount) then
                    (UnknownError "Buffer too small for REAL array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> BitConverter.ToSingle(data, offset + i * 4) |]
                    (NoError, Some (box values), offset + byteCount)
                
            | t when t = Constants.DataTypeCodes.Lreal ->
                let byteCount = elementCount * 8
                if not (validateBufferRange data offset byteCount) then
                    (UnknownError "Buffer too small for LREAL array", None, offset)
                else
                    let values = [| for i in 0 .. elementCount - 1 -> BitConverter.ToDouble(data, offset + i * 8) |]
                    (NoError, Some (box values), offset + byteCount)
                
            | t when t = Constants.DataTypeCodes.String ->
                let mutable currentOffset = offset
                let strings = ResizeArray<string>()
                let mutable error = NoError
                
                for i in 0 .. elementCount - 1 do
                    if error = NoError then
                        let (err, value, nextOffset) = parseString data currentOffset
                        match err with
                        | NoError ->
                            match value with
                            | Some str -> 
                                strings.Add(unbox<string> str)
                                currentOffset <- nextOffset
                            | None -> error <- UnknownError "STRING parse returned no value"
                        | _ -> error <- err
                
                if error = NoError then
                    (NoError, Some (box (strings.ToArray())), currentOffset)
                else
                    (error, None, offset)
                
            | t when t = Constants.DataTypeCodes.Timer ->
                let mutable currentOffset = offset
                let timers = ResizeArray<obj>()
                let mutable error = NoError
                
                for i in 0 .. elementCount - 1 do
                    if error = NoError then
                        let (err, value, nextOffset) = parseTimer data currentOffset
                        match err with
                        | NoError ->
                            match value with
                            | Some timer -> 
                                timers.Add(timer)
                                currentOffset <- nextOffset
                            | None -> error <- UnknownError "TIMER parse returned no value"
                        | _ -> error <- err
                
                if error = NoError then
                    (NoError, Some (box (timers.ToArray())), currentOffset)
                else
                    (error, None, offset)
                
            | _ -> (InvalidDataType(DINT, BOOL), None, offset)
        with ex ->
            (UnknownError (sprintf "Array parse error: %s" ex.Message), None, offset)
    
    /// Entry point that parses data based on the CIP data-type code.
    let parseValueByType (dataType: uint16) (data: byte[]) (offset: int) (expectedCount: int option) =
        try
            let isArray = Constants.DataTypeCodes.isArray dataType
            let baseType = Constants.DataTypeCodes.getBaseType dataType
            let normalizedExpectedCount =
                match expectedCount with
                | Some count when count > 1 -> Some count
                | _ -> None

            if isArray then
                if not (validateBufferRange data offset 2) then
                    (UnknownError "Cannot read array element count", None)
                else
                    match tryReadUInt16 data offset with
                    | Some count ->
                        let elementCount = int count
                        let dataOffset = offset + 2
                        let (error, value, _) = parseArrayValues baseType data dataOffset elementCount
                        (error, value)
                    | None ->
                        (UnknownError "Cannot read array element count", None)
            else
                match normalizedExpectedCount with
                | Some count ->
                    let (error, value, _) = parseArrayValues baseType data offset count
                    (error, value)
                | None ->
                    let (error, value, _) = parseSingleValue baseType data offset
                    (error, value)
            
        with ex ->
            (UnknownError (sprintf "Parse error: %s" ex.Message), None)
    
 // ========================================
    // Read Tag response parsing
    // ========================================
    
    /// Parses a Read Tag response using the requested element count.
    let parseReadTagResponse (buffer: byte[]) (length: int) (requestedCount: int) =
        match parseEIPHeader buffer with
        | NoError ->
            let (error, items) = parseCPFItems buffer (getEIPDataOffset())
            if error <> NoError then
                (error, None)
            else
                match extractUCMMData items with
                | None -> (UnknownError "No UCMM data found", None)
                | Some data ->
                    let (error, replyService, dataOffset) = parseCIPResponse data 0
                    if error <> NoError then
                        (error, None)
                    else
                        let expectedReply = Constants.CIP.toReply Constants.CIP.ReadTag
                        if replyService <> expectedReply then
                            (UnknownError (sprintf "Invalid reply service: 0x%02X (expected 0x%02X)" replyService expectedReply), None)
                        elif dataOffset + 2 > data.Length then
                            (UnknownError "No data type in response", None)
                        else
                            match tryReadUInt16 data dataOffset with
                            | None -> (UnknownError "Cannot read data type", None)
                            | Some dataType ->
                                let valueOffset = dataOffset + 2
                                let baseType = Constants.DataTypeCodes.getBaseType dataType
                                
                                // CIP responses do not carry the element count
                                // Values are streamed consecutively based on the requested count
                                if requestedCount = 1 then
                                    // Single value parsing path
                                    let (error, value, _) = parseSingleValue baseType data valueOffset
                                    (error, value)
                                else
                                    // Array parsing path
                                    let (error, value, _) = parseArrayValues baseType data valueOffset requestedCount
                                    (error, value)
        | err -> (err, None)
    // ========================================
    // Write Tag response parsing
    // ========================================
    
    /// Parses a Write Tag response.
    let parseWriteTagResponse (buffer: byte[]) (length: int) =
        match parseEIPHeader buffer with
        | NoError ->
            let (error, items) = parseCPFItems buffer (getEIPDataOffset())
            if error <> NoError then
                error
            else
                match extractUCMMData items with
                | None -> UnknownError "No UCMM data found"
                | Some data -> 
                    let (error, replyService, _) = parseCIPResponse data 0
                    if error <> NoError then
                        error
                    else
                        let expectedReply = Constants.CIP.toReply Constants.CIP.WriteTag
                        if replyService <> expectedReply then
                            UnknownError (sprintf "Invalid reply service: 0x%02X (expected 0x%02X)" replyService expectedReply)
                        else
                            NoError
        | err -> err
    
    // ========================================
    // Multiple Service response parsing
    // ========================================
    
    // ========================================
    // Multiple Service response parsing (final stage)
    // ========================================
    
    /// Parses a Multiple Service Packet response.
    let parseMultipleServiceResponse (buffer: byte[]) (length: int) (expectedCounts: int[]) =
        match parseEIPHeader buffer with
        | NoError ->
            let (error, items) = parseCPFItems buffer (getEIPDataOffset())
            if error <> NoError then
                (error, [||])
            else
                match extractUCMMData items with
                | None -> (UnknownError "No UCMM data found", [||])
                | Some data ->
                    let (error, replyService, dataOffset) = parseCIPResponse data 0
                    if error <> NoError then
                        (error, [||])
                    else
                        let expectedReply = Constants.CIP.toReply Constants.CIP.MultipleServicePacket
                        if replyService <> expectedReply then
                            (UnknownError (sprintf "Invalid reply service: 0x%02X (expected 0x%02X)" replyService expectedReply), [||])
                        else
                            match tryReadUInt16 data dataOffset with
                            | None -> (UnknownError "Cannot read service count", [||])
                            | Some serviceCount ->
                                let mutable pos = dataOffset + 2
                                
                                // Read the per-service offset table
                                let offsets = 
                                    [| for i in 0 .. int serviceCount - 1 do
                                        match tryReadUInt16 data pos with
                                        | Some offset -> 
                                            pos <- pos + 2
                                            yield offset
                                        | None -> yield 0us |]
                                
                                // Parse each individual service response
                                let responses = ResizeArray<AbProtocolError * obj option>()
                                
                                for i in 0 .. offsets.Length - 1 do
                                    let servicePos = dataOffset + int offsets.[i]
                                    
                                    let (error, service, dataPos) = parseCIPResponse data servicePos
                                    if error = NoError then
                                        let readTagReply = Constants.CIP.toReply Constants.CIP.ReadTag
                                        let writeTagReply = Constants.CIP.toReply Constants.CIP.WriteTag
                                        
                                        if service = readTagReply && dataPos + 2 <= data.Length then
                                            // Parse read responses
                                            match tryReadUInt16 data dataPos with
                                            | Some dataType ->
                                                let valuePos = dataPos + 2
                                                let expectedCount =
                                                    if i < expectedCounts.Length then
                                                        let count = expectedCounts.[i]
                                                        if count <= 0 then 1 else count
                                                    else 1
                                                let (err, value) = parseValueByType dataType data valuePos (Some expectedCount)
                                                responses.Add((err, value))
                                            | None -> 
                                                responses.Add((UnknownError "Cannot read data type in multi-service", None))
                                        elif service = writeTagReply then
                                            // Parse write responses (success code only)
                                            responses.Add((NoError, Some (box ())))
                                        else
                                            responses.Add((UnknownError (sprintf "Unknown service in multi-service: 0x%02X" service), None))
                                    else
                                        responses.Add((error, None))
                                
                                (NoError, responses.ToArray())
        | err -> (err, [||])
    /// Parses the response from Get Attribute List.
    let parseAttributeListResponse (buffer: byte[]) (length: int) =
        match parseEIPHeader buffer with
        | NoError ->
            let (error, items) = parseCPFItems buffer (getEIPDataOffset())
            if error <> NoError then
                (error, None)
            else
                match extractUCMMData items with
                | None -> (UnknownError "No UCMM data found", None)
                | Some data ->
                    let (error, replyService, dataOffset) = parseCIPResponse data 0
                    if error <> NoError then
                        (error, None)
                    else
                        let expectedReply = Constants.CIP.toReply Constants.CIP.GetAttributeList
                        if replyService <> expectedReply then
                            (UnknownError (sprintf "Invalid reply service: 0x%02X (expected 0x%02X)" replyService expectedReply), None)
                        elif dataOffset + 2 > data.Length then
                            (UnknownError "Attribute list response too short", None)
                        else
                            match tryReadUInt16 data dataOffset with
                            | None -> (UnknownError "Cannot read attribute count", None)
                            | Some attributeCount ->
                                let mutable offset = dataOffset + 2
                                let results = ResizeArray<uint16 * uint16 * byte[]>()
                            
                                for _ in 0 .. int attributeCount - 1 do
                                    if offset + 4 <= data.Length then
                                        let attrId = BitConverter.ToUInt16(data, offset)
                                        let status = BitConverter.ToUInt16(data, offset + 2)
                                        offset <- offset + 4
                                    
                                        let valueBytes =
                                            if status <> 0us then [||]
                                            else
                                                match attrId with
                                                | id when id = Constants.AttributeId.SymbolName ->
                                                    if offset + 2 > data.Length then [||]
                                                    else
                                                        let strlen = BitConverter.ToUInt16(data, offset) |> int
                                                        offset <- offset + 2
                                                        let actual = min strlen (data.Length - offset)
                                                        let value = 
                                                            if actual <= 0 then [||]
                                                            else data.[offset .. offset + actual - 1]
                                                        offset <- offset + actual
                                                        if actual % 2 = 1 && offset < data.Length && data.[offset] = 0uy then
                                                            offset <- offset + 1
                                                        Array.append (BitConverter.GetBytes(uint16 strlen)) value
                                            
                                                | id when id = Constants.AttributeId.MaxInstance ||
                                                          id = Constants.AttributeId.NumInstances ||
                                                          id = Constants.AttributeId.SymbolType ->
                                                    if offset + 4 > data.Length then [||]
                                                    else
                                                        let bytes = data.[offset .. offset + 3]
                                                        offset <- offset + 4
                                                        bytes
                                            
                                                | id when id = Constants.AttributeId.SymbolDimensions ->
                                                    let available = data.Length - offset
                                                    let take = min 12 available
                                                    let bytes = data.[offset .. offset + take - 1]
                                                    offset <- offset + take
                                                    bytes
                                            
                                                | _ ->
                                                    offset <- data.Length
                                                    [||]
                                    
                                        results.Add((attrId, status, valueBytes))
                            
                                (NoError, Some (results.ToArray()))
        | error -> (error, None)


    // ========================================
    // Read Tag Fragmented response parsing
    // ========================================
/// Parses fragmented Read Tag responses.
    let parseReadTagFragmentedResponse (buffer: byte[]) (length: int) (requestedCount: int) =
        match parseEIPHeader buffer with
        | NoError ->
            let (error, items) = parseCPFItems buffer (getEIPDataOffset())
            if error <> NoError then
                (error, None)
            else
                match extractUCMMData items with
                | None -> (UnknownError "No UCMM data found", None)
                | Some data ->
                    printfn "[Parser] Fragmented response data length: %d" data.Length
                    printfn "[Parser] First 20 bytes: %s" 
                        (data.[0..min 19 (data.Length-1)] 
                         |> Array.map (sprintf "%02X") 
                         |> String.concat " ")
                
                    let (error, replyService, dataOffset) = parseCIPResponse data 0
                
                    printfn "[CIP] Reply: 0x%02X, Status: 0x%02X, AddStatusSize: 0, DataOffset: %d" 
                        replyService 0uy dataOffset
                
                    if error <> NoError then
                        (error, None)
                    else
                        let expectedReply = Constants.CIP.toReply Constants.CIP.ReadTagFragmented
                        if replyService <> expectedReply then
                            (UnknownError (sprintf "Invalid reply service: 0x%02X (expected 0x%02X)" replyService expectedReply), None)
                        elif dataOffset + 2 > data.Length then
                            (UnknownError "No data type in fragmented response", None)
                        else
                            match tryReadUInt16 data dataOffset with
                            | None -> (UnknownError "Cannot read data type", None)
                            | Some dataType ->
                                printfn "[Parser] DataType: 0x%04X" dataType
                                let valueOffset = dataOffset + 2
                            
                                let availableBytes = data.Length - valueOffset
                                printfn "[Parser] Available data bytes: %d" availableBytes
                            
                                if availableBytes <= 0 then
                                    printfn "[Parser] No data available - returning empty result"
                                    (NoError, Some (box Array.empty<int16>))
                                else
                                    // Compute the element count from the received byte length
                                    let baseType = Constants.DataTypeCodes.getBaseType dataType
                                    let elementSize = 
                                        match baseType with
                                        | t when t = Constants.DataTypeCodes.Bool -> 1
                                        | t when t = Constants.DataTypeCodes.Sint -> 1
                                        | t when t = Constants.DataTypeCodes.Int -> 2
                                        | t when t = Constants.DataTypeCodes.Dint -> 4
                                        | t when t = Constants.DataTypeCodes.Lint -> 8
                                        | t when t = Constants.DataTypeCodes.Usint -> 1
                                        | t when t = Constants.DataTypeCodes.Uint -> 2
                                        | t when t = Constants.DataTypeCodes.Udint -> 4
                                        | t when t = Constants.DataTypeCodes.Ulint -> 8
                                        | t when t = Constants.DataTypeCodes.Real -> 4
                                        | t when t = Constants.DataTypeCodes.Lreal -> 8
                                        | _ -> 4
                                
                                    // Determine how many elements we can parse from the payload
                                    let actualElementCount = availableBytes / elementSize
                                
                                    // Use the smaller of requested vs. received element counts
                                    let elementCountToParse = min actualElementCount requestedCount
                                
                                    printfn "[Parser] Parsing %d elements (available: %d, requested: %d) from offset %d" 
                                        elementCountToParse actualElementCount requestedCount valueOffset
                                
                                    parseValueByType dataType data valueOffset (Some elementCountToParse)
        | error -> (error, None)