namespace Dual.Plc2DS

open System
open System.Runtime.CompilerServices

type Plc2DsExtensionForCSharp =
    [<Extension>] static member CsTryGetFDA(tag:IPlcTag, semantic:Semantic) = tag.TryGetFDA(semantic);
    [<Extension>] static member CsSetFDA(tag:IPlcTag, optFDA:PlcTagBaseFDA option) = tag.SetFDA(optFDA);
    [<Extension>] static member CsGetName(tag:IPlcTag) = tag.GetName();
    [<Extension>] static member CsIsValid(tag:IPlcTag) = tag.IsValid();
    [<Extension>] static member CsGetTagType(vendor:Vendor):Type = vendor.GetVendorTagType()
