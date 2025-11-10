module Ev2.PLC.Driver.Tests.DriverConfigurationTests

open Xunit
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Interfaces

let private logger = NullLogger.Instance

let private defaultOptions =
    { ConnectionOptions.Default with
        Config =
            { ConnectionOptions.Default.Config with
                Host = "127.0.0.1"
                Port = 44818
                Protocol = Protocol.AllenBradley AllenBradleyProtocol.EtherNetIP_Logix }
        EnableKeepAlive = false }

