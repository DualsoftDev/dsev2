namespace Ev2.S7Protocol.Tests

open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestHelpers

module CoreTypesTests =

    [<Fact>]
    let ``CpuType enum exposes expected value`` () =
        assertEqual 10 (int CpuType.S7300)
