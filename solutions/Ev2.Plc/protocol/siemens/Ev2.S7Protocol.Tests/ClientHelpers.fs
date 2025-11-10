namespace Ev2.S7Protocol.Tests

open System
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Client

module ClientHelpers =
    
    let buildConfig () : S7Config =
        { Name = TestHelpers.s7Name
          IpAddress = TestHelpers.s7Ip
          CpuType = TestHelpers.s7CpuType
          Rack = TestHelpers.s7Rack
          Slot = TestHelpers.s7Slot
          Port = TestHelpers.s7Port
          LocalTSAP = TestHelpers.s7LocalTsap
          RemoteTSAP = TestHelpers.s7RemoteTsap
          Timeout = TimeSpan.FromMilliseconds(float TestHelpers.s7TimeoutMs)
          MaxPDUSize = TestHelpers.s7MaxPdu
          Password = TestHelpers.s7Password }
    
    let createClient () =
        let config = buildConfig ()
        let logger = TestHelpers.createPacketLogger config
        new S7Client(config, packetLogger = logger)
