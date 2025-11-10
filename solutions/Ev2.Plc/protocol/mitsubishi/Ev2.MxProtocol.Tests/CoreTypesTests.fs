module Ev2.MxProtocol.Tests.CoreTypesTests

open System
open Xunit
open Ev2.MxProtocol.Core

[<Fact>]
let ``DeviceCode.ToByte returns correct byte values`` () =
    // Bit devices
    Assert.Equal(0x91uy, DeviceCode.SM.ToByte())
    Assert.Equal(0xA9uy, DeviceCode.SD.ToByte())
    Assert.Equal(0x9Cuy, DeviceCode.X.ToByte())
    Assert.Equal(0x9Duy, DeviceCode.Y.ToByte())
    Assert.Equal(0x90uy, DeviceCode.M.ToByte())
    Assert.Equal(0x92uy, DeviceCode.L.ToByte())
    Assert.Equal(0x93uy, DeviceCode.F.ToByte())
    Assert.Equal(0x94uy, DeviceCode.V.ToByte())
    Assert.Equal(0xA0uy, DeviceCode.B.ToByte())
    
    // Word devices
    Assert.Equal(0xA8uy, DeviceCode.D.ToByte())
    Assert.Equal(0xB4uy, DeviceCode.W.ToByte())
    Assert.Equal(0xAFuy, DeviceCode.R.ToByte())
    Assert.Equal(0xB0uy, DeviceCode.ZR.ToByte())
    Assert.Equal(0xC2uy, DeviceCode.T.ToByte())
    Assert.Equal(0xC5uy, DeviceCode.C.ToByte())

[<Fact>]
let ``DeviceCode.IsWordDevice correctly identifies device types`` () =
    // Word devices should return true
    Assert.True(DeviceCode.D.IsWordDevice())
    Assert.True(DeviceCode.W.IsWordDevice())
    Assert.True(DeviceCode.R.IsWordDevice())
    Assert.True(DeviceCode.ZR.IsWordDevice())
    Assert.True(DeviceCode.T.IsWordDevice())
    Assert.True(DeviceCode.C.IsWordDevice())
    Assert.True(DeviceCode.Z.IsWordDevice())
    
    // Bit devices should return false
    Assert.False(DeviceCode.M.IsWordDevice())
    Assert.False(DeviceCode.X.IsWordDevice())
    Assert.False(DeviceCode.Y.IsWordDevice())
    Assert.False(DeviceCode.SM.IsWordDevice())
    Assert.False(DeviceCode.L.IsWordDevice())
    Assert.False(DeviceCode.F.IsWordDevice())
    Assert.False(DeviceCode.V.IsWordDevice())
    Assert.False(DeviceCode.B.IsWordDevice())

[<Fact>]
let ``EndCode identifies success and error correctly`` () =
    let successCode = EndCodeSuccess 0x0000us
    Assert.True(successCode.IsSuccess)
    Assert.Equal(0x0000us, successCode.Code)
    
    let errorCode = EndCodeError (0x5001us, 0x01uy, 0x02uy)
    Assert.False(errorCode.IsSuccess)
    Assert.Equal(0x5001us, errorCode.Code)

[<Fact>]
let ``MelsecConfig timeout calculation works correctly`` () =
    let config = {
        Name = "Test"
        Host = "192.168.1.1"
        Port = 5000
        Timeout = TimeSpan.FromMilliseconds(500.0)
        FrameType = FrameType.QnA_3E_Binary
        AccessRoute = {
            NetworkNumber = 0x00uy
            StationNumber = 0x00uy
            IoNumber = 0x03FFus
            RelayType = 0x00uy
        }
        MonitoringTimer = 0x0010us
    }
    
    // Should return minimum 1000ms even if configured less
    Assert.Equal(1000, config.TimeoutMilliseconds)
    
    let config2 = { config with Timeout = TimeSpan.FromSeconds(5.0) }
    Assert.Equal(5000, config2.TimeoutMilliseconds)

[<Fact>]
let ``CommandCode enum values are correct`` () =
    Assert.Equal(0x0401us, uint16 CommandCode.BatchRead)
    Assert.Equal(0x1401us, uint16 CommandCode.BatchWrite)
    Assert.Equal(0x0403us, uint16 CommandCode.RandomRead)
    Assert.Equal(0x1402us, uint16 CommandCode.RandomWrite)
    Assert.Equal(0x0406us, uint16 CommandCode.MultiBlockRead)
    Assert.Equal(0x1406us, uint16 CommandCode.MultiBlockWrite)
    Assert.Equal(0x1001us, uint16 CommandCode.RemoteRun)
    Assert.Equal(0x1002us, uint16 CommandCode.RemoteStop)
    Assert.Equal(0x0101us, uint16 CommandCode.ReadCpuType)

[<Fact>]
let ``SubcommandCode enum values are correct`` () =
    Assert.Equal(0x0001us, uint16 SubcommandCode.BitUnits)
    Assert.Equal(0x0000us, uint16 SubcommandCode.WordUnits)

[<Fact>]
let ``DeviceAccess structure stores values correctly`` () =
    let access = {
        DeviceCode = DeviceCode.D
        HeadNumber = 100
        Count = 10us
    }
    
    Assert.Equal(DeviceCode.D, access.DeviceCode)
    Assert.Equal(100, access.HeadNumber)
    Assert.Equal(10us, access.Count)

[<Fact>]
let ``RandomDeviceAccess structure stores values correctly`` () =
    let access = {
        DeviceCode = DeviceCode.M
        DeviceNumber = 500
        AccessSize = 1uy
    }
    
    Assert.Equal(DeviceCode.M, access.DeviceCode)
    Assert.Equal(500, access.DeviceNumber)
    Assert.Equal(1uy, access.AccessSize)

[<Fact>]
let ``BlockAccess structure stores values correctly`` () =
    let block = {
        DeviceCode = DeviceCode.W
        HeadNumber = 0
        PointCount = 100us
    }
    
    Assert.Equal(DeviceCode.W, block.DeviceCode)
    Assert.Equal(0, block.HeadNumber)
    Assert.Equal(100us, block.PointCount)