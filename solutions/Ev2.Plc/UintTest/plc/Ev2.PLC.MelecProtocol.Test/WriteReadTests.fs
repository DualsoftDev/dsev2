namespace Ev2.PLC.MelecProtocol.Tests

open System
open System.IO
open System.Net.Sockets
open Xunit
open Xunit.Sdk
open Ev2.PLC.Common.Types
open Ev2.PLC.MelecProtocol
open TestEndpoints

module WriteReadTests =

    let private runWithPlc  action =
        try
            use plc = new TestMxEthernet()
            action plc
        with
        | :? SocketException as ex -> Assert.True(false, sprintf "MELSEC PLC connection failed (%s)" ex.Message)
        | :? IOException as ex -> Assert.True(false, sprintf "MELSEC PLC IO failure (%s)" ex.Message)

    [<Fact>]
    let ``Write and read D registers over Ethernet`` () =
        runWithPlc   (fun plc ->
            let baseAddress = 200
            let values = [| for i in 0..3 -> baseAddress + i |]
            for i = 0 to values.Length - 1 do
                plc.WriteWord(MxDevice.D, baseAddress + i, values.[i])
            let readBack = plc.ReadWords(MxDevice.D, baseAddress, values.Length)
            Assert.Equal<int>(values.[0], readBack.[0])
            Assert.Equal<int[]>(values, readBack)
        )

    [<Fact>]
    let ``Write and read M bits over Local Ethernet`` () =
        runWithPlc   (fun plc ->
            let baseAddress = 1000
            let bools = [| true; false; true; true |]
            for i = 0 to bools.Length - 1 do
                plc.WriteBit(MxDevice.M, baseAddress + i, if bools.[i] then 1 else 0)
            let readBack = plc.ReadBits(MxDevice.M, baseAddress, bools.Length)
            Assert.Equal<bool[]>(bools, readBack)
        )

    [<Fact>]
    let ``Random word write uses command successfully`` () =
        runWithPlc   (fun plc ->
            let targets =
                [|
                    (MxDevice.D, 310, 4321)
                    (MxDevice.D, 311, 5432)
                |]
            plc.WriteWordRandom(targets)
            let readBack = plc.ReadWords(MxDevice.D, 310, targets.Length)
            Assert.Equal<int[]>(targets |> Array.map (fun (_, _, v) -> v), readBack)
        )
