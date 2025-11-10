namespace Ev2.AbProtocol.Protocol

open System
open System.Text
open Ev2.AbProtocol.Core

/// <summary>
///     Constructs EtherNet/IP and CIP payloads used throughout the client.
///     Keeping the builders together makes it easier to reason about wire formatting.
/// </summary>
module PacketBuilder =
    
    // ========================================
    // Helper functions
    // ========================================
    
    /// Pads a buffer to an even byte count so the payload remains word aligned.
    let private ensureWordAlignment (data: ResizeArray<byte>) =
        if data.Count % 2 = 1 then
            data.Add(0uy)
        data.ToArray()
    
    /// Ensures the provided buffer is word aligned by appending a single byte when necessary.
    let private padToWordBoundary (bytes: byte[]) =
        if bytes.Length % 2 = 1 then
            Array.append bytes [| 0uy |]
        else
            bytes
    
    /// Converts a sequence to a ResizeArray and enforces word alignment.
    let private toAlignedArray (items: byte seq) =
        let list = ResizeArray<byte>(items)
        ensureWordAlignment list
    
    // ========================================
    // EtherNet/IP header builder
    // ========================================
    
    /// Builds a standard EtherNet/IP header including command, session and context metadata.
    let buildEIPHeader (command: uint16) (sessionHandle: uint32) (context: uint64) (dataLength: int) =
        let buffer = Array.zeroCreate<byte> Constants.EIP.HeaderSize
        
        // Command (2 bytes)
        BitConverter.GetBytes(command).CopyTo(buffer, 0)
        // Length (2 bytes)
        BitConverter.GetBytes(uint16 dataLength).CopyTo(buffer, 2)
        // Session Handle (4 bytes)
        BitConverter.GetBytes(sessionHandle).CopyTo(buffer, 4)
        // Status (4 bytes) - 0 for requests
        // Sender Context (8 bytes)
        BitConverter.GetBytes(context).CopyTo(buffer, 12)
        // Options (4 bytes) - 0
        
        buffer
    
    // ========================================
    // Session management packets
    // ========================================
    
    /// Builds the payload for the Register Session command.
    let buildRegisterSession() =
        let header = buildEIPHeader Constants.EIP.RegisterSession 0u 0UL 4
        let data = [| 0x01uy; 0x00uy; 0x00uy; 0x00uy |]  // Protocol version 1
        Array.append header data
    
    /// Builds the payload for the Unregister Session command.
    let buildUnregisterSession (sessionHandle: uint32) =
        buildEIPHeader Constants.EIP.UnregisterSession sessionHandle 0UL 0
    
    // ========================================
    // Discovery packets
    // ========================================
    
    /// Builds a ListIdentity request.
    let buildListIdentity() =
        buildEIPHeader Constants.EIP.ListIdentity 0u 0UL 0
    
    /// Builds a ListServices request.
    let buildListServices() =
        buildEIPHeader Constants.EIP.ListServices 0u 0UL 0
    
    // ========================================
    // CIP path builders
    // ========================================
    
    /// Creates an ANSI extended symbol segment for a tag name.
    let buildSymbolSegment (tagName: string) =
        let tagBytes = Encoding.ASCII.GetBytes(tagName)
        let tagLength = tagBytes.Length
        
        let segment = ResizeArray<byte>()
        segment.Add(Constants.PathSegment.Symbolic)  // 0x91
        segment.Add(byte tagLength)
        segment.AddRange(tagBytes)
        
        ensureWordAlignment segment
    
    /// Creates a backplane/slot path segment.
    let buildConnectionPath (slot: byte) =
        [| 
            0x01uy  // Backplane port
            slot    // Slot number
        |]
    
    /// Creates a class/instance path segment.
    let buildClassInstancePath (classId: byte) (instanceId: uint16 option) =
        let path = ResizeArray<byte>()
        
        // Class segment
        path.Add(Constants.PathSegment.LogicalClass)
        path.Add(classId)
        
        // Instance segment (optional)
        match instanceId with
        | None -> ()
        | Some id when id <= 0xFFus ->
            // 8-bit instance
            path.Add(Constants.PathSegment.LogicalInstance)
            path.Add(byte id)
        | Some id ->
            // 16-bit extended instance
            path.Add(Constants.PathSegment.ExtendedLogical16Bit)
            path.Add(0x00uy)  // Reserved
            path.AddRange(BitConverter.GetBytes(id))
        
        ensureWordAlignment path
    
    /// Creates an attribute path segment.
    let buildAttributePath (attributeId: uint16) =
        if attributeId <= 0xFFus then
            [| Constants.PathSegment.LogicalAttribute; byte attributeId |]
        else
            Array.concat [|
                [| Constants.PathSegment.ExtendedLogical16Bit; 0x00uy |]
                BitConverter.GetBytes(attributeId)
            |]
    
    // ========================================
    // CIP service builders
    // ========================================
    
/// Builds a Read Tag service payload (supports array indexing).
    let buildReadTagService tagName elementCount =
        // Parse an optional array index: Tag[123] -> (Tag, Some 123)
        let (baseTagName, arrayIndex) =
            let pattern = @"^(.+?)\[(\d+)\]$"
            let m = System.Text.RegularExpressions.Regex.Match(tagName, pattern)
            if m.Success then
                (m.Groups.[1].Value, Some (int m.Groups.[2].Value))
            else
                (tagName, None)
    
        let tagBytes = Encoding.ASCII.GetBytes(baseTagName)
        let tagLength = byte tagBytes.Length
    
        // Compose the EPATH
        let epath = 
            let segments = ResizeArray<byte>()
        
            // 1. Symbolic segment (tag name)
            segments.Add(0x91uy)  // Symbolic segment type
            segments.Add(tagLength)
            segments.AddRange(tagBytes)
        
            // Pad when the length is odd
            if tagLength % 2uy = 1uy then
                segments.Add(0x00uy)
        
            // 2. Element segment (array index)
            match arrayIndex with
            | Some index ->
                if index <= 255 then
                    // 8-bit element
                    segments.Add(0x28uy)  // 8-bit element segment
                    segments.Add(byte index)
                elif index <= 65535 then
                    // 16-bit element
                    segments.Add(0x29uy)  // 16-bit element segment
                    segments.Add(0x00uy)  // Padding
                    segments.AddRange(BitConverter.GetBytes(uint16 index))
                else
                    // 32-bit element
                    segments.Add(0x2Auy)  // 32-bit element segment
                    segments.Add(0x00uy)  // Padding
                    segments.AddRange(BitConverter.GetBytes(uint32 index))
            | None -> ()
        
            segments.ToArray()
    
        let pathSize = byte (epath.Length / 2)
    
        // CIP service data
        let serviceData = 
            let data = ResizeArray<byte>()
            data.Add(Constants.CIP.ReadTag)  // 0x4C
            data.Add(pathSize)
            data.AddRange(epath)
            data.AddRange(BitConverter.GetBytes(uint16 elementCount))
            data.ToArray()
    
        serviceData
/// Builds a Write Tag service payload (supports array indexing).
    let buildWriteTagService tagName (dataType: DataType) (values: byte[]) =
        // Parse an optional array index: Tag[123] -> (Tag, Some 123)
        let (baseTagName, arrayIndex) =
            let pattern = @"^(.+?)\[(\d+)\]$"
            let m = System.Text.RegularExpressions.Regex.Match(tagName, pattern)
            if m.Success then
                (m.Groups.[1].Value, Some (int m.Groups.[2].Value))
            else
                (tagName, None)

        let tagBytes = Encoding.ASCII.GetBytes(baseTagName)
        let tagLength = byte tagBytes.Length

        // Compose the EPATH
        let epath = 
            let segments = ResizeArray<byte>()
    
            // 1. Symbolic segment (tag name)
            segments.Add(0x91uy)
            segments.Add(tagLength)
            segments.AddRange(tagBytes)
    
            // Pad when the length is odd
            if tagLength % 2uy = 1uy then
                segments.Add(0x00uy)
    
            // 2. Element segment (array index)
            match arrayIndex with
            | Some index ->
                if index <= 255 then
                    segments.Add(0x28uy)  // 8-bit element
                    segments.Add(byte index)
                elif index <= 65535 then
                    segments.Add(0x29uy)  // 16-bit element
                    segments.Add(0x00uy)
                    segments.AddRange(BitConverter.GetBytes(uint16 index))
                else
                    segments.Add(0x2Auy)  // 32-bit element
                    segments.Add(0x00uy)
                    segments.AddRange(BitConverter.GetBytes(uint32 index))
            | None -> ()
    
            segments.ToArray()

        let pathSize = byte (epath.Length / 2)

        // CIP service data
        let elementCount = 
            if dataType.ByteSize > 0 then values.Length / dataType.ByteSize
            else 1

        Array.concat [|
            [| Constants.CIP.WriteTag |]
            [| pathSize |]
            epath
            [| dataType.CIPCode; 0x00uy |]  // Data type (2 bytes)
            BitConverter.GetBytes(uint16 elementCount)  // Element count
            values  // Data
        |]
    /// Builds a Get Attribute List service payload.
    let buildGetAttributeListService (classId: byte) (instanceId: uint16 option) (attributeIds: uint16 list) =
        let path = buildClassInstancePath classId instanceId
        let attributeCount = uint16 attributeIds.Length
        let attributesBytes =
            attributeIds
            |> List.collect (fun attr -> BitConverter.GetBytes(attr) |> Array.toList)
            |> List.toArray
        
        Array.concat [|
            [| Constants.CIP.GetAttributeList |]
            [| byte (path.Length / 2) |]
            path
            BitConverter.GetBytes(attributeCount)
            attributesBytes
        |]
    
    /// Builds a Get Attribute Single service payload.
    let buildGetAttributeSingleService (classId: byte) (instanceId: uint16) (attributeId: uint16) =
        let classInstancePath = buildClassInstancePath classId (Some instanceId)
        let attributePath = buildAttributePath attributeId
        let fullPath = Array.append classInstancePath attributePath
        
        Array.concat [|
            [| Constants.CIP.GetAttributeSingle |]
            [| byte (fullPath.Length / 2) |]
            fullPath
        |]
    
    // ========================================
    // Multiple Service Packet builder
    // ========================================
    
    /// Builds a Multiple Service Packet request.
    let buildMultipleServicePacket (services: byte[][]) =
        if services.Length = 0 then
            invalidArg "services" "Services array cannot be empty"
        
        if services.Length > Constants.Performance.MaxServicesPerMultiPacket then
            invalidArg "services" 
                (sprintf "Too many services: %d (max: %d)" 
                    services.Length Constants.Performance.MaxServicesPerMultiPacket)
        
        let packet = ResizeArray<byte>()
        
        // Multiple Service header
        packet.Add(Constants.CIP.MultipleServicePacket)  // 0x0A
        packet.Add(0x02uy)  // Path size
        packet.Add(Constants.PathSegment.LogicalClass)  // 0x20
        packet.Add(Constants.ClassId.MessageRouter)  // 0x02
        packet.Add(Constants.PathSegment.LogicalInstance)  // 0x24
        packet.Add(0x01uy)  // Instance 1
        
        // Service count
        packet.AddRange(BitConverter.GetBytes(uint16 services.Length))
        
        // Calculate offsets
        let offsetTableSize = services.Length * 2  // Each offset entry uses 2 bytes
        let mutable currentOffset = uint16 (2 + offsetTableSize)  // 2 = service count
        let offsets = ResizeArray<uint16>()
        
        for service in services do
            offsets.Add(currentOffset)
            currentOffset <- currentOffset + uint16 service.Length
        
        // Add offset list
        for offset in offsets do
            packet.AddRange(BitConverter.GetBytes(offset))
        
        // Add services
        for service in services do
            packet.AddRange(service)
        
        packet.ToArray()
    
    // ========================================
    // CPF (Common Packet Format) builder
    // ========================================
    
    /// Creates a CPF container for an unconnected message.
    let buildCPFUnconnected (data: byte[]) =
        Array.concat [|
            BitConverter.GetBytes(0u)  // Interface handle
            BitConverter.GetBytes(0us)  // Timeout
            BitConverter.GetBytes(2us)  // Item count
            BitConverter.GetBytes(Constants.EIP.ItemNull)  // NULL address item
            BitConverter.GetBytes(0us)  // Length
            BitConverter.GetBytes(Constants.EIP.ItemUnconnectedData)  // UCMM item
            BitConverter.GetBytes(uint16 data.Length)
            data
        |]
    
    /// Creates a CPF container for a connected message.
    let buildCPFConnected (connectionId: uint32) (sequenceNumber: uint16) (data: byte[]) =
        Array.concat [|
            BitConverter.GetBytes(0u)  // Interface handle
            BitConverter.GetBytes(0us)  // Timeout
            BitConverter.GetBytes(2us)  // Item count
            BitConverter.GetBytes(Constants.EIP.ItemConnectionAddress)  // Connected address
            BitConverter.GetBytes(4us)  // Length
            BitConverter.GetBytes(connectionId)
            BitConverter.GetBytes(Constants.EIP.ItemConnectedData)  // Connected data
            BitConverter.GetBytes(uint16 (data.Length + 2))
            BitConverter.GetBytes(sequenceNumber)
            data
        |]
    
    /// Adds an item to a CPF container (extensible builder).
    let buildCPFWithItems (items: (uint16 * byte[])[]) =
        let packet = ResizeArray<byte>()
        
        // CPF header
        packet.AddRange(BitConverter.GetBytes(0u))  // Interface handle
        packet.AddRange(BitConverter.GetBytes(0us))  // Timeout
        packet.AddRange(BitConverter.GetBytes(uint16 items.Length))  // Item count
        
        // Append each item
        for (itemType, itemData) in items do
            packet.AddRange(BitConverter.GetBytes(itemType))
            packet.AddRange(BitConverter.GetBytes(uint16 itemData.Length))
            packet.AddRange(itemData)
        
        packet.ToArray()
    
    // ========================================
    // Unconnected Send Wrapper
    // ========================================
    
    /// Wraps a payload in an Unconnected Send request.
    let buildUnconnectedSend (cipData: byte[]) (connectionPath: byte[]) =
        let baseData = Array.concat [|
            [| 0x52uy |]  // Unconnected Send service
            [| 0x02uy |]  // Path size
            [| Constants.PathSegment.LogicalClass; Constants.ClassId.ConnectionManager |]
            [| Constants.PathSegment.LogicalInstance; 0x01uy |]
            [| 0x0Auy |]  // Priority/Time tick
            [| 0x0Euy |]  // Timeout ticks
            BitConverter.GetBytes(uint16 cipData.Length)
            cipData
        |]
        
        if connectionPath.Length > 0 then
            Array.concat [|
                baseData
                [| byte (connectionPath.Length / 2); 0x00uy |]  // Route path size
                connectionPath
            |]
        else
            baseData
    
    // ========================================
    // Complete request builders
    // ========================================
    
    /// Builds a fully encapsulated Read Tag request.
    let buildReadTagRequest (sessionHandle: uint32) (context: uint64) (tagName: string) (elementCount: int) =
        let readService = buildReadTagService tagName elementCount
        let cpf = buildCPFUnconnected readService
        let header = buildEIPHeader Constants.EIP.UnconnectedSend sessionHandle context cpf.Length
        Array.append header cpf
    
    /// Builds a fully encapsulated Write Tag request.
    let buildWriteTagRequest (sessionHandle: uint32) (context: uint64) (tagName: string) (dataType: DataType) (values: byte[]) =
        let writeService = buildWriteTagService tagName dataType values
        let cpf = buildCPFUnconnected writeService
        let header = buildEIPHeader Constants.EIP.UnconnectedSend sessionHandle context cpf.Length
        Array.append header cpf
    
/// Builds a batch read request using the Multiple Service Packet.
    let buildBatchReadRequest (_sessionHandle: uint32) (_context: uint64) (requests: ReadRequest[]) =
        if requests.Length = 0 then
            [||]
        else
            requests
            |> Array.chunkBySize Constants.Performance.MaxServicesPerMultiPacket
            |> Array.map (fun chunk ->
                let services =
                    chunk
                    |> Array.map (fun req ->
                        let tagName =
                            if req.StartIndex > 0 && not (req.TagName.Contains "[") then
                                sprintf "%s[%d]" req.TagName req.StartIndex
                            else
                                req.TagName
                        buildReadTagService tagName req.ElementCount)
                buildMultipleServicePacket services)
    
    /// Builds a batch write request using the Multiple Service Packet.
    let buildBatchWriteRequest (_sessionHandle: uint32) (_context: uint64) (requests: (string * DataType * byte[])[]) =
        if requests.Length = 0 then
            [||]
        else
            requests
            |> Array.chunkBySize Constants.Performance.MaxServicesPerMultiPacket
            |> Array.map (fun chunk ->
                let services =
                    chunk
                    |> Array.map (fun (tag, dataType, values) ->
                        buildWriteTagService tag dataType values)
                buildMultipleServicePacket services)
        
    let buildReadTagFragmentedService tagName elementCount (byteOffset: uint32) =

        printfn "[PacketBuilder] Building fragmented read:"
        printfn "  Tag: %s" tagName
        printfn "  Elements: %d" elementCount
        printfn "  Byte Offset: %d (0x%08X)" byteOffset byteOffset
    
        let tagBytes = Encoding.ASCII.GetBytes(tagName)
        let tagLength = byte tagBytes.Length
    
        // Service code
        let serviceCode = Constants.CIP.ReadTagFragmented  // 0x52
    
        // Build the EPATH
        let epath = [|
            yield 0x91uy  // Symbolic segment
            yield tagLength
            yield! tagBytes
            if tagBytes.Length % 2 = 1 
                then yield 0x00uy  // Padding to keep the EPATH aligned
        |]
    
        // Build the service data
        let serviceData = [|
            yield serviceCode
            yield byte ((epath.Length / 2) &&& 0xFF)  // Path size in words
            yield! epath
            yield! BitConverter.GetBytes(uint16 elementCount)  // Element count (2 bytes)
            yield! BitConverter.GetBytes(byteOffset)        // Offset (4 bytes) - element units
        |]
    
        printfn "  Packet size: %d bytes" serviceData.Length
        printfn "  Service code: 0x%02X" serviceCode
    
        serviceData
    // ========================================
    // Utility helpers
    // ========================================
    
    /// Calculates the total packet size.
    let calculatePacketSize (services: byte[][]) =
        let headerSize = Constants.EIP.HeaderSize
        let cpfHeaderSize = 8  // Interface handle + timeout + item count
        let itemHeaderSize = 4  // Type + length
        let multiServiceHeaderSize = 8  // Service code + path + service count
        let offsetTableSize = services.Length * 2
        let servicesSize = services |> Array.sumBy (fun s -> s.Length)
        
        headerSize + cpfHeaderSize + itemHeaderSize + multiServiceHeaderSize + 
        offsetTableSize + servicesSize
    
    /// Verifies whether a packet exceeds the allowed size.
    let exceedsMaxSize (services: byte[][]) =
        calculatePacketSize services > Constants.Performance.MaxUnconnectedPacketSize


    /// Builds a generic CIP service (for Service 0x4E, Find Next Instance, etc.)
    let buildGenericService (serviceCode: byte) (classId: byte) (instanceId: uint16 option) (data: byte[]) =
        let path = buildClassInstancePath classId instanceId
    
        Array.concat [|
            [| serviceCode |]
            [| byte (path.Length / 2) |]
            path
            data
        |]