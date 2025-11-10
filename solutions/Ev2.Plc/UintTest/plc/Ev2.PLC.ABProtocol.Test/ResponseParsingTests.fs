namespace Ev2.PLC.ABProtocol.Tests

open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.ABProtocol

module ResponseParsingTests =

    [<Fact>]
    let ``connection settings loader honours additional parameters`` () =
        let config =
            { ConnectionConfig.Default with
                Host = "192.168.1.10"
                Timeout = 3_000
                AdditionalParams =
                    [
                        "path", "1,0"
                        "cpu", "PLC5"
                        "debug", "2"
                    ]
                    |> Map.ofList }
        let options = { ConnectionOptions.Default with Config = config }
        let settings = AbConnectionSettingsLoader.fromOptions options
        Assert.Equal(Some "1,0", settings.Path)
        Assert.Equal(CpuType.PLC5, settings.Cpu)
        Assert.Equal(3_000, settings.TimeoutMs)
        Assert.Equal(2, settings.DebugLevel)

    [<Fact>]
    let ``connection settings loader falls back to defaults`` () =
        let config = { ConnectionConfig.Default with Timeout = -1 }
        let options = { ConnectionOptions.Default with Config = config }
        let settings = AbConnectionSettingsLoader.fromOptions options
        Assert.Equal(None, settings.Path)
        Assert.Equal(CpuType.LGX, settings.Cpu)
        Assert.Equal(AbConnectionSettings.Default.TimeoutMs, settings.TimeoutMs)
        Assert.Equal(0, settings.DebugLevel)
