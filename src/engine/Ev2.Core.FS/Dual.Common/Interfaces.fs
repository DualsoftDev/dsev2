namespace Dual.Ev2

open System
open Dual.Common.Base.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module InterfacesModule =
    type IWithType = interface end
    type IWithType<'T> =
        inherit IWithType

    type IExpression =
        inherit IWithType
        inherit IWithName

    type IExpression<'T> =
        inherit IExpression
        inherit IWithType<'T>

[<AutoOpen>]
module ReflectionInterfacesModule =
    type IWithType with
        /// IWithType.Type (no setter)
        member x.Type = getPropertyValueDynmaically(x, "Type") :?> Type
        member x.DataType = x.Type  // 임시
    type IWithAddress with
        /// IWithAddress.Address
        member x.Address
            with get() = tryGetStringPropertyDynmaically(x, "Address") |? null
            and set (v:string) = setPropertyValueDynmaically(x, "Address", v)

    type IWithComment with
        /// IWithComment.Comment
        member x.Comment
            with get() = tryGetStringPropertyDynmaically(x, "Comment") |? null
            and set (v:string) = setPropertyValueDynmaically(x, "Comment", v)

