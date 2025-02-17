namespace CpuEvalTest

//module Cpu

open System
open System.Collections.Generic
open System.Linq


[<AutoOpen>]
module MainModule =
    /// 프로그램 실행부
    [<EntryPoint>]
    let main argv =
        asyncScanLoop() |> Async.RunSynchronously
        1
