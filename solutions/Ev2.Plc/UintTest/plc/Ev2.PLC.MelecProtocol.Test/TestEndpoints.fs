namespace Ev2.PLC.MelecProtocol.Tests

open System
open System.Net.Sockets
open Ev2.PLC.MelecProtocol

module TestEndpoints =

    let ipMitsubishiLocalEthernet = "192.168.9.120"
    let ipMitsubishiEthernet = "192.168.9.121"
    let defaultPort = 5000
    let defaultTimeoutMs = 2000
    let defaultIsUdp = false

    type TestMxEthernet() =
        let client = new MxEthernet(ipMitsubishiEthernet, defaultPort, defaultTimeoutMs, true)
        
        interface IDisposable with
            member _.Dispose() =
                (client :> IDisposable).Dispose()

        member _.Client = client

        member _.WriteWord(device: MxDevice, address: int, value: int) =
            client.WriteWord(device, address, value)

        member _.WriteBit(device: MxDevice, address: int, value: int) =
            client.WriteBit(device, address, value)

        member _.ReadWords(device: MxDevice, address: int, count: int) =
            client.ReadWords(device, address, count)

        member _.ReadBits(device: MxDevice, address: int, count: int) =
            client.ReadBits(device, address, count)

        member _.WriteWordRandom(devices: (MxDevice * int * int)[]) =
            client.WriteWordRandom(devices)

        member _.WriteBitRandom(devices: (MxDevice * int * int)[]) =
            client.WriteBitRandom(devices)
            
  