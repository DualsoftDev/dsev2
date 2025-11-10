namespace Ev2.PLC.Driver.Base

open System
open System.ComponentModel
open Ev2.PLC.Common.Types

/// Base implementation for scanned tags shared by protocol drivers.
[<AbstractClass>]
type DsScanTagBase(name: string, address: string, dataType: PlcTagDataType, ?comment: string) =

    let mutable value: obj = null
    let mutable pendingWrite: obj option = None

    member _.Name = name
    member _.Address = address
    member _.DataType = dataType

    [<Browsable(false)>]
    member val IsLowSpeedArea = false with get, set

    member this.Value
        with get() = value
        and set v = value <- v

    member _.Comment = defaultArg comment ""

    abstract member ReadWriteType: ReadWriteType

    [<Browsable(false)>]
    abstract member IsMemory: bool

    /// Updates the value using the supplied buffer and indicates whether it changed.
    abstract member UpdateValue: byte[] -> bool
    default _.UpdateValue _ = false

    member this.SetWriteValue(v: obj) = pendingWrite <- Some v
    member this.ClearWriteValue() = pendingWrite <- None
    member this.GetWriteValue() = pendingWrite

    override this.ToString() =
        $"[{this.ReadWriteType}] {name} @ {address} ({dataType})"


        
/// Event payload describing connection transitions.
type ConnectChangedEventArgs =
    {
        Ip: string
        State: PlcConnectionStatus
    }

/// Event payload for tag value updates during scanning.
type DsScanTagValueChangedEventArgs =
    {
        Ip: string
        Tag: DsScanTagBase
    }

