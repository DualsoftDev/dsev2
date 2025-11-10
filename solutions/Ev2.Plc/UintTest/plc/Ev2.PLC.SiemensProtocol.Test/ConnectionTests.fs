namespace Ev2.PLC.SiemensProtocol.Tests

open Xunit
open Ev2.PLC.SiemensProtocol
open TestEndpoints

module ConnectionTests =

    [<Fact>]
    let ``Connection settings resolve rack slot`` () =
        let settings = createConnectionSettings siemensS315
        Assert.Equal(0, settings.Rack)
        Assert.Equal(0, settings.Slot)
        Assert.Equal(Some "S71200", settings.CpuName)

    [<Fact>]
    let ``Connection settings default cycle delay`` () =
        let settings = createConnectionSettings siemensS1500
        Assert.True(settings.CyclicDelayMs > 0)
