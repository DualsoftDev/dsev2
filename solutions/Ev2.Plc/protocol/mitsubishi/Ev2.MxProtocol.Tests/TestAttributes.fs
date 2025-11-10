module Ev2.MxProtocol.Tests.TestAttributes

open System
open Xunit

[<AttributeUsage(AttributeTargets.Method)>]
type RequiresMelsecPLCAttribute() =
    inherit FactAttribute()
    do
        let plcHost = Environment.GetEnvironmentVariable("MELSEC_PLC_HOST")
        if String.IsNullOrEmpty(plcHost) then
            // Set default values based on selected PLC
            let selectedPlc = Environment.GetEnvironmentVariable("MELSEC_TEST_PLC")
            match selectedPlc with
            | "PLC2" | "2" ->
                Environment.SetEnvironmentVariable("MELSEC_PLC_HOST", "192.168.9.121")
                Environment.SetEnvironmentVariable("MELSEC_PLC_PORT", "5002")
            | _ -> // Default to PLC1
                Environment.SetEnvironmentVariable("MELSEC_PLC_HOST", "192.168.9.120")
                Environment.SetEnvironmentVariable("MELSEC_PLC_PORT", "7777")

// Specific PLC test attributes
[<AttributeUsage(AttributeTargets.Method)>]
type RequiresPLC1Attribute() =
    inherit FactAttribute()
    do
        Environment.SetEnvironmentVariable("MELSEC_PLC_HOST", "192.168.9.120")
        Environment.SetEnvironmentVariable("MELSEC_PLC_PORT", "7777")

[<AttributeUsage(AttributeTargets.Method)>]
type RequiresPLC2Attribute() =
    inherit FactAttribute()
    do
        Environment.SetEnvironmentVariable("MELSEC_PLC_HOST", "192.168.9.121")
        Environment.SetEnvironmentVariable("MELSEC_PLC_PORT", "5002")


[<AttributeUsage(AttributeTargets.Method)>]
type LongRunningTestAttribute() =
    inherit FactAttribute()
    do
        let skipLongTests = Environment.GetEnvironmentVariable("SKIP_LONG_TESTS")
        if skipLongTests = "true" then
            base.Skip <- "Long running tests are skipped."

module TestCategory =
    [<Literal>]
    let Unit = "Unit"
    [<Literal>]
    let Integration = "Integration"
    [<Literal>]
    let Performance = "Performance"
    [<Literal>]
    let Stress = "Stress"

[<AttributeUsage(AttributeTargets.Method, AllowMultiple = true)>]
type CategoryAttribute(category: string) =
    inherit Attribute()
    member _.Category = category