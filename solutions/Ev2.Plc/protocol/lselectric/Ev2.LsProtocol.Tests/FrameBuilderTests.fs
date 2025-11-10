namespace Ev2.LsProtocol.Tests

open System
open Xunit
open Ev2.LsProtocol
open ProtocolTestHelper

module XgtFrameBuilderTests =

    [<Fact>]
    let ``createMultiReadFrame should create valid XGT read frame`` () =
        let frameId = generateFrameId()
        let addresses = [| "M100" |]
        let dataTypes = [| PlcTagDataType.UInt16 |]
        
        let frame = XgtFrameBuilder.createMultiReadFrame frameId addresses dataTypes
        
        // Basic validation
        assertValidXgtFrame frame
        
        // Check command code for read request
        let commandBytes = frame.[20..21]
        let expectedCommand = BitConverter.GetBytes(uint16 CommandCode.ReadRequest)
        BufferAssert.equal expectedCommand commandBytes
        
        // Check data type
        Assert.Equal(byte DataType.Word, frame.[22])
        
        // Check variable count
        Assert.Equal(1uy, frame.[26])

    [<Fact>]
    let ``createMultiReadFrameEFMTB should create valid EFMTB read frame`` () =
        let frameId = generateFrameId()
        let addresses = [| "M10" |]
        let dataTypes = [| PlcTagDataType.UInt16 |]
        
        let frame = XgtFrameBuilder.createMultiReadFrameEFMTB frameId addresses dataTypes
        
        // Basic validation
        assertValidXgtFrame frame
        
        // Check command code for EFMTB read request
        let commandBytes = frame.[20..21]
        let expectedCommand = BitConverter.GetBytes(uint16 CommandCode.ReadRequestEFMTB)
        BufferAssert.equal expectedCommand commandBytes
        
        // Check EFMTB data type indicator
        Assert.Equal(0x10uy, frame.[22])
        
        // Check variable count
        Assert.Equal(1uy, frame.[26])

    [<Fact>]
    let ``createMultiReadFrame should support multiple addresses`` () =
        let frameId = generateFrameId()
        let addresses = [| "M100"; "M101"; "M102" |]
        let dataTypes = [| PlcTagDataType.UInt16; PlcTagDataType.UInt16; PlcTagDataType.UInt16 |]
        
        let frame = XgtFrameBuilder.createMultiReadFrame frameId addresses dataTypes
        
        assertValidXgtFrame frame
        Assert.Equal(3uy, frame.[26]) // Variable count

    [<Fact>]
    let ``createMultiReadFrame should enforce maximum variable count`` () =
        let frameId = generateFrameId()
        let addresses = Array.init 17 (fun i -> sprintf "M%d" (100 + i))
        let dataTypes = Array.init 17 (fun _ -> PlcTagDataType.UInt16)
        
        Assert.Throws<ArgumentException>(fun () ->
            XgtFrameBuilder.createMultiReadFrame frameId addresses dataTypes |> ignore)

    [<Fact>]
    let ``createMultiReadFrame should enforce same data types`` () =
        let frameId = generateFrameId()
        let addresses = [| "M100"; "M101" |]
        let dataTypes = [| PlcTagDataType.UInt16; PlcTagDataType.UInt32 |] // Mixed types
        
        Assert.Throws<ArgumentException>(fun () ->
            XgtFrameBuilder.createMultiReadFrame frameId addresses dataTypes |> ignore)

    [<Theory>]
    [<InlineData(PlcTagDataType.Bool, 0x00uy)>]
    [<InlineData(PlcTagDataType.UInt8, 0x01uy)>]
    [<InlineData(PlcTagDataType.UInt16, 0x02uy)>]
    [<InlineData(PlcTagDataType.UInt32, 0x03uy)>]
    [<InlineData(PlcTagDataType.UInt64, 0x04uy)>]
    let ``createMultiReadFrame should set correct data type code`` (dataType: PlcTagDataType) (expectedCode: byte) =
        let frameId = generateFrameId()
        let addresses = [| "M100" |]
        let dataTypes = [| dataType |]
        
        let frame = XgtFrameBuilder.createMultiReadFrame frameId addresses dataTypes
        
        Assert.Equal(expectedCode, frame.[22])

    [<Fact>]
    let ``createMultiWriteFrame should create valid XGT write frame`` () =
        let frameId = generateFrameId()
        let block = {
            Address = "M100"
            DataType = PlcTagDataType.UInt16
            DeviceType = DeviceType.M
            ByteOffset = 100
            BitPosition = 0
            Value = Some (box 1234us)
        }
        let blocks = [| block |]
        
        let frame = XgtFrameBuilder.createMultiWriteFrame frameId blocks
        
        // Basic validation
        assertValidXgtFrame frame
        
        // Check command code for write request
        let commandBytes = frame.[20..21]
        let expectedCommand = BitConverter.GetBytes(uint16 CommandCode.WriteRequest)
        BufferAssert.equal expectedCommand commandBytes

    [<Fact>]
    let ``createMultiWriteFrameEFMTB should create valid EFMTB write frame`` () =
        let frameId = generateFrameId()
        let block = {
            Address = "M100"
            DataType = PlcTagDataType.UInt16
            DeviceType = DeviceType.M
            ByteOffset = 100
            BitPosition = 0
            Value = Some (box 1234us)
        }
        let blocks = [| block |]
        
        let frame = XgtFrameBuilder.createMultiWriteFrameEFMTB frameId blocks
        
        // Basic validation
        assertValidXgtFrame frame
        
        // Check command code for EFMTB write request
        let commandBytes = frame.[20..21]
        let expectedCommand = BitConverter.GetBytes(uint16 CommandCode.WriteRequestEFMTB)
        BufferAssert.equal expectedCommand commandBytes

    [<Fact>]
    let ``createMultiWriteFrame should enforce maximum block count`` () =
        let frameId = generateFrameId()
        let blocks = Array.init 17 (fun i -> {
            Address = sprintf "M%d" (100 + i)
            DataType = PlcTagDataType.UInt16
            DeviceType = DeviceType.M
            ByteOffset = 100 + i
            BitPosition = 0
            Value = Some (box 1234us)
        })
        
        Assert.Throws<ArgumentException>(fun () ->
            XgtFrameBuilder.createMultiWriteFrame frameId blocks |> ignore)

    [<Fact>]
    let ``createMultiWriteFrame should require values for all blocks`` () =
        let frameId = generateFrameId()
        let block = {
            Address = "M100"
            DataType = PlcTagDataType.UInt16
            DeviceType = DeviceType.M
            ByteOffset = 100
            BitPosition = 0
            Value = None // Missing value
        }
        let blocks = [| block |]
        
        Assert.Throws<ArgumentException>(fun () ->
            XgtFrameBuilder.createMultiWriteFrame frameId blocks |> ignore)

    [<Fact>]
    let ``Frame checksum should be calculated correctly`` () =
        let frameId = generateFrameId()
        let frame = XgtFrameBuilder.createMultiReadFrame frameId [| "M100" |] [| PlcTagDataType.UInt16 |]
        
        // Verify checksum is in expected position
        let checksumPosition = 19
        Assert.True(checksumPosition < frame.Length)
        
        // Checksum should be non-zero for valid frame
        Assert.NotEqual(0uy, frame.[checksumPosition])

    [<Fact>]
    let ``Frame should contain correct company ID`` () =
        let frameId = generateFrameId()
        let frame = XgtFrameBuilder.createMultiReadFrame frameId [| "M100" |] [| PlcTagDataType.UInt16 |]
        
        let companyIdBytes = frame.[0..11]
        BufferAssert.equal CompanyHeader.IdBytes companyIdBytes

    [<Fact>]
    let ``Frame ID should be preserved in frame`` () =
        let frameId = [| 0x12uy; 0x34uy |]
        let frame = XgtFrameBuilder.createMultiReadFrame frameId [| "M100" |] [| PlcTagDataType.UInt16 |]
        
        Assert.Equal(0x12uy, frame.[14])
        Assert.Equal(0x34uy, frame.[15])
