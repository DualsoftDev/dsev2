namespace Ev2.AbProtocol.Protocol

open System
open System.Text
open Ev2.AbProtocol.Core

[<AutoOpen>]
/// <summary>
///     Provides utilities for enumerating Logix symbol metadata via the GetAttributeList CIP service.
///     The helpers in this module are used by <see cref="Client.ABClient"/> to build a tag catalog.
/// </summary>
module TagEnumerator =

    [<Literal>]
    let private SymbolClassId = 0x6Buy

    // ========================================
    // Primitive parsing helpers
    // ========================================
    
    let private readUInt32 (bytes: byte[]) =
        if bytes.Length >= 4 then BitConverter.ToUInt32(bytes, 0) else 0u

    let private readUInt16 (bytes: byte[]) =
        if bytes.Length >= 2 then BitConverter.ToUInt16(bytes, 0) else 0us

    let private readArrayDimensions (bytes: byte[]) =
        [|
            if bytes.Length >= 4 then BitConverter.ToInt32(bytes, 0) else 0
            if bytes.Length >= 8 then BitConverter.ToInt32(bytes, 4) else 0
            if bytes.Length >= 12 then BitConverter.ToInt32(bytes, 8) else 0
        |]
        |> Array.filter (fun d -> d > 0)

    let private readCountedString (bytes: byte[]) =
        if bytes.Length >= 2 then
            let length = BitConverter.ToUInt16(bytes, 0) |> int
            let actual = min length (bytes.Length - 2)
            Encoding.ASCII.GetString(bytes, 2, actual)
        else
            String.Empty

    let private baseDataType (symbolType: uint16) (elementSize: uint16) : DataType =
        match symbolType &&& 0x0FFFus with
        | 0x00C1us | 0x002Cus -> DataType.BOOL
        | 0x00C2us -> DataType.SINT
        | 0x00C3us -> DataType.INT
        | 0x00C4us -> DataType.DINT
        | 0x00C5us -> DataType.LINT
        | 0x00C6us -> DataType.USINT
        | 0x00C7us -> DataType.UINT
        | 0x00C8us -> DataType.UDINT
        | 0x00C9us -> DataType.ULINT
        | 0x00CAus -> DataType.REAL
        | 0x00CBus -> DataType.LREAL
        | 0x00D0us -> DataType.STRING 82
        | 0x00D1us | 0x00D2us | 0x00D3us | 0x00D4us -> DataType.BOOL
        | 0x0F83us -> DataType.TIMER
        | 0x0FCEus ->
            let maxLen = max 0 (int elementSize - 6)
            DataType.STRING (if maxLen <= 0 then 82 else maxLen)
        | _ -> DataType.DINT

    // ========================================
    // Attribute list handling
    // ========================================
    
    let private requestAttributeList (session: SessionManager) (instanceId: uint16 option) (attributeIds: uint16 list) =
        let service = PacketBuilder.buildGetAttributeListService SymbolClassId instanceId attributeIds
        match session.SendUnconnected service with
        | (NoError, Some (buffer, length)) -> 
            PacketParser.parseAttributeListResponse buffer length
        | (error, _) -> 
            (error, None)

    // ========================================
    // Class level lookups
    // ========================================
    
    let getClassStatistics (session: SessionManager) =
        match requestAttributeList session None [0x0002us; 0x0003us] with
        | (NoError, Some attributes) ->
            let maxId =
                attributes
                |> Array.tryFind (fun (id, _, _) -> id = 0x0002us)
                |> Option.map (fun (_, _, data) -> readUInt32 data)
                |> Option.defaultValue 0u
            let count =
                attributes
                |> Array.tryFind (fun (id, _, _) -> id = 0x0003us)
                |> Option.map (fun (_, _, data) -> readUInt32 data)
                |> Option.defaultValue 0u
            (NoError, Some (maxId, count))
        | (error, _) -> 
            (error, None)

    // ========================================
    // Find Next Instance (Service 0x4E)
    // ========================================
    
    let private findNextInstance (session: SessionManager) (startInstance: uint16) =
        let serviceData = BitConverter.GetBytes(startInstance)
        let service = PacketBuilder.buildGenericService 0x4Euy SymbolClassId None serviceData
        
        match session.SendUnconnected service with
        | (NoError, Some (buffer, length)) when length >= 2 ->
            let nextInstance = BitConverter.ToUInt16(buffer, 0)
            if nextInstance > startInstance then
                (NoError, Some nextInstance)
            else
                (NoError, None)
        | (NoError, _) -> (NoError, None)
        | (error, _) -> (error, None)

    // ========================================
    // Enumerate all instance identifiers (sequential)
    // ========================================
    
    let private getAllInstanceIds (session: SessionManager) =
        let instances = ResizeArray<uint16>()
        let mutable currentInstance = 0us
        let mutable continueSearch = true
        let mutable errorCount = 0
        
        while continueSearch && errorCount < 10 do
            match findNextInstance session currentInstance with
            | (NoError, Some nextId) ->
                instances.Add(nextId)
                currentInstance <- nextId
                errorCount <- 0
            | (NoError, None) ->
                continueSearch <- false
            | _ ->
                errorCount <- errorCount + 1
                if errorCount >= 10 then
                    continueSearch <- false
        
        if instances.Count > 0 then
            (NoError, Some (instances.ToArray()))
        else
            (NoError, None)

    // ========================================
    // Tag records
    // ========================================
    
    type TagRecord = {
        Id: uint16
        Name: string
        SymbolType: uint16
        ElementSize: uint16
        Dimensions: int[]
    }

    // ========================================
    // Tag record lookup
    // ========================================
    
    let getTagRecord (session: SessionManager) (instanceId: uint16) =
        let attributes = requestAttributeList session (Some instanceId) [0x0001us; 0x0002us; 0x0007us; 0x0008us]
        match attributes with
        | (NoError, Some attrs) ->
            let nameAttr = attrs |> Array.tryFind (fun (id, status, _) -> id = 0x0001us && status = 0us)
            match nameAttr with
            | None -> 
                (UnknownError (sprintf "Tag instance %d has no name attribute" instanceId), None)
            | Some (_, _, nameData) ->
                let name = readCountedString nameData
                let symbolType =
                    attrs
                    |> Array.tryFind (fun (id, status, _) -> id = 0x0002us && status = 0us)
                    |> Option.map (fun (_, _, data) -> readUInt16 data)
                    |> Option.defaultValue 0us
                let elementSize =
                    attrs
                    |> Array.tryFind (fun (id, status, _) -> id = 0x0007us && status = 0us)
                    |> Option.map (fun (_, _, data) -> readUInt16 data)
                    |> Option.defaultValue 0us
                let dims =
                    attrs
                    |> Array.tryFind (fun (id, status, _) -> id = 0x0008us && status = 0us)
                    |> Option.map (fun (_, _, data) -> readArrayDimensions data)
                    |> Option.defaultValue [||]
                
                let record = {
                    Id = instanceId
                    Name = name
                    SymbolType = symbolType
                    ElementSize = elementSize
                    Dimensions = dims
                }
                (NoError, Some record)
        | (error, _) -> 
            (error, None)

    // ========================================
    // Tag metadata helpers
    // ========================================
    
    let private shouldFilterTag (record: TagRecord) =
        let baseCode = record.SymbolType &&& 0x0FFFus
        record.ElementSize = 0us && 
        (baseCode = 0x0068us || baseCode = 0x0069us || baseCode = 0x0070us)

    // ========================================
    // Optimised tag enumeration (instance id baseline)
    // ========================================
    
    let private enumerateTagsViaInstanceIds (session: SessionManager) (instanceIds: uint16[]) =
        let records =
            instanceIds
            |> Array.choose (fun instanceId ->
                match getTagRecord session instanceId with
                | (NoError, Some record) when not (String.IsNullOrWhiteSpace record.Name) ->
                    if not (shouldFilterTag record) then Some record else None
                | _ -> None
            )
        
        if records.Length > 0 then
            (NoError, Some records)
        else
            (UnknownError "No valid tags found", None)

    // ========================================
    // Bulk enumeration (sequential scan)
    // ========================================
    
    let private enumerateTagsSequential (session: SessionManager) =
        match getClassStatistics session with
        | (NoError, Some (maxId, count)) ->
            let results = ResizeArray<TagRecord>()
            let mutable instance = 1us
            let mutable consecutiveErrors = 0
            let maxConsecutiveErrors = 20
            
            let safeMaxInstance =
                if maxId = 0u then uint32 UInt16.MaxValue else maxId
            let maxInstance =
                safeMaxInstance |> min (uint32 UInt16.MaxValue) |> uint16
            let targetCount =
                if count = 0u then safeMaxInstance else min count safeMaxInstance
                |> min (uint32 Int32.MaxValue) |> int
            
            while instance <= maxInstance && 
                  results.Count < targetCount && 
                  consecutiveErrors < maxConsecutiveErrors do
                
                match getTagRecord session instance with
                | (NoError, Some record) when not (String.IsNullOrWhiteSpace record.Name) ->
                    if not (shouldFilterTag record) then
                        results.Add(record)
                    consecutiveErrors <- 0
                | _ ->
                    consecutiveErrors <- consecutiveErrors + 1
                
                instance <- instance + 1us
            
            if results.Count > 0 then
                (NoError, Some (results.ToArray()))
            else
                (UnknownError "No tags found", None)
        | (error, _) -> 
            (error, None)

    // ========================================
    // Smart tag enumeration (auto optimisation)
    // ========================================
    
    let enumerateTags (session: SessionManager) =
        match getAllInstanceIds session with
        | (NoError, Some instanceIds) when instanceIds.Length > 0 ->
            enumerateTagsViaInstanceIds session instanceIds
        | _ ->
            enumerateTagsSequential session

    // ========================================
    // TagInfo conversion
    // ========================================
    
    let toTagInfo (record: TagRecord) =
        let baseType = baseDataType record.SymbolType record.ElementSize
        let bitStringInfo =
            match record.SymbolType &&& 0x0FFFus with
            | 0x00D1us -> Some 8
            | 0x00D2us -> Some 16
            | 0x00D3us -> Some 32
            | 0x00D4us -> Some 64
            | _ -> None
        
        let dims, dataType =
            match bitStringInfo with
            | Some bitsPerElement ->
                let baseDims =
                    if record.Dimensions.Length = 0 then [| 1 |]
                    else record.Dimensions |> Array.map (fun d -> max 1 d)
                let elementCount = baseDims |> Array.fold (*) 1
                let totalBits = max bitsPerElement (bitsPerElement * elementCount)
                let adjustedDims = [| totalBits |]
                let dt = DataType.ARRAY(DataType.BOOL, totalBits)
                adjustedDims, dt
            | None ->
                let dims =
                    if record.Dimensions.Length = 0 then [||]
                    else record.Dimensions |> Array.filter (fun d -> d > 0)
                
                let concreteDims =
                    if dims.Length = 0 && (record.SymbolType &&& 0x2000us) <> 0us then
                        [| 0 |]
                    else
                        dims
                
                let dt = concreteDims |> Array.fold (fun dt len -> DataType.ARRAY(dt, len)) baseType
                concreteDims, dt
        
        { 
            Name = record.Name
            DataType = dataType
            ArrayDimensions = dims
            AccessLevel = ReadWrite
            Description = None
            Alias = None 
        }

    // ========================================
    // Convenience enumeration helpers
    // ========================================
    
    let enumerateTagsWithFilter (session: SessionManager) (filter: TagRecord -> bool) =
        match enumerateTags session with
        | (NoError, Some tags) ->
            let filtered = tags |> Array.filter filter
            (NoError, Some filtered)
        | (error, _) -> (error, None)
    
    let findTagByName (session: SessionManager) (tagName: string) =
        match enumerateTags session with
        | (NoError, Some tags) ->
            tags 
            |> Array.tryFind (fun tag -> 
                tag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase))
            |> function
                | Some tag -> (NoError, Some tag)
                | None -> (InvalidTag tagName, None)
        | (error, _) -> (error, None)
    
    let findTagsByPattern (session: SessionManager) (pattern: string) =
        match enumerateTags session with
        | (NoError, Some tags) ->
            let matches = 
                tags |> Array.filter (fun tag -> 
                    tag.Name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
            (NoError, Some matches)
        | (error, _) -> (error, None)
    
    let findTagsByDataType (session: SessionManager) (dataType: DataType) =
        match enumerateTags session with
        | (NoError, Some tags) ->
            let matches = 
                tags |> Array.filter (fun tag -> 
                    baseDataType tag.SymbolType tag.ElementSize = dataType)
            (NoError, Some matches)
        | (error, _) -> (error, None)
    
    let enumerateArrayTags (session: SessionManager) =
        enumerateTagsWithFilter session (fun tag -> 
            tag.Dimensions.Length > 0 && tag.Dimensions |> Array.forall (fun d -> d > 0))
    
    let enumerateScalarTags (session: SessionManager) =
        enumerateTagsWithFilter session (fun tag -> tag.Dimensions.Length = 0)
    
    let getTagStatistics (session: SessionManager) =
        match enumerateTags session with
        | (NoError, Some tags) ->
            let stats = {|
                TotalCount = tags.Length
                ArrayCount = tags |> Array.filter (fun t -> t.Dimensions.Length > 0) |> Array.length
                ScalarCount = tags |> Array.filter (fun t -> t.Dimensions.Length = 0) |> Array.length
                TypeDistribution = 
                    tags 
                    |> Array.groupBy (fun t -> baseDataType t.SymbolType t.ElementSize)
                    |> Array.map (fun (dt, items) -> dt, items.Length)
                    |> Map.ofArray
            |}
            (NoError, Some stats)
        | (error, _) -> (error, None)
    
    let enumerateTagInfos (session: SessionManager) =
        match enumerateTags session with
        | (NoError, Some tags) ->
            let tagInfos = 
                tags 
                |> Array.Parallel.map toTagInfo
            (NoError, Some tagInfos)
        | (error, _) -> (error, None)

    // ========================================
    // Developer utilities
    // ========================================
    
    let printTagRecord (record: TagRecord) =
        printfn "Tag ID: %d" record.Id
        printfn "  Name: %s" record.Name
        printfn "  SymbolType: 0x%04X" record.SymbolType
        printfn "  ElementSize: %d" record.ElementSize
        printfn "  Dimensions: %A" record.Dimensions
        let tagInfo = toTagInfo record
        printfn "  DataType: %A" tagInfo.DataType
    
    let printAllTags (session: SessionManager) =
        match enumerateTags session with
        | (NoError, Some tags) ->
            printfn "Found %d tags:" tags.Length
            tags |> Array.iter printTagRecord
            NoError
        | (error, _) -> 
            printfn "Error enumerating tags: %A" error
            error
    
    let getTagNames (session: SessionManager) =
        match enumerateTags session with
        | (NoError, Some tags) ->
            let names = tags |> Array.map (fun t -> t.Name)
            (NoError, Some names)
        | (error, _) -> (error, None)
    
    let enumerateTagRange (session: SessionManager) (startInstance: uint16) (endInstance: uint16) =
        let results = 
            [| startInstance .. endInstance |]
            |> Array.choose (fun instance ->
                match getTagRecord session instance with
                | (NoError, Some record) when not (String.IsNullOrWhiteSpace record.Name) ->
                    if not (shouldFilterTag record) then Some record else None
                | _ -> None
            )
        
        if results.Length > 0 then
            (NoError, Some results)
        else
            (UnknownError "No tags found in specified range", None)
    
    // ========================================
    // Cache system
    // ========================================
    
    let private tagCache = System.Collections.Concurrent.ConcurrentDictionary<string, TagRecord[] * DateTime>()
    let private cacheTimeout = TimeSpan.FromMinutes(5.0)
    
    let enumerateTagsCached (session: SessionManager) (forceRefresh: bool) =
        let cacheKey = sprintf "%s:%d" session.Config.IpAddress session.Config.Port
        
        if not forceRefresh then
            match tagCache.TryGetValue(cacheKey) with
            | true, (tags, timestamp) when DateTime.Now - timestamp < cacheTimeout ->
                (NoError, Some tags)
            | _ ->
                match enumerateTags session with
                | (NoError, Some tags) as result ->
                    tagCache.[cacheKey] <- (tags, DateTime.Now)
                    result
                | result -> result
        else
            match enumerateTags session with
            | (NoError, Some tags) as result ->
                tagCache.[cacheKey] <- (tags, DateTime.Now)
                result
            | result -> result
    
    let clearCache (session: SessionManager) =
        let cacheKey = sprintf "%s:%d" session.Config.IpAddress session.Config.Port
        tagCache.TryRemove(cacheKey) |> ignore
    
    let clearAllCaches () =
        tagCache.Clear()
