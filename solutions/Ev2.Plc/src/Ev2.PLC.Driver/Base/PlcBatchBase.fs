namespace Ev2.PLC.Driver.Base

open System
open Ev2.PLC.Common.Types

/// Generic batch container used by protocol implementations.
[<AbstractClass>]
type PlcBatchBase<'T when 'T :> DsScanTagBase>(buffer: byte[], initialTags: 'T[]) =

    let mutable tags = Array.copy initialTags

    member val Buffer = buffer with get, set

    member this.Tags = Array.copy tags

    member this.SetTags(newTags: 'T[]) =
        tags <- Array.copy newTags

    abstract member BatchToText: unit -> string
