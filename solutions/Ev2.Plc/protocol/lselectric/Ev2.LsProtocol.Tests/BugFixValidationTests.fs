namespace Ev2.LsProtocol.Tests

open System
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests

module BugFixValidationTests =
    let ip = "192.168.9.100"

    [<Fact>]
    let ``Frame ID generation creates proper 2-byte array`` () =
        let sourcePort = 12345
        
        let frameId = XgtUtil.getFrameIdBytes ip sourcePort
        
        Assert.Equal(2, frameId.Length)
        Assert.Equal(byte (12345 &&& 0xFF), frameId.[0])  // Port byte
        Assert.Equal(byte 100, frameId.[1])               // Last IP octet
        
   
        
    [<Fact>]
    let ``Multi-read parser handles variable data sizes`` () =
        // Create a mock response buffer for testing
        let createMockResponse (dataTypes: PlcTagDataType[]) =
            let totalDataSize = dataTypes |> Array.sumBy XgtTypes.byteSize
            let bufferSize = 30 + totalDataSize + 20  // header + data + padding
            let buffer = Array.zeroCreate<byte> bufferSize
            
            // Set success status (error state at offset 26-27 = 0)
            buffer.[26] <- 0uy
            buffer.[27] <- 0uy
            
            // Set read command response (offset 20)
            buffer.[20] <- 0x55uy
            
            // Fill mock data starting at offset 30 (MultiReadDataStartOffset)
            let mutable offset = 30
            for dataType in dataTypes do
                let size = XgtTypes.byteSize dataType
                // Add 2-byte header for each element
                buffer.[offset] <- 0x00uy
                buffer.[offset + 1] <- byte size
                offset <- offset + 2
                
                // Add mock data
                for i = 0 to size - 1 do
                    buffer.[offset + i] <- byte (i + 1)
                offset <- offset + size + 2  // advance past data and any padding
            
            buffer
            
        let dataTypes = [| PlcTagDataType.Int16; PlcTagDataType.Int32; PlcTagDataType.Bool |]
        let mockResponse = createMockResponse dataTypes
        let targetBuffer = Array.zeroCreate<byte> (dataTypes |> Array.sumBy XgtTypes.byteSize)
        
        // This should not throw an exception with the fixed parser
        XgtResponse.parseStandardMultiRead mockResponse dataTypes.Length dataTypes targetBuffer
        
        // Verify we got some data (not all zeros)
        let hasData = targetBuffer |> Array.exists (fun b -> b <> 0uy)
        Assert.True(hasData, "Parser should extract some non-zero data")