namespace Dual.Plc2DS

open System.Runtime.CompilerServices

type Plc2DsExtensionForCSharp =
    [<Extension>] static member CsTryGetFDA(tag:IPlcTag, semantic:Semantic) = tag.TryGetFDA(semantic);
    [<Extension>] static member CsSetFDA(tag:IPlcTag, optFDA:FDA option) = tag.SetFDA(optFDA);
    [<Extension>] static member CsGetName(tag:IPlcTag) = tag.GetName();
