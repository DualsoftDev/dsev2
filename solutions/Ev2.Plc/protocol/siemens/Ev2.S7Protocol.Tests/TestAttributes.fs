module Ev2.S7Protocol.Tests.TestAttributes

open System
open Xunit

[<AttributeUsage(AttributeTargets.Method)>]
type RequiresS7PLCAttribute() =
    inherit FactAttribute()
    do
        let skipIntegration = false // Force integration tests to run
        let ip = "192.168.9.97" // Force use of Siemens LocalEthernet PLC
        if skipIntegration then
            base.Skip <- "S7 integration tests require a configured PLC. Set S7_TEST_IP and unset S7_SKIP_INTEGRATION to run."
