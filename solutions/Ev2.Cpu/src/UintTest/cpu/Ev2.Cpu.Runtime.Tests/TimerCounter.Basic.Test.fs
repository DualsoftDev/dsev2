module Ev2.Cpu.Runtime.Tests.TimerCounterBasic

open System
open System.Threading.Tasks
open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Runtime

let inline ticksAfter startMs deltaMs =
    Timebase.addMilliseconds startMs deltaMs

[<Fact>]
let ``TON enable true should start timer`` () =
    let ctx = Context.create()
    let start = Timebase.nowTicks()
    let doneAtStart = Context.updateTimerOnWithTimestamp ctx "T1" true 50 start
    doneAtStart |> should equal false

    let mid = ticksAfter start 25
    Context.updateTimerOnWithTimestamp ctx "T1" true 50 mid |> should equal false

    let complete = ticksAfter start 60
    Context.updateTimerOnWithTimestamp ctx "T1" true 50 complete |> should equal true

[<Fact>]
let ``TON disable resets accumulated state`` () =
    let ctx = Context.create()
    let start = Timebase.nowTicks()
    Context.updateTimerOnWithTimestamp ctx "T_RST" true 30 start |> ignore
    let mid = ticksAfter start 20
    Context.updateTimerOnWithTimestamp ctx "T_RST" false 30 mid |> should equal false
    let resumed = ticksAfter start 40
    Context.updateTimerOnWithTimestamp ctx "T_RST" true 30 resumed |> should equal false

[<Fact>]
let ``TON preset zero completes immediately`` () =
    let ctx = Context.create()
    Context.updateTimerOn ctx "T_ZERO" true 0 |> should equal true

[<Fact>]
let ``TOF countdown clears after preset`` () =
    let ctx = Context.create()
    let start = Timebase.nowTicks()
    Context.updateTimerOffWithTimestamp ctx "T_OF" true 40 start |> should equal true
    let disable = ticksAfter start 10
    Context.updateTimerOffWithTimestamp ctx "T_OF" false 40 disable |> should equal true
    let finish = ticksAfter disable 45
    Context.updateTimerOffWithTimestamp ctx "T_OF" false 40 finish |> should equal false

[<Fact>]
let ``CTU rising edge increments count`` () =
    let ctx = Context.create()
    Context.updateCounterUp ctx "C1" false false 3 |> ignore
    Context.updateCounterUp ctx "C1" true false 3 |> should equal 1
    Context.updateCounterUp ctx "C1" true false 3 |> should equal 1 // sustained high no increment
    Context.updateCounterUp ctx "C1" false false 3 |> should equal 1
    Context.updateCounterUp ctx "C1" true false 3 |> should equal 2

[<Fact>]
let ``CTU reset clears count and done`` () =
    let ctx = Context.create()
    Context.updateCounterUp ctx "C2" true false 1 |> ignore
    Context.updateCounterUp ctx "C2" false true 1 |> should equal 0

[<Fact>]
let ``CTD load sets count to preset`` () =
    let ctx = Context.create()
    Context.updateCounterDown ctx "C_D" false true 5 |> should equal 5
    Context.updateCounterDown ctx "C_D" true false 5 |> should equal 4

[<Fact>]
let ``Timer preset clamps negative value`` () =
    let ctx = Context.create()
    Context.updateTimerOn ctx "NEG" true (-10) |> should equal true

[<Fact>]
let ``Counter preset clamps negative`` () =
    let ctx = Context.create()
    // Negative preset is clamped to 0, but counter can still increment (IEC 61131-3 compliant)
    // First call with count=true causes rising edge (LastCountInput: false->true), incrementing to 1
    // Done bit is true immediately since Count(1) >= Preset(0)
    Context.updateCounterUp ctx "NEG_C" true false (-5) |> should equal 1

[<Fact>]
let ``Concurrent timer updates maintain consistency`` () =
    let ctx = Context.create()
    let start = Timebase.nowTicks()
    let iterations = 100

    let worker offset =
        Task.Run(fun () ->
            for i in 0 .. iterations do
                let ticks = ticksAfter start (offset + i)
                Context.updateTimerOnWithTimestamp ctx "T_CON" true 200 ticks |> ignore)

    let tasks = [| for offset in 0 .. 3 -> worker (offset * 2) |]
    Task.WaitAll tasks

    let finish = ticksAfter start 250
    Context.updateTimerOnWithTimestamp ctx "T_CON" true 200 finish |> should equal true

[<Fact>]
let ``Timer preset change during timing updates done state`` () =
    let ctx = Context.create()
    let start = Timebase.nowTicks()
    Context.updateTimerOnWithTimestamp ctx "T_CHG" true 100 start |> ignore
    let mid = ticksAfter start 60
    Context.updateTimerOnWithTimestamp ctx "T_CHG" true 100 mid |> ignore
    let finish = ticksAfter mid 15
    Context.updateTimerOnWithTimestamp ctx "T_CHG" true 50 finish |> should equal true

[<Fact>]
let ``Counter preset change updates done state`` () =
    let ctx = Context.create()
    Context.updateCounterUp ctx "C_CHG" true false 5 |> ignore
    Context.updateCounterUp ctx "C_CHG" true false 5 |> ignore
    // Change preset to lower value mid-operation
    Context.updateCounterUp ctx "C_CHG" true false 2 |> ignore
    Context.updateCounterUp ctx "C_CHG" false false 2 |> ignore
    Context.updateCounterUp ctx "C_CHG" true false 2 |> ignore
    match Context.tryGetCounterInfo ctx "C_CHG" with
    | Some info ->
        info.Preset |> should equal 2
        info.Done   |> should equal true
    | None -> failwith "Counter info missing"
