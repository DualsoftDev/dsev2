namespace Ev2.PLC.MelecProtocol.Tests

open System
open System.IO
open System.Net.Sockets
open Xunit
open Xunit.Sdk
open Ev2.PLC.Common.Types
open Ev2.PLC.MelecProtocol
open TestEndpoints

module IntegrationTests =

    let private rnd = Random()

    let private generateValue (device: MxDevice) =
        if MxDevice.IsBit device then
            let bit = rnd.Next(0, 2)
            bit, true
        else
            rnd.Next(0, 0xFFFF), false

    let private runDeviceRangeTest  devices minIndex maxIndex =
        try
            use plc = new TestMxEthernet()
            for device in devices do
                for idx in minIndex .. maxIndex do
                    let value, isBit = generateValue device
                    if isBit then
                        plc.WriteBit(device, idx, value)
                        let readBack = plc.ReadBits(device, idx, 1)
                        Assert.Equal(value <> 0, readBack.[0])
                    else
                        plc.WriteWord(device, idx, value)
                        let readBack = plc.ReadWords(device, idx, 1)
                        Assert.Equal(value, readBack.[0])
        with
        | :? SocketException as ex -> Assert.True(false, sprintf "MELSEC integration test failed (%s)" ex.Message)
        | :? IOException as ex -> Assert.True(false, sprintf "MELSEC integration IO failure (%s)" ex.Message)

    [<Fact>]
    let ``Ethernet word devices range write/read`` () =
        let wordDevices = [ MxDevice.D; MxDevice.R; MxDevice.ZR ]
        runDeviceRangeTest  wordDevices 400 405

    [<Fact>]
    let ``Local Ethernet bit devices range write/read`` () =
        let bitDevices = [ MxDevice.M; MxDevice.X; MxDevice.Y ]
        runDeviceRangeTest  bitDevices 800 805

    [<Fact>]
    let ``Random write multiple devices`` () =
        try
            use plc = new TestMxEthernet()
            let devices =
                [|
                    (MxDevice.D, 512, rnd.Next(0, 0xFFFF))
                    (MxDevice.D, 513, rnd.Next(0, 0xFFFF))
                    (MxDevice.W, 20, rnd.Next(0, 0xFFFF))
                |]
            plc.WriteWordRandom(devices)
            for (device, address, value) in devices do
                let readBack = plc.ReadWords(device, address, 1)
                Assert.Equal(value, readBack.[0])
        with
        | :? SocketException as ex -> Assert.True(false, sprintf "MELSEC random write failed (%s)" ex.Message)
        | :? IOException as ex -> Assert.True(false, sprintf "MELSEC random write IO failure (%s)" ex.Message)