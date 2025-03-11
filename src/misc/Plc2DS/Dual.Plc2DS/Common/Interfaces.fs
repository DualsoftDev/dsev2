namespace Dual.Plc2DS

open System.IO
open Dual.Common.Core.FS

[<AutoOpen>]
module InterfaceModule =
    //type IDataReader = interface end
    //type ILogicReader = interface end

    /// 주로 CSV 를 통해 읽어 들인, vendor 별 PLC 태그 정보를 담는 인터페이스
    type IPlcTag = interface end

type Vendor =
    | AB
    | S7
    | LS
    | MX

type SemanticCategory =
    | DuNone
    | DuAction
    | DuDevice
    | DuFlow
    | DuModifier
    | DuPrefixModifier
    | DuPostfixModifier
    | DuState
    | DuUnmatched
    with
        member x.IsMandatory = x.IsDuAction || x.IsDuDevice || x.IsDuFlow


/// Positional index type
type PIndex = int

type CategorySummary = {
    Multiples: (SemanticCategory * PIndex[])[]    // e.g. [Action, [2; 4]]
    Nopes: PIndex[]
    Uniqs: (PIndex * SemanticCategory)[]    // e.g. (1, Modifier)
    Showns: SemanticCategory[]
    NotShowns: SemanticCategory[]
} with
    member x.ShownsMandatory    = x.Showns    |> filter _.IsMandatory
    member x.NotShownsMandatory = x.NotShowns |> filter _.IsMandatory