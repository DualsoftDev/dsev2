namespace Ev2.PLC.ABProtocol.Tests

open System
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open ProtocolTestHelper
open Ev2.PLC.Common.Types
open Ev2.PLC.ABProtocol

module TestEndpoints =

    module Env = ProtocolTestHelper.TestEnvironment

    let plcIp = Env.getString ["AB_TEST_IP"; "PLC_AB_TEST_IP"] "192.168.9.110"
    let private defaultPath = Env.getString ["AB_TEST_PATH"; "PLC_AB_TEST_PATH"] "1,0"
    let private defaultTimeoutMs = Env.getInt ["AB_TEST_TIMEOUT_MS"; "PLC_AB_TEST_TIMEOUT_MS"] 5_000
    let private defaultPort =
        let protocol = Protocol.AllenBradley AllenBradleyProtocol.EtherNetIP_Logix
        Env.getInt ["AB_TEST_PORT"; "PLC_AB_TEST_PORT"] protocol.DefaultPort
    let private cpuParameter = Env.getString ["AB_TEST_CPU"; "PLC_AB_TEST_CPU"] "LGX"
    let private debugLevel = Env.getInt ["AB_TEST_DEBUG"; "PLC_AB_TEST_DEBUG"] 0

    let createConnectionOptions () =
        let protocol = Protocol.AllenBradley AllenBradleyProtocol.EtherNetIP_Logix
        let additionalParams =
            [
                "path", defaultPath
                "cpu", cpuParameter
                "debug", string debugLevel
            ]
            |> Map.ofList
        {
            ConnectionOptions.Default with
                Config =
                    { ConnectionConfig.Default with
                        Host = plcIp
                        Port = defaultPort
                        Protocol = protocol
                        Timeout = defaultTimeoutMs
                        AdditionalParams = additionalParams }
        }

    let createConnectionSettings () =
        createConnectionOptions () |> AbConnectionSettingsLoader.fromOptions

    let createTagManager () =
        let settings = createConnectionSettings ()
        let path = settings.Path |> Option.defaultValue defaultPath
        new TagManager(plcIp, path, settings.Cpu, settings.TimeoutMs)

    let createLogger () : ILogger =
        NullLogger.Instance :> ILogger

    let createDriver plcId =
        let options = createConnectionOptions ()
        let logger = createLogger ()
        new AbPlcDriver(plcId, options, logger)

    let createTagConfiguration plcId name (dataType: PlcTagDataType) =
        let address =
            {
                Raw = name
                DeviceType = name
                Index = 0
                BitIndex = None
                ArrayLength = None
                DataSize = dataType.Size
            }
        TagConfiguration.Create(Guid.NewGuid().ToString("N"), plcId, name, address, dataType)

    let createTagInfo name dataType isLowSpeed isOutput =
        {
            Name = name
            Address = name
            DataType = Some dataType
            Comment = ""
            IsLowSpeedArea = isLowSpeed
            IsOutput = isOutput
        }
