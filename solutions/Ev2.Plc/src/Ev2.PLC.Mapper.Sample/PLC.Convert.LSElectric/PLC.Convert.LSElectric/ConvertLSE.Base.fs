namespace PLC.Convert.LSElectric

open System
open System.ComponentModel
open System.Diagnostics
open System.Runtime.Serialization
open System.Text.Json.Serialization
open Dual.Common.Core.FS

module XgxBase =

    type IPlcTag = interface end

    type Choice =
        | Stage
        | Discarded
        | Chosen
        | Categorized

    [<DebuggerDisplay("{Stringify()}")>]
    [<DataContract>]
    type PlcTagBaseFDA(?flow, ?device, ?action) =
        new () = PlcTagBaseFDA("", "", "") 

        interface IPlcTag

        [<DataMember>] member val FlowName   = defaultArg flow ""   with get, set
        [<DataMember>] member val DeviceName = defaultArg device "" with get, set
        [<DataMember>] member val ActionName = defaultArg action "" with get, set
        [<DataMember>] member val Choice     = Choice.Stage         with get, set
        [<JsonIgnore>] [<Browsable(false)>]
        member val Temporary : obj = null with get, set

        member x.Set(flow, device, action) =
            x.FlowName   <- flow
            x.DeviceName <- device
            x.ActionName <- action

        member x.TryGet() =
            if x.FlowName <> "" && x.DeviceName <> "" && x.ActionName <> "" then Some x else None

        member x.GetTuples() = x.FlowName, x.DeviceName, x.ActionName

        abstract member Stringify: unit -> string
        abstract member Csvify: unit -> string
        abstract member OnDeserialized: unit -> unit
        abstract member OnSerializing: unit -> unit

        default x.Stringify() = $"{x.FlowName}:{x.DeviceName}:{x.ActionName}"
        default x.Csvify()    = $"{x.FlowName},{x.DeviceName},{x.ActionName}"
        default x.OnDeserialized() = ()
        default x.OnSerializing()  = ()

        [<OnDeserialized>] member x.OnDeserializedMethod(_: StreamingContext) = x.OnDeserialized()
        [<OnSerializing>]  member x.OnSerializingMethod(_: StreamingContext)  = x.OnSerializing()

    [<DebuggerDisplay("{Stringify()}")>]
    [<DataContract>]
    type PlcTagInfo
        (
            ?typ, ?scope, ?variable, ?address,
            ?dataType, ?property, ?comment, ?outputFlag
        ) =
        inherit PlcTagBaseFDA()

        let v = defaultArg variable ""

        [<DataMember>] member val Type        = defaultArg typ ""            with get, set
        [<DataMember>] member val Scope       = defaultArg scope ""          with get, set
        [<DataMember>] member val Variable    = v                            with get, set
        [<DataMember>] member val Address     = defaultArg address ""        with get, set
        [<DataMember>] member val DataType    = defaultArg dataType ""       with get, set
        [<DataMember>] member val Property    = defaultArg property ""       with get, set
        [<DataMember>] member val Comment     = defaultArg comment ""        with get, set
        [<DataMember>] member val OutputFlag  = defaultArg outputFlag false  with get, set

        [<DataMember>] member val VariableProcessing = v with get, set
        [<JsonIgnore>] member val VariableOriginal   = v with get, set

        new () = PlcTagInfo()

        override x.Stringify() =
            $"{x.Variable} = {base.Stringify()}, {x.Address}, {x.Type}, {x.DataType}, {x.Comment}"

        override x.Csvify() =
            $"{x.Type},{x.Scope},{x.Variable},{x.Address},{x.DataType},{x.Property},{x.Comment},{x.VariableOriginal},{base.Csvify()}"

        override x.OnDeserialized() =
            x.Variable         <- x.VariableProcessing
            x.VariableOriginal <- x.Variable

        override x.OnSerializing() =
            x.VariableProcessing <- x.Variable
            x.Variable           <- x.VariableOriginal
