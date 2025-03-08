namespace Dual.Plc2DS

open System.IO
open Dual.Common.Core.FS

[<AutoOpen>]
module InterfaceModule =
    //type IDataReader = interface end
    //type ILogicReader = interface end

    /// 주로 CSV 를 통해 읽어 들인, vendor 별 PLC 태그 정보를 담는 인터페이스
    type IPlcTag = interface end

[<RequireQualifiedAccess>]
module K =
    let [<Literal>] AB = "AB"
    let [<Literal>] S7 = "S7"
    let [<Literal>] LS = "LS"
    let [<Literal>] MX = "MX"

type SemanticCategory =
    | Nope
    | Action
    | Device
    | Flow
    | Modifier
    | State

/// Positional index type
type PIndex = int

type CategorySummary = {
    Multiples: (SemanticCategory * PIndex[])[]    // e.g. [Action, [2; 4]]
    Nopes: PIndex[]
    Uniqs: (PIndex * SemanticCategory)[]    // e.g. (1, Modifier)
    Showns: SemanticCategory[]
    NotShowns: SemanticCategory[]
}