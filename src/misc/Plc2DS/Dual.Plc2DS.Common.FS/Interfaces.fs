namespace Dual.Plc2DS.Common.FS

open System.IO
open Dual.Common.Core.FS

[<AutoOpen>]

module InterfaceModule =
    type IDataReader = interface end
    type ILogicReader = interface end

    type IDeviceComment = interface end

