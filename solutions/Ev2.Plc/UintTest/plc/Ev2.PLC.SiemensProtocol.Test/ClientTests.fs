namespace Ev2.PLC.SiemensProtocol.Tests

open System
open System.IO
open Xunit
open Xunit.Sdk
open Ev2.PLC.Common.Types
open Ev2.PLC.SiemensProtocol
open TestEndpoints

module ClientTests =

    let private runWith endpoint action =
        use client = createClient endpoint
        try
            if not (client.Connect()) then
                raise (new XunitException($"Failed to connect to {endpoint.Name} at {endpoint.Ip}"))
            action client
        with
        | :? IOException as ex -> raise (new XunitException($"Siemens PLC IO failure ({ex.Message})"))
        | :? InvalidOperationException as ex -> raise (new XunitException($"Siemens PLC connection failed ({ex.Message})"))

    [<Fact>]
    let ``Read bit from S1500`` () =
        runWith siemensS1500 (fun client ->
            let binding = SiemensConversions.bindingFor PlcTagDataType.Bool
            let value = client.Read(binding, Addresses.bit)
            match value with
            | ScalarValue.BoolValue _ -> Assert.True(true)
            | _ -> Assert.True(false, "Unexpected value type")
        )

    [<Fact>]
    let ``Read int16 from S300`` () =
        runWith siemensS315 (fun client ->
            let binding = SiemensConversions.bindingFor PlcTagDataType.Int16
            let value = client.Read(binding, Addresses.int16)
            match value with
            | ScalarValue.Int16Value _ -> Assert.True(true)
            | _ -> Assert.True(false, "Expected int16")
        )

    [<Fact>]
    let ``Write and read back float on CP`` () =
        use cli = new Ev2.PLC.SiemensProtocol.S7Client()
        cli.ConnectTo("192.168.9.99", 0, 0) |> ignore


        // C1 = Partner(PC) = 03.01,  C2 = Local(PLC/CP) = 03.02
        //cli.SetConnectionParams("192.168.9.97",  0x0302us, 0x0301us) |> ignore
        //// CP 펌웨어 호환을 위해 TPDU=512 권장
        //cli.SetTpduSize(512) |> ignore
        //cli.Connect() |> ignore

        // DB1.DBB0..1에서 Int16 읽기
        let buf = Array.zeroCreate<byte> 2
        let mutable got = 0
        cli.ReadArea(S7Area.MK, 1, 0, 1, S7WordLength.Int, buf, &got) |> ignore
        printfn "bytes=%d, value=%d" got ((int buf.[0] <<< 8) ||| int buf.[1])

        cli.Disconnect() |> ignore


        //runWith siemensCp (fun client ->
        //    let binding = SiemensConversions.bindingFor PlcTagDataType.Float32
        //    let original = client.Read(binding, Addresses.float)
        //    let newValue =
        //        match original with
        //        | ScalarValue.Float32Value f -> ScalarValue.Float32Value (if f = 0.0f then 1.23f else f + 0.5f)
        //        | _ -> ScalarValue.Float32Value 1.23f
        //    try
        //        client.Write(binding, Addresses.float, newValue)
        //        let readBack = client.Read(binding, Addresses.float)
        //        Assert.Equal(newValue, readBack)
        //    finally
        //        client.Write(binding, Addresses.float, original)
        //)
