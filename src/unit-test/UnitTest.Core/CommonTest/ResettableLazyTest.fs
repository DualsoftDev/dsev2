module ResettableLazyTest

open System.Reflection
open NUnit.Framework

open Dual.Common.Core.FS
open Dual.Common.UnitTest.FS
open Dual.Ev2
open System
open Dual.Common.Base

[<TestFixture>]
type ResettableLazyTest() =


    [<Test>]
    member _.Function() =
        let lz = ResettableLazy<int64>(fun () -> DateTime.Now.Ticks)
        lz.OnValueChanged <- Some (fun v -> tracefn $"onValueChanged: {lz.Value} == {v}")
        let a = lz.Value
        lz.Reset() |> ignore
        let a = lz.Value
        lz.Reset() |> ignore
        noop()

