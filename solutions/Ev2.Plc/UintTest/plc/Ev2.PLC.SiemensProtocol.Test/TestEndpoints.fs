namespace Ev2.PLC.SiemensProtocol.Tests

open System
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.SiemensProtocol

module TestEndpoints =

    type SiemensEndpoint =
        {
            Name: string
            Ip: string
            Cpu: string
            Rack: int
            Slot: int
        }

    let siemensCp    = { Name = "Siemens CP";    Ip = "192.168.9.96"; Cpu = "S7300"; Rack = 0; Slot = 2 }
    let siemensS315  = { Name = "Siemens S315";  Ip = "192.168.9.97"; Cpu = "S7300"; Rack = 0; Slot = 2 }
    let siemensS1500 = { Name = "Siemens S1500"; Ip = "192.168.9.99"; Cpu = "S71500"; Rack = 0; Slot = 0 }

    let defaultTimeoutMs = 5_000

    let createConnectionOptions endpoint =
        let additionalParams =
            [
                "cpu", endpoint.Cpu
                "rack", string endpoint.Rack
                "slot", string endpoint.Slot
                "timeout", string defaultTimeoutMs
            ]
            |> Map.ofList
        {
            ConnectionOptions.Default with
                Config =
                    { ConnectionConfig.Default with
                        Host = endpoint.Ip
                        Port = 102
                        Protocol = Protocol.Siemens SiemensProtocol.S7_1500
                        Timeout = defaultTimeoutMs
                        AdditionalParams = additionalParams }
        }

    let private mapProtocol cpu =
        match cpu with
        | "S71500" -> SiemensProtocol.S7_1500
        | "S71200" -> SiemensProtocol.S7_1200
        | "S7300"
        | "S7400" -> SiemensProtocol.S7_300_400
        | "S7200" -> SiemensProtocol.S7_200
        | _ -> SiemensProtocol.S7_1200

    let createConnectionSettings endpoint =
        let options = createConnectionOptions endpoint
        let protocol = mapProtocol endpoint.Cpu
        let config =
            { options.Config with
                Protocol = Protocol.Siemens protocol
                AdditionalParams = options.Config.AdditionalParams }
        { options with Config = config }
        |> SiemensConnectionSettingsLoader.fromOptions

    let createClient endpoint =
        let settings = createConnectionSettings endpoint
        new SiemensClient(endpoint.Ip, settings)

    let createDriver endpoint plcId =
        let options = createConnectionOptions endpoint
        let protocol = mapProtocol endpoint.Cpu
        let config = { options.Config with Protocol = Protocol.Siemens protocol }
        let options = { options with Config = config }
        let logger = (NullLogger.Instance :> ILogger)
        new SiemensPlcDriver(plcId, options, logger)

    let createTagConfiguration plcId address (dataType: PlcTagDataType) =
        let plcAddress =
            {
                Raw = address
                DeviceType = address
                Index = 0
                BitIndex = None
                ArrayLength = None
                DataSize = dataType.Size
            }
        TagConfiguration.Create(Guid.NewGuid().ToString("N"), plcId, address, plcAddress, dataType)

    let createTagInfo address dataType isLowSpeed isOutput =
        {
            Name = address
            Address = address
            DataType = Some dataType
            Comment = ""
            IsLowSpeedArea = isLowSpeed
            IsOutput = isOutput
        }

    module Addresses =
        let private envOrDefault env fallback =
            match Environment.GetEnvironmentVariable env with
            | null | "" -> fallback
            | value -> value
        let bit = envOrDefault "SIEMENS_TEST_BIT" "M0.0"
        let int16 = envOrDefault "SIEMENS_TEST_INT16" "MW0"
        let int32 = envOrDefault "SIEMENS_TEST_INT32" "MD4"
        let float = envOrDefault "SIEMENS_TEST_FLOAT" "MD8"
