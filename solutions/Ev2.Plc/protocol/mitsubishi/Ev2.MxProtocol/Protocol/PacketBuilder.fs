namespace Ev2.MxProtocol.Protocol

open System
open Ev2.MxProtocol.Core

/// <summary>
///     Builds MELSEC command payloads. Keeping the logic centralised here keeps the client code concise and ensures
///     consistent validation across request types.
/// </summary>
module PacketBuilder =

    // ---------------------------------------------------------------------
    // Validation helpers
    // ---------------------------------------------------------------------

    let private ensureCountInRange (name: string) (value: int) (minValue: int) (maxValue: int) =
        if value < minValue || value > maxValue then
            invalidArg name (sprintf "%s must be between %d and %d (actual %d)." name minValue maxValue value)

    let private ensureValuesNotEmpty (values: 'a array) name =
        if values.Length = 0 then invalidArg name (sprintf "%s collection cannot be empty." name)

    // ---------------------------------------------------------------------
    // Batch operations (0401 / 1401)
    // ---------------------------------------------------------------------

    /// Builds a batch read request (command: 0401).
    let buildBatchRead (device: DeviceCode) (headAddress: int) (count: uint16) (isBitUnit: bool) =
        let subcommand = if isBitUnit then SubcommandCode.BitUnits else SubcommandCode.WordUnits
        let maxCount =
            if isBitUnit then
                if device.IsWordDevice() then MaxBatchReadWords else MaxBatchReadBits
            else
                MaxBatchReadWords

        ensureCountInRange "count" (int count) 1 maxCount

        let payload =
            [|
                yield! Frame.Device.encodeBinary device headAddress isBitUnit
                yield! Frame.toBytes count
            |]

        { Command = CommandCode.BatchRead
          Subcommand = subcommand
          Payload = payload }

    /// Builds a batch write request (command: 1401).
    let buildBatchWrite (device: DeviceCode) (headAddress: int) (values: uint16 array) (isBitUnit: bool) =
        ensureValuesNotEmpty values "values"
        
        // Validate count against protocol limits
        let maxCount =
            if isBitUnit then
                if device.IsWordDevice() then MaxBatchReadWords else MaxBatchWriteBits
            else
                MaxBatchReadWords
        
        ensureCountInRange "values.Length" values.Length 1 maxCount
        
        let subcommand = if isBitUnit then SubcommandCode.BitUnits else SubcommandCode.WordUnits

        let payload =
            [|
                yield! Frame.Device.encodeBinary device headAddress isBitUnit
                yield! Frame.toBytes (uint16 values.Length)
                for value in values do
                    yield! Frame.toBytes value
            |]

        { Command = CommandCode.BatchWrite
          Subcommand = subcommand
          Payload = payload }

    // ---------------------------------------------------------------------
    // Random access (0403 / 1402)
    // ---------------------------------------------------------------------

    let buildRandomRead (devices: RandomDeviceAccess array) =
        ensureCountInRange "devices" devices.Length 1 MaxRandomReadPoints

        let wordCount = devices |> Array.filter (fun d -> d.AccessSize = 0uy) |> Array.length
        let dwordCount = devices |> Array.filter (fun d -> d.AccessSize = 1uy) |> Array.length

        let payload =
            [|
                yield byte wordCount
                yield byte dwordCount
                for device in devices do
                    yield! Frame.Device.encodeRandomBinary device.DeviceCode device.DeviceNumber (device.AccessSize = 1uy)
            |]

        { Command = CommandCode.RandomRead
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    let buildRandomWriteBit (devices: (DeviceCode * int * bool) array) =
        ensureCountInRange "devices" devices.Length 1 MaxRandomWriteBitPoints

        let payload =
            [|
                yield byte devices.Length
                for (deviceCode, address, desired) in devices do
                    yield! Frame.Device.encodeBinary deviceCode address true  // Bit-unit for bit devices
                    yield if desired then 0x01uy else 0x00uy
            |]

        { Command = CommandCode.RandomWrite
          Subcommand = SubcommandCode.BitUnits
          Payload = payload }

    let buildRandomWriteWord (devices: (RandomDeviceAccess * uint16 array) array) =
        ensureValuesNotEmpty devices "devices"
        ensureCountInRange "devices" devices.Length 1 MaxRandomReadPoints

        let payload =
            [|
                let wordCount = devices |> Array.filter (fun (d, _) -> d.AccessSize = 0uy) |> Array.length
                let dwordCount = devices |> Array.filter (fun (d, _) -> d.AccessSize = 1uy) |> Array.length
                yield byte wordCount
                yield byte dwordCount
                for (device, values) in devices do
                    yield! Frame.Device.encodeRandomBinary device.DeviceCode device.DeviceNumber (device.AccessSize = 1uy)
                    for value in values do
                        yield! Frame.toBytes value
            |]

        { Command = CommandCode.RandomWrite
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    // ---------------------------------------------------------------------
    // Multi-block operations (0406 / 1406)
    // ---------------------------------------------------------------------

    let buildMultiBlockRead (wordBlocks: BlockAccess array) (bitBlocks: BlockAccess array) =
        let totalWords = wordBlocks |> Array.sumBy (fun block -> int block.PointCount)
        let totalBits = bitBlocks |> Array.sumBy (fun block -> int block.PointCount)
        
        // Validate each block type separately according to MELSEC specs
        ensureCountInRange "totalWords" totalWords 0 MaxBatchReadWords
        ensureCountInRange "totalBits" totalBits 0 MaxMultiBlockBits

        let payload =
            [|
                yield byte wordBlocks.Length
                yield byte bitBlocks.Length

                for block in wordBlocks do
                    yield! Frame.Device.encodeBinary block.DeviceCode block.HeadNumber false  // Word-unit
                    yield! Frame.toBytes block.PointCount

                for block in bitBlocks do
                    yield! Frame.Device.encodeBinary block.DeviceCode block.HeadNumber true  // Bit-unit
                    yield! Frame.toBytes block.PointCount
            |]

        { Command = CommandCode.MultiBlockRead
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    let buildMultiBlockWrite (wordBlocks: (BlockAccess * uint16 array) array) (bitBlocks: (BlockAccess * uint16 array) array) =
        // Validate block counts
        let totalWords = wordBlocks |> Array.sumBy (fun (block, _) -> int block.PointCount)
        let totalBits = bitBlocks |> Array.sumBy (fun (block, _) -> int block.PointCount)
        
        ensureCountInRange "totalWords" totalWords 0 MaxBatchReadWords
        ensureCountInRange "totalBits" totalBits 0 MaxMultiBlockBits
        
        let payload =
            [|
                yield byte wordBlocks.Length
                yield byte bitBlocks.Length

                for (block, _) in wordBlocks do
                    yield! Frame.Device.encodeBinary block.DeviceCode block.HeadNumber false  // Word-unit
                    yield! Frame.toBytes block.PointCount

                for (block, _) in bitBlocks do
                    yield! Frame.Device.encodeBinary block.DeviceCode block.HeadNumber true  // Bit-unit
                    yield! Frame.toBytes block.PointCount

                for (_, values) in wordBlocks do
                    for value in values do
                        yield! Frame.toBytes value

                for (_, values) in bitBlocks do
                    for value in values do
                        yield! Frame.toBytes value
            |]

        { Command = CommandCode.MultiBlockWrite
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    // ---------------------------------------------------------------------
    // Buffer memory operations (0613 / 1613)
    // ---------------------------------------------------------------------

    let buildBufferRead (startAddress: uint16) (count: uint16) =
        ensureCountInRange "count" (int count) 1 MaxBatchReadWords

        let payload =
            [|
                yield! Frame.toBytes startAddress
                yield! Frame.toBytes count
            |]

        { Command = CommandCode.BufferRead
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    let buildBufferWrite (startAddress: uint16) (values: uint16 array) =
        ensureValuesNotEmpty values "values"
        ensureCountInRange "values.Length" values.Length 1 MaxBatchReadWords

        let payload =
            [|
                yield! Frame.toBytes startAddress
                yield! Frame.toBytes (uint16 values.Length)
                for value in values do
                    yield! Frame.toBytes value
            |]

        { Command = CommandCode.BufferWrite
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    // ---------------------------------------------------------------------
    // Intelligent function module operations (0601 / 1601)
    // ---------------------------------------------------------------------

    let buildIntelligentRead (moduleSlot: uint16) (startAddress: uint16) (count: uint16) =
        ensureCountInRange "count" (int count) 1 MaxBatchReadWords
        
        let payload =
            [|
                yield! Frame.toBytes moduleSlot
                yield! Frame.toBytes startAddress
                yield! Frame.toBytes count
            |]

        { Command = CommandCode.IntelligentRead
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    let buildIntelligentWrite (moduleSlot: uint16) (startAddress: uint16) (values: uint16 array) =
        ensureValuesNotEmpty values "values"
        ensureCountInRange "values.Length" values.Length 1 MaxBatchReadWords

        let payload =
            [|
                yield! Frame.toBytes moduleSlot
                yield! Frame.toBytes startAddress
                yield! Frame.toBytes (uint16 values.Length)
                for value in values do
                    yield! Frame.toBytes value
            |]

        { Command = CommandCode.IntelligentWrite
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    // ---------------------------------------------------------------------
    // CPU control operations (1001 - 1006, 0101)
    // ---------------------------------------------------------------------

    /// Remote RUN with proper payload (Mode, Clear mode, Fixed value)
    let buildRemoteRun () =
        { Command = CommandCode.RemoteRun
          Subcommand = SubcommandCode.WordUnits
          Payload = [| 
              0x01uy; 0x00uy  // Mode = 1 (RUN)
              0x00uy; 0x00uy  // Clear mode = 0 (Do not clear device)
              0x00uy; 0x00uy  // Fixed value = 0x0000
          |] }

    /// Remote STOP with proper payload (Mode)
    let buildRemoteStop () =
        { Command = CommandCode.RemoteStop
          Subcommand = SubcommandCode.WordUnits
          Payload = [| 0x01uy; 0x00uy |] }  // Mode = 1 (STOP)

    /// Remote PAUSE with proper payload (Mode)
    let buildRemotePause () =
        { Command = CommandCode.RemotePause
          Subcommand = SubcommandCode.WordUnits
          Payload = [| 0x01uy; 0x00uy |] }  // Mode = 1 (PAUSE)

    /// Remote latch clear with proper payload (Mode)
    let buildRemoteLatchClear () =
        { Command = CommandCode.RemoteLatchClear
          Subcommand = SubcommandCode.WordUnits
          Payload = [| 0x01uy; 0x00uy |] }  // Mode = 1 (Clear)

    /// Remote RESET (no payload required)
    let buildRemoteReset () =
        { Command = CommandCode.RemoteReset
          Subcommand = SubcommandCode.WordUnits
          Payload = [||] }

    /// Read CPU type (no payload required)
    let buildReadCpuType () =
        { Command = CommandCode.ReadCpuType
          Subcommand = SubcommandCode.WordUnits
          Payload = [||] }

    // ---------------------------------------------------------------------
    // Monitor operations (0801 / 0802)
    // ---------------------------------------------------------------------

    let buildMonitorRegister (devices: RandomDeviceAccess array) =
        ensureCountInRange "devices" devices.Length 1 MaxMonitorPoints
        buildRandomRead devices

    let buildMonitor () =
        { Command = CommandCode.Monitor
          Subcommand = SubcommandCode.WordUnits
          Payload = [||] }