namespace Ev2.PLC.ABProtocol.Tests

open System
open System.IO
open Xunit
open Ev2.PLC.ABProtocol
open TestEndpoints

module WriteReadTests =

    let private runWithTagManager action =
        try
            use manager = createTagManager ()
            action manager
        with
        | :? InvalidOperationException as ex -> Assert.True(false, $"Allen-Bradley PLC connection failed ({ex.Message})")
        | :? IOException as ex -> Assert.True(false, $"Allen-Bradley PLC IO failure ({ex.Message})")

    [<Fact>]
    let ``Read bit tag returns boolean`` () =


            // Logix 5000, 백플레인(1), 슬롯(0), 타임아웃 3000ms
        let mgr = new TagManager("192.168.9.110", "1,0", CpuType.LGX, 3000)

        // 단일 값 읽기
        let b = mgr.ReadBit("N[100].0")
        //let n = mgr.ReadInt32("MyDint")
        b
        //runWithTagManager (fun manager ->
        //    let tag = new Tag(manager, "bit1", TagDataType.Bit, 1)
        //    let status = manager.Read(tag)
        //    Assert.Equal(0, status)
        //    let value = manager.ReadValue(tag, 0)
        //    Assert.IsType<bool>(value) |> ignore
        //)

    [<Fact>]
    let ``Rewrite int16 tag preserves roundtrip`` () =
        runWithTagManager (fun manager ->
            let tag = new Tag(manager, "N[10]", TagDataType.Int16, 1)
            let status = manager.Read(tag)
            Assert.Equal(0, status)
            let original = manager.ReadValue(tag, 0) |> Convert.ToInt16
            let candidate = if original = Int16.MaxValue then original - 1s else original + 1s
            try
                manager.WriteValue(tag, 0, box candidate)
                ignore (manager.Read(tag))
                let readBack = manager.ReadValue(tag, 0) |> Convert.ToInt16
                Assert.Equal(candidate, readBack)
            finally
                manager.WriteValue(tag, 0, box original)
        )

    [<Fact>]
    let ``Read string tag returns text`` () =
        runWithTagManager (fun manager ->
            let tag = new Tag(manager, "text1", TagDataType.String, 1)
            let status = manager.Read(tag)
            Assert.Equal(0, status)
            let value = manager.ReadValue(tag, 0)
            Assert.IsType<string>(value) |> ignore
        )
