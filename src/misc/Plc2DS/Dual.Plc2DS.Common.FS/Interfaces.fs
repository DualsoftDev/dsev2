namespace Dual.Plc2DS.Common.FS

open System.IO
open Dual.Common.Core.FS

[<AutoOpen>]

module InterfaceModule =
    type IDataReader = interface end
    type ILogicReader = interface end

    type IDeviceComment = interface end

    type DeviceComment = {
        Device: string      // WOrk3 에서는 "Assign (Device/Label)" 로 표시
        Label: string       // 일반적으로 null
        Comment: string

        // Works3.csv

        /// "VAR_GLOBAL",
        Class: string
        DataType: string
        Address: string

    } with
        static member CreateDefault() = { Device = ""; Label = ""; Comment = ""; Class = "VAR_GLOBAL"; DataType = "BOOL"; Address = "" }
        static member Create(device, comment, ?label:string) = {
            DeviceComment.CreateDefault() with
                Device = device
                Comment = comment
                Label = label |? null
        }
