namespace Ev2.PLC.XgtProtocol.Tests

open System
open Xunit.Sdk
open Ev2.PLC.XgtProtocol

module TestEndpoints =

    let ipXGK = "192.168.9.103"
    let ipXGILocalCpuZ = "192.168.9.101"
    let ipXGILocal = "192.168.9.102"
    let ipXGIEFMTB = "192.168.9.100"
    let ipLoopback = "127.0.0.1"
    let defaultPort = 2004
    let defaultTimeoutMs = 500

    /// Centralised test connection helper so hardware setup happens in one place.
    type TestXgtEthernet(?ip: string, ?localEthernet: bool) as this =
        inherit XgtEthernet(defaultArg ip ipXGILocal, defaultPort, defaultTimeoutMs, defaultArg localEthernet true)
        //inherit XgtEthernet(defaultArg ip ipXGIEFMTB, defaultPort, defaultTimeoutMs, defaultArg localEthernet false)
        
        do
            if not (base.Connect()) then
                failwith (sprintf "Unable to connect to PLC at %s:%d. Set XGT_TEST_IP_* to a reachable endpoint." this.Ip defaultPort)

        member _.EnsureConnected() = ()

        interface IDisposable with
            member this.Dispose() = ignore (this.Disconnect())
