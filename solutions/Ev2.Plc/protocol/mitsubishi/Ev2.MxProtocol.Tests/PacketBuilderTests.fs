module Ev2.MxProtocol.Tests.PacketBuilderTests

open System
open Xunit
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Protocol
open Ev2.MxProtocol.Tests.TestHelpers

[<Fact>]
let ``buildBatchRead creates correct request for word devices`` () =
    log "Validating batch read for word devices."
    let request = PacketBuilder.buildBatchRead DeviceCode.D 100 10us false
    
    assertEqual CommandCode.BatchRead request.Command
    assertEqual SubcommandCode.WordUnits request.Subcommand
    
    // Check payload structure: address(3) + device(1) + count(2) = 6 bytes
    assertEqual 6 request.Payload.Length
    // Address bytes (100 = 0x64)
    assertEqual 0x64uy request.Payload.[0]   // Address low byte
    assertEqual 0x00uy request.Payload.[1]   // Address mid byte  
    assertEqual 0x00uy request.Payload.[2]   // Address high byte
    // Device code (D = 0xA8)
    assertEqual 0xA8uy request.Payload.[3]
    // Count (10)
    assertEqual 0x0Auy request.Payload.[4]
    assertEqual 0x00uy request.Payload.[5]

[<Fact>]
let ``buildBatchRead creates correct request for bit devices`` () =
    log "Validating batch read for bit devices."
    let request = PacketBuilder.buildBatchRead DeviceCode.M 200 16us true
    
    assertEqual CommandCode.BatchRead request.Command
    assertEqual SubcommandCode.BitUnits request.Subcommand
    
    assertEqual 6 request.Payload.Length
    // Address bytes (200 = 0xC8)
    assertEqual 0xC8uy request.Payload.[0]   // Address low byte
    assertEqual 0x00uy request.Payload.[1]   // Address mid byte
    assertEqual 0x00uy request.Payload.[2]   // Address high byte
    // Device code (M = 0x90)
    assertEqual 0x90uy request.Payload.[3]
    // Count (16)
    assertEqual 0x10uy request.Payload.[4]
    assertEqual 0x00uy request.Payload.[5]

[<Fact>]
let ``buildBatchWrite creates correct request for word devices`` () =
    log "Validating batch write for word devices."
    let values = [| 0x1234us; 0x5678us; 0xABCDus |]
    let request = PacketBuilder.buildBatchWrite DeviceCode.D 1000 values false
    
    assertEqual CommandCode.BatchWrite request.Command
    assertEqual SubcommandCode.WordUnits request.Subcommand
    
    // Payload: device(1) + address(3) + count(2) + data(6)
    assertEqual 12 request.Payload.Length
    
    // Check address and device: address(3) + device(1) + count(2)
    // Address bytes (1000 = 0x03E8)
    assertEqual 0xE8uy request.Payload.[0]   // Address low byte
    assertEqual 0x03uy request.Payload.[1]   // Address mid byte
    assertEqual 0x00uy request.Payload.[2]   // Address high byte
    // Device code (D = 0xA8)
    assertEqual 0xA8uy request.Payload.[3]
    // Count (3)
    assertEqual 0x03uy request.Payload.[4]
    assertEqual 0x00uy request.Payload.[5]
    
    // Check data values
    assertEqual 0x34uy request.Payload.[6]
    assertEqual 0x12uy request.Payload.[7]
    assertEqual 0x78uy request.Payload.[8]
    assertEqual 0x56uy request.Payload.[9]
    assertEqual 0xCDuy request.Payload.[10]
    assertEqual 0xABuy request.Payload.[11]

[<Fact>]
let ``buildRandomRead creates correct request`` () =
    log "Validating random read payload."
    let devices = [|
        { DeviceCode = DeviceCode.D; DeviceNumber = 100; AccessSize = 0uy }
        { DeviceCode = DeviceCode.M; DeviceNumber = 50; AccessSize = 0uy }
        { DeviceCode = DeviceCode.W; DeviceNumber = 200; AccessSize = 0uy }
    |]
    
    let request = PacketBuilder.buildRandomRead devices
    
    assertEqual CommandCode.RandomRead request.Command
    assertEqual SubcommandCode.WordUnits request.Subcommand
    
    // Each device: address(3) + device(1) + access_size(1) = 5 bytes
    // Total: word_count(1) + dword_count(1) + devices(5*3) = 17 bytes
    assertEqual 17 request.Payload.Length
    assertEqual 0x03uy request.Payload.[0]  // Word count (all 3 devices are word access)
    assertEqual 0x00uy request.Payload.[1]  // Dword count (0)
    
    // First device (D100)
    assertEqual 0x64uy request.Payload.[2]   // Address low byte
    assertEqual 0x00uy request.Payload.[3]   // Address mid byte
    assertEqual 0x00uy request.Payload.[4]   // Address high byte
    assertEqual 0xA8uy request.Payload.[5]   // Device code (D = 0xA8)
    assertEqual 0x00uy request.Payload.[6]   // Access size (word = 0)
    
    // Second device (M50, bit device in word-unit mode: 50*16=800=0x320)
    assertEqual 0x20uy request.Payload.[7]   // Address low byte (800 = 0x320, low byte = 0x20)
    assertEqual 0x03uy request.Payload.[8]   // Address mid byte (0x320 >> 8 = 0x03)
    assertEqual 0x00uy request.Payload.[9]   // Address high byte
    assertEqual 0x90uy request.Payload.[10]  // Device code (M = 0x90)
    assertEqual 0x00uy request.Payload.[11]  // Access size (word = 0)
    
    // Third device (W200)
    assertEqual 0xC8uy request.Payload.[12]  // Address low byte (200 = 0xC8)
    assertEqual 0x00uy request.Payload.[13]  // Address mid byte
    assertEqual 0x00uy request.Payload.[14]  // Address high byte
    assertEqual 0xB4uy request.Payload.[15]  // Device code (W = 0xB4)
    assertEqual 0x00uy request.Payload.[16]  // Access size (word = 0)

[<Fact>]
let ``buildRandomWriteBit creates correct request`` () =
    log "Validating random write bit payload."
    let devices = [|
        (DeviceCode.M, 100, true)
        (DeviceCode.Y, 16, false)
        (DeviceCode.L, 200, true)
    |]
    
    let request = PacketBuilder.buildRandomWriteBit devices
    
    assertEqual CommandCode.RandomWrite request.Command
    assertEqual SubcommandCode.BitUnits request.Subcommand
    
    // Count(1) + devices(5*3) = 16 bytes: device_count(1) + (address(3)+device(1)+value(1))*3
    assertEqual 16 request.Payload.Length
    assertEqual 0x03uy request.Payload.[0]  // Device count
    
    // Check first device (M100, true)
    assertEqual 0x64uy request.Payload.[1]   // Address low byte (100 = 0x64)
    assertEqual 0x00uy request.Payload.[2]   // Address mid byte
    assertEqual 0x00uy request.Payload.[3]   // Address high byte
    assertEqual 0x90uy request.Payload.[4]   // Device code (M = 0x90)
    assertEqual 0x01uy request.Payload.[5]   // Value (true = 0x01)

[<Fact>]
let ``buildRemoteRun creates correct request`` () =
    log "Validating remote run payload."
    let request = PacketBuilder.buildRemoteRun()
    
    assertEqual CommandCode.RemoteRun request.Command
    assertEqual SubcommandCode.WordUnits request.Subcommand
    assertEqual 6 request.Payload.Length
    
    // Check mode, clear mode, and fixed value
    assertEqual 0x01uy request.Payload.[0]  // Mode = 1 (RUN)
    assertEqual 0x00uy request.Payload.[1]  // Mode high byte
    assertEqual 0x00uy request.Payload.[2]  // Clear mode = 0 (Do not clear device)
    assertEqual 0x00uy request.Payload.[3]  // Clear mode high byte
    assertEqual 0x00uy request.Payload.[4]  // Fixed value = 0x0000
    assertEqual 0x00uy request.Payload.[5]  // Fixed value high byte

[<Fact>]
let ``buildRemoteStop creates correct request`` () =
    log "Validating remote stop payload."
    let request = PacketBuilder.buildRemoteStop()
    
    assertEqual CommandCode.RemoteStop request.Command
    assertEqual SubcommandCode.WordUnits request.Subcommand
    assertEqual 2 request.Payload.Length
    
    // Check mode
    assertEqual 0x01uy request.Payload.[0]  // Mode = 1 (STOP)
    assertEqual 0x00uy request.Payload.[1]  // Mode high byte

[<Fact>]
let ``buildReadCpuType creates correct request`` () =
    log "Validating CPU type request payload."
    let request = PacketBuilder.buildReadCpuType()
    
    assertEqual CommandCode.ReadCpuType request.Command
    assertEqual SubcommandCode.WordUnits request.Subcommand
    assertEqual 0 request.Payload.Length

[<Fact>]
let ``buildBufferRead creates correct request`` () =
    log "Validating buffer read payload."
    let request = PacketBuilder.buildBufferRead 0x1000us 256us
    
    assertEqual CommandCode.BufferRead request.Command
    assertEqual SubcommandCode.WordUnits request.Subcommand
    
    assertEqual 4 request.Payload.Length
    assertEqual 0x00uy request.Payload.[0]
    assertEqual 0x10uy request.Payload.[1]
    assertEqual 0x00uy request.Payload.[2]
    assertEqual 0x01uy request.Payload.[3]

[<Fact>]
let ``buildBufferWrite creates correct request`` () =
    log "Validating buffer write payload."
    let values = [| 0xAABBus; 0xCCDDus |]
    let request = PacketBuilder.buildBufferWrite 0x2000us values
    
    assertEqual CommandCode.BufferWrite request.Command
    assertEqual SubcommandCode.WordUnits request.Subcommand
    
    assertEqual 8 request.Payload.Length
    assertEqual 0x00uy request.Payload.[0]
    assertEqual 0x20uy request.Payload.[1]
    assertEqual 0x02uy request.Payload.[2]
    assertEqual 0x00uy request.Payload.[3]
    assertEqual 0xBBuy request.Payload.[4]
    assertEqual 0xAAuy request.Payload.[5]
    assertEqual 0xDDuy request.Payload.[6]
    assertEqual 0xCCuy request.Payload.[7]
