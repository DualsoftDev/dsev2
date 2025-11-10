namespace Ev2.LsProtocol.Tests

open System
open Xunit
open Ev2.LsProtocol
open ProtocolTestHelper

module XgtResponseTests =

    let createMockReadResponse (frameId: byte[]) (dataSize: int) =
        let frame = Array.zeroCreate<byte> (XgtUtil.HeaderSize + 12 + dataSize)
        
        // Company ID
        Array.Copy(CompanyHeader.IdBytes, 0, frame, 0, 12)
        
        // Protocol ID and direction
        frame.[12] <- 0x00uy
        frame.[13] <- byte FrameSource.ServerToClient
        
        // Frame ID
        frame.[14] <- frameId.[0]
        frame.[15] <- frameId.[1]
        
        // Body length
        let bodyLength = 12 + dataSize
        let lengthBytes = BitConverter.GetBytes(uint16 bodyLength)
        frame.[16] <- lengthBytes.[0]
        frame.[17] <- lengthBytes.[1]
        
        // Position and checksum
        frame.[18] <- 0uy
        frame.[19] <- FrameUtils.calculateChecksum frame 19
        
        // Command (Read Response)
        let cmdBytes = BitConverter.GetBytes(uint16 CommandCode.ReadResponse)
        frame.[20] <- cmdBytes.[0]
        frame.[21] <- cmdBytes.[1]
        
        // Data type
        frame.[22] <- byte DataType.Word
        frame.[23] <- 0x00uy
        
        // Reserved
        frame.[24] <- 0x00uy
        frame.[25] <- 0x00uy
        
        // Error status (OK)
        frame.[26] <- 0x00uy
        frame.[27] <- 0x00uy
        
        // Error info
        frame.[28] <- 0x00uy
        frame.[29] <- 0x00uy
        
        // Variable count
        frame.[30] <- 0x01uy
        frame.[31] <- 0x00uy
        
        frame

    let createMockErrorResponse (frameId: byte[]) (errorCode: byte) =
        let frame = Array.zeroCreate<byte> (XgtUtil.HeaderSize + 9)
        
        // Company ID
        Array.Copy(CompanyHeader.IdBytes, 0, frame, 0, 12)
        
        // Protocol ID and direction
        frame.[12] <- 0x00uy
        frame.[13] <- byte FrameSource.ServerToClient
        
        // Frame ID
        frame.[14] <- frameId.[0]
        frame.[15] <- frameId.[1]
        
        // Body length
        let bodyLength = 9
        let lengthBytes = BitConverter.GetBytes(uint16 bodyLength)
        frame.[16] <- lengthBytes.[0]
        frame.[17] <- lengthBytes.[1]
        
        // Position and checksum
        frame.[18] <- 0uy
        frame.[19] <- FrameUtils.calculateChecksum frame 19
        
        // Command (Read Response)
        let cmdBytes = BitConverter.GetBytes(uint16 CommandCode.ReadResponse)
        frame.[20] <- cmdBytes.[0]
        frame.[21] <- cmdBytes.[1]
        
        // Data type
        frame.[22] <- byte DataType.Word
        frame.[23] <- 0x00uy
        
        // Reserved
        frame.[24] <- 0x00uy
        frame.[25] <- 0x00uy
        
        // Error status (Error)
        frame.[26] <- 0xFFuy
        frame.[27] <- 0xFFuy
        
        // Error code
        frame.[28] <- errorCode
        
        frame

    [<Fact>]
    let ``parseReadResponse should handle successful response`` () =
        let frameId = generateFrameId()
        let dataSize = 4 // 2 words
        let response = createMockReadResponse frameId dataSize
        
        // Add some test data
        let testData = [| 0x34uy; 0x12uy; 0x78uy; 0x56uy |] // Little endian values
        Array.Copy(testData, 0, response, XgtUtil.HeaderSize + 12, dataSize)
        
        assertValidXgtResponse response
        
        // Verify command code
        let commandBytes = response.[20..21]
        let expectedCommand = BitConverter.GetBytes(uint16 CommandCode.ReadResponse)
        BufferAssert.equal expectedCommand commandBytes

    [<Fact>]
    let ``parseReadResponse should handle error response`` () =
        let frameId = generateFrameId()
        let errorCode = 0x21uy // Frame checksum error
        let response = createMockErrorResponse frameId errorCode
        
        assertValidXgtResponse response
        
        // Check error status
        Assert.Equal(0xFFuy, response.[26])
        Assert.Equal(0xFFuy, response.[27])
        
        // Check error code
        Assert.Equal(errorCode, response.[28])

    [<Fact>]
    let ``Response should preserve frame ID`` () =
        let frameId = [| 0x12uy; 0x34uy |]
        let response = createMockReadResponse frameId 2
        
        Assert.Equal(0x12uy, response.[14])
        Assert.Equal(0x34uy, response.[15])

    [<Fact>]
    let ``Response should have correct company ID`` () =
        let frameId = generateFrameId()
        let response = createMockReadResponse frameId 2
        
        let companyIdBytes = response.[0..11]
        BufferAssert.equal CompanyHeader.IdBytes companyIdBytes

    [<Fact>]
    let ``Response should have server-to-client direction`` () =
        let frameId = generateFrameId()
        let response = createMockReadResponse frameId 2
        
        Assert.Equal(byte FrameSource.ServerToClient, response.[13])

    [<Theory>]
    [<InlineData(CommandCode.ReadResponse)>]
    [<InlineData(CommandCode.WriteResponse)>]
    [<InlineData(CommandCode.StatusResponse)>]
    let ``Response should support different command types`` (commandCode: CommandCode) =
        let frameId = generateFrameId()
        let response = createMockReadResponse frameId 2
        
        // Update command code
        let cmdBytes = BitConverter.GetBytes(uint16 commandCode)
        response.[20] <- cmdBytes.[0]
        response.[21] <- cmdBytes.[1]
        
        // Verify command is preserved
        let actualCommandBytes = response.[20..21]
        BufferAssert.equal cmdBytes actualCommandBytes

    [<Fact>]
    let ``parseWriteResponse should handle successful response`` () =
        let frameId = generateFrameId()
        let response = Array.zeroCreate<byte> (XgtUtil.HeaderSize + 10)
        
        // Company ID
        Array.Copy(CompanyHeader.IdBytes, 0, response, 0, 12)
        
        // Protocol ID and direction
        response.[12] <- 0x00uy
        response.[13] <- byte FrameSource.ServerToClient
        
        // Frame ID
        response.[14] <- frameId.[0]
        response.[15] <- frameId.[1]
        
        // Body length
        let bodyLength = 10
        let lengthBytes = BitConverter.GetBytes(uint16 bodyLength)
        response.[16] <- lengthBytes.[0]
        response.[17] <- lengthBytes.[1]
        
        // Position and checksum
        response.[18] <- 0uy
        response.[19] <- FrameUtils.calculateChecksum response 19
        
        // Command (Write Response)
        let cmdBytes = BitConverter.GetBytes(uint16 CommandCode.WriteResponse)
        response.[20] <- cmdBytes.[0]
        response.[21] <- cmdBytes.[1]
        
        // Data type
        response.[22] <- byte DataType.Word
        response.[23] <- 0x00uy
        
        // Reserved
        response.[24] <- 0x00uy
        response.[25] <- 0x00uy
        
        // Error status (OK)
        response.[26] <- 0x00uy
        response.[27] <- 0x00uy
        
        // Block count
        response.[28] <- 0x01uy
        response.[29] <- 0x00uy
        
        assertValidXgtResponse response
        
        // Verify command code
        let commandBytes = response.[20..21]
        let expectedCommand = BitConverter.GetBytes(uint16 CommandCode.WriteResponse)
        BufferAssert.equal expectedCommand commandBytes

    [<Fact>]
    let ``Response checksum should be valid`` () =
        let frameId = generateFrameId()
        let response = createMockReadResponse frameId 2
        
        // Verify checksum position
        let checksumPosition = 19
        Assert.True(checksumPosition < response.Length)
        
        // Checksum should be non-zero for valid response
        Assert.NotEqual(0uy, response.[checksumPosition])

    [<Theory>]
    [<InlineData(0x10uy)>]
    [<InlineData(0x11uy)>]
    [<InlineData(0x21uy)>]
    [<InlineData(0x99uy)>]
    let ``Error response should preserve error code`` (errorCode: byte) =
        let frameId = generateFrameId()
        let response = createMockErrorResponse frameId errorCode
        
        Assert.Equal(errorCode, response.[28])

    [<Fact>]
    let ``Status response should have correct structure`` () =
        let frameId = generateFrameId()
        let response = Array.zeroCreate<byte> (XgtUtil.HeaderSize + 30) // Status data is 24 bytes + header
        
        // Company ID
        Array.Copy(CompanyHeader.IdBytes, 0, response, 0, 12)
        
        // Protocol ID and direction
        response.[12] <- 0x00uy
        response.[13] <- byte FrameSource.ServerToClient
        
        // Frame ID
        response.[14] <- frameId.[0]
        response.[15] <- frameId.[1]
        
        // Body length
        let bodyLength = 30
        let lengthBytes = BitConverter.GetBytes(uint16 bodyLength)
        response.[16] <- lengthBytes.[0]
        response.[17] <- lengthBytes.[1]
        
        // Position and checksum
        response.[18] <- 0uy
        response.[19] <- FrameUtils.calculateChecksum response 19
        
        // Command (Status Response)
        let cmdBytes = BitConverter.GetBytes(uint16 CommandCode.StatusResponse)
        response.[20] <- cmdBytes.[0]
        response.[21] <- cmdBytes.[1]
        
        // Status response has specific data structure
        response.[22] <- 0x00uy  // Data type (don't care)
        response.[23] <- 0x00uy
        response.[24] <- 0x00uy  // Reserved
        response.[25] <- 0x00uy
        response.[26] <- 0x00uy  // Error status (OK)
        response.[27] <- 0x00uy
        response.[28] <- 0x18uy  // Data size (24 bytes)
        response.[29] <- 0x00uy
        
        assertValidXgtResponse response
        
        // Verify command code
        let commandBytes = response.[20..21]
        let expectedCommand = BitConverter.GetBytes(uint16 CommandCode.StatusResponse)
        BufferAssert.equal expectedCommand commandBytes