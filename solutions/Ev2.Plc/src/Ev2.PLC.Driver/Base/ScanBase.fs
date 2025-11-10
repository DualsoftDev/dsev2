namespace Ev2.PLC.Driver.Base

open System
open System.Collections.Generic
open System.Threading
open Ev2.PLC.Common.Types

[<AbstractClass>]
type DsScanBase(ip: string, scanDelay: int, isMonitorOnly: bool) =

    let tagChangedEvent = Event<DsScanTagValueChangedEventArgs>()
    let connectChangedEvent = Event<ConnectChangedEventArgs>()

    let mutable cancellation = new CancellationTokenSource()
    let mutable running = false

    do
        if String.IsNullOrWhiteSpace ip then invalidArg "ip" "PLC IP cannot be empty."

    [<CLIEvent>]
    member _.TagValueChanged = tagChangedEvent.Publish

    [<CLIEvent>]
    member _.ConnectChanged = connectChangedEvent.Publish

    member this.RaiseTagChanged(tag: DsScanTagBase) =
        tagChangedEvent.Trigger { Ip = ip; Tag = tag }

    member this.RaiseConnectChanged(state: ConnectionStatus) =
        connectChangedEvent.Trigger { Ip = ip; State = state }

    abstract member ConnectionClose: unit -> unit
    abstract member IsConnected: bool
    abstract member WriteTags: unit -> unit
    abstract member ReadHighSpeedAreaAsync: int -> Async<unit>
    abstract member ReadLowSpeedAreaAsync: int -> Async<unit>
    abstract member PrepareTags: TagInfo seq -> IDictionary<ScanAddress, DsScanTagBase>
    abstract member GetCurrentScanTimeMs: unit -> int

    member _.IsScanning = running

    member this.Scan(tags: TagInfo seq) =
        cancellation.Cancel()
        let effectiveDelay =
            if scanDelay < 0 then
                let current = this.GetCurrentScanTimeMs() * 3
                if current = 0 then 50 else current
            else scanDelay

        let tagMap = this.PrepareTags(tags)
        let lowSpeedDelay = effectiveDelay * 10
        let mutable elapsed = 0

        async {
            while running do
                do! Async.Sleep 50

            cancellation <- new CancellationTokenSource()
            running <- true
            try
                try
                    while not cancellation.IsCancellationRequested do
                        if isMonitorOnly then
                            if tagMap.Values |> Seq.exists (fun t -> t.GetWriteValue().IsSome) then
                                failwith "Write request detected in monitor-only mode."
                        else
                            this.WriteTags()

                        do! this.ReadHighSpeedAreaAsync(effectiveDelay)

                        elapsed <- elapsed + effectiveDelay
                        if elapsed >= lowSpeedDelay then
                            do! this.ReadLowSpeedAreaAsync(effectiveDelay)
                            elapsed <- 0

                        do! Async.Sleep effectiveDelay
                with ex ->
                    eprintfn "[SCAN ERROR] %s: %s" ip ex.Message
            finally
                running <- false
                cancellation.Cancel()
        }
        |> Async.Start

        tagMap

    member this.StopScan() =
        if not cancellation.IsCancellationRequested then
            cancellation.Cancel()
            async {
                while running do
                    do! Async.Sleep 50
                this.ConnectionClose()
            }
            |> Async.Start
