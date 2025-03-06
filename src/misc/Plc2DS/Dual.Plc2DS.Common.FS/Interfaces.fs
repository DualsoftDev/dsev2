namespace Dual.Plc2DS.Common.FS

open System.IO
open Dual.Common.Core.FS

[<AutoOpen>]

module InterfaceModule =
    //type IDataReader = interface end
    //type ILogicReader = interface end

    /// 주로 CSV 를 통해 읽어 들인, vendor 별 PLC 태그 정보를 담는 인터페이스
    type IPlcTag = interface end

