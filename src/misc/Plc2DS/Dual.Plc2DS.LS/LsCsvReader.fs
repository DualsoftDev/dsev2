namespace Dual.Plc2DS.LS

open Dual.Common.Core.FS
open Dual.Plc2DS.Common.FS


[<AutoOpen>]
module Ls =
    type DeviceComment = {
        Device: string
        Comment: string
        Label: string
    } with
        interface IDeviceComment


    type CsvReader =
        static member ReadCommentCSV(filePath: string): DeviceComment[] =
            [||]