namespace Dual.Plc2DS

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions

type Plc2DsExtensionForCSharp =
    [<Extension>] static member CsTryGetFDA(tag:IPlcTag, fdaPatterns:Regex[]) = tag.TryGetFDA(fdaPatterns);
    [<Extension>] static member CsSetFDA(tag:IPlcTag, optFDA:PlcTagBaseFDA option) = tag.SetFDA(optFDA);
    [<Extension>] static member CsGetName(tag:IPlcTag) = tag.GetName();
    [<Extension>] static member CsSetName(tag:IPlcTag, n:string) = tag.SetName(n);
    [<Extension>] static member CsIsValid(tag:IPlcTag) = tag.IsValid();
    [<Extension>] static member CsGetTagType(vendor:Vendor):Type = vendor.GetVendorTagType()
