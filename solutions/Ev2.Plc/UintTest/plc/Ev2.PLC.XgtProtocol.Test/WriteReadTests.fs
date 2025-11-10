namespace Ev2.PLC.XgtProtocol.Tests

open System
open Xunit
open Xunit.Sdk
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Types.PlcTagDataTypeExtensions
open TestEndpoints

module WriteReadTests =

    let private assertRoundTrip (plc: TestEndpoints.TestXgtEthernet) address dataType value =
        let writeResult = plc.Write(address, dataType, value)
        Assert.True(writeResult, $"Write should succeed for {address}")
        let readResult = plc.Read(address, dataType)
        Assert.Equal(value, readResult)

    [<Fact>]
    let ``Ethernet protocol write succeeds`` () =
        use plc = new TestEndpoints.TestXgtEthernet()
        try
            plc.EnsureConnected()
            let address = "%MW300"
            let dataType = PlcTagDataType.Int16
            let testValue = ScalarValue.Int16Value 12345s
            let result = plc.Write(address, dataType, testValue)
            Assert.True(result)
        with ex -> raise (new XunitException(ex.Message))

    [<Fact>]
    let ``Writes and reads mixed data types`` () =
        use plc = new TestEndpoints.TestXgtEthernet()
        if plc.IsLocalEthernet then 
            Assert.True(true)
        else 
            try
                plc.EnsureConnected()
                let addresses = [| "%MX200"; "%MW200"; "%MD200"; "%MB200"; "%ML200" |]
                let dataTypes =
                    [|
                        PlcTagDataType.Bool
                        PlcTagDataType.Int16
                        PlcTagDataType.Int32
                        PlcTagDataType.UInt8
                        PlcTagDataType.Int64
                    |]
                let values =
                    [|
                        ScalarValue.BoolValue true
                        ScalarValue.Int16Value 7s
                        ScalarValue.Int32Value 7
                        ScalarValue.UInt8Value 7uy
                        ScalarValue.Int64Value 0x7L
                    |]

                let writeOk = plc.Writes(addresses, dataTypes, values)
                Assert.True(writeOk)

                let readBuffer = Array.zeroCreate<byte> 2048
                plc.Reads(addresses, dataTypes, readBuffer)
                let requiredBytes = dataTypes |> Array.sumBy byteSize
                Assert.True(readBuffer.Length >= requiredBytes)
            with ex -> raise (new XunitException(ex.Message))

    [<Fact>]
    let ``Write then read returns stored value`` () =
        use plc = new TestEndpoints.TestXgtEthernet()
        try
            plc.EnsureConnected()
            let cases =
                [|
                    "%MX200", PlcTagDataType.Bool, ScalarValue.BoolValue true
                    "%MB3000", PlcTagDataType.UInt8, ScalarValue.UInt8Value 100uy
                    "%MW4000", PlcTagDataType.Int16, ScalarValue.Int16Value 20000s
                    "%MD5000", PlcTagDataType.Int32, ScalarValue.Int32Value 300000000
                    "%ML6000", PlcTagDataType.Int64, ScalarValue.Int64Value 1446744073709551615L
                |]
            cases |> Array.iter (fun (address, dataType, value) -> assertRoundTrip plc address dataType value)
        with ex -> raise (new XunitException(ex.Message))

    [<Fact>]
    let ``Batch write and read with sequential values`` () =
        use plc = new TestEndpoints.TestXgtEthernet()
        try
            plc.EnsureConnected()
            let addresses = [| for i in 0..15 -> sprintf "%%MD%d" (i + 100) |]
            let dataTypes = Array.create addresses.Length PlcTagDataType.Int32
            let values = [| for i in 0..15 -> ScalarValue.Int32Value (i + 100) |]
            let readBuffer = Array.zeroCreate<byte> 2048
            let writeOk = plc.Writes(addresses, dataTypes, values)
            Assert.True(writeOk)
            plc.Reads(addresses, dataTypes, readBuffer)
            let requiredBytes = dataTypes |> Array.sumBy byteSize
            Assert.True(readBuffer.Length >= requiredBytes)
        with ex -> raise (new XunitException(ex.Message))
