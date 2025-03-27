namespace Dual.Plc2DS

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open Dual.Common.Core.FS

type FDAT =
    | DuFlow
    | DuDevice
    | DuAction
    | DuTag

type Plc2DsExtensionForCSharp =
    [<Extension>] static member CsTryGetFDA(tag:IPlcTag, fdaPatterns:Regex[]) = tag.TryGetFDA(fdaPatterns);
    [<Extension>] static member CsSetFDA(tag:IPlcTag, optFDA:PlcTagBaseFDA option) = tag.SetFDA(optFDA);
    [<Extension>] static member CsGetName(tag:IPlcTag) = tag.GetName();
    [<Extension>] static member CsSetName(tag:IPlcTag, n:string) = tag.SetName(n);
    [<Extension>] static member CsIsValid(tag:IPlcTag) = tag.IsValid();
    [<Extension>] static member CsGetTagType(vendor:Vendor):Type = vendor.GetVendorTagType()
    [<Extension>] static member CsTryMatch(pattern: CsvFilterExpression, tag: PlcTagBaseFDA) : bool option = if isItNull pattern then None else pattern.TryMatch(tag)

