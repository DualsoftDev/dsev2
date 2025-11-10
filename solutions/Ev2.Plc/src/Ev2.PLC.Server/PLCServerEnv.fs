namespace DSPLCServer

open Dual.PLC.Common.FS
open System.Collections
open System.Collections.Generic

[<AutoOpen>]
module RuntimeEnvModule =


    type RuntimeScanState =
        {
            mutable ScanManager: IDsScanManager option
            mutable DsScanTags: IDictionary<string, DsScanTagBase>
        }

    and IDsScanManager =
        abstract member ActiveIPs: seq<string>
        abstract member GetScanner: string -> DsScanBase option
        abstract member DisconnectAll: unit -> unit