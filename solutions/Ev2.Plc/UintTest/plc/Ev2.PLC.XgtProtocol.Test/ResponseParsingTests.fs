namespace Ev2.PLC.XgtProtocol.Tests

open System
open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.XgtProtocol.XgtResponse

module ResponseParsingTests =

    [<Fact>]
    let ``Parses read response`` () =
        let buffer = Array.zeroCreate<byte> 48
        buffer.[20] <- 0x55uy
        buffer.[26] <- 0uy
        let valueBytes = BitConverter.GetBytes(1234s)
        Array.Copy(valueBytes, 0, buffer, 32, valueBytes.Length)
        let result = parseReadResponse buffer PlcTagDataType.Int16
        Assert.Equal(ScalarValue.Int16Value 1234s, result)

    [<Fact>]
    let ``Extracts values from multi-read buffer`` () =
        let temp = Array.zeroCreate<byte> 4
        Array.Copy(BitConverter.GetBytes(10s), 0, temp, 0, 2)
        Array.Copy(BitConverter.GetBytes(20s), 0, temp, 2, 2)
        let values = extractValues temp [| PlcTagDataType.Int16; PlcTagDataType.Int16 |]
        Assert.Equal(ScalarValue.Int16Value 10s, values.[0])
        Assert.Equal(ScalarValue.Int16Value 20s, values.[1])
